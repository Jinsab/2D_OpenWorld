#if MEMORYPACK && ARAWN_REMEMBERME
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime
{
	/// <summary>
	/// Integration contract for custom scene loading systems.
	/// Implement this interface to integrate your custom loader (Addressables, asset bundles, etc.)
	/// with Crystal Save's prefab population and scene activation pipeline.
	/// </summary>
	/// <remarks>
	/// This interface provides hooks at critical points in the scene loading lifecycle:
	/// - PreLoad: Before scene load starts (prime prefab data here)
	/// - WillActivate: After load completes, before scene becomes active (final preparations)
	/// - Activated: After scene becomes active (spawn prefabs, cleanup)
	/// - Unloaded: After scene unloads (cleanup resources)
	/// 
	/// Example usage:
	/// <code>
	/// public class MyAddressableLoader : ISceneLoadOrchestrator
	/// {
	///     public async Task OnScenePreLoad(string sceneName)
	///     {
	///         // Prime Crystal Save before loading
	///         await SaveManager.Instance.PopulatePendingPrefabsFromSlotAsync(0);
	///     }
	///     
	///     public async Task OnSceneWillActivate(Scene scene)
	///     {
	///         // Final setup before activation
	///         await PrepareSceneAsync(scene);
	///     }
	///     
	///     public void OnSceneActivated(Scene scene)
	///     {
	///         // Scene is now active, prefabs will spawn
	///         HideLoadingScreen();
	///     }
	///     
	///     public void OnSceneUnloaded(Scene scene)
	///     {
	///         // Cleanup resources
	///         UnloadAssets(scene);
	///     }
	/// }
	/// </code>
	/// </remarks>
	public interface ISceneLoadOrchestrator
	{
		/// <summary>
		/// Called before the scene load operation begins.
		/// Use this to prime Crystal Save with prefab data.
		/// </summary>
		/// <param name="sceneName">Name of the scene about to be loaded</param>
		/// <returns>Task that completes when pre-load operations are done</returns>
		/// <remarks>
		/// IMPORTANT: Call SaveManager.PopulatePendingPrefabsFromSlotAsync() or
		/// SaveManager.PopulatePendingPrefabsFromSnapshotAsync() here to ensure
		/// prefabs are queued BEFORE the scene loads.
		/// 
		/// Example:
		/// <code>
		/// public async Task OnScenePreLoad(string sceneName)
		/// {
		///     // Prime prefab data for the target scene
		///     await SaveManager.Instance.PopulatePendingPrefabsFromSlotAsync(currentSlot);
		///     
		///     // Optional: Show loading screen
		///     ShowLoadingScreen(sceneName);
		/// }
		/// </code>
		/// </remarks>
		Task OnScenePreLoad(string sceneName);

		/// <summary>
		/// Called after the scene has finished loading but before it becomes the active scene.
		/// Use this for final preparations before activation.
		/// </summary>
		/// <param name="scene">The loaded scene that will become active</param>
		/// <returns>Task that completes when preparation is done</returns>
		/// <remarks>
		/// This is called after SceneManager.LoadSceneAsync().isDone = true
		/// but before SceneManager.SetActiveScene() is called.
		/// 
		/// Use this for:
		/// - Validating scene state
		/// - Setting up scene-specific systems
		/// - Finalizing resource loading
		/// 
		/// Example:
		/// <code>
		/// public async Task OnSceneWillActivate(Scene scene)
		/// {
		///     // Wait for all scene dependencies to load
		///     await WaitForSceneDependencies(scene);
		///     
		///     // Validate scene is ready
		///     ValidateSceneState(scene);
		/// }
		/// </code>
		/// </remarks>
		Task OnSceneWillActivate(Scene scene);

		/// <summary>
		/// Called immediately after the scene becomes the active scene.
		/// Crystal Save will spawn queued prefabs on the next frame.
		/// </summary>
		/// <param name="scene">The newly activated scene</param>
		/// <remarks>
		/// This is called immediately after SceneManager.SetActiveScene(scene).
		/// Crystal Save will spawn prefabs in the next LateUpdate.
		/// 
		/// Use this for:
		/// - Hiding loading screens
		/// - Activating scene UI
		/// - Starting scene-specific logic
		/// 
		/// Example:
		/// <code>
		/// public void OnSceneActivated(Scene scene)
		/// {
		///     // Hide loading screen
		///     HideLoadingScreen();
		///     
		///     // Enable scene camera
		///     EnableSceneCamera(scene);
		///     
		///     // Log activation
		///     Debug.Log($"Scene '{scene.name}' is now active");
		/// }
		/// </code>
		/// </remarks>
		void OnSceneActivated(Scene scene);

		/// <summary>
		/// Called after a scene has been unloaded.
		/// Use this for cleanup and resource management.
		/// </summary>
		/// <param name="scene">The scene that was unloaded</param>
		/// <remarks>
		/// This is called after SceneManager.UnloadSceneAsync() completes.
		/// 
		/// Use this for:
		/// - Releasing Addressable handles
		/// - Unloading asset bundles
		/// - Cleaning up scene-specific resources
		/// 
		/// Example:
		/// <code>
		/// public void OnSceneUnloaded(Scene scene)
		/// {
		///     // Release Addressables handle
		///     if (sceneHandles.TryGetValue(scene.name, out var handle))
		///     {
		///         Addressables.Release(handle);
		///         sceneHandles.Remove(scene.name);
		///     }
		/// }
		/// </code>
		/// </remarks>
		void OnSceneUnloaded(Scene scene);
	}

	/// <summary>
	/// Base implementation of ISceneLoadOrchestrator with default behavior.
	/// Inherit from this class and override only the methods you need.
	/// </summary>
	/// <remarks>
	/// This provides sensible defaults for all hooks:
	/// - OnScenePreLoad: Does nothing (override to prime Crystal Save)
	/// - OnSceneWillActivate: Does nothing (override for preparations)
	/// - OnSceneActivated: Does nothing (override for UI/activation logic)
	/// - OnSceneUnloaded: Does nothing (override for cleanup)
	/// 
	/// Example:
	/// <code>
	/// public class MyLoader : SceneLoadOrchestratorBase
	/// {
	///     // Only override what you need
	///     public override async Task OnScenePreLoad(string sceneName)
	///     {
	///         await SaveManager.Instance.PopulatePendingPrefabsFromSlotAsync(0);
	///     }
	/// }
	/// </code>
	/// </remarks>
	public abstract class SceneLoadOrchestratorBase : ISceneLoadOrchestrator
	{
		/// <summary>
		/// Default implementation: does nothing.
		/// Override to prime Crystal Save with prefab data.
		/// </summary>
		public virtual Task OnScenePreLoad(string sceneName)
		{
			return Task.CompletedTask;
		}

		/// <summary>
		/// Default implementation: does nothing.
		/// Override for final preparations before activation.
		/// </summary>
		public virtual Task OnSceneWillActivate(Scene scene)
		{
			return Task.CompletedTask;
		}

		/// <summary>
		/// Default implementation: does nothing.
		/// Override for post-activation logic (hide loading screen, etc.)
		/// </summary>
		public virtual void OnSceneActivated(Scene scene)
		{
			// Override in derived class
		}

		/// <summary>
		/// Default implementation: does nothing.
		/// Override for cleanup after scene unload.
		/// </summary>
		public virtual void OnSceneUnloaded(Scene scene)
		{
			// Override in derived class
		}
	}
}
#endif
