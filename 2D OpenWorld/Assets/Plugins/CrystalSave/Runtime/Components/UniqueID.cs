#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Arawn.CrystalSave.Runtime
{
	[AddComponentMenu("Crystal Save/ID/Unique ID Crystal Save")]
	[DisallowMultipleComponent]
	[DefaultExecutionOrder(-90)]
	public sealed class UniqueID : MonoBehaviour 
	{
		[SerializeField]
		private string id = string.Empty;                     // let OnValidate fill it

		public string ID                                          // public accessor
		{
			get => id;
			set { if (!string.IsNullOrEmpty(value)) id = value; }
		}

#if UNITY_EDITOR
		private static SaveSettings cachedSettings;
		private static bool settingsCacheInitialized = false;
		
		// Track if this specific instance has been validated to avoid redundant work
		[System.NonSerialized] private bool hasBeenValidated = false;
#endif

		/*──────────────────────────────  RUNTIME  ───────────────────────────*/
		private void Awake()
			{
				hideFlags |= HideFlags.HideInInspector;

				if (string.IsNullOrEmpty(id))
					id = Guid.NewGuid().ToString("N");             // only runtime-spawned objects
			}
			
#if UNITY_EDITOR
			/*────────────────────────────  EDIT-TIME  ───────────────────────────*/

		private void OnValidate()
		{
			if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
					return;

			// Early exit: if we already have a valid ID and have been validated, skip expensive operations
			var settings = GetCachedSettings();
			bool skipDuplicateCheck = (settings != null && settings.skipDuplicateIDCheck);
			
			if (hasBeenValidated && !string.IsNullOrEmpty(id) && skipDuplicateCheck)
			{
				hideFlags |= HideFlags.HideInInspector;
				return;
			}

			/* ❶ make sure we *have* an ID */
			if (string.IsNullOrEmpty(id))
			{
				id = Guid.NewGuid().ToString("N");
				EditorUtility.SetDirty(this);
			}

			/* ❷ fix clones: regenerate ID (GUID collision is statistically impossible) */
			if (!skipDuplicateCheck)
			{
				if (IsDuplicateInScope(id))
				{
					id = Guid.NewGuid().ToString("N");
					EditorUtility.SetDirty(this);
				}
			}	
			
			/* ❸ propagate to SaveableComponents that do not have their own UniqueID.
			* Without this guard, child objects would inherit the parent's ID and
			* become corrupt when detached. */
			foreach (var comp in GetComponentsInChildren<SaveableComponent>(includeInactive: true))
			{
				var childUid = comp.GetComponent<UniqueID>();
				if (childUid == null || childUid == this)
					comp.OverrideGameObjectID(id);
			}

			/* ❹ (optional) regenerate *componentID* when two comps of the
					*same* GameObject collide                              */
			var siblings = GetComponents<SaveableComponent>();
			var seen = new HashSet<string>();
			foreach (var sc in siblings)
			{
				if (seen.Contains(sc.ComponentID))
				{
					sc.ComponentID = Guid.NewGuid().ToString("N");
					EditorUtility.SetDirty(sc);
				}
				seen.Add(sc.ComponentID);
			}

			/* ❺ stay invisible */
			hideFlags |= HideFlags.HideInInspector;
			hasBeenValidated = true;
		}
		private static SaveSettings GetCachedSettings()
		{
			if (!settingsCacheInitialized)
			{
				settingsCacheInitialized = true;
				try
				{
					string[] guids = AssetDatabase.FindAssets("t:SaveSettings");
					foreach (string guid in guids)
					{
						string path = AssetDatabase.GUIDToAssetPath(guid);
						var asset = AssetDatabase.LoadAssetAtPath<SaveSettings>(path);
						if (asset != null)
						{
							cachedSettings = asset;
							break;
						}
					}
				}
				catch {}
			}
			return cachedSettings;
		}
		private bool IsDuplicateInScope(string candidate)
		{
#pragma warning disable CS0618 // Suppress FindObjectsByType deprecation warning for cross-version compatibility
			var all = FindObjectsByType<UniqueID>(
										FindObjectsInactive.Include, FindObjectsSortMode.None);
#pragma warning restore CS0618
			bool thisIsPrefab = PrefabUtility.IsPartOfPrefabAsset(gameObject);
			foreach (var u in all)
			{
				if (u == null || u == this)
					continue;

				// Ignore editor preview objects which use HideAndDontSave
				if ((u.hideFlags & HideFlags.DontSave) != 0)
					continue;

				bool otherIsPrefab = PrefabUtility.IsPartOfPrefabAsset(u.gameObject);

				// ignore duplicates across scene <-> prefab boundaries
				if (thisIsPrefab != otherIsPrefab)
					continue;

				if (u.ID == candidate)
					return true;
			}
			return false;
		}
#endif
	}
}
#endif