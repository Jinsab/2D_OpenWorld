#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arawn.CrystalSave.Runtime
{
	[AddComponentMenu("Crystal Save/Remember Components/Remember MeshRenderer")]
	[DisallowMultipleComponent]
	[RememberTarget(typeof(MeshRenderer))]
	public class RememberMeshRenderer : SaveableComponent
	{
                [Header("Performance")]
                [Tooltip("Enable lightweight caching of the MeshRenderer reference to avoid repeated GetComponent calls.")]
                [SerializeField] private bool enablePerformanceCaching = false;
                [Header("Save Optimization")]
                [SerializeField] private bool skipSavingWhenUnchanged;
		[Header("MeshRenderer Toggles")]
		[Tooltip("Serialize references to shared materials.")]
		public bool RememberSharedMaterials = false;

		[Tooltip("Serialize properties of unique material instances (e.g., color).")]
		public bool RememberUniqueMaterialProperties = false;

		[Header("MeshRenderer Properties Toggles")]
		public bool RememberEnabled = true;
		public bool RememberShadowCastingMode = false;
		public bool RememberReceiveShadows = false;
		public bool RememberLightProbeUsage = false;
		public bool RememberReflectionProbeUsage = false;
		public bool RememberProbeAnchor = false;
		public bool RememberMotionVectorGenerationMode = false;
		public bool RememberSortingLayerID = false;
		public bool RememberSortingOrder = false;

                private MeshRenderer meshRenderer;
                private MeshRendererSnapshot cachedSnapshot;
                private bool hasCachedSnapshot;
                private byte[] cachedSerializedData;
                private const float FloatTolerance = 0.0001f;
                private const float ColorTolerance = 0.001f;

                private struct MeshRendererSnapshot
                {
                        public bool SharedMaterialsCaptured;
                        public string[] SharedMaterialNames;
                        public bool UniqueMaterialPropertiesCaptured;
                        public UniqueMaterialData[] UniqueMaterialProperties;
                        public bool RendererEnabledCaptured;
                        public bool RendererEnabled;
                        public bool ShadowCastingCaptured;
                        public ShadowCastingMode ShadowCasting;
                        public bool ReceiveShadowsCaptured;
                        public bool ReceiveShadows;
                        public bool LightProbeUsageCaptured;
                        public LightProbeUsage LightProbeUsage;
                        public bool ReflectionProbeUsageCaptured;
                        public ReflectionProbeUsage ReflectionProbeUsage;
                        public bool ProbeAnchorCaptured;
                        public string ProbeAnchorName;
                        public bool MotionVectorModeCaptured;
                        public MotionVectorGenerationMode MotionVectorMode;
                        public bool SortingLayerIdCaptured;
                        public int SortingLayerId;
                        public bool SortingOrderCaptured;
                        public int SortingOrder;

                        public MeshRendererSnapshot Clone()
                        {
                                return new MeshRendererSnapshot
                                {
                                        SharedMaterialsCaptured = SharedMaterialsCaptured,
                                        SharedMaterialNames = SharedMaterialNames != null ? (string[])SharedMaterialNames.Clone() : null,
                                        UniqueMaterialPropertiesCaptured = UniqueMaterialPropertiesCaptured,
                                        UniqueMaterialProperties = CloneUniqueMaterialsArray(UniqueMaterialProperties),
                                        RendererEnabledCaptured = RendererEnabledCaptured,
                                        RendererEnabled = RendererEnabled,
                                        ShadowCastingCaptured = ShadowCastingCaptured,
                                        ShadowCasting = ShadowCasting,
                                        ReceiveShadowsCaptured = ReceiveShadowsCaptured,
                                        ReceiveShadows = ReceiveShadows,
                                        LightProbeUsageCaptured = LightProbeUsageCaptured,
                                        LightProbeUsage = LightProbeUsage,
                                        ReflectionProbeUsageCaptured = ReflectionProbeUsageCaptured,
                                        ReflectionProbeUsage = ReflectionProbeUsage,
                                        ProbeAnchorCaptured = ProbeAnchorCaptured,
                                        ProbeAnchorName = ProbeAnchorName,
                                        MotionVectorModeCaptured = MotionVectorModeCaptured,
                                        MotionVectorMode = MotionVectorMode,
                                        SortingLayerIdCaptured = SortingLayerIdCaptured,
                                        SortingLayerId = SortingLayerId,
                                        SortingOrderCaptured = SortingOrderCaptured,
                                        SortingOrder = SortingOrder
                                };
                        }
                }

                private static UniqueMaterialData[] CloneUniqueMaterialsArray(UniqueMaterialData[] source)
                {
                        if (source == null)
                        {
                                return null;
                        }

                        UniqueMaterialData[] clone = new UniqueMaterialData[source.Length];
                        for (int i = 0; i < source.Length; i++)
                        {
                                UniqueMaterialData materialData = source[i];
                                if (materialData == null)
                                {
                                        clone[i] = null;
                                        continue;
                                }

                                clone[i] = new UniqueMaterialData
                                {
                                        MaterialName = materialData.MaterialName,
                                        Color = materialData.Color
                                };
                        }

                        return clone;
                }

		// Static cache to store loaded materials and avoid redundant Resources.Load calls
		private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

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

                protected override void Awake()
                {
                        base.Awake();
                        // Always get the MeshRenderer component, regardless of caching setting
                        meshRenderer = GetComponent<MeshRenderer>();
                        if (meshRenderer == null)
                        {
                                Logger.Log($"RememberMeshRenderer: No MeshRenderer found on '{gameObject.name}'. Disabling component.", LogCategory.RememberMeshRenderer, LogLevel.Warning);
                                enabled = false;
                                hasCachedSnapshot = false;
                                cachedSnapshot = default;
                                return;
                        }

                        if (skipSavingWhenUnchanged && TryCaptureCurrentState(out MeshRendererSnapshot snapshot, false))
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
		/// Serializes the MeshRenderer properties based on toggles.
		/// </summary>
		/// <returns>Serialized byte array of MeshRendererData.</returns>
                protected override byte[] SerializeComponentData()
                {
                        if (!TryCaptureCurrentState(out MeshRendererSnapshot currentSnapshot, true))
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

                        MeshRendererData data = ConvertSnapshotToData(currentSnapshot);

                        try
                        {
                                byte[] serializedData = Serializer.Serialize<MeshRendererData>(data);
                                Logger.Log($"RememberMeshRenderer: Successfully serialized mesh renderer data for '{gameObject.name}'.", LogCategory.RememberMeshRenderer, LogLevel.Info);

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
                                Logger.Log($"RememberMeshRenderer: Serialization error for '{gameObject.name}': {ex.Message}", LogCategory.RememberMeshRenderer, LogLevel.Error);
                                return null;
                        }
                }

                private bool TryCaptureCurrentState(out MeshRendererSnapshot snapshot, bool logWarnings)
                {
                        snapshot = default;

                        MeshRenderer renderer = enablePerformanceCaching ? meshRenderer : GetComponent<MeshRenderer>();

                        if (renderer == null)
                        {
                                if (logWarnings)
                                {
                                        Logger.Log($"SerializeComponentData: No MeshRenderer on '{gameObject.name}'. Skipping serialization.", LogCategory.RememberMeshRenderer, LogLevel.Warning);
                                }

                                return false;
                        }

                        MeshRendererSnapshot tempSnapshot = new MeshRendererSnapshot();
                        bool capturedAny = false;

                        if (RememberSharedMaterials)
                        {
                                Material[] sharedMats = renderer.sharedMaterials;
                                tempSnapshot.SharedMaterialNames = sharedMats.Select(m => m != null ? GetRelativeResourcePath(m) : string.Empty).ToArray();
                                tempSnapshot.SharedMaterialsCaptured = true;
                                capturedAny = true;
                        }

                        if (RememberUniqueMaterialProperties)
                        {
                                Material[] uniqueMats = renderer.materials;
                                List<UniqueMaterialData> uniqueMaterialDataList = new List<UniqueMaterialData>();

                                foreach (Material mat in uniqueMats)
                                {
                                        if (mat != null && mat.name.EndsWith("(Instance)"))
                                        {
                                                UniqueMaterialData uniqueData = new UniqueMaterialData
                                                {
                                                        MaterialName = mat.name.Replace("(Instance)", string.Empty).Trim(),
                                                        Color = mat.HasProperty("_Color") ? mat.color : Color.white
                                                };
                                                uniqueMaterialDataList.Add(uniqueData);
                                        }
                                }

                                tempSnapshot.UniqueMaterialProperties = uniqueMaterialDataList.ToArray();
                                tempSnapshot.UniqueMaterialPropertiesCaptured = true;
                                capturedAny = true;
                        }

                        if (RememberEnabled)
                        {
                                tempSnapshot.RendererEnabled = renderer.enabled;
                                tempSnapshot.RendererEnabledCaptured = true;
                                capturedAny = true;
                        }

                        if (RememberShadowCastingMode)
                        {
                                tempSnapshot.ShadowCasting = renderer.shadowCastingMode;
                                tempSnapshot.ShadowCastingCaptured = true;
                                capturedAny = true;
                        }

                        if (RememberReceiveShadows)
                        {
                                tempSnapshot.ReceiveShadows = renderer.receiveShadows;
                                tempSnapshot.ReceiveShadowsCaptured = true;
                                capturedAny = true;
                        }

                        if (RememberLightProbeUsage)
                        {
                                tempSnapshot.LightProbeUsage = renderer.lightProbeUsage;
                                tempSnapshot.LightProbeUsageCaptured = true;
                                capturedAny = true;
                        }

                        if (RememberReflectionProbeUsage)
                        {
                                tempSnapshot.ReflectionProbeUsage = renderer.reflectionProbeUsage;
                                tempSnapshot.ReflectionProbeUsageCaptured = true;
                                capturedAny = true;
                        }

                        if (RememberProbeAnchor)
                        {
                                tempSnapshot.ProbeAnchorName = renderer.probeAnchor ? renderer.probeAnchor.name : string.Empty;
                                tempSnapshot.ProbeAnchorCaptured = true;
                                capturedAny = true;
                        }

                        if (RememberMotionVectorGenerationMode)
                        {
                                tempSnapshot.MotionVectorMode = renderer.motionVectorGenerationMode;
                                tempSnapshot.MotionVectorModeCaptured = true;
                                capturedAny = true;
                        }

                        if (RememberSortingLayerID)
                        {
                                tempSnapshot.SortingLayerId = renderer.sortingLayerID;
                                tempSnapshot.SortingLayerIdCaptured = true;
                                capturedAny = true;
                        }

                        if (RememberSortingOrder)
                        {
                                tempSnapshot.SortingOrder = renderer.sortingOrder;
                                tempSnapshot.SortingOrderCaptured = true;
                                capturedAny = true;
                        }

                        if (!capturedAny)
                        {
                                snapshot = default;
                                return false;
                        }

                        snapshot = tempSnapshot;
                        return true;
                }

                private MeshRendererData ConvertSnapshotToData(MeshRendererSnapshot snapshot)
                {
                        MeshRendererData data = new MeshRendererData();

                        if (snapshot.SharedMaterialsCaptured)
                        {
                                data.SharedMaterialNames = snapshot.SharedMaterialNames != null ? (string[])snapshot.SharedMaterialNames.Clone() : Array.Empty<string>();
                        }

                        if (snapshot.UniqueMaterialPropertiesCaptured)
                        {
                                data.UniqueMaterialProperties = CloneUniqueMaterialsArray(snapshot.UniqueMaterialProperties) ?? Array.Empty<UniqueMaterialData>();
                        }

                        if (snapshot.RendererEnabledCaptured)
                        {
                                data.RendererEnabled = snapshot.RendererEnabled;
                        }

                        if (snapshot.ShadowCastingCaptured)
                        {
                                data.ShadowCasting = snapshot.ShadowCasting;
                        }

                        if (snapshot.ReceiveShadowsCaptured)
                        {
                                data.ReceiveShadows = snapshot.ReceiveShadows;
                        }

                        if (snapshot.LightProbeUsageCaptured)
                        {
                                data.LightProbeUsage = snapshot.LightProbeUsage;
                        }

                        if (snapshot.ReflectionProbeUsageCaptured)
                        {
                                data.ReflectionProbeUsage = snapshot.ReflectionProbeUsage;
                        }

                        if (snapshot.ProbeAnchorCaptured)
                        {
                                data.ProbeAnchorName = snapshot.ProbeAnchorName;
                        }

                        if (snapshot.MotionVectorModeCaptured)
                        {
                                data.MotionVectorMode = snapshot.MotionVectorMode;
                        }

                        if (snapshot.SortingLayerIdCaptured)
                        {
                                data.SortingLayerID = snapshot.SortingLayerId;
                        }

                        if (snapshot.SortingOrderCaptured)
                        {
                                data.SortingOrder = snapshot.SortingOrder;
                        }

                        return data;
                }

                private bool AreEquivalent(MeshRendererSnapshot cached, MeshRendererSnapshot current)
                {
                        if (cached.SharedMaterialsCaptured != current.SharedMaterialsCaptured)
                        {
                                return false;
                        }

                        if (cached.SharedMaterialsCaptured && !AreStringArraysEquivalent(cached.SharedMaterialNames, current.SharedMaterialNames))
                        {
                                return false;
                        }

                        if (cached.UniqueMaterialPropertiesCaptured != current.UniqueMaterialPropertiesCaptured)
                        {
                                return false;
                        }

                        if (cached.UniqueMaterialPropertiesCaptured && !AreUniqueMaterialArraysEquivalent(cached.UniqueMaterialProperties, current.UniqueMaterialProperties))
                        {
                                return false;
                        }

                        if (cached.RendererEnabledCaptured != current.RendererEnabledCaptured)
                        {
                                return false;
                        }

                        if (cached.RendererEnabledCaptured && cached.RendererEnabled != current.RendererEnabled)
                        {
                                return false;
                        }

                        if (cached.ShadowCastingCaptured != current.ShadowCastingCaptured)
                        {
                                return false;
                        }

                        if (cached.ShadowCastingCaptured && cached.ShadowCasting != current.ShadowCasting)
                        {
                                return false;
                        }

                        if (cached.ReceiveShadowsCaptured != current.ReceiveShadowsCaptured)
                        {
                                return false;
                        }

                        if (cached.ReceiveShadowsCaptured && cached.ReceiveShadows != current.ReceiveShadows)
                        {
                                return false;
                        }

                        if (cached.LightProbeUsageCaptured != current.LightProbeUsageCaptured)
                        {
                                return false;
                        }

                        if (cached.LightProbeUsageCaptured && cached.LightProbeUsage != current.LightProbeUsage)
                        {
                                return false;
                        }

                        if (cached.ReflectionProbeUsageCaptured != current.ReflectionProbeUsageCaptured)
                        {
                                return false;
                        }

                        if (cached.ReflectionProbeUsageCaptured && cached.ReflectionProbeUsage != current.ReflectionProbeUsage)
                        {
                                return false;
                        }

                        if (cached.ProbeAnchorCaptured != current.ProbeAnchorCaptured)
                        {
                                return false;
                        }

                        if (cached.ProbeAnchorCaptured && !string.Equals(cached.ProbeAnchorName, current.ProbeAnchorName, StringComparison.Ordinal))
                        {
                                return false;
                        }

                        if (cached.MotionVectorModeCaptured != current.MotionVectorModeCaptured)
                        {
                                return false;
                        }

                        if (cached.MotionVectorModeCaptured && cached.MotionVectorMode != current.MotionVectorMode)
                        {
                                return false;
                        }

                        if (cached.SortingLayerIdCaptured != current.SortingLayerIdCaptured)
                        {
                                return false;
                        }

                        if (cached.SortingLayerIdCaptured && cached.SortingLayerId != current.SortingLayerId)
                        {
                                return false;
                        }

                        if (cached.SortingOrderCaptured != current.SortingOrderCaptured)
                        {
                                return false;
                        }

                        if (cached.SortingOrderCaptured && cached.SortingOrder != current.SortingOrder)
                        {
                                return false;
                        }

                        return true;
                }

                private static bool AreStringArraysEquivalent(string[] first, string[] second)
                {
                        if (ReferenceEquals(first, second))
                        {
                                return true;
                        }

                        if (first == null || second == null)
                        {
                                return false;
                        }

                        if (first.Length != second.Length)
                        {
                                return false;
                        }

                        for (int i = 0; i < first.Length; i++)
                        {
                                if (!string.Equals(first[i], second[i], StringComparison.Ordinal))
                                {
                                        return false;
                                }
                        }

                        return true;
                }

                private static bool AreUniqueMaterialArraysEquivalent(UniqueMaterialData[] first, UniqueMaterialData[] second)
                {
                        if (ReferenceEquals(first, second))
                        {
                                return true;
                        }

                        if (first == null || second == null)
                        {
                                return false;
                        }

                        if (first.Length != second.Length)
                        {
                                return false;
                        }

                        for (int i = 0; i < first.Length; i++)
                        {
                                UniqueMaterialData firstData = first[i];
                                UniqueMaterialData secondData = second[i];

                                if (firstData == null && secondData == null)
                                {
                                        continue;
                                }

                                if (firstData == null || secondData == null)
                                {
                                        return false;
                                }

                                if (!string.Equals(firstData.MaterialName, secondData.MaterialName, StringComparison.Ordinal))
                                {
                                        return false;
                                }

                                if (!ColorsApproximatelyEqual(firstData.Color, secondData.Color))
                                {
                                        return false;
                                }
                        }

                        return true;
                }

                private static bool ColorsApproximatelyEqual(Color first, Color second)
                {
                        return ChannelApproximatelyEqual(first.r, second.r)
                               && ChannelApproximatelyEqual(first.g, second.g)
                               && ChannelApproximatelyEqual(first.b, second.b)
                               && ChannelApproximatelyEqual(first.a, second.a);
                }

                private static bool ApproximatelyEqual(float first, float second)
                {
                        return Mathf.Approximately(first, second) || Mathf.Abs(first - second) <= FloatTolerance;
                }

                private static bool ChannelApproximatelyEqual(float first, float second)
                {
                        return ApproximatelyEqual(first, second) || Mathf.Abs(first - second) <= ColorTolerance;
                }

		/// <summary>
		/// Deserializes and applies the MeshRenderer properties based on toggles.
		/// </summary>
		/// <param name="data">Serialized byte array of MeshRendererData.</param>
		protected override void DeserializeComponentData(byte[] data)
		{
                        // Use cached reference if caching enabled, otherwise get component each time
                        MeshRenderer renderer = enablePerformanceCaching ? meshRenderer : GetComponent<MeshRenderer>();

                        if (renderer == null)
                        {
                                Logger.Log($"DeserializeComponentData: No MeshRenderer on '{gameObject.name}'. Skipping deserialization.", LogCategory.RememberMeshRenderer, LogLevel.Warning);
                                return;
                        }

			if (data == null || data.Length == 0)
			{
				Logger.Log("DeserializeComponentData failed: data is null or empty.", LogCategory.RememberMeshRenderer, LogLevel.Warning);
				return;
			}

			try
			{
				MeshRendererData deserializedData = Serializer.Deserialize<MeshRendererData>(data);
				if (deserializedData == null)
				{
					Logger.Log("DeserializeComponentData failed: deserialized data is null.", LogCategory.RememberMeshRenderer, LogLevel.Warning);
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
								Logger.Log($"RememberMeshRenderer: Could not find shared material '{matPath}' in Resources for '{gameObject.name}'. Assigning default material.", LogCategory.RememberMeshRenderer, LogLevel.Warning);
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
							Logger.Log($"RememberMeshRenderer: Could not find unique material '{uniqueData.MaterialName}' on '{gameObject.name}'.", LogCategory.RememberMeshRenderer, LogLevel.Warning);
						}
					}
				}

				// Deserialize Other MeshRenderer Properties
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
						Logger.Log($"RememberMeshRenderer: Could not find probe anchor '{deserializedData.ProbeAnchorName}' in scene for '{gameObject.name}'.", LogCategory.RememberMeshRenderer, LogLevel.Warning);
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

                                if (skipSavingWhenUnchanged)
                                {
                                        if (TryCaptureCurrentState(out MeshRendererSnapshot snapshot, false))
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

                                Logger.Log($"RememberMeshRenderer: Successfully loaded mesh renderer data for '{gameObject.name}'.", LogCategory.RememberMeshRenderer, LogLevel.Info);
			}
			catch (Exception ex)
			{
				Logger.Log($"RememberMeshRenderer: DeserializeComponentData encountered an error: {ex.Message}", LogCategory.RememberMeshRenderer, LogLevel.Error);
			}
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			// Additional enable logic if needed
		}

		protected override void OnDisable()
		{
			base.OnDisable();
			// Additional disable logic if needed
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
				Logger.Log("GetRelativeResourcePath: Provided material is null.", LogCategory.RememberMeshRenderer, LogLevel.Warning);
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
			Logger.Log($"GetRelativeResourcePath: Could not find a direct resource path for '{material.name}'. Returning base name.", LogCategory.RememberMeshRenderer, LogLevel.Info);
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
				// NOTE: You might want a more robust equivalence check (e.g. shader name match).
				if (loadedMat != null && loadedMat.shader == originalMaterial.shader)
				{
					Logger.Log($"TryFindMaterialInSubfolders: Found '{baseName}' in '{path}'.", LogCategory.RememberMeshRenderer, LogLevel.Info);
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
					Logger.Log($"LoadMaterial: Found material '{baseName}' in '{candidatePath}'.", LogCategory.RememberMeshRenderer, LogLevel.Info);
					MaterialCache[matPath] = foundMat;
					return foundMat;
				}
			}

			// If still not found, log a warning or silently fail
			Logger.Log($"LoadMaterial: Could not find material '{matPath}' in any known subfolder.", LogCategory.RememberMeshRenderer, LogLevel.Warning);
			return null;
		}
	}

	/// <summary>
	/// Data structure for MeshRenderer properties.
	/// </summary>
	[MemoryPackable]
	public partial class MeshRendererData
	{
		// Shared Materials by relative path in Resources
		public string[] SharedMaterialNames { get; set; }

		// Unique Material Properties
		public UniqueMaterialData[] UniqueMaterialProperties { get; set; }

		// Basic MeshRenderer properties
		public bool RendererEnabled { get; set; }
		public ShadowCastingMode ShadowCasting { get; set; }
		public bool ReceiveShadows { get; set; }
		public LightProbeUsage LightProbeUsage { get; set; }
		public ReflectionProbeUsage ReflectionProbeUsage { get; set; }
		public string ProbeAnchorName { get; set; }
		public MotionVectorGenerationMode MotionVectorMode { get; set; }
		public int SortingLayerID { get; set; }
		public int SortingOrder { get; set; }

		public MeshRendererData() { }
	}
}
#endif