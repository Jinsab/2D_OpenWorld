using System.Collections.Generic;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	[CreateAssetMenu(fileName = "PrefabRegistry", menuName = "Crystal Save/Settings/Prefab Registry")]
	public class PrefabRegistry : ScriptableObject
	{
		[Tooltip("List of prefab entries mapping unique IDs to prefab references.")]
                public List<PrefabEntry> prefabEntries = new List<PrefabEntry>();

                private Dictionary<string, PrefabEntry> prefabDictionary;

                [System.Serializable]
                public class PrefabEntry
                {
                        [Tooltip("Unique identifier for the prefab asset.")]
                        public string uniqueID;

                        [Tooltip("Reference to the prefab asset.")]
                        public GameObject prefab;

                        [Tooltip("Pool size to maintain for this prefab when pooling is enabled.\n" +
                                 "Overrides the global default when greater than zero.")]
                        public int poolSize = 0;

                        [Tooltip("When enabled this prefab will always bypass pooling even if the global " +
                                 "Save Settings request prefab pooling.")]
                        public bool disablePooling = false;
                }

		private void OnEnable()
		{
			InitializeDictionary();
		}

		private void InitializeDictionary()
		{
                        prefabDictionary = new Dictionary<string, PrefabEntry>();
                        foreach (var entry in prefabEntries)
                        {
                                if (!prefabDictionary.ContainsKey(entry.uniqueID))
                                {
                                        prefabDictionary.Add(entry.uniqueID, entry);
                                }
                                else
                                {
                                        Logger.Log($"Duplicate uniqueID '{entry.uniqueID}' found in PrefabRegistry.", LogLevel.Warning);
                                }
                        }
                }

		public GameObject FindPrefabByID(string uniqueID)
		{
			if (prefabDictionary == null)
			{
				InitializeDictionary();
			}

                        if (prefabDictionary.TryGetValue(uniqueID, out var entry))
                        {
                                return entry.prefab;
                        }
                        return null;
                }

                public PrefabEntry FindEntryByID(string uniqueID)
                {
                        if (prefabDictionary == null)
                        {
                                InitializeDictionary();
                        }

                        prefabDictionary.TryGetValue(uniqueID, out var entry);
                        return entry;
                }

                public void AddPrefab(string uniqueID, GameObject prefab)
                {
                        if (FindPrefabByID(uniqueID) == null)
                        {
                                var entry = new PrefabEntry
                                {
                                        uniqueID = uniqueID,
                                        prefab = prefab,
                                        poolSize = 0,
                                        disablePooling = false
                                };
                                prefabEntries.Add(entry);
                                prefabDictionary.Add(uniqueID, entry); // Update dictionary
                                Logger.Log($"Added prefab '{prefab.name}' with ID '{uniqueID}' to PrefabRegistry.");
			}
			else
			{
				Logger.Log($"Prefab with ID '{uniqueID}' already exists in PrefabRegistry.", LogLevel.Warning);
			}
		}

                public bool RemovePrefab(string uniqueID, bool log = true)
		{
                        if (prefabDictionary == null)
                        {
                                InitializeDictionary();
                        }

                        var entry = prefabEntries.Find(e => e.uniqueID == uniqueID);
                        if (entry != null)
                        {
                                prefabEntries.Remove(entry);
                                prefabDictionary.Remove(uniqueID); // Update dictionary
                                if (log)
                                {
                                        Logger.Log($"Removed prefab with ID '{uniqueID}' from PrefabRegistry.");
                                }
                                return true;
                        }

                        if (log)
                        {
                                Logger.Log($"No prefab found with ID '{uniqueID}' in PrefabRegistry.", LogLevel.Warning);
                        }
                        return false;
		}

                public bool TryAddPrefab(string uniqueID, GameObject prefab, out PrefabEntry entry, out string reason)
                {
                        reason = null;
                        entry = null;

                        if (prefab == null)
                        {
                                reason = "Prefab reference cannot be null.";
                                return false;
                        }

                        if (prefabDictionary == null)
                        {
                                InitializeDictionary();
                        }

                        entry = FindEntryByPrefab(prefab);
                        if (entry != null)
                        {
                                if (!string.Equals(entry.uniqueID, uniqueID))
                                {
                                        if (prefabDictionary != null &&
                                            prefabDictionary.TryGetValue(uniqueID, out var existingEntry) &&
                                            existingEntry != entry)
                                        {
                                                reason = $"UniqueID '{uniqueID}' is already used by another prefab.";
                                                return false;
                                        }

                                        if (!string.IsNullOrEmpty(entry.uniqueID) &&
                                            prefabDictionary.ContainsKey(entry.uniqueID))
                                        {
                                                prefabDictionary.Remove(entry.uniqueID);
                                        }

                                        entry.uniqueID = uniqueID;
                                        prefabDictionary[uniqueID] = entry;

                                        return true;
                                }

                                reason = $"Prefab '{prefab.name}' is already registered.";
                                if (!string.IsNullOrEmpty(entry.uniqueID) &&
                                    !prefabDictionary.ContainsKey(entry.uniqueID))
                                {
                                        prefabDictionary.Add(entry.uniqueID, entry);
                                }
                                return false;
                        }

                        if (prefabDictionary != null && prefabDictionary.TryGetValue(uniqueID, out entry))
                        {
                                reason = $"UniqueID '{uniqueID}' is already used by another prefab.";
                                return false;
                        }

                        entry = new PrefabEntry
                        {
                                uniqueID = uniqueID,
                                prefab = prefab,
                                poolSize = 0,
                                disablePooling = false
                        };

                        prefabEntries.Add(entry);
                        prefabDictionary.Add(uniqueID, entry);
                        return true;
                }

                public bool TryAddPrefab(string uniqueID, GameObject prefab, out string reason)
                {
                        return TryAddPrefab(uniqueID, prefab, out _, out reason);
                }

                public bool IsPoolingDisabled(string uniqueID)
                {
                        if (string.IsNullOrEmpty(uniqueID)) return false;

                        var entry = FindEntryByID(uniqueID);
                        return entry != null && entry.disablePooling;
                }

                public PrefabEntry FindEntryByPrefab(GameObject prefab)
                {
                        if (prefab == null) return null;

                        foreach (var entry in prefabEntries)
                        {
                                if (entry.prefab == prefab)
                                        return entry;
                        }

                        return null;
                }

                public bool IsPoolingDisabled(GameObject prefab)
                {
                        var entry = FindEntryByPrefab(prefab);
                        return entry != null && entry.disablePooling;
                }

                public int ResolvePoolSize(string uniqueID, int defaultPoolSize)
                {
                        if (string.IsNullOrEmpty(uniqueID)) return defaultPoolSize;

                        var entry = FindEntryByID(uniqueID);
                        if (entry != null && entry.poolSize > 0)
                                return entry.poolSize;

                        return defaultPoolSize;
                }

        }
}
