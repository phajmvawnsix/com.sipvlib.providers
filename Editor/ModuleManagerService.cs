using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SiPVLib.Debugging;
using UnityEditor;

namespace SiPVLib.Providers.Editor
{
    /// <summary>
    /// Installs/updates/removes SiPVLib packages (<c>Assets/SiPVLib/&lt;module-id&gt;</c>) by shelling
    /// out to <c>git</c> directly, since these are independent git repos cloned into the project, not
    /// UPM registry packages resolvable via Package Manager's <c>Client</c> API.
    /// </summary>
    [InitializeOnLoad]
    public static class ModuleManagerService
    {
        private static readonly List<ModuleOperation> PendingOperations = new();

        public static event Action Changed;

        static ModuleManagerService()
        {
            EditorApplication.update += PollPendingOperations;
        }

        // ── Paths / status ──────────────────────────────────────────────

        private static string ModulesRootPath =>
            Path.Combine(UnityEngine.Application.dataPath, "SiPVLib");

        public static string GetModulePath(ModuleDefinition module) =>
            Path.Combine(ModulesRootPath, module.Id);

        public static bool IsModuleInstalled(ModuleDefinition module) =>
            Directory.Exists(GetModulePath(module));

        public static bool IsBusy(ModuleDefinition module) =>
            PendingOperations.Any(op => op.ModuleId == module.Id);

        public static bool AreDependenciesInstallable(ModuleDefinition module)
        {
            foreach (var dependencyId in module.DependsOnModuleIds)
            {
                var dependency = ModuleRegistry.Find(dependencyId);
                if (dependency == null || string.IsNullOrEmpty(dependency.GitUrl)) return false;
            }

            return true;
        }

        // ── Actions ──────────────────────────────────────────────────────

        /// <summary>Installs this module and any missing dependencies, closest-to-foundation first.</summary>
        public static void InstallModule(ModuleDefinition module)
        {
            foreach (var toInstall in ResolveInstallOrder(module))
            {
                InstallSingle(toInstall);
            }
        }

        public static void UpdateModule(ModuleDefinition module)
        {
            if (!IsModuleInstalled(module) || IsBusy(module)) return;

            RunGit(module.Id, "pull", GetModulePath(module), success =>
            {
                CustomLog.Log(success
                    ? $"[SiPV.Modules] Updated {module.DisplayName}."
                    : $"[SiPV.Modules] Failed to update {module.DisplayName}. See console for git output.");
                AssetDatabase.Refresh();
            });
        }

        /// <summary>Deletes the module's folder. Refuses if another installed module still depends on it.</summary>
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

            var path = GetModulePath(module);
            Directory.Delete(path, true);

            var metaPath = path + ".meta";
            if (File.Exists(metaPath)) File.Delete(metaPath);

            CustomLog.Log($"[SiPV.Modules] Removed {module.DisplayName}.");
            AssetDatabase.Refresh();
            Changed?.Invoke();
        }

        // ── Install ordering ────────────────────────────────────────────

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

        private static void InstallSingle(ModuleDefinition module)
        {
            if (IsModuleInstalled(module) || IsBusy(module)) return;

            if (string.IsNullOrEmpty(module.GitUrl))
            {
                CustomLog.LogError($"[SiPV.Modules] {module.DisplayName} has no git URL; install it manually.");
                return;
            }

            Directory.CreateDirectory(ModulesRootPath);
            var arguments = $"clone \"{module.GitUrl}\" \"{GetModulePath(module)}\"";

            RunGit(module.Id, arguments, ModulesRootPath, success =>
            {
                CustomLog.Log(success
                    ? $"[SiPV.Modules] Installed {module.DisplayName}."
                    : $"[SiPV.Modules] Failed to install {module.DisplayName}. See console for git output.");
                AssetDatabase.Refresh();
            });
        }

        // ── git process handling ────────────────────────────────────────

        private static void RunGit(string moduleId, string arguments, string workingDirectory, Action<bool> onComplete)
        {
            var startInfo = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            Process process;
            try
            {
                process = Process.Start(startInfo);
            }
            catch (Exception e)
            {
                CustomLog.LogError($"[SiPV.Modules] Failed to start git: {e.Message}");
                onComplete?.Invoke(false);
                return;
            }

            PendingOperations.Add(new ModuleOperation(moduleId, process, onComplete));
            Changed?.Invoke();
        }

        private static void PollPendingOperations()
        {
            if (PendingOperations.Count == 0) return;

            for (var i = PendingOperations.Count - 1; i >= 0; i--)
            {
                var operation = PendingOperations[i];
                if (!operation.Process.HasExited) continue;

                var success = operation.Process.ExitCode == 0;
                if (!success)
                {
                    var error = operation.Process.StandardError.ReadToEnd();
                    CustomLog.LogError($"[SiPV.Modules] git failed for {operation.ModuleId}: {error}");
                }

                operation.Process.Dispose();
                PendingOperations.RemoveAt(i);
                operation.OnComplete?.Invoke(success);
                Changed?.Invoke();
            }
        }

        private class ModuleOperation
        {
            public readonly string ModuleId;
            public readonly Process Process;
            public readonly Action<bool> OnComplete;

            public ModuleOperation(string moduleId, Process process, Action<bool> onComplete)
            {
                ModuleId = moduleId;
                Process = process;
                OnComplete = onComplete;
            }
        }
    }
}
