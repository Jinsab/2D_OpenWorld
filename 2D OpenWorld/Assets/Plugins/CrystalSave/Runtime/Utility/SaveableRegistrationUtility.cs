#if MEMORYPACK && ARAWN_REMEMBERME

using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Helper methods used by editor integrations to register <see cref="SaveableComponent"/>
    /// and <see cref="SaveablePrefab"/> instances when the SaveManager misses them.
    /// </summary>
    public static class SaveableRegistrationUtility
    {
        public readonly struct SaveableRegistrationResult
        {
            public SaveableRegistrationResult(GameObject target, int registeredComponents, bool prefabRegistered)
            {
                Target = target;
                RegisteredComponents = registeredComponents;
                PrefabRegistered = prefabRegistered;
            }

            public GameObject Target { get; }

            /// <summary>Total number of <see cref="SaveableComponent"/> instances that were registered.</summary>
            public int RegisteredComponents { get; }

            /// <summary>True when <see cref="SaveablePrefab.RegisterForSaving"/> was invoked.</summary>
            public bool PrefabRegistered { get; }
        }

        /// <summary>
        /// Ensures the provided <see cref="GameObject"/> and its saveable pieces are tracked by the save system.
        /// </summary>
        /// <param name="target">GameObject containing saveable components.</param>
        /// <param name="includeChildren">If true, also registers components on child objects.</param>
        public static SaveableRegistrationResult RegisterSaveables(GameObject target, bool includeChildren)
        {
            if (target == null)
            {
                Logger.Log("SaveableRegistrationUtility: Target GameObject is null.", LogLevel.Warning);
                return new SaveableRegistrationResult(null, 0, false);
            }

            var manager = SaveManager.Instance;
            if (manager == null)
            {
                Logger.Log("SaveableRegistrationUtility: SaveManager.Instance is null.", LogLevel.Warning);
                return new SaveableRegistrationResult(null, 0, false);
            }

            bool prefabRegistered = false;

            // Register the SaveablePrefab root if required.
            var prefab = target.GetComponent<SaveablePrefab>();
            var prefabManager = manager.GetPrefabManager;

            if (prefab != null)
            {
                // IMPORTANT: Enable RegisterWithSaveSystem if it's disabled
                // This is needed for scene-baked prefabs where the user disabled it at design time
                if (!prefab.RegisterWithSaveSystem)
                {
                    prefab.RegisterWithSaveSystem = true;
                }

                bool isTracked = manager.IsGameObjectTracked(prefab.gameObject);
                if (!isTracked)
                {
                    prefab.RegisterForSaving();
                    prefabRegistered = true;
                }

                // Ensure the PrefabManager tracks the instance so it participates in save cycles.
                prefabManager?.RegisterPrefab(prefab);
            }

            int registeredComponents = 0;
            var componentManager = manager.ComponentManager;
            if (componentManager == null)
            {
                Logger.Log(
                    "SaveableRegistrationUtility: SaveManager.ComponentManager is null – skipping SaveableComponent registration.",
                    LogLevel.Warning);
                return new SaveableRegistrationResult(target, registeredComponents, prefabRegistered);
            }

            var components = includeChildren
                ? target.GetComponentsInChildren<SaveableComponent>(includeInactive: true)
                : target.GetComponents<SaveableComponent>();

            foreach (var component in components)
            {
                if (component == null)
                    continue;

                if (componentManager.Contains(component))
                    continue;

                componentManager.RegisterSaveableComponent(component);
                registeredComponents++;
            }

            return new SaveableRegistrationResult(target, registeredComponents, prefabRegistered);
        }

        /// <summary>
        /// Resolves a GameObject by identifier and registers its saveable elements.
        /// </summary>
        /// <param name="identifier">UniqueID or PrefabAssetID.</param>
        /// <param name="identifierType">Specifies whether the identifier is a UniqueID, PrefabAssetID, etc.</param>
        /// <param name="includeChildren">If true, also registers components on child objects.</param>
        public static SaveableRegistrationResult RegisterSaveables(
            string identifier,
            SaveManager.IdentifierType identifierType,
            bool includeChildren)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                Logger.Log("SaveableRegistrationUtility: Identifier is null or empty.", LogLevel.Warning);
                return new SaveableRegistrationResult(null, 0, false);
            }

            var manager = SaveManager.Instance;
            if (manager == null)
            {
                Logger.Log("SaveableRegistrationUtility: SaveManager.Instance is null.", LogLevel.Warning);
                return new SaveableRegistrationResult(null, 0, false);
            }

            GameObject target = manager.FindGameObjectByUniqueID(identifier, identifierType);

            if (target == null && identifierType == SaveManager.IdentifierType.PrefabAssetID)
            {
                string currentUniqueId = manager.GetCurrentUniqueIDFromPrefabAssetID(identifier);
                if (!string.IsNullOrEmpty(currentUniqueId))
                {
                    target = manager.FindGameObjectByUniqueID(currentUniqueId, SaveManager.IdentifierType.UniqueID);
                }
            }

            if (target == null)
            {
                Logger.Log(
                    $"SaveableRegistrationUtility: Unable to find GameObject for identifier '{identifier}' ({identifierType}).",
                    LogLevel.Warning);
                return new SaveableRegistrationResult(null, 0, false);
            }

            // IMPORTANT: Enable RegisterWithSaveSystem if there's a SaveablePrefab
            // This is needed for scene-baked prefabs where the user disabled it at design time
            var prefab = target.GetComponent<SaveablePrefab>();
            if (prefab != null && !prefab.RegisterWithSaveSystem)
            {
                prefab.RegisterWithSaveSystem = true;
            }

            return RegisterSaveables(target, includeChildren);
        }
    }
}

#endif
