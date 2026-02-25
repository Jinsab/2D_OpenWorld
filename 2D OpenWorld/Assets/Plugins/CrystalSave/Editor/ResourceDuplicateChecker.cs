#if MEMORYPACK
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace Arawn.CrystalSave.Runtime
{
	public static class ResourceDuplicatesChecker
	{
		[MenuItem("Tools/Crystal Save/Project/Check Resource Duplicates")]
		public static void CheckResourceDuplicateNames()
		{
			// Find all assets in the "Assets" folder
			string[] allAssetGuids = AssetDatabase.FindAssets("", new[] { "Assets" });

			// Dictionary to track fileName => list of asset paths that have this fileName
			Dictionary<string, List<string>> nameToPathsMap = new Dictionary<string, List<string>>();

			foreach (string guid in allAssetGuids)
			{
				string assetPath = AssetDatabase.GUIDToAssetPath(guid);

				// We're only interested if the path has a "/Resources/" folder in it
				if (assetPath.ToLower().Contains("/resources/"))
				{
					// Skip if this path is a folder
					if (AssetDatabase.IsValidFolder(assetPath))
					{
						// If it's a folder, ignore it; we don't want to warn about folder name duplicates.
						continue;
					}

					// Get the file name without extension (e.g., "Prefab_Cube")
					string fileName = Path.GetFileNameWithoutExtension(assetPath);

					if (!nameToPathsMap.ContainsKey(fileName))
					{
						nameToPathsMap[fileName] = new List<string>();
					}

					nameToPathsMap[fileName].Add(assetPath);
				}
			}

			// Now we have a map of "fileName" -> "list of full paths"
			// Let's report duplicates (files only)
			bool foundDuplicates = false;
			foreach (var kvp in nameToPathsMap)
			{
				if (kvp.Value.Count > 1)
				{
					foundDuplicates = true;
					Debug.LogWarning(
						$"Duplicate Resource name detected: \"{kvp.Key}\" " +
						$"found in the following paths:\n - {string.Join("\n - ", kvp.Value)}"
					);
				}
			}

			if (!foundDuplicates)
			{
				Debug.Log("No duplicate resource names found. All good!");
			}
		}
	}
}

#endif