#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Arawn.CrystalSave.UI;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Editor
{
    [CustomEditor(typeof(SaveSlotsRuntimeUI))]
    public class SaveSlotsRuntimeUIEditor : UnityEditor.Editor
    {
        SerializedProperty slotNameKeyProp;
    SerializedProperty hiddenSlotNumbersProp;

        void OnEnable()
        {
            slotNameKeyProp = serializedObject.FindProperty("slotNameMetadataKey");
            hiddenSlotNumbersProp = serializedObject.FindProperty("hiddenSlotNumbers");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw everything except the two custom-handled fields
            DrawPropertiesExcluding(serializedObject, "slotNameMetadataKey", "hiddenSlotNumbers");

            // Visibility section
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Slot Visibility", EditorStyles.boldLabel);
            var hiddenLabel = new GUIContent(
                "Hidden Slot Numbers",
                "Comma-separated list of SAVE SLOT numbers to hide in this UI (e.g. '1,2, 3 , 4').\n" +
                "• Spaces are ignored and order doesn't matter.\n" +
                "• Only affects this UI: the slots still exist and can be used via code.\n" +
                "• Invalid or out-of-range entries are ignored.");
            EditorGUILayout.PropertyField(hiddenSlotNumbersProp, hiddenLabel);

            // Quick preview/validation
            if (!string.IsNullOrWhiteSpace(hiddenSlotNumbersProp.stringValue))
            {
                var tokens = hiddenSlotNumbersProp.stringValue.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
                var valid = new SortedSet<int>();
                var invalid = new List<string>();
                foreach (var t in tokens)
                {
                    var s = t.Trim();
                    if (int.TryParse(s, out int n) && n > 0) valid.Add(n); else invalid.Add(s);
                }
                if (valid.Count > 0)
                    EditorGUILayout.HelpBox($"Will hide slots: {string.Join(", ", valid)}", MessageType.Info);
                if (invalid.Count > 0)
                    EditorGUILayout.HelpBox($"Ignored: {string.Join(", ", invalid)}", MessageType.None);
            }

            var options = new List<string> { "Ignore" };
            var settings = AssetProvider.Load<SaveSettings>("SaveSettings");
            if (settings != null && settings.defaultSlotMetadata != null)
            {
                foreach (var entry in settings.defaultSlotMetadata.entries)
                {
                    if (!string.IsNullOrEmpty(entry.key) && !options.Contains(entry.key))
                        options.Add(entry.key);
                }
            }

            int index = string.IsNullOrEmpty(slotNameKeyProp.stringValue) ? 0 : options.IndexOf(slotNameKeyProp.stringValue);
            if (index < 0) index = 0;
            EditorGUILayout.LabelField("Slot Auto-Name", EditorStyles.boldLabel);
            int newIndex = EditorGUILayout.Popup("Slot Name Metadata Key", index, options.ToArray());
            slotNameKeyProp.stringValue = newIndex == 0 ? string.Empty : options[newIndex];

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
#endif
