using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SiPVLib.Providers.Editor
{
    /// <summary>
    /// Lets the user install/update/remove SiPVLib packages as UPM git dependencies in
    /// <c>Packages/manifest.json</c>, comparing the installed version against the latest published
    /// GitHub release and linking out to that release's changelog.
    /// </summary>
    public class ModuleManagerWindow : EditorWindow
    {
        private Vector2 _scroll;

        [MenuItem("SiPV/Modules")]
        public static void Open()
        {
            var window = GetWindow<ModuleManagerWindow>();
            window.titleContent = new GUIContent("SiPV Modules");
            window.minSize = new Vector2(620, 360);
        }

        private void OnEnable()
        {
            ModuleManagerService.Changed += Repaint;
            ModuleRemoteInfoService.Changed += Repaint;

            // The manifest can change outside this window (a hand edit, a git pull), so re-read it
            // whenever the window is opened.
            ModuleManagerService.InvalidateCache();
        }

        private void OnDisable()
        {
            ModuleManagerService.Changed -= Repaint;
            ModuleRemoteInfoService.Changed -= Repaint;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "SiPVLib modules are UPM git dependencies in Packages/manifest.json. Install adds a " +
                "module (and any missing dependencies) to the manifest, Update re-points it at the " +
                "latest published release, and Remove takes it back out — refusing while another " +
                "installed module still depends on it.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (ModuleManagerService.IsResolvingVersions)
            {
                EditorGUILayout.LabelField("Resolving installed versions...", EditorStyles.miniLabel);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
            {
                ModuleManagerService.InvalidateCache();
                ModuleRemoteInfoService.InvalidateCache();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var module in ModuleRegistry.All)
            {
                DrawModule(module);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawModule(ModuleDefinition module)
        {
            var installed = ModuleManagerService.IsModuleInstalled(module);
            var busy = ModuleManagerService.IsBusy(module);
            var localVersion = ModuleManagerService.GetLocalVersion(module);
            var remoteVersion = ModuleRemoteInfoService.GetRemoteVersion(module);
            var updateAvailable = installed && ModuleRemoteInfoService.IsNewer(remoteVersion, localVersion);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            var statusLabel = busy ? "Working..." : installed ? "Installed" : "Not installed";
            EditorGUILayout.LabelField($"{module.DisplayName}  —  {statusLabel}", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(busy))
            {
                if (!installed)
                {
                    using (new EditorGUI.DisabledScope(!ModuleManagerService.AreDependenciesInstallable(module)))
                    {
                        if (GUILayout.Button("Install", GUILayout.Width(70)))
                        {
                            PromptInstall(module, remoteVersion);
                        }
                    }
                }
                else
                {
                    using (new EditorGUI.DisabledScope(!updateAvailable))
                    {
                        if (GUILayout.Button("Update", GUILayout.Width(70)))
                        {
                            PromptUpdate(module, localVersion, remoteVersion);
                        }
                    }

                    if (GUILayout.Button("Remove", GUILayout.Width(70)))
                    {
                        if (EditorUtility.DisplayDialog("Remove module",
                                $"Remove {module.DisplayName} from Packages/manifest.json?", "Remove", "Cancel"))
                        {
                            ModuleManagerService.RemoveModule(module);
                        }
                    }
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(module.GitUrl)))
                {
                    if (GUILayout.Button("Changelog", GUILayout.Width(80)))
                    {
                        ModuleChangelogWindow.Show(module);
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

            DrawVersionRow(module, installed, localVersion, remoteVersion, updateAvailable);

            if (module.DependsOnModuleIds.Length > 0)
            {
                var depNames = string.Join(", ", module.DependsOnModuleIds
                    .Select(ModuleRegistry.Find)
                    .Where(d => d != null)
                    .Select(d => d.DisplayName));
                EditorGUILayout.LabelField($"Depends on: {depNames}", EditorStyles.miniLabel);
            }

            if (!installed && !ModuleManagerService.AreDependenciesInstallable(module))
            {
                EditorGUILayout.HelpBox("One or more dependencies has no git URL and must be added to the manifest manually.", MessageType.Warning);
            }

            if (!string.IsNullOrEmpty(module.Notes))
            {
                EditorGUILayout.LabelField(module.Notes, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawVersionRow(ModuleDefinition module, bool installed, string localVersion,
            string remoteVersion, bool updateAvailable)
        {
            EditorGUILayout.BeginHorizontal();

            var installedText = !installed
                ? "—"
                : string.IsNullOrEmpty(localVersion) ? "default branch" : localVersion;
            EditorGUILayout.LabelField($"Installed: {installedText}", EditorStyles.miniLabel, GUILayout.Width(190));

            string latestText;
            if (string.IsNullOrEmpty(module.GitUrl)) latestText = "n/a";
            else if (ModuleRemoteInfoService.IsFetching(module) || remoteVersion == null) latestText = "checking...";
            else if (remoteVersion.Length == 0) latestText = "unavailable";
            else latestText = remoteVersion;

            EditorGUILayout.LabelField($"Latest: {latestText}", EditorStyles.miniLabel, GUILayout.Width(190));

            if (updateAvailable)
            {
                var previous = GUI.color;
                GUI.color = new Color(1f, 0.8f, 0.3f);
                EditorGUILayout.LabelField("Update available", EditorStyles.miniBoldLabel);
                GUI.color = previous;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static void PromptInstall(ModuleDefinition module, string remoteVersion)
        {
            var missingDeps = module.DependsOnModuleIds
                .Select(ModuleRegistry.Find)
                .Where(d => d != null && !ModuleManagerService.IsModuleInstalled(d))
                .Select(d => d.DisplayName)
                .ToArray();

            var versionText = string.IsNullOrEmpty(remoteVersion) ? "" : $" {remoteVersion}";
            var message = $"Add {module.DisplayName}{versionText} to Packages/manifest.json?";
            if (missingDeps.Length > 0)
            {
                message += $"\n\nIts missing dependencies will be added too: {string.Join(", ", missingDeps)}.";
            }

            if (EditorUtility.DisplayDialog("Install module", message, "Install", "Cancel"))
            {
                ModuleManagerService.InstallModule(module);
            }
        }

        private static void PromptUpdate(ModuleDefinition module, string localVersion, string remoteVersion)
        {
            var from = string.IsNullOrEmpty(localVersion) ? "default branch" : localVersion;
            if (EditorUtility.DisplayDialog("Update module",
                    $"Update {module.DisplayName} from {from} to {remoteVersion}?", "Update", "Cancel"))
            {
                ModuleManagerService.UpdateModule(module);
            }
        }
    }
}
