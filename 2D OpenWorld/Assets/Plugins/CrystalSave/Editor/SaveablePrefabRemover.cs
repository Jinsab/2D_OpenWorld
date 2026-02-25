#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR && REMEMBERME_EDITOR_COROUTINES_PRESENT
using UnityEditor;
using UnityEngine;
using System.Collections;
using Arawn.CrystalSave.Runtime;
using Unity.EditorCoroutines.Editor;
using UnityEditor.SceneManagement;
using Logger = Arawn.CrystalSave.Runtime.Logger;

namespace Arawn.CrystalSave.Editor
{
	public class SaveablePrefabRemover
	{
		private const string MenuPath = "Tools/Crystal Save/Project/Remove Rememember Prefab Components from All Prefabs";
		private const string ConfirmationTitle = "Confirm Removal";
		private const string ConfirmationMessage = "Are you sure you want to remove all Remember Prefab components from all Prefabs in the Assets folder and its subfolders?\n\nThis action cannot be undone via this menu entry, but can be reverted using version control or by re-importing prefabs.";
		private const string RemovalCompleteTitle = "Removal Complete";
		private const int BatchSize = 50; // Adjust based on performance testing

		[MenuItem(MenuPath)]
		public static void RemoveAllSaveablePrefabComponents()
		{
			// Show a confirmation dialog to prevent accidental execution
			bool confirm = EditorUtility.DisplayDialog(
				ConfirmationTitle,
				ConfirmationMessage,
				"Yes, Remove All",
				"Cancel"
			);

			if (!confirm)
			{
				Debug.Log("SaveablePrefabRemover: Operation canceled by the user.");
				return;
			}

			// Start the asynchronous removal process
			EditorCoroutineUtility.StartCoroutineOwnerless(RemoveComponentsCoroutine());
		}

		private static IEnumerator RemoveComponentsCoroutine()
		{
			// Find all prefab asset GUIDs in the Assets folder and its subfolders
			string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
			int totalPrefabs = prefabGuids.Length;
			int processed = 0;
			int removedCount = 0;

			for (int i = 0; i < prefabGuids.Length; i += BatchSize)
			{
				for (int j = i; j < Mathf.Min(i + BatchSize, prefabGuids.Length); j++)
				{
					string guid = prefabGuids[j];
					string assetPath = AssetDatabase.GUIDToAssetPath(guid);

					// Double-check to ensure the asset is within the Assets folder
					if (!IsInAssetsFolder(assetPath))
					{
						processed++;
						continue; // Skip assets not in Assets folder
					}

					GameObject prefab = null;
					GameObject prefabContents = null;
#pragma warning disable CS0219 // Variable is assigned but its value is never used
					bool saveSuccess = false;
#pragma warning restore CS0219 // Variable is assigned but its value is never used

					try
					{
						prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

						if (prefab != null)
						{
							// Avoid processing Prefabs currently open in PrefabStage
							PrefabStage prefabStage = PrefabStageUtility.GetPrefabStage(prefab);
							if (prefabStage == null)
							{
								prefabContents = PrefabUtility.LoadPrefabContents(assetPath);
								if (prefabContents != null)
								{
									// Register undo for the prefab contents

									Undo.RegisterCompleteObjectUndo(prefabContents, "Remove SaveablePrefab Component");

									// Check and remove the SaveablePrefab component if it exists
									SaveablePrefab saveablePrefab = prefabContents.GetComponent<SaveablePrefab>();
									if (saveablePrefab != null)
									{
										Undo.DestroyObjectImmediate(saveablePrefab);
										removedCount++;
										Debug.Log($"SaveablePrefab removed from {assetPath}");
									}
									else
									{
										Debug.Log($"No SaveablePrefab found on {assetPath}");
									}

									// Save the prefab asset using the appropriate method
									PrefabUtility.SaveAsPrefabAsset(prefabContents, assetPath);
									saveSuccess = true; // Assume success for this modified save method
								}
								else
								{
									Debug.LogWarning($"SaveablePrefabRemover: Could not load prefab contents at {assetPath}");
								}
							}
						}
					}
					catch (System.Exception ex)
					{
						Debug.LogError($"SaveablePrefabRemover: Error processing prefab at {assetPath}: {ex.Message}");
					}
					finally
					{
						if (prefabContents != null)
						{
							PrefabUtility.UnloadPrefabContents(prefabContents);
						}
					}

					// Update progress bar
					processed++;
					float progress = (float)processed / totalPrefabs;
					EditorUtility.DisplayProgressBar("Removing SaveablePrefab Components", $"Processing {processed} of {totalPrefabs} Prefabs...", progress);
				}

				// Yield after each batch to allow the Editor to remain responsive
				yield return null;
			}

			// Clear the progress bar
			EditorUtility.ClearProgressBar();

			// Display a result dialog
			if (removedCount > 0)
			{
				EditorUtility.DisplayDialog(RemovalCompleteTitle,
					$"Removed {removedCount} SaveablePrefab components from the project.", "OK");
			}
			else
			{
				EditorUtility.DisplayDialog(RemovalCompleteTitle,
					"No SaveablePrefab components were found in the project.", "OK");
			}

			Debug.Log($"SaveablePrefabRemover: Removed {removedCount} SaveablePrefab components from the project.");
		}

		/// <summary>
		/// Checks if the given asset path is within the Assets folder or its subfolders.
		/// Excludes paths outside Assets (e.g., Packages).
		/// </summary>
		/// <param name="assetPath">The asset path to check.</param>
		/// <returns>True if within Assets, otherwise false.</returns>
		private static bool IsInAssetsFolder(string assetPath)
		{
			// Normalize path separators
			string normalizedPath = assetPath.Replace('\\', '/');

			// Ensure the path starts with "Assets/"
			return normalizedPath.StartsWith("Assets/");
		}
	}
}
#endif
#endif