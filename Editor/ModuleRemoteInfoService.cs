using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using SiPVLib.Debugging;
using UnityEditor;
using UnityEngine.Networking;

namespace SiPVLib.Providers.Editor
{
    /// <summary>
    /// Fetches each module's latest published version (highest semver git tag) and that version's
    /// CHANGELOG.md from GitHub. Everything is polled off <see cref="EditorApplication.update"/>
    /// rather than awaited, so nothing here blocks the Editor's main thread.
    /// </summary>
    [InitializeOnLoad]
    public static class ModuleRemoteInfoService
    {
        private const string RawContentBaseUrl = "https://raw.githubusercontent.com/phajmvawnsix/";

        private static readonly Regex TagPattern =
            new(@"refs/tags/(?<tag>v?\d+\.\d+\.\d+)(?:\^\{\})?$", RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Dictionary<string, string> RemoteVersions = new();
        private static readonly Dictionary<string, string> Changelogs = new();
        private static readonly HashSet<string> InFlight = new();

        private static readonly List<TagQuery> PendingTagQueries = new();
        private static readonly List<ChangelogQuery> PendingChangelogQueries = new();

        public static event Action Changed;

        static ModuleRemoteInfoService()
        {
            EditorApplication.update += Poll;
        }

        // ── Remote version ───────────────────────────────────────────────

        /// <summary>
        /// Latest published version for a module, or null while unknown. The first call per module
        /// kicks off a background <c>git ls-remote --tags</c>; <see cref="Changed"/> fires when it lands.
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

            var startInfo = new ProcessStartInfo("git", $"ls-remote --tags \"{module.GitUrl}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            try
            {
                var process = Process.Start(startInfo);
                PendingTagQueries.Add(new TagQuery(module.Id, process));
                InFlight.Add(module.Id);
            }
            catch (Exception e)
            {
                CustomLog.LogError($"[SiPV.Modules] Failed to query tags for {module.DisplayName}: {e.Message}");
            }
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

            // Tags are published as either "1.2.0" or "v1.2.0"; the raw-content path needs whichever
            // one actually exists, so fall back to the default branch when no tag is known yet.
            var version = GetRemoteVersion(module);
            var reference = string.IsNullOrEmpty(version) ? "main" : version;
            var url = $"{RawContentBaseUrl}{module.Id}/{reference}/CHANGELOG.md";

            var request = UnityWebRequest.Get(url);
            request.SendWebRequest();
            PendingChangelogQueries.Add(new ChangelogQuery(module.Id, request));
        }

        // ── Polling ──────────────────────────────────────────────────────

        private static void Poll()
        {
            PollTagQueries();
            PollChangelogQueries();
        }

        private static void PollTagQueries()
        {
            if (PendingTagQueries.Count == 0) return;

            for (var i = PendingTagQueries.Count - 1; i >= 0; i--)
            {
                var query = PendingTagQueries[i];
                if (!query.Process.HasExited) continue;

                var version = string.Empty;
                if (query.Process.ExitCode == 0)
                {
                    version = ParseHighestTag(query.Process.StandardOutput.ReadToEnd());
                }
                else
                {
                    CustomLog.LogWarning($"[SiPV.Modules] Could not read tags for {query.ModuleId}: {query.Process.StandardError.ReadToEnd()}");
                }

                query.Process.Dispose();
                PendingTagQueries.RemoveAt(i);
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

        /// <summary>Picks the highest semver tag out of <c>git ls-remote --tags</c> output.</summary>
        private static string ParseHighestTag(string lsRemoteOutput)
        {
            string best = null;
            var bestParsed = new Version(0, 0, 0);

            foreach (Match match in TagPattern.Matches(lsRemoteOutput))
            {
                var tag = match.Groups["tag"].Value;
                var numeric = tag.TrimStart('v');

                if (!Version.TryParse(numeric, out var parsed)) continue;
                if (best != null && parsed <= bestParsed) continue;

                best = tag;
                bestParsed = parsed;
            }

            return best ?? string.Empty;
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

        private class TagQuery
        {
            public readonly string ModuleId;
            public readonly Process Process;

            public TagQuery(string moduleId, Process process)
            {
                ModuleId = moduleId;
                Process = process;
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
