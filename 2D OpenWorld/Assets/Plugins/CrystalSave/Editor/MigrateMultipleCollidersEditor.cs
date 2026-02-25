#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;
using UnityEngine;
using UnityEditor;

namespace Arawn.CrystalSave.Editor
{
	[CustomEditor(typeof(MigrateMultipleColliders))]
	public class MigrateMultipleCollidersEditor : UnityEditor.Editor
	{
		SerializedProperty migrationEntriesProp;
		// Cache for Source GameObject references (editor-only; not serialized).
		Dictionary<int, GameObject> sourceObjects = new Dictionary<int, GameObject>();
		// Dictionary to control whether the Source GameObject field is shown for each entry.
		Dictionary<int, bool> editSourceFlags = new Dictionary<int, bool>();

		private void OnEnable()
		{
			migrationEntriesProp = serializedObject.FindProperty("migrationEntries");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.LabelField("Collider Migration Entries", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(
				"For each entry, assign a Source GameObject that has both a UniqueID and a RememberCollider component. " +
				"This is used only at edit time to auto-populate the Target Unique ID and a friendly Target Name (set to the GameObject�s name). " +
				"NOTE: The Source GameObject field is temporary and is not saved.",
				MessageType.Info);
			EditorGUILayout.Space();

			int removeIndex = -1;
			for (int i = 0; i < migrationEntriesProp.arraySize; i++)
			{
				SerializedProperty entryProp = migrationEntriesProp.GetArrayElementAtIndex(i);
				// Retrieve targetName for foldout label.
				SerializedProperty targetNameProp = entryProp.FindPropertyRelative("targetName");
				string targetName = (targetNameProp != null && !string.IsNullOrEmpty(targetNameProp.stringValue)) ? targetNameProp.stringValue : "";
				string foldoutLabel = $"#{i + 1} Migrate: {(string.IsNullOrEmpty(targetName) ? "Entry" : targetName)}";

				EditorGUILayout.BeginVertical("box");
				entryProp.isExpanded = EditorGUILayout.Foldout(entryProp.isExpanded, foldoutLabel);
				if (entryProp.isExpanded)
				{
					EditorGUI.indentLevel++;

					SerializedProperty targetUniqueIDProp = entryProp.FindPropertyRelative("targetUniqueID");

					// Default to showing the Source GameObject field if targetUniqueID is empty.
					if (!editSourceFlags.ContainsKey(i))
						editSourceFlags[i] = string.IsNullOrEmpty(targetUniqueIDProp.stringValue);

					if (editSourceFlags[i])
					{
						GameObject currentSource = sourceObjects.ContainsKey(i) ? sourceObjects[i] : null;
						GameObject newSource = (GameObject)EditorGUILayout.ObjectField("Source GameObject", currentSource, typeof(GameObject), true);
						if (newSource != currentSource)
						{
							// Check if newSource has required components.
							if (newSource != null)
							{
								UniqueID uniqueIDComp = newSource.GetComponent<UniqueID>();
								RememberCollider rememberColComp = newSource.GetComponent<RememberCollider>();
								if (uniqueIDComp == null || rememberColComp == null)
								{
									EditorUtility.DisplayDialog("Missing Component",
										$"The selected GameObject '{newSource.name}' must have both a UniqueID and a RememberCollider component.",
										"OK");
									// Reset the source field so it remains visible.
									sourceObjects[i] = null;
									targetUniqueIDProp.stringValue = "";
									if (targetNameProp != null)
										targetNameProp.stringValue = "";
								}
								else
								{
									// Valid source: store it and auto-populate the fields.
									sourceObjects[i] = newSource;
									string computedID = $"{uniqueIDComp.ID}_{rememberColComp.ComponentID}";
									targetUniqueIDProp.stringValue = computedID;
									if (targetNameProp != null)
										targetNameProp.stringValue = newSource.name;
									// Hide the source field.
									editSourceFlags[i] = false;
								}
							}
							else
							{
								// User cleared the source.
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

					// Display computed fields.
					EditorGUILayout.LabelField("Target Unique ID", targetUniqueIDProp.stringValue);
					if (targetNameProp != null)
						EditorGUILayout.LabelField("Target Name", targetNameProp.stringValue);

					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Collider Property Updates", EditorStyles.boldLabel);
					DrawConditionalProperty(entryProp, "updateEnabled", "newEnabled", "New Enabled Value");
					DrawConditionalProperty(entryProp, "updateIsTrigger", "newIsTrigger", "New IsTrigger Value");
					DrawConditionalProperty(entryProp, "updateMaterial", "newMaterialName", "New Material Name");

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

			if (GUILayout.Button("Add New Collider Migration Entry"))
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

		// Helper method to draw an update flag and its corresponding new value field.
		private void DrawConditionalProperty(SerializedProperty entryProp, string flagName, string valueName, string label)
		{
			SerializedProperty flagProp = entryProp.FindPropertyRelative(flagName);
			EditorGUILayout.PropertyField(flagProp, new GUIContent($"Update {label}"));
			if (flagProp.boolValue)
			{
				SerializedProperty valueProp = entryProp.FindPropertyRelative(valueName);
				EditorGUILayout.PropertyField(valueProp, new GUIContent(label));
			}
		}
	}
}

#endif