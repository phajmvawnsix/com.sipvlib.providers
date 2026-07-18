using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SiPVLib.Providers.Editor
{
    /// <summary>
    /// Lets the user add/remove/enable third-party providers (ad networks, storage backends)
    /// supported by SiPVLib. Providers not installed here have their related code excluded via
    /// scripting defines, so unused SDKs never end up in a build.
    /// </summary>
    public class ProviderManagerWindow : EditorWindow
    {
        private Vector2 _scroll;

        [MenuItem("SiPV/Providers")]
        public static void Open()
        {
            var window = GetWindow<ProviderManagerWindow>();
            window.titleContent = new GUIContent("SiPV Providers");
            window.minSize = new Vector2(480, 320);
        }

        private void OnEnable()
        {
            ProviderManagerService.Changed += Repaint;
        }

        private void OnDisable()
        {
            ProviderManagerService.Changed -= Repaint;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Add a provider to install its package (where available) and enable its code. " +
                "Removing a provider uninstalls its package and hides its code again via scripting " +
                "defines.", MessageType.Info);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var category in System.Enum.GetValues(typeof(ProviderCategory)).Cast<ProviderCategory>())
            {
                DrawCategory(category);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawCategory(ProviderCategory category)
        {
            var providers = ProviderRegistry.All.Where(p => p.Category == category).ToArray();
            if (providers.Length == 0) return;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(category.ToString(), EditorStyles.boldLabel);

            foreach (var provider in providers)
            {
                DrawProvider(provider);
            }
        }

        private void DrawProvider(ProviderDefinition provider)
        {
            var installed = ProviderManagerService.IsProviderInstalled(provider);
            var dependenciesSatisfied = ProviderManagerService.AreDependenciesSatisfied(provider);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            var statusLabel = installed ? "Installed" : "Not installed";
            EditorGUILayout.LabelField($"{provider.DisplayName}  —  {statusLabel}", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(!installed && !dependenciesSatisfied))
            {
                if (!installed)
                {
                    if (GUILayout.Button("Install", GUILayout.Width(80)))
                    {
                        var version = string.IsNullOrEmpty(provider.PackageVersion) ? "" : $" {provider.PackageVersion}";
                        if (EditorUtility.DisplayDialog(
                                "Install provider",
                                $"Install {provider.DisplayName}{version}? This may modify Packages/manifest.json and Player Settings scripting defines.",
                                "Install", "Cancel"))
                        {
                            ProviderManagerService.InstallProvider(provider);
                        }
                    }
                }
                else
                {
                    if (GUILayout.Button("Remove", GUILayout.Width(80)))
                    {
                        if (EditorUtility.DisplayDialog(
                                "Remove provider",
                                $"Remove {provider.DisplayName}? This may modify Packages/manifest.json and Player Settings scripting defines.",
                                "Remove", "Cancel"))
                        {
                            ProviderManagerService.RemoveProvider(provider);
                        }
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

            if (!installed && !dependenciesSatisfied)
            {
                var missing = string.Join(", ", provider.DependsOnProviderIds
                    .Select(ProviderRegistry.Find)
                    .Where(d => d != null)
                    .Select(d => d.DisplayName));
                EditorGUILayout.HelpBox($"Requires: {missing}", MessageType.Warning);
            }

            if (!string.IsNullOrEmpty(provider.InstallNotes))
            {
                EditorGUILayout.LabelField(provider.InstallNotes, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }
    }
}
