#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Arawn.CrystalSave.UI;

namespace Arawn.CrystalSave.Editor
{
    [CustomEditor(typeof(SaveSlotEntryTheme))]
    public class SaveSlotEntryThemeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            GUILayout.Space(8);
            if (GUILayout.Button("Refresh"))
            {
                var theme = (SaveSlotEntryTheme)target;
                theme.NotifyThemeChanged();
                EditorUtility.SetDirty(theme);
            }
        }
    }
}
#endif
#endif
