#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	[CreateAssetMenu(fileName = "MigrateMultipleGameObjects", menuName = "Crystal Save/Create Migration Actions/Migrate Multiple GameObjects")]
	public class MigrateMultipleGameObjects : MigrationAction
	{
		[System.Serializable]
		public class GameObjectMigrationEntry
		{
			[Tooltip("Unique Identifier of the GameObject to migrate.")]
			public string targetUniqueID;

			[Tooltip("Human-friendly name for the target GameObject (auto-populated).")]
			public string targetName;

			[Tooltip("New name to set.")]
			public string newName = string.Empty;

			[Tooltip("New layer to set.")]
			public int newLayer = 0;

			[Tooltip("New tag to set.")]
			public string newTag = "Untagged";

			[Tooltip("New active state to set.")]
			public bool newIsActive = true;
		}

		[Tooltip("List of game object migration entries for all GameObjects that need updating.")]
		public List<GameObjectMigrationEntry> migrationEntries = new List<GameObjectMigrationEntry>();

		public override void ApplyMigration(SaveData data)
		{
			if (data == null)
			{
				Logger.Log("MigrateMultipleGameObjects: SaveData is null. Migration aborted.", LogLevel.Warning);
				return;
			}

			if (migrationEntries == null || migrationEntries.Count == 0)
			{
				Logger.Log("MigrateMultipleGameObjects: No migration entries provided. Nothing to migrate.", LogLevel.Warning);
				return;
			}

			foreach (var entry in migrationEntries)
			{
				if (entry == null)
				{
					Logger.Log("MigrateMultipleGameObjects: Encountered a null migration entry. Skipping.", LogLevel.Warning);
					continue;
				}
				if (string.IsNullOrEmpty(entry.targetUniqueID))
				{
					Logger.Log("MigrateMultipleGameObjects: targetUniqueID is not set for one of the entries. Skipping entry.", LogLevel.Warning);
					continue;
				}

				if (data.ComponentsData.ContainsKey(entry.targetUniqueID))
				{
					byte[] componentData = data.ComponentsData[entry.targetUniqueID];
					if (componentData == null || componentData.Length == 0)
					{
						Logger.Log($"MigrateMultipleGameObjects: No data found for UniqueIdentifier '{entry.targetUniqueID}'. Skipping entry.", LogLevel.Warning);
						continue;
					}

					// Attempt to deserialize GameObjectData.
					GameObjectData gameObjectData = SaveDataSerializer.Instance.Deserialize<GameObjectData>(componentData);
					bool dataChanged = false;

					if (gameObjectData != null)
					{
						Logger.Log($"MigrateMultipleGameObjects: Successfully deserialized GameObjectData for UniqueIdentifier '{entry.targetUniqueID}'.", LogLevel.Info);
						Logger.Log($"Original Name: {gameObjectData.Name}, Layer: {gameObjectData.Layer}, Tag: {gameObjectData.Tag}, IsActive: {gameObjectData.IsActive}", LogLevel.Info);

						// Update Name.
						if (!string.IsNullOrEmpty(entry.newName) && gameObjectData.Name != entry.newName)
						{
							gameObjectData.Name = entry.newName;
							Logger.Log($"MigrationAction: Updated Name for UniqueIdentifier '{entry.targetUniqueID}' to '{entry.newName}'.", LogLevel.Info);
							dataChanged = true;
						}
						else
						{
							Logger.Log($"MigrationAction: Name for UniqueIdentifier '{entry.targetUniqueID}' is already '{entry.newName}'. Skipping name update.", LogLevel.Info);
						}

						// Update Layer.
						if (gameObjectData.Layer.HasValue && gameObjectData.Layer.Value != entry.newLayer)
						{
							if (IsValidLayer(entry.newLayer))
							{
								gameObjectData.Layer = entry.newLayer;
								Logger.Log($"MigrationAction: Updated Layer for UniqueIdentifier '{entry.targetUniqueID}' to '{LayerMask.LayerToName(entry.newLayer)}' (Layer {entry.newLayer}).", LogLevel.Info);
								dataChanged = true;
							}
							else
							{
								Logger.Log($"MigrationAction: Invalid layer '{entry.newLayer}' for UniqueIdentifier '{entry.targetUniqueID}'. Skipping layer update.", LogLevel.Warning);
							}
						}
						else
						{
							Logger.Log($"MigrationAction: Layer for UniqueIdentifier '{entry.targetUniqueID}' is already '{entry.newLayer}'. Skipping layer update.", LogLevel.Info);
						}

						// Update Tag.
						if (!string.IsNullOrEmpty(entry.newTag) && gameObjectData.Tag != entry.newTag)
						{
							if (IsValidTag(entry.newTag))
							{
								gameObjectData.Tag = entry.newTag;
								Logger.Log($"MigrationAction: Updated Tag for UniqueIdentifier '{entry.targetUniqueID}' to '{entry.newTag}'.", LogLevel.Info);
								dataChanged = true;
							}
							else
							{
								Logger.Log($"MigrationAction: Invalid tag '{entry.newTag}' for UniqueIdentifier '{entry.targetUniqueID}'. Skipping tag update.", LogLevel.Warning);
							}
						}
						else
						{
							Logger.Log($"MigrationAction: Tag for UniqueIdentifier '{entry.targetUniqueID}' is already '{entry.newTag}'. Skipping tag update.", LogLevel.Info);
						}

						// Update IsActive.
						if (gameObjectData.IsActive.HasValue && gameObjectData.IsActive.Value != entry.newIsActive)
						{
							gameObjectData.IsActive = entry.newIsActive;
							Logger.Log($"MigrationAction: Updated IsActive for UniqueIdentifier '{entry.targetUniqueID}' to '{entry.newIsActive}'.", LogLevel.Info);
							dataChanged = true;
						}
						else
						{
							Logger.Log($"MigrationAction: IsActive for UniqueIdentifier '{entry.targetUniqueID}' is already '{entry.newIsActive}'. Skipping active state update.", LogLevel.Info);
						}

						// Serialize and save back if data has changed.
						if (dataChanged)
						{
							byte[] updatedComponentData = SaveDataSerializer.Instance.Serialize(gameObjectData);
							if (updatedComponentData != null)
							{
								data.ComponentsData[entry.targetUniqueID] = updatedComponentData;
								Logger.Log($"MigrationAction: Successfully updated GameObjectData for UniqueIdentifier '{entry.targetUniqueID}'.", LogLevel.Info);
							}
							else
							{
								Logger.Log($"MigrationAction: Failed to serialize updated GameObjectData for UniqueIdentifier '{entry.targetUniqueID}'.", LogLevel.Error);
							}
						}
						else
						{
							Logger.Log($"MigrationAction: No changes applied for UniqueIdentifier '{entry.targetUniqueID}'.", LogLevel.Info);
						}
					}
					else
					{
						Logger.Log($"MigrateMultipleGameObjects: Failed to deserialize GameObjectData for UniqueIdentifier '{entry.targetUniqueID}'. Attempting default assignment.", LogLevel.Warning);

						// Assign default GameObjectData if deserialization fails.
						GameObjectData defaultGameObjectData = new GameObjectData
						{
							Name = string.IsNullOrEmpty(entry.newName) ? string.Empty : entry.newName,
							Layer = IsValidLayer(entry.newLayer) ? entry.newLayer : (int?)null,
							Tag = IsValidTag(entry.newTag) ? entry.newTag : "Untagged",
							IsActive = entry.newIsActive
						};

						byte[] defaultComponentData = SaveDataSerializer.Instance.Serialize(defaultGameObjectData);
						if (defaultComponentData != null)
						{
							data.ComponentsData[entry.targetUniqueID] = defaultComponentData;
							Logger.Log($"MigrationAction: Assigned default GameObjectData for UniqueIdentifier '{entry.targetUniqueID}'.", LogLevel.Info);
						}
						else
						{
							Logger.Log($"MigrationAction: Failed to serialize default GameObjectData for UniqueIdentifier '{entry.targetUniqueID}'.", LogLevel.Error);
						}
					}
				}
				else
				{
					Logger.Log($"MigrateMultipleGameObjects: No data found for UniqueIdentifier '{entry.targetUniqueID}'. Skipping entry.", LogLevel.Warning);
				}
			}
		}

		/// <summary>
		/// Validates if the provided layer index is within Unity's layer range.
		/// </summary>
		/// <param name="layer">Layer index to validate.</param>
		/// <returns>True if valid; otherwise, false.</returns>
		private bool IsValidLayer(int layer)
		{
			// Unity supports layers 0-31.
			return layer >= 0 && layer < 32;
		}

		/// <summary>
		/// Validates if the provided tag exists in the TagRegistry.
		/// </summary>
		/// <param name="tag">Tag to validate.</param>
		/// <returns>True if valid; otherwise, false.</returns>
		private bool IsValidTag(string tag)
		{
			if (string.IsNullOrEmpty(tag))
				return false;

			TagRegistry tagRegistry = GetTagRegistry();
			if (tagRegistry == null)
			{
				Logger.Log("MigrateMultipleGameObjects: TagRegistry is not available. Cannot validate tags.", LogLevel.Error);
				return false;
			}

			return tagRegistry.Tags.Contains(tag);
		}

		/// <summary>
		/// Retrieves the TagRegistry instance.
		/// </summary>
		/// <returns>The TagRegistry instance if found; otherwise, null.</returns>
		private TagRegistry GetTagRegistry()
		{
                        string configuredKey = SaveManager.GetTagRegistryAssetKey();
                        TagRegistry cachedTagRegistry = AssetProvider.Load<TagRegistry>(configuredKey);

                        if (cachedTagRegistry == null &&
                                !string.Equals(configuredKey, SaveManager.DefaultTagRegistryAssetKey, System.StringComparison.Ordinal))
                        {
                                cachedTagRegistry = AssetProvider.Load<TagRegistry>(SaveManager.DefaultTagRegistryAssetKey);
                        }

                        if (cachedTagRegistry == null)
                        {
                                Logger.Log(
                                        $"MigrateMultipleGameObjects: Failed to load TagRegistry using key '{configuredKey}'.",
                                        LogLevel.Error);
                        }
                        return cachedTagRegistry;
		}
	}
}
#endif