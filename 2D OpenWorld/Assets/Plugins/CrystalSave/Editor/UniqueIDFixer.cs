#if MEMORYPACK && ARAWN_REMEMBERME && ARAWN_DEACTIVATED
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Editor
{
	public class UniqueIDFixer
	{
		private static Queue<Component> objectsToProcess = new Queue<Component>();
		private static int fixedCount = 0;

		[MenuItem("Tools/Crystal Save/Scene/Fix all Duplicate UniqueIDs")]
		public static void FixAllDuplicateUniqueIDs()
		{
			// Prepare queue for UniqueID components only
			PrepareQueue<UniqueID>();

			// Reset counters
			fixedCount = 0;

			// Start processing via EditorApplication.update
			if (objectsToProcess.Count > 0)
			{
				EditorApplication.update += ProcessQueue;
			}
			else
			{
				EditorUtility.DisplayDialog("Fix Complete", "No duplicate or empty UniqueIDs were found.", "OK");
			}
		}

		private static void PrepareQueue<T>() where T : Component
		{
			T[] allComponents = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

			// Track unique IDs to detect duplicates
			HashSet<string> uniqueIDSet = new HashSet<string>();

			foreach (var component in allComponents)
			{
				string id = GetID(component);

				// Queue objects with empty or duplicate IDs
				if (string.IsNullOrEmpty(id) || !uniqueIDSet.Add(id))
				{
					objectsToProcess.Enqueue(component);
				}
			}
		}

		private static void ProcessQueue()
		{
			// Number of components to process per frame
			const int batchSize = 10;

			for (int i = 0; i < batchSize && objectsToProcess.Count > 0; i++)
			{
				var component = objectsToProcess.Dequeue();
				string newID = Guid.NewGuid().ToString();

				// Safely update the ID
				SerializedObject serializedObject = new SerializedObject(component);
				SerializedProperty idProperty = serializedObject.FindProperty("id"); // UniqueID has 'id' property
				idProperty.stringValue = newID;
				serializedObject.ApplyModifiedProperties();

				// Mark the object as dirty
				EditorUtility.SetDirty(component);

				// Log the fix
				Debug.Log($"UniqueIDFixer: Assigned new ID '{newID}' to '{component.gameObject.name}' ({component.GetType().Name}).");
				fixedCount++;
			}

			// If all components are processed, stop the update callback
			if (objectsToProcess.Count == 0)
			{
				EditorApplication.update -= ProcessQueue;

				// Display results
				EditorUtility.DisplayDialog("Fix Complete",
					$"Fixed {fixedCount} duplicate or missing UniqueIDs.", "OK");
			}
		}

		private static string GetID(Component component)
		{
			return component is UniqueID uniqueID
				? uniqueID.ID
				: string.Empty;
		}
	}
}
#endif
#endif