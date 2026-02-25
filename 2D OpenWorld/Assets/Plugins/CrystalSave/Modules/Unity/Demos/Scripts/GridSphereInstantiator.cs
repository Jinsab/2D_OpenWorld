#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;
using System.Collections;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Demo
{
	[DefaultExecutionOrder(+20)]
	public class GridSphereInstantiator : MonoBehaviour 
	{
		// ───── designer inputs ────────────────────────────────────────────
		[Header("Sphere Prefab & Origin")]
		public GameObject spherePrefab;
		public Transform startPosition;
		[Tooltip("Optional parent for the spawned spheres")]
		public Transform parentObject;

		[Header("Grid Settings")]
		[Min(1)] public int gridSizeX = 4;
		[Min(1)] public int gridSizeZ = 4;
		[Min(0f)] public float spacing = 6f;

		[Header("Save-system")]
		public bool registerWithSaveSystem = true;
		[Min(1)] public int batchSize = 32;               // spawn per frame

		// ───── private ────────────────────────────────────────────────────
		private bool gridInitialised;
		private SaveablePrefabPool nonSavingPool;

		/* ───── Unity lifecycle ─────────────────────────────────────────── */
		private void Awake()
		{
			if (!ValidateInputs()) { enabled = false; return; }

			/* ensure asset owns a SaveablePrefab component */
			var spAsset = spherePrefab.GetComponent<SaveablePrefab>() ??
						  spherePrefab.AddComponent<SaveablePrefab>();

			if (!registerWithSaveSystem)
			{
				int count = gridSizeX * gridSizeZ;
				nonSavingPool = SaveablePrefabPoolCache.Get(spAsset, count, false);
			}
		}

		private void Start()
		{
			//StartCoroutine(DeferredInstantiate());
		}

		// ───── public API ─────────────────────────────────────────────────
		public void InstantiateGrid()
		{
			if (gridInitialised || !ValidateInputs()) return;
			StartCoroutine(BuildGridCoroutine());
		}

		// ───── implementation ────────────────────────────────────────────
		private IEnumerator DeferredInstantiate()
		{
			yield return null; // wait one frame for SaveManager

			while (SaveManager.Instance &&
				   SaveManager.Instance.StateMachine.CurrentState == SaveState.Loading)
				yield return null;

			bool loaded = SaveManager.Instance &&
						  SaveManager.Instance.CurrentSaveData != null;

			if (!loaded) InstantiateGrid();
		}

		private IEnumerator BuildGridCoroutine()
		{
			int spawned = 0;

			for (int x = 0; x < gridSizeX; ++x)
			{
				for (int z = 0; z < gridSizeZ; ++z)
				{
					Vector3 pos = startPosition.position +
								  new Vector3(x * spacing, 0f, z * spacing);

					if (registerWithSaveSystem)
					{
						SaveablePrefabFactory.Instantiate(
							spherePrefab, pos, Quaternion.identity,
							parentObject, true);
					}
					else
					{
						var sp = nonSavingPool.Spawn(pos, Quaternion.identity);
						if (parentObject) sp.transform.SetParent(parentObject, true);
						sp.gameObject.SetActive(true);
					}

					if (++spawned >= batchSize)
					{
						spawned = 0;
						yield return null;  // keep editor responsive
					}
				}
			}

			gridInitialised = true;
		}

		// ───── helpers ────────────────────────────────────────────────────
		private bool ValidateInputs()
		{
			if (!spherePrefab || !startPosition)
			{
				Debug.LogError($"{nameof(GridSphereInstantiator)}: assign both Sphere Prefab and Start Position.");
				return false;
			}
			return true;
		}
	}
}
#endif