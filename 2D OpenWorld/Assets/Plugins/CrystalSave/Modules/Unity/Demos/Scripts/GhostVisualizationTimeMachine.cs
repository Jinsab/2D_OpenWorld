#if MEMORYPACK && ARAWN_REMEMBERME && CRYSTALSAVE_TIMEMACHINE
using UnityEngine;
using Arawn.CrystalSave.Runtime.TimeMachine;
using System.Collections.Generic;

namespace Arawn.CrystalSave.Examples
{
    /// <summary>
    /// Example implementation of GameObjectTimeMachine with automatic ghost visualization.
    /// This demonstrates how to use the extension hooks to create ghost GameObjects
    /// during ghost mode playback without modifying core TimeMachine code.
    /// 
    /// USAGE:
    /// 1. Attach this component instead of the default GameObjectTimeMachine
    /// 2. Configure ghost settings in the Inspector
    /// 3. Enable Ghost Mode and start playback
    /// 4. Ghosts will automatically spawn and track playback
    /// 
    /// FEATURES:
    /// - Automatic ghost creation during ghost mode
    /// - Customizable ghost color and transparency
    /// - Optional trail effects
    /// - Automatic cleanup when playback stops
    /// </summary>
    [AddComponentMenu("Crystal Save/Examples/Ghost Visualization Time Machine")]
    public class GhostVisualizationTimeMachine : GameObjectTimeMachine
    {
        #region Inspector Settings

        [Header("Ghost Visualization Settings")]
        [Tooltip("Color tint for ghost GameObjects")]
        [SerializeField] private Color ghostColor = new Color(0f, 1f, 1f, 0.4f); // Cyan, 40% opacity

        [Tooltip("Enable trail effect on ghosts")]
        [SerializeField] private bool enableTrailEffect = true;

        [Tooltip("Trail duration in seconds")]
        [SerializeField] private float trailTime = 1.0f;

        [Tooltip("Trail material (leave null for default)")]
        [SerializeField] private Material trailMaterial;

        [Tooltip("Automatically destroy ghosts when playback stops")]
        [SerializeField] private bool autoCleanupGhosts = true;

        [Tooltip("Show debug logs for ghost creation/destruction")]
        [SerializeField] private bool debugGhosts = false;

        #endregion

        #region Private Fields

        /// <summary>Cache of active ghost GameObjects (key = original GameObject)</summary>
        private Dictionary<GameObject, GameObject> activeGhosts = new Dictionary<GameObject, GameObject>();

        /// <summary>Cache of ghost trail renderers for cleanup</summary>
        private Dictionary<GameObject, TrailRenderer> ghostTrails = new Dictionary<GameObject, TrailRenderer>();

        /// <summary>Was ghost mode active in the previous frame (for cleanup detection)</summary>
        private bool wasGhostModeActive = false;

        #endregion

        #region Unity Lifecycle

        private void OnDestroy()
        {
            // Always cleanup ghosts on destroy
            CleanupAllGhosts();
        }

        #endregion

        #region Extension Hook Overrides

        /// <summary>
        /// Called every frame during playback for each tracked object.
        /// This is where we create and update ghost visualizations.
        /// </summary>
        protected override void OnUpdatePlaybackVisualization(GameObject trackedObject, GameObjectSnapshot snapshot, float currentTime)
        {
            // Only create ghosts during ghost mode
            if (!IsGhostModeActive())
            {
                // Ghost mode ended - cleanup if auto-cleanup is enabled
                if (wasGhostModeActive && autoCleanupGhosts)
                {
                    CleanupAllGhosts();
                }
                wasGhostModeActive = false;
                return;
            }

            wasGhostModeActive = true;

            // Get or create ghost for this object
            if (!activeGhosts.TryGetValue(trackedObject, out GameObject ghost) || ghost == null)
            {
                ghost = CreateGhostFromObject(trackedObject, ghostColor);
                
                if (ghost != null)
                {
                    activeGhosts[trackedObject] = ghost;
                    
                    if (debugGhosts)
                    {
                        Debug.Log($"[GhostVisualization] Created ghost for '{trackedObject.name}' " +
                                  $"(Branch: {GetPlaybackBranchName()}, Recording: {GetGhostRecordingBranchName()})");
                    }
                }
            }

            // Update ghost position from snapshot
            if (ghost != null)
            {
                ApplySnapshotToGhost(ghost, snapshot);
            }
        }

