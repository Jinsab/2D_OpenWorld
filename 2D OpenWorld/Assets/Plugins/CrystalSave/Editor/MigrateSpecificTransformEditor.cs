#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;
using UnityEditor;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Runtime
{
	[CustomEditor(typeof(MigrateSpecificTransform))]
	public class MigrateSpecificTransformEditor : UnityEditor.Editor
	{
		// Serialized properties for better handling
		SerializedProperty targetUniqueIDProp;
		SerializedProperty newPositionProp;
		SerializedProperty newEulerRotationProp;
		SerializedProperty newScaleProp;

		// Selected GameObject in the scene
		GameObject selectedGameObject;

		private void OnEnable()
		{
			// Link serialized properties
			targetUniqueIDProp = serializedObject.FindProperty("targetUniqueID");
			newPositionProp = serializedObject.FindProperty("newPosition");
			newEulerRotationProp = serializedObject.FindProperty("newEulerRotation");
			newScaleProp = serializedObject.FindProperty("newScale");
		}

		public override void OnInspectorGUI()
		{
			// Update serialized object
			serializedObject.Update();

			// Draw the default inspector properties
			EditorGUILayout.PropertyField(targetUniqueIDProp);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("New Transform Values", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(newPositionProp);
			EditorGUILayout.PropertyField(newEulerRotationProp);
			EditorGUILayout.PropertyField(newScaleProp);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Transform Copier", EditorStyles.boldLabel);

			// Select GameObject field
			selectedGameObject = (GameObject)EditorGUILayout.ObjectField("Source GameObject", selectedGameObject, typeof(GameObject), true);

			if (GUILayout.Button("Copy Transform from Selected GameObject"))
			{
				CopyTransformFromSelectedGameObject();
			}

			// Apply changes to the serialized object
			serializedObject.ApplyModifiedProperties();
		}

		private void CopyTransformFromSelectedGameObject()
		{
			if (selectedGameObject == null)
			{
				EditorUtility.DisplayDialog("No GameObject Selected", "Please select a GameObject from the scene to copy its Transform.", "OK");
				return;
			}

			// Attempt to get the UniqueID component
			UniqueID uniqueIDComponent = selectedGameObject.GetComponent<UniqueID>();

			if (uniqueIDComponent == null)
			{
				bool proceed = EditorUtility.DisplayDialog(
					"UniqueID Component Missing",
					$"The selected GameObject '{selectedGameObject.name}' does not have a UniqueID component.\n\nDo you want to proceed without copying the UniqueID?",
					"Yes",
					"No"
				);

				if (!proceed)
				{
					return;
				}
			}

			// Attempt to get the RememberTransform component
			RememberTransform rememberTransformComponent = selectedGameObject.GetComponent<RememberTransform>();

			if (rememberTransformComponent == null)
			{
				bool proceed = EditorUtility.DisplayDialog(
					"RememberTransform Component Missing",
					$"The selected GameObject '{selectedGameObject.name}' does not have a RememberTransform component.\n\nMigration requires this component to retrieve the combined Unique Identifier.\n\nDo you want to proceed without copying the Unique Identifier?",
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

			if (uniqueIDComponent != null && rememberTransformComponent != null)
			{
				uniqueIdentifier = $"{uniqueIDComponent.ID}_{rememberTransformComponent.ComponentID}";
			}
			else if (uniqueIDComponent != null)
			{
				uniqueIdentifier = uniqueIDComponent.ID;
			}
			else if (rememberTransformComponent != null)
			{
				uniqueIdentifier = rememberTransformComponent.ComponentID;
			}

			if (string.IsNullOrEmpty(uniqueIdentifier))
			{
				EditorUtility.DisplayDialog("Unique Identifier Missing", "Cannot determine Unique Identifier. Ensure that either UniqueID or RememberTransform component is present.", "OK");
				return;
			}

			Transform sourceTransform = selectedGameObject.transform;
			MigrateSpecificTransform migrationAction = (MigrateSpecificTransform)target;

			// Record the current state for Undo
			Undo.RecordObject(migrationAction, "Copy Transform and UniqueIdentifier Values");

			// Assign Position
			newPositionProp.vector3Value = sourceTransform.localPosition;

			// Assign Rotation (Euler angles)
			newEulerRotationProp.vector3Value = sourceTransform.localEulerAngles;

			// Assign Scale
			newScaleProp.vector3Value = sourceTransform.localScale;

			// Assign targetUniqueID with combined Unique Identifier
			targetUniqueIDProp.stringValue = uniqueIdentifier;

			// Mark the scriptable object as dirty to ensure changes are saved
			EditorUtility.SetDirty(migrationAction);

			// Refresh the Inspector
			Repaint();

			// Prepare the success message
			string message = $"Transform values from '{selectedGameObject.name}' have been copied to '{migrationAction.name}'.";
			message += $"\nUnique Identifier '{uniqueIdentifier}' has been copied to 'targetUniqueID'.";

			EditorUtility.DisplayDialog("Transform Copied", message, "OK");
		}
	}
}
#endif