#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Arawn.CrystalSave.UI;

namespace Arawn.CrystalSave.Editor
{
    [CustomEditor(typeof(SaveSlotsRuntimeTheme))]
    public class SaveSlotsRuntimeThemeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            GUILayout.Space(8);
            if (GUILayout.Button("Refresh"))
            {
                var theme = (SaveSlotsRuntimeTheme)target;
                theme.NotifyThemeChanged();
                if (theme.entryTheme != null)
                    theme.entryTheme.NotifyThemeChanged();
                EditorUtility.SetDirty(theme);
            }

            if (GUILayout.Button("Randomize Colors"))
            {
                var theme = (SaveSlotsRuntimeTheme)target;
                Undo.RecordObject(theme, "Randomize Theme Colors");
                if (theme.entryTheme != null)
                    Undo.RecordObject(theme.entryTheme, "Randomize Entry Theme Colors");

                GenerateRandomColors(theme);

                theme.NotifyThemeChanged();
                if (theme.entryTheme != null)
                    theme.entryTheme.NotifyThemeChanged();

                EditorUtility.SetDirty(theme);
                if (theme.entryTheme != null)
                    EditorUtility.SetDirty(theme.entryTheme);
            }
        }

        static void GenerateRandomColors(SaveSlotsRuntimeTheme theme)
        {
            // Pick a base hue and create lighter/darker variants for harmony
            Color baseColor = Random.ColorHSV(0f, 1f, 0.5f, 0.9f, 0.6f, 1f);
            Color lighter   = Color.Lerp(baseColor, Color.white, 0.3f);
            Color darker    = Color.Lerp(baseColor, Color.black, 0.2f);

            theme.backgroundColor      = lighter;
            theme.panelColor           = baseColor;
            theme.titleBackgroundColor = darker;
            theme.titleTextColor       = Color.Lerp(darker, Color.white, 0.8f);

            if (theme.entryTheme != null)
            {
                var entry = theme.entryTheme;
                entry.backgroundColor  = lighter;
                entry.panelColor       = baseColor;
                entry.textColor        = theme.titleTextColor;
                entry.buttonTextColor  = theme.titleTextColor;
            }
        }
    }
}
#endif
#endif
