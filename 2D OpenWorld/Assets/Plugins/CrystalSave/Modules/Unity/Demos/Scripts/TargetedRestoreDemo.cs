// ©2025 Arawn – Crystal Save Demo
//
// HOW TO USE
// 1.  In an empty scene create two Cubes, add Rigidbody & either
//     ▸ SaveablePrefab (component) or ▸ RememberGameObject + UniqueID
//     – Set the Rigidbody’s *Is Kinematic* = true so they start “floating”.
// 2.  Create five UI Buttons (Canvas ▸ Button (TextMeshPro)) and label them:
//
//       • Turn On Gravity
//       • Save
//       • Load
//       • Restore Only Cube 1
//       • Restore Only Cube 2
//
// 3.  Add this script to an empty GameObject called **TargetedRestoreDemo** and
//     drag the cubes / buttons into the matching inspector fields.
// 4.  Press Play → click **Turn On Gravity** → cubes fall, **Save** to slot 1.
// 5.  Re-arrange cubes, click **Load** to reload the full save, or hit one of
//     the *Restore Only* buttons to bring back just that cube’s state.
#if MEMORYPACK && ARAWN_REMEMBERME
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using UnityEngine;
using UnityEngine.UI;

// ← SaveManager lives here

namespace Arawn.CrystalSave.Demo
{
	public class TargetedRestoreDemo : MonoBehaviour
	{
		[Header("Rigidbodies (scene cubes)")]
		[SerializeField] private Rigidbody cube1;
		[SerializeField] private Rigidbody cube2;

		[Header("UI Buttons")]
		[SerializeField] private Button turnOnGravityBtn;
		[SerializeField] private Button snapshotBtn;
		[SerializeField] private Button restoreCube1Btn;
		[SerializeField] private Button restoreCube2Btn;

		private const int Slot = 1;           // hard-coded demo slot

		private void Awake()
		{
			// Button wiring
			turnOnGravityBtn.onClick.AddListener(TurnOnGravity);
			snapshotBtn.onClick.AddListener(TakeSnapshot);
			restoreCube1Btn.onClick.AddListener(() => RestoreSingle(cube1));
			restoreCube2Btn.onClick.AddListener(() => RestoreSingle(cube2));
		}

                private async void Start()
                {
                        await WaitForSaveManagerReadyAsync();
                        await Task.Yield();
                        SaveManager.Instance?.SnapshotCurrentData();
                }

		private static async Task WaitForSaveManagerReadyAsync()
		{
			// Fast-path if already initialized
			if (SaveManager.IsInitialized && SaveManager.Instance != null) return;

			var tcs = new TaskCompletionSource<bool>();
			void Handler(SaveManager mgr)
			{
				SaveManager.Initialized -= Handler;
				if (!tcs.Task.IsCompleted) tcs.SetResult(true);
			}
			SaveManager.Initialized += Handler;

			// Re-check after subscribing to avoid missing a race where it initialized between the first check and subscription
			if (SaveManager.IsInitialized && SaveManager.Instance != null)
			{
				SaveManager.Initialized -= Handler;
				return;
			}

			await tcs.Task;
		}

		private void TurnOnGravity()
		{
			cube1.isKinematic = false;
			cube2.isKinematic = false;
		}

		private void TakeSnapshot()
		{
			SaveManager.Instance?.SnapshotCurrentData();
		}

		private async void RestoreSingle(Rigidbody body)
		{
			// Uses the async-void helper inside SaveManager; safe to call from UI. :contentReference[oaicite:2]{index=2}:contentReference[oaicite:3]{index=3}
			// await SaveManager.Instance.RestoreSingleGameObjectWithRetryAsync(body.gameObject, Slot);
			await SaveManager.Instance.RestoreSingleGameObjectFromCurrentDataAsync(body.gameObject);
		}
	}
}

#endif