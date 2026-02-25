#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;
using UnityEngine;
using UnityEditor;

namespace Arawn.CrystalSave.Editor
{
	[CustomEditor(typeof(MigrateMultipleRigidbodies))]
	public class MigrateMultipleRigidbodiesEditor : UnityEditor.Editor
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

			EditorGUILayout.LabelField("Rigidbody Migration Entries", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(
				"For each entry, assign a Source GameObject that has both a UniqueID and a RememberRigidbody component. " +
				"This is used to automatically populate the Target Unique ID and a friendly Target Name (set to the GameObject�s name). " +
				"NOTE: The Source GameObject field is used only at edit time to extract these values and will not be saved.",
				MessageType.Info);
			EditorGUILayout.Space();

			// Instead of breaking from the loop, record an index to remove.
			int removeIndex = -1;
			for (int i = 0; i < migrationEntriesProp.arraySize; i++)
			{
				SerializedProperty entryProp = migrationEntriesProp.GetArrayElementAtIndex(i);

				// Use targetName to create a foldout label.
				SerializedProperty targetNameProp = entryProp.FindPropertyRelative("targetName");
				string targetName = (targetNameProp != null && !string.IsNullOrEmpty(targetNameProp.stringValue)) ? targetNameProp.stringValue : "";
				string foldoutLabel = $"#{i + 1} Migrate: {(string.IsNullOrEmpty(targetName) ? "Entry" : targetName)}";

				EditorGUILayout.BeginVertical("box");
				entryProp.isExpanded = EditorGUILayout.Foldout(entryProp.isExpanded, foldoutLabel);
				if (entryProp.isExpanded)
				{
					EditorGUI.indentLevel++;

					// Source GameObject field handling.
					SerializedProperty targetUniqueIDProp = entryProp.FindPropertyRelative("targetUniqueID");
					// Default to showing the source field if no valid ID is present.
					if (!editSourceFlags.ContainsKey(i))
						editSourceFlags[i] = string.IsNullOrEmpty(targetUniqueIDProp.stringValue);

					if (editSourceFlags[i])
					{
						GameObject currentSource = sourceObjects.ContainsKey(i) ? sourceObjects[i] : null;
						GameObject newSource = (GameObject)EditorGUILayout.ObjectField("Source GameObject", currentSource, typeof(GameObject), true);
						if (newSource != currentSource)
						{
							sourceObjects[i] = newSource;
							if (newSource != null)
							{
								UniqueID uniqueIDComp = newSource.GetComponent<UniqueID>();
								RememberRigidbody rememberRBComp = newSource.GetComponent<RememberRigidbody>();
								if (uniqueIDComp == null || rememberRBComp == null)
								{
									EditorUtility.DisplayDialog("Missing Component",
										$"The selected GameObject '{newSource.name}' must have both a UniqueID and a RememberRigidbody component.",
										"OK");
									targetUniqueIDProp.stringValue = "";
									if (targetNameProp != null)
										targetNameProp.stringValue = "";
								}
								else
								{
									string computedID = $"{uniqueIDComp.ID}_{rememberRBComp.ComponentID}";
									targetUniqueIDProp.stringValue = computedID;
									if (targetNameProp != null)
										targetNameProp.stringValue = newSource.name;
								}
								// Hide source field once a valid source is set.
								editSourceFlags[i] = false;
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

					// Display the auto-populated fields.
					EditorGUILayout.LabelField("Target Unique ID", targetUniqueIDProp.stringValue);
					if (targetNameProp != null)
						EditorGUILayout.LabelField("Target Name", targetNameProp.stringValue);

					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Rigidbody Property Updates", EditorStyles.boldLabel);
					DrawConditionalProperty(entryProp, "updateIsKinematic", "newIsKinematic", "New IsKinematic");
					DrawConditionalProperty(entryProp, "updateUseGravity", "newUseGravity", "New UseGravity");
					DrawConditionalProperty(entryProp, "updateMass", "newMass", "New Mass");
					DrawConditionalProperty(entryProp, "updateDrag", "newDrag", "New Drag");
					DrawConditionalProperty(entryProp, "updateAngularDrag", "newAngularDrag", "New Angular Drag");
					DrawConditionalProperty(entryProp, "updateConstraints", "newConstraints", "New Constraints");
					DrawConditionalProperty(entryProp, "updateVelocity", "newVelocity", "New Velocity");
					DrawConditionalProperty(entryProp, "updateAngularVelocity", "newAngularVelocity", "New Angular Velocity");
					DrawConditionalProperty(entryProp, "updateDetectCollisions", "newDetectCollisions", "New Detect Collisions");

					EditorGUI.indentLevel--;
				}

				// Remove button - do not break out immediately.
				if (GUILayout.Button("Remove Entry"))
				{
					removeIndex = i;
				}
				EditorGUILayout.EndVertical();
				EditorGUILayout.Space();
			}

			// If any entry was marked for removal, remove it now.
			if (removeIndex != -1)
			{
				migrationEntriesProp.DeleteArrayElementAtIndex(removeIndex);
				if (sourceObjects.ContainsKey(removeIndex))
					sourceObjects.Remove(removeIndex);
				if (editSourceFlags.ContainsKey(removeIndex))
					editSourceFlags.Remove(removeIndex);
			}

			if (GUILayout.Button("Add New Rigidbody Migration Entry"))
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

		// Helper method to conditionally draw an update flag and its corresponding new value field.
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