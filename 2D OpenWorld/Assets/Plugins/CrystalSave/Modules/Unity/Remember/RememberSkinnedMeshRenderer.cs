// RememberSkinnedMeshRenderer.cs
#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Arawn.CrystalSave.Runtime
{
	[AddComponentMenu("Crystal Save/Remember Components/Remember SkinnedMeshRenderer")]
	[DisallowMultipleComponent]
	[RememberTarget(typeof(SkinnedMeshRenderer))]
	public class RememberSkinnedMeshRenderer : SaveableComponent
	{
		[Header("Performance")]
		[Tooltip("Enable lightweight caching of the SkinnedMeshRenderer reference to avoid repeated GetComponent calls.")]
		[SerializeField] private bool enablePerformanceCaching = false;
                [Header("Save Optimization")]
                [SerializeField] private bool skipSavingWhenUnchanged;

                private SkinnedMeshRendererData cachedSnapshot;
                private bool hasCachedSnapshot;
                private byte[] cachedSerializedData;

                private const float FloatTolerance = 0.0001f;
                private const float ColorTolerance = 0.001f;

                [Header("Blendshape Weights")]
                [Tooltip("Enable or disable serialization of blend shape weights.")]
                public bool RememberBlendShapeWeights = true;

		[Header("SkinnedMeshRenderer Toggles")]
		[Tooltip("Serialize references to shared materials.")]
		public bool RememberSharedMaterials = false;

		[Tooltip("Serialize properties of unique material instances (e.g., color).")]
		public bool RememberUniqueMaterialProperties = false;

		[Header("SkinnedMeshRenderer Properties Toggles")]
		[Tooltip("Serialize the enabled state of the SkinnedMeshRenderer.")]
		public bool RememberEnabled = true;

		[Tooltip("Serialize the Shadow Casting Mode.")]
		public bool RememberShadowCastingMode = false;

		[Tooltip("Serialize whether the SkinnedMeshRenderer receives shadows.")]
		public bool RememberReceiveShadows = false;

		[Tooltip("Serialize the Light Probe Usage.")]
		public bool RememberLightProbeUsage = false;

		[Tooltip("Serialize the Reflection Probe Usage.")]
		public bool RememberReflectionProbeUsage = false;

		[Tooltip("Serialize the Probe Anchor GameObject name.")]
		public bool RememberProbeAnchor = false;

		[Tooltip("Serialize the Motion Vector Generation Mode.")]
		public bool RememberMotionVectorGenerationMode = false;

		[Tooltip("Serialize the Sorting Layer ID.")]
		public bool RememberSortingLayerID = false;

		[Tooltip("Serialize the Sorting Order.")]
		public bool RememberSortingOrder = false;

		[Tooltip("Serialize the Root Bone reference.")]
		public bool RememberRootBone = false;

		[Tooltip("Serialize the Quality setting.")]
		public bool RememberQuality = false;

		[Tooltip("Serialize whether the SkinnedMeshRenderer updates when offscreen.")]
		public bool RememberUpdateWhenOffscreen = false;

		[Header("Mesh Reference")]
		[Tooltip("Remember which mesh asset is assigned to the SkinnedMeshRenderer. Uses AssetProvider to load the mesh by name/path.")]
		public bool RememberMeshReference = false;

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

		[Tooltip("Include bone weights in procedural mesh serialization (for fully procedural skinned meshes).")]
		public bool IncludeBoneWeights = false;

		[Tooltip("Include bind poses in procedural mesh serialization (for fully procedural skinned meshes).")]
		public bool IncludeBindPoses = false;

		[SerializeField]
		private SkinnedMeshRendererData data = new SkinnedMeshRendererData();

		public List<float> BlendShapeWeights => data.BlendShapeWeights;

		private SkinnedMeshRenderer skinnedMeshRenderer;

		// Static cache to store loaded materials and avoid redundant Resources.Load calls
		private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

		// Static cache to store loaded meshes and avoid redundant load calls
		private static readonly Dictionary<string, Mesh> MeshCache = new Dictionary<string, Mesh>();

		// List of potential subfolder names within Resources where materials might reside
		private static readonly List<string> ResourceSubfolders = new List<string>
		{
			"", // Root of Resources
            "Materials",
			"Mats",
			"Mat",
			"materials",
			"mats",
			"mat"
            // Add more variations or additional subfolders if needed
        };

		// List of potential subfolder names within Resources where meshes might reside
		private static readonly List<string> MeshResourceSubfolders = new List<string>
		{
			"", // Root of Resources
			"Meshes",
			"Models",
			"meshes",
			"models"
		};

                protected override void Awake()
                {
                        base.Awake();
                        // Always get the SkinnedMeshRenderer component, regardless of caching setting
                        skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
                        if (skinnedMeshRenderer == null)
                        {
                                Logger.Log($"RememberSkinnedMeshRenderer: No SkinnedMeshRenderer found on '{gameObject.name}'. Disabling component.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Warning);
                                enabled = false;
                        }

                        if (skipSavingWhenUnchanged)
                        {
                                if (TryCaptureCurrentState(out var snapshot, false))
                                {
                                        cachedSnapshot = CloneSnapshot(snapshot);
                                        hasCachedSnapshot = cachedSnapshot != null;
                                }
                                else
                                {
                                        cachedSnapshot = null;
                                        hasCachedSnapshot = false;
                                }
                        }
                        else
                        {
                                cachedSnapshot = null;
                                hasCachedSnapshot = false;
                        }
                }

                /// <summary>
                /// Serializes the SkinnedMeshRenderer properties based on toggles.
                /// </summary>
		/// <returns>Serialized byte array of SkinnedMeshRendererData.</returns>
		protected override byte[] SerializeComponentData()
		{
                        if (!TryCaptureCurrentState(out var snapshot, true))
                        {
                                return null;
                        }

                        if (skipSavingWhenUnchanged && hasCachedSnapshot && AreEquivalent(cachedSnapshot, snapshot))
                        {
                                if (cachedSerializedData != null && cachedSerializedData.Length > 0)
                                {
                                        Logger.Log($"RememberSkinnedMeshRenderer: Returning cached serialized data for '{gameObject.name}' (unchanged).", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Off);
                                        return cachedSerializedData;
                                }
                                
                                Logger.Log($"RememberSkinnedMeshRenderer: Data unchanged but no cached serialized data for '{gameObject.name}' - will serialize fresh.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Off);
                        }

                        try
                        {
                                byte[] serializedData = Serializer.Serialize<SkinnedMeshRendererData>(snapshot);
                                if (skipSavingWhenUnchanged)
                                {
                                        cachedSnapshot = CloneSnapshot(snapshot);
                                        hasCachedSnapshot = cachedSnapshot != null;
                                        cachedSerializedData = serializedData;
                                }
                                Logger.Log($"RememberSkinnedMeshRenderer: Successfully serialized SkinnedMeshRenderer data for '{gameObject.name}'.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Info);
                                return serializedData;
                        }
			catch (Exception ex)
			{
				Logger.Log($"RememberSkinnedMeshRenderer: Serialization error for '{gameObject.name}': {ex.Message}", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Error);
				return null;
			}
		}

		/// <summary>
		/// Deserializes and applies the SkinnedMeshRenderer properties based on toggles.
		/// </summary>
		/// <param name="binaryData">Serialized byte array of SkinnedMeshRendererData.</param>
		protected override void DeserializeComponentData(byte[] binaryData)
		{
			// Use cached reference if caching enabled, otherwise get component each time
			SkinnedMeshRenderer renderer = enablePerformanceCaching ? skinnedMeshRenderer : GetComponent<SkinnedMeshRenderer>();
			
			if (renderer == null)
			{
				Logger.Log($"DeserializeComponentData: No SkinnedMeshRenderer on '{gameObject.name}'. Skipping deserialization.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Warning);
				return;
			}

			if (binaryData == null || binaryData.Length == 0)
			{
				Logger.Log("DeserializeComponentData failed: binaryData is null or empty.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Warning);
				return;
			}

			try
			{
				SkinnedMeshRendererData deserializedData = Serializer.Deserialize<SkinnedMeshRendererData>(binaryData);
				if (deserializedData == null)
				{
					Logger.Log("DeserializeComponentData failed: deserialized data is null.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Warning);
					return;
				}

                                // Deserialize Shared Materials
                                if (RememberSharedMaterials && deserializedData.SharedMaterialNames != null && deserializedData.SharedMaterialNames.Length > 0)
                                {
                                        Material[] restoredSharedMats = deserializedData.SharedMaterialNames.Select(matPath =>
                                        {
                                                if (!string.IsNullOrEmpty(matPath))
						{
							// Attempt to load the material from Resources
							Material foundMat = LoadMaterial(matPath);
							if (foundMat == null)
							{
								Logger.Log($"RememberSkinnedMeshRenderer: Could not find shared material '{matPath}' in Resources for '{gameObject.name}'. Assigning default material.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Warning);
								// Assign a default material (e.g., Standard shader)
								Shader standardShader = Shader.Find("Standard");
								return standardShader != null ? new Material(standardShader) : null;
							}
							return foundMat;
						}
						return null;
					}).ToArray();

					// Assign restored shared materials
					renderer.sharedMaterials = restoredSharedMats;
				}

				// Deserialize Unique Material Properties
				if (RememberUniqueMaterialProperties && deserializedData.UniqueMaterialProperties != null && deserializedData.UniqueMaterialProperties.Length > 0)
				{
					Material[] uniqueMats = renderer.materials;
					foreach (var uniqueData in deserializedData.UniqueMaterialProperties)
					{
						// Find the material by name (without "(Instance)")
						Material targetMat = uniqueMats.FirstOrDefault(m => m != null && m.name.Replace("(Instance)", "").Trim() == uniqueData.MaterialName);
						if (targetMat != null)
						{
							// Apply properties
							if (targetMat.HasProperty("_Color"))
							{
								targetMat.color = uniqueData.Color;
							}
							// Apply other properties as needed
						}
						else
						{
							Logger.Log($"RememberSkinnedMeshRenderer: Could not find unique material '{uniqueData.MaterialName}' on '{gameObject.name}'.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Warning);
						}
					}
				}

				// Deserialize Blend Shape Weights
				if (RememberBlendShapeWeights && deserializedData.BlendShapeWeights != null && deserializedData.BlendShapeWeights.Count > 0) // Conditional Deserialization
				{
					int blendShapeCount = renderer.sharedMesh != null ? renderer.sharedMesh.blendShapeCount : 0;
					int weightsCount = deserializedData.BlendShapeWeights.Count;

					// Determine the minimum count to prevent index out of range
					int minCount = Math.Min(blendShapeCount, weightsCount);

					for (int i = 0; i < minCount; i++)
					{
						renderer.SetBlendShapeWeight(i, deserializedData.BlendShapeWeights[i]);
					}

					if (weightsCount > blendShapeCount)
					{
						Logger.Log($"RememberSkinnedMeshRenderer: Deserialized blend shape weights count ({weightsCount}) exceeds blend shape count ({blendShapeCount}) on '{gameObject.name}'. Extra weights are ignored.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Warning);
					}
					else if (weightsCount < blendShapeCount)
					{
						Logger.Log($"RememberSkinnedMeshRenderer: Deserialized blend shape weights count ({weightsCount}) is less than blend shape count ({blendShapeCount}) on '{gameObject.name}'. Missing weights are left unchanged.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Warning);
					}

					Logger.Log($"RememberSkinnedMeshRenderer: Successfully loaded {minCount} blend shape weights for GameObject '{gameObject.name}'.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Info);
				}

				// Deserialize Other SkinnedMeshRenderer Properties
				if (RememberEnabled)
				{
					renderer.enabled = deserializedData.RendererEnabled;
				}

				if (RememberShadowCastingMode)
				{
					renderer.shadowCastingMode = deserializedData.ShadowCasting;
				}

				if (RememberReceiveShadows)
				{
					renderer.receiveShadows = deserializedData.ReceiveShadows;
				}

				if (RememberLightProbeUsage)
				{
					renderer.lightProbeUsage = deserializedData.LightProbeUsage;
				}

				if (RememberReflectionProbeUsage)
				{
					renderer.reflectionProbeUsage = deserializedData.ReflectionProbeUsage;
				}

				if (RememberProbeAnchor && !string.IsNullOrEmpty(deserializedData.ProbeAnchorName))
				{
					// Attempt to find the probe anchor by name in the scene
					Transform anchor = FindTransformInScene(deserializedData.ProbeAnchorName);
					if (anchor != null)
					{
						renderer.probeAnchor = anchor;
					}
					else
					{
						Logger.Log($"RememberSkinnedMeshRenderer: Could not find probe anchor '{deserializedData.ProbeAnchorName}' in scene for '{gameObject.name}'.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Warning);
					}
				}

				if (RememberMotionVectorGenerationMode)
				{
					renderer.motionVectorGenerationMode = deserializedData.MotionVectorMode;
				}

				if (RememberSortingLayerID)
				{
					renderer.sortingLayerID = deserializedData.SortingLayerID;
				}

				if (RememberSortingOrder)
				{
					renderer.sortingOrder = deserializedData.SortingOrder;
				}

				if (RememberRootBone && !string.IsNullOrEmpty(deserializedData.RootBoneName))
				{
					// Attempt to find the root bone by name in the scene
					Transform rootBone = FindTransformInScene(deserializedData.RootBoneName);
					if (rootBone != null)
					{
						renderer.rootBone = rootBone;
					}
					else
					{
						Logger.Log($"RememberSkinnedMeshRenderer: Could not find root bone '{deserializedData.RootBoneName}' in scene for '{gameObject.name}'.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Warning);
					}
				}

				if (RememberQuality)
				{
					renderer.quality = deserializedData.Quality;
				}

                                if (RememberUpdateWhenOffscreen)
                                {
                                        renderer.updateWhenOffscreen = deserializedData.UpdateWhenOffscreen;
                                }

                                // Restore mesh reference or procedural mesh
                                if (RememberProceduralMeshData && deserializedData.IsProceduralMesh && deserializedData.ProceduralMeshData != null)
                                {
                                        // Restore procedural mesh
                                        Mesh restoredMesh = deserializedData.ProceduralMeshData.ToMesh();
                                        if (restoredMesh != null)
                                        {
                                                renderer.sharedMesh = restoredMesh;
                                                Logger.Log($"RememberSkinnedMeshRenderer: Restored procedural mesh '{deserializedData.MeshName}' for '{gameObject.name}'.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Info);
                                        }
                                        else
                                        {
                                                Logger.Log($"RememberSkinnedMeshRenderer: Failed to restore procedural mesh for '{gameObject.name}'.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Warning);
                                        }
                                }
                                else if (RememberMeshReference && !string.IsNullOrEmpty(deserializedData.MeshAssetPath))
                                {
                                        // Load mesh from asset
                                        Mesh loadedMesh = LoadMesh(deserializedData.MeshAssetPath);
                                        if (loadedMesh != null)
                                        {
                                                renderer.sharedMesh = loadedMesh;
                                                Logger.Log($"RememberSkinnedMeshRenderer: Restored mesh '{deserializedData.MeshAssetPath}' for '{gameObject.name}'.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Info);
                                        }
                                        else
                                        {
                                                Logger.Log($"RememberSkinnedMeshRenderer: Could not find mesh '{deserializedData.MeshAssetPath}' for '{gameObject.name}'.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Warning);
                                        }
                                }
                                else if (RememberMeshReference && !string.IsNullOrEmpty(deserializedData.MeshName))
                                {
                                        // Try to find by name
                                        Mesh loadedMesh = LoadMesh(deserializedData.MeshName);
                                        if (loadedMesh != null)
                                        {
                                                renderer.sharedMesh = loadedMesh;
                                                Logger.Log($"RememberSkinnedMeshRenderer: Restored mesh '{deserializedData.MeshName}' for '{gameObject.name}'.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Info);
                                        }
                                }

                                Logger.Log($"RememberSkinnedMeshRenderer: Successfully loaded SkinnedMeshRenderer data for '{gameObject.name}'.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Info);

                                if (skipSavingWhenUnchanged)
                                {
                                        cachedSnapshot = CloneSnapshot(deserializedData);
                                        hasCachedSnapshot = cachedSnapshot != null;
                                }
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"RememberSkinnedMeshRenderer: DeserializeComponentData encountered an error: {ex.Message}", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Error);
                        }
                }

                private bool TryCaptureCurrentState(out SkinnedMeshRendererData snapshot, bool logWarnings)
                {
                        snapshot = null;

                        SkinnedMeshRenderer renderer = enablePerformanceCaching ? skinnedMeshRenderer : GetComponent<SkinnedMeshRenderer>();

                        if (renderer == null)
                        {
                                if (logWarnings)
                                {
                                        Logger.Log($"SerializeComponentData: No SkinnedMeshRenderer on '{gameObject.name}'. Skipping serialization.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Warning);
                                }
                                return false;
                        }

                        SkinnedMeshRendererData data = new SkinnedMeshRendererData
                        {
                                BlendShapeWeights = null
                        };

                        bool capturedAny = false;

                        if (RememberSharedMaterials)
                        {
                                Material[] sharedMats = renderer.sharedMaterials;
                                data.SharedMaterialNames = sharedMats != null
                                        ? sharedMats.Select(m => m != null ? GetRelativeResourcePath(m) : string.Empty).ToArray()
                                        : Array.Empty<string>();
                                capturedAny = true;
                        }

                        if (RememberUniqueMaterialProperties)
                        {
                                Material[] uniqueMats = renderer.materials;
                                List<UniqueMaterialData> uniqueMaterialDataList = new List<UniqueMaterialData>();

                                if (uniqueMats != null)
                                {
                                        foreach (var mat in uniqueMats)
                                        {
                                                if (mat != null && mat.name.EndsWith("(Instance)"))
                                                {
                                                        UniqueMaterialData uniqueData = new UniqueMaterialData
                                                        {
                                                                MaterialName = mat.name.Replace("(Instance)", "").Trim(),
                                                                Color = mat.HasProperty("_Color") ? mat.color : Color.white
                                                        };
                                                        uniqueMaterialDataList.Add(uniqueData);
                                                }
                                        }
                                }

                                data.UniqueMaterialProperties = uniqueMaterialDataList.Count > 0
                                        ? uniqueMaterialDataList.Select(CloneUniqueMaterialData).ToArray()
                                        : Array.Empty<UniqueMaterialData>();
                                capturedAny = true;
                        }

                        if (RememberBlendShapeWeights)
                        {
                                List<float> weights = new List<float>();
                                int blendShapeCount = renderer.sharedMesh != null ? renderer.sharedMesh.blendShapeCount : 0;
                                for (int i = 0; i < blendShapeCount; i++)
                                {
                                        float weight = renderer.GetBlendShapeWeight(i);
                                        weights.Add(weight);
                                }

                                data.BlendShapeWeights = weights;
                                capturedAny = true;
                        }

                        if (RememberEnabled)
                        {
                                data.RendererEnabled = renderer.enabled;
                                capturedAny = true;
                        }

                        if (RememberShadowCastingMode)
                        {
                                data.ShadowCasting = renderer.shadowCastingMode;
                                capturedAny = true;
                        }

                        if (RememberReceiveShadows)
                        {
                                data.ReceiveShadows = renderer.receiveShadows;
                                capturedAny = true;
                        }

                        if (RememberLightProbeUsage)
                        {
                                data.LightProbeUsage = renderer.lightProbeUsage;
                                capturedAny = true;
                        }

                        if (RememberReflectionProbeUsage)
                        {
                                data.ReflectionProbeUsage = renderer.reflectionProbeUsage;
                                capturedAny = true;
                        }

                        if (RememberProbeAnchor)
                        {
                                data.ProbeAnchorName = renderer.probeAnchor ? renderer.probeAnchor.name : string.Empty;
                                capturedAny = true;
                        }

                        if (RememberMotionVectorGenerationMode)
                        {
                                data.MotionVectorMode = renderer.motionVectorGenerationMode;
                                capturedAny = true;
                        }

                        if (RememberSortingLayerID)
                        {
                                data.SortingLayerID = renderer.sortingLayerID;
                                capturedAny = true;
                        }

                        if (RememberSortingOrder)
                        {
                                data.SortingOrder = renderer.sortingOrder;
                                capturedAny = true;
                        }

                        if (RememberRootBone)
                        {
                                data.RootBoneName = renderer.rootBone ? renderer.rootBone.name : string.Empty;
                                capturedAny = true;
                        }

                        if (RememberQuality)
                        {
                                data.Quality = renderer.quality;
                                capturedAny = true;
                        }

                        if (RememberUpdateWhenOffscreen)
                        {
                                data.UpdateWhenOffscreen = renderer.updateWhenOffscreen;
                                capturedAny = true;
                        }

                        // Mesh reference and procedural mesh data
                        if (RememberMeshReference || RememberProceduralMeshData)
                        {
                                Mesh mesh = renderer.sharedMesh;
                                if (mesh != null)
                                {
                                        bool isInstanceMesh = mesh.name.EndsWith("(Instance)") || mesh.name.EndsWith(" Instance");
                                        string baseMeshName = GetBaseMeshName(mesh.name);

                                        if (RememberProceduralMeshData && isInstanceMesh)
                                        {
                                                // Save full procedural mesh data
                                                data.IsProceduralMesh = true;
                                                data.MeshName = baseMeshName;
                                                data.ProceduralMeshData = MeshData.FromProceduralMesh(
                                                        mesh,
                                                        IncludeUV2,
                                                        IncludeUV3,
                                                        IncludeUV4,
                                                        IncludeColors,
                                                        IncludeTangents,
                                                        IncludeBoneWeights,
                                                        IncludeBindPoses
                                                );
                                                capturedAny = true;
                                        }
                                        else if (RememberMeshReference)
                                        {
                                                // Save mesh asset reference
                                                data.IsProceduralMesh = false;
                                                data.MeshName = baseMeshName;
                                                data.MeshAssetPath = GetMeshAssetPath(mesh);
                                                capturedAny = true;
                                        }
                                }
                        }

                        if (!capturedAny)
                        {
                                return false;
                        }

                        snapshot = data;
                        return true;
                }

                private bool AreEquivalent(SkinnedMeshRendererData a, SkinnedMeshRendererData b)
                {
                        if (ReferenceEquals(a, b)) return true;
                        if (a == null || b == null) return false;

                        if (!SequenceEquals(a.SharedMaterialNames, b.SharedMaterialNames)) return false;
                        if (!UniqueMaterialArraysEqual(a.UniqueMaterialProperties, b.UniqueMaterialProperties)) return false;
                        if (!BlendShapeListsEqual(a.BlendShapeWeights, b.BlendShapeWeights)) return false;

                        if (a.RendererEnabled != b.RendererEnabled) return false;
                        if (a.ShadowCasting != b.ShadowCasting) return false;
                        if (a.ReceiveShadows != b.ReceiveShadows) return false;
                        if (a.LightProbeUsage != b.LightProbeUsage) return false;
                        if (a.ReflectionProbeUsage != b.ReflectionProbeUsage) return false;

                        if (!string.Equals(a.ProbeAnchorName ?? string.Empty, b.ProbeAnchorName ?? string.Empty, StringComparison.Ordinal)) return false;

                        if (a.MotionVectorMode != b.MotionVectorMode) return false;
                        if (a.SortingLayerID != b.SortingLayerID) return false;
                        if (a.SortingOrder != b.SortingOrder) return false;

                        if (!string.Equals(a.RootBoneName ?? string.Empty, b.RootBoneName ?? string.Empty, StringComparison.Ordinal)) return false;

                        if (a.Quality != b.Quality) return false;
                        if (a.UpdateWhenOffscreen != b.UpdateWhenOffscreen) return false;

                        // Mesh reference comparison
                        if (a.IsProceduralMesh != b.IsProceduralMesh) return false;
                        if (!string.Equals(a.MeshName ?? string.Empty, b.MeshName ?? string.Empty, StringComparison.Ordinal)) return false;
                        if (!a.IsProceduralMesh && !string.Equals(a.MeshAssetPath ?? string.Empty, b.MeshAssetPath ?? string.Empty, StringComparison.Ordinal)) return false;
                        // For procedural meshes, assume they're different (would need deep comparison which is expensive)
                        if (a.IsProceduralMesh && a.ProceduralMeshData != null) return false;

                        return true;
                }

                private bool SequenceEquals<T>(T[] first, T[] second)
                {
                        if (ReferenceEquals(first, second)) return true;
                        bool firstEmpty = first == null || first.Length == 0;
                        bool secondEmpty = second == null || second.Length == 0;
                        if (firstEmpty || secondEmpty)
                        {
                                return firstEmpty && secondEmpty;
                        }

                        return first.SequenceEqual(second);
                }

                private bool UniqueMaterialArraysEqual(UniqueMaterialData[] first, UniqueMaterialData[] second)
                {
                        if (ReferenceEquals(first, second)) return true;
                        bool firstEmpty = first == null || first.Length == 0;
                        bool secondEmpty = second == null || second.Length == 0;
                        if (firstEmpty || secondEmpty)
                        {
                                return firstEmpty && secondEmpty;
                        }

                        if (first.Length != second.Length) return false;

                        for (int i = 0; i < first.Length; i++)
                        {
                                var a = first[i];
                                var b = second[i];
                                if (a == null || b == null)
                                {
                                        if (!(a == null && b == null))
                                        {
                                                return false;
                                        }
                                        continue;
                                }

                                if (!string.Equals(a.MaterialName, b.MaterialName, StringComparison.Ordinal)) return false;
                                if (!ColorsApproximatelyEqual(a.Color, b.Color)) return false;
                        }

                        return true;
                }

                private bool BlendShapeListsEqual(List<float> first, List<float> second)
                {
                        if (ReferenceEquals(first, second)) return true;
                        bool firstEmpty = first == null || first.Count == 0;
                        bool secondEmpty = second == null || second.Count == 0;
                        if (firstEmpty || secondEmpty)
                        {
                                return firstEmpty && secondEmpty;
                        }

                        if (first.Count != second.Count) return false;

                        for (int i = 0; i < first.Count; i++)
                        {
                                if (Mathf.Abs(first[i] - second[i]) > FloatTolerance)
                                {
                                        return false;
                                }
                        }

                        return true;
                }

                private bool ColorsApproximatelyEqual(Color a, Color b)
                {
                        return Mathf.Abs(a.r - b.r) <= ColorTolerance &&
                               Mathf.Abs(a.g - b.g) <= ColorTolerance &&
                               Mathf.Abs(a.b - b.b) <= ColorTolerance &&
                               Mathf.Abs(a.a - b.a) <= ColorTolerance;
                }

                private UniqueMaterialData CloneUniqueMaterialData(UniqueMaterialData source)
                {
                        if (source == null)
                        {
                                return null;
                        }

                        return new UniqueMaterialData
                        {
                                MaterialName = source.MaterialName,
                                Color = source.Color
                        };
                }

                private SkinnedMeshRendererData CloneSnapshot(SkinnedMeshRendererData source)
                {
                        if (source == null)
                        {
                                return null;
                        }

                        SkinnedMeshRendererData clone = new SkinnedMeshRendererData
                        {
                                SharedMaterialNames = source.SharedMaterialNames != null ? (string[])source.SharedMaterialNames.Clone() : null,
                                UniqueMaterialProperties = source.UniqueMaterialProperties != null ? source.UniqueMaterialProperties.Select(CloneUniqueMaterialData).ToArray() : null,
                                BlendShapeWeights = source.BlendShapeWeights != null ? new List<float>(source.BlendShapeWeights) : null,
                                RendererEnabled = source.RendererEnabled,
                                ShadowCasting = source.ShadowCasting,
                                ReceiveShadows = source.ReceiveShadows,
                                LightProbeUsage = source.LightProbeUsage,
                                ReflectionProbeUsage = source.ReflectionProbeUsage,
                                ProbeAnchorName = source.ProbeAnchorName,
                                MotionVectorMode = source.MotionVectorMode,
                                SortingLayerID = source.SortingLayerID,
                                SortingOrder = source.SortingOrder,
                                RootBoneName = source.RootBoneName,
                                Quality = source.Quality,
                                UpdateWhenOffscreen = source.UpdateWhenOffscreen,
                                MeshName = source.MeshName,
                                MeshAssetPath = source.MeshAssetPath,
                                IsProceduralMesh = source.IsProceduralMesh,
                                ProceduralMeshData = source.ProceduralMeshData // Note: Reference copy is fine for comparison
                        };

                        return clone;
                }

		protected override void OnEnable()
		{
			base.OnEnable();
			// Any additional initialization if necessary
		}

		protected override void OnDisable()
		{
			base.OnDisable();
			// Any additional cleanup if necessary
		}

		/// <summary>
		/// Attempts to find a Transform in the scene by name.
		/// </summary>
		/// <param name="name">Name of the Transform to find.</param>
		/// <returns>The Transform if found; otherwise, null.</returns>
		private Transform FindTransformInScene(string name)
		{
			// Simple utility method to find a Transform by name
			// Consider making this more robust or using a dictionary for performance
			GameObject foundObj = GameObject.Find(name);
			if (foundObj != null) return foundObj.transform;
			return null;
		}

		/// <summary>
		/// Retrieves the relative resource path of a material by trying various subfolders.
		/// </summary>
		/// <param name="material">The Material to get the path for.</param>
		/// <returns>A relative resource path if found; otherwise, the material�s name.</returns>
		private string GetRelativeResourcePath(Material material)
		{
			if (material == null)
			{
				Logger.Log("GetRelativeResourcePath: Provided material is null.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Warning);
				return string.Empty;
			}

			// Handle "(Instance)" suffix
			string matName = material.name;
			if (matName.EndsWith("(Instance)"))
			{
				matName = matName.Replace("(Instance)", "").Trim();
			}

			// First, try the name as-is in subfolders
			string foundPath = TryFindMaterialInSubfolders(matName, material);
			if (!string.IsNullOrEmpty(foundPath))
			{
				return foundPath;
			}

			// If not found, you could attempt fallback heuristics:
			// For example, try lowercase/uppercase variants, or partial matches if desired.
			// For simplicity, let's just return the material's base name if we can't find it.
			Logger.Log($"GetRelativeResourcePath: Could not find a direct resource path for '{material.name}'. Returning base name.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Info);
			return matName;
		}

		/// <summary>
		/// Attempts to find a Material in the known ResourceSubfolders by checking if 
		/// loading it from that subfolder returns the same Material instance.
		/// </summary>
		/// <param name="baseName">Base material name (without (Instance) suffix).</param>
		/// <param name="originalMaterial">The original material instance.</param>
		/// <returns>The resource path if found, otherwise empty string.</returns>
		private string TryFindMaterialInSubfolders(string baseName, Material originalMaterial)
		{
			foreach (var subfolder in ResourceSubfolders)
			{
				string path = string.IsNullOrEmpty(subfolder) ? baseName : $"{subfolder}/{baseName}";
                                Material loadedMat = AssetProvider.Load<Material>(path);
				// If loadedMat is not null and considered equivalent
				// We assume the resource path is correct. 
				// NOTE: You might want a more robust equivalence check (e.g., shader name match).
				if (loadedMat != null && loadedMat.shader == originalMaterial.shader)
				{
					Logger.Log($"TryFindMaterialInSubfolders: Found '{baseName}' in '{path}'.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Info);
					return path;
				}
			}

			return string.Empty;
		}

		/// <summary>
		/// Loads a material from the Resources folder using caching and more flexible search logic.
		/// </summary>
		/// <param name="matPath">Relative path or name of the material.</param>
		/// <returns>The loaded Material, or null if not found.</returns>
		private Material LoadMaterial(string matPath)
		{
			if (MaterialCache.TryGetValue(matPath, out var cachedMat))
			{
				return cachedMat;
			}

			// Attempt to load directly if matPath might be already a full resource path
                        Material foundMat = AssetProvider.Load<Material>(matPath);
			if (foundMat != null)
			{
				MaterialCache[matPath] = foundMat;
				return foundMat;
			}

			// If direct load failed, we try searching through subfolders by name.
			// This handles the case where matPath might just be a material name and not a full path.
			// Remove (Instance) if present
			string baseName = matPath;
			if (baseName.EndsWith("(Instance)"))
			{
				baseName = baseName.Replace("(Instance)", "").Trim();
			}

			foreach (var subfolder in ResourceSubfolders)
			{
				string candidatePath = string.IsNullOrEmpty(subfolder) ? baseName : $"{subfolder}/{baseName}";
                                foundMat = AssetProvider.Load<Material>(candidatePath);
				if (foundMat != null)
				{
					Logger.Log($"LoadMaterial: Found material '{baseName}' in '{candidatePath}'.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Info);
					MaterialCache[matPath] = foundMat;
					return foundMat;
				}
			}

			// If still not found, log a warning or silently fail
			Logger.Log($"LoadMaterial: Could not find material '{matPath}' in any known subfolder.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Warning);
			return null;
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
			foreach (var subfolder in MeshResourceSubfolders)
			{
				string path = string.IsNullOrEmpty(subfolder) ? baseName : $"{subfolder}/{baseName}";
				Mesh loadedMesh = AssetProvider.Load<Mesh>(path);
				if (loadedMesh != null)
				{
					Logger.Log($"GetMeshAssetPath: Found '{baseName}' in '{path}'.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Info);
					return path;
				}
			}

			// Return base name as fallback
			Logger.Log($"GetMeshAssetPath: Could not find direct resource path for '{mesh.name}'. Returning base name.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Info);
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
			foreach (var subfolder in MeshResourceSubfolders)
			{
				string candidatePath = string.IsNullOrEmpty(subfolder) ? baseName : $"{subfolder}/{baseName}";
				foundMesh = AssetProvider.Load<Mesh>(candidatePath);
				if (foundMesh != null)
				{
					Logger.Log($"LoadMesh: Found mesh '{baseName}' in '{candidatePath}'.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Info);
					MeshCache[meshPath] = foundMesh;
					return foundMesh;
				}
			}

			Logger.Log($"LoadMesh: Could not find mesh '{meshPath}' in any known subfolder.", LogCategory.RememberSkinnedMeshRenderer, LogLevel.Warning);
			return null;
		}
	}

	/// <summary>
	/// Data structure for SkinnedMeshRenderer properties.
	/// </summary>
	[MemoryPackable]
	public partial class SkinnedMeshRendererData
	{
		// Shared Materials by relative path in Resources
		public string[] SharedMaterialNames { get; set; }

		// Unique Material Properties
		public UniqueMaterialData[] UniqueMaterialProperties { get; set; }

		// Blend Shape Weights
		public List<float> BlendShapeWeights { get; set; } = new List<float>();

		// Basic SkinnedMeshRenderer properties
		public bool RendererEnabled { get; set; }
		public ShadowCastingMode ShadowCasting { get; set; }
		public bool ReceiveShadows { get; set; }
		public LightProbeUsage LightProbeUsage { get; set; }
		public ReflectionProbeUsage ReflectionProbeUsage { get; set; }
		public string ProbeAnchorName { get; set; }
		public MotionVectorGenerationMode MotionVectorMode { get; set; }
		public int SortingLayerID { get; set; }
		public int SortingOrder { get; set; }
		public string RootBoneName { get; set; }
		public SkinQuality Quality { get; set; }
		public bool UpdateWhenOffscreen { get; set; }

		// Mesh reference properties
		public string MeshName { get; set; }
		public string MeshAssetPath { get; set; }
		public bool IsProceduralMesh { get; set; }
		public MeshData ProceduralMeshData { get; set; }

		public SkinnedMeshRendererData() { }
	}
}
#endif