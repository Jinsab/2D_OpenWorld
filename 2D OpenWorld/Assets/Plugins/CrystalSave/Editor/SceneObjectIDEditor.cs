#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using Logger = Arawn.CrystalSave.Runtime.Logger;
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Editor
{
	[CustomEditor(typeof(SceneObjectID))]
	public class SceneObjectIDEditor : UnityEditor.Editor
	{
		private SerializedProperty uniqueIDProp;
		private SerializedProperty keepAcrossScenesProp;
		private SerializedProperty disableOnSceneLoadProp;
		private bool isEditing = false;
		private string newUniqueID = "";
		private static readonly Dictionary<string, DateTime> lastLoggedWarnings = new Dictionary<string, DateTime>();
		private const int WarningIntervalSeconds = 10;

		// On-demand validation state
		private string _lastScanMessage;
		private DateTime _lastScanTime;

		private void OnEnable()
		{
			// Link the serialized properties
			uniqueIDProp = serializedObject.FindProperty("uniqueID");
			keepAcrossScenesProp = serializedObject.FindProperty("keepAcrossScenes");
			disableOnSceneLoadProp = serializedObject.FindProperty("disableOnSceneLoad");
		}

		public override void OnInspectorGUI()
		{
			// Update the serialized object to reflect current values
			serializedObject.Update();

			// Duplicate-ID scan is now on-demand via button below to avoid heavy editor scans.

			// Draw all properties except 'm_Script', 'uniqueID', 'keepAcrossScenes', and 'disableOnSceneLoad'
			DrawPropertiesExcluding(serializedObject, "m_Script", "uniqueID", "keepAcrossScenes", "disableOnSceneLoad");

			// Add spacing for clarity
			EditorGUILayout.Space();

			// Display 'uniqueID' as a read-only field with a bold label
			GUIStyle boldStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold };
			EditorGUILayout.LabelField("Scene Object Unique ID", uniqueIDProp.stringValue, boldStyle);

			// Add spacing
			EditorGUILayout.Space();

			// Begin a horizontal group for the Unique ID buttons
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();

			// If 'uniqueID' is empty, show only "Generate Unique ID" button
			if (string.IsNullOrEmpty(uniqueIDProp.stringValue))
			{
				if (GUILayout.Button("Generate New ID", GUILayout.Width(150)))
				{
					Undo.RecordObject(target, "Generate Unique ID");
					string generatedID = Guid.NewGuid().ToString();
					uniqueIDProp.stringValue = generatedID;
					serializedObject.ApplyModifiedProperties();
					EditorUtility.SetDirty(target);

					SceneObjectID sceneObjectID = (SceneObjectID)target;
					Logger.Log($"SceneObjectIDEditor: Assigned new UniqueID '{generatedID}' to '{sceneObjectID.gameObject.name}'.");

					EditorUtility.DisplayDialog("Unique ID Generated", $"A new Unique ID has been generated:\n{generatedID}", "OK");
				}
			}
			else
			{
				// Show "Edit Unique ID" button
				if (GUILayout.Button("Edit", GUILayout.Width(100)))
				{
					newUniqueID = uniqueIDProp.stringValue;
					isEditing = true;
				}

				// Show "Generate New ID" button
				if (GUILayout.Button("Generate New ID", GUILayout.Width(150)))
				{
					if (EditorUtility.DisplayDialog("Generate New Unique ID",
						"Are you sure you want to generate a new Unique ID? This action cannot be undone and may affect data associations.",
						"Yes", "No"))
					{
						GenerateNewUniqueID();
					}
				}

				// Show "Copy ID" button
				if (GUILayout.Button("Copy ID", GUILayout.Width(100)))
				{
					EditorGUIUtility.systemCopyBuffer = uniqueIDProp.stringValue;
					EditorUtility.DisplayDialog("Copied", "Unique ID copied to clipboard.", "OK");
				}
			}

			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();

			// If in editing mode, display the editing interface
			if (isEditing)
			{
				EditorGUILayout.BeginVertical("box");
				EditorGUILayout.LabelField("Edit Unique ID", EditorStyles.boldLabel);

				newUniqueID = EditorGUILayout.TextField("New Unique ID", newUniqueID);

				if (!IsValidUniqueID(newUniqueID))
				{
					EditorGUILayout.HelpBox("Unique ID cannot be empty or whitespace.", MessageType.Error);
				}

				if (GUILayout.Button("Auto-Generate ID", GUILayout.Width(150)))
				{
					newUniqueID = Guid.NewGuid().ToString();
				}

				EditorGUILayout.BeginHorizontal();
				if (GUILayout.Button("OK"))
				{
					if (IsValidUniqueID(newUniqueID))
					{
						Undo.RecordObject(target, "Edit Unique ID");
						uniqueIDProp.stringValue = newUniqueID;
						serializedObject.ApplyModifiedProperties();
						isEditing = false;
						EditorUtility.SetDirty(target);

						SceneObjectID sceneObjectID = (SceneObjectID)target;
						Logger.Log($"SceneObjectIDEditor: Edited UniqueID to '{newUniqueID}' for '{sceneObjectID.gameObject.name}'.");

						EditorUtility.DisplayDialog("Unique ID Edited", $"Unique ID has been updated to:\n{newUniqueID}", "OK");
					}
					else
					{
						EditorUtility.DisplayDialog("Invalid Unique ID",
							"The Unique ID cannot be null, empty, or consist solely of whitespace.", "OK");
					}
				}

				if (GUILayout.Button("Cancel"))
				{
					isEditing = false;
				}
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.EndVertical();
			}

			// Determine if the GameObject is a root object
			var sceneObj = target as SceneObjectID;
			bool isRootObject = sceneObj != null && sceneObj.transform.root == sceneObj.transform;

			// If not root, automatically set keepAcrossScenes to false
			if (!isRootObject && keepAcrossScenesProp.boolValue)
			{
				keepAcrossScenesProp.boolValue = false;
				serializedObject.ApplyModifiedProperties();
			}

			// Add a Toggle Button for 'keepAcrossScenes'
			EditorGUILayout.Space();
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();

			// Define button label based on current state
			string buttonLabel = keepAcrossScenesProp.boolValue ? "Disable Keep Across Scenes" : "Enable Keep Across Scenes";

			if (GUILayout.Button(buttonLabel, GUILayout.Width(200)))
			{
				ToggleKeepAcrossScenes(sceneObj, keepAcrossScenesProp.boolValue);
			}

			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();

			serializedObject.ApplyModifiedProperties();

			// ───────────── Validation (on-demand) ─────────────
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Scan duplicates (Active Scene)", GUILayout.Width(230)))
			{
				_lastScanMessage = ScanForDuplicateIDsInActiveScene();
				_lastScanTime = DateTime.Now;
			}
			EditorGUILayout.EndHorizontal();
			if (!string.IsNullOrEmpty(_lastScanMessage))
			{
				EditorGUILayout.HelpBox(_lastScanMessage, MessageType.Info);
			}
		}

		/// <summary>
		/// Toggles the 'keepAcrossScenes' property and synchronizes it with SaveableComponent.
		/// Ensures that DontDestroyOnLoad is only called during play mode.
		/// </summary>
		/// <param name="sceneObjectID">The SceneObjectID component.</param>
		/// <param name="currentState">The current state of 'keepAcrossScenes'.</param>
		private void ToggleKeepAcrossScenes(SceneObjectID sceneObjectID, bool currentState)
		{
			// Toggle the flag
			bool newState = !currentState;

			// Record undo operation
			Undo.RecordObject(sceneObjectID, "Toggle Keep Across Scenes");

			// Update SceneObjectID's keepAcrossScenes
			keepAcrossScenesProp.boolValue = newState;

			// Find and synchronize SaveableComponent if present
			SaveableComponent saveableComponent = sceneObjectID.GetComponent<SaveableComponent>();
			if (saveableComponent != null)
			{
				Undo.RecordObject(saveableComponent, "Synchronize Keep Across Scenes");
				saveableComponent.KeepAcrossScenes = newState;
				EditorUtility.SetDirty(saveableComponent);
			}

			// Apply changes
			serializedObject.ApplyModifiedProperties();
			EditorUtility.SetDirty(sceneObjectID);

			// Handle persistence based on play mode
			if (newState)
			{
				if (EditorApplication.isPlaying)
				{
					// Call MakePersistent only during play mode
					PersistentManager.MakePersistent(sceneObjectID.gameObject, true);
					Debug.Log($"'{sceneObjectID.gameObject.name}' has been set to DontDestroyOnLoad.", sceneObjectID);
				}
				else
				{
					Debug.LogWarning($"Cannot enable Keep Across Scenes for '{sceneObjectID.gameObject.name}' outside of play mode. The change will take effect during play mode.", sceneObjectID);
				}
			}
			else
			{
				// Disabling Keep Across Scenes is not straightforward as Unity cannot unset DontDestroyOnLoad.
				// We'll log a warning and set the flags accordingly.
				Debug.LogWarning($"'{sceneObjectID.gameObject.name}' cannot be unset from DontDestroyOnLoad at runtime. Consider scene management strategies.", sceneObjectID);
				// Note: The GameObject remains persistent if it was already set.
			}
		}

		/// <summary>
		/// Validates the new Unique ID to ensure it's not null, empty, or whitespace.
		/// </summary>
		/// <param name="id">The new Unique ID string.</param>
		/// <returns>True if valid; otherwise, false.</returns>
		private bool IsValidUniqueID(string id)
		{
			return !string.IsNullOrWhiteSpace(id);
		}

		/// <summary>
		/// Generates a new GUID and assigns it to the 'uniqueID' property.
		/// </summary>
		private void GenerateNewUniqueID()
		{
			string generatedID = Guid.NewGuid().ToString();

			// Register Undo operation
			Undo.RecordObject(target, "Generate New Unique ID");

			// Assign the generated ID
			uniqueIDProp.stringValue = generatedID;
			serializedObject.ApplyModifiedProperties();

			// Mark the object as dirty to ensure the change is saved
			EditorUtility.SetDirty(target);

			// Log the change
			SceneObjectID sceneObjectID = (SceneObjectID)target;
			Logger.Log($"SceneObjectIDEditor: Generated new UniqueID '{generatedID}' for '{sceneObjectID.gameObject.name}'.");

			// Notify the user
			EditorUtility.DisplayDialog("New Unique ID Generated", $"A new Unique ID has been generated:\n{generatedID}", "OK");
		}

		/// <summary>
		/// Scans only the active scene for duplicate IDs matching the currently selected object's ID.
		/// </summary>
		private string ScanForDuplicateIDsInActiveScene()
		{
			var active = SceneManager.GetActiveScene();
			if (!active.IsValid())
				return "No active scene.";

			var roots = active.GetRootGameObjects();
			List<SceneObjectID> all = new();
			foreach (var root in roots)
				all.AddRange(root.GetComponentsInChildren<SceneObjectID>(true));

			string currentID = uniqueIDProp?.stringValue;
			if (string.IsNullOrEmpty(currentID))
				return "Current object has no Unique ID set.";

			int count = 0;
			List<GameObject> dupObjects = new();
			foreach (var soid in all)
			{
				if (soid == null) continue;
				if (string.Equals(soid.UniqueID, currentID, StringComparison.Ordinal))
				{
					count++;
					if (soid.gameObject != ((SceneObjectID)target).gameObject)
						dupObjects.Add(soid.gameObject);
				}
			}

			if (count <= 1)
				return $"No duplicates found for ID '{currentID}' in active scene '{active.name}'.";

		// Log once per found duplicate
		foreach (var go in dupObjects)
		{
			string warningKey = $"{currentID}-{UnityObjectHelper.GetUniqueId(go)}";
			if (ShouldLogWarning(warningKey))
			{
				Debug.LogWarning($"Duplicate Unique ID detected: '{currentID}' on GameObject <b><a href='context:{UnityObjectHelper.GetUniqueId(go)}'>\"{go.name}\"</a></b> in active scene.", go);
				UpdateLastLoggedTime(warningKey);
			}
		}			return $"Found {count - 1} duplicate(s) of '{currentID}' in active scene '{active.name}'.";
		}

		/// <summary>
		/// Determines if a warning should be logged based on the last logged time.
		/// </summary>
		private bool ShouldLogWarning(string warningKey)
		{
			if (!lastLoggedWarnings.TryGetValue(warningKey, out var lastLoggedTime))
			{
				// If no record exists, log immediately
				return true;
			}

			// Log if the specified interval has passed
			return (DateTime.Now - lastLoggedTime).TotalSeconds >= WarningIntervalSeconds;
		}

		/// <summary>
		/// Updates the last logged time for a warning.
		/// </summary>
		private void UpdateLastLoggedTime(string warningKey)
		{
			lastLoggedWarnings[warningKey] = DateTime.Now;
		}

		private static void ClearLoggedWarnings()
		{
			lastLoggedWarnings.Clear();
		}

		// Removed global hierarchyChanged scanning; auto-assignment now happens in SceneObjectID.Reset/OnValidate.
	}
}
#endif
#endif