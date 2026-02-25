#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	/// <summary>
	/// Saves and restores MeshFilter mesh references and optionally procedural mesh data.
	/// Supports both asset-based meshes (loaded via AssetProvider) and procedurally modified meshes.
	/// </summary>
	[AddComponentMenu("Crystal Save/Remember Components/Remember MeshFilter")]
	[DisallowMultipleComponent]
	[RememberTarget(typeof(MeshFilter))]
	public class RememberMeshFilter : SaveableComponent
	{
		[Header("Performance")]
		[Tooltip("Enable lightweight caching of the MeshFilter reference to avoid repeated GetComponent calls.")]
		[SerializeField] private bool enablePerformanceCaching = false;

		[Header("Save Optimization")]
		[SerializeField] private bool skipSavingWhenUnchanged;

		[Header("Mesh Reference")]
		[Tooltip("Remember which mesh asset is assigned to the MeshFilter. Uses AssetProvider to load the mesh by name/path.")]
		public bool RememberMeshReference = true;

		[Header("Procedural Mesh Data")]
		[Tooltip("Save and restore procedural mesh data (vertices, triangles, UVs, etc.). Enable this if your mesh is modified at runtime.")]
		public bool RememberProceduralMeshData = false;

		[Tooltip("Include UV2 coordinates in procedural mesh serialization (used for lightmapping).")]
		public bool IncludeUV2 = false;

		[Tooltip("Include UV3 coordinates in procedural mesh serialization.")]
		public bool IncludeUV3 = false;

		[Tooltip("Include UV4 coordinates in procedural mesh serialization.")]
		public bool IncludeUV4 = false;

		[Tooltip("Include vertex colors in procedural mesh serialization.")]
		public bool IncludeColors = false;

		[Tooltip("Include tangents in procedural mesh serialization (used for normal mapping).")]
		public bool IncludeTangents = false;

		private MeshFilter meshFilter;
		private MeshFilterSnapshot cachedSnapshot;
		private bool hasCachedSnapshot;
		private byte[] cachedSerializedData;
		private const float FloatTolerance = 0.0001f;

		// Static cache to store loaded meshes and avoid redundant load calls
		private static readonly Dictionary<string, Mesh> MeshCache = new Dictionary<string, Mesh>();

		// List of potential subfolder names within Resources where meshes might reside
		private static readonly List<string> ResourceSubfolders = new List<string>
		{
			"", // Root of Resources
			"Meshes",
			"Models",
			"meshes",
			"models"
		};

		private struct MeshFilterSnapshot
		{
			public bool MeshReferenceCaptured;
			public string MeshAssetPath;
			public string MeshName;
			public bool IsProceduralMesh;
			public MeshData ProceduralMeshData;

			public MeshFilterSnapshot Clone()
			{
				return new MeshFilterSnapshot
				{
					MeshReferenceCaptured = MeshReferenceCaptured,
					MeshAssetPath = MeshAssetPath,
					MeshName = MeshName,
					IsProceduralMesh = IsProceduralMesh,
					ProceduralMeshData = ProceduralMeshData // Note: MeshData is a reference type, but for comparison purposes this is fine
				};
			}
		}

		protected override void Awake()
		{
			base.Awake();
			meshFilter = GetComponent<MeshFilter>();
			if (meshFilter == null)
			{
				Logger.Log($"RememberMeshFilter: No MeshFilter found on '{gameObject.name}'. Disabling component.", LogCategory.RememberMeshFilter, LogLevel.Warning);
				enabled = false;
				hasCachedSnapshot = false;
				cachedSnapshot = default;
				return;
			}

			if (skipSavingWhenUnchanged && TryCaptureCurrentState(out MeshFilterSnapshot snapshot, false))
			{
				cachedSnapshot = snapshot.Clone();
				hasCachedSnapshot = true;
			}
			else
			{
				cachedSnapshot = default;
				hasCachedSnapshot = false;
			}
		}

		/// <summary>
		/// Serializes the MeshFilter data based on toggles.
		/// </summary>
		/// <returns>Serialized byte array of MeshFilterData.</returns>
		protected override byte[] SerializeComponentData()
		{
			if (!TryCaptureCurrentState(out MeshFilterSnapshot currentSnapshot, true))
			{
				if (skipSavingWhenUnchanged)
				{
					cachedSnapshot = default;
					hasCachedSnapshot = false;
				}
				return null;
			}

			if (skipSavingWhenUnchanged && hasCachedSnapshot && AreEquivalent(cachedSnapshot, currentSnapshot))
			{
				if (cachedSerializedData != null && cachedSerializedData.Length > 0)
				{
					return cachedSerializedData;
				}
			}

			MeshFilterData data = ConvertSnapshotToData(currentSnapshot);

			try
			{
				byte[] serializedData = Serializer.Serialize<MeshFilterData>(data);
				Logger.Log($"RememberMeshFilter: Successfully serialized mesh filter data for '{gameObject.name}'.", LogCategory.RememberMeshFilter, LogLevel.Info);

				if (skipSavingWhenUnchanged)
				{
					cachedSnapshot = currentSnapshot.Clone();
					hasCachedSnapshot = true;
					cachedSerializedData = serializedData;
				}

				return serializedData;
			}
			catch (Exception ex)
			{
				Logger.Log($"RememberMeshFilter: Serialization error for '{gameObject.name}': {ex.Message}", LogCategory.RememberMeshFilter, LogLevel.Error);
				return null;
			}
		}

		private bool TryCaptureCurrentState(out MeshFilterSnapshot snapshot, bool logWarnings)
		{
			snapshot = default;

			MeshFilter filter = enablePerformanceCaching ? meshFilter : GetComponent<MeshFilter>();

			if (filter == null)
			{
				if (logWarnings)
				{
					Logger.Log($"SerializeComponentData: No MeshFilter on '{gameObject.name}'. Skipping serialization.", LogCategory.RememberMeshFilter, LogLevel.Warning);
				}
				return false;
			}

			Mesh mesh = filter.sharedMesh;
			if (mesh == null)
			{
				if (logWarnings)
				{
					Logger.Log($"SerializeComponentData: No mesh assigned to MeshFilter on '{gameObject.name}'. Skipping serialization.", LogCategory.RememberMeshFilter, LogLevel.Warning);
				}
				return false;
			}

			MeshFilterSnapshot tempSnapshot = new MeshFilterSnapshot();
			bool capturedAny = false;

			// Determine if the mesh is procedural (instance) or asset-based
			bool isInstanceMesh = mesh.name.EndsWith("(Instance)") || mesh.name.EndsWith(" Instance");
			string baseMeshName = GetBaseMeshName(mesh.name);

			if (RememberProceduralMeshData && isInstanceMesh)
			{
				// Save full procedural mesh data
				tempSnapshot.IsProceduralMesh = true;
				tempSnapshot.MeshName = baseMeshName;
				tempSnapshot.ProceduralMeshData = MeshData.FromProceduralMesh(
					mesh,
					IncludeUV2,
					IncludeUV3,
					IncludeUV4,
					IncludeColors,
					IncludeTangents,
					false, // bone weights (not applicable for MeshFilter)
					false  // bind poses (not applicable for MeshFilter)
				);
				tempSnapshot.MeshReferenceCaptured = true;
				capturedAny = true;
			}
			else if (RememberMeshReference)
			{
				// Save mesh asset reference
				tempSnapshot.IsProceduralMesh = false;
				tempSnapshot.MeshName = baseMeshName;
				tempSnapshot.MeshAssetPath = GetMeshAssetPath(mesh);
				tempSnapshot.MeshReferenceCaptured = true;
				capturedAny = true;
			}

			if (!capturedAny)
			{
				return false;
			}

			snapshot = tempSnapshot;
			return true;
		}

		private MeshFilterData ConvertSnapshotToData(MeshFilterSnapshot snapshot)
		{
			MeshFilterData data = new MeshFilterData
			{
				MeshName = snapshot.MeshName,
				MeshAssetPath = snapshot.MeshAssetPath,
				IsProceduralMesh = snapshot.IsProceduralMesh,
				ProceduralMeshData = snapshot.ProceduralMeshData
			};
			return data;
		}

		private bool AreEquivalent(MeshFilterSnapshot cached, MeshFilterSnapshot current)
		{
			if (cached.MeshReferenceCaptured != current.MeshReferenceCaptured)
			{
				return false;
			}

			if (!string.Equals(cached.MeshName, current.MeshName, StringComparison.Ordinal))
			{
				return false;
			}

			if (cached.IsProceduralMesh != current.IsProceduralMesh)
			{
				return false;
			}

			if (!cached.IsProceduralMesh)
			{
				return string.Equals(cached.MeshAssetPath, current.MeshAssetPath, StringComparison.Ordinal);
			}

			// For procedural meshes, we'd need deep comparison which is expensive
			// For now, assume they're different if procedural (will always re-serialize)
			return false;
		}

		/// <summary>
		/// Deserializes and applies the MeshFilter data based on toggles.
		/// </summary>
		/// <param name="data">Serialized byte array of MeshFilterData.</param>
		protected override void DeserializeComponentData(byte[] data)
		{
			MeshFilter filter = enablePerformanceCaching ? meshFilter : GetComponent<MeshFilter>();

			if (filter == null)
			{
				Logger.Log($"DeserializeComponentData: No MeshFilter on '{gameObject.name}'. Skipping deserialization.", LogCategory.RememberMeshFilter, LogLevel.Warning);
				return;
			}

			if (data == null || data.Length == 0)
			{
				Logger.Log("DeserializeComponentData failed: data is null or empty.", LogCategory.RememberMeshFilter, LogLevel.Warning);
				return;
			}

			try
			{
				MeshFilterData deserializedData = Serializer.Deserialize<MeshFilterData>(data);
				if (deserializedData == null)
				{
					Logger.Log("DeserializeComponentData failed: deserialized data is null.", LogCategory.RememberMeshFilter, LogLevel.Warning);
					return;
				}

				if (deserializedData.IsProceduralMesh && deserializedData.ProceduralMeshData != null)
				{
					// Restore procedural mesh
					Mesh restoredMesh = deserializedData.ProceduralMeshData.ToMesh();
					if (restoredMesh != null)
					{
						filter.mesh = restoredMesh;
						Logger.Log($"RememberMeshFilter: Restored procedural mesh '{deserializedData.MeshName}' for '{gameObject.name}'.", LogCategory.RememberMeshFilter, LogLevel.Info);
					}
					else
					{
						Logger.Log($"RememberMeshFilter: Failed to restore procedural mesh for '{gameObject.name}'.", LogCategory.RememberMeshFilter, LogLevel.Warning);
					}
				}
				else if (RememberMeshReference && !string.IsNullOrEmpty(deserializedData.MeshAssetPath))
				{
					// Load mesh from asset
					Mesh loadedMesh = LoadMesh(deserializedData.MeshAssetPath);
					if (loadedMesh != null)
					{
						filter.sharedMesh = loadedMesh;
						Logger.Log($"RememberMeshFilter: Restored mesh '{deserializedData.MeshAssetPath}' for '{gameObject.name}'.", LogCategory.RememberMeshFilter, LogLevel.Info);
					}
					else
					{
						Logger.Log($"RememberMeshFilter: Could not find mesh '{deserializedData.MeshAssetPath}' for '{gameObject.name}'.", LogCategory.RememberMeshFilter, LogLevel.Warning);
					}
				}
				else if (!string.IsNullOrEmpty(deserializedData.MeshName))
				{
					// Try to find by name
					Mesh loadedMesh = LoadMesh(deserializedData.MeshName);
					if (loadedMesh != null)
					{
						filter.sharedMesh = loadedMesh;
						Logger.Log($"RememberMeshFilter: Restored mesh '{deserializedData.MeshName}' for '{gameObject.name}'.", LogCategory.RememberMeshFilter, LogLevel.Info);
					}
				}

				if (skipSavingWhenUnchanged)
				{
					if (TryCaptureCurrentState(out MeshFilterSnapshot snapshot, false))
					{
						cachedSnapshot = snapshot.Clone();
						hasCachedSnapshot = true;
					}
					else
					{
						cachedSnapshot = default;
						hasCachedSnapshot = false;
					}
				}

				Logger.Log($"RememberMeshFilter: Successfully loaded mesh filter data for '{gameObject.name}'.", LogCategory.RememberMeshFilter, LogLevel.Info);
			}
			catch (Exception ex)
			{
				Logger.Log($"RememberMeshFilter: DeserializeComponentData encountered an error: {ex.Message}", LogCategory.RememberMeshFilter, LogLevel.Error);
			}
		}

		protected override void OnEnable()
		{
			base.OnEnable();
		}

		protected override void OnDisable()
		{
			base.OnDisable();
		}

		/// <summary>
		/// Gets the base mesh name without instance suffixes.
		/// </summary>
		private string GetBaseMeshName(string meshName)
		{
			if (string.IsNullOrEmpty(meshName))
			{
				return string.Empty;
			}

			if (meshName.EndsWith("(Instance)"))
			{
				return meshName.Replace("(Instance)", "").Trim();
			}

			if (meshName.EndsWith(" Instance"))
			{
				return meshName.Replace(" Instance", "").Trim();
			}

			return meshName;
		}

		/// <summary>
		/// Gets the asset path for a mesh by searching known subfolders.
		/// </summary>
		private string GetMeshAssetPath(Mesh mesh)
		{
			if (mesh == null)
			{
				return string.Empty;
			}

			string baseName = GetBaseMeshName(mesh.name);

			// Try to find the mesh in known subfolders
			foreach (var subfolder in ResourceSubfolders)
			{
				string path = string.IsNullOrEmpty(subfolder) ? baseName : $"{subfolder}/{baseName}";
				Mesh loadedMesh = AssetProvider.Load<Mesh>(path);
				if (loadedMesh != null)
				{
					Logger.Log($"GetMeshAssetPath: Found '{baseName}' in '{path}'.", LogCategory.RememberMeshFilter, LogLevel.Info);
					return path;
				}
			}

			// Return base name as fallback
			Logger.Log($"GetMeshAssetPath: Could not find direct resource path for '{mesh.name}'. Returning base name.", LogCategory.RememberMeshFilter, LogLevel.Info);
			return baseName;
		}

		/// <summary>
		/// Loads a mesh from Resources/Addressables using caching.
		/// </summary>
		private Mesh LoadMesh(string meshPath)
		{
			if (string.IsNullOrEmpty(meshPath))
			{
				return null;
			}

			if (MeshCache.TryGetValue(meshPath, out var cachedMesh))
			{
				return cachedMesh;
			}

			// Try direct load
			Mesh foundMesh = AssetProvider.Load<Mesh>(meshPath);
			if (foundMesh != null)
			{
				MeshCache[meshPath] = foundMesh;
				return foundMesh;
			}

			// Try subfolders
			string baseName = GetBaseMeshName(meshPath);
			foreach (var subfolder in ResourceSubfolders)
			{
				string candidatePath = string.IsNullOrEmpty(subfolder) ? baseName : $"{subfolder}/{baseName}";
				foundMesh = AssetProvider.Load<Mesh>(candidatePath);
				if (foundMesh != null)
				{
					Logger.Log($"LoadMesh: Found mesh '{baseName}' in '{candidatePath}'.", LogCategory.RememberMeshFilter, LogLevel.Info);
					MeshCache[meshPath] = foundMesh;
					return foundMesh;
				}
			}

			Logger.Log($"LoadMesh: Could not find mesh '{meshPath}' in any known subfolder.", LogCategory.RememberMeshFilter, LogLevel.Warning);
			return null;
		}
	}

	/// <summary>
	/// Data structure for MeshFilter serialization.
	/// </summary>
	[MemoryPackable]
	public partial class MeshFilterData
	{
		/// <summary>
		/// The name of the mesh.
		/// </summary>
		public string MeshName { get; set; }

		/// <summary>
		/// The asset path used to load the mesh via AssetProvider.
		/// </summary>
		public string MeshAssetPath { get; set; }

		/// <summary>
		/// Whether this is a procedural mesh that needs full data restoration.
		/// </summary>
		public bool IsProceduralMesh { get; set; }

		/// <summary>
		/// Full procedural mesh data (only populated if IsProceduralMesh is true).
		/// </summary>
		public MeshData ProceduralMeshData { get; set; }

		public MeshFilterData() { }
	}
}
#endif
