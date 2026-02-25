// RememberMaterial.cs
#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	[AddComponentMenu("Crystal Save/Remember Components/Remember Material")]
	[DisallowMultipleComponent]
	[RememberTarget(typeof(Renderer))]
	[RememberIcon("Material Icon")]
	public class RememberMaterial : SaveableComponent
	{
		[Header("Performance")]
		[Tooltip("Enable lightweight caching of the Renderer reference to avoid repeated GetComponent calls.")]
		[SerializeField] private bool enablePerformanceCaching = false;

		[Header("Save Optimization")]
		[SerializeField] private bool skipSavingWhenUnchanged;

		[Header("Material Tracking")]
		[Tooltip("Track all materials on the Renderer. If disabled, only track materials specified in Material Indices.")]
		[SerializeField] private bool trackAllMaterials = true;

		[Tooltip("Specific material indices to track (only used when Track All Materials is disabled). Leave empty to track none.")]
		[SerializeField] private List<int> materialIndices = new List<int> { 0 };

		[Header("Material Property Toggles")]
		[Tooltip("Remember the main color (_Color property).")]
		public bool RememberColor = true;

		[Tooltip("Remember the main texture (_MainTex property).")]
		public bool RememberMainTexture = true;

		[Tooltip("Remember additional textures (normal maps, metallic maps, etc.).")]
		public bool RememberAdditionalTextures = true;

		[Tooltip("Remember float properties (e.g., _Metallic, _Glossiness, _BumpScale).")]
		public bool RememberFloatProperties = true;

		[Tooltip("Remember vector properties (e.g., tiling and offset).")]
		public bool RememberVectorProperties = true;

		[Tooltip("Remember the shader reference.")]
		public bool RememberShader = false;

		[Tooltip("Remember render queue.")]
		public bool RememberRenderQueue = false;

		[Tooltip("Remember shader keywords.")]
		public bool RememberShaderKeywords = false;

		private Renderer targetRenderer;
		private Dictionary<int, MaterialSnapshot> cachedSnapshots; // Changed to support multiple materials
		private bool hasCachedSnapshots;
		private byte[] cachedSerializedData;

		// Public properties for inspector/editor
		public bool TrackAllMaterials => trackAllMaterials;
		public List<int> MaterialIndices => materialIndices;

		private const float FloatTolerance = 0.0001f;
		private const float ColorTolerance = 0.001f;

		// Common property names to serialize
		private static readonly string[] CommonFloatProperties = new string[]
		{
			"_Metallic",
			"_Glossiness",
			"_Smoothness",
			"_BumpScale",
			"_OcclusionStrength",
			"_Parallax",
			"_DetailNormalMapScale",
			"_Cutoff",
			"_Mode",
			"_SrcBlend",
			"_DstBlend",
			"_ZWrite"
		};

		private static readonly string[] CommonVectorProperties = new string[]
		{
			"_MainTex",      // Tiling and Offset
			"_BumpMap",
			"_EmissionMap",
			"_MetallicGlossMap",
			"_OcclusionMap",
			"_ParallaxMap",
			"_DetailMask",
			"_DetailAlbedoMap",
			"_DetailNormalMap"
		};

		private static readonly string[] CommonTextureProperties = new string[]
		{
			"_MainTex",
			"_BumpMap",
			"_EmissionMap",
			"_MetallicGlossMap",
			"_OcclusionMap",
			"_ParallaxMap",
			"_DetailMask",
			"_DetailAlbedoMap",
			"_DetailNormalMap"
		};

		// Static cache to store loaded textures and materials
		private static readonly Dictionary<string, Texture> TextureCache = new Dictionary<string, Texture>();
		private static readonly Dictionary<string, Shader> ShaderCache = new Dictionary<string, Shader>();

		// List of potential subfolder names within Resources
		private static readonly List<string> ResourceSubfolders = new List<string>
		{
			"",
			"Textures",
			"Texture",
			"Materials",
			"Mats",
			"Mat",
			"Shaders",
			"textures",
			"texture",
			"materials",
			"mats",
			"mat",
			"shaders"
		};

		private struct MaterialSnapshot
		{
			public bool ColorCaptured;
			public Color Color;
			public bool MainTextureCaptured;
			public string MainTexturePath;
			public int MainTextureInstanceID; // For material instances with non-Resources textures
			public bool AdditionalTexturesCaptured;
			public SerializedTexture[] AdditionalTextures;
			public bool FloatPropertiesCaptured;
			public SerializedFloat[] FloatProperties;
			public bool VectorPropertiesCaptured;
			public SerializedVector[] VectorProperties;
			public bool ShaderCaptured;
			public string ShaderName;
			public bool RenderQueueCaptured;
			public int RenderQueue;
			public bool ShaderKeywordsCaptured;
			public string[] ShaderKeywords;

			public MaterialSnapshot Clone()
			{
				return new MaterialSnapshot
				{
					ColorCaptured = ColorCaptured,
					Color = Color,
					MainTextureCaptured = MainTextureCaptured,
					MainTexturePath = MainTexturePath,
					MainTextureInstanceID = MainTextureInstanceID,
					AdditionalTexturesCaptured = AdditionalTexturesCaptured,
					AdditionalTextures = AdditionalTextures != null ? AdditionalTextures.Select(t => t.Clone()).ToArray() : null,
					FloatPropertiesCaptured = FloatPropertiesCaptured,
					FloatProperties = FloatProperties != null ? FloatProperties.Select(f => f.Clone()).ToArray() : null,
					VectorPropertiesCaptured = VectorPropertiesCaptured,
					VectorProperties = VectorProperties != null ? VectorProperties.Select(v => v.Clone()).ToArray() : null,
					ShaderCaptured = ShaderCaptured,
					ShaderName = ShaderName,
					RenderQueueCaptured = RenderQueueCaptured,
					RenderQueue = RenderQueue,
					ShaderKeywordsCaptured = ShaderKeywordsCaptured,
					ShaderKeywords = ShaderKeywords != null ? (string[])ShaderKeywords.Clone() : null
				};
			}
		}

		protected override void Awake()
		{
			base.Awake();
			targetRenderer = GetComponent<Renderer>();
			if (targetRenderer == null)
			{
				Logger.Log($"RememberMaterial: No Renderer found on '{gameObject.name}'. Disabling component.", LogCategory.RememberMaterial, LogLevel.Warning);
				enabled = false;
				hasCachedSnapshots = false;
				cachedSnapshots = null;
				return;
			}

			if (skipSavingWhenUnchanged)
			{
				cachedSnapshots = new Dictionary<int, MaterialSnapshot>();
				var indices = GetMaterialIndicesToTrack();
				
				foreach (int index in indices)
				{
					if (TryCaptureCurrentStateForIndex(index, out MaterialSnapshot snapshot, false))
					{
						cachedSnapshots[index] = snapshot.Clone();
					}
				}
				
				hasCachedSnapshots = cachedSnapshots.Count > 0;
			}
			else
			{
				cachedSnapshots = null;
				hasCachedSnapshots = false;
			}
		}

		/// <summary>
		/// Gets the list of material indices to track based on settings.
		/// </summary>
		private List<int> GetMaterialIndicesToTrack()
		{
			Renderer renderer = enablePerformanceCaching ? targetRenderer : GetComponent<Renderer>();
			if (renderer == null) return new List<int>();

			Material[] materials = renderer.sharedMaterials;
			if (materials == null || materials.Length == 0) return new List<int>();

			if (trackAllMaterials)
			{
				// Return all valid indices
				return Enumerable.Range(0, materials.Length).ToList();
			}
			else
			{
				// Return only specified indices that are within range
				return materialIndices
					.Where(i => i >= 0 && i < materials.Length)
					.Distinct()
					.ToList();
			}
		}

		protected override byte[] SerializeComponentData()
		{
			Renderer renderer = enablePerformanceCaching ? targetRenderer : GetComponent<Renderer>();
			if (renderer == null)
			{
				Logger.Log($"SerializeComponentData: No Renderer on '{gameObject.name}'. Skipping serialization.", LogCategory.RememberMaterial, LogLevel.Warning);
				return null;
			}

			var indices = GetMaterialIndicesToTrack();
			if (indices.Count == 0)
			{
				Logger.Log($"RememberMaterial: No materials to track on '{gameObject.name}'.", LogCategory.RememberMaterial, LogLevel.Info);
				return null;
			}

			// Capture snapshots for all tracked materials
			var currentSnapshots = new Dictionary<int, MaterialSnapshot>();
			foreach (int index in indices)
			{
				if (TryCaptureCurrentStateForIndex(index, out MaterialSnapshot snapshot, true))
				{
					currentSnapshots[index] = snapshot;
				}
			}

			if (currentSnapshots.Count == 0)
			{
				if (skipSavingWhenUnchanged)
				{
					cachedSnapshots?.Clear();
					hasCachedSnapshots = false;
				}
				return null;
			}

			// Check if unchanged (Skip Saving When Unchanged)
			if (skipSavingWhenUnchanged && hasCachedSnapshots && cachedSnapshots != null)
			{
				bool allUnchanged = true;
				foreach (var kvp in currentSnapshots)
				{
					if (!cachedSnapshots.TryGetValue(kvp.Key, out var cachedSnapshot) ||
					    !AreEquivalent(cachedSnapshot, kvp.Value))
					{
						allUnchanged = false;
						break;
					}
				}

				if (allUnchanged && currentSnapshots.Count == cachedSnapshots.Count)
				{
					Logger.Log($"RememberMaterial: Skipping serialization for '{gameObject.name}' - no changes detected.", LogCategory.RememberMaterial, LogLevel.Info);
					if (cachedSerializedData != null && cachedSerializedData.Length > 0)
					{
						return cachedSerializedData;
					}
				}
			}

			// Convert snapshots to data
			MaterialData data = new MaterialData
			{
				Materials = new Dictionary<int, SingleMaterialData>()
			};

			foreach (var kvp in currentSnapshots)
			{
				data.Materials[kvp.Key] = ConvertSnapshotToSingleMaterialData(kvp.Value);
			}

			try
			{
				byte[] serializedData = Serializer.Serialize<MaterialData>(data);
				Logger.Log($"RememberMaterial: Successfully serialized {currentSnapshots.Count} material(s) for '{gameObject.name}'.", LogCategory.RememberMaterial, LogLevel.Info);

				if (skipSavingWhenUnchanged)
				{
					cachedSnapshots = new Dictionary<int, MaterialSnapshot>();
					foreach (var kvp in currentSnapshots)
					{
						cachedSnapshots[kvp.Key] = kvp.Value.Clone();
					}
					hasCachedSnapshots = true;
					cachedSerializedData = serializedData;
				}

				return serializedData;
			}
			catch (Exception ex)
			{
				Logger.Log($"RememberMaterial: Serialization error for '{gameObject.name}': {ex.Message}", LogCategory.RememberMaterial, LogLevel.Error);
				return null;
			}
		}

		/// <summary>
		/// Captures the current state of a specific material by index.
		/// </summary>
		private bool TryCaptureCurrentStateForIndex(int matIndex, out MaterialSnapshot snapshot, bool logWarnings)
		{
			snapshot = default;

			Renderer renderer = enablePerformanceCaching ? targetRenderer : GetComponent<Renderer>();

			if (renderer == null)
			{
				if (logWarnings)
				{
					Logger.Log($"TryCaptureCurrentStateForIndex: No Renderer on '{gameObject.name}'. Skipping.", LogCategory.RememberMaterial, LogLevel.Warning);
				}
				return false;
			}

			Material[] materials = renderer.sharedMaterials;
			if (matIndex < 0 || matIndex >= materials.Length)
			{
				if (logWarnings)
				{
					Logger.Log($"TryCaptureCurrentStateForIndex: Material index {matIndex} out of range for '{gameObject.name}'. Material count: {materials.Length}", LogCategory.RememberMaterial, LogLevel.Warning);
				}
				return false;
			}

			Material material = materials[matIndex];
			if (material == null)
			{
				if (logWarnings)
				{
					Logger.Log($"TryCaptureCurrentStateForIndex: Material at index {matIndex} is null on '{gameObject.name}'.", LogCategory.RememberMaterial, LogLevel.Warning);
				}
				return false;
			}

			MaterialSnapshot tempSnapshot = new MaterialSnapshot();
			bool capturedAny = false;

			// Capture Color
			if (RememberColor && material.HasProperty("_Color"))
			{
				tempSnapshot.Color = material.color;
				tempSnapshot.ColorCaptured = true;
				capturedAny = true;
			}

			// Capture Main Texture
		if (RememberMainTexture && material.HasProperty("_MainTex"))
		{
			Texture mainTex = material.mainTexture;
			tempSnapshot.MainTexturePath = mainTex != null ? GetRelativeResourcePath(mainTex) : string.Empty;
			tempSnapshot.MainTextureInstanceID = mainTex != null ? UnityObjectHelper.GetUniqueId(mainTex) : 0;
			tempSnapshot.MainTextureCaptured = true;
			capturedAny = true;
		}

		// Capture Additional Textures
		if (RememberAdditionalTextures)
		{
			List<SerializedTexture> textureList = new List<SerializedTexture>();
			foreach (var propName in CommonTextureProperties)
			{
				if (propName == "_MainTex") continue; // Already handled

				if (material.HasProperty(propName))
				{
					Texture tex = material.GetTexture(propName);
					if (tex != null)
					{
						textureList.Add(new SerializedTexture
						{
							PropertyName = propName,
							TexturePath = GetRelativeResourcePath(tex),
							TextureInstanceID = UnityObjectHelper.GetUniqueId(tex)
						});
					}
				}
			}
			tempSnapshot.AdditionalTextures = textureList.ToArray();
			tempSnapshot.AdditionalTexturesCaptured = true;
			capturedAny = true;
		}			// Capture Float Properties
			if (RememberFloatProperties)
			{
				List<SerializedFloat> floatList = new List<SerializedFloat>();
				foreach (var propName in CommonFloatProperties)
				{
					if (material.HasProperty(propName))
					{
						floatList.Add(new SerializedFloat
						{
							PropertyName = propName,
							Value = material.GetFloat(propName)
						});
					}
				}
				tempSnapshot.FloatProperties = floatList.ToArray();
				tempSnapshot.FloatPropertiesCaptured = true;
				capturedAny = true;
			}

			// Capture Vector Properties (including texture tiling and offset)
			if (RememberVectorProperties)
			{
				List<SerializedVector> vectorList = new List<SerializedVector>();
				
				// Capture texture scale and offset for common textures
				foreach (var propName in CommonVectorProperties)
				{
					if (material.HasProperty(propName))
					{
						Vector2 scale = material.GetTextureScale(propName);
						Vector2 offset = material.GetTextureOffset(propName);
						vectorList.Add(new SerializedVector
						{
							PropertyName = propName + "_ST",
							Value = new Vector4(scale.x, scale.y, offset.x, offset.y)
						});
					}
				}

				tempSnapshot.VectorProperties = vectorList.ToArray();
				tempSnapshot.VectorPropertiesCaptured = true;
				capturedAny = true;
			}

			// Capture Shader
			if (RememberShader && material.shader != null)
			{
				tempSnapshot.ShaderName = material.shader.name;
				tempSnapshot.ShaderCaptured = true;
				capturedAny = true;
			}

			// Capture Render Queue
			if (RememberRenderQueue)
			{
				tempSnapshot.RenderQueue = material.renderQueue;
				tempSnapshot.RenderQueueCaptured = true;
				capturedAny = true;
			}

			// Capture Shader Keywords
			if (RememberShaderKeywords)
			{
				tempSnapshot.ShaderKeywords = material.shaderKeywords;
				tempSnapshot.ShaderKeywordsCaptured = true;
				capturedAny = true;
			}

			if (!capturedAny)
			{
				if (logWarnings)
				{
					Logger.Log($"TryCaptureCurrentStateForIndex: No material properties were captured for index {matIndex} on '{gameObject.name}'.", LogCategory.RememberMaterial, LogLevel.Warning);
				}
				return false;
			}

			snapshot = tempSnapshot;
			return true;
		}

		/// <summary>
		/// Converts a MaterialSnapshot to SingleMaterialData for serialization.
		/// </summary>
		private SingleMaterialData ConvertSnapshotToSingleMaterialData(MaterialSnapshot snapshot)
		{
			return new SingleMaterialData
			{
				Color = snapshot.Color,
				MainTexturePath = snapshot.MainTexturePath ?? string.Empty,
				MainTextureInstanceID = snapshot.MainTextureInstanceID,
				AdditionalTextures = snapshot.AdditionalTextures ?? Array.Empty<SerializedTexture>(),
				FloatProperties = snapshot.FloatProperties ?? Array.Empty<SerializedFloat>(),
				VectorProperties = snapshot.VectorProperties ?? Array.Empty<SerializedVector>(),
				ShaderName = snapshot.ShaderName ?? string.Empty,
				RenderQueue = snapshot.RenderQueue,
				ShaderKeywords = snapshot.ShaderKeywords ?? Array.Empty<string>()
			};
		}

		protected override void DeserializeComponentData(byte[] binaryData)
		{
			Renderer renderer = enablePerformanceCaching ? targetRenderer : GetComponent<Renderer>();

			if (renderer == null)
			{
				Logger.Log($"DeserializeComponentData: No Renderer on '{gameObject.name}'. Skipping deserialization.", LogCategory.RememberMaterial, LogLevel.Warning);
				return;
			}

			if (binaryData == null || binaryData.Length == 0)
			{
				Logger.Log("DeserializeComponentData failed: binaryData is null or empty.", LogCategory.RememberMaterial, LogLevel.Warning);
				return;
			}

			try
			{
				MaterialData deserializedData = Serializer.Deserialize<MaterialData>(binaryData);
				if (deserializedData == null)
				{
					Logger.Log("DeserializeComponentData failed: deserialized data is null.", LogCategory.RememberMaterial, LogLevel.Warning);
					return;
				}

				Material[] materials = renderer.materials;

				// Check if this is legacy single-material data
				if (deserializedData.Materials == null || deserializedData.Materials.Count == 0)
				{
					// Backward compatibility: restore old single-material format to index 0
					Logger.Log($"RememberMaterial: Loading legacy single-material data for '{gameObject.name}'.", LogCategory.RememberMaterial, LogLevel.Info);
					RestoreSingleMaterial(materials, 0, deserializedData, renderer);
				}
				else
				{
					// New multi-material format: loop through Materials dictionary
					foreach (var kvp in deserializedData.Materials)
					{
						int matIndex = kvp.Key;
						SingleMaterialData singleData = kvp.Value;
						
						// Convert SingleMaterialData back to MaterialData format for restoration
						MaterialData legacyData = new MaterialData
						{
							Color = singleData.Color,
							MainTexturePath = singleData.MainTexturePath,
							MainTextureInstanceID = singleData.MainTextureInstanceID,
							AdditionalTextures = singleData.AdditionalTextures,
							FloatProperties = singleData.FloatProperties,
							VectorProperties = singleData.VectorProperties,
							ShaderName = singleData.ShaderName,
							RenderQueue = singleData.RenderQueue,
							ShaderKeywords = singleData.ShaderKeywords
						};

						RestoreSingleMaterial(materials, matIndex, legacyData, renderer);
					}
				}

				// Update the materials array to apply all changes
				renderer.materials = materials;

				Logger.Log($"RememberMaterial: Successfully loaded material data for '{gameObject.name}'.", LogCategory.RememberMaterial, LogLevel.Info);

				// Update cached snapshots for "Skip Saving When Unchanged"
				if (skipSavingWhenUnchanged)
				{
					cachedSnapshots = new Dictionary<int, MaterialSnapshot>();
					hasCachedSnapshots = false;

					List<int> indicesToTrack = GetMaterialIndicesToTrack();
					foreach (int matIndex in indicesToTrack)
					{
						if (TryCaptureCurrentStateForIndex(matIndex, out MaterialSnapshot newSnapshot, false))
						{
							cachedSnapshots[matIndex] = newSnapshot.Clone();
							hasCachedSnapshots = true;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.Log($"RememberMaterial: DeserializeComponentData encountered an error: {ex.Message}", LogCategory.RememberMaterial, LogLevel.Error);
			}
		}

		/// <summary>
		/// Restores a single material at the specified index from deserialized data.
		/// </summary>
		private void RestoreSingleMaterial(Material[] materials, int matIndex, MaterialData data, Renderer renderer)
		{
			if (matIndex < 0 || matIndex >= materials.Length)
			{
				Logger.Log($"RestoreSingleMaterial: Material index {matIndex} out of range for '{gameObject.name}'. Material count: {materials.Length}", LogCategory.RememberMaterial, LogLevel.Warning);
				return;
			}

			Material targetMaterial = materials[matIndex];
			if (targetMaterial == null)
			{
				Logger.Log($"RestoreSingleMaterial: Material at index {matIndex} is null on '{gameObject.name}'.", LogCategory.RememberMaterial, LogLevel.Warning);
				return;
			}

			// Apply Shader first (if remembered) so properties can be set
			if (RememberShader && !string.IsNullOrEmpty(data.ShaderName))
			{
				Shader shader = LoadShader(data.ShaderName);
				if (shader != null)
				{
					targetMaterial.shader = shader;
				}
				else
				{
					Logger.Log($"RestoreSingleMaterial: Could not find shader '{data.ShaderName}' for '{gameObject.name}'.", LogCategory.RememberMaterial, LogLevel.Warning);
				}
			}

			// Apply Color
			if (RememberColor && targetMaterial.HasProperty("_Color"))
			{
				targetMaterial.color = data.Color;
			}

			// Apply Main Texture
			if (RememberMainTexture && !string.IsNullOrEmpty(data.MainTexturePath))
			{
				Texture mainTex = LoadTexture(data.MainTexturePath, data.MainTextureInstanceID);
				if (mainTex != null && targetMaterial.HasProperty("_MainTex"))
				{
					targetMaterial.mainTexture = mainTex;
				}
				else if (mainTex == null)
				{
					Logger.Log($"RestoreSingleMaterial: Could not find main texture '{data.MainTexturePath}' for '{gameObject.name}'.", LogCategory.RememberMaterial, LogLevel.Warning);
				}
			}

			// Apply Additional Textures
			if (RememberAdditionalTextures && data.AdditionalTextures != null)
			{
				foreach (var texData in data.AdditionalTextures)
				{
					if (string.IsNullOrEmpty(texData.PropertyName) || string.IsNullOrEmpty(texData.TexturePath))
						continue;

					if (targetMaterial.HasProperty(texData.PropertyName))
					{
						Texture tex = LoadTexture(texData.TexturePath, texData.TextureInstanceID);
						if (tex != null)
						{
							targetMaterial.SetTexture(texData.PropertyName, tex);
						}
						else
						{
							Logger.Log($"RestoreSingleMaterial: Could not find texture '{texData.TexturePath}' for property '{texData.PropertyName}' on '{gameObject.name}'.", LogCategory.RememberMaterial, LogLevel.Warning);
						}
					}
				}
			}

			// Apply Float Properties
			if (RememberFloatProperties && data.FloatProperties != null)
			{
				foreach (var floatData in data.FloatProperties)
				{
					if (string.IsNullOrEmpty(floatData.PropertyName))
						continue;

					if (targetMaterial.HasProperty(floatData.PropertyName))
					{
						targetMaterial.SetFloat(floatData.PropertyName, floatData.Value);
					}
				}
			}

			// Apply Vector Properties (including tiling and offset)
			if (RememberVectorProperties && data.VectorProperties != null)
			{
				foreach (var vecData in data.VectorProperties)
				{
					if (string.IsNullOrEmpty(vecData.PropertyName))
						continue;

					// Handle texture scale and offset separately
					if (vecData.PropertyName.EndsWith("_ST"))
					{
						string textureName = vecData.PropertyName.Replace("_ST", "");
						if (targetMaterial.HasProperty(textureName))
						{
							targetMaterial.SetTextureScale(textureName, new Vector2(vecData.Value.x, vecData.Value.y));
							targetMaterial.SetTextureOffset(textureName, new Vector2(vecData.Value.z, vecData.Value.w));
						}
					}
					else if (targetMaterial.HasProperty(vecData.PropertyName))
					{
						targetMaterial.SetVector(vecData.PropertyName, vecData.Value);
					}
				}
			}

			// Apply Render Queue
			if (RememberRenderQueue)
			{
				targetMaterial.renderQueue = data.RenderQueue;
			}

			// Apply Shader Keywords
			if (RememberShaderKeywords && data.ShaderKeywords != null)
			{
				foreach (var keyword in data.ShaderKeywords)
				{
					if (!string.IsNullOrEmpty(keyword))
					{
						targetMaterial.EnableKeyword(keyword);
					}
				}
			}
		}

		private bool AreEquivalent(MaterialSnapshot a, MaterialSnapshot b)
		{
			if (a.ColorCaptured != b.ColorCaptured) return false;
			if (a.ColorCaptured && !ColorsApproximatelyEqual(a.Color, b.Color)) return false;

			if (a.MainTextureCaptured != b.MainTextureCaptured) return false;
			if (a.MainTextureCaptured && a.MainTexturePath != b.MainTexturePath) return false;

			if (a.AdditionalTexturesCaptured != b.AdditionalTexturesCaptured) return false;
			if (a.AdditionalTexturesCaptured && !TextureArraysEqual(a.AdditionalTextures, b.AdditionalTextures)) return false;

			if (a.FloatPropertiesCaptured != b.FloatPropertiesCaptured) return false;
			if (a.FloatPropertiesCaptured && !FloatArraysEqual(a.FloatProperties, b.FloatProperties)) return false;

			if (a.VectorPropertiesCaptured != b.VectorPropertiesCaptured) return false;
			if (a.VectorPropertiesCaptured && !VectorArraysEqual(a.VectorProperties, b.VectorProperties)) return false;

			if (a.ShaderCaptured != b.ShaderCaptured) return false;
			if (a.ShaderCaptured && a.ShaderName != b.ShaderName) return false;

			if (a.RenderQueueCaptured != b.RenderQueueCaptured) return false;
			if (a.RenderQueueCaptured && a.RenderQueue != b.RenderQueue) return false;

			if (a.ShaderKeywordsCaptured != b.ShaderKeywordsCaptured) return false;
			if (a.ShaderKeywordsCaptured && !StringArraysEqual(a.ShaderKeywords, b.ShaderKeywords)) return false;

			return true;
		}

		private bool ColorsApproximatelyEqual(Color a, Color b)
		{
			return Mathf.Abs(a.r - b.r) <= ColorTolerance &&
			       Mathf.Abs(a.g - b.g) <= ColorTolerance &&
			       Mathf.Abs(a.b - b.b) <= ColorTolerance &&
			       Mathf.Abs(a.a - b.a) <= ColorTolerance;
		}

		private bool TextureArraysEqual(SerializedTexture[] a, SerializedTexture[] b)
		{
			if (ReferenceEquals(a, b)) return true;
			if ((a == null) != (b == null)) return false;
			if (a == null) return true;
			if (a.Length != b.Length) return false;

			for (int i = 0; i < a.Length; i++)
			{
				if (a[i].PropertyName != b[i].PropertyName || a[i].TexturePath != b[i].TexturePath)
					return false;
			}

			return true;
		}

		private bool FloatArraysEqual(SerializedFloat[] a, SerializedFloat[] b)
		{
			if (ReferenceEquals(a, b)) return true;
			if ((a == null) != (b == null)) return false;
			if (a == null) return true;
			if (a.Length != b.Length) return false;

			for (int i = 0; i < a.Length; i++)
			{
				if (a[i].PropertyName != b[i].PropertyName || Mathf.Abs(a[i].Value - b[i].Value) > FloatTolerance)
					return false;
			}

			return true;
		}

		private bool VectorArraysEqual(SerializedVector[] a, SerializedVector[] b)
		{
			if (ReferenceEquals(a, b)) return true;
			if ((a == null) != (b == null)) return false;
			if (a == null) return true;
			if (a.Length != b.Length) return false;

			for (int i = 0; i < a.Length; i++)
			{
				if (a[i].PropertyName != b[i].PropertyName || !VectorsApproximatelyEqual(a[i].Value, b[i].Value))
					return false;
			}

			return true;
		}

		private bool VectorsApproximatelyEqual(Vector4 a, Vector4 b)
		{
			return Mathf.Abs(a.x - b.x) <= FloatTolerance &&
			       Mathf.Abs(a.y - b.y) <= FloatTolerance &&
			       Mathf.Abs(a.z - b.z) <= FloatTolerance &&
			       Mathf.Abs(a.w - b.w) <= FloatTolerance;
		}

		private bool StringArraysEqual(string[] a, string[] b)
		{
			if (ReferenceEquals(a, b)) return true;
			if ((a == null) != (b == null)) return false;
			if (a == null) return true;
			if (a.Length != b.Length) return false;

			return a.SequenceEqual(b);
		}

		private MaterialData ConvertSnapshotToData(MaterialSnapshot snapshot)
		{
			return new MaterialData
			{
				Color = snapshot.Color,
				MainTexturePath = snapshot.MainTexturePath ?? string.Empty,
				AdditionalTextures = snapshot.AdditionalTextures ?? Array.Empty<SerializedTexture>(),
				FloatProperties = snapshot.FloatProperties ?? Array.Empty<SerializedFloat>(),
				VectorProperties = snapshot.VectorProperties ?? Array.Empty<SerializedVector>(),
				ShaderName = snapshot.ShaderName ?? string.Empty,
				RenderQueue = snapshot.RenderQueue,
				ShaderKeywords = snapshot.ShaderKeywords ?? Array.Empty<string>()
			};
		}

		private string GetRelativeResourcePath(UnityEngine.Object asset)
		{
			if (asset == null)
			{
				Logger.Log("GetRelativeResourcePath: Provided asset is null.", LogCategory.RememberMaterial, LogLevel.Warning);
				return string.Empty;
			}

			string assetName = asset.name;

			// Try to find the asset in subfolders
			string foundPath = TryFindAssetInSubfolders(assetName, asset);
			if (!string.IsNullOrEmpty(foundPath))
			{
				return foundPath;
			}

			Logger.Log($"GetRelativeResourcePath: Could not find a direct resource path for '{asset.name}'. Returning base name.", LogCategory.RememberMaterial, LogLevel.Info);
			return assetName;
		}

		private string TryFindAssetInSubfolders(string baseName, UnityEngine.Object originalAsset)
		{
			foreach (var subfolder in ResourceSubfolders)
			{
				string path = string.IsNullOrEmpty(subfolder) ? baseName : $"{subfolder}/{baseName}";
				
				if (originalAsset is Texture)
				{
					Texture loadedAsset = AssetProvider.Load<Texture>(path);
					if (loadedAsset != null && loadedAsset.name == originalAsset.name)
					{
						Logger.Log($"TryFindAssetInSubfolders: Found '{baseName}' in '{path}'.", LogCategory.RememberMaterial, LogLevel.Info);
						return path;
					}
				}
				else if (originalAsset is Shader)
				{
					// Shaders are typically found by name
					Shader loadedShader = Shader.Find(baseName);
					if (loadedShader != null)
					{
						return baseName;
					}
				}
			}

			return string.Empty;
		}

		private Texture LoadTexture(string texturePath, int instanceID = 0)
		{
			// Try loading by instance ID first (for material instances with non-Resources textures)
			if (instanceID != 0)
			{
			// Use Resources.FindObjectsOfTypeAll for runtime support
			UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(typeof(Texture));
			foreach (var obj in objects)
			{
				if (obj != null && UnityObjectHelper.GetUniqueId(obj) == instanceID)
				{
					Texture texByID = obj as Texture;
					if (texByID != null)
					{
						Logger.Log($"LoadTexture: Loaded texture '{texByID.name}' by instance ID {instanceID}.", LogCategory.RememberMaterial, LogLevel.Info);
						return texByID;
					}
				}
			}
		}			// Fall back to path-based loading
			if (string.IsNullOrEmpty(texturePath))
				return null;

			if (TextureCache.TryGetValue(texturePath, out var cachedTex))
			{
				return cachedTex;
			}

			Texture foundTex = AssetProvider.Load<Texture>(texturePath);
			if (foundTex != null)
			{
				TextureCache[texturePath] = foundTex;
				return foundTex;
			}

			// Try searching through subfolders
			string baseName = texturePath;
			foreach (var subfolder in ResourceSubfolders)
			{
				string candidatePath = string.IsNullOrEmpty(subfolder) ? baseName : $"{subfolder}/{baseName}";
				foundTex = AssetProvider.Load<Texture>(candidatePath);
				if (foundTex != null)
				{
					Logger.Log($"LoadTexture: Found texture '{baseName}' in '{candidatePath}'.", LogCategory.RememberMaterial, LogLevel.Info);
					TextureCache[texturePath] = foundTex;
					return foundTex;
				}
			}

			Logger.Log($"LoadTexture: Could not find texture '{texturePath}' in any known subfolder.", LogCategory.RememberMaterial, LogLevel.Warning);
			return null;
		}

		private Shader LoadShader(string shaderName)
		{
			if (string.IsNullOrEmpty(shaderName))
				return null;

			if (ShaderCache.TryGetValue(shaderName, out var cachedShader))
			{
				return cachedShader;
			}

			Shader foundShader = Shader.Find(shaderName);
			if (foundShader != null)
			{
				ShaderCache[shaderName] = foundShader;
				return foundShader;
			}

			Logger.Log($"LoadShader: Could not find shader '{shaderName}'.", LogLevel.Warning);
			return null;
		}
	}

	[MemoryPackable]
	public partial class MaterialData
	{
		// Single material support (legacy/current)
		public Color Color { get; set; }
		public string MainTexturePath { get; set; }
		public int MainTextureInstanceID { get; set; } // For material instances with non-Resources textures
		public SerializedTexture[] AdditionalTextures { get; set; }
		public SerializedFloat[] FloatProperties { get; set; }
		public SerializedVector[] VectorProperties { get; set; }
		public string ShaderName { get; set; }
		public int RenderQueue { get; set; }
		public string[] ShaderKeywords { get; set; }

		// Multi-material support (new)
		public Dictionary<int, SingleMaterialData> Materials { get; set; }

		public MaterialData() { }
	}

	[MemoryPackable]
	public partial class SingleMaterialData
	{
		public Color Color { get; set; }
		public string MainTexturePath { get; set; }
		public int MainTextureInstanceID { get; set; } // For material instances with non-Resources textures
		public SerializedTexture[] AdditionalTextures { get; set; }
		public SerializedFloat[] FloatProperties { get; set; }
		public SerializedVector[] VectorProperties { get; set; }
		public string ShaderName { get; set; }
		public int RenderQueue { get; set; }
		public string[] ShaderKeywords { get; set; }

		public SingleMaterialData() { }
	}

	[MemoryPackable]
	public partial struct SerializedTexture
	{
		public string PropertyName { get; set; }
		public string TexturePath { get; set; }
		public int TextureInstanceID { get; set; } // For material instances with non-Resources textures

		public SerializedTexture Clone()
		{
			return new SerializedTexture
			{
				PropertyName = PropertyName,
				TexturePath = TexturePath,
				TextureInstanceID = TextureInstanceID
			};
		}
	}

	[MemoryPackable]
	public partial struct SerializedFloat
	{
		public string PropertyName { get; set; }
		public float Value { get; set; }

		public SerializedFloat Clone()
		{
			return new SerializedFloat
			{
				PropertyName = PropertyName,
				Value = Value
			};
		}
	}

	[MemoryPackable]
	public partial struct SerializedVector
	{
		public string PropertyName { get; set; }
		public Vector4 Value { get; set; }

		public SerializedVector Clone()
		{
			return new SerializedVector
			{
				PropertyName = PropertyName,
				Value = Value
			};
		}
	}
}
#endif
