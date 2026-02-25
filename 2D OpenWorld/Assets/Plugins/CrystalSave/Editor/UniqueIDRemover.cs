#if MEMORYPACK && ARAWN_REMEMBERME && ARAWN_DEACTIVATED
#if UNITY_EDITOR
using Arawn.CrystalSave.Runtime;
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
	public class UniqueIDRemover
	{
		[MenuItem("Tools/Crystal Save/Scene/Remove all UniqueID Components")]
		public static void RemoveAllUniqueIDComponents()
		{
			// Show a confirmation dialog to prevent accidental execution
			bool confirm = EditorUtility.DisplayDialog(
				"Confirm Removal",
				"Are you sure you want to remove all UniqueID components from all GameObjects in the scene?\n\nThis action cannot be undone via this menu entry, but can be reverted using the Unity Undo system.",
				"Yes, Remove All",
				"Cancel"
			);

			if (!confirm)
			{
				Debug.Log("UniqueIDRemover: Operation canceled by the user.");
				return;
			}

			// Find all GameObjects in the scene
			GameObject[] allGameObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

			int removedCount = 0; // Counter for removed components

			foreach (GameObject gameObject in allGameObjects)
			{
				// Get the UniqueID component, if it exists
				UniqueID uniqueID = gameObject.GetComponent<UniqueID>();
				if (uniqueID != null)
				{
					// Use Undo for safe removal and support for undo functionality
					Undo.DestroyObjectImmediate(uniqueID);
					removedCount++;
				}
			}

			// Display a result dialog
			if (removedCount > 0)
			{
				EditorUtility.DisplayDialog("Removal Complete",
					$"Removed {removedCount} UniqueID components from the scene.", "OK");
			}
			else
			{
				EditorUtility.DisplayDialog("Removal Complete",
					"No UniqueID components were found in the scene.", "OK");
			}

			Debug.Log($"UniqueIDRemover: Removed {removedCount} UniqueID components from the scene.");
		}
	}
}
#endif
#endif