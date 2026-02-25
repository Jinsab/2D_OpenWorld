// SaveablePrefab.Persistence.cs
#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	public partial class SaveablePrefab
	{
		public static event System.Action<SaveablePrefab> OnPrefabInstantiated;
		public static event System.Action<SaveablePrefab> OnPrefabDestroyed;
		public static event System.Action<SaveablePrefab> OnAfterRestore;

                internal static void RaiseAfterRestore(SaveablePrefab p)
                {
                        p?.RefreshOptimizationBaseline();
                        OnAfterRestore?.Invoke(p);
                }

		public void RegisterForSaving()
		{
			if (isRegisteredWithSaveManager) return;

			var mgr = SaveManager.Instance;
			if (mgr == null) return;

                        if (settings == null)
                        {
                                var remember = GetComponent<RememberGameObject>();
                                settings = remember ? remember.PropertySettings : propertySettings;
                                settings ??= new GameObjectPropertySettings { RememberActive = true };
                        }

			mgr.RegisterGameObject(gameObject, settings);
			isRegisteredWithSaveManager = true;
			
			// Capture initial snapshot for RememberHomeScene functionality
			// This ensures design-time prefabs registered at runtime have snapshot data
			// for snapshot-based scene switching (LoadSceneAfterSnapshotAndPopulatePendingPrefabsAsync)
			if (rememberHomeScene && !lastSnapshot.HasValue)
			{
				if (TryCaptureCurrentState(out var snapshot))
				{
					lastSnapshot = snapshot;
					Logger.Log($"[CrystalSave][RegisterForSaving] Captured initial snapshot for '{name}' (RememberHomeScene enabled)", LogCategory.SaveablePrefab, LogLevel.Info);
				}
			}
		}

		public void UnregisterFromSaving()
		{
			if (!isRegisteredWithSaveManager) return;
			var mgr = SaveManager.Instance;
			if (mgr == null) return;

			mgr.UnregisterGameObject(gameObject);
			isRegisteredWithSaveManager = false;
		}

		public void ClearRegisteredFlag() => isRegisteredWithSaveManager = false;
	}
}
#endif