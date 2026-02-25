#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Editor
{
	public class UniqueIDChecker : EditorWindow
	{
		[MenuItem("Tools/Crystal Save/Runtime Debug/Check Duplicate UniqueIDs")]
		public static void ShowWindow()
		{
			GetWindow<UniqueIDChecker>("UniqueID Checker");
		}

		private void OnGUI()
		{
			if (GUILayout.Button("Check UniqueIDs"))
			{
				CheckUniqueIDs();
			}
		}

		private void CheckUniqueIDs()
		{
#pragma warning disable CS0618 // Suppress FindObjectsByType deprecation warning for cross-version compatibility
			var saveablePrefabs = UnityEngine.Object.FindObjectsByType<SaveablePrefab>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#pragma warning restore CS0618
			Dictionary<string, List<SaveablePrefab>> idMap = new Dictionary<string, List<SaveablePrefab>>();

			foreach (var prefab in saveablePrefabs)
			{
				if (string.IsNullOrEmpty(prefab.UniqueID))
				{
					Debug.LogWarning($"SaveablePrefab '{prefab.gameObject.name}' has an empty uniqueID.");
					continue;
				}

				if (!idMap.ContainsKey(prefab.UniqueID))
				{
					idMap[prefab.UniqueID] = new List<SaveablePrefab>();
				}

				idMap[prefab.UniqueID].Add(prefab);
			}

			bool hasDuplicates = false;
		foreach (var kvp in idMap)
		{
			if (kvp.Value.Count > 1)
			{
				hasDuplicates = true;
				Debug.LogError($"Duplicate uniqueID '{kvp.Key}' found in the following SaveablePrefabs:");
				foreach (var prefab in kvp.Value)
				{
					Debug.LogError($" - {prefab.gameObject.name} (Instance ID: {UnityObjectHelper.GetUniqueId(prefab)})");
				}
			}
		}			if (!hasDuplicates)
			{
				Debug.Log("No duplicate uniqueIDs found among SaveablePrefabs.");
			}
		}
	}
#endif
}
#endif