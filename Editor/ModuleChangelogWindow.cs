using UnityEditor;
using UnityEngine;

namespace SiPVLib.Providers.Editor
{
    /// <summary>
    /// Shows the CHANGELOG.md published on GitHub for a module's latest release.
    /// </summary>
    public class ModuleChangelogWindow : EditorWindow
    {
        private ModuleDefinition _module;
        private Vector2 _scroll;
        private GUIStyle _bodyStyle;

        public static void Show(ModuleDefinition module)
        {
            var window = CreateInstance<ModuleChangelogWindow>();
            window._module = module;
            window.titleContent = new GUIContent($"{module.DisplayName} Changelog");
            window.minSize = new Vector2(520, 420);

            ModuleRemoteInfoService.RequestChangelog(module);
            window.ShowUtility();
        }

        private void OnEnable()
        {
            ModuleRemoteInfoService.Changed += Repaint;
        }

        private void OnDisable()
        {
            ModuleRemoteInfoService.Changed -= Repaint;
        }

        private void OnGUI()
        {
            if (_module == null)
            {
                Close();
                return;
            }

            var remoteVersion = ModuleRemoteInfoService.GetRemoteVersion(_module);
            EditorGUILayout.LabelField(
                string.IsNullOrEmpty(remoteVersion)
                    ? _module.DisplayName
                    : $"{_module.DisplayName}  —  {remoteVersion}",
                EditorStyles.boldLabel);

            EditorGUILayout.Space(4);

            var changelog = ModuleRemoteInfoService.GetChangelog(_module);
            if (changelog == null)
            {
                EditorGUILayout.HelpBox("Fetching changelog from GitHub...", MessageType.Info);
                return;
            }

            // Built lazily and reused: OnGUI runs per repaint.
            _bodyStyle ??= new GUIStyle(EditorStyles.label) { wordWrap = true, richText = false };

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.SelectableLabel(changelog, _bodyStyle,
                GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Open on GitHub", GUILayout.Width(130)))
            {
                Application.OpenURL($"https://github.com/phajmvawnsix/{_module.Id}");
            }
            if (GUILayout.Button("Close", GUILayout.Width(80)))
            {
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
