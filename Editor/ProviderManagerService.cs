using System;
using System.Collections.Generic;
using System.Linq;
using SiPVLib.Debugging;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace SiPVLib.Providers.Editor
{
    /// <summary>
    /// Installs/removes provider packages via Package Manager and toggles the scripting define
    /// symbols that don't already get derived automatically via an asmdef's own versionDefines.
    /// </summary>
    [InitializeOnLoad]
    public static class ProviderManagerService
    {
        private static readonly List<Request> PendingRequests = new();
        private static readonly HashSet<string> EmptyPackageIds = new();
        private static HashSet<string> _installedPackageIdsCache;
        private static ListRequest _pendingListRequest;

        public static event Action Changed;

        static ProviderManagerService()
        {
            EditorApplication.update += PollPendingRequests;
        }

        // ── Status ───────────────────────────────────────────────────

        public static bool IsPackageInstalled(string packageId)
        {
            if (string.IsNullOrEmpty(packageId)) return false;
            return GetInstalledPackageIds().Contains(packageId);
        }

        public static bool IsDefineEnabled(string defineSymbol)
        {
            if (string.IsNullOrEmpty(defineSymbol)) return false;
            return GetCurrentDefines().Contains(defineSymbol);
        }

        /// <summary>
        /// A provider counts as "installed" if its package is present, or - for define-only
        /// providers with no package of their own - if its define symbol is enabled.
        /// </summary>
        public static bool IsProviderInstalled(ProviderDefinition provider)
        {
            if (!string.IsNullOrEmpty(provider.PackageId))
            {
                return IsPackageInstalled(provider.PackageId);
            }

            return !string.IsNullOrEmpty(provider.ScriptingDefineSymbol) && IsDefineEnabled(provider.ScriptingDefineSymbol);
        }

        public static bool AreDependenciesSatisfied(ProviderDefinition provider)
        {
            foreach (var dependencyId in provider.DependsOnProviderIds)
            {
                var dependency = ProviderRegistry.Find(dependencyId);
                if (dependency == null || !IsProviderInstalled(dependency)) return false;
            }

            return true;
        }

        // ── Actions ──────────────────────────────────────────────────

        public static void InstallProvider(ProviderDefinition provider)
        {
            if (!string.IsNullOrEmpty(provider.PackageId))
            {
                var version = string.IsNullOrEmpty(provider.PackageVersion)
                    ? provider.PackageId
                    : $"{provider.PackageId}@{provider.PackageVersion}";
                CustomLog.Log($"[SiPV.Providers] Adding package {version}...");
                Track(Client.Add(version));
            }

            if (!string.IsNullOrEmpty(provider.ScriptingDefineSymbol))
            {
                AddDefine(provider.ScriptingDefineSymbol);
            }
        }

        public static void RemoveProvider(ProviderDefinition provider)
        {
            if (!string.IsNullOrEmpty(provider.PackageId) && IsPackageInstalled(provider.PackageId))
            {
                CustomLog.Log($"[SiPV.Providers] Removing package {provider.PackageId}...");
                Track(Client.Remove(provider.PackageId));
            }

            if (!string.IsNullOrEmpty(provider.ScriptingDefineSymbol))
            {
                RemoveDefine(provider.ScriptingDefineSymbol);
            }
        }

        // ── Scripting defines ────────────────────────────────────────

        private static NamedBuildTarget CurrentNamedBuildTarget =>
            NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);

        private static HashSet<string> GetCurrentDefines()
        {
            PlayerSettings.GetScriptingDefineSymbols(CurrentNamedBuildTarget, out var defines);
            return new HashSet<string>(defines);
        }

        private static void AddDefine(string symbol)
        {
            var defines = GetCurrentDefines();
            if (!defines.Add(symbol)) return;

            PlayerSettings.SetScriptingDefineSymbols(CurrentNamedBuildTarget, defines.ToArray());
            CustomLog.Log($"[SiPV.Providers] Added scripting define {symbol}.");
            InvalidateCache();
        }

        private static void RemoveDefine(string symbol)
        {
            var defines = GetCurrentDefines();
            if (!defines.Remove(symbol)) return;

            PlayerSettings.SetScriptingDefineSymbols(CurrentNamedBuildTarget, defines.ToArray());
            CustomLog.Log($"[SiPV.Providers] Removed scripting define {symbol}.");
            InvalidateCache();
        }

        // ── Package cache ────────────────────────────────────────────

        /// <summary>
        /// Returns the installed package ids, or an empty set while the first (or a refreshed)
        /// Package Manager listing is still in flight. Reached from OnGUI, so this never blocks the
        /// main thread: the pending <see cref="ListRequest"/> is polled in <see cref="PollPendingRequests"/>
        /// and <see cref="Changed"/> fires to repaint once it lands.
        /// </summary>
        private static HashSet<string> GetInstalledPackageIds()
        {
            if (_installedPackageIdsCache != null) return _installedPackageIdsCache;

            if (_pendingListRequest == null)
            {
                _pendingListRequest = Client.List(true);
            }

            return EmptyPackageIds;
        }

        /// <summary>True while the package listing hasn't resolved yet; lets the UI say so.</summary>
        public static bool IsRefreshingPackages => _installedPackageIdsCache == null;

        private static void PollListRequest()
        {
            if (_pendingListRequest == null || !_pendingListRequest.IsCompleted) return;

            var completed = _pendingListRequest;
            _pendingListRequest = null;

            var ids = new HashSet<string>();
            if (completed.Status == StatusCode.Success)
            {
                foreach (var package in completed.Result)
                {
                    ids.Add(package.name);
                }
            }
            else
            {
                CustomLog.LogError($"[SiPV.Providers] Failed to list packages: {completed.Error?.message}");
            }

            _installedPackageIdsCache = ids;
            Changed?.Invoke();
        }

        private static void InvalidateCache()
        {
            _installedPackageIdsCache = null;
            Changed?.Invoke();
        }

        // ── Request polling ──────────────────────────────────────────

        private static void Track(Request request)
        {
            PendingRequests.Add(request);
        }

        private static void PollPendingRequests()
        {
            PollListRequest();

            if (PendingRequests.Count == 0) return;

            for (var i = PendingRequests.Count - 1; i >= 0; i--)
            {
                var request = PendingRequests[i];
                if (!request.IsCompleted) continue;

                if (request.Status == StatusCode.Failure)
                {
                    CustomLog.LogError($"[SiPV.Providers] Package Manager request failed: {request.Error?.message}");
                }

                PendingRequests.RemoveAt(i);
                InvalidateCache();
            }
        }
    }
}
