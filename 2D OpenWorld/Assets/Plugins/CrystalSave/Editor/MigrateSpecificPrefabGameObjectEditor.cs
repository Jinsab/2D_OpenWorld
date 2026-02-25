#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEditor;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Runtime
{
    [CustomEditor(typeof(MigrateSpecificPrefabGameObject))]
    public class MigrateSpecificPrefabGameObjectEditor : UnityEditor.Editor
    {
        SerializedProperty targetPrefabAssetIDProp;
        SerializedProperty newNameProp;
        SerializedProperty newLayerProp;
        SerializedProperty newTagProp;
        SerializedProperty newIsActiveProp;

        void OnEnable()
        {
            targetPrefabAssetIDProp = serializedObject.FindProperty("targetPrefabAssetID");
            newNameProp = serializedObject.FindProperty("newName");
            newLayerProp = serializedObject.FindProperty("newLayer");
            newTagProp = serializedObject.FindProperty("newTag");
            newIsActiveProp = serializedObject.FindProperty("newIsActive");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(targetPrefabAssetIDProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("New GameObject Properties", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(newNameProp);
            EditorGUILayout.PropertyField(newLayerProp);
            EditorGUILayout.PropertyField(newTagProp);
            EditorGUILayout.PropertyField(newIsActiveProp);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
