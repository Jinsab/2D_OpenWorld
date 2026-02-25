#if MEMORYPACK && ARAWN_REMEMBERME
using Arawn.CrystalSave.Runtime;
using UnityEngine;
using UnityEditor;

namespace Arawn.CrystalSave.Editor
{
	[CustomEditor(typeof(MigrateSpecificParent))]
	public class MigrateSpecificParentEditor : UnityEditor.Editor
	{
		// Serialized properties for better handling
		SerializedProperty targetComponentUniqueIDProp;
		SerializedProperty newParentUniqueIDProp;

		// Selected GameObjects in the scene
		GameObject sourceGameObject;
		GameObject newParentGameObject;

		private void OnEnable()
		{
			// Link serialized properties
			targetComponentUniqueIDProp = serializedObject.FindProperty("targetComponentUniqueID");
			newParentUniqueIDProp = serializedObject.FindProperty("newParentUniqueID");
		}

		public override void OnInspectorGUI()
		{
			// Update serialized object
			serializedObject.Update();

			// Draw the default inspector properties
			EditorGUILayout.PropertyField(targetComponentUniqueIDProp, new GUIContent("Target Component Unique ID"));

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("New Parent Information", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(newParentUniqueIDProp, new GUIContent("New Parent Unique ID"));

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Migration Copier", EditorStyles.boldLabel);

			// Source Component Copier
			EditorGUILayout.LabelField("Target Component Copier", EditorStyles.boldLabel);
			sourceGameObject = (GameObject)EditorGUILayout.ObjectField("Source GameObject", sourceGameObject, typeof(GameObject), true);

			if (GUILayout.Button("Copy Target Component Unique Identifier"))
			{
				CopyTargetComponentUniqueID();
			}

			EditorGUILayout.Space();

			// New Parent Copier
			EditorGUILayout.LabelField("New Parent Copier", EditorStyles.boldLabel);
			newParentGameObject = (GameObject)EditorGUILayout.ObjectField("New Parent GameObject", newParentGameObject, typeof(GameObject), true);

			if (GUILayout.Button("Copy New Parent Unique Identifier"))
			{
				CopyNewParentUniqueID();
			}

			EditorGUILayout.Space();

			// Optionally, provide a button to clear the newParentUniqueID to remove the parent
			if (!string.IsNullOrEmpty(newParentUniqueIDProp.stringValue))
			{
				if (GUILayout.Button("Clear New Parent Unique Identifier (Remove Parent)"))
				{
					ClearNewParentUniqueID();
				}
			}

			// Apply changes to the serialized object
			serializedObject.ApplyModifiedProperties();
		}

		/// <summary>
		/// Copies the Unique Identifier from the RememberParent component of the selected source GameObject.
		/// </summary>
		private void CopyTargetComponentUniqueID()
		{
			if (sourceGameObject == null)
			{
				EditorUtility.DisplayDialog("No GameObject Selected", "Please select a Source GameObject from the scene to copy its Target Component Unique Identifier.", "OK");
				return;
			}

			// Attempt to get the UniqueID component
			UniqueID uniqueIDComponent = sourceGameObject.GetComponent<UniqueID>();

			if (uniqueIDComponent == null)
			{
				bool proceed = EditorUtility.DisplayDialog(
					"UniqueID Component Missing",
					$"The selected GameObject '{sourceGameObject.name}' does not have a UniqueID component.\n\nDo you want to proceed without copying the Unique Identifier?",
					"Yes",
					"No"
				);

				if (!proceed)
				{
					return;
				}
			}

			// Attempt to get the RememberParent component
			RememberParent rememberParentComponent = sourceGameObject.GetComponent<RememberParent>();

			if (rememberParentComponent == null)
			{
				bool proceed = EditorUtility.DisplayDialog(
					"RememberParent Component Missing",
					$"The selected GameObject '{sourceGameObject.name}' does not have a RememberParent component.\n\nMigration requires this component to retrieve the combined Unique Identifier.\n\nDo you want to proceed without copying the Unique Identifier?",
					"Yes",
					"No"
				);

				if (!proceed)
				{
					return;
				}
			}

			// Prepare the Unique Identifier
			string uniqueIdentifier = string.Empty;

			if (uniqueIDComponent != null && rememberParentComponent != null)
			{
				uniqueIdentifier = $"{uniqueIDComponent.ID}_{rememberParentComponent.ComponentID}";
			}
			else if (uniqueIDComponent != null)
			{
				uniqueIdentifier = uniqueIDComponent.ID;
			}
			else if (rememberParentComponent != null)
			{
				uniqueIdentifier = rememberParentComponent.ComponentID;
			}

			if (string.IsNullOrEmpty(uniqueIdentifier))
			{
				EditorUtility.DisplayDialog("Unique Identifier Missing", "Cannot determine Unique Identifier. Ensure that either UniqueID or RememberParent component is present.", "OK");
				return;
			}

			// Reference to the MigrationAction ScriptableObject
			MigrateSpecificParent migrationAction = (MigrateSpecificParent)target;

			// Record the current state for Undo
			Undo.RecordObject(migrationAction, "Copy Target Component Unique Identifier");

			// Assign targetComponentUniqueID with combined Unique Identifier
			targetComponentUniqueIDProp.stringValue = uniqueIdentifier;

			// Mark the ScriptableObject as dirty to ensure changes are saved
			EditorUtility.SetDirty(migrationAction);

			// Refresh the Inspector
			Repaint();

			// Prepare the success message
			string message = $"Unique Identifier '{uniqueIdentifier}' from '{sourceGameObject.name}' has been copied to '{migrationAction.name}' as 'targetComponentUniqueID'.";

			EditorUtility.DisplayDialog("Unique Identifier Copied", message, "OK");
		}

		/// <summary>
		/// Copies the Unique Identifier from the RememberGameObject component of the selected new parent GameObject.
		/// </summary>
		private void CopyNewParentUniqueID()
		{
			if (newParentGameObject == null)
			{
				EditorUtility.DisplayDialog("No GameObject Selected", "Please select a New Parent GameObject from the scene to copy its Unique Identifier.", "OK");
				return;
			}

			// Attempt to get the UniqueID component
			UniqueID uniqueIDComponent = newParentGameObject.GetComponent<UniqueID>();

			if (uniqueIDComponent == null)
			{
				bool proceed = EditorUtility.DisplayDialog(
					"UniqueID Component Missing",
					$"The selected GameObject '{newParentGameObject.name}' does not have a UniqueID component.\n\nDo you want to proceed without copying the Unique Identifier?",
					"Yes",
					"No"
				);

				if (!proceed)
				{
					return;
				}
			}

			// Attempt to get the relevant SaveableComponent (e.g., RememberGameObject)
			RememberGameObject rememberGameObjectComponent = newParentGameObject.GetComponent<RememberGameObject>();

			if (rememberGameObjectComponent == null)
			{
				bool proceed = EditorUtility.DisplayDialog(
					"RememberGameObject Component Missing",
					$"The selected GameObject '{newParentGameObject.name}' does not have a RememberGameObject component.\n\nMigration requires this component to retrieve the combined Unique Identifier.\n\nDo you want to proceed without copying the Unique Identifier?",
					"Yes",
					"No"
				);

				if (!proceed)
				{
					return;
				}
			}

			// Prepare the Unique Identifier
			string uniqueIdentifier = string.Empty;

			if (uniqueIDComponent != null && rememberGameObjectComponent != null)
			{
				uniqueIdentifier = $"{uniqueIDComponent.ID}_{rememberGameObjectComponent.ComponentID}";
			}
			else if (uniqueIDComponent != null)
			{
				uniqueIdentifier = uniqueIDComponent.ID;
			}
			else if (rememberGameObjectComponent != null)
			{
				uniqueIdentifier = rememberGameObjectComponent.ComponentID;
			}

			if (string.IsNullOrEmpty(uniqueIdentifier))
			{
				EditorUtility.DisplayDialog("Unique Identifier Missing", "Cannot determine Unique Identifier. Ensure that either UniqueID or RememberGameObject component is present.", "OK");
				return;
			}

			// Reference to the MigrationAction ScriptableObject
			MigrateSpecificParent migrationAction = (MigrateSpecificParent)target;

			// Record the current state for Undo
			Undo.RecordObject(migrationAction, "Copy New Parent Unique Identifier");

			// Assign newParentUniqueID with combined Unique Identifier
			newParentUniqueIDProp.stringValue = uniqueIdentifier;

			// Mark the ScriptableObject as dirty to ensure changes are saved
			EditorUtility.SetDirty(migrationAction);

			// Refresh the Inspector
			Repaint();

			// Prepare the success message
			string message = $"Unique Identifier '{uniqueIdentifier}' from '{newParentGameObject.name}' has been copied to '{migrationAction.name}' as 'newParentUniqueID'.";

			EditorUtility.DisplayDialog("Unique Identifier Copied", message, "OK");
		}

		/// <summary>
		/// Clears the newParentUniqueID, effectively removing the parent.
		/// </summary>
		private void ClearNewParentUniqueID()
		{
			// Reference to the MigrationAction ScriptableObject
			MigrateSpecificParent migrationAction = (MigrateSpecificParent)target;

			// Record the current state for Undo
			Undo.RecordObject(migrationAction, "Clear New Parent Unique Identifier");

			// Clear the newParentUniqueID
			newParentUniqueIDProp.stringValue = string.Empty;

			// Mark the ScriptableObject as dirty to ensure changes are saved
			EditorUtility.SetDirty(migrationAction);

			// Refresh the Inspector
			Repaint();

			// Prepare the success message
			string message = $"The 'newParentUniqueID' for '{migrationAction.name}' has been cleared. The parent will be removed upon migration.";

			EditorUtility.DisplayDialog("New Parent Unique ID Cleared", message, "OK");
		}
	}
}
#endif