#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Utility methods for saving procedurally generated or modified GameObjects.
    /// 
    /// <para><b>Two Scenarios:</b></para>
    /// <list type="bullet">
    ///   <item>
    ///     <b>Scenario 1 – Prefab Variations:</b> Runtime modifications to a prefab instance
    ///     (added components, material swaps, child changes). Use <see cref="ConfigureForPrefabVariations"/>.
    ///   </item>
    ///   <item>
    ///     <b>Scenario 2 – Truly Procedural:</b> GameObjects created entirely from code with no prefab base.
    ///     Use <see cref="ConfigureForProceduralGameObject"/>. This approach is resource-intensive; see documentation.
    ///   </item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// See "Procedural GameObjects Documentation.md" in the Documentation folder for a detailed guide
    /// on when to use each approach and performance considerations.
    /// </remarks>
    public static class ProceduralSaveUtility
    {
        #region ══════════════════════════════════════════════════════════════
        //  SCENARIO 1 – PREFAB VARIATIONS
        //  (Recommended: efficient, uses prefab diffing)
        #endregion

        /// <summary>
        /// Configures a prefab instance to track runtime modifications (added components, material swaps, etc.).
        /// This is the recommended approach for procedural content that starts from a prefab base.
        /// </summary>
        /// <param name="prefab">The SaveablePrefab instance to configure.</param>
        /// <param name="trackAddedComponents">Enable tracking of components added at runtime.</param>
        /// <param name="trackComponentBlobs">Enable tracking of custom ISaveable data from added components.</param>
        /// <param name="trackMaterialOverrides">Enable tracking of material slot changes.</param>
        /// <param name="trackChildStateOverrides">Enable tracking of child object active/tag/layer changes.</param>
        /// <param name="trackChildTransformOverrides">Enable tracking of child transform changes.</param>
        /// <param name="trackMeshOverrides">Enable tracking of mesh swaps (MeshFilter.sharedMesh, SkinnedMeshRenderer.sharedMesh).</param>
        /// <param name="trackBlendshapeOverrides">Enable tracking of blendshape values on SkinnedMeshRenderers.</param>
        /// <param name="trackTextureOverrides">Enable tracking of texture swaps on materials.</param>
        /// <param name="trackParticleSnapshots">Enable tracking of particle system state.</param>
        /// <param name="trackColliderSettings">Enable tracking of collider configurations.</param>
        /// <returns>The configured SaveablePrefab for method chaining.</returns>
        /// <example>
        /// <code>
        /// // Instantiate a base "GenericItem" prefab and customize it at runtime
        /// var instance = SaveablePrefabFactory.Instantiate(genericItemPrefab, position, rotation);
        /// 
        /// // Configure for runtime modifications
        /// ProceduralSaveUtility.ConfigureForPrefabVariations(instance);
        /// 
        /// // Now add components dynamically – they'll be saved/restored automatically
        /// instance.gameObject.AddComponent&lt;HealthComponent&gt;();
        /// instance.gameObject.AddComponent&lt;InventorySlot&gt;();
        /// </code>
        /// </example>
        public static SaveablePrefab ConfigureForPrefabVariations(
            SaveablePrefab prefab,
            bool trackAddedComponents = true,
            bool trackComponentBlobs = true,
            bool trackMaterialOverrides = false,
            bool trackChildStateOverrides = true,
            bool trackChildTransformOverrides = false,
            bool trackMeshOverrides = false,
            bool trackBlendshapeOverrides = false,
            bool trackTextureOverrides = false,
            bool trackParticleSnapshots = false,
            bool trackColliderSettings = false)
        {
            if (prefab == null)
            {
                Debug.LogWarning("ProceduralSaveUtility.ConfigureForPrefabVariations: prefab is null");
                return null;
            }

            prefab.TrackAddedComponents = trackAddedComponents;
            prefab.TrackComponentBlobs = trackComponentBlobs;
            prefab.TrackMaterialOverrides = trackMaterialOverrides;
            prefab.TrackChildStateOverrides = trackChildStateOverrides;
            prefab.TrackChildTransformOverrides = trackChildTransformOverrides;
            prefab.TrackSkinnedMeshOverrides = trackMeshOverrides;
            prefab.TrackBlendshapeOverrides = trackBlendshapeOverrides;
            prefab.TrackTextureOverrides = trackTextureOverrides;
            prefab.TrackParticleSnapshots = trackParticleSnapshots;
            prefab.TrackColliderSettings = trackColliderSettings;

            return prefab;
        }

        /// <summary>
        /// Instantiates a prefab and configures it for runtime modifications in one call.
        /// Combines <see cref="SaveablePrefabFactory.Instantiate"/> with <see cref="ConfigureForPrefabVariations"/>.
        /// </summary>
        /// <param name="prefabAsset">The prefab asset to instantiate.</param>
        /// <param name="position">World position for the instance.</param>
        /// <param name="rotation">World rotation for the instance.</param>
        /// <param name="parent">Optional parent transform.</param>
        /// <param name="trackAddedComponents">Enable tracking of components added at runtime.</param>
        /// <param name="trackComponentBlobs">Enable tracking of custom ISaveable data.</param>
        /// <returns>The configured SaveablePrefab instance.</returns>
        public static SaveablePrefab InstantiateWithVariationTracking(
            GameObject prefabAsset,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            bool trackAddedComponents = true,
            bool trackComponentBlobs = true)
        {
            var instance = SaveablePrefabFactory.Instantiate(
                prefabAsset, position, rotation, parent, registerWithSaveSystem: true);

            if (instance == null) return null;

            return ConfigureForPrefabVariations(
                instance,
                trackAddedComponents,
                trackComponentBlobs,
                trackMaterialOverrides: false,
                trackChildStateOverrides: true,
                trackChildTransformOverrides: false);
        }

        #region ══════════════════════════════════════════════════════════════
        //  SCENARIO 2 – TRULY PROCEDURAL GAMEOBJECTS
        //  (Resource-intensive: only use when no prefab base is possible)
        #endregion

        /// <summary>
        /// Configures a purely procedural GameObject (created entirely from code) for saving.
        /// 
        /// <para><b>⚠️ WARNING: This approach is resource-intensive!</b></para>
        /// <para>
        /// Unity's serialization and instantiation systems are optimized for prefabs. When you save
        /// a "truly procedural" GameObject, Crystal Save must serialize the complete component graph
        /// including all field values. On load, it must:
        /// <list type="number">
        ///   <item>Create a new GameObject from scratch</item>
        ///   <item>Add each component via <c>AddComponent()</c> calls (slow reflection)</item>
        ///   <item>Deserialize and apply every field value</item>
        /// </list>
        /// This is orders of magnitude slower than prefab instantiation which uses Unity's
        /// native binary deserialization.
        /// </para>
        /// 
        /// <para><b>When to use Scenario 2:</b></para>
        /// <list type="bullet">
        ///   <item>The object structure is completely dynamic and cannot be predicted</item>
        ///   <item>Performance during save/load is not critical (e.g., single save on quit)</item>
        ///   <item>The number of procedural objects is very small (&lt;10)</item>
        /// </list>
        /// 
        /// <para><b>Prefer Scenario 1 (Prefab Variations) when:</b></para>
        /// <list type="bullet">
        ///   <item>You can create a "blank" or "generic" prefab as a base</item>
        ///   <item>You have many similar procedural objects</item>
        ///   <item>Load time performance matters</item>
        /// </list>
        /// </summary>
        /// <param name="gameObject">The procedural GameObject to configure.</param>
        /// <param name="emptyPrefabAsset">
        /// A minimal prefab asset to use as the restoration base. This should be a blank GameObject
        /// with just a SaveablePrefab component. If null, the system will attempt to use an internal
        /// empty prefab, but providing one is recommended for reliability.
        /// </param>
        /// <returns>The SaveablePrefab component added to the GameObject.</returns>
        /// <example>
        /// <code>
        /// // Create a purely procedural object
        /// var go = new GameObject("ProceduralThing");
        /// go.AddComponent&lt;MeshFilter&gt;();
        /// go.AddComponent&lt;MeshRenderer&gt;();
        /// go.AddComponent&lt;CustomBehavior&gt;();
        /// 
        /// // Configure it for saving (use a blank prefab as the base)
        /// var saveable = ProceduralSaveUtility.ConfigureForProceduralGameObject(go, emptyPrefabAsset);
        /// </code>
        /// </example>
        public static SaveablePrefab ConfigureForProceduralGameObject(
            GameObject gameObject,
            GameObject emptyPrefabAsset = null)
        {
            if (gameObject == null)
            {
                Debug.LogWarning("ProceduralSaveUtility.ConfigureForProceduralGameObject: gameObject is null");
                return null;
            }

            // Get or add SaveablePrefab
            var prefab = gameObject.GetComponent<SaveablePrefab>();
            if (prefab == null)
            {
                prefab = gameObject.AddComponent<SaveablePrefab>();
            }

            // Enable all tracking features needed for truly procedural objects
            // These are all enabled by default because procedural objects often need:
            // - Component tracking for dynamically added components
            // - Mesh/Material tracking for procedurally generated or assigned visuals
            // - Transform/State tracking for child objects
            // - Particle/Collider tracking for physics and effects
            prefab.TrackAddedComponents = true;
            prefab.TrackComponentBlobs = true;
            prefab.TrackChildStateOverrides = true;
            prefab.TrackChildTransformOverrides = true;
            prefab.TrackSkinnedMeshOverrides = true;    // Track mesh swaps (MeshFilter.sharedMesh, SkinnedMeshRenderer.sharedMesh)
            prefab.TrackMaterialOverrides = true;       // Track material swaps (Renderer.sharedMaterials)
            prefab.TrackBlendshapeOverrides = true;     // Track blendshape values
            prefab.TrackTextureOverrides = true;        // Track texture swaps on materials
            prefab.TrackParticleSnapshots = true;       // Track particle system state
            prefab.TrackColliderSettings = true;        // Track collider configurations

            // For procedural objects, we DO NOT set originalPrefabAsset because:
            // 1. Procedural objects have no "original" to compare against
            // 2. All components/meshes/materials are "new" by definition
            // 3. Setting it to a blank prefab causes MissingComponentException when comparing
            //
            // We only use the emptyPrefabAsset for its PrefabAssetID (needed for re-instantiation on load)
            if (emptyPrefabAsset != null)
            {
                // DO NOT call: prefab.SetOriginalPrefabAsset(emptyPrefabAsset);
                // This would cause issues when comparing meshes/materials against a blank prefab

                // Ensure the empty prefab has a SaveablePrefab component for registry
                var assetPrefab = emptyPrefabAsset.GetComponent<SaveablePrefab>();
                if (assetPrefab == null)
                {
                    Debug.LogWarning(
                        "ProceduralSaveUtility: The emptyPrefabAsset should have a SaveablePrefab component. " +
                        "Add one in the editor for reliable save/load.");
                }
                else if (string.IsNullOrEmpty(assetPrefab.PrefabAssetID))
                {
                    // Generate and register the prefab asset ID
                    assetPrefab.GenerateAndRegisterPrefabAssetIDAtRuntime();
                }

                // Copy the PrefabAssetID from the asset (uses the property setter which only works if currently empty)
                if (assetPrefab != null && !string.IsNullOrEmpty(assetPrefab.PrefabAssetID))
                {
                    prefab.PrefabAssetID = assetPrefab.PrefabAssetID;
                }
            }

            // Generate unique instance ID
            if (string.IsNullOrEmpty(prefab.UniqueID))
            {
                prefab.SetUniqueID(Guid.NewGuid().ToString());
            }

            // Mark as runtime-added for proper tracking
            prefab.MarkAsAddedAtRuntime();

            // Register with save system
            prefab.RegisterWithSaveSystem = true;
            prefab.RegisterForSaving();

            return prefab;
        }

        /// <summary>
        /// Creates a new empty GameObject configured for procedural component additions.
        /// This is a convenience method for Scenario 2.
        /// </summary>
        /// <param name="name">Name for the new GameObject.</param>
        /// <param name="position">World position.</param>
        /// <param name="rotation">World rotation.</param>
        /// <param name="emptyPrefabAsset">A minimal prefab asset to use as the restoration base.</param>
        /// <returns>A new GameObject with SaveablePrefab configured for procedural content.</returns>
        /// <remarks>
        /// <b>⚠️ This uses Scenario 2 which is resource-intensive.</b> See 
        /// <see cref="ConfigureForProceduralGameObject"/> for details on when this is appropriate.
        /// </remarks>
        public static GameObject CreateProceduralGameObject(
            string name,
            Vector3 position,
            Quaternion rotation,
            GameObject emptyPrefabAsset = null)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.transform.rotation = rotation;

            ConfigureForProceduralGameObject(go, emptyPrefabAsset);

            return go;
        }

        #region ══════════════════════════════════════════════════════════════
        //  HELPER METHODS
        #endregion

        /// <summary>
        /// Adds a component to a SaveablePrefab instance.
        /// The component will be automatically detected as runtime-added based on comparison
        /// with the original prefab asset.
        /// </summary>
        /// <typeparam name="T">The component type to add.</typeparam>
        /// <param name="prefab">The SaveablePrefab to add the component to.</param>
        /// <returns>The newly added component.</returns>
        /// <remarks>
        /// Crystal Save automatically detects runtime-added components by comparing the instance's
        /// components against the original prefab asset. Any component type not present on the
        /// original prefab is considered "runtime-added" and will be serialized when
        /// <see cref="SaveablePrefab.TrackAddedComponents"/> is enabled.
        /// </remarks>
        public static T AddTrackedComponent<T>(SaveablePrefab prefab) where T : Component
        {
            if (prefab == null)
            {
                Debug.LogWarning("ProceduralSaveUtility.AddTrackedComponent: prefab is null");
                return null;
            }

            // Ensure tracking is enabled
            if (!prefab.TrackAddedComponents)
            {
                Debug.LogWarning(
                    "ProceduralSaveUtility.AddTrackedComponent: TrackAddedComponents is disabled on this prefab. " +
                    "The component will be added but may not be saved. Enable tracking via ConfigureForPrefabVariations().");
            }

            return prefab.gameObject.AddComponent<T>();
        }

        /// <summary>
        /// Adds a component by type to a SaveablePrefab instance.
        /// </summary>
        /// <param name="prefab">The SaveablePrefab to add the component to.</param>
        /// <param name="componentType">The type of component to add.</param>
        /// <returns>The newly added component.</returns>
        public static Component AddTrackedComponent(SaveablePrefab prefab, Type componentType)
        {
            if (prefab == null)
            {
                Debug.LogWarning("ProceduralSaveUtility.AddTrackedComponent: prefab is null");
                return null;
            }

            if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
            {
                Debug.LogWarning("ProceduralSaveUtility.AddTrackedComponent: invalid component type");
                return null;
            }

            // Ensure tracking is enabled
            if (!prefab.TrackAddedComponents)
            {
                Debug.LogWarning(
                    "ProceduralSaveUtility.AddTrackedComponent: TrackAddedComponents is disabled on this prefab. " +
                    "The component will be added but may not be saved. Enable tracking via ConfigureForPrefabVariations().");
            }

            return prefab.gameObject.AddComponent(componentType);
        }

        /// <summary>
        /// Checks if a SaveablePrefab is properly configured for procedural content saving.
        /// </summary>
        /// <param name="prefab">The SaveablePrefab to validate.</param>
        /// <returns>True if the prefab has the minimum required settings for procedural saving.</returns>
        public static bool IsConfiguredForProceduralSaving(SaveablePrefab prefab)
        {
            if (prefab == null) return false;

            return prefab.TrackAddedComponents && prefab.TrackComponentBlobs;
        }
    }
}
#endif
