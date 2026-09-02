using System;
using System.Collections.Generic;
using System.Linq;
using SiPVLib.Debugging;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace SiPVLib.Providers.Editor
{
    /// <summary>
    /// Installs/updates/removes SiPVLib packages as UPM git dependencies in the project's
    /// <c>Packages/manifest.json</c>, via Package Manager. A consuming project pulls SiPVLib in
    /// through the manifest, so the manifest — not any folder under Assets — decides what's installed.
    /// </summary>
    [InitializeOnLoad]
    public static class ModuleManagerService
    {
        private static readonly List<ModuleOperation> PendingOperations = new();

        /// <summary>Manifest dependency map, re-read only when invalidated (the window queries per repaint).</summary>
        private static Dictionary<string, string> _manifestCache;

        /// <summary>Resolved package versions from Package Manager, keyed by package id.</summary>
        private static Dictionary<string, string> _installedVersions;
        private static ListRequest _pendingListRequest;

        public static event Action Changed;

        static ModuleManagerService()
        {
            EditorApplication.update += PollPendingRequests;
        }

        // ── Status ───────────────────────────────────────────────────────

        private static Dictionary<string, string> Manifest =>
            _manifestCache ??= ModuleManifest.ReadDependencies();

        public static bool IsModuleInstalled(ModuleDefinition module) =>
            Manifest.ContainsKey(module.Id);

        public static bool IsBusy(ModuleDefinition module) =>
            PendingOperations.Any(op => op.ModuleId == module.Id);

        /// <summary>The manifest value (git URL, possibly <c>#tag</c>-pinned) for an installed module.</summary>
        public static string GetManifestEntry(ModuleDefinition module) =>
            Manifest.GetValueOrDefault(module.Id);

        /// <summary>
        /// The module's currently installed version: the resolved package version where Package
        /// Manager has reported one, otherwise the <c>#tag</c> the manifest pins, otherwise null
        /// (installed but tracking the default branch, with the listing still in flight).
        /// </summary>
        public static string GetLocalVersion(ModuleDefinition module)
        {
            if (!IsModuleInstalled(module)) return null;

            EnsureVersionsRequested();

            if (_installedVersions != null && _installedVersions.TryGetValue(module.Id, out var resolved))
            {
                return resolved;
            }

            return ModuleManifest.GetPinnedFragment(GetManifestEntry(module));
        }

        public static bool IsResolvingVersions => _installedVersions == null;

        private static void EnsureVersionsRequested()
        {
            if (_installedVersions != null || _pendingListRequest != null) return;

            _pendingListRequest = Client.List(true);
        }

        public static bool AreDependenciesInstallable(ModuleDefinition module)
        {
            foreach (var dependencyId in module.DependsOnModuleIds)
            {
                var dependency = ModuleRegistry.Find(dependencyId);
                if (dependency == null || string.IsNullOrEmpty(dependency.GitUrl)) return false;
            }

            return true;
        }

        /// <summary>Drops cached manifest/version state so the next query re-reads it.</summary>
        public static void InvalidateCache()
        {
            _manifestCache = null;
            _installedVersions = null;
            Changed?.Invoke();
        }

        // ── Actions ──────────────────────────────────────────────────────

        /// <summary>
        /// Adds this module and any not-yet-installed dependencies to the manifest, foundation
        /// packages first. Pins to <paramref name="version"/> when given, else tracks the default branch.
        /// </summary>
        public static void InstallModule(ModuleDefinition module, string version = null)
        {
            foreach (var toInstall in ResolveInstallOrder(module))
            {
                if (IsModuleInstalled(toInstall) || IsBusy(toInstall)) continue;

                // Only the explicitly requested module gets pinned; dependencies track their default
                // branch, since a version valid for one package says nothing about another's tags.
                var pinned = ReferenceEquals(toInstall, module) ? version : null;
                AddToManifest(toInstall, pinned);
            }
        }

        /// <summary>Re-points the manifest entry at <paramref name="version"/> (or the default branch).</summary>
        public static void UpdateModule(ModuleDefinition module, string version = null)
        {
            if (!IsModuleInstalled(module) || IsBusy(module)) return;

            AddToManifest(module, version);
        }

        public static void RemoveModule(ModuleDefinition module)
        {
            if (!IsModuleInstalled(module) || IsBusy(module)) return;

            var dependents = ModuleRegistry.All
                .Where(m => IsModuleInstalled(m) && m.DependsOnModuleIds.Contains(module.Id))
                .ToArray();

            if (dependents.Length > 0)
            {
                var names = string.Join(", ", dependents.Select(d => d.DisplayName));
                EditorUtility.DisplayDialog("Cannot remove module",
                    $"{module.DisplayName} is still required by: {names}. Remove those first.", "OK");
                return;
            }

            CustomLog.Log($"[SiPV.Modules] Removing {module.DisplayName}...");
            Track(module.Id, Client.Remove(module.Id));
        }

        private static void AddToManifest(ModuleDefinition module, string version)
        {
            if (string.IsNullOrEmpty(module.GitUrl))
            {
                CustomLog.LogError($"[SiPV.Modules] {module.DisplayName} has no git URL; add it to the manifest manually.");
                return;
            }

            var url = ModuleManifest.BuildGitUrl(module, version);
            CustomLog.Log($"[SiPV.Modules] Adding {module.DisplayName} ({url})...");
            Track(module.Id, Client.Add(url));
        }

        // ── Install ordering ─────────────────────────────────────────────

        private static List<ModuleDefinition> ResolveInstallOrder(ModuleDefinition module)
        {
            var order = new List<ModuleDefinition>();
            var seen = new HashSet<string>();
            Visit(module);
            return order;

            void Visit(ModuleDefinition current)
            {
                if (!seen.Add(current.Id)) return;

                foreach (var dependencyId in current.DependsOnModuleIds)
                {
                    var dependency = ModuleRegistry.Find(dependencyId);
                    if (dependency != null) Visit(dependency);
                }

                order.Add(current);
            }
        }

        // ── Request polling ──────────────────────────────────────────────

        private static void Track(string moduleId, Request request)
        {
            PendingOperations.Add(new ModuleOperation(moduleId, request));
            Changed?.Invoke();
        }

        private static void PollPendingRequests()
        {
            PollListRequest();

            if (PendingOperations.Count == 0) return;

            for (var i = PendingOperations.Count - 1; i >= 0; i--)
            {
                var operation = PendingOperations[i];
                if (!operation.Request.IsCompleted) continue;

                if (operation.Request.Status == StatusCode.Failure)
                {
                    CustomLog.LogError($"[SiPV.Modules] Package Manager request failed for {operation.ModuleId}: {operation.Request.Error?.message}");
                }

                PendingOperations.RemoveAt(i);
                InvalidateCache();
            }
        }

        private static void PollListRequest()
        {
            if (_pendingListRequest == null || !_pendingListRequest.IsCompleted) return;

            var completed = _pendingListRequest;
            _pendingListRequest = null;

            var versions = new Dictionary<string, string>(StringComparer.Ordinal);
            if (completed.Status == StatusCode.Success)
            {
                foreach (var package in completed.Result)
                {
                    versions[package.name] = package.version;
                }
            }
            else
            {
                CustomLog.LogError($"[SiPV.Modules] Failed to list packages: {completed.Error?.message}");
            }

            _installedVersions = versions;
            Changed?.Invoke();
        }

        private class ModuleOperation
        {
            public readonly string ModuleId;
            public readonly Request Request;

            public ModuleOperation(string moduleId, Request request)
            {
                ModuleId = moduleId;
                Request = request;
            }
        }
    }
}
