#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    [CreateAssetMenu(fileName = "MigrateSpecificPrefabGameObject_ToNewVersion", menuName = "Crystal Save/Create Migration Actions/Legacy/Migrate Specific Prefab GameObject")]
    public class MigrateSpecificPrefabGameObject : MigrationAction
    {
        [Header("Target Identification")]
        [Tooltip("Prefab Asset ID of the SaveablePrefab to migrate.")]
        public string targetPrefabAssetID;

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
                Logger.Log("MigrateSpecificPrefabGameObject: SaveData is null. Migration aborted.", LogLevel.Warning);
                return;
            }

            if (string.IsNullOrEmpty(targetPrefabAssetID))
            {
                Logger.Log("MigrateSpecificPrefabGameObject: targetPrefabAssetID is not set. Migration aborted.", LogLevel.Warning);
                return;
            }

            if (data.Prefabs == null || data.Prefabs.Count == 0)
            {
                Logger.Log("MigrateSpecificPrefabGameObject: No SaveablePrefab data available. Migration aborted.", LogLevel.Warning);
                return;
            }

            bool found = false;

            foreach (var prefabData in data.Prefabs)
            {
                if (prefabData != null && prefabData.PrefabID == targetPrefabAssetID)
                {
                    found = true;

                    RuntimeModificationData runtimeMods = null;
                    if (prefabData.RuntimeModificationData != null && prefabData.RuntimeModificationData.Length > 0)
                    {
                        runtimeMods = SaveDataSerializer.Instance.Deserialize<RuntimeModificationData>(prefabData.RuntimeModificationData);
                    }
                    runtimeMods ??= new RuntimeModificationData();
                    runtimeMods.RootState ??= new RootStateOverride();

                    bool dataChanged = false;

                    if (!string.IsNullOrEmpty(newName) && runtimeMods.RootState.Name != newName)
                    {
                        runtimeMods.RootState.Name = newName;
                        Logger.Log($"MigrationAction: Updated Name for PrefabID '{targetPrefabAssetID}' to '{newName}'.", LogLevel.Info);
                        dataChanged = true;
                    }

                    if (IsValidLayer(newLayer) && runtimeMods.RootState.Layer != newLayer)
                    {
                        runtimeMods.RootState.Layer = newLayer;
                        Logger.Log($"MigrationAction: Updated Layer for PrefabID '{targetPrefabAssetID}' to '{LayerMask.LayerToName(newLayer)}' (Layer {newLayer}).", LogLevel.Info);
                        dataChanged = true;
                    }

                    if (!string.IsNullOrEmpty(newTag) && runtimeMods.RootState.Tag != newTag)
                    {
                        if (IsValidTag(newTag))
                        {
                            runtimeMods.RootState.Tag = newTag;
                            Logger.Log($"MigrationAction: Updated Tag for PrefabID '{targetPrefabAssetID}' to '{newTag}'.", LogLevel.Info);
                            dataChanged = true;
                        }
                        else
                        {
                            Logger.Log($"MigrationAction: Invalid tag '{newTag}' for PrefabID '{targetPrefabAssetID}'. Skipping tag update.", LogLevel.Warning);
                        }
                    }

                    if (dataChanged)
                    {
                        prefabData.RuntimeModificationData = SaveDataSerializer.Instance.Serialize(runtimeMods);
                    }

                    if (data.GameObjectStates == null)
                        data.GameObjectStates = new List<GameObjectState>();

                    var state = data.GameObjectStates.FirstOrDefault(s => s.UniqueID == prefabData.InstanceID);
                    if (state == null)
                    {
                        data.GameObjectStates.Add(new GameObjectState(prefabData.InstanceID, newIsActive));
                        Logger.Log($"MigrationAction: Added GameObjectState for Prefab Instance '{prefabData.InstanceID}' with IsActive '{newIsActive}'.", LogLevel.Info);
                    }
                    else
                    {
                        state.IsActive = newIsActive;
                        Logger.Log($"MigrationAction: Updated GameObjectState for Prefab Instance '{prefabData.InstanceID}' to IsActive '{newIsActive}'.", LogLevel.Info);
                    }
                }
            }

            if (!found)
            {
                Logger.Log($"MigrateSpecificPrefabGameObject: No SaveablePrefabData found with PrefabID '{targetPrefabAssetID}'.", LogLevel.Warning);
            }
        }

        private bool IsValidLayer(int layer)
        {
            return layer >= 0 && layer < 32;
        }

        private bool IsValidTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return false;

            TagRegistry tagRegistry = GetTagRegistry();
            if (tagRegistry == null)
            {
                Logger.Log("MigrateSpecificPrefabGameObject: TagRegistry is not available. Cannot validate tags.", LogLevel.Error);
                return false;
            }

            return tagRegistry.Tags.Contains(tag);
        }

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
                    $"MigrateSpecificPrefabGameObject: Failed to load TagRegistry using key '{configuredKey}'.",
                    LogLevel.Error);
            }
            return cachedTagRegistry;
        }
    }
}
#endif
