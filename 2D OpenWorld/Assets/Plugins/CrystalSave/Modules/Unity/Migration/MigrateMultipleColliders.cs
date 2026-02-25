#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using UnityEngine;
using MemoryPack;

namespace Arawn.CrystalSave.Runtime
{
	[CreateAssetMenu(fileName = "MigrateMultipleColliders", menuName = "Crystal Save/Create Migration Actions/Migrate Multiple Colliders")]
	public class MigrateMultipleColliders : MigrationAction
	{
		[System.Serializable]
		public class ColliderMigrationEntry
		{
			public string targetName;

			[Tooltip("Unique Identifier of the Collider to migrate.")]
			public string targetUniqueID;

			[Tooltip("If true, update the Collider's enabled state.")]
			public bool updateEnabled = false;
			[Tooltip("New enabled value.")]
			public bool newEnabled;

			[Tooltip("If true, update the Collider's isTrigger state.")]
			public bool updateIsTrigger = false;
			[Tooltip("New isTrigger value.")]
			public bool newIsTrigger;

			[Tooltip("If true, update the Collider's material reference.")]
			public bool updateMaterial = false;
			[Tooltip("New material name (resource path) for the Collider.")]
			public string newMaterialName;
		}

		[Tooltip("List of Collider migration entries.")]
		public List<ColliderMigrationEntry> migrationEntries = new List<ColliderMigrationEntry>();

		public override void ApplyMigration(SaveData data)
		{
			if (data == null)
			{
				Logger.Log("MigrateMultipleColliders: SaveData is null. Migration aborted.", LogLevel.Warning);
				return;
			}
			if (migrationEntries == null || migrationEntries.Count == 0)
			{
				Logger.Log("MigrateMultipleColliders: No migration entries provided. Nothing to migrate.", LogLevel.Warning);
				return;
			}

			foreach (var entry in migrationEntries)
			{
				if (entry == null)
				{
					Logger.Log("MigrateMultipleColliders: Encountered a null migration entry. Skipping.", LogLevel.Warning);
					continue;
				}
				if (string.IsNullOrEmpty(entry.targetUniqueID))
				{
					Logger.Log("MigrateMultipleColliders: targetUniqueID is not set. Skipping entry.", LogLevel.Warning);
					continue;
				}
				if (!data.ComponentsData.ContainsKey(entry.targetUniqueID))
				{
					Logger.Log($"MigrateMultipleColliders: No data found for UniqueIdentifier '{entry.targetUniqueID}'. Skipping entry.", LogLevel.Warning);
					continue;
				}

				byte[] compData = data.ComponentsData[entry.targetUniqueID];
				if (compData == null || compData.Length == 0)
				{
					Logger.Log($"MigrateMultipleColliders: Component data is empty for '{entry.targetUniqueID}'. Skipping entry.", LogLevel.Warning);
					continue;
				}

				// Deserialize the stored ColliderData.
				ColliderData colliderData = SaveDataSerializer.Instance.Deserialize<ColliderData>(compData);
				if (colliderData == null)
				{
					Logger.Log($"MigrateMultipleColliders: Failed to deserialize ColliderData for '{entry.targetUniqueID}'. Skipping entry.", LogLevel.Warning);
					continue;
				}

				bool dataChanged = false;

				if (entry.updateEnabled)
				{
					colliderData.Enabled = entry.newEnabled;
					dataChanged = true;
					Logger.Log($"MigrateMultipleColliders: Updated Enabled for '{entry.targetUniqueID}' to {entry.newEnabled}.", LogLevel.Info);
				}
				if (entry.updateIsTrigger)
				{
					colliderData.IsTrigger = entry.newIsTrigger;
					dataChanged = true;
					Logger.Log($"MigrateMultipleColliders: Updated IsTrigger for '{entry.targetUniqueID}' to {entry.newIsTrigger}.", LogLevel.Info);
				}
				if (entry.updateMaterial)
				{
					colliderData.MaterialName = entry.newMaterialName;
					dataChanged = true;
					Logger.Log($"MigrateMultipleColliders: Updated MaterialName for '{entry.targetUniqueID}' to '{entry.newMaterialName}'.", LogLevel.Info);
				}

				if (dataChanged)
				{
					try
					{
						byte[] updatedData = SaveDataSerializer.Instance.Serialize(colliderData);
						if (updatedData != null)
						{
							data.ComponentsData[entry.targetUniqueID] = updatedData;
							Logger.Log($"MigrateMultipleColliders: Successfully updated ColliderData for '{entry.targetUniqueID}'.", LogLevel.Info);
						}
						else
						{
							Logger.Log($"MigrateMultipleColliders: Serialization returned null for '{entry.targetUniqueID}'.", LogLevel.Error);
						}
					}
					catch (Exception ex)
					{
						Logger.Log($"MigrateMultipleColliders: Exception during serialization for '{entry.targetUniqueID}': {ex.Message}", LogLevel.Error);
					}
				}
				else
				{
					Logger.Log($"MigrateMultipleColliders: No changes applied for '{entry.targetUniqueID}'.", LogLevel.Info);
				}
			}
		}
	}
}
#endif