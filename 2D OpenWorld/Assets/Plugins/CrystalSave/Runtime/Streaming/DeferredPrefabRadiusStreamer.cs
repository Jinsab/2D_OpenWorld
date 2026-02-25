#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime
{
        /// <summary>
        /// Streams deferred prefabs in when the player approaches them.
        /// Listens for deferred prefab batches, caches them per scene, and
        /// requests instantiation once entries fall within the configured radius.
        /// </summary>
        [AddComponentMenu("Crystal Save/Streaming/Deferred Prefab Radius Streamer")]
        public sealed class DeferredPrefabRadiusStreamer : MonoBehaviour
        {
                [Header("Streaming Target")]
                [Tooltip("Transform used as the distance origin when deciding which prefabs to spawn.")]
                [SerializeField] private Transform playerTransform;

                [HideInInspector]
                [SerializeField] private PrefabManager prefabManagerOverride;

                [Header("Streaming Settings")]
                [Min(0f)]
                [Tooltip("Deferred prefabs within this radius (in world units) from the player are spawned.")]
                [SerializeField] private float activationRadius = 40f;

                [Min(0f)]
                [Tooltip("Interval (in seconds) between radius evaluations.")]
                [SerializeField] private float refreshInterval = 0.5f;

#if UNITY_EDITOR
                [Header("Debug Visualization")]
                [Tooltip("Show the activation radius sphere in the Scene view.")]
                [SerializeField] private bool showDebugRadius = true;

                [Tooltip("Color of the activation radius sphere.")]
                [SerializeField] private Color debugRadiusColor = new Color(0f, 1f, 0f, 0.3f);

                [Tooltip("Show deferred prefab positions as spheres in the Scene view.")]
                [SerializeField] private bool showDeferredPrefabPositions = true;

                [Tooltip("Color for deferred prefabs outside activation radius.")]
                [SerializeField] private Color deferredPrefabColor = new Color(1f, 0.5f, 0f, 0.6f);

                [Tooltip("Color for deferred prefabs inside activation radius (about to spawn).")]
                [SerializeField] private Color activePrefabColor = new Color(0f, 1f, 1f, 0.8f);

                [Tooltip("Size of the sphere markers for deferred prefabs.")]
                [SerializeField] private float prefabMarkerSize = 0.5f;
#endif

                private readonly Dictionary<string, Dictionary<string, SaveablePrefabData>> deferredPrefabCache = new(StringComparer.Ordinal);
                private readonly Dictionary<string, HashSet<string>> deferredComponentCache = new(StringComparer.Ordinal);
                private PrefabManager activePrefabManager;
                private ComponentManager activeComponentManager;
                private float nextEvaluationTime;
                private bool loggedMissingSaveManagerForComponents;

                private void OnEnable()
                {
                        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
                        SaveManager.Initialized += HandleSaveManagerInitialized;

                        AttachToPrefabManager(ResolvePrefabManager());
                        AttachToComponentManager(ResolveComponentManager());
                        PrimeCaches();
                        ScheduleNextEvaluation(immediate: true);
                }

                private void OnDisable()
                {
                        SaveManager.Initialized -= HandleSaveManagerInitialized;
                        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
                        DetachFromPrefabManager();
                        DetachFromComponentManager();
                        deferredPrefabCache.Clear();
                        deferredComponentCache.Clear();
                        loggedMissingSaveManagerForComponents = false;
                }

                private void Update()
                {
                        if (Time.time < nextEvaluationTime)
                                return;

                        ScheduleNextEvaluation();
                        EvaluateStreaming();
                }

                private void HandleSaveManagerInitialized(SaveManager manager)
                {
                        AttachToPrefabManager(ResolvePrefabManager());
                        AttachToComponentManager(ResolveComponentManager());
                        PrimeCaches();
                }

                private void HandleActiveSceneChanged(Scene previous, Scene next)
                {
                        if (!next.IsValid())
                                return;

                        RefreshPrefabCache(next.name);
                        RefreshPrefabCache(string.Empty);
                        RefreshComponentCache(next.name);
                        RefreshComponentCache(string.Empty);
                        ScheduleNextEvaluation(immediate: true);
                }

                private PrefabManager ResolvePrefabManager()
                {
                        if (prefabManagerOverride != null)
                                return prefabManagerOverride;

                        return SaveManager.Instance?.GetPrefabManager;
                }

                private void AttachToPrefabManager(PrefabManager manager)
                {
                        if (activePrefabManager == manager)
                                return;

                        DetachFromPrefabManager();

                        if (manager == null)
                                return;

                        activePrefabManager = manager;
                        activePrefabManager.OnDeferredPrefabsQueued += HandleDeferredPrefabsQueued;
                }

                private void DetachFromPrefabManager()
                {
                        if (activePrefabManager == null)
                                return;

                        activePrefabManager.OnDeferredPrefabsQueued -= HandleDeferredPrefabsQueued;
                        activePrefabManager = null;
                }

                private ComponentManager ResolveComponentManager()
                {
                        return ComponentManager.Instance;
                }

                private void AttachToComponentManager(ComponentManager manager)
                {
                        if (activeComponentManager == manager)
                                return;

                        DetachFromComponentManager();

                        if (manager == null)
                                return;

                        activeComponentManager = manager;
                        activeComponentManager.OnDeferredComponentsQueued += HandleDeferredComponentsQueued;
                }

                private void DetachFromComponentManager()
                {
                        if (activeComponentManager == null)
                                return;

                        activeComponentManager.OnDeferredComponentsQueued -= HandleDeferredComponentsQueued;
                        activeComponentManager = null;
                }

                private void HandleDeferredPrefabsQueued(IReadOnlyList<SaveablePrefabData> prefabs)
                {
                        if (prefabs == null || prefabs.Count == 0)
                                return;

                        var affectedScenes = new HashSet<string>(StringComparer.Ordinal);
                        foreach (var data in prefabs)
                        {
                                if (data == null)
                                        continue;

                                string sceneName = string.IsNullOrEmpty(data.HomeScene) ? string.Empty : data.HomeScene;
                                if (!affectedScenes.Contains(sceneName))
                                        affectedScenes.Add(sceneName);
                        }

                        foreach (var sceneName in affectedScenes)
                                RefreshPrefabCache(sceneName);
                }

                private void HandleDeferredComponentsQueued(IReadOnlyCollection<string> sceneKeys)
                {
                        if (sceneKeys == null || sceneKeys.Count == 0)
                                return;

                        foreach (var sceneKey in sceneKeys)
                        {
                                string normalized = string.IsNullOrEmpty(sceneKey) ? string.Empty : sceneKey;
                                RefreshComponentCache(normalized);
                        }
                }

                private void PrimeCaches()
                {
                        PrimePrefabCaches();
                        PrimeComponentCaches();
                }

                private void PrimePrefabCaches()
                {
                        if (activePrefabManager == null)
                                return;

                        foreach (var sceneKey in activePrefabManager.GetDeferredSceneKeys().ToList())
                                RefreshPrefabCache(sceneKey);
                }

                private void PrimeComponentCaches()
                {
                        if (activeComponentManager == null)
                                return;

                        foreach (var sceneKey in activeComponentManager.GetDeferredSceneKeys().ToList())
                                RefreshComponentCache(sceneKey);
                }

                private void RefreshPrefabCache(string sceneName)
                {
                        if (activePrefabManager == null)
                                return;

                        var snapshot = activePrefabManager.PeekDeferredPrefabsForScene(sceneName);
                        string key = string.IsNullOrEmpty(sceneName) ? string.Empty : sceneName;

                        if (snapshot == null || snapshot.Count == 0)
                        {
                                deferredPrefabCache.Remove(key);
                                return;
                        }

                        if (!deferredPrefabCache.TryGetValue(key, out var cacheForScene))
                        {
                                cacheForScene = new Dictionary<string, SaveablePrefabData>(StringComparer.Ordinal);
                                deferredPrefabCache[key] = cacheForScene;
                        }
                        else
                        {
                                cacheForScene.Clear();
                        }

                        foreach (var entry in snapshot)
                        {
                                if (entry == null || string.IsNullOrEmpty(entry.InstanceID))
                                        continue;

                                cacheForScene[entry.InstanceID] = entry;
                        }
                }

                private void RefreshComponentCache(string sceneName)
                {
                        if (activeComponentManager == null)
                                return;

                        var snapshot = activeComponentManager.PeekDeferredComponentsForScene(sceneName);
                        string key = string.IsNullOrEmpty(sceneName) ? string.Empty : sceneName;

                        if (snapshot == null || snapshot.Count == 0)
                        {
                                deferredComponentCache.Remove(key);
                                return;
                        }

                        if (!deferredComponentCache.TryGetValue(key, out var cacheForScene))
                        {
                                cacheForScene = new HashSet<string>(StringComparer.Ordinal);
                                deferredComponentCache[key] = cacheForScene;
                        }
                        else
                        {
                                cacheForScene.Clear();
                        }

                        foreach (var uniqueID in snapshot)
                        {
                                if (string.IsNullOrEmpty(uniqueID))
                                        continue;

                                cacheForScene.Add(uniqueID);
                        }
                }

                private void EvaluateStreaming()
                {
                        if (playerTransform == null)
                                return;

                        var activeScene = SceneManager.GetActiveScene();
                        string sceneName = activeScene.IsValid() ? activeScene.name : string.Empty;

                        var playerPosition = playerTransform.position;
                        float activationRadiusSqr = activationRadius * activationRadius;

                        if (activePrefabManager != null)
                                EvaluatePrefabStreaming(sceneName, playerPosition, activationRadiusSqr);

                        if (activeComponentManager != null)
                                EvaluateComponentStreaming(sceneName, playerPosition, activationRadiusSqr);
                }

                private void EvaluatePrefabStreaming(string sceneName, Vector3 playerPosition, float activationRadiusSqr)
                {
                        var lookup = BuildPrefabSceneLookup(sceneName);
                        if (lookup.Count == 0)
                                return;

                        var instantiatedPrefabs = activePrefabManager.GetInstantiatedPrefabs();
                        if (instantiatedPrefabs.Count > 0)
                                RemovePrefabsFromCache(instantiatedPrefabs.Keys);

                        var idsToSpawn = new HashSet<string>(StringComparer.Ordinal);

                        foreach (var data in lookup.Values)
                        {
                                if (data == null || string.IsNullOrEmpty(data.InstanceID))
                                        continue;

                                if (!TryGetWorldTransform(data, lookup, instantiatedPrefabs, out var position, out _))
                                        continue;

                                if ((position - playerPosition).sqrMagnitude > activationRadiusSqr)
                                        continue;

                                if (!idsToSpawn.Add(data.InstanceID))
                                        continue;

                                IncludeParentCluster(data, lookup, idsToSpawn);
                        }

                        if (idsToSpawn.Count == 0)
                                return;

                        activePrefabManager.ProcessDeferredPrefabsByInstanceIDs(idsToSpawn);
                        RemovePrefabsFromCache(idsToSpawn);
                        RefreshPrefabCache(sceneName);
                        RefreshPrefabCache(string.Empty);
                }

                private void EvaluateComponentStreaming(string sceneName, Vector3 playerPosition, float activationRadiusSqr)
                {
                        var componentIDs = BuildComponentIDSet(sceneName);
                        if (componentIDs.Count == 0)
                                return;

                        var saveManager = SaveManager.Instance;
                        if (saveManager == null)
                        {
                                if (!loggedMissingSaveManagerForComponents)
                                {
                                        Logger.Log(
                                                "DeferredPrefabRadiusStreamer: SaveManager unavailable for deferred component streaming.",
                                                LogLevel.Warning
                                        );
                                        loggedMissingSaveManagerForComponents = true;
                                }

                                return;
                        }

                        loggedMissingSaveManagerForComponents = false;

                        var idsToProcess = new HashSet<string>(StringComparer.Ordinal);
                        var missingIDs = new HashSet<string>(StringComparer.Ordinal);

                        foreach (var uniqueID in componentIDs)
                        {
                                if (string.IsNullOrEmpty(uniqueID))
                                        continue;

                                string ownerID = ExtractGameObjectUniqueID(uniqueID);
                                if (string.IsNullOrEmpty(ownerID))
                                {
                                        Logger.Log(
                                                $"DeferredPrefabRadiusStreamer: Unable to resolve owner ID for deferred component '{uniqueID}'.",
                                                LogLevel.Warning
                                        );
                                        missingIDs.Add(uniqueID);
                                        continue;
                                }

                                var owner = saveManager.FindGameObjectByUniqueID(ownerID, SaveManager.IdentifierType.UniqueID);
                                if (owner == null)
                                {
                                        Logger.Log(
                                                $"DeferredPrefabRadiusStreamer: Missing GameObject '{ownerID}' for deferred component '{uniqueID}'.",
                                                LogLevel.Warning
                                        );
                                        missingIDs.Add(uniqueID);
                                        continue;
                                }

                                if ((owner.transform.position - playerPosition).sqrMagnitude > activationRadiusSqr)
                                        continue;

                                idsToProcess.Add(uniqueID);
                        }

                        if (missingIDs.Count > 0)
                                RemoveComponentIDsFromCache(missingIDs);

                        if (idsToProcess.Count > 0)
                        {
                                activeComponentManager.ProcessDeferredComponentsByUniqueIDs(idsToProcess);
                                RemoveComponentIDsFromCache(idsToProcess);
                        }

                        if (missingIDs.Count == 0 && idsToProcess.Count == 0)
                                return;

                        RefreshComponentCache(sceneName);
                        RefreshComponentCache(string.Empty);
                }

                private Dictionary<string, SaveablePrefabData> BuildPrefabSceneLookup(string sceneName)
                {
                        var lookup = new Dictionary<string, SaveablePrefabData>(StringComparer.Ordinal);

                        if (deferredPrefabCache.TryGetValue(string.Empty, out var globalCache))
                        {
                                foreach (var kvp in globalCache)
                                {
                                        if (!lookup.ContainsKey(kvp.Key))
                                                lookup.Add(kvp.Key, kvp.Value);
                                }
                        }

                        if (!string.IsNullOrEmpty(sceneName) && deferredPrefabCache.TryGetValue(sceneName, out var sceneCache))
                        {
                                foreach (var kvp in sceneCache)
                                        lookup[kvp.Key] = kvp.Value;
                        }

                        return lookup;
                }

                private void RemovePrefabsFromCache(IEnumerable<string> instanceIDs)
                {
                        if (instanceIDs == null)
                                return;

                        var ids = instanceIDs as ICollection<string> ?? instanceIDs.ToArray();
                        if (ids.Count == 0)
                                return;

                        foreach (var cache in deferredPrefabCache.Values)
                        {
                                foreach (var id in ids)
                                        cache.Remove(id);
                        }
                }

                private HashSet<string> BuildComponentIDSet(string sceneName)
                {
                        var ids = new HashSet<string>(StringComparer.Ordinal);

                        if (deferredComponentCache.TryGetValue(string.Empty, out var globalCache))
                        {
                                foreach (var id in globalCache)
                                        ids.Add(id);
                        }

                        if (!string.IsNullOrEmpty(sceneName) && deferredComponentCache.TryGetValue(sceneName, out var sceneCache))
                        {
                                foreach (var id in sceneCache)
                                        ids.Add(id);
                        }

                        return ids;
                }

                private void RemoveComponentIDsFromCache(IEnumerable<string> uniqueIDs)
                {
                        if (uniqueIDs == null)
                                return;

                        var ids = uniqueIDs as ICollection<string> ?? uniqueIDs.Where(id => !string.IsNullOrEmpty(id)).ToArray();
                        if (ids.Count == 0)
                                return;

                        foreach (var cache in deferredComponentCache.Values)
                        {
                                foreach (var id in ids)
                                        cache.Remove(id);
                        }
                }

                private static string ExtractGameObjectUniqueID(string componentUniqueID)
                {
                        if (string.IsNullOrEmpty(componentUniqueID))
                                return string.Empty;

                        int separatorIndex = componentUniqueID.IndexOf('_');
                        return separatorIndex < 0 ? componentUniqueID : componentUniqueID.Substring(0, separatorIndex);
                }

                private bool TryGetWorldTransform(
                        SaveablePrefabData data,
                        IDictionary<string, SaveablePrefabData> lookup,
                        Dictionary<string, GameObject> instantiatedPrefabs,
                        out Vector3 position,
                        out Quaternion rotation)
                {
                        position = Vector3.zero;
                        rotation = Quaternion.identity;

                        if (data == null)
                                return false;

                        var visited = new HashSet<string>(StringComparer.Ordinal);
                        return TryResolveWorldTransformRecursive(data, lookup, instantiatedPrefabs, visited, out position, out rotation);
                }

                private bool TryResolveWorldTransformRecursive(
                        SaveablePrefabData data,
                        IDictionary<string, SaveablePrefabData> lookup,
                        Dictionary<string, GameObject> instantiatedPrefabs,
                        HashSet<string> visited,
                        out Vector3 position,
                        out Quaternion rotation)
                {
                        position = data.Position;
                        rotation = data.Rotation;

                        if (string.IsNullOrEmpty(data.ParentID))
                                return true;

                        if (!visited.Add(data.InstanceID))
                                return false;

                        try
                        {
                                if (data.IsParentSceneObject)
                                {
                                        var parentGO = SaveManager.Instance?.FindGameObjectByUniqueID(data.ParentID, SaveManager.IdentifierType.UniqueID);
                                        if (parentGO != null)
                                        {
                                                position = parentGO.transform.TransformPoint(data.Position);
                                                rotation = parentGO.transform.rotation * data.Rotation;
                                                return true;
                                        }

                                        position = data.Position;
                                        rotation = data.Rotation;
                                        return true;
                                }

                                if (instantiatedPrefabs != null && instantiatedPrefabs.TryGetValue(data.ParentID, out var liveParent) && liveParent != null)
                                {
                                        position = liveParent.transform.TransformPoint(data.Position);
                                        rotation = liveParent.transform.rotation * data.Rotation;
                                        return true;
                                }

                                if (lookup != null && lookup.TryGetValue(data.ParentID, out var parentData) && parentData != null)
                                {
                                        if (TryResolveWorldTransformRecursive(parentData, lookup, instantiatedPrefabs, visited, out var parentPos, out var parentRot))
                                        {
                                                position = parentPos + parentRot * data.Position;
                                                rotation = parentRot * data.Rotation;
                                                return true;
                                        }
                                }

                                position = data.Position;
                                rotation = data.Rotation;
                                return true;
                        }
                        finally
                        {
                                visited.Remove(data.InstanceID);
                        }
                }

                private void IncludeParentCluster(SaveablePrefabData data, IDictionary<string, SaveablePrefabData> lookup, HashSet<string> accumulator)
                {
                        if (data == null || string.IsNullOrEmpty(data.ParentID) || data.IsParentSceneObject)
                                return;

                        if (lookup != null && lookup.TryGetValue(data.ParentID, out var parentData) && parentData != null)
                        {
                                if (accumulator.Add(parentData.InstanceID))
                                        IncludeParentCluster(parentData, lookup, accumulator);
                        }
                }

                private void ScheduleNextEvaluation(bool immediate = false)
                {
                        float interval = Mathf.Max(0f, refreshInterval);
                        nextEvaluationTime = immediate ? Time.time : Time.time + interval;
                }

#if UNITY_EDITOR
                private void OnDrawGizmos()
                {
                        if (!showDebugRadius && !showDeferredPrefabPositions)
                                return;

                        if (playerTransform == null)
                                return;

                        Vector3 playerPosition = playerTransform.position;

                        // Draw activation radius sphere
                        if (showDebugRadius)
                        {
                                Gizmos.color = debugRadiusColor;
                                Gizmos.DrawWireSphere(playerPosition, activationRadius);
                                
                                // Draw a semi-transparent filled sphere
                                Color fillColor = debugRadiusColor;
                                fillColor.a *= 0.1f;
                                Gizmos.color = fillColor;
                                Gizmos.DrawSphere(playerPosition, activationRadius);
                        }

                        // Draw deferred prefab positions
                        if (showDeferredPrefabPositions && Application.isPlaying)
                        {
                                var activeScene = SceneManager.GetActiveScene();
                                string sceneName = activeScene.IsValid() ? activeScene.name : string.Empty;
                                
                                var lookup = BuildPrefabSceneLookup(sceneName);
                                
                                if (lookup.Count == 0)
                                        return;

                                var instantiatedPrefabs = activePrefabManager?.GetInstantiatedPrefabs();
                                if (instantiatedPrefabs == null)
                                        instantiatedPrefabs = new Dictionary<string, GameObject>();

                                float activationRadiusSqr = activationRadius * activationRadius;

                                foreach (var data in lookup.Values)
                                {
                                        if (data == null || string.IsNullOrEmpty(data.InstanceID))
                                                continue;

                                        // Skip if already instantiated
                                        if (instantiatedPrefabs.ContainsKey(data.InstanceID))
                                                continue;

                                        if (!TryGetWorldTransform(data, lookup, instantiatedPrefabs, out var position, out _))
                                                continue;

                                        bool isWithinRadius = (position - playerPosition).sqrMagnitude <= activationRadiusSqr;
                                        Gizmos.color = isWithinRadius ? activePrefabColor : deferredPrefabColor;
                                        Gizmos.DrawSphere(position, prefabMarkerSize);
                                        
                                        // Draw a line from the prefab to the player if within radius
                                        if (isWithinRadius)
                                        {
                                                Gizmos.color = new Color(activePrefabColor.r, activePrefabColor.g, activePrefabColor.b, 0.3f);
                                                Gizmos.DrawLine(position, playerPosition);
                                        }

                                        // Draw prefab name in editor
                                        if (isWithinRadius && !string.IsNullOrEmpty(data.GameObjectName))
                                        {
                                                UnityEditor.Handles.Label(position + Vector3.up * (prefabMarkerSize + 0.5f), 
                                                        $"{data.GameObjectName}\n(Deferred - {(position - playerPosition).magnitude:F1}m)");
                                        }
                                }

                                // Draw summary text
                                int totalDeferred = 0;
                                int withinRadius = 0;
                                foreach (var data in lookup.Values)
                                {
                                        if (data == null || string.IsNullOrEmpty(data.InstanceID))
                                                continue;
                                        if (instantiatedPrefabs.ContainsKey(data.InstanceID))
                                                continue;
                                        if (TryGetWorldTransform(data, lookup, instantiatedPrefabs, out var position, out _))
                                        {
                                                totalDeferred++;
                                                if ((position - playerPosition).sqrMagnitude <= activationRadiusSqr)
                                                        withinRadius++;
                                        }
                                }

                                string info = $"Deferred Prefabs: {totalDeferred}\nWithin Radius: {withinRadius}\nActivation Radius: {activationRadius:F1}m";
                                UnityEditor.Handles.Label(playerPosition + Vector3.up * 3f, info, 
                                        new GUIStyle(UnityEditor.EditorStyles.boldLabel) { normal = { textColor = Color.white } });
                        }
                }
#endif
        }
}
#endif
