#if MEMORYPACK && ARAWN_REMEMBERME
using Arawn.CrystalSave.Runtime;
using UnityEngine;

namespace Arawn.CrystalSave.Demo
{
	public class PrefabAssetIDRuntimeTester : MonoBehaviour 
	{
		[Tooltip("Assign the GameObject that has the SaveablePrefab component.")]
		[SerializeField] private GameObject targetPrefab;

		/// <summary>
		/// This method is intended to be called via a UI Button OnClick event.
		/// It triggers the runtime generation and registration of a prefabAssetID.
		/// </summary>
		public void OnButtonClicked()
		{
			if (targetPrefab == null)
			{
				Debug.LogWarning("PrefabAssetIDRuntimeTester: Target prefab is not assigned.");
				return;
			}

			SaveablePrefab saveable = targetPrefab.GetComponent<SaveablePrefab>();
			if (saveable == null)
			{
				Debug.LogWarning("PrefabAssetIDRuntimeTester: The target GameObject does not have a SaveablePrefab component.");
				return;
			}

			saveable.GenerateAndRegisterPrefabAssetIDAtRuntime();
		}
	}

}
#endif