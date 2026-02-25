#if MEMORYPACK && ARAWN_REMEMBERME
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ADDRESSABLES_PRESENT
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
#endif

namespace Arawn.CrystalSave.Runtime.Examples
{
	/// <summary>
	/// Example implementation of ISceneLoadOrchestrator for Unity's standard SceneManager.
	/// This shows the recommended pattern for integrating Crystal Save with scene loading.
	/// </summary>
	/// <remarks>
	/// Usage:
	/// <code>
	/// var loader = new StandardSceneLoader();
	/// SaveManager.Instance.RegisterSceneLoadOrchestrator(loader);
	/// await loader.LoadSceneAsync("Island", 0);
	/// </code>
	/// </remarks>
	public class StandardSceneLoader : SceneLoadOrchestratorBase
	{
		private int currentSaveSlot = 0;

		public void SetSaveSlot(int slotNumber)
		{
			currentSaveSlot = slotNumber;
		}

		public override async Task OnScenePreLoad(string sceneName)
		{
			// Prime Crystal Save with prefab data BEFORE the scene loads
			await SaveManager.Instance.PopulatePendingPrefabsFromSlotAsync(currentSaveSlot);
			Debug.Log($"[SCENELOAD][StandardSceneLoader] Primed prefabs from slot {currentSaveSlot} for scene '{sceneName}'");
		}

		public override async Task OnSceneWillActivate(Scene scene)
		{
			// Optional: Final preparations before activation
			Debug.Log($"[SCENELOAD][StandardSceneLoader] Scene '{scene.name}' loaded and ready to activate");
			await Task.CompletedTask;
		}

		public override void OnSceneActivated(Scene scene)
		{
			// Scene is now active, prefabs will spawn on next frame
			Debug.Log($"[SCENELOAD][StandardSceneLoader] Scene '{scene.name}' activated. Prefabs will spawn next frame.");
		}

		public override void OnSceneUnloaded(Scene scene)
		{
			// Cleanup
			Debug.Log($"[SCENELOAD][StandardSceneLoader] Scene '{scene.name}' unloaded");
		}

		/// <summary>
		/// Loads a scene using the standard SceneManager with proper Crystal Save integration.
		/// </summary>
		public async Task LoadSceneAsync(string sceneName, int saveSlot, bool additive = false)
		{
			currentSaveSlot = saveSlot;

			// Trigger OnScenePreLoad
			await OnScenePreLoad(sceneName);

			// Load the scene
			var loadOp = SceneManager.LoadSceneAsync(sceneName, additive ? LoadSceneMode.Additive : LoadSceneMode.Single);
			
			// Wait for load to complete
			while (!loadOp.isDone)
			{
				await Task.Yield();
			}

			// Get the loaded scene
			Scene loadedScene = SceneManager.GetSceneByName(sceneName);

			// Trigger OnSceneWillActivate
			await OnSceneWillActivate(loadedScene);

			// Set as active scene
			if (additive)
			{
				SceneManager.SetActiveScene(loadedScene);
			}

			// Trigger OnSceneActivated
			OnSceneActivated(loadedScene);

			// Wait one frame for prefabs to spawn
			await Task.Yield();
		}
	}

#if ADDRESSABLES_PRESENT
	/// <summary>
	/// Example implementation of ISceneLoadOrchestrator for Unity Addressables.
	/// Demonstrates proper integration with Addressable asset system.
	/// </summary>
	/// <remarks>
	/// Usage:
	/// <code>
	/// var loader = new AddressableSceneLoader();
	/// SaveManager.Instance.RegisterSceneLoadOrchestrator(loader);
	/// await loader.LoadSceneAsync(mySceneReference, 0);
	/// </code>
	/// </remarks>
	public class AddressableSceneLoader : SceneLoadOrchestratorBase
	{
		private int currentSaveSlot = 0;
		private readonly System.Collections.Generic.Dictionary<string, AsyncOperationHandle<SceneInstance>> sceneHandles 
			= new System.Collections.Generic.Dictionary<string, AsyncOperationHandle<SceneInstance>>();

		public void SetSaveSlot(int slotNumber)
		{
			currentSaveSlot = slotNumber;
		}

		public override async Task OnScenePreLoad(string sceneName)
		{
			// Prime Crystal Save with prefab data BEFORE the scene loads
			await SaveManager.Instance.PopulatePendingPrefabsFromSlotAsync(currentSaveSlot);
			Debug.Log($"[SCENELOAD][AddressableSceneLoader] Primed prefabs from slot {currentSaveSlot} for scene '{sceneName}'");
		}

		public override async Task OnSceneWillActivate(Scene scene)
		{
			// Optional: Wait for any additional addressable dependencies
			Debug.Log($"[SCENELOAD][AddressableSceneLoader] Scene '{scene.name}' loaded and ready to activate");
			await Task.CompletedTask;
		}

		public override void OnSceneActivated(Scene scene)
		{
			// Scene is now active, prefabs will spawn on next frame
			Debug.Log($"[SCENELOAD][AddressableSceneLoader] Scene '{scene.name}' activated. Prefabs will spawn next frame.");
		}

