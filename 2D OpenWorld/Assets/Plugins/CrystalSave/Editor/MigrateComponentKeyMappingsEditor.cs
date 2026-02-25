#if MEMORYPACK && ARAWN_REMEMBERME
using Arawn.CrystalSave.Runtime;
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
	[CustomEditor(typeof(MigrateComponentKeyMappings))]
	public class MigrateComponentKeyMappingsEditor : UnityEditor.Editor
	{
		private SerializedProperty mappingsProp;
		private SerializedProperty overwriteExistingTargetProp;
		private SerializedProperty removeSourceAfterCopyProp;
		private SerializedProperty syncComponentMetadataProp;
		private SerializedProperty removeWhenNewKeyEmptyProp;

		private void OnEnable()
		{
			mappingsProp = serializedObject.FindProperty("mappings");
			overwriteExistingTargetProp = serializedObject.FindProperty("overwriteExistingTarget");
			removeSourceAfterCopyProp = serializedObject.FindProperty("removeSourceAfterCopy");
			syncComponentMetadataProp = serializedObject.FindProperty("syncComponentMetadata");
			removeWhenNewKeyEmptyProp = serializedObject.FindProperty("removeWhenNewKeyEmpty");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.HelpBox(
				"No-code key migration for component identifiers.\n\n" +
				"Use this when component keys changed between versions (old key -> new key).\n" +
				"Leave New Key empty to remove obsolete old keys (if enabled below).",
				MessageType.Info);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Behavior", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(overwriteExistingTargetProp);
			EditorGUILayout.PropertyField(removeSourceAfterCopyProp);
			EditorGUILayout.PropertyField(syncComponentMetadataProp);
			EditorGUILayout.PropertyField(removeWhenNewKeyEmptyProp);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Key Mappings", EditorStyles.boldLabel);

			if (mappingsProp.arraySize == 0)
			{
				EditorGUILayout.HelpBox(
					"No mappings configured yet.\nAdd entries from your Component Key Diff report.",
					MessageType.Warning);
			}

			int removeIndex = -1;
			for (int i = 0; i < mappingsProp.arraySize; i++)
			{
				SerializedProperty entryProp = mappingsProp.GetArrayElementAtIndex(i);
				SerializedProperty oldKeyProp = entryProp.FindPropertyRelative("oldKey");
				SerializedProperty newKeyProp = entryProp.FindPropertyRelative("newKey");
				SerializedProperty noteProp = entryProp.FindPropertyRelative("note");

				string oldKeyPreview = string.IsNullOrEmpty(oldKeyProp.stringValue) ? "<empty>" : oldKeyProp.stringValue;
				string newKeyPreview = string.IsNullOrEmpty(newKeyProp.stringValue) ? "<remove>" : newKeyProp.stringValue;

				EditorGUILayout.BeginVertical("box");
				EditorGUILayout.LabelField($"#{i + 1}: {oldKeyPreview} -> {newKeyPreview}", EditorStyles.boldLabel);
				EditorGUILayout.PropertyField(oldKeyProp, new GUIContent("Old Key"));
				EditorGUILayout.PropertyField(newKeyProp, new GUIContent("New Key"));
				EditorGUILayout.PropertyField(noteProp, new GUIContent("Note"));

				if (GUILayout.Button("Remove Mapping"))
				{
					removeIndex = i;
				}

				EditorGUILayout.EndVertical();
				EditorGUILayout.Space(2f);
			}

			if (removeIndex >= 0)
			{
				mappingsProp.DeleteArrayElementAtIndex(removeIndex);
			}

			EditorGUILayout.Space(4f);
			if (GUILayout.Button("Add Mapping Entry"))
			{
				int newIndex = mappingsProp.arraySize;
				mappingsProp.arraySize++;
				SerializedProperty newEntry = mappingsProp.GetArrayElementAtIndex(newIndex);
				newEntry.FindPropertyRelative("oldKey").stringValue = string.Empty;
				newEntry.FindPropertyRelative("newKey").stringValue = string.Empty;
				newEntry.FindPropertyRelative("note").stringValue = string.Empty;
			}

			EditorGUILayout.Space();
			DrawValidationSummary();

			serializedObject.ApplyModifiedProperties();
		}

		private void DrawValidationSummary()
		{
			int emptyOldKeyCount = 0;
			int selfMapCount = 0;
			int removeModeCount = 0;

			for (int i = 0; i < mappingsProp.arraySize; i++)
			{
				SerializedProperty entryProp = mappingsProp.GetArrayElementAtIndex(i);
				string oldKey = entryProp.FindPropertyRelative("oldKey").stringValue?.Trim();
				string newKey = entryProp.FindPropertyRelative("newKey").stringValue?.Trim();

				if (string.IsNullOrEmpty(oldKey))
					emptyOldKeyCount++;

				if (!string.IsNullOrEmpty(oldKey) && oldKey == newKey)
					selfMapCount++;

				if (!string.IsNullOrEmpty(oldKey) && string.IsNullOrEmpty(newKey))
					removeModeCount++;
			}

			string summary =
				$"Entries: {mappingsProp.arraySize} | " +
				$"Remove-mode entries (empty New Key): {removeModeCount}";
			EditorGUILayout.HelpBox(summary, MessageType.None);

			if (emptyOldKeyCount > 0 || selfMapCount > 0)
			{
				EditorGUILayout.HelpBox(
					$"Validation notes:\n" +
					$"- Empty Old Key entries: {emptyOldKeyCount}\n" +
					$"- Old Key == New Key entries: {selfMapCount}",
					MessageType.Warning);
			}
		}
	}
}
#endif
