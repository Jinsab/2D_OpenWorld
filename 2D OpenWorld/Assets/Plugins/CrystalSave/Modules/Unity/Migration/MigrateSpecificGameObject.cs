#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	[CreateAssetMenu(fileName = "MigrateSpecificGameObject_ToNewVersion", menuName = "Crystal Save/Create Migration Actions/Legacy/Migrate Specific GameObject")]
	public class MigrateSpecificGameObject : MigrationAction
	{
		[Header("Target Identification")]
		[Tooltip("Unique Identifier of the GameObject to migrate.")]
		public string targetUniqueID;

		[Header("New GameObject Properties")]
		[Tooltip("New name to set.")]
		public string newName = string.Empty;

		[Tooltip("New layer to set.")]
		public int newLayer = 0;

		[Tooltip("New tag to set.")]
		public string newTag = "Untagged";

		[Tooltip("New active state to set.")]
		public bool newIsActive = true;

		public override void ApplyMigration(SaveData data)
		{
			if (data == null)
			{
				Logger.Log("MigrateSpecificGameObject: SaveData is null. Migration aborted.", LogLevel.Warning);
				return;
			}

			if (string.IsNullOrEmpty(targetUniqueID))
			{
				Logger.Log("MigrateSpecificGameObject: targetUniqueID is not set. Migration aborted.", LogLevel.Warning);
				return;
			}

			// Check if ComponentsData contains the targetUniqueID
			if (data.ComponentsData.ContainsKey(targetUniqueID))
			{
				byte[] componentData = data.ComponentsData[targetUniqueID];
				if (componentData == null || componentData.Length == 0)
				{
					Logger.Log($"MigrateSpecificGameObject: No data found for UniqueIdentifier '{targetUniqueID}'. Migration aborted.", LogLevel.Warning);
					return;
				}

				// Deserialize using the new Deserialize<T> method
				GameObjectData gameObjectData = SaveDataSerializer.Instance.Deserialize<GameObjectData>(componentData);
				if (gameObjectData != null)
				{
					Logger.Log($"MigrateSpecificGameObject: Successfully deserialized GameObjectData for UniqueIdentifier '{targetUniqueID}'.", LogLevel.Info);
					Logger.Log($"Original Name: {gameObjectData.Name}, Layer: {gameObjectData.Layer}, Tag: {gameObjectData.Tag}, IsActive: {gameObjectData.IsActive}", LogLevel.Info);

					bool dataChanged = false;

					// Update Name
					if (!string.IsNullOrEmpty(newName) && gameObjectData.Name != newName)
					{
						gameObjectData.Name = newName;
						Logger.Log($"MigrationAction: Updated Name for UniqueIdentifier '{targetUniqueID}' to '{newName}'.", LogLevel.Info);
						dataChanged = true;
					}
					else
					{
						Logger.Log($"MigrationAction: Name for UniqueIdentifier '{targetUniqueID}' is already '{newName}'. Skipping name update.", LogLevel.Info);
					}

					// Update Layer
					if (gameObjectData.Layer.HasValue && gameObjectData.Layer.Value != newLayer)
					{
						if (IsValidLayer(newLayer))
						{
							gameObjectData.Layer = newLayer;
							Logger.Log($"MigrationAction: Updated Layer for UniqueIdentifier '{targetUniqueID}' to '{LayerMask.LayerToName(newLayer)}' (Layer {newLayer}).", LogLevel.Info);
							dataChanged = true;
						}
						else
						{
							Logger.Log($"MigrationAction: Invalid layer '{newLayer}' for UniqueIdentifier '{targetUniqueID}'. Skipping layer update.", LogLevel.Warning);
						}
					}
					else
					{
						Logger.Log($"MigrationAction: Layer for UniqueIdentifier '{targetUniqueID}' is already '{newLayer}'. Skipping layer update.", LogLevel.Info);
					}

					// Update Tag
					if (!string.IsNullOrEmpty(newTag) && gameObjectData.Tag != newTag)
					{
						if (IsValidTag(newTag))
						{
							gameObjectData.Tag = newTag;
							Logger.Log($"MigrationAction: Updated Tag for UniqueIdentifier '{targetUniqueID}' to '{newTag}'.", LogLevel.Info);
							dataChanged = true;
						}
						else
						{
							Logger.Log($"MigrationAction: Invalid tag '{newTag}' for UniqueIdentifier '{targetUniqueID}'. Skipping tag update.", LogLevel.Warning);
						}
					}
					else
					{
						Logger.Log($"MigrationAction: Tag for UniqueIdentifier '{targetUniqueID}' is already '{newTag}'. Skipping tag update.", LogLevel.Info);
					}

					// Update IsActive
					if (gameObjectData.IsActive.HasValue && gameObjectData.IsActive.Value != newIsActive)
					{
						gameObjectData.IsActive = newIsActive;
						Logger.Log($"MigrationAction: Updated IsActive for UniqueIdentifier '{targetUniqueID}' to '{newIsActive}'.", LogLevel.Info);
						dataChanged = true;
					}
					else
					{
						Logger.Log($"MigrationAction: IsActive for UniqueIdentifier '{targetUniqueID}' is already '{newIsActive}'. Skipping active state update.", LogLevel.Info);
					}

					// Serialize and save back if data has changed
					if (dataChanged)
					{
						byte[] updatedComponentData = SaveDataSerializer.Instance.Serialize(gameObjectData);
						if (updatedComponentData != null)
						{
							data.ComponentsData[targetUniqueID] = updatedComponentData;
							Logger.Log($"MigrationAction: Successfully updated GameObjectData for UniqueIdentifier '{targetUniqueID}'.", LogLevel.Info);
						}
						else
						{
							Logger.Log($"MigrationAction: Failed to serialize updated GameObjectData for UniqueIdentifier '{targetUniqueID}'.", LogLevel.Error);
						}
					}
					else
					{
						Logger.Log($"MigrationAction: No changes applied for UniqueIdentifier '{targetUniqueID}'.", LogLevel.Info);
					}
				}
				else
				{
					Logger.Log($"MigrateSpecificGameObject: Failed to deserialize GameObjectData for UniqueIdentifier '{targetUniqueID}'. Migration aborted.", LogLevel.Warning);

					// Optionally, assign default GameObjectData or handle accordingly
					GameObjectData defaultGameObjectData = new GameObjectData
					{
						Name = string.IsNullOrEmpty(newName) ? string.Empty : newName,
						Layer = IsValidLayer(newLayer) ? newLayer : (int?)null,
						Tag = IsValidTag(newTag) ? newTag : "Untagged",
						IsActive = newIsActive
					};

					byte[] defaultComponentData = SaveDataSerializer.Instance.Serialize(defaultGameObjectData);
					if (defaultComponentData != null)
					{
						data.ComponentsData[targetUniqueID] = defaultComponentData;
						Logger.Log($"MigrateSpecificGameObject: Assigned default GameObjectData for UniqueIdentifier '{targetUniqueID}'.", LogLevel.Info);
					}
					else
					{
						Logger.Log($"MigrateSpecificGameObject: Failed to serialize default GameObjectData for UniqueIdentifier '{targetUniqueID}'.", LogLevel.Error);
					}
				}
			}
			else
			{
				Logger.Log($"MigrateSpecificGameObject: No data found for UniqueIdentifier '{targetUniqueID}'. Migration aborted.", LogLevel.Warning);
			}
		}

		/// <summary>
		/// Validates if the provided layer index is within Unity's layer range.
		/// </summary>
		/// <param name="layer">Layer index to validate.</param>
		/// <returns>True if valid; otherwise, false.</returns>
		private bool IsValidLayer(int layer)
		{
			// Unity supports layers 0-31
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
				Logger.Log("MigrateSpecificGameObject: TagRegistry is not available. Cannot validate tags.", LogLevel.Error);
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
                                        $"MigrateSpecificGameObject: Failed to load TagRegistry using key '{configuredKey}'.",
                                        LogLevel.Error);
                        }
                        return cachedTagRegistry;
		}
	}
}
#endif