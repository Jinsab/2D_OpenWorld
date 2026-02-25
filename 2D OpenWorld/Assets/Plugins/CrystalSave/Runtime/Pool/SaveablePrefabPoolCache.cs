// ©2025 Arawn – Crystal Save
#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime
{
        /// <summary>Hands out (and owns) one pool per prefab asset.</summary>
        public static class SaveablePrefabPoolCache
	{
		private static readonly Dictionary<string, SaveablePrefabPool> _pools = new();

		/// <summary>
		/// Flag to suppress pool disposal during snapshot-based scene transitions.
		/// This prevents pooled prefabs from being destroyed when using LoadSceneAfterSnapshotAndPopulatePendingPrefabsAsync.
		/// </summary>
                public static bool SuppressPoolDisposal { get; set; } = false;

		/* Flush EVERYTHING on a real scene-change (keeps memory tight) */
		static SaveablePrefabPoolCache() =>
			SceneManager.activeSceneChanged += (_, __) =>
			{
				// Skip pool disposal during snapshot-based scene transitions
				if (SuppressPoolDisposal)
					return;

				foreach (var pool in _pools.Values) pool.Dispose();
				_pools.Clear();
			};

		private static bool IsLoading =>
			SaveManager.Instance != null &&
			SaveManager.Instance.StateMachine.CurrentState == SaveState.Loading;

                /* ---------------------------------------------------------------- */
                public static SaveablePrefabPool Get(
                        SaveablePrefab prefab,
                        int initialSize,
                        bool rememberObjects)
                {
                        if (!prefab) return null;

                        string id = string.IsNullOrEmpty(prefab.PrefabAssetID)
                                ? prefab.PrefabAssetID = System.Guid.NewGuid().ToString()
                                : prefab.PrefabAssetID;

                        if (!_pools.TryGetValue(id, out var pool) || pool == null)
                        {
                                int warm = IsLoading ? 0 : initialSize;          // avoid double warm-up
                                pool = new SaveablePrefabPool(prefab, warm, rememberObjects);
                                _pools[id] = pool;
                        }

                        pool.SetTargetSize(initialSize);                     // designer’s intent
                        return pool;
                }

                /// <summary>
                /// Convenience overload using default warm-up and remembering behaviour.
                /// </summary>
                public static SaveablePrefabPool Get(SaveablePrefab prefab)
                        => Get(prefab, 0, true);

                public static bool TryDespawn(
                        SaveablePrefab inst,
                        int initialSize,
                        bool rememberObjects)
                {
                        if (!inst) return false;

                        if (!_pools.TryGetValue(inst.PrefabAssetID, out var pool) || pool == null)
                        {
                                int warm = IsLoading ? 0 : initialSize;
                                pool = new SaveablePrefabPool(inst, warm, rememberObjects);
                                _pools[inst.PrefabAssetID] = pool;
                        }

                        pool.SetTargetSize(initialSize);
                        pool.Despawn(inst);
                        return true;
                }

                /// <summary>
                /// Convenience overload using default warm-up and remembering behaviour.
                /// </summary>
                public static bool TryDespawn(SaveablePrefab inst)
                        => TryDespawn(inst, 0, true);
        }
}
#endif