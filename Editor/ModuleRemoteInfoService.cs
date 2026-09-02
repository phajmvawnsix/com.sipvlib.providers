using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SiPVLib.Debugging;
using UnityEditor;
using UnityEngine.Networking;

namespace SiPVLib.Providers.Editor
{
    /// <summary>
    /// Fetches each module's published version and CHANGELOG.md from GitHub. The version of record
    /// is the <c>version</c> field of <c>package.json</c> on the default branch — SiPVLib packages
    /// are consumed as git dependencies tracking that branch and are not necessarily tagged, so a
    /// tag lookup would under-report (or miss entirely) the current release. Everything is polled
    /// off <see cref="EditorApplication.update"/>, so nothing here blocks the Editor's main thread.
    /// </summary>
    [InitializeOnLoad]
    public static class ModuleRemoteInfoService
    {
        private const string RawContentBaseUrl = "https://raw.githubusercontent.com/phajmvawnsix/";
        private const string DefaultBranch = "main";

        private static readonly Regex VersionPattern =
            new(@"""version""\s*:\s*""(?<version>[^""]+)""", RegexOptions.Compiled);

        private static readonly Dictionary<string, string> RemoteVersions = new();
        private static readonly Dictionary<string, string> Changelogs = new();
        private static readonly HashSet<string> InFlight = new();

        private static readonly List<VersionQuery> PendingVersionQueries = new();
        private static readonly List<ChangelogQuery> PendingChangelogQueries = new();

        public static event Action Changed;

        static ModuleRemoteInfoService()
        {
            EditorApplication.update += Poll;
        }

        // ── Remote version ───────────────────────────────────────────────

        /// <summary>
        /// Published version for a module, or null while unknown. The first call per module kicks
        /// off a fetch of <c>package.json</c> from the default branch; <see cref="Changed"/> fires
        /// when it lands.
        /// </summary>
        public static string GetRemoteVersion(ModuleDefinition module)
        {
            if (RemoteVersions.TryGetValue(module.Id, out var version)) return version;

            RequestRemoteVersion(module);
            return null;
        }

        public static bool IsFetching(ModuleDefinition module) => InFlight.Contains(module.Id);

        public static void RequestRemoteVersion(ModuleDefinition module)
        {
            if (string.IsNullOrEmpty(module.GitUrl)) return;
            if (RemoteVersions.ContainsKey(module.Id) || InFlight.Contains(module.Id)) return;

            var url = $"{RawContentBaseUrl}{module.Id}/{DefaultBranch}/package.json";
            var request = UnityWebRequest.Get(url);
            request.SendWebRequest();

            PendingVersionQueries.Add(new VersionQuery(module.Id, request));
            InFlight.Add(module.Id);
        }

        /// <summary>Drops all cached remote data so the next query re-fetches.</summary>
        public static void InvalidateCache()
        {
            RemoteVersions.Clear();
            Changelogs.Clear();
            Changed?.Invoke();
        }

        // ── Changelog ────────────────────────────────────────────────────

        /// <summary>Cached CHANGELOG.md for a module's remote version, or null while unknown.</summary>
        public static string GetChangelog(ModuleDefinition module) =>
            Changelogs.GetValueOrDefault(module.Id);

        public static void RequestChangelog(ModuleDefinition module)
        {
            if (Changelogs.ContainsKey(module.Id)) return;
            if (PendingChangelogQueries.Exists(q => q.ModuleId == module.Id)) return;

            var url = $"{RawContentBaseUrl}{module.Id}/{DefaultBranch}/CHANGELOG.md";

            var request = UnityWebRequest.Get(url);
            request.SendWebRequest();
            PendingChangelogQueries.Add(new ChangelogQuery(module.Id, request));
        }

        // ── Polling ──────────────────────────────────────────────────────

        private static void Poll()
        {
            PollVersionQueries();
            PollChangelogQueries();
        }

        private static void PollVersionQueries()
        {
            if (PendingVersionQueries.Count == 0) return;

            for (var i = PendingVersionQueries.Count - 1; i >= 0; i--)
            {
                var query = PendingVersionQueries[i];
                if (!query.Request.isDone) continue;

                var version = string.Empty;
                if (query.Request.result == UnityWebRequest.Result.Success)
                {
                    version = ParseVersion(query.Request.downloadHandler.text);
                }
                else
                {
                    CustomLog.LogWarning($"[SiPV.Modules] Could not read package.json for {query.ModuleId}: {query.Request.error}");
                }

                query.Request.Dispose();
                PendingVersionQueries.RemoveAt(i);
                InFlight.Remove(query.ModuleId);

                RemoteVersions[query.ModuleId] = version;
                Changed?.Invoke();
            }
        }

        private static void PollChangelogQueries()
        {
            if (PendingChangelogQueries.Count == 0) return;

            for (var i = PendingChangelogQueries.Count - 1; i >= 0; i--)
            {
                var query = PendingChangelogQueries[i];
                if (!query.Request.isDone) continue;

                Changelogs[query.ModuleId] = query.Request.result == UnityWebRequest.Result.Success
                    ? query.Request.downloadHandler.text
                    : $"Failed to fetch changelog: {query.Request.error}";

                query.Request.Dispose();
                PendingChangelogQueries.RemoveAt(i);
                Changed?.Invoke();
            }
        }

        /// <summary>Reads the <c>version</c> field out of a package.json payload.</summary>
        private static string ParseVersion(string packageJson)
        {
            var match = VersionPattern.Match(packageJson);
            return match.Success ? match.Groups["version"].Value : string.Empty;
        }

        /// <summary>Compares two version strings, tolerating a leading "v" and unparseable input.</summary>
        public static bool IsNewer(string candidate, string current)
        {
            if (string.IsNullOrEmpty(candidate)) return false;
            if (string.IsNullOrEmpty(current)) return true;

            return Version.TryParse(candidate.TrimStart('v'), out var candidateVersion)
                   && Version.TryParse(current.TrimStart('v'), out var currentVersion)
                   && candidateVersion > currentVersion;
        }

        private class VersionQuery
        {
            public readonly string ModuleId;
            public readonly UnityWebRequest Request;

            public VersionQuery(string moduleId, UnityWebRequest request)
            {
                ModuleId = moduleId;
                Request = request;
            }
        }

        private class ChangelogQuery
        {
            public readonly string ModuleId;
            public readonly UnityWebRequest Request;

            public ChangelogQuery(string moduleId, UnityWebRequest request)
            {
                ModuleId = moduleId;
                Request = request;
            }
        }
    }
}
