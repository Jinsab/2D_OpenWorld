// SaveablePrefabPool.cs ©2025 Arawn – Crystal Save
#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime
{
	public sealed class SaveablePrefabPool : IDisposable
	{
		/* ─── Global root ───────────────────────────────────────────────────── */
		private static Transform _globalRoot;
		private static Transform GlobalRoot
		{
			get
			{
				if (_globalRoot == null)
				{
					var go = new GameObject("RememberMe_Pools") { hideFlags = HideFlags.DontSave };
					UnityEngine.Object.DontDestroyOnLoad(go);
					_globalRoot = go.transform;
				}
				return _globalRoot;
			}
		}

		/* ─── Fields ────────────────────────────────────────────────────────── */
		private readonly ObjectPool<SaveablePrefab> _pool;
		private readonly HashSet<SaveablePrefab> _active = new();
		private readonly SaveablePrefab _prefab;
		private readonly bool _remember;
		private int _targetSize;
		private Transform _container;

		/* ─── Ctor ──────────────────────────────────────────────────────────── */
		public SaveablePrefabPool(SaveablePrefab prefab,
								  int initialSize,
								  bool rememberObjects,
								  bool collectionCheck = false)
		{
			if (!prefab) throw new ArgumentNullException(nameof(prefab));

			_prefab = prefab;
			_remember = rememberObjects;
			_targetSize = Mathf.Max(0, initialSize);

			/* Re-use bucket if one already exists (prevents duplicates) */
			_container = GlobalRoot.Find($"{_prefab.name}_Pool")
						?? new GameObject($"{_prefab.name}_Pool").transform;
			_container.SetParent(GlobalRoot, false);
			_container.gameObject.SetActive(false);

			_pool = new ObjectPool<SaveablePrefab>(
				CreatePooledItem, OnSpawn, OnDespawn, null,
				collectionCheck, 0, int.MaxValue);          // start empty

			EnsureCapacity();                               // warm-up once

			if (_remember) EnsurePrefabRegistered(_prefab);
		}

		/* ─── Public API ───────────────────────────────────────────────────── */
                public SaveablePrefab Spawn(Vector3 pos, Quaternion rot)
                {
                        var obj = _pool.Get() ?? CreatePooledItem();
                        _active.Add(obj);

                        obj.transform.SetParent(null, true);

                        var settings = SaveManager.Instance?.SaveSettings;
                        if (settings?.spawnPooledPrefabsInScene == true)
                        {
                                SceneManager.MoveGameObjectToScene(obj.gameObject, SceneManager.GetActiveScene());
                        }

                        obj.transform.SetPositionAndRotation(pos, rot);
                        return obj;
                }

                public void Despawn(SaveablePrefab inst)
                {
                        if (inst) _pool.Release(inst);
                        EnsureCapacity();
                }

		public void SetTargetSize(int size)
		{
			_targetSize = Mathf.Max(0, size);
			EnsureCapacity();
		}

		public void Dispose()
		{
			/* 1 ─ destroy any STILL-ACTIVE clones (bullets in mid-air, etc.) */
                        foreach (var sp in _active)
                        {
                                if (!sp) continue;
                                SoftUnregister(sp);           // clears save-tracking first
                                if (SaveManager.Instance != null)
                                        SaveManager.Instance.DestroyWithSnapshot(sp.gameObject, true, allowPooling: false);
                                else
                                        UnityEngine.Object.Destroy(sp.gameObject);
                        }
			_active.Clear();

			/* 2 ─ flush the inactive stack */
			_pool.Clear();

			/* 3 ─ hard-destroy whatever is parked in the bucket right now    */
			if (_container != null)
			{
                                for (int i = _container.childCount - 1; i >= 0; i--)
                                {
                                        var go = _container.GetChild(i).gameObject;
                                        if (SaveManager.Instance != null)
                                                SaveManager.Instance.DestroyWithSnapshot(go, true, allowPooling: false);
                                        else
                                                UnityEngine.Object.Destroy(go);
                                }

				_container.gameObject.SetActive(false);      // keep the bucket itself
			}
		}

		/* ─── Capacity management ─────────────────────────────────────────── */
                internal void EnsureCapacity()
                {
                        var settings = SaveManager.Instance?.SaveSettings;
                        if (settings?.enablePooledPrefabBatching == true && settings.pooledPrefabSpawnBatchSize > 0)
                        {
                                // Use coroutine-based batching for better performance
                                StartEnsureCapacityCoroutine();
                        }
                        else
                        {
                                // Use immediate capacity adjustment (original behavior)
                                EnsureCapacityImmediate();
                        }
                }

                private void StartEnsureCapacityCoroutine()
                {
                        if (_container != null && _container.gameObject.activeInHierarchy)
                        {
                                SaveManager.Instance?.StartCoroutine(EnsureCapacityCoroutine());
                        }
                        else
                        {
                                // Fallback to immediate if no active container
                                EnsureCapacityImmediate();
                        }
                }

                private System.Collections.IEnumerator EnsureCapacityCoroutine()
                {
                        int desiredInactive = Mathf.Max(0, _targetSize - _active.Count);
                        int currentInactive = _pool.CountInactive;
                        var settings = SaveManager.Instance?.SaveSettings;
                        int batchSize = settings?.pooledPrefabSpawnBatchSize ?? 10;

                        if (currentInactive < desiredInactive)
                        {
                                int missing = desiredInactive - currentInactive;
                                int processed = 0;
                                
                                for (int i = 0; i < missing; i++)
                                {
                                        var clone = CreatePooledItem();
                                        clone.gameObject.SetActive(false);
                                        _pool.Release(clone);               // parks it + runs OnDespawn

                                        processed++;
                                        if (processed % batchSize == 0)
                                                yield return null; // Yield to prevent frame drops
                                }
                        }
                        else if (currentInactive > desiredInactive)
                        {
                                int excess = currentInactive - desiredInactive;
                                int processed = 0;
                                
                                for (int i = 0; i < excess; i++)
                                {
                                        var inst = _pool.Get();             // remove from pool
                                        if (!inst) continue;

                                        if (inst.RegisterWithSaveSystem)
                                                SoftUnregister(inst);

                                        // Deactivate first so any SaveableComponents
                                        // immediately unregister from the manager.
                                        if (inst.gameObject.activeSelf)
                                                inst.gameObject.SetActive(false);

                                        // We no longer capture a snapshot or register
                                        // a destroyed ID for pooled clones that exceed
                                        // the desired capacity.
                                        UnityEngine.Object.Destroy(inst.gameObject);

                                        processed++;
                                        if (processed % batchSize == 0)
                                                yield return null; // Yield to prevent frame drops
                                }
                        }
                }

                private void EnsureCapacityImmediate()
                {
                        int desiredInactive = Mathf.Max(0, _targetSize - _active.Count);
                        int currentInactive = _pool.CountInactive;

                        if (currentInactive < desiredInactive)
                        {
                                int missing = desiredInactive - currentInactive;
                                for (int i = 0; i < missing; i++)
                                {
                                        var clone = CreatePooledItem();
                                        clone.gameObject.SetActive(false);
                                        _pool.Release(clone);               // parks it + runs OnDespawn
                                }
                        }
                        else if (currentInactive > desiredInactive)
                        {
                                int excess = currentInactive - desiredInactive;
                                for (int i = 0; i < excess; i++)
                                {
                                        var inst = _pool.Get();             // remove from pool
                                        if (!inst) continue;

                                        if (inst.RegisterWithSaveSystem)
                                                SoftUnregister(inst);

                                        // Deactivate first so any SaveableComponents
                                        // immediately unregister from the manager.
                                        if (inst.gameObject.activeSelf)
                                                inst.gameObject.SetActive(false);

                                        // We no longer capture a snapshot or register
                                        // a destroyed ID for pooled clones that exceed
                                        // the desired capacity.
                                        UnityEngine.Object.Destroy(inst.gameObject);
                                }
                        }
                }

		/* ─── Pool callbacks & helpers ────────────────────────────────────── */
		private Transform EnsureContainer()
		{
			if (_container == null)
			{
				_container = new GameObject($"{_prefab.name}_Pool").transform;
				_container.SetParent(GlobalRoot, false);
				_container.gameObject.SetActive(false);
			}
			return _container;
		}

		private SaveablePrefab CreatePooledItem()
		{
			var clone = UnityEngine.Object.Instantiate(_prefab, EnsureContainer());

			if (SaveManager.Instance)
				SaveManager.Instance.SoftUnregisterGameObject(clone.gameObject);

			clone.RegisterWithSaveSystem = false;
			clone.ClearRegisteredFlag();

			/* NEW ▸ never keep pooled clones across scene loads                        */
			clone.KeepAcrossScenes = false;                          // <───── added
			return clone;
		}

		private void OnSpawn(SaveablePrefab sp)
		{
			if (!sp) return;

			/* NEW ▸ guarantee the runtime instance itself is *not* KAS even if the     *
			 * prefab asset had the flag set in the Inspector                           */
			sp.KeepAcrossScenes = false;                             // <───── added

			if (_remember)
			{
				sp.RegisterWithSaveSystem = true;
				sp.OnBeforeSpawn();                                  // fresh GUID + registration
			}
			else
			{
				SoftUnregister(sp);
				sp.RegisterWithSaveSystem = false;
			}

			sp.gameObject.SetActive(true);
		}

		private void OnDespawn(SaveablePrefab sp)
		{
			if (!sp) return;

			_active.Remove(sp);

			if (sp.RegisterWithSaveSystem)
			{
				SoftUnregister(sp);
				sp.RegisterWithSaveSystem = false;
			}

			sp.gameObject.name = _prefab.name + "(Clone)";
			sp.transform.SetParent(EnsureContainer(), false);
			sp.gameObject.SetActive(false);
		}

		private static void SoftUnregister(SaveablePrefab sp)
		{
			if (SaveManager.Instance && !string.IsNullOrEmpty(sp.UniqueID))
				SaveManager.Instance.SoftUnregisterGameObject(sp.gameObject);
		}

		private static void EnsurePrefabRegistered(SaveablePrefab prefab)
		{
                        var registry = AssetProvider.Load<PrefabRegistry>("PrefabRegistry");
			if (!registry) return;

			if (string.IsNullOrEmpty(prefab.PrefabAssetID))
				prefab.PrefabAssetID = Guid.NewGuid().ToString();

			if (!registry.prefabEntries.Exists(e => e.uniqueID == prefab.PrefabAssetID))
				registry.TryAddPrefab(prefab.PrefabAssetID, prefab.gameObject, out _);
		}
	}
}
#endif