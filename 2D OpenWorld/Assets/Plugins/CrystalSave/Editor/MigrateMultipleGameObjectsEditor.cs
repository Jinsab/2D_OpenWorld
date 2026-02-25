#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;
using UnityEngine;
using UnityEditor;

namespace Arawn.CrystalSave.Editor
{
	[CustomEditor(typeof(MigrateMultipleGameObjects))]
	public class MigrateMultipleGameObjectsEditor : UnityEditor.Editor
	{
		SerializedProperty migrationEntriesProp;
		// Cache for Source GameObject references (editor-only; not serialized)
		Dictionary<int, GameObject> sourceObjects = new Dictionary<int, GameObject>();
		// Controls whether the Source GameObject field is shown for each entry
		Dictionary<int, bool> editSourceFlags = new Dictionary<int, bool>();

		private void OnEnable()
		{
			migrationEntriesProp = serializedObject.FindProperty("migrationEntries");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.LabelField("GameObject Migration Entries", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(
				"For each entry, assign a Source GameObject that has both a UniqueID and a RememberGameObject component. " +
				"This is used at edit time to automatically populate the Target Unique ID and a friendly Target Name (set to the GameObject�s name). " +
				"NOTE: The Source GameObject field is temporary and is not saved.",
				MessageType.Info);
			EditorGUILayout.Space();

			int removeIndex = -1;
			for (int i = 0; i < migrationEntriesProp.arraySize; i++)
			{
				SerializedProperty entryProp = migrationEntriesProp.GetArrayElementAtIndex(i);
				// Retrieve targetName to use in the foldout label.
				SerializedProperty targetNameProp = entryProp.FindPropertyRelative("targetName");
				string targetName = (targetNameProp != null && !string.IsNullOrEmpty(targetNameProp.stringValue)) ? targetNameProp.stringValue : "";
				string foldoutLabel = $"#{i + 1} Migrate: {(string.IsNullOrEmpty(targetName) ? "Entry" : targetName)}";

				EditorGUILayout.BeginVertical("box");
				entryProp.isExpanded = EditorGUILayout.Foldout(entryProp.isExpanded, foldoutLabel);
				if (entryProp.isExpanded)
				{
					EditorGUI.indentLevel++;

					SerializedProperty targetUniqueIDProp = entryProp.FindPropertyRelative("targetUniqueID");
					// If not already set, show the source field.
					if (!editSourceFlags.ContainsKey(i))
						editSourceFlags[i] = string.IsNullOrEmpty(targetUniqueIDProp.stringValue);

					if (editSourceFlags[i])
					{
						GameObject currentSource = sourceObjects.ContainsKey(i) ? sourceObjects[i] : null;
						GameObject newSource = (GameObject)EditorGUILayout.ObjectField("Source GameObject", currentSource, typeof(GameObject), true);
						if (newSource != currentSource)
						{
							// Check for required components: UniqueID and RememberGameObject.
							if (newSource != null)
							{
								UniqueID uniqueIDComp = newSource.GetComponent<UniqueID>();
								RememberGameObject rememberGOComp = newSource.GetComponent<RememberGameObject>();
								if (uniqueIDComp == null || rememberGOComp == null)
								{
									EditorUtility.DisplayDialog("Missing Component",
										$"The selected GameObject '{newSource.name}' must have both a UniqueID and a RememberGameObject component.",
										"OK");
									// Reset the source reference.
									sourceObjects[i] = null;
									targetUniqueIDProp.stringValue = "";
									if (targetNameProp != null)
										targetNameProp.stringValue = "";
								}
								else
								{
									// Valid source: store and auto-populate fields.
									sourceObjects[i] = newSource;
									string computedID = $"{uniqueIDComp.ID}_{rememberGOComp.ComponentID}";
									targetUniqueIDProp.stringValue = computedID;
									if (targetNameProp != null)
										targetNameProp.stringValue = newSource.name;
									// Hide the source field.
									editSourceFlags[i] = false;
								}
							}
							else
							{
								// If source is cleared.
								sourceObjects[i] = null;
								targetUniqueIDProp.stringValue = "";
								if (targetNameProp != null)
									targetNameProp.stringValue = "";
							}
						}
					}
					else
					{
						if (GUILayout.Button("Edit Source GameObject"))
						{
							editSourceFlags[i] = true;
						}
					}

					// Display the computed fields.
					EditorGUILayout.LabelField("Target Unique ID", targetUniqueIDProp.stringValue);
					if (targetNameProp != null)
						EditorGUILayout.LabelField("Target Name", targetNameProp.stringValue);

					EditorGUILayout.Space();
					EditorGUILayout.LabelField("GameObject Property Updates", EditorStyles.boldLabel);
					// Draw update fields for new GameObject properties.
					EditorGUILayout.PropertyField(entryProp.FindPropertyRelative("newName"), new GUIContent("New Name"));
					EditorGUILayout.PropertyField(entryProp.FindPropertyRelative("newLayer"), new GUIContent("New Layer"));
					EditorGUILayout.PropertyField(entryProp.FindPropertyRelative("newTag"), new GUIContent("New Tag"));
					EditorGUILayout.PropertyField(entryProp.FindPropertyRelative("newIsActive"), new GUIContent("New Active State"));

					EditorGUI.indentLevel--;
				}
				EditorGUILayout.Space();
				if (GUILayout.Button("Remove Entry"))
				{
					removeIndex = i;
				}
				EditorGUILayout.EndVertical();
				EditorGUILayout.Space();
			}

			if (removeIndex != -1)
			{
				migrationEntriesProp.DeleteArrayElementAtIndex(removeIndex);
				if (sourceObjects.ContainsKey(removeIndex))
					sourceObjects.Remove(removeIndex);
				if (editSourceFlags.ContainsKey(removeIndex))
					editSourceFlags.Remove(removeIndex);
			}

			if (GUILayout.Button("Add New GameObject Migration Entry"))
			{
				int newIndex = migrationEntriesProp.arraySize;
				migrationEntriesProp.arraySize++;
				SerializedProperty newEntry = migrationEntriesProp.GetArrayElementAtIndex(newIndex);
				newEntry.FindPropertyRelative("targetUniqueID").stringValue = "";
				SerializedProperty newTargetName = newEntry.FindPropertyRelative("targetName");
				if (newTargetName != null)
					newTargetName.stringValue = "";
				editSourceFlags[newIndex] = true;
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
}

#endif