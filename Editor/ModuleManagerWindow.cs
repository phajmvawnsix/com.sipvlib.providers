using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SiPVLib.Providers.Editor
{
    /// <summary>
    /// Lets the user install/update/remove SiPVLib packages (<c>Assets/SiPVLib/&lt;module-id&gt;</c>)
    /// via git, including pulling in a module's missing dependencies automatically on install.
    /// </summary>
    public class ModuleManagerWindow : EditorWindow
    {
        private Vector2 _scroll;

        [MenuItem("SiPV/Modules")]
        public static void Open()
        {
            var window = GetWindow<ModuleManagerWindow>();
            window.titleContent = new GUIContent("SiPV Modules");
            window.minSize = new Vector2(480, 320);
        }

        private void OnEnable()
        {
            ModuleManagerService.Changed += Repaint;
        }

        private void OnDisable()
        {
            ModuleManagerService.Changed -= Repaint;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Install a module to clone it (and any missing dependencies) into Assets/SiPVLib. " +
                "Update pulls the latest commit on the module's current branch. Remove deletes the " +
                "folder and refuses if another installed module still depends on it.", MessageType.Info);

            EditorGUILayout.Space(8);

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

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            var statusLabel = busy ? "Working..." : installed ? "Installed" : "Not installed";
            EditorGUILayout.LabelField($"{module.DisplayName}  —  {statusLabel}", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(busy))
            {
                if (!installed)
                {
                    var canInstall = ModuleManagerService.AreDependenciesInstallable(module);
                    using (new EditorGUI.DisabledScope(!canInstall))
                    {
                        if (GUILayout.Button("Install", GUILayout.Width(80)))
                        {
                            var missingDeps = module.DependsOnModuleIds
                                .Select(ModuleRegistry.Find)
                                .Where(d => d != null && !ModuleManagerService.IsModuleInstalled(d))
                                .Select(d => d.DisplayName)
                                .ToArray();
                            var message = missingDeps.Length > 0
                                ? $"Install {module.DisplayName}? This will also clone its missing dependencies: {string.Join(", ", missingDeps)}."
                                : $"Install {module.DisplayName}?";

                            if (EditorUtility.DisplayDialog("Install module", message, "Install", "Cancel"))
                            {
                                ModuleManagerService.InstallModule(module);
                            }
                        }
                    }
                }
                else
                {
                    if (GUILayout.Button("Update", GUILayout.Width(80)))
                    {
                        ModuleManagerService.UpdateModule(module);
                    }

                    if (GUILayout.Button("Remove", GUILayout.Width(80)))
                    {
                        if (EditorUtility.DisplayDialog("Remove module",
                                $"Remove {module.DisplayName}? This deletes Assets/SiPVLib/{module.Id}.", "Remove", "Cancel"))
                        {
                            ModuleManagerService.RemoveModule(module);
                        }
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

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
                EditorGUILayout.HelpBox("One or more dependencies has no git URL and must be installed manually first.", MessageType.Warning);
            }

            if (!string.IsNullOrEmpty(module.Notes))
            {
                EditorGUILayout.LabelField(module.Notes, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }
    }
}
