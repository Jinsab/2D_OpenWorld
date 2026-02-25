#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    [AddComponentMenu("Crystal Save/Remember Components/Remember Parent")]
    [DisallowMultipleComponent]
    [RememberCustomIcon("Assets/Plugins/CrystalSave/Editor/Gizmos/Parented.png")]
    public class RememberParent : SaveableComponent, IParentAppliable
    {
    [Header("Performance")]
    [Tooltip("Cache references on the current parent to avoid repeated GetComponent calls. Cache is invalidated when the parent changes.")]
    [SerializeField] private bool enablePerformanceCaching = true;

        [Header("Save Optimization")]
        [SerializeField] private bool skipSavingWhenUnchanged;

        private bool shouldApplyParent = false;
        private ParentData pendingParentData;
        private int retryCount = 0;
        private const int maxRetries = 3;
        
        [SerializeField] [HideInInspector]
        private bool hasDeserialized = false;
        
        // Track if this GameObject has ever been activated (to distinguish design-time inactive vs runtime inactive)
        private bool hasBeenActivated = false;
        
        // Static dictionary to store target parent IDs that survive component destruction
        private static readonly Dictionary<string, string> pendingParentIDs = new Dictionary<string, string>();

    // Cached references for the current parent
    private Transform cachedParentTransform;
    private UniqueID cachedParentUniqueID;
    private SaveablePrefab cachedParentPrefab;

        private ParentSnapshot cachedSnapshot;
        private bool hasCachedSnapshot;
        private byte[] cachedSerializedData;
    private bool suppressParentChangeCallbacks = false;

        private struct ParentSnapshot
        {
            public string ParentUniqueID;
            public string FallbackSaveablePrefabID;
            public bool HasNoParent;

            public ParentSnapshot(string parentUniqueID, string fallbackSaveablePrefabID, bool hasNoParent)
            {
                ParentUniqueID = parentUniqueID;
                FallbackSaveablePrefabID = fallbackSaveablePrefabID;
                HasNoParent = hasNoParent;
            }

            public ParentSnapshot Clone()
            {
                return new ParentSnapshot(ParentUniqueID, FallbackSaveablePrefabID, HasNoParent);
            }
        }

        protected override void Awake()
        {
            base.Awake();

            // Only capture initial state if this is the very first Awake call
            // (When skipSavingWhenUnchanged is enabled, we capture the initial design-time state)
            if (skipSavingWhenUnchanged && !hasCachedSnapshot && !hasDeserialized)
            {
                if (TryCaptureCurrentState(out ParentSnapshot snapshot))
                {
                    RefreshCachedSnapshot(snapshot);
                }
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            
            // Mark that this GameObject has been activated at least once
            hasBeenActivated = true;
            
            // Don't re-apply parent in OnEnable if we have pending data queued for LateUpdate
            // This prevents interference with the deferred application strategy
            if (shouldApplyParent && pendingParentData != null)
            {
                Logger.Log($"[RememberParent] OnEnable - '{gameObject.name}' has pending parent data, will apply in LateUpdate.", LogCategory.RememberParent, LogLevel.Info);
                return;
            }
            
            Transform currentParent = transform.parent;
            string currentParentName = currentParent != null ? currentParent.name : "null";
            
            // Get my unique ID to look up pending parent
            string myUniqueID = GetComponent<UniqueID>()?.ID;
            string targetParentID = null;
            if (!string.IsNullOrEmpty(myUniqueID) && pendingParentIDs.TryGetValue(myUniqueID, out targetParentID))
            {
                // Found a pending parent ID
            }
            
            Logger.Log($"[RememberParent] OnEnable - '{gameObject.name}', Current Parent: '{currentParentName}', Target Parent ID: '{targetParentID}', hasDeserialized: {hasDeserialized}", LogCategory.RememberParent, LogLevel.Info);
            
            // Only re-apply parent if we have a target and current parent doesn't match
            if (!string.IsNullOrEmpty(targetParentID))
            {
                // Check if current parent matches target
                bool parentMatches = false;
                if (currentParent != null)
                {
                    var currentParentUniqueID = currentParent.GetComponent<UniqueID>();
                    if (currentParentUniqueID != null && currentParentUniqueID.ID == targetParentID)
                    {
                        parentMatches = true;
                    }
                }
                
                // Only set parent if it doesn't match
                if (!parentMatches)
                {
                    GameObject parentObject = SaveManager.Instance.FindGameObjectByUniqueID(targetParentID, SaveManager.IdentifierType.UniqueID);
                    if (parentObject != null)
                    {
                        Logger.Log($"[RememberParent] OnEnable - Re-applying parent for '{gameObject.name}' to '{parentObject.name}'. Current parent before: '{currentParentName}'", LogCategory.RememberParent, LogLevel.Info);
                        SafeSetParent(parentObject.transform, true);
                        Logger.Log($"[RememberParent] OnEnable - Parent set successfully. Current parent after: '{(transform.parent != null ? transform.parent.name : "null")}'", LogCategory.RememberParent, LogLevel.Info);
                    }
                    else
                    {
                        Logger.Log($"[RememberParent] OnEnable - Parent object with ID '{targetParentID}' not found for '{gameObject.name}'!", LogCategory.RememberParent, LogLevel.Warning);
                    }
                }
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
        }

        void LateUpdate()
        {
            // Don't apply pending parent data if we've already deserialized
            // This prevents snapshots from overwriting properly deserialized parent relationships
            if (shouldApplyParent && pendingParentData != null)
            {
                Logger.Log($"[RememberParent] LateUpdate - Applying queued parent data for '{gameObject.name}'", LogCategory.RememberParent, LogLevel.Info);
                ApplyParentData(pendingParentData);
                shouldApplyParent = false;
                pendingParentData = null;
            }
        }

        /// <summary>
        /// Unity callback – fired every time <c>transform.parent</c> changes at runtime.
        /// Used here purely for debugging / verification.
        /// </summary>
        private void OnTransformParentChanged()
        {
            Transform currentParent = transform.parent;
            string parentName = currentParent != null ? currentParent.name : "null";
            Logger.Log($"[RememberParent] OnTransformParentChanged - '{gameObject.name}' new parent: '{parentName}'", LogCategory.RememberParent, LogLevel.Info);

            // Check if parent matches our pending target
            string myUniqueID = GetComponent<UniqueID>()?.ID;
            string expectedParentID = null;
            bool hasPendingParent = !string.IsNullOrEmpty(myUniqueID) && pendingParentIDs.TryGetValue(myUniqueID, out expectedParentID) && !string.IsNullOrEmpty(expectedParentID);

            if (transform.parent != null && hasPendingParent)
            {
                var parentUniqueID = transform.parent.GetComponent<UniqueID>();
                if (parentUniqueID != null && parentUniqueID.ID == expectedParentID)
                {
                    // Keep the targetParentID in the dictionary - we need it to survive disable/enable cycles
                }
            }

            if (!suppressParentChangeCallbacks && hasPendingParent && currentParent == null)
            {
                var manager = SaveManager.Instance;
                bool activeStateApplication = false;
                if (RememberGameObject.TryGetCachedRemember(gameObject, out var cachedRemember) && cachedRemember != null)
                {
                    activeStateApplication = cachedRemember.IsApplyingActiveState;
                }
                else if (gameObject.TryGetComponent(out RememberGameObject rememberComponent) && rememberComponent != null)
                {
                    activeStateApplication = rememberComponent.IsApplyingActiveState;
                }

                bool systemApplyingState = (manager != null && (manager.IsLoading || manager.IsInSceneTransition)) || activeStateApplication;

                if (systemApplyingState)
                {
                    GameObject parentObject = null;
                    if (manager != null)
                    {
                        parentObject = manager.FindGameObjectByUniqueID(expectedParentID, SaveManager.IdentifierType.UniqueID);
                    }
                    else
                    {
                        parentObject = SaveManager.Instance?.FindGameObjectByUniqueID(expectedParentID, SaveManager.IdentifierType.UniqueID);
                    }
                    if (parentObject != null)
                    {
                        Logger.Log($"[RememberParent] OnTransformParentChanged - Lost parent during load for '{gameObject.name}'. Re-applying '{parentObject.name}'.", LogCategory.RememberParent, LogLevel.Info);
                        SafeSetParent(parentObject.transform, true);
                    }
                    else
                    {
                        Logger.Log($"[RememberParent] OnTransformParentChanged - Expected parent with ID '{expectedParentID}' not found while recovering '{gameObject.name}'. Queueing for later.", LogCategory.RememberParent, LogLevel.Warning);
                        manager?.ComponentManager?.QueueParenting(transform, expectedParentID);
                    }
                }
            }
            
            if (skipSavingWhenUnchanged && hasCachedSnapshot)
            {
                // Invalidate cached snapshot because parent relationship has changed
                ClearCachedSnapshot();
            }
        }


        protected override byte[] SerializeComponentData()
        {
            // Skip serialization only for GameObjects that are inactive AND have never been activated
            // This prevents capturing unreliable parent data for design-time inactive objects
            // but still allows saving parent relationships for objects that were activated and then disabled
            if (!gameObject.activeSelf && !hasBeenActivated)
            {
                return null;
            }

            if (!TryCaptureCurrentState(out var snapshot))
            {
                ClearCachedSnapshot();
                return null;
            }

            // During snapshot capture, we still compare against cached state to avoid
            // unnecessary serialization, but we DON'T return null for unchanged data.
            // Instead, we serialize the cached state to ensure snapshot completeness.
            bool isSnapshot = IsCapturingSnapshot;
            
            if (skipSavingWhenUnchanged && hasCachedSnapshot && AreEquivalent(snapshot, cachedSnapshot))
            {
                if (isSnapshot)
                {
                    // Data unchanged: for snapshot, serialize the existing cached state
                    ParentData cachedParentData = new ParentData
                    {
                        ParentUniqueID = cachedSnapshot.ParentUniqueID,
                        FallbackSaveablePrefabID = cachedSnapshot.FallbackSaveablePrefabID
                    };
                    return Serializer.Serialize(cachedParentData);
                }
                else
                {
                    // Data unchanged: for regular save, return cached serialized data
                    if (cachedSerializedData != null && cachedSerializedData.Length > 0)
                    {
                        return cachedSerializedData;
                    }
                }
            }

            ParentData parentData = new ParentData
            {
                ParentUniqueID = snapshot.ParentUniqueID,
                FallbackSaveablePrefabID = snapshot.FallbackSaveablePrefabID
            };
            
            Logger.Log($"PARENT_SERIALIZE>>> '{gameObject.name}' saving parent: ParentUniqueID='{snapshot.ParentUniqueID}', FallbackSaveablePrefabID='{snapshot.FallbackSaveablePrefabID}'", LogCategory.RememberParent, LogLevel.Info);

            byte[] serialized = Serializer.Serialize(parentData);

            RefreshCachedSnapshot(snapshot);
            
            if (skipSavingWhenUnchanged)
            {
                cachedSerializedData = serialized;
            }

            return serialized;
        }

        protected override void DeserializeComponentData(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return;
            }

            try
            {
                ParentData deserializedData = Serializer.Deserialize<ParentData>(data);

                if (deserializedData == null)
                {
                    return;
                }

                // Mark that we've deserialized, so LateUpdate won't apply snapshot data
                hasDeserialized = true;

                string targetID = deserializedData.ParentUniqueID ?? deserializedData.FallbackSaveablePrefabID;
                
                // Store the target parent ID in static dictionary to survive component destruction
                string myUniqueID = GetComponent<UniqueID>()?.ID;
                if (!string.IsNullOrEmpty(myUniqueID) && !string.IsNullOrEmpty(targetID))
                {
                    pendingParentIDs[myUniqueID] = targetID;
                }
                
                // Queue the data for LateUpdate application (like RememberTransform does)
                // This ensures parenting happens AFTER active-state enforcement completes
                pendingParentData = deserializedData;
                shouldApplyParent = true;
                
                Logger.Log($"PARENT_DESERIALIZE>>> '{gameObject.name}' queued parent data for LateUpdate. Target ID: '{targetID}' (UniqueID: '{deserializedData.ParentUniqueID}', SaveablePrefabID: '{deserializedData.FallbackSaveablePrefabID}')", LogCategory.RememberParent, LogLevel.Info);
                
                RefreshCachedSnapshot(CreateSnapshotFromData(deserializedData));
            }
            catch (Exception ex)
            {
                Logger.Log($"PARENT_DESERIALIZE>>> '{gameObject.name}' encountered error: {ex.Message}", LogCategory.RememberParent, LogLevel.Error);
            }
        }

        public void ApplyParentData(ParentData data)
        {
            if (data == null)
            {
                Logger.Log($"PARENT_APPLY>>> '{gameObject.name}' received null ParentData - skipping", LogCategory.RememberParent, LogLevel.Info);
                return;
            }

            try
            {
                string parentID = data.ParentUniqueID ?? data.FallbackSaveablePrefabID;

                if (!string.IsNullOrEmpty(parentID))
                {
                    Logger.Log($"PARENT_APPLY>>> '{gameObject.name}' attempting to find parent with ID '{parentID}'", LogCategory.RememberParent, LogLevel.Info);
                    
                    GameObject parentObject = SaveManager.Instance.FindGameObjectByUniqueID(parentID, SaveManager.IdentifierType.UniqueID);
                    
                    if (parentObject != null)
                    {
                        bool childIsActive = gameObject.activeInHierarchy;
                        bool parentActive = parentObject.activeInHierarchy;
                        Logger.Log($"PARENT_APPLY>>> '{gameObject.name}' (active: {childIsActive}) found parent '{parentObject.name}' (active: {parentActive}). Assigning parent...", LogCategory.RememberParent, LogLevel.Info);
                        SafeSetParent(parentObject.transform, true);
                        Logger.Log($"PARENT_APPLY>>> '{gameObject.name}' parent assigned successfully. Current parent: '{(transform.parent != null ? transform.parent.name : "null")}'", LogCategory.RememberParent, LogLevel.Info);
                        retryCount = 0;

                        RefreshCachedSnapshot(CreateSnapshotFromData(data));
                    }
                    else
                    {
                        Logger.Log($"PARENT_APPLY>>> '{gameObject.name}' FAILED to find parent with ID '{parentID}'. Retry attempt {retryCount + 1}/{maxRetries}", LogCategory.RememberParent, LogLevel.Info);
                        if (++retryCount < maxRetries)
                        {
                            shouldApplyParent = true;
                            pendingParentData = data;
                        }
                        else
                        {
                            Logger.Log($"[RememberParent] ApplyParentData - Failed to apply parent after {maxRetries} retries for '{gameObject.name}'.", LogCategory.RememberParent, LogLevel.Error);
                        }
                    }
                }
                else
                {
                    SafeSetParent(null, true);

                    RefreshCachedSnapshot(CreateSnapshotFromData(data));
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"PARENT_APPLY>>> '{gameObject.name}' encountered error: {ex.Message}", LogCategory.RememberParent, LogLevel.Error);
            }
        }

        private void RefreshCachedSnapshot(ParentSnapshot snapshot)
        {
            if (!skipSavingWhenUnchanged)
            {
                return;
            }

            cachedSnapshot = snapshot.Clone();
            hasCachedSnapshot = true;
        }

        private void ClearCachedSnapshot()
        {
            hasCachedSnapshot = false;
            cachedSnapshot = default;
        }

        private bool TryCaptureCurrentState(out ParentSnapshot snapshot)
        {
            Transform parentTransform = transform.parent;
            if (parentTransform == null)
            {
                snapshot = new ParentSnapshot(null, null, true);
                return true;
            }

            UniqueID uniqueIDComponent = null;
            SaveablePrefab prefab = null;

            if (enablePerformanceCaching)
            {
                if (cachedParentTransform != parentTransform)
                {
                    cachedParentTransform = parentTransform;
                    cachedParentUniqueID = null;
                    cachedParentPrefab = null;
                    cachedParentTransform.TryGetComponent(out cachedParentUniqueID);
                    cachedParentTransform.TryGetComponent(out cachedParentPrefab);
                }

                uniqueIDComponent = cachedParentUniqueID;
                prefab = cachedParentPrefab;
            }
            else
            {
                parentTransform.TryGetComponent(out uniqueIDComponent);
                parentTransform.TryGetComponent(out prefab);
            }

            if (uniqueIDComponent != null && !string.IsNullOrEmpty(uniqueIDComponent.ID))
            {
                snapshot = new ParentSnapshot(uniqueIDComponent.ID, null, false);
                Logger.Log($"PARENT_CAPTURE>>> '{gameObject.name}' captured parent via UniqueID: '{uniqueIDComponent.ID}' from GameObject '{parentTransform.gameObject.name}'", LogCategory.RememberParent, LogLevel.Info);
                return true;
            }

            if (prefab != null && !string.IsNullOrEmpty(prefab.UniqueID))
            {
                snapshot = new ParentSnapshot(null, prefab.UniqueID, false);
                Logger.Log($"PARENT_CAPTURE>>> '{gameObject.name}' captured parent via SaveablePrefab: '{prefab.UniqueID}' from GameObject '{parentTransform.gameObject.name}'", LogCategory.RememberParent, LogLevel.Info);
                return true;
            }

            Logger.Log($"PARENT_CAPTURE>>> '{gameObject.name}' FAILED to capture parent from GameObject '{parentTransform.gameObject.name}' - no UniqueID or SaveablePrefab found", LogCategory.RememberParent, LogLevel.Info);
            snapshot = default;
            return false;
        }

        private static ParentSnapshot CreateSnapshotFromData(ParentData data)
        {
            if (data == null)
            {
                return default;
            }

            bool hasNoParent = string.IsNullOrEmpty(data.ParentUniqueID) && string.IsNullOrEmpty(data.FallbackSaveablePrefabID);
            return new ParentSnapshot(data.ParentUniqueID, data.FallbackSaveablePrefabID, hasNoParent);
        }

        private static bool AreEquivalent(ParentSnapshot a, ParentSnapshot b)
        {
            return a.HasNoParent == b.HasNoParent &&
                   string.Equals(a.ParentUniqueID, b.ParentUniqueID, StringComparison.Ordinal) &&
                   string.Equals(a.FallbackSaveablePrefabID, b.FallbackSaveablePrefabID, StringComparison.Ordinal);
        }

        private void SafeSetParent(Transform newParent, bool worldPositionStays)
        {
            if (transform == null)
                return;

            try
            {
                suppressParentChangeCallbacks = true;
                transform.SetParent(newParent, worldPositionStays);
            }
            finally
            {
                suppressParentChangeCallbacks = false;
            }
        }
    }

    [MemoryPackable]
    public partial class ParentData
    {
        public string ParentUniqueID { get; set; }
        public string FallbackSaveablePrefabID { get; set; }

        public ParentData() { }

        [MemoryPackConstructor]
        public ParentData(string parentUniqueID, string fallbackSaveablePrefabID = null)
        {
            ParentUniqueID = parentUniqueID;
            FallbackSaveablePrefabID = fallbackSaveablePrefabID;
        }
    }

    public interface IParentAppliable
    {
        void ApplyParentData(ParentData data);
    }
}
#endif
