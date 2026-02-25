#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
        public static class SaveableDeregisterUtility
        {
                public static bool DeregisterSaveables(GameObject target, out bool componentsDeregistered, out bool prefabDeregistered)
                {
                        componentsDeregistered = false;
                        prefabDeregistered     = false;

                        if (target == null)
                        {
                                return false;
                        }

                        var manager = SaveManager.Instance;
                        if (manager == null)
                        {
                                return false;
                        }

                        var componentManager = manager.ComponentManager;
                        if (componentManager != null)
                        {
                                var saveableComponents = target.GetComponents<SaveableComponent>();
                                for (int i = 0; i < saveableComponents.Length; i++)
                                {
                                        var saveable = saveableComponents[i];
                                        if (saveable == null) continue;

                                        componentManager.UnregisterSaveableComponent(saveable);
                                        componentsDeregistered = true;
                                }
                        }

                var saveablePrefab = target.GetComponent<SaveablePrefab>();
                if (saveablePrefab != null)
                {
                        // Disable RegisterWithSaveSystem to prevent automatic re-registration
                        // when the GameObject is disabled/enabled (OnEnable would re-register it)
                        saveablePrefab.RegisterWithSaveSystem = false;
                        
                        saveablePrefab.UnregisterFromSaving();
                        manager.GetPrefabManager?.UnregisterPrefab(saveablePrefab);
                        prefabDeregistered = true;
                }                        return componentsDeregistered || prefabDeregistered;
                }
        }
}
#endif
