#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	// ──────────────────────────────────────────────────────────────
	// CURRENT FORMAT  (Crystal Save ≥ 1.5.0)
	// ──────────────────────────────────────────────────────────────
	[MemoryPackable]
	public partial class SaveablePrefabData
	{
		// Core identity & transform
		public string InstanceID { get; set; }
		public string PrefabID { get; set; }
		public string GameObjectName { get; set; }  // Scene instance name (preserves "Cylinder (1)" vs "Cylinder")
                public Vector3 Position { get; set; }
                public Quaternion Rotation { get; set; }
                public Vector3 Scale { get; set; }
                public string ParentID { get; set; }
                public bool IsParentSceneObject { get; set; }
                public byte[] VisibilitySettingsData { get; set; }
                public bool HasTransformOverride { get; set; } = true;
                public bool HasParentOverride { get; set; } = true;
                public bool HasVisibilityData { get; set; } = true;
                public bool UsesOptimizationFlags { get; set; } = false;

                // Load scheduling
                public int LoadPriority { get; set; }
                public bool DeferLowPriorityUntilRequested { get; set; }
                public bool ReuseSceneInstanceOnLoad { get; set; } = false;

                // Pooling settings
                public bool DisablePooling { get; set; }

                // Scene where this instance was spawned
                public string HomeScene { get; set; }

		// Rigidbody state
		public bool HasRigidbody { get; set; }
		public Vector3 RigidbodyVelocity { get; set; }
		public Vector3 RigidbodyAngularVelocity { get; set; }
		public bool RigidbodyUseGravity { get; set; }
		public bool RigidbodyIsKinematic { get; set; }
		public float RigidbodyDrag { get; set; }
		public float RigidbodyAngularDrag { get; set; }

		// 1.3.0 ─ runtime-added components & diffs
		public byte[] RuntimeModificationData { get; set; }

		// 1.5.0 ─ Animator snapshot
		public bool HasAnimator { get; set; }
		public int AnimatorStateHash { get; set; }
		public float AnimatorNormalizedTime { get; set; }

		// New: parent fingerprinting for robust scene-parent restoration
		// Helps resolve parent SaveablePrefabs that were baked into the scene
		// and had empty/changed UniqueIDs across sessions.
                public string ParentPrefabAssetID { get; set; }
		public string ParentStableKey { get; set; }

		// Active state for scene-baked prefabs to ensure correct restoration
		public bool? ActiveSelfAtSave { get; set; }

		// Tracking flags (1.6.0) - needed for procedural/runtime-modified prefabs
		public bool TrackAddedComponents { get; set; }
		public bool TrackComponentBlobs { get; set; }
		public bool TrackMaterialOverrides { get; set; }
		public bool TrackChildStateOverrides { get; set; }
		public bool TrackChildTransformOverrides { get; set; }
		public bool TrackSkinnedMeshOverrides { get; set; }
		public bool TrackBlendshapeOverrides { get; set; }
		public bool TrackTextureOverrides { get; set; }
		public bool TrackParticleSnapshots { get; set; }
		public bool TrackColliderSettings { get; set; }

		/*───────── 1.5.0 – Collider snapshot ───*/
		public List<ColliderSnapshot> Colliders { get; set; }		public SaveablePrefabData(
			string instanceID,
			string prefabID,
			string gameObjectName,
			Vector3 position,
			Quaternion rotation,
			Vector3 scale,
			string parentID,
			bool isParentSceneObject,
                        byte[] visibilitySettingsData,
                        string homeScene,
                        bool disablePooling = false)
                {
			// Identity
			InstanceID = instanceID;
			PrefabID = prefabID;
			GameObjectName = gameObjectName;

			// Transform
			Position = position;
			Rotation = rotation;
			Scale = scale;
			ParentID = parentID;
			IsParentSceneObject = isParentSceneObject;
                        VisibilitySettingsData = visibilitySettingsData;
                        HasTransformOverride = true;
                        HasParentOverride = true;
                        HasVisibilityData = visibilitySettingsData != null && visibilitySettingsData.Length > 0;
                        UsesOptimizationFlags = false;
                        HomeScene = homeScene;

                        // Load scheduling defaults
                        LoadPriority = 50;
                        DeferLowPriorityUntilRequested = false;

                        // Pooling settings
                        DisablePooling = disablePooling;

			// Rigidbody defaults
			HasRigidbody = false;
			RigidbodyVelocity = Vector3.zero;
			RigidbodyAngularVelocity = Vector3.zero;
			RigidbodyUseGravity = true;
			RigidbodyIsKinematic = false;
			RigidbodyDrag = 0f;
			RigidbodyAngularDrag = 0.05f;

			// Runtime modifications (1.3.0)
			RuntimeModificationData = null;

			// Animator defaults (1.5.0)
			HasAnimator = false;
			AnimatorStateHash = 0;
			AnimatorNormalizedTime = 0f;

			// Colliders (1.5.0)
			Colliders = new List<ColliderSnapshot>();
		}
	}

	// ───────────────────────────────────────────────
	// Auxiliary containers
	// ───────────────────────────────────────────────
	[MemoryPackable]
        public partial class RuntimeModificationData
        {
                public List<ComponentModification> AddedComponents { get; set; }
                        = new List<ComponentModification>();

		/* 1.5.0 – new high-fidelity lists */
                public List<MeshOverride> MeshOverrides { get; set; } = new();
                public List<MaterialOverride> MaterialOverrides { get; set; } = new();
                public List<BlendshapeOverride> BlendshapeOverrides { get; set; } = new();
                public List<TextureOverride> TextureOverrides { get; set; } = new();
                public List<ParticleSystemSnapshot> ParticleSnapshots { get; set; } = new();
                public List<ChildStateOverride> ChildStates { get; set; } = new();

                // New: store root-level property changes
                public RootStateOverride RootState { get; set; }
                        = null;
        }

	[MemoryPackable]
	public partial class ComponentModification
	{
		public string ComponentTypeName { get; set; } // Assembly-qualified
		public byte[] SerializedData { get; set; } // Binary state blob
	}

	/*──────── helpers (1.5.0) ────────*/
	[MemoryPackable]
	public partial class MeshOverride
	{
		public string Path;        // child transform path
		public string MeshName;    // fallback Resources load
#if UNITY_EDITOR
		public string MeshGUID;    // used in Editor for exact asset
#endif
	}

        [MemoryPackable]
        public partial class MaterialOverride
        {
                public string Path;
                public int SlotIndex;
                public string MaterialName;
#if UNITY_EDITOR
                public string MaterialGUID;
#endif
        }

        [MemoryPackable]
        public partial class BlendshapeOverride
        {
                public string Path;
                public List<BlendshapeWeight> Weights { get; set; } = new();
        }

        [MemoryPackable]
        public partial class BlendshapeWeight
        {
                public int Index { get; set; }
                public float Weight { get; set; }
        }

        [MemoryPackable]
        public partial class TextureOverride
        {
                public string Path;                // child transform path
                public int MaterialSlot;           // which material in sharedMaterials array
                public List<TextureProperty> Textures { get; set; } = new();
        }

        [MemoryPackable]
        public partial class TextureProperty
        {
                public string PropertyName { get; set; }   // e.g., "_MainTex", "_BumpMap"
                public string TextureName { get; set; }    // fallback Resources load
                public int TextureInstanceID { get; set; } // For material instances with non-Resources textures
#if UNITY_EDITOR
                public string TextureGUID { get; set; }    // used in Editor for exact asset
#endif
        }

        [MemoryPackable]
        public partial class ParticleSystemSnapshot
        {
		public string Path;
		public float Time;
		public bool WasPlaying;
	}

	[MemoryPackable]                      // 1.5.0
        public partial class ChildStateOverride
        {
                public string Guid;
                public string Path;               // "Root/Weapon/Blade"
                public bool Exists;             // false = removed,  true = still here but active flag differs
                public bool ActiveWhenSaved;    // only meaningful if Exists == true
                public string Name { get; set; }
                public string Tag { get; set; }
                public int? Layer { get; set; }
                public Vector3? Position { get; set; }
                public Quaternion? Rotation { get; set; }
                public Vector3? Scale { get; set; }
        }

        [MemoryPackable]
        public partial class RootStateOverride
        {
                public string Name { get; set; }
                public string Tag { get; set; }
                public int? Layer { get; set; }
                public bool? ActiveSelf { get; set; }
        }

	/*──────── NEW: collider container (1.5.0) ──────*/
	[MemoryPackable]
	public partial class ColliderSnapshot
	{
		public string Path;            // child transform path
		public string ColliderType;    // BoxCollider, SphereCollider, …
		public bool Enabled;
		public bool IsTrigger;

		/* type-specific */
		public Vector3 Center;
		public Vector3 Size;           // Box
		public float Radius;         // Sphere/Capsule
		public float Height;         // Capsule
		public int Direction;      // Capsule (0=x,1=y,2=z)
	}

	// ───────────────────────────────────────────────
	// LEGACY FORMAT  (pre-1.3.0 saves)
	// ───────────────────────────────────────────────
	[MemoryPackable]
	public partial class LegacySaveablePrefabData : ILegacyConvertible<SaveablePrefabData>
	{
		// Same fields the old format contained
		public string InstanceID { get; set; }
		public string PrefabID { get; set; }
		public Vector3 Position { get; set; }
		public Quaternion Rotation { get; set; }
		public Vector3 Scale { get; set; }
		public string ParentID { get; set; }
		public bool IsParentSceneObject { get; set; }
		public byte[] VisibilitySettingsData { get; set; }
		public bool HasRigidbody { get; set; }
		public Vector3 RigidbodyVelocity { get; set; }
		public Vector3 RigidbodyAngularVelocity { get; set; }
		public bool RigidbodyUseGravity { get; set; }
		public bool RigidbodyIsKinematic { get; set; }
		public float RigidbodyDrag { get; set; }
		public float RigidbodyAngularDrag { get; set; }

		public LegacySaveablePrefabData() { }

		[MemoryPackConstructor]
		public LegacySaveablePrefabData(
			string instanceID,
			string prefabID,
			Vector3 position,
			Quaternion rotation,
			Vector3 scale,
			string parentID,
			bool isParentSceneObject,
			byte[] visibilitySettingsData,
			bool hasRigidbody,
			Vector3 rigidbodyVelocity,
			Vector3 rigidbodyAngularVelocity,
			bool rigidbodyUseGravity,
			bool rigidbodyIsKinematic,
			float rigidbodyDrag,
			float rigidbodyAngularDrag)
		{
			InstanceID = instanceID;
			PrefabID = prefabID;
			Position = position;
			Rotation = rotation;
			Scale = scale;
			ParentID = parentID;
			IsParentSceneObject = isParentSceneObject;
			VisibilitySettingsData = visibilitySettingsData;
			HasRigidbody = hasRigidbody;
			RigidbodyVelocity = rigidbodyVelocity;
			RigidbodyAngularVelocity = rigidbodyAngularVelocity;
			RigidbodyUseGravity = rigidbodyUseGravity;
			RigidbodyIsKinematic = rigidbodyIsKinematic;
			RigidbodyDrag = rigidbodyDrag;
			RigidbodyAngularDrag = rigidbodyAngularDrag;
		}

		public SaveablePrefabData ConvertToCurrent()
		{
                        return new SaveablePrefabData(
                                           InstanceID, PrefabID, "", Position, Rotation, Scale,
                                           ParentID, IsParentSceneObject, VisibilitySettingsData,
                                           null, false)  // Legacy saves default to DisablePooling = false
                        {
				// Re-apply rigidbody state
				HasRigidbody = HasRigidbody,
				RigidbodyVelocity = RigidbodyVelocity,
				RigidbodyAngularVelocity = RigidbodyAngularVelocity,
				RigidbodyUseGravity = RigidbodyUseGravity,
				RigidbodyIsKinematic = RigidbodyIsKinematic,
				RigidbodyDrag = RigidbodyDrag,
				RigidbodyAngularDrag = RigidbodyAngularDrag,

				// Fields introduced after this legacy version
				RuntimeModificationData = null,   // 1.3.0+
				HasAnimator = false, // 1.5.0
				AnimatorStateHash = 0,
				AnimatorNormalizedTime = 0f,

				// Colliders (1.5.0) → empty list keeps legacy loads safe
				Colliders = new List<ColliderSnapshot>(),

				// Active state preservation (current version) - legacy saves don't have this
				ActiveSelfAtSave = null
			};
		}
	}
}
#endif