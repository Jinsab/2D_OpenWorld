#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Editor
{
    [CustomEditor(typeof(RememberEventValue))]
    [CanEditMultipleObjects]
    public class RememberEventValueEditor : SaveableComponentEditor
    {
        private SerializedProperty persistentObjectProperty;
        private SerializedProperty keyProperty;
        private SerializedProperty typeProperty;
        private SerializedProperty defaultBoolProperty;
        private SerializedProperty defaultFloatProperty;
        private SerializedProperty defaultIntProperty;
        private SerializedProperty defaultStringProperty;
        private SerializedProperty onSavingProperty;
        private SerializedProperty onLoadedSingleProperty;
        private SerializedProperty onLoadedBoolProperty;
        private SerializedProperty onLoadedFloatProperty;
        private SerializedProperty onLoadedIntProperty;
        private SerializedProperty onLoadedStringProperty;

        protected override void OnEnable()
        {
            base.OnEnable();
            persistentObjectProperty = serializedObject.FindProperty("persistentObject");
            keyProperty = serializedObject.FindProperty("key");
            typeProperty = serializedObject.FindProperty("type");
            defaultBoolProperty = serializedObject.FindProperty("defaultBool");
            defaultFloatProperty = serializedObject.FindProperty("defaultFloat");
            defaultIntProperty = serializedObject.FindProperty("defaultInt");
            defaultStringProperty = serializedObject.FindProperty("defaultString");
            onSavingProperty = serializedObject.FindProperty("onSaving");
            onLoadedSingleProperty = serializedObject.FindProperty("onLoadedSingle");
            onLoadedBoolProperty = serializedObject.FindProperty("onLoadedBool");
            onLoadedFloatProperty = serializedObject.FindProperty("onLoadedFloat");
            onLoadedIntProperty = serializedObject.FindProperty("onLoadedInt");
            onLoadedStringProperty = serializedObject.FindProperty("onLoadedString");
        }

        protected override void DrawDerivedProperties()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Prototype Value Bridge", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Prototype adapter component.\n" +
                "Use 'On Saving' to pull runtime state into this component via SetBool/SetFloat/SetInt/SetString.\n" +
                "Use 'On Loaded' events to replay gameplay logic from restored values.",
                MessageType.Info);

            EditorGUILayout.PropertyField(persistentObjectProperty, new GUIContent("Persistent Object"));
            EditorGUILayout.PropertyField(keyProperty, new GUIContent("Key"));

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Type Configuration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(typeProperty, new GUIContent("Type"));

            DrawDefaultField();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(onSavingProperty, new GUIContent("On Saving"), true);
            EditorGUILayout.PropertyField(onLoadedSingleProperty, new GUIContent("On Loaded (Single)"), true);

            DrawTypedLoadedEvent();
        }

        private void DrawDefaultField()
        {
            if (typeProperty == null || typeProperty.hasMultipleDifferentValues)
            {
                EditorGUILayout.HelpBox("Default field is hidden for multi-object mixed type selection.", MessageType.Info);
                return;
            }

            var valueType = (RememberEventValue.StoredValueType)typeProperty.enumValueIndex;
            switch (valueType)
            {
                case RememberEventValue.StoredValueType.Bool:
                    EditorGUILayout.PropertyField(defaultBoolProperty, new GUIContent("Default"));
                    break;
                case RememberEventValue.StoredValueType.Float:
                    EditorGUILayout.PropertyField(defaultFloatProperty, new GUIContent("Default"));
                    break;
                case RememberEventValue.StoredValueType.Int:
                    EditorGUILayout.PropertyField(defaultIntProperty, new GUIContent("Default"));
                    break;
                case RememberEventValue.StoredValueType.String:
                    EditorGUILayout.PropertyField(defaultStringProperty, new GUIContent("Default"));
                    break;
            }
        }

        private void DrawTypedLoadedEvent()
        {
            if (typeProperty == null || typeProperty.hasMultipleDifferentValues)
            {
                return;
            }

            var valueType = (RememberEventValue.StoredValueType)typeProperty.enumValueIndex;
            switch (valueType)
            {
                case RememberEventValue.StoredValueType.Bool:
                    EditorGUILayout.PropertyField(onLoadedBoolProperty, new GUIContent("On Loaded (Bool)"), true);
                    break;
                case RememberEventValue.StoredValueType.Float:
                    EditorGUILayout.PropertyField(onLoadedFloatProperty, new GUIContent("On Loaded (Float)"), true);
                    break;
                case RememberEventValue.StoredValueType.Int:
                    EditorGUILayout.PropertyField(onLoadedIntProperty, new GUIContent("On Loaded (Int)"), true);
                    break;
                case RememberEventValue.StoredValueType.String:
                    EditorGUILayout.PropertyField(onLoadedStringProperty, new GUIContent("On Loaded (String)"), true);
                    break;
            }
        }

        protected override string[] AdditionalExclusions()
        {
            return new[]
            {
                "persistentObject",
                "key",
                "type",
                "defaultBool",
                "defaultFloat",
                "defaultInt",
                "defaultString",
                "onSaving",
                "onLoadedSingle",
                "onLoadedBool",
                "onLoadedFloat",
                "onLoadedInt",
                "onLoadedString"
            };
        }
    }
}
#endif
#endif