        /// <summary>
        /// Called after creating a ghost GameObject.
        /// Customize the ghost appearance here (trails, effects, etc.).
        /// </summary>
        protected override void OnCustomizeGhostAppearance(GameObject ghostObject, GameObject originalObject)
        {
            // Add trail effect if enabled
            if (enableTrailEffect && !ghostTrails.ContainsKey(ghostObject))
            {
                TrailRenderer trail = ghostObject.AddComponent<TrailRenderer>();
                trail.time = trailTime;
                trail.startWidth = 0.5f;
                trail.endWidth = 0.05f;
                trail.startColor = new Color(ghostColor.r, ghostColor.g, ghostColor.b, ghostColor.a);
                trail.endColor = new Color(ghostColor.r, ghostColor.g, ghostColor.b, 0f);
                
                if (trailMaterial != null)
                {
                    trail.material = trailMaterial;
                }
                else
                {
                    // Use default trail material
                    trail.material = new Material(Shader.Find("Sprites/Default"));
                    trail.material.color = ghostColor;
                }

                ghostTrails[ghostObject] = trail;

                if (debugGhosts)
                {
                    Debug.Log($"[GhostVisualization] Added trail effect to '{ghostObject.name}'");
                }
            }

            // Disable shadow casting on ghosts
            var renderers = ghostObject.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            // Tag ghost objects for easy identification
            ghostObject.tag = "Ghost";
            
            // Put ghosts on a separate layer (optional - requires "Ghost" layer to be defined)
            int ghostLayer = LayerMask.NameToLayer("Ghost");
            if (ghostLayer >= 0)
            {
                SetLayerRecursively(ghostObject, ghostLayer);
            }
        }

        /// <summary>
        /// Called before applying a snapshot.
        /// Prevent applying snapshots to our ghost objects.
        /// </summary>
        protected override bool OnBeforeApplySnapshot(GameObject target, GameObjectSnapshot snapshot)
        {
            // Don't apply snapshots to ghost objects (they're managed separately)
            if (target.CompareTag("Ghost"))
            {
                return false;
            }

            return base.OnBeforeApplySnapshot(target, snapshot);
        }

        /// <summary>
        /// Called after applying a snapshot.
        /// Can be used to trigger effects or update UI.
        /// </summary>
        protected override void OnAfterApplySnapshot(GameObject target, GameObjectSnapshot snapshot, bool success)
        {
            if (debugGhosts && success && IsGhostModeActive())
            {
                Debug.Log($"[GhostVisualization] Applied snapshot at {snapshot.Timestamp:F2}s to '{target.name}' " +
                          $"(Ghost mode: recording to '{GetGhostRecordingBranchName()}')");
            }

            base.OnAfterApplySnapshot(target, snapshot, success);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Manually cleanup all ghost GameObjects.
        /// Called automatically when playback stops if autoCleanupGhosts is true.
        /// </summary>
        public void CleanupAllGhosts()
        {
            int destroyedCount = 0;

            // Destroy all active ghosts
            foreach (var ghost in activeGhosts.Values)
            {
                if (ghost != null)
                {
                    Destroy(ghost);
                    destroyedCount++;
                }
            }

            activeGhosts.Clear();
            ghostTrails.Clear();

            if (debugGhosts && destroyedCount > 0)
            {
                Debug.Log($"[GhostVisualization] Cleaned up {destroyedCount} ghost GameObject(s)");
            }
        }

        /// <summary>
        /// Change ghost color at runtime.
        /// </summary>
        public void SetGhostColor(Color newColor)
        {
            ghostColor = newColor;

            // Update existing ghosts
            foreach (var ghost in activeGhosts.Values)
            {
                if (ghost != null)
                {
                    ApplyGhostMaterial(ghost, newColor);
                }
            }

            if (debugGhosts)
            {
                Debug.Log($"[GhostVisualization] Changed ghost color to {newColor}");
            }
        }

        /// <summary>
        /// Get the number of currently active ghost GameObjects.
        /// </summary>
        public int GetActiveGhostCount()
        {
            // Count non-null ghosts
            int count = 0;
            foreach (var ghost in activeGhosts.Values)
            {
                if (ghost != null)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Check if a ghost exists for a specific GameObject.
        /// </summary>
        public bool HasGhost(GameObject original)
        {
            return activeGhosts.ContainsKey(original) && activeGhosts[original] != null;
        }

        /// <summary>
        /// Get the ghost GameObject for a specific original GameObject.
        /// </summary>
        public GameObject GetGhost(GameObject original)
        {
            if (activeGhosts.TryGetValue(original, out GameObject ghost))
            {
                return ghost;
            }
            return null;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Recursively set layer on GameObject and all children.
        /// </summary>
        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        #endregion

        #region Debug Visualization

        private void OnGUI()
        {
            if (!debugGhosts || !IsGhostModeActive())
                return;

            // Show ghost mode status
            GUILayout.BeginArea(new Rect(10, Screen.height - 120, 400, 110));
            
            GUI.color = new Color(0f, 1f, 1f, 0.8f);
            GUILayout.Box("=== GHOST MODE ACTIVE ===");
            
            GUI.color = Color.white;
            GUILayout.Label($"Playback Branch: {GetPlaybackBranchName()}");
            GUILayout.Label($"Recording Branch: {GetGhostRecordingBranchName()}");
            GUILayout.Label($"Active Ghosts: {GetActiveGhostCount()}");
            
        
        GUILayout.EndArea();
    }

    #endregion
}
}
#endif