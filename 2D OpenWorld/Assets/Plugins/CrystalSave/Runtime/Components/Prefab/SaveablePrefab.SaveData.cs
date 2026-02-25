#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Linq;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
        public partial class SaveablePrefab
        {
                public SaveablePrefabData TryBuildSaveData()
                {
                        if (!RegisterWithSaveSystem)
                                return null;

                        Logger.Log($"[SaveablePrefab] TryBuildSaveData starting for '{gameObject.name}'", LogCategory.SaveablePrefab, LogLevel.Info);

                        visibilityController ??= GetComponent<PersistentVisibilityController>();

                        if (!TryCaptureCurrentState(out var snapshot))
                        {
                                Logger.Log($"[SaveablePrefab] TryCaptureCurrentState failed for '{gameObject.name}'", LogCategory.SaveablePrefab, LogLevel.Warning);
                                return null;
                        }

                        lastSnapshot = snapshot;

                        bool isRuntimeInstance = isAddedAtRuntime;
                        bool isSnapshotCapture = SaveableComponent.IsCapturingSnapshotStatic;
                        bool optimizationActive = skipSavingWhenUnchanged && !isRuntimeInstance;
                        bool hasBaseline = optimizationActive && baselineSnapshot.HasValue;

                        bool parentChanged = !hasBaseline || !SnapshotsShareParent(baselineSnapshot.Value, snapshot);
                        bool transformChanged = !hasBaseline || parentChanged || !SnapshotsShareTransform(baselineSnapshot.Value, snapshot);
                        bool activeChanged = !hasBaseline || baselineSnapshot.Value.ActiveSelf != snapshot.ActiveSelf;
                        bool nameChanged = !hasBaseline || !string.Equals(baselineSnapshot.Value.Name, snapshot.Name, StringComparison.Ordinal);
                        bool tagChanged = !hasBaseline || !string.Equals(baselineSnapshot.Value.Tag, snapshot.Tag, StringComparison.Ordinal);
                        bool layerChanged = !hasBaseline || baselineSnapshot.Value.Layer != snapshot.Layer;

                        byte[] visibilityData = GetVisibilitySettings();
                        bool hasVisibilityData = visibilityData != null && visibilityData.Length > 0;

                        byte[] runtimeMods = CaptureRuntimeModifications();
                        bool hasRuntimeMods = runtimeMods != null && runtimeMods.Length > 0;

                        bool hasMeaningfulChange = parentChanged || transformChanged || activeChanged || nameChanged || tagChanged || layerChanged;
                        
                        // Always emit data if component blob tracking is enabled to prevent load inconsistencies
                        bool hasComponentBlobTracking = trackComponentBlobs && GetComponentsInChildren<ISaveable>(includeInactive: true).Any(helper => 
                                helper is MonoBehaviour mb && mb.GetComponent<SaveablePrefab>() == null);
                        
                        // CRITICAL FIX: Also check if prefab contains ISaveable components that might have state changes
                        // Even when trackComponentBlobs=false, we need to ensure the prefab gets saved if it contains
                        // components that are registered with the global ComponentManager, as those components
                        // depend on this prefab being instantiated during load to receive their data
                        var childSaveables = GetComponentsInChildren<ISaveable>(includeInactive: true);
                        // Count ISaveable components that are NOT the SaveablePrefab itself (which also implements ISaveable)
                        bool hasISaveableComponents = childSaveables.Any(helper => !ReferenceEquals(helper, this));
                        
                        bool shouldEmitData = !optimizationActive || !hasBaseline || hasMeaningfulChange || hasVisibilityData || hasRuntimeMods || hasComponentBlobTracking || hasISaveableComponents;

                        // During snapshot capture: if data unchanged, serialize baseline to ensure completeness
                        // During regular save: if data unchanged, return null (optimization)
                        if (!shouldEmitData && optimizationActive)
                        {
                                if (isSnapshotCapture && hasBaseline)
                                {
                                        // Data unchanged: for snapshot, use baseline snapshot to build save data
                                        Logger.Log($"SaveablePrefab: Snapshot capture for '{gameObject.name}' using baseline state (unchanged).", LogCategory.SaveablePrefab, LogLevel.Off);
                                        snapshot = baselineSnapshot.Value; // Use baseline for building SaveablePrefabData below
                                }
                                else
                                {
                                        // Data unchanged: for regular save, skip serialization (optimization)
                                        baselineSnapshot = snapshot;
                                        return null;
                                }
                        }

                        if (optimizationActive)
                        {
                                baselineSnapshot = snapshot;
                        }

                        if (rememberHomeScene)
                        {
                                var currentScene = gameObject.scene;
                                if (currentScene.IsValid() && string.IsNullOrEmpty(homeScene))
                                {
                                        SetHomeScene(currentScene.name);
                                }

                                if (homeSceneCaptureMode == HomeSceneMode.LastSnapshotScene &&
                                    currentScene.name != "DontDestroyOnLoad")
                                {
                                        SetHomeScene(currentScene.name);
                                }
                        }

                        string parentId = snapshot.ParentID;
                        bool isParentSceneObject = snapshot.IsParentSceneObject;
                        Vector3 storedPosition = snapshot.HasParent ? snapshot.LocalPosition : snapshot.WorldPosition;
                        Quaternion storedRotation = snapshot.HasParent ? snapshot.LocalRotation : snapshot.WorldRotation;
                        Vector3 storedScale = snapshot.LocalScale;
                        string homeSceneValue = rememberHomeScene ? homeScene : null;

                        if (!isRuntimeInstance && reuseSceneInstanceOnLoad)
                        {
                                var currentScene = gameObject.scene;
                                if (currentScene.IsValid() && string.IsNullOrEmpty(homeSceneValue))
                                {
                                        homeSceneValue = currentScene.name;
                                }
                        }

                        var data = new SaveablePrefabData(
                                uniqueID,
                                prefabAssetID,
                                gameObject.name,
                                storedPosition,
                                storedRotation,
                                storedScale,
                                parentId,
                                isParentSceneObject,
                                hasVisibilityData ? visibilityData : null,
                                homeSceneValue,
                                disablePooling);

                        if (!isRuntimeInstance && reuseSceneInstanceOnLoad && string.IsNullOrEmpty(data.HomeScene))
                        {
                                var scene = gameObject.scene;
                                if (scene.IsValid())
                                        data.HomeScene = scene.name;
                        }

                        data.HasParentOverride = isRuntimeInstance || parentChanged || !hasBaseline;
                        data.HasTransformOverride = isRuntimeInstance || transformChanged || parentChanged || !hasBaseline;
                        data.HasVisibilityData = hasVisibilityData;
                        data.UsesOptimizationFlags = true;

                        if (!data.HasParentOverride)
                        {
                                data.IsParentSceneObject = isParentSceneObject;
                                data.ParentID = parentId;
                        }

                        if (!data.HasTransformOverride && !isRuntimeInstance)
                        {
                                data.Position = storedPosition;
                                data.Rotation = storedRotation;
                                data.Scale = storedScale;
                        }

                        data.LoadPriority = loadPriority;
                        data.DeferLowPriorityUntilRequested = deferLowPriorityUntilRequested;
                        if (reuseSceneInstanceOnLoad && !isRuntimeInstance)
                        {
                                data.ReuseSceneInstanceOnLoad = true;
                        }
                        data.DisablePooling = disablePooling;

                        data.RuntimeModificationData = hasRuntimeMods ? runtimeMods : null;

                        // Save tracking flags for procedural/runtime-modified prefabs (1.6.0)
                        data.TrackAddedComponents = trackAddedComponents;
                        data.TrackComponentBlobs = trackComponentBlobs;
                        data.TrackMaterialOverrides = trackMaterialOverrides;
                        data.TrackChildStateOverrides = trackChildStateOverrides;
                        data.TrackChildTransformOverrides = trackChildTransformOverrides;
                        data.TrackSkinnedMeshOverrides = trackSkinnedMeshOverrides;
                        data.TrackBlendshapeOverrides = trackBlendshapeOverrides;
                        data.TrackTextureOverrides = trackTextureOverrides;
                        data.TrackParticleSnapshots = trackParticleSnapshots;
                        data.TrackColliderSettings = trackColliderSettings;

                        // Always save active state for scene-baked prefabs to ensure correct restoration
                        if (!isRuntimeInstance)
                        {
                                data.ActiveSelfAtSave = gameObject.activeSelf;
                        }

                        var rb = GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                                data.HasRigidbody = true;
#if UNITY_6000_0_OR_NEWER
                                data.RigidbodyVelocity = rb.linearVelocity;
#else
                                data.RigidbodyVelocity = rb.velocity;
#endif
                                data.RigidbodyAngularVelocity = rb.angularVelocity;
                                data.RigidbodyUseGravity = rb.useGravity;
                                data.RigidbodyIsKinematic = rb.isKinematic;
#if UNITY_6000_0_OR_NEWER
                                data.RigidbodyDrag = rb.linearDamping;
                                data.RigidbodyAngularDrag = rb.angularDamping;
#else
                                data.RigidbodyDrag = rb.drag;
                                data.RigidbodyAngularDrag = rb.angularDrag;
#endif
                        }

                        // Parent fingerprinting for robust reattachment to scene-baked parents
                        if (snapshot.HasParent)
                        {
                                var parentSp = transform.parent ? transform.parent.GetComponentInParent<SaveablePrefab>() : null;
                                if (parentSp != null)
                                {
                                        data.ParentPrefabAssetID = parentSp.PrefabAssetID;
                                        // reuse same stable key logic as PrefabManager
                                        data.ParentStableKey = SaveablePrefab.BuildStableHierarchyKey(parentSp);
                                }
                        }

                        var anim = GetComponent<Animator>();
                        if (anim != null)
                        {
                                data.HasAnimator = true;
                                AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
                                data.AnimatorStateHash = info.shortNameHash;
                                data.AnimatorNormalizedTime = info.normalizedTime;
                        }

                        return data;
                }

                private static bool SnapshotsShareParent(PrefabSnapshot baseline, PrefabSnapshot current)
                {
                        return baseline.HasParent == current.HasParent &&
                               string.Equals(baseline.ParentID, current.ParentID, StringComparison.Ordinal) &&
                               baseline.IsParentSceneObject == current.IsParentSceneObject;
                }

                private static bool SnapshotsShareTransform(PrefabSnapshot baseline, PrefabSnapshot current)
                {
                        if (current.HasParent)
                        {
                                return Approximately(baseline.LocalPosition, current.LocalPosition) &&
                                       Approximately(baseline.LocalRotation, current.LocalRotation) &&
                                       Approximately(baseline.LocalScale, current.LocalScale);
                        }

                        return Approximately(baseline.WorldPosition, current.WorldPosition) &&
                               Approximately(baseline.WorldRotation, current.WorldRotation) &&
                               Approximately(baseline.LocalScale, current.LocalScale);
                }
        }
}
#endif
