#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;
using UnityEngine;
using UnityEditor;

namespace Arawn.CrystalSave.Editor
{
	[CustomEditor(typeof(MigrateMultipleCameras))]
	public class MigrateMultipleCamerasEditor : UnityEditor.Editor
	{
		SerializedProperty migrationEntriesProp;
		// Cache for source GameObject references (editor-only; not serialized)
		Dictionary<int, GameObject> sourceObjects = new Dictionary<int, GameObject>();
		// Controls whether the Source GameObject field is shown (true) or hidden (false) for each entry.
		Dictionary<int, bool> editSourceFlags = new Dictionary<int, bool>();

		private void OnEnable()
		{
			migrationEntriesProp = serializedObject.FindProperty("migrationEntries");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.LabelField("Camera Migration Entries", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(
				"For each entry, assign a Source GameObject that has both a UniqueID and a RememberCamera component. " +
				"This is used only at edit time to auto-populate the Target Unique ID and a friendly Target Name (set to the GameObject�s name). " +
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

					SerializedProperty targetUniqueIDProp = entryProp.FindPropertyRelative("targetUniqueID");
					// If no valid ID is set, default to showing the source field.
					if (!editSourceFlags.ContainsKey(i))
						editSourceFlags[i] = string.IsNullOrEmpty(targetUniqueIDProp.stringValue);

					if (editSourceFlags[i])
					{
						GameObject currentSource = sourceObjects.ContainsKey(i) ? sourceObjects[i] : null;
						GameObject newSource = (GameObject)EditorGUILayout.ObjectField("Source GameObject", currentSource, typeof(GameObject), true);
						if (newSource != currentSource)
						{
							// When a new source is assigned, check for required components.
							if (newSource != null)
							{
								UniqueID uniqueIDComp = newSource.GetComponent<UniqueID>();
								RememberCamera rememberCamComp = newSource.GetComponent<RememberCamera>();
								if (uniqueIDComp == null || rememberCamComp == null)
								{
									EditorUtility.DisplayDialog("Missing Component",
										$"The selected GameObject '{newSource.name}' must have both a UniqueID and a RememberCamera component.",
										"OK");
									// Reset the source so it stays null and the field remains visible.
									sourceObjects[i] = null;
									targetUniqueIDProp.stringValue = "";
									if (targetNameProp != null)
										targetNameProp.stringValue = "";
									// Remain in edit mode.
									editSourceFlags[i] = true;
								}
								else
								{
									// Valid source: store it and auto-populate the fields.
									sourceObjects[i] = newSource;
									string computedID = $"{uniqueIDComp.ID}_{rememberCamComp.ComponentID}";
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
						// Only show the Edit button if a valid source is set.
						if (!string.IsNullOrEmpty(targetUniqueIDProp.stringValue))
						{
							if (GUILayout.Button("Edit Source GameObject"))
							{
								editSourceFlags[i] = true;
							}
						}
					}

					// Display the auto-populated fields.
					EditorGUILayout.LabelField("Target Unique ID", targetUniqueIDProp.stringValue);
					if (targetNameProp != null)
						EditorGUILayout.LabelField("Target Name", targetNameProp.stringValue);

					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Camera Property Updates", EditorStyles.boldLabel);
					DrawConditionalProperty(entryProp, "updateFieldOfView", "newFieldOfView", "New Field of View");
					DrawConditionalProperty(entryProp, "updateClippingPlanes", "newClippingPlanes", "New Clipping Planes");
					DrawConditionalProperty(entryProp, "updateProjection", "newProjection", "New Projection");
					DrawConditionalProperty(entryProp, "updateOrthographicSize", "newOrthographicSize", "New Orthographic Size");
					DrawConditionalProperty(entryProp, "updateClearFlags", "newClearFlags", "New Clear Flags");
					DrawConditionalProperty(entryProp, "updateBackgroundColor", "newBackgroundColor", "New Background Color");
					DrawConditionalProperty(entryProp, "updateCullingMask", "newCullingMask", "New Culling Mask");
					DrawConditionalProperty(entryProp, "updateDepth", "newDepth", "New Depth");
					DrawConditionalProperty(entryProp, "updateAspect", "newAspect", "New Aspect");

#if REMEMBERME_HDRP_PRESENT
                DrawConditionalProperty(entryProp, "updateHDRPDynamicResolutionEnabled", "newHDRPDynamicResolutionEnabled", "New HDRP Dynamic Resolution");
                DrawConditionalProperty(entryProp, "updateExposureTargetUniqueID", "newExposureTargetUniqueID", "New Exposure Target UniqueID");
#endif

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