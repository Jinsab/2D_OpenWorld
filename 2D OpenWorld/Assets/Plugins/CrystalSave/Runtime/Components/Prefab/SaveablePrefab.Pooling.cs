#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime
{
	public partial class SaveablePrefab : IPoolableSaveable
	{
		public void OnBeforeSpawn()
		{
			if (!RegisterWithSaveSystem)
				return;
			
			// During loading (either global loading state or individual prefab loading state),
			// PrefabManager will assign the saved UniqueID after spawning,
			// so we should not generate a new one here as it would break restoration
			var currentState = SaveManager.Instance?.StateMachine?.CurrentState;
			if (currentState == SaveState.Loading || IsLoading)
				return;

			// fresh composite key
			SetUniqueID(Guid.NewGuid().ToString());

			// Fix for pooled prefabs: Capture the ACTIVE scene as HomeScene when remembering home scene,
			// not the pool's scene (which is typically "DontDestroyOnLoad")
			if (RememberHomeScene && HomeSceneCaptureMode == HomeSceneMode.InstantiationScene)
			{
				var activeScene = SceneManager.GetActiveScene();
				if (activeScene.IsValid())
				{
					SetHomeScene(activeScene.name);
				}
			}

			// (re-)register exactly once
			RegisterForSaving();
		}

		public void OnBeforeDespawn()
		{
			if (RegisterWithSaveSystem)
				UnregisterFromSaving();
		}
	}
}
#endif