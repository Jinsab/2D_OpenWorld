#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime
{
	[DefaultExecutionOrder(-10)]
	[DisallowMultipleComponent]
	public class PersistentVisibilityController : MonoBehaviour 
	{
		private SaveableComponent saveableComponent;
		private SaveablePrefab saveablePrefab;

		private VisibilitySettingsData visibilitySettings;

		private readonly Dictionary<Collider, bool> originalColliderStates = new Dictionary<Collider, bool>();
		private readonly Dictionary<Renderer, bool> originalRendererStates = new Dictionary<Renderer, bool>();
		private readonly Dictionary<Canvas, bool> originalCanvasStates = new Dictionary<Canvas, bool>();
		private readonly Dictionary<Rigidbody, bool> originalRigidbodyStates = new Dictionary<Rigidbody, bool>();
		private readonly Dictionary<CharacterController, bool> originalCharacterControllerStates = new Dictionary<CharacterController, bool>();

		private Collider[] colliders;
		private Renderer[] renderers;
		private Canvas[] canvases;
		private Rigidbody[] rigidbodies;
		private CharacterController[] characterControllers;


		private void Awake()
		{
			InitializeReferences();
			InitializeComponentStates();
			StoreOriginalStates();

			SceneManager.sceneLoaded += OnSceneLoaded;
			SceneManager.sceneUnloaded += OnSceneUnloaded;
			SceneManager.activeSceneChanged += OnActiveSceneChanged;

			string currentScene = SceneManager.GetActiveScene().name;
			ApplyVisibilityBasedOnScene(currentScene);
		}

		private void OnDestroy()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
			SceneManager.sceneUnloaded -= OnSceneUnloaded;
			SceneManager.activeSceneChanged -= OnActiveSceneChanged;
		}

		private void OnEnable()
		{
			if (!Application.isPlaying) return;
			// Re-evaluate visibility whenever this object is re-enabled (e.g., after pool revive or after a load)
			var sceneName = SceneManager.GetActiveScene().name;
			ApplyVisibilityBasedOnScene(sceneName);
		}

#if UNITY_EDITOR
		private void OnValidate()
		{
			if (!Application.isPlaying)
				hideFlags |= HideFlags.HideInInspector | HideFlags.NotEditable;
		}
#endif

		private void InitializeReferences()
		{
			saveableComponent = GetComponent<SaveableComponent>();
			saveablePrefab = GetComponent<SaveablePrefab>();

			if (saveableComponent == null && saveablePrefab == null)
			{
				Logger.Log($"PersistentVisibilityController: Neither SaveableComponent nor SaveablePrefab is attached to '{gameObject.name}'. Disabling component.", LogCategory.SaveableComponent, LogLevel.Error);
				enabled = false;
				return;
			}

			Collider[] collidersOnSelf = GetComponents<Collider>();
			colliders = collidersOnSelf.Length > 0 ? collidersOnSelf : GetComponentsInChildren<Collider>();

			MeshRenderer[] meshRenderers = GetComponents<MeshRenderer>();
			SkinnedMeshRenderer[] skinnedMeshRenderers = GetComponents<SkinnedMeshRenderer>();
			renderers = (meshRenderers.Length > 0 || skinnedMeshRenderers.Length > 0)
				? meshRenderers.Cast<Renderer>().Concat(skinnedMeshRenderers).ToArray()
				: GetComponentsInChildren<MeshRenderer>().Cast<Renderer>().Concat(GetComponentsInChildren<SkinnedMeshRenderer>()).ToArray();

			Rigidbody[] rigidbodiesOnSelf = GetComponents<Rigidbody>();
			rigidbodies = rigidbodiesOnSelf.Length > 0 ? rigidbodiesOnSelf : GetComponentsInChildren<Rigidbody>();

			CharacterController[] characterControllersOnSelf = GetComponents<CharacterController>();
			characterControllers = characterControllersOnSelf.Length > 0 ? characterControllersOnSelf : GetComponentsInChildren<CharacterController>();

			canvases = GetComponents<Canvas>();
		}

		private void InitializeComponentStates()
		{
			visibilitySettings = new VisibilitySettingsData
			{
				DisableAllColliders = saveablePrefab?.DisableAllColliders ?? saveableComponent.DisableAllColliders,
				DisableRenderers = saveablePrefab?.DisableRenderers ?? saveableComponent.DisableRenderers,
				DisableCharacterController = saveablePrefab?.DisableCharacterController ?? saveableComponent.DisableCharacterController,
				SetRigidbodyKinematic = saveablePrefab?.SetRigidbodyKinematic ?? saveableComponent.SetRigidbodyKinematic,
				VisibleInScenes = saveablePrefab?.VisibleInScenes ?? saveableComponent.VisibleInScenes
			};
		}

		public byte[] CaptureAndSerializeSettings()
		{
			if (saveablePrefab != null)
			{
				UpdateVisibilitySettingsFromPrefab();
			}
			else if (saveableComponent != null)
			{
				UpdateVisibilitySettingsFromComponent();
			}

			if (saveablePrefab != null && saveablePrefab.VisibleInScenes != null)
			{
				visibilitySettings.VisibleInScenes = new List<string>(saveablePrefab.VisibleInScenes);
			}
			else if (saveableComponent != null && saveableComponent.VisibleInScenes != null)
			{
				visibilitySettings.VisibleInScenes = new List<string>(saveableComponent.VisibleInScenes);
			}

			return SaveDataSerializer.Instance.Serialize(visibilitySettings);
		}

		public void DeserializeAndStoreSettings(byte[] data)
		{
			if (data == null || data.Length == 0)
			{
				Logger.Log($"PersistentVisibilityController: No visibility settings data to deserialize for '{gameObject.name}'.", LogCategory.SaveableComponent, LogLevel.Warning);
				return;
			}

			try
			{
				visibilitySettings = SaveDataSerializer.Instance.Deserialize<VisibilitySettingsData>(data);
				ApplyVisibilitySettingsToSource();

				if (visibilitySettings.VisibleInScenes != null && visibilitySettings.VisibleInScenes.Count > 0)
				{
					Logger.Log($"PersistentVisibilityController: '{gameObject.name}' is set to be visible in the following scenes: {string.Join(", ", visibilitySettings.VisibleInScenes)}.");
				}

				string currentScene = SceneManager.GetActiveScene().name;
				ApplyVisibilityBasedOnScene(currentScene);
				// Also re-apply next frame to win against late systems (pool toggles, etc.).
				StartCoroutine(ReapplyNextFrame(currentScene));

				Logger.Log($"PersistentVisibilityController: Visibility settings applied to '{gameObject.name}'.");
			}
			catch (Exception ex)
			{
				Logger.Log($"PersistentVisibilityController: Failed to deserialize visibility settings for '{gameObject.name}'. Error: {ex.Message}", LogCategory.SaveableComponent, LogLevel.Warning);
			}
		}

		public void ApplyVisibilityBasedOnScene(string currentSceneName)
		{
			bool hasSceneFilter = visibilitySettings.VisibleInScenes != null &&
								  visibilitySettings.VisibleInScenes.Count > 0;
			bool shouldBeVisible = !hasSceneFilter ||
								   visibilitySettings.VisibleInScenes
									   .Any(scene => string.Equals(scene, currentSceneName, StringComparison.OrdinalIgnoreCase));

			if (shouldBeVisible)
			{
				// First, try to restore to the originally captured component states
				RestoreOriginalStates();
				// Then, as a safety net (post-load/pooling), ensure categories that were disabled
				// for off-screen are definitely enabled again when visible.
				EnsureVisibleStates();
				Logger.Log($"PersistentVisibilityController: '{gameObject.name}' is set to be visible in scene '{currentSceneName}'. Components enabled.", LogCategory.SaveableComponent, LogLevel.Off);
			}
			else
			{
				ApplyVisibilityModifications();
				Logger.Log($"PersistentVisibilityController: '{gameObject.name}' is NOT set to be visible in scene '{currentSceneName}'. Components disabled.", LogCategory.SaveableComponent, LogLevel.Info);
			}
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			ApplyVisibilityBasedOnScene(scene.name);
			
			// Only start coroutine if GameObject is active (avoid issues with pooled prefabs)
			if (gameObject.activeInHierarchy)
			{
				StartCoroutine(ReapplyNextFrame(scene.name));
			}
		}

		private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
		{
			ApplyVisibilityBasedOnScene(newScene.name);
			
			// Only start coroutine if GameObject is active (avoid issues with pooled prefabs)
			if (gameObject.activeInHierarchy)
			{
				StartCoroutine(ReapplyNextFrame(newScene.name));
			}
		}

		private System.Collections.IEnumerator ReapplyNextFrame(string sceneName)
		{
			yield return null; // wait one frame
			ApplyVisibilityBasedOnScene(sceneName);
		}

		private void OnSceneUnloaded(Scene scene) { }

		private void ApplyVisibilityModifications()
		{
			foreach (var collider in colliders)
			{
				if (collider != null)
				{
					collider.enabled = !visibilitySettings.DisableAllColliders;
				}
			}

			foreach (var renderer in renderers)
			{
				if (renderer != null)
				{
					renderer.enabled = !visibilitySettings.DisableRenderers;
				}
			}

			foreach (var canvas in canvases)
			{
				if (canvas != null)
				{
					canvas.enabled = !visibilitySettings.DisableRenderers;
				}
			}

			foreach (var characterController in characterControllers)
			{
				if (characterController != null)
				{
					characterController.enabled = !visibilitySettings.DisableCharacterController;
				}
			}

			foreach (var rigidbody in rigidbodies)
			{
				if (rigidbody != null)
				{
					rigidbody.isKinematic = visibilitySettings.SetRigidbodyKinematic;
				}
			}
		}

		// Ensure components are actively enabled when we should be visible (post-load/pool safety net)
		private void EnsureVisibleStates()
		{
			if (visibilitySettings.DisableAllColliders)
			{
				foreach (var collider in colliders)
					if (collider) collider.enabled = true;
			}
			if (visibilitySettings.DisableRenderers)
			{
				foreach (var renderer in renderers)
					if (renderer) renderer.enabled = true;
				foreach (var canvas in canvases)
					if (canvas) canvas.enabled = true;
			}
			if (visibilitySettings.DisableCharacterController)
			{
				foreach (var characterController in characterControllers)
					if (characterController) characterController.enabled = true;
			}
			if (visibilitySettings.SetRigidbodyKinematic)
			{
				foreach (var rigidbody in rigidbodies)
					if (rigidbody) rigidbody.isKinematic = false;
			}
		}

		private void RestoreOriginalStates()
		{
			foreach (var kvp in originalColliderStates)
			{
				if (kvp.Key != null)
				{
					kvp.Key.enabled = kvp.Value;
				}
			}

			foreach (var kvp in originalRendererStates)
			{
				if (kvp.Key != null)
				{
					kvp.Key.enabled = kvp.Value;
				}
			}

			foreach (var kvp in originalCanvasStates)
			{
				if (kvp.Key != null)
				{
					kvp.Key.enabled = kvp.Value;
				}
			}

			foreach (var kvp in originalCharacterControllerStates)
			{
				if (kvp.Key != null)
				{
					kvp.Key.enabled = kvp.Value;
				}
			}

			foreach (var kvp in originalRigidbodyStates)
			{
				if (kvp.Key != null)
				{
					kvp.Key.isKinematic = kvp.Value;
				}
			}
		}

		private void StoreOriginalStates()
		{
			foreach (var collider in colliders)
			{
				if (collider != null && !originalColliderStates.ContainsKey(collider))
				{
					originalColliderStates[collider] = collider.enabled;
				}
			}

			foreach (var renderer in renderers)
			{
				if (renderer != null && !originalRendererStates.ContainsKey(renderer))
				{
					originalRendererStates[renderer] = renderer.enabled;
				}
			}

			foreach (var canvas in canvases)
			{
				if (canvas != null && !originalCanvasStates.ContainsKey(canvas))
				{
					originalCanvasStates[canvas] = canvas.enabled;
				}
			}

			foreach (var characterController in characterControllers)
			{
				if (characterController != null && !originalCharacterControllerStates.ContainsKey(characterController))
				{
					originalCharacterControllerStates[characterController] = characterController.enabled;
				}
			}

			foreach (var rigidbody in rigidbodies)
			{
				if (rigidbody != null && !originalRigidbodyStates.ContainsKey(rigidbody))
				{
					originalRigidbodyStates[rigidbody] = rigidbody.isKinematic;
				}
			}
		}

		private void UpdateVisibilitySettingsFromPrefab()
		{
			visibilitySettings.DisableAllColliders = saveablePrefab.DisableAllColliders;
			visibilitySettings.DisableRenderers = saveablePrefab.DisableRenderers;
			visibilitySettings.DisableCharacterController = saveablePrefab.DisableCharacterController;
			visibilitySettings.SetRigidbodyKinematic = saveablePrefab.SetRigidbodyKinematic;
		}

		private void UpdateVisibilitySettingsFromComponent()
		{
			visibilitySettings.DisableAllColliders = saveableComponent.DisableAllColliders;
			visibilitySettings.DisableRenderers = saveableComponent.DisableRenderers;
			visibilitySettings.DisableCharacterController = saveableComponent.DisableCharacterController;
			visibilitySettings.SetRigidbodyKinematic = saveableComponent.SetRigidbodyKinematic;
		}

		private void ApplyVisibilitySettingsToSource()
		{
			if (saveablePrefab != null)
			{
				saveablePrefab.DisableAllColliders = visibilitySettings.DisableAllColliders;
				saveablePrefab.DisableRenderers = visibilitySettings.DisableRenderers;
				saveablePrefab.DisableCharacterController = visibilitySettings.DisableCharacterController;
				saveablePrefab.SetRigidbodyKinematic = visibilitySettings.SetRigidbodyKinematic;
			}
			else if (saveableComponent != null)
			{
				saveableComponent.DisableAllColliders = visibilitySettings.DisableAllColliders;
				saveableComponent.DisableRenderers = visibilitySettings.DisableRenderers;
				saveableComponent.DisableCharacterController = visibilitySettings.DisableCharacterController;
				saveableComponent.SetRigidbodyKinematic = visibilitySettings.SetRigidbodyKinematic;
			}
		}
	}

	[MemoryPackable]
	public partial class VisibilitySettingsData
	{
		public bool DisableAllColliders { get; set; }
		public bool DisableRenderers { get; set; }
		public bool DisableCharacterController { get; set; }
		public bool SetRigidbodyKinematic { get; set; }
		public List<string> VisibleInScenes { get; set; } = new List<string>();

		[MemoryPackConstructor]
		public VisibilitySettingsData() { }
	}
}
#endif
