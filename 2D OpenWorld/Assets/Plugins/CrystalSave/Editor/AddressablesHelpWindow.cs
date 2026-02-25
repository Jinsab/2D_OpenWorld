#if UNITY_EDITOR && MEMORYPACK
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
    public class AddressablesHelpWindow : EditorWindow
    {
        private static readonly GUIContent s_WindowTitle = new GUIContent("Addressables Help");
        private static readonly GUIContent s_HelpButtonContent = new GUIContent("?", "Open guidance for configuring Crystal Save with Unity Addressables.");

        private Vector2 scrollPosition;

        public static GUIContent HelpButtonContent => s_HelpButtonContent;

        public static void ShowWindow()
        {
            var window = GetWindow<AddressablesHelpWindow>();
            window.titleContent = s_WindowTitle;
            window.minSize = new Vector2(420f, 320f);
            window.Focus();
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Using Crystal Save with Addressables", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox("Follow these steps to migrate Crystal Save assets from Resources to Addressables.", MessageType.Info);
            EditorGUILayout.Space();

            using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPosition))
            {
                scrollPosition = scroll.scrollPosition;
                DrawStep(1, "Move settings assets out of Resources.", "Locate SaveSettings, PrefabRegistry, SaveSlotMetadata, and related assets. Place them outside any Resources folder so Addressables controls their loading.");
                DrawStep(2, "Mark them as Addressable and replace the default path-based address with a short key (\"SaveSlotMetadata\", \"PrefabRegistry\", etc.).", "Enable the Addressable checkbox for each asset and supply a concise key instead of the original Resources-style path.");
                DrawStep(3, "Build or update the Addressables catalog.", "Open Window > Asset Management > Addressables > Groups and choose Build > New Build > Default Build Script (or Update a Previous Build) after making changes.");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Unity documentation", EditorStyles.boldLabel);
            if (GUILayout.Button("Addressables manual", EditorStyles.linkLabel))
            {
                Application.OpenURL("https://docs.unity3d.com/Packages/com.unity.addressables@latest/manual/index.html");
            }
            if (GUILayout.Button("Addressables build workflow", EditorStyles.linkLabel))
            {
                Application.OpenURL("https://docs.unity3d.com/Packages/com.unity.addressables@latest/manual/Builds.html");
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Close", GUILayout.Width(80f)))
                {
                    Close();
                }
            }
        }

        private static void DrawStep(int index, string title, string description)
        {
            EditorGUILayout.LabelField($"{index}. {title}", EditorStyles.boldLabel);
            int previousIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedLabel);
            EditorGUI.indentLevel = previousIndent;
            EditorGUILayout.Space();
        }
    }
}
#endif