		public override void OnSceneUnloaded(Scene scene)
		{
			// Release the Addressables handle
			if (sceneHandles.TryGetValue(scene.name, out var handle))
			{
				Addressables.Release(handle);
				sceneHandles.Remove(scene.name);
				Debug.Log($"[SCENELOAD][AddressableSceneLoader] Released Addressables handle for '{scene.name}'");
			}
		}

		/// <summary>
		/// Loads a scene via Addressables with proper Crystal Save integration.
		/// </summary>
		public async Task LoadSceneAsync(AssetReference sceneRef, int saveSlot, bool additive = false)
		{
			currentSaveSlot = saveSlot;

			// Get scene name (you'll need to load the asset first to get the name)
			string sceneName = sceneRef.editorAsset != null ? sceneRef.editorAsset.name : "Unknown";

			// Trigger OnScenePreLoad
			await OnScenePreLoad(sceneName);

			// Load via Addressables
			var handle = Addressables.LoadSceneAsync(
				sceneRef, 
				additive ? LoadSceneMode.Additive : LoadSceneMode.Single
			);

			// Wait for load to complete
			while (!handle.IsDone)
			{
				await Task.Yield();
			}

			// Get the loaded scene
			Scene loadedScene = handle.Result.Scene;
			sceneHandles[loadedScene.name] = handle;

			// Trigger OnSceneWillActivate
			await OnSceneWillActivate(loadedScene);

			// Set as active scene
			if (additive)
			{
				SceneManager.SetActiveScene(loadedScene);
			}

			// Trigger OnSceneActivated
			OnSceneActivated(loadedScene);

			// Wait one frame for prefabs to spawn
			await Task.Yield();
		}
	}
#endif

	/// <summary>
	/// Example implementation with custom loading screen control.
	/// Demonstrates how to use SceneActivationPipeline hook to delay prefab spawning.
	/// </summary>
	public class LoadingScreenSceneLoader : SceneLoadOrchestratorBase
	{
		private int currentSaveSlot = 0;
		private bool isLoadingScreenVisible = false;
		private GameObject loadingScreenUI;

		public void SetSaveSlot(int slotNumber)
		{
			currentSaveSlot = slotNumber;
		}

		private void ShowLoadingScreen()
		{
			isLoadingScreenVisible = true;
			// Show your loading screen UI
			// loadingScreenUI.SetActive(true);
			Debug.Log("[SCENELOAD][LoadingScreenLoader] Loading screen shown");
		}

		private void HideLoadingScreen()
		{
			isLoadingScreenVisible = false;
			// Hide your loading screen UI
			// loadingScreenUI.SetActive(false);
			Debug.Log("[SCENELOAD][LoadingScreenLoader] Loading screen hidden");
		}

		public override async Task OnScenePreLoad(string sceneName)
		{
			// Show loading screen
			ShowLoadingScreen();

			// Set up hook to prevent prefabs from spawning until loading screen is hidden
			SaveManager.Instance.SceneActivationPipeline = (scene) => !isLoadingScreenVisible;

			// Prime Crystal Save with prefab data
			await SaveManager.Instance.PopulatePendingPrefabsFromSlotAsync(currentSaveSlot);
			Debug.Log($"[SCENELOAD][LoadingScreenLoader] Primed prefabs from slot {currentSaveSlot}");
		}

		public override async Task OnSceneWillActivate(Scene scene)
		{
			// Simulate loading screen showing for a minimum time
			await Task.Delay(1000); // Show for at least 1 second
			Debug.Log($"[SCENELOAD][LoadingScreenLoader] Scene '{scene.name}' ready");
		}

		public override void OnSceneActivated(Scene scene)
		{
			// Hide loading screen - this allows prefabs to spawn
			HideLoadingScreen();
			
			// Clear the hook
			SaveManager.Instance.SceneActivationPipeline = null;
			
			Debug.Log($"[SCENELOAD][LoadingScreenLoader] Scene '{scene.name}' activated");
		}

		public override void OnSceneUnloaded(Scene scene)
		{
			Debug.Log($"[SCENELOAD][LoadingScreenLoader] Scene '{scene.name}' unloaded");
		}

		public async Task LoadSceneAsync(string sceneName, int saveSlot)
		{
			currentSaveSlot = saveSlot;

			// Trigger OnScenePreLoad (shows loading screen, sets up hook)
			await OnScenePreLoad(sceneName);

			// Load the scene
			var loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
			
			// Update progress bar
			while (!loadOp.isDone)
			{
				// Update your progress bar here: loadOp.progress
				await Task.Yield();
			}

			// Get the loaded scene
			Scene loadedScene = SceneManager.GetSceneByName(sceneName);

			// Trigger OnSceneWillActivate (waits minimum time)
			await OnSceneWillActivate(loadedScene);

			// Scene is already active in Single mode, but we trigger the callback
			OnSceneActivated(loadedScene);
			// This hides the loading screen, which allows the hook to return true,
			// which allows prefabs to spawn

			// Wait one more frame for prefabs to spawn
			await Task.Yield();
		}
	}
}
#endif
