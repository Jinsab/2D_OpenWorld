using UnityEngine;
using System;
using System.Collections.Generic;

namespace Arawn.CrystalSave.Runtime
{
	[CreateAssetMenu(fileName = "SceneObjectRegistry", menuName = "Crystal Save/Settings/SceneObjectRegistry", order = 1)]
	public class SceneObjectRegistry : ScriptableObject
	{
		[Serializable]
		public class SceneObjectEntry
		{
			[SerializeField]
			public string UniqueID;

			[SerializeField]
			public GameObject PrefabAsset;
		}

		[SerializeField]
		private List<SceneObjectEntry> entries = new List<SceneObjectEntry>();
		public List<SceneObjectEntry> Entries
		{
			get { return entries; }
			set { entries = value; }
		}

		// Provide a dictionary-like interface for SaveManager
		public Dictionary<string, GameObject> GetPrefabMappings()
		{
			var mappings = new Dictionary<string, GameObject>();
			foreach (var entry in entries)
			{
				if (!string.IsNullOrEmpty(entry.UniqueID) && entry.PrefabAsset != null)
				{
					mappings[entry.UniqueID] = entry.PrefabAsset;
				}
                                else
                                {
                                        Logger.Log($"SceneObjectRegistry: Invalid entry - UniqueID: '{entry.UniqueID}', PrefabAsset: {(entry.PrefabAsset != null ? entry.PrefabAsset.name : "null")}", LogLevel.Warning);
                                }
			}
			return mappings;
		}

		// Optional: Validation in Editor
		private void OnValidate()
		{
			HashSet<string> seenIds = new HashSet<string>();
			for (int i = entries.Count - 1; i >= 0; i--)
			{
				var entry = entries[i];
                                if (string.IsNullOrEmpty(entry.UniqueID) || entry.PrefabAsset == null)
                                {
                                        Logger.Log($"SceneObjectRegistry: Entry {i} has invalid UniqueID or PrefabAsset. Consider fixing or removing.", LogLevel.Warning);
                                }
                                else if (!seenIds.Add(entry.UniqueID))
                                {
                                        Logger.Log($"SceneObjectRegistry: Duplicate UniqueID '{entry.UniqueID}' found. Only the first occurrence will be used.", LogLevel.Warning);
                                }
			}
		}
	}
}