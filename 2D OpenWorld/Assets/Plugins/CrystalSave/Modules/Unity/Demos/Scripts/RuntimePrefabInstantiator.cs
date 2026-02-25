#if MEMORYPACK && ARAWN_REMEMBERME
using Arawn.CrystalSave.Runtime;
using UnityEngine;

namespace Arawn.CrystalSave.Demo
{
	public class RuntimePrefabInstantiator : MonoBehaviour 
	{
		[Tooltip("Reference to the original prefab asset (not a scene instance).")]
		[SerializeField]
		private GameObject prefabAsset;  // This must be the original prefab asset (e.g., from a Resources folder)

		private void Update()
		{
			// When the user presses Space, instantiate and register the prefab.
			if (Input.GetKeyDown(KeyCode.Space))
			{
				if (prefabAsset == null)
				{
					Debug.LogWarning("RuntimePrefabInstantiator: No prefab asset assigned.");
					return;
				}

				// Instantiate the prefab asset.
				GameObject instance = Instantiate(prefabAsset);
				instance.name = prefabAsset.name; // Optional: Reset name

				// Ensure the instance has a SaveablePrefab component.
				SaveablePrefab saveable = instance.GetComponent<SaveablePrefab>();
				if (saveable == null)
				{
					saveable = instance.AddComponent<SaveablePrefab>();
				}

				// Mark this SaveablePrefab as having been added at runtime.
				//saveable.MarkAsAddedAtRuntime();

				// Set the reference to the original prefab asset so that registration uses the asset reference.
				saveable.SetOriginalPrefabAsset(prefabAsset);

				// Automatically generate and register a prefab asset ID.
				saveable.GenerateAndRegisterPrefabAssetIDAtRuntime();

				Debug.Log("RuntimePrefabInstantiator: Instantiated and registered prefab asset.");
			}
		}
	}

}
#endif