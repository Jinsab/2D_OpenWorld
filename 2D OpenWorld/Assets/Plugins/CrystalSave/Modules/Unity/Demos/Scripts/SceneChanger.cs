#if ARAWN_REMEMBERME && MEMORYPACK
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Demo
{
	public class SceneChanger : MonoBehaviour
	{
		[Header("Scene Change Settings")]
		[Tooltip("Enter the name of the scene to load.")]
		public string sceneName;

		[Tooltip("Assign the UI Button that will trigger the scene change.")]
		public Button changeSceneButton;

		private void Start()
		{
			// Ensure a button is assigned
			if (changeSceneButton != null)
			{
				// Add the OnClick event listener
				changeSceneButton.onClick.AddListener(ChangeScene);
			}
			else
			{
				Debug.LogError("No button assigned to the SceneChanger script.");
			}
		}

		// Note: in-memory holistic helper doesn't require a slot

		private bool isBusy;

		private async void ChangeScene()
		{
			if (!string.IsNullOrEmpty(sceneName))
			{
				if (isBusy) return;
				isBusy = true;
				try
				{
					var mgr = SaveManager.Instance;
					if (mgr == null)
					{
						Debug.LogWarning("SceneChanger: SaveManager instance not found, loading scene without saving.");
						SceneManager.LoadScene(sceneName);
						return;
					}

					if (!SaveManager.AreSaveSlotsReady)
						await mgr.WaitForSaveSlotsAsync();

					// Use the in-memory holistic helper: take snapshot, populate pending prefabs, then load (no slot/disk write)
					await SaveManagerExtensions.LoadSceneAfterSnapshotAndPopulatePendingPrefabsAsync(mgr, sceneName, false, false);
				}
				catch (System.Threading.Tasks.TaskCanceledException)
				{
					Debug.LogWarning("SceneChanger: operation cancelled");
				}
				catch (System.Exception ex)
				{
					Debug.LogError($"SceneChanger: failed to save and switch – {ex.Message}");
				}
				finally
				{
					isBusy = false;
				}
			}
			else
			{
				Debug.LogError("Scene name is empty or null. Please set a valid scene name.");
			}
		}
	}
}

#endif