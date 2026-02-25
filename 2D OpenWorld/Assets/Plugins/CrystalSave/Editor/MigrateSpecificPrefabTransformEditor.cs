#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEditor;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Runtime
{
	[CustomEditor(typeof(MigrateSpecificPrefabTransform))]
	public class MigrateSpecificPrefabTransformEditor : UnityEditor.Editor
	{
		SerializedProperty targetPrefabAssetIDProp;
		SerializedProperty newPositionProp;
		SerializedProperty newEulerRotationProp;
                SerializedProperty newScaleProp;

		void OnEnable()
		{
			targetPrefabAssetIDProp = serializedObject.FindProperty("targetPrefabAssetID");
			newPositionProp = serializedObject.FindProperty("newPosition");
			newEulerRotationProp = serializedObject.FindProperty("newEulerRotation");
			newScaleProp = serializedObject.FindProperty("newScale");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.PropertyField(targetPrefabAssetIDProp);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("New Transform Values", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(newPositionProp);
			EditorGUILayout.PropertyField(newEulerRotationProp);
                        EditorGUILayout.PropertyField(newScaleProp);

			serializedObject.ApplyModifiedProperties();
		}

	}
}
#endif
