#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;
using Logger = Arawn.CrystalSave.Runtime.Logger;

namespace Arawn.CrystalSave.Editor
{
	[CustomEditor(typeof(PrefabRegistry))]
	public class PrefabRegistryEditor : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			PrefabRegistry registry = (PrefabRegistry)target;

			// Track changes to automatically sync when registry is modified
			EditorGUI.BeginChangeCheck();

			/* ── 1. Scene-instance → prefab autocorrect ─────────────────── */
			for (int i = 0; i < registry.prefabEntries.Count; i++)
			{
				var e = registry.prefabEntries[i];
				if (e.prefab != null && PrefabUtility.IsPartOfPrefabInstance(e.prefab))
				{
					var asset = PrefabUtility.GetCorrespondingObjectFromSource(e.prefab);
					if (asset != null)
					{
						Logger.Log($"[Crystal Save] Corrected scene object '{e.prefab.name}' to prefab asset '{asset.name}'.", LogLevel.Warning);
						e.prefab = asset;
						EditorUtility.SetDirty(registry);
					}
				}
			}

			/* ── 2. Draw default inspector (users can add rows with “+”) ─── */
			DrawDefaultInspector();

			GUILayout.Space(6);
			GUILayout.BeginHorizontal();

			/* Auto-register button --------------------------------------- */
			if (GUILayout.Button("Auto-Register Prefab Assets"))
			{
				AutoRegisterPrefabs(registry);
			}

			/* Clean duplicates button ------------------------------------ */
			if (GUILayout.Button("Clean Duplicates"))
			{
				int removed = RemoveDuplicateEntries(registry);
				if (removed > 0)
					Logger.Log($"[Crystal Save] Removed {removed} duplicate prefab entr{(removed == 1 ? "y" : "ies")}.", LogLevel.Warning);
				else
					Logger.Log("[Crystal Save] No duplicates found in PrefabRegistry.", LogLevel.Info);
			}

			GUILayout.EndHorizontal();

			/* Sync pooling settings button -------------------------------- */
			GUILayout.Space(6);
			if (GUILayout.Button("Sync Pooling Settings to SaveablePrefabs"))
			{
				SyncPoolingSettingsToComponents(registry);
			}

			// Note: Changes are tracked but auto-sync is intentionally disabled for performance
			EditorGUI.EndChangeCheck();
		}

		/* ─────────────────────────────────────────────────────────────── */
		private static int RemoveDuplicateEntries(PrefabRegistry registry)
		{
			var seen = new HashSet<GameObject>();
			int removed = 0;

			for (int i = registry.prefabEntries.Count - 1; i >= 0; i--)
			{
				var entry = registry.prefabEntries[i];
				if (entry.prefab == null) continue;   // keep blank rows

				if (!seen.Add(entry.prefab))
				{
					registry.prefabEntries.RemoveAt(i);
					removed++;
				}
			}

			if (removed > 0)
			{
				EditorUtility.SetDirty(registry);
				AssetDatabase.SaveAssets();
			}

			return removed;
		}

		/* ─────────────────────────────────────────────────────────────── */
		private static void AutoRegisterPrefabs(PrefabRegistry registry)
		{
			string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets" });
			registry.prefabEntries.Clear();

			foreach (string guid in guids)
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>(path);
				if (pf == null) continue;

				SaveablePrefab saveable = pf.GetComponent<SaveablePrefab>();
				if (saveable == null) continue;

				if (string.IsNullOrEmpty(saveable.PrefabAssetID))
				{
					saveable.PrefabAssetID = Guid.NewGuid().ToString();
					EditorUtility.SetDirty(pf);
					Logger.Log($"PrefabRegistryEditor: Assigned prefabAssetID '{saveable.PrefabAssetID}' to '{pf.name}'.", LogLevel.Info);
				}

				if (!registry.TryAddPrefab(saveable.PrefabAssetID, pf, out string reason))
					Logger.Log($"[Crystal Save] Could not add prefab '{pf.name}': {reason}", LogLevel.Warning);
			}

			EditorUtility.SetDirty(registry);
			AssetDatabase.SaveAssets();
			Logger.Log("PrefabRegistryEditor: Auto-registered prefab assets.", LogLevel.Info);
		}

		/* ─────────────────────────────────────────────────────────────── */
		private static void SyncPoolingSettingsToComponents(PrefabRegistry registry)
		{
			int syncedCount = 0;
			string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets" });

                        foreach (string guid in guids)
                        {
                                string path = AssetDatabase.GUIDToAssetPath(guid);
                                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                                if (prefab == null) continue;

                                SaveablePrefab saveablePrefab = prefab.GetComponent<SaveablePrefab>();
                                if (saveablePrefab == null || string.IsNullOrEmpty(saveablePrefab.PrefabAssetID)) continue;

                                // Only attempt to sync when the prefab is registered.
                                var entry = registry.FindEntryByID(saveablePrefab.PrefabAssetID);
                                if (entry == null) continue;

                                bool registryDisablesPooling = entry.disablePooling;

                                if (saveablePrefab.DisablePooling != registryDisablesPooling)
                                {
                                        Undo.RecordObject(saveablePrefab, "Sync Disable Pooling from Registry");
                                        saveablePrefab.DisablePooling = registryDisablesPooling;
                                        EditorUtility.SetDirty(prefab);
                                        syncedCount++;
                                        Logger.Log($"PrefabRegistryEditor: Synced DisablePooling={registryDisablesPooling} to SaveablePrefab '{prefab.name}'.", LogLevel.Info);
                                }
                        }

			if (syncedCount > 0)
			{
				AssetDatabase.SaveAssets();
				Logger.Log($"PrefabRegistryEditor: Synced pooling settings to {syncedCount} SaveablePrefab component{(syncedCount == 1 ? "" : "s")}.", LogLevel.Info);
			}
			else
			{
				Logger.Log("PrefabRegistryEditor: No SaveablePrefab components needed pooling setting synchronization.", LogLevel.Info);
			}
		}
	}
}
#endif
#endif