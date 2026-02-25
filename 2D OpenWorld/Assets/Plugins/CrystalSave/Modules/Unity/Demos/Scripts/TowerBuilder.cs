#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Demo
{
	[DefaultExecutionOrder(+20)]               // run after most gameplay scripts
	public class TowerBuilder : MonoBehaviour 
	{
		[Header("Prefab Settings")]
		public GameObject prefabToSpawn;            // asset with SaveablePrefab

        [Header("Tower Settings")]
        public int maxPrefabCount = 100;            // prewarm/upper bound

        [Header("Pooling")]
        public bool rememberSpawnedObjects = true;  // register with Save-System?

		/* ─── private ─────────────────────────────────────────── */
	private readonly List<SaveablePrefab> active = new();
	private SaveablePrefab prefabAssetComponent;
                private Camera cam;

		/* ─── Unity lifecycle ─────────────────────────────────── */
		private void Awake()
		{
			if (!prefabToSpawn)
			{
				Debug.LogError("TowerBuilder: Prefab missing");
				enabled = false;
				return;
			}

			cam = Camera.main ?? throw new System.Exception("TowerBuilder: No Main Camera");

			/* ensure asset carries SaveablePrefab and is registered */
			prefabAssetComponent = prefabToSpawn.GetComponent<SaveablePrefab>() ??
								   prefabToSpawn.AddComponent<SaveablePrefab>();

			if (string.IsNullOrEmpty(prefabAssetComponent.PrefabAssetID))
				prefabAssetComponent.GenerateAndRegisterPrefabAssetIDAtRuntime();
			else
				AssetProvider.Load<PrefabRegistry>("PrefabRegistry")
							  ?.TryAddPrefab(prefabAssetComponent.PrefabAssetID, prefabToSpawn, out _);

                }

		private void Start()
		{
			/* Wait until the Save-System has finished loading (or there is no load) */
			StartCoroutine(DeferredBuild());
		}

	private IEnumerator DeferredBuild()
		{
			/* wait one frame so SaveManager singleton exists */
			yield return null;

			while (SaveManager.Instance && SaveManager.Instance.StateMachine.CurrentState == SaveState.Loading)
				yield return null;                                    // keep waiting

			/* If a slot was loaded CurrentSaveData is not null → skip initial tower */
			bool hasLoadedData = SaveManager.Instance &&
								 SaveManager.Instance.CurrentSaveData != null;

                        if (!hasLoadedData)
                                BuildTower();
                }

		/* ─── public API ──────────────────────────────────────── */
		public void RebuildTower() => BuildTower();

		/* ─── tower builder ───────────────────────────────────── */
		private void BuildTower()
		{
			/* A ▸ despawn previously active cubes */
			foreach (var cube in active)
			{
				if (cube)
				{
					// Avoid destroying/returning to pool while SaveManager is loading
					var sm = SaveManager.Instance;
					if (sm == null || sm.StateMachine?.CurrentState == SaveState.Loading)
						Destroy(cube.gameObject);
					else
						SaveablePrefabFactory.Destroy(cube);
				}
			}
			active.Clear();

			/* B ▸ stack a fresh tower */
			Vector3 basePos = new(0f, 0.5f, 25f);
			const float step = 1.01f;
			const int width = 4;

			bool reachedCap = false;

			while (InsideFrustum(basePos) && !reachedCap)
			{
				for (int x = 0; x < width && !reachedCap; ++x)
					for (int z = 0; z < width && !reachedCap; ++z)
					{
						if (active.Count >= maxPrefabCount) { reachedCap = true; break; }

						Vector3 p = basePos + new Vector3(x * step, 0, z * step);
						var cube = SaveablePrefabFactory.Instantiate(
							prefabToSpawn,
							p,
							Quaternion.identity,
							parent: null,
							registerWithSaveSystem: rememberSpawnedObjects);
						var rb = cube.GetComponent<Rigidbody>();

						if (rb)
						{
							if (rb.isKinematic)   // un-freeze first if prefab was kinematic
							{
								rb.isKinematic = false;

								#if UNITY_6000_0_OR_NEWER
									rb.linearVelocity  = Vector3.zero;
								#else
									rb.velocity        = Vector3.zero;
								#endif

								rb.angularVelocity = Vector3.zero;
							}

							rb.isKinematic = true;  // freeze for tower assembly
						}

						active.Add(cube);
					}
				basePos.y += step;
			}

			/* C ▸ always un-freeze on the next physics step */
			StartCoroutine(UnfreezeAfterFixedUpdate());
		}

		private IEnumerator UnfreezeAfterFixedUpdate()
		{
			yield return new WaitForFixedUpdate();

			foreach (var cube in active)
			{
				Rigidbody rb = cube ? cube.GetComponent<Rigidbody>() : null;
				if (rb)
				{
					rb.isKinematic = false;          // 1️⃣ make dynamic

					#if UNITY_6000_0_OR_NEWER
						rb.linearVelocity  = Vector3.zero; 
					#else
						rb.velocity        = Vector3.zero;
					#endif

					rb.angularVelocity = Vector3.zero;
					rb.WakeUp();
				}
			}
		}

                /* ─── helpers ────────────────────────────────────────── */
		private bool InsideFrustum(Vector3 worldPos)
		{
			Vector3 v = cam.WorldToViewportPoint(worldPos);
			return v.x is >= 0f and <= 1f &&
				   v.y is >= 0f and <= 1f &&
				   v.z is > 0f and <= 100f;
		}
	}
}
#endif