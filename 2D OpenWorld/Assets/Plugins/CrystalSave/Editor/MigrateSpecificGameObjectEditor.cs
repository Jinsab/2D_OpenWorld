// MigrateSpecificGameObjectEditor.cs
#if MEMORYPACK && ARAWN_REMEMBERME
using Arawn.CrystalSave.Runtime;
using UnityEngine;
using UnityEditor;

namespace Arawn.CrystalSave.Editor
{
	[CustomEditor(typeof(MigrateSpecificGameObject))]
	public class MigrateSpecificGameObjectEditor : UnityEditor.Editor
	{
		// Serialized properties for better handling
		SerializedProperty targetUniqueIDProp;
		SerializedProperty newNameProp;
		SerializedProperty newLayerProp;
		SerializedProperty newTagProp;
		SerializedProperty newIsActiveProp;

		// Selected GameObject in the scene
		GameObject selectedGameObject;

		private void OnEnable()
		{
			// Link serialized properties
			targetUniqueIDProp = serializedObject.FindProperty("targetUniqueID");
			newNameProp = serializedObject.FindProperty("newName");
			newLayerProp = serializedObject.FindProperty("newLayer");
			newTagProp = serializedObject.FindProperty("newTag");
			newIsActiveProp = serializedObject.FindProperty("newIsActive");
		}

		public override void OnInspectorGUI()
		{
			// Update serialized object
			serializedObject.Update();

			// Draw the default inspector properties
			EditorGUILayout.PropertyField(targetUniqueIDProp);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("New GameObject Properties", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(newNameProp);
			EditorGUILayout.PropertyField(newLayerProp);
			EditorGUILayout.PropertyField(newTagProp);
			EditorGUILayout.PropertyField(newIsActiveProp);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("GameObject Copier", EditorStyles.boldLabel);

			// Select GameObject field
			selectedGameObject = (GameObject)EditorGUILayout.ObjectField("Source GameObject", selectedGameObject, typeof(GameObject), true);

			if (GUILayout.Button("Copy GameObject Properties from Selected GameObject"))
			{
				CopyGameObjectProperties();
			}

			// Apply changes to the serialized object
			serializedObject.ApplyModifiedProperties();
		}

		private void CopyGameObjectProperties()
		{
			if (selectedGameObject == null)
			{
				EditorUtility.DisplayDialog("No GameObject Selected", "Please select a GameObject from the scene to copy its properties.", "OK");
				return;
			}

			// Attempt to get the UniqueID component
			UniqueID uniqueIDComponent = selectedGameObject.GetComponent<UniqueID>();

			if (uniqueIDComponent == null)
			{
				bool proceed = EditorUtility.DisplayDialog(
					"UniqueID Component Missing",
					$"The selected GameObject '{selectedGameObject.name}' does not have a UniqueID component.\n\nDo you want to proceed without copying the Unique Identifier?",
					"Yes",
					"No"
				);

				if (!proceed)
				{
					return;
				}
			}

			// Attempt to get the RememberGameObject component
			RememberGameObject rememberGameObjectComponent = selectedGameObject.GetComponent<RememberGameObject>();

			if (rememberGameObjectComponent == null)
			{
				bool proceed = EditorUtility.DisplayDialog(
					"RememberGameObject Component Missing",
					$"The selected GameObject '{selectedGameObject.name}' does not have a RememberGameObject component.\n\nMigration requires this component to retrieve the combined Unique Identifier.\n\nDo you want to proceed without copying the Unique Identifier?",
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
			MigrateSpecificGameObject migrationAction = (MigrateSpecificGameObject)target;

			// Record the current state for Undo
			Undo.RecordObject(migrationAction, "Copy GameObject Properties and Unique Identifier Values");

			// Assign GameObject Properties
			newNameProp.stringValue = selectedGameObject.name;
			newLayerProp.intValue = selectedGameObject.layer;
			newTagProp.stringValue = selectedGameObject.tag;
			newIsActiveProp.boolValue = selectedGameObject.activeSelf;

			// Assign targetUniqueID with combined Unique Identifier
			targetUniqueIDProp.stringValue = uniqueIdentifier;

			// Mark the ScriptableObject as dirty to ensure changes are saved
			EditorUtility.SetDirty(migrationAction);

			// Refresh the Inspector
			Repaint();

			// Prepare the success message
			string message = $"GameObject properties from '{selectedGameObject.name}' have been copied to '{migrationAction.name}'.";
			message += $"\nUnique Identifier '{uniqueIdentifier}' has been copied to 'targetUniqueID'.";

			EditorUtility.DisplayDialog("GameObject Properties Copied", message, "OK");
		}
	}
}
#endif