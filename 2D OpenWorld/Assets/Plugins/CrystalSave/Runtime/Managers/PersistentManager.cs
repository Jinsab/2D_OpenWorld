// PersistentManager.cs  ©2025 Arawn – Crystal Save
#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	/// <summary>Marks objects as <c>DontDestroyOnLoad</c> when
	/// <paramref name="keepAcrossScenes"/> is <c>true</c>.</summary>
	public static class PersistentManager
	{
		private static readonly object _lock = new();

		/// <remarks>
		/// • If <paramref name="keepAcrossScenes"/> is <c>false</c> the call is ignored.  
		/// • All work is executed on the main-thread; every step re-checks the
		///   reference so a GameObject destroyed meanwhile never trips a
		///   <c>MissingReferenceException</c>.
		/// </remarks>
		public static void MakePersistent(GameObject obj, bool keepAcrossScenes)
		{
			if (!keepAcrossScenes) return;                 // caller opted out
			if (!obj || obj.Equals(null)) return;          // destroyed already

			/* Always marshal to the main thread – we don’t need IsMainThread  */
			UnityMainThreadDispatcher.Instance().Enqueue(() =>
			{
				/* The object might have been destroyed in the meantime */
				if (!obj || obj.Equals(null)) return;

				lock (_lock)
				{
					string uid =
						obj.GetComponent<SceneObjectID>()?.UniqueID ??
						obj.GetComponent<SaveablePrefab>()?.UniqueID ??
						obj.GetComponent<UniqueID>()?.ID;

				if (string.IsNullOrEmpty(uid))
				{
					Logger.Log($"PersistentManager: '{obj.name}' has no UniqueID – skipping.",
							   LogCategory.SceneManagement, LogLevel.Warning);
					return;
				}					/* If another instance with the same UID exists we keep the first */
                                        GameObject existing = SaveManager.Instance ?
                                                SaveManager.Instance.FindGameObjectByUniqueID(uid, SaveManager.IdentifierType.UniqueID) : null;

				if (existing && existing != obj && !existing.Equals(null))
				{
					Logger.Log($"PersistentManager: Duplicate '{obj.name}' (UID {uid}) – destroying clone.",
							   LogCategory.SceneManagement, LogLevel.Info);
					DestroyHelper.DestroyWithLogging(obj,
						"PersistentManager duplicate removal");
					return;
				}					/* Make persistent if not yet in the DDOL scene */
					if (obj.scene.name != "DontDestroyOnLoad")
					{
					obj.transform.SetParent(null);     // ensure it's a root
					Object.DontDestroyOnLoad(obj);
					Logger.Log($"PersistentManager: '{obj.name}' set to DontDestroyOnLoad.",
							   LogCategory.SceneManagement, LogLevel.Info);
				}

				/* Register with SaveManager if available */
					if (SaveManager.Instance)
					{
						var settings = obj.GetComponent<RememberGameObject>()?.PropertySettings
									 ?? new GameObjectPropertySettings { RememberActive = true };

						SaveManager.Instance.RegisterGameObject(obj, settings);
					}
				}
			});
		}
	}
}
#endif