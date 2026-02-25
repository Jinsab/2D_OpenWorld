#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	/// <summary>
	/// Proxy component to detect when a GameObject is activated, deactivated, or destroyed.
	/// </summary>
        public class TrackedGameObjectProxy : MonoBehaviour
        {
                private GameObjectTracker tracker;
                private string uniqueID;

		/// <summary>
		/// Initializes the proxy with a reference to the SaveManager and the GameObject's UniqueID.
		/// </summary>
                /// <param name="tracker">Reference to the GameObjectTracker.</param>
                /// <param name="id">UniqueID of the GameObject.</param>
                public void Initialize(GameObjectTracker tracker, string id)
                {
                        this.tracker = tracker;
                        uniqueID = id;
                        Logger.Log($"[TrackedGameObjectProxy] Initialized with UniqueID: {uniqueID}", LogCategory.TrackedGameObjectProxy);
                }

		private void Awake()
		{
			hideFlags |= HideFlags.HideInInspector;
			Logger.Log($"[TrackedGameObjectProxy] Awake called on '{gameObject.name}'.", LogCategory.TrackedGameObjectProxy);
		}

		private void OnEnable()
		{
			Logger.Log($"[TrackedGameObjectProxy] OnEnable called on '{gameObject.name}'.", LogCategory.TrackedGameObjectProxy);
                        if (tracker != null)
                        {
                                tracker.UpdateActiveState(uniqueID, gameObject.activeSelf);
                                Logger.Log($"TrackedGameObjectProxy: GameObject with ID '{uniqueID}' enabled.", LogCategory.TrackedGameObjectProxy, LogLevel.Info);
                        }
		}

		private void OnDisable()
		{
			Logger.Log($"[TrackedGameObjectProxy] OnDisable called on '{gameObject.name}'.", LogCategory.TrackedGameObjectProxy, LogLevel.Off);
                        if (tracker != null)
                        {
                                // Defensive: uniqueID may be empty during certain lifecycle edges
                                try { tracker.UpdateActiveState(uniqueID, gameObject.activeSelf); }
                                catch { /* ignore edge-case tracker issues during shutdown/despawn */ }
                                Logger.Log($"TrackedGameObjectProxy: GameObject with ID '{uniqueID}' disabled.", LogCategory.TrackedGameObjectProxy, LogLevel.Info);

                                // -- Optionally capture final transform data if applicable and safe
                                if (Application.isPlaying)
                                {
                                        var sm = tracker.SaveManager;
                                        if (sm != null && !sm.IsLoading)
                                        {
                                                var rememberGO = gameObject.GetComponent<RememberGameObject>();
                                                var prefab     = SaveablePrefab.TryGetCachedSaveablePrefab(gameObject, out var cachedSp)
                                                                ? cachedSp
                                                                : gameObject.GetComponent<SaveablePrefab>();

                                                bool shouldCapture = false;

                                                if (prefab != null && prefab.PropertySettings != null && prefab.PropertySettings.RememberDestroyed)
                                                {
                                                        // Pool-driven despawns disable prefabs instead of destroying them; capture immediately.
                                                        shouldCapture = true;
                                                }
                                                else if (rememberGO != null && rememberGO.PropertySettings != null && rememberGO.PropertySettings.RememberDestroyed)
                                                {
                                                        // Only capture for plain RememberGameObjects once they've been marked destroyed.
                                                        shouldCapture = tracker.IsGameObjectDestroyed(uniqueID);
                                                }

                                                if (shouldCapture)
                                                {
                                                        tracker.CaptureDestroyedDataIfPossible(uniqueID);
                                                }
                                        }
                                }
                        }
                }

		private void OnDestroy()
		{
			Logger.Log($"[TrackedGameObjectProxy] OnDestroy called on '{gameObject.name}'.", LogCategory.TrackedGameObjectProxy, LogLevel.Off);

                        if (!Application.isPlaying) return;
                        if (tracker == null) return;
                        var sm = tracker.SaveManager;
                        if (sm == null || sm.IsLoading || sm.IsInSceneTransition) return;
                        if (tracker.IsGameObjectDestroyed(uniqueID)) return;

                        var rememberGO = gameObject.GetComponent<RememberGameObject>();
                        var prefab     = SaveablePrefab.TryGetCachedSaveablePrefab(gameObject, out var cachedSp)
                                                                ? cachedSp
                                                                : gameObject.GetComponent<SaveablePrefab>();
                        bool shouldRegister = (rememberGO != null && rememberGO.PropertySettings != null && rememberGO.PropertySettings.RememberDestroyed) ||
                                                                 (prefab != null && prefab.PropertySettings != null && prefab.PropertySettings.RememberDestroyed);

                        if (shouldRegister)
                        {
                                tracker.RegisterDestroyedGameObject(uniqueID);
                                Logger.Log($"[TrackedGameObjectProxy] Registered destruction of '{uniqueID}'.", LogCategory.TrackedGameObjectProxy);
                        }
                        else
                        {
                                if (rememberGO == null && prefab == null)
                                {
                                        Logger.Log($"[TrackedGameObjectProxy] No Remember settings found on '{gameObject.name}'.", LogCategory.TrackedGameObjectProxy, LogLevel.Info);
                                }
                                else
                                {
                                        Logger.Log($"[TrackedGameObjectProxy] SaveDestroyed is disabled for '{uniqueID}'.", LogCategory.TrackedGameObjectProxy, LogLevel.Info);
                                }
                        }
		}
	}
}
#endif