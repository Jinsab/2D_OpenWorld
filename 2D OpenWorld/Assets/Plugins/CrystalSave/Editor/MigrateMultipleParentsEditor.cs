#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;
using UnityEngine;
using UnityEditor;

namespace Arawn.CrystalSave.Editor
{
	[CustomEditor(typeof(MigrateMultipleParents))]
	public class MigrateMultipleParentsEditor : UnityEditor.Editor
	{
		SerializedProperty migrationEntriesProp;
		// Editor-only cache for Source GameObject references.
		Dictionary<int, GameObject> sourceObjects = new Dictionary<int, GameObject>();
		// Controls whether the Source GameObject field is shown for each entry.
		Dictionary<int, bool> editSourceFlags = new Dictionary<int, bool>();

		private void OnEnable()
		{
			migrationEntriesProp = serializedObject.FindProperty("migrationEntries");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.LabelField("Parent Migration Entries", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(
				"For each entry, assign a Source GameObject that has both a UniqueID and a RememberParent component. " +
				"This is used at edit time to auto-populate the Target Unique ID and a friendly Target Name (set to the GameObject�s name). " +
				"NOTE: The Source GameObject field is temporary and will not be saved.",
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

					SerializedProperty targetUniqueIDProp = entryProp.FindPropertyRelative("targetComponentUniqueID");
					// Default to showing the source field if no valid unique ID exists.
					if (!editSourceFlags.ContainsKey(i))
						editSourceFlags[i] = string.IsNullOrEmpty(targetUniqueIDProp.stringValue);

					if (editSourceFlags[i])
					{
						GameObject currentSource = sourceObjects.ContainsKey(i) ? sourceObjects[i] : null;
						GameObject newSource = (GameObject)EditorGUILayout.ObjectField("Source GameObject", currentSource, typeof(GameObject), true);
						if (newSource != currentSource)
						{
							if (newSource != null)
							{
								// Check for required components: UniqueID and RememberParent.
								UniqueID uniqueIDComp = newSource.GetComponent<UniqueID>();
								RememberParent rememberParentComp = newSource.GetComponent<RememberParent>();
								if (uniqueIDComp == null || rememberParentComp == null)
								{
									EditorUtility.DisplayDialog("Missing Component",
										$"The selected GameObject '{newSource.name}' must have both a UniqueID and a RememberParent component.",
										"OK");
									// Reset the source.
									sourceObjects[i] = null;
									targetUniqueIDProp.stringValue = "";
									if (targetNameProp != null)
										targetNameProp.stringValue = "";
									editSourceFlags[i] = true; // Remain in edit mode.
								}
								else
								{
									sourceObjects[i] = newSource;
									string computedID = $"{uniqueIDComp.ID}_{rememberParentComp.ComponentID}";
									targetUniqueIDProp.stringValue = computedID;
									if (targetNameProp != null)
										targetNameProp.stringValue = newSource.name;
									// Hide the source field.
									editSourceFlags[i] = false;
								}
							}
							else
							{
								// If cleared.
								sourceObjects[i] = null;
								targetUniqueIDProp.stringValue = "";
								if (targetNameProp != null)
									targetNameProp.stringValue = "";
							}
						}
					}
					else
					{
						if (!string.IsNullOrEmpty(targetUniqueIDProp.stringValue))
						{
							if (GUILayout.Button("Edit Source GameObject"))
							{
								editSourceFlags[i] = true;
							}
						}
					}

					// Display computed fields.
					EditorGUILayout.LabelField("Target Unique ID", targetUniqueIDProp.stringValue);
					if (targetNameProp != null)
						EditorGUILayout.LabelField("Target Name", targetNameProp.stringValue);

					EditorGUILayout.Space();
					EditorGUILayout.LabelField("New Parent Information", EditorStyles.boldLabel);
					EditorGUILayout.PropertyField(entryProp.FindPropertyRelative("newParentUniqueID"), new GUIContent("New Parent Unique ID"));

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

			if (GUILayout.Button("Add New Parent Migration Entry"))
			{
				int newIndex = migrationEntriesProp.arraySize;
				migrationEntriesProp.arraySize++;
				SerializedProperty newEntry = migrationEntriesProp.GetArrayElementAtIndex(newIndex);
				newEntry.FindPropertyRelative("targetComponentUniqueID").stringValue = "";
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