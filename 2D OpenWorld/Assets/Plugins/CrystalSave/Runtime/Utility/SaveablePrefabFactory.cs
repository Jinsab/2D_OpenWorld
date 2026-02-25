#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;
using System;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime
{
	/// <summary>One-stop shop for instantiating SaveablePrefab assets.</summary>
	public static class SaveablePrefabFactory
	{
                /// <remarks>
                /// • Pass the *prefab asset* that already carries a SaveablePrefab component.
                /// • If the asset has no SaveablePrefab the helper adds one automatically.
                /// • Respects <see cref="SaveSettings.usePrefabPooling"/> – when enabled instances are
                ///   spawned from <see cref="SaveablePrefabPoolCache"/> and returned via
                ///   <see cref="Destroy"/> which calls <c>TryDespawn</c>; otherwise classic
                ///   <c>Instantiate</c>/<c>Destroy</c> is used.
                /// • Individual prefab "Disable Pooling" settings always take precedence over global 
                ///   pooling settings and overrides, checked in this order:
                ///   1. <see cref="SaveablePrefab.DisablePooling"/> property on the component
                ///   2. "Disable Pooling" setting in <see cref="PrefabRegistry"/> entry
                /// </remarks>
                public static SaveablePrefab Instantiate(
                        GameObject prefabAsset,
                        Vector3 position,
                        Quaternion rotation,
                        string sceneName,
                        Transform parent = null,
                        bool registerWithSaveSystem = true,
                        bool? overrideUsePooling = null)
                {
                        if (!string.IsNullOrEmpty(sceneName))
                                SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
                        return Instantiate(prefabAsset, position, rotation, parent, registerWithSaveSystem, overrideUsePooling);
                }

                public static SaveablePrefab Instantiate(
                        GameObject prefabAsset,
                        Vector3 position,
                        Quaternion rotation,
                        Transform parent = null,
                        bool registerWithSaveSystem = true,
                        bool? overrideUsePooling = null)
                {
                        if (!prefabAsset)
                        {
                                Debug.LogWarning("SaveablePrefabFactory.Instantiate: prefabAsset is null");
                                return null;
                        }

                        var settings = SaveManager.Instance?.SaveSettings;
                        bool usePooling = settings?.usePrefabPooling ?? false;

                        if (overrideUsePooling.HasValue)
                                usePooling = overrideUsePooling.Value;

                        var prefabComp = prefabAsset.GetComponent<SaveablePrefab>() ??
                                         prefabAsset.AddComponent<SaveablePrefab>();

                        PrefabRegistry prefabRegistry = null;

                        if (string.IsNullOrEmpty(prefabComp.PrefabAssetID))
                        {
                                prefabComp.GenerateAndRegisterPrefabAssetIDAtRuntime();
                        }
                        else
                        {
                                prefabRegistry = AssetProvider.Load<PrefabRegistry>("PrefabRegistry");
                                prefabRegistry?.TryAddPrefab(prefabComp.PrefabAssetID, prefabAsset, out _);
                        }

                        prefabRegistry ??= AssetProvider.Load<PrefabRegistry>("PrefabRegistry");

                        // Check if pooling is disabled directly on the SaveablePrefab component - this always takes precedence
                        if (usePooling && prefabComp.DisablePooling)
                        {
                                usePooling = false;
                        }
                        // If not disabled on component, check the PrefabRegistry setting
                        else if (usePooling && prefabRegistry != null &&
                            prefabRegistry.IsPoolingDisabled(prefabComp.PrefabAssetID))
                        {
                                usePooling = false;
                        }

                        if (usePooling)
                        {
                                int poolSize = prefabRegistry?.ResolvePoolSize(
                                                       prefabComp.PrefabAssetID,
                                                       settings?.defaultPrefabPoolSize ?? 0)
                                                ?? (settings?.defaultPrefabPoolSize ?? 0);

                                var pool = SaveablePrefabPoolCache.Get(
                                        prefabComp,
                                        poolSize,
                                        registerWithSaveSystem);

                                SaveablePrefab sp = pool?.Spawn(position, rotation);
                                if (sp == null) return null;
                                if (parent) sp.transform.SetParent(parent, true);

                                sp.SetOriginalPrefabAsset(prefabAsset);
                                sp.MarkAsAddedAtRuntime();

                                if (!registerWithSaveSystem && sp.RegisterWithSaveSystem)
                                {
                                        sp.UnregisterFromSaving();
                                        sp.RegisterWithSaveSystem = false;
                                }

                                return sp;
                        }

                        // 1 ▸ instantiate + optional parenting
                        GameObject go = UnityEngine.Object.Instantiate(prefabAsset, position, rotation);
                        if (parent) go.transform.SetParent(parent, true);

                        // 2 ▸ ensure SaveablePrefab component
                        SaveablePrefab spFallback = go.GetComponent<SaveablePrefab>() ??
                                                                  go.AddComponent<SaveablePrefab>();

                        // 3 ▸ honor save-system flag and initialize so inactive instances
                        //     get a UniqueID and register correctly
                        spFallback.RegisterWithSaveSystem = registerWithSaveSystem;
                        spFallback.InitializeInstance();

                        // 4 ▸ store reference to original asset (needed for diff logic)
                        spFallback.SetOriginalPrefabAsset(prefabAsset);

                        // 5 ▸ guarantee PrefabAssetID + registry entry
                        if (string.IsNullOrEmpty(spFallback.PrefabAssetID))
                                spFallback.GenerateAndRegisterPrefabAssetIDAtRuntime();
                        else
                                prefabRegistry?.TryAddPrefab(spFallback.PrefabAssetID, prefabAsset, out _);

                        // 6 ▸ runtime flag + optional save-system registration
                        spFallback.MarkAsAddedAtRuntime();
                        if (registerWithSaveSystem) spFallback.RegisterForSaving();

                        return spFallback;
                }

                public static void Destroy(SaveablePrefab inst)
                {
                        if (!inst) return;

                        var settings = SaveManager.Instance?.SaveSettings;
                        bool usePooling = settings?.usePrefabPooling ?? false;
                        PrefabRegistry prefabRegistry = null;

                        if (usePooling)
                        {
                                // Check if pooling is disabled directly on the SaveablePrefab component first
                                if (inst.DisablePooling)
                                {
                                        usePooling = false;
                                }
                                // If not disabled on component, check the PrefabRegistry setting
                                else
                                {
                                        prefabRegistry = AssetProvider.Load<PrefabRegistry>("PrefabRegistry");
                                        if (prefabRegistry != null &&
                                            prefabRegistry.IsPoolingDisabled(inst.PrefabAssetID))
                                        {
                                                usePooling = false;
                                        }
                                }
                        }

                        if (usePooling)
                        {
                                int poolSize = prefabRegistry?.ResolvePoolSize(
                                                       inst.PrefabAssetID,
                                                       settings?.defaultPrefabPoolSize ?? 0)
                                                ?? (settings?.defaultPrefabPoolSize ?? 0);

                                SaveablePrefabPoolCache.TryDespawn(
                                        inst,
                                        poolSize,
                                        inst.RegisterWithSaveSystem);
                        }
                        else
                        {
                                if (SaveManager.Instance != null)
                                        SaveManager.Instance.DestroyWithSnapshot(inst.gameObject);
                                else
                                        UnityEngine.Object.Destroy(inst.gameObject);
                        }
                }

                /// <summary>
                /// Instantiates a SaveablePrefab and forces generation of a new
                /// instance ID even if the prefab asset already has one.
                /// </summary>
                public static SaveablePrefab InstantiateWithFreshID(
                        GameObject prefabAsset,
                        Vector3 position,
                        Quaternion rotation,
                        string sceneName,
                        Transform parent = null,
                        bool registerWithSaveSystem = true,
                        bool? overrideUsePooling = null)
                {
                        if (!string.IsNullOrEmpty(sceneName))
                                SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
                        return InstantiateWithFreshID(prefabAsset, position, rotation, parent, registerWithSaveSystem, overrideUsePooling);
                }

                public static SaveablePrefab InstantiateWithFreshID(
                        GameObject prefabAsset,
                        Vector3 position,
                        Quaternion rotation,
                        Transform parent = null,
                        bool registerWithSaveSystem = true,
                        bool? overrideUsePooling = null)
                {
                        var sp = Instantiate(
                                prefabAsset,
                                position,
                                rotation,
                                parent,
                                registerWithSaveSystem,
                                overrideUsePooling);

                        if (sp == null) return null;

                        sp.UnregisterFromSaving();
                        sp.ClearRegisteredFlag();
                        sp.SetUniqueID(Guid.NewGuid().ToString());

                        return sp;
                }
        }
}
#endif
