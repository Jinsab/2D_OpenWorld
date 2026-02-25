#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arawn.CrystalSave.Runtime
{
    [AddComponentMenu("Crystal Save/Remember Components/Remember Terrain")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Terrain))]
    [RememberTarget(typeof(Terrain))]
    public class RememberTerrain : SaveableComponent
    {
        private Terrain _terrain;

        [Header("Save Optimization")]
        [SerializeField] private bool skipSavingWhenUnchanged;

        private RememberTerrainData cachedSnapshot;
        private bool hasCachedSnapshot;
        private byte[] cachedSerializedData;
        private const float FloatTolerance = 0.0001f;

        protected override void Awake()
        {
            base.Awake();
            _terrain = GetComponent<Terrain>();
            if (_terrain == null)
            {
                Logger.Log($"{nameof(RememberTerrain)} requires a Terrain component on '{gameObject.name}'.", LogCategory.RememberTerrain, LogLevel.Error);
                enabled = false;
            }

            if (skipSavingWhenUnchanged && TryCaptureCurrentState(out var snapshot))
            {
                cachedSnapshot = CloneSnapshot(snapshot);
                hasCachedSnapshot = true;
            }
            else
            {
                cachedSnapshot = null;
                hasCachedSnapshot = false;
            }
        }

        protected override byte[] SerializeComponentData()
        {
            if (!TryCaptureCurrentState(out var snapshot))
            {
                Logger.Log("SerializeComponentData failed: Terrain component not found.", LogCategory.RememberTerrain, LogLevel.Warning);
                return null;
            }

            if (skipSavingWhenUnchanged && hasCachedSnapshot && AreEquivalent(cachedSnapshot, snapshot))
            {
                if (cachedSerializedData != null && cachedSerializedData.Length > 0)
                {
                    return cachedSerializedData;
                }
            }

            var serialized = SaveDataSerializer.Instance.Serialize(snapshot);

            if (skipSavingWhenUnchanged)
            {
                cachedSnapshot = CloneSnapshot(snapshot);
                hasCachedSnapshot = true;
                cachedSerializedData = serialized;
            }

            return serialized;
        }

        protected override void DeserializeComponentData(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                Logger.Log("DeserializeComponentData failed: Data is null or empty.", LogCategory.RememberTerrain, LogLevel.Warning);
                return;
            }
            if (_terrain == null)
            {
                Logger.Log("DeserializeComponentData failed: Terrain component not found.", LogCategory.RememberTerrain, LogLevel.Warning);
                return;
            }

            try
            {
                var data = SaveDataSerializer.Instance.Deserialize<RememberTerrainData>(bytes);
                if (data == null)
                {
                    Logger.Log("Deserialized data is null.", LogCategory.RememberTerrain, LogLevel.Warning);
                    return;
                }

                _terrain.allowAutoConnect = data.AllowAutoConnect;
                _terrain.groupingID = data.GroupingID;
                _terrain.drawHeightmap = data.DrawHeightmap;
                _terrain.drawTreesAndFoliage = data.DrawTreesAndFoliage;
                _terrain.heightmapPixelError = data.HeightmapPixelError;
                _terrain.basemapDistance = data.BasemapDistance;
                _terrain.treeDistance = data.TreeDistance;
                _terrain.treeBillboardDistance = data.TreeBillboardDistance;
                _terrain.treeCrossFadeLength = data.TreeCrossFadeLength;
                _terrain.treeMaximumFullLODCount = data.TreeMaximumFullLODCount;
                _terrain.detailObjectDistance = data.DetailObjectDistance;
                _terrain.detailObjectDensity = data.DetailObjectDensity;
                _terrain.drawInstanced = data.DrawInstanced;
                _terrain.shadowCastingMode = data.ShadowCastingMode;
                _terrain.lightmapIndex = data.LightmapIndex;
                _terrain.realtimeLightmapIndex = data.RealtimeLightmapIndex;

                if (data.MaterialTemplate != null)
                {
                    if (data.MaterialTemplate.GetValue() is Material mat)
                        _terrain.materialTemplate = mat;
                }

                if (skipSavingWhenUnchanged)
                {
                    cachedSnapshot = CloneSnapshot(data);
                    hasCachedSnapshot = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Deserialization failed: {ex.Message}", LogCategory.RememberTerrain, LogLevel.Error);
            }
        }

        private bool TryCaptureCurrentState(out RememberTerrainData snapshot)
        {
            if (_terrain == null)
            {
                snapshot = null;
                return false;
            }

            snapshot = new RememberTerrainData
            {
                AllowAutoConnect = _terrain.allowAutoConnect,
                GroupingID = _terrain.groupingID,
                DrawHeightmap = _terrain.drawHeightmap,
                DrawTreesAndFoliage = _terrain.drawTreesAndFoliage,
                HeightmapPixelError = _terrain.heightmapPixelError,
                BasemapDistance = _terrain.basemapDistance,
                TreeDistance = _terrain.treeDistance,
                TreeBillboardDistance = _terrain.treeBillboardDistance,
                TreeCrossFadeLength = _terrain.treeCrossFadeLength,
                TreeMaximumFullLODCount = _terrain.treeMaximumFullLODCount,
                DetailObjectDistance = _terrain.detailObjectDistance,
                DetailObjectDensity = _terrain.detailObjectDensity,
                DrawInstanced = _terrain.drawInstanced,
                ShadowCastingMode = _terrain.shadowCastingMode,
                LightmapIndex = _terrain.lightmapIndex,
                RealtimeLightmapIndex = _terrain.realtimeLightmapIndex,
                MaterialTemplate = _terrain.materialTemplate != null ? new MaterialWrapper(_terrain.materialTemplate) : null
            };

            return true;
        }

        private RememberTerrainData CloneSnapshot(RememberTerrainData source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new RememberTerrainData
            {
                AllowAutoConnect = source.AllowAutoConnect,
                GroupingID = source.GroupingID,
                DrawHeightmap = source.DrawHeightmap,
                DrawTreesAndFoliage = source.DrawTreesAndFoliage,
                HeightmapPixelError = source.HeightmapPixelError,
                BasemapDistance = source.BasemapDistance,
                TreeDistance = source.TreeDistance,
                TreeBillboardDistance = source.TreeBillboardDistance,
                TreeCrossFadeLength = source.TreeCrossFadeLength,
                TreeMaximumFullLODCount = source.TreeMaximumFullLODCount,
                DetailObjectDistance = source.DetailObjectDistance,
                DetailObjectDensity = source.DetailObjectDensity,
                DrawInstanced = source.DrawInstanced,
                ShadowCastingMode = source.ShadowCastingMode,
                LightmapIndex = source.LightmapIndex,
                RealtimeLightmapIndex = source.RealtimeLightmapIndex,
                MaterialTemplate = CloneMaterialWrapper(source.MaterialTemplate)
            };

            return clone;
        }

        private MaterialWrapper CloneMaterialWrapper(MaterialWrapper source)
        {
            if (source == null)
            {
                return null;
            }

            return new MaterialWrapper
            {
                MaterialName = source.MaterialName,
                ShaderName = source.ShaderName,
                TextureNames = source.TextureNames != null
                    ? new Dictionary<string, string>(source.TextureNames)
                    : new Dictionary<string, string>()
            };
        }

        private bool AreEquivalent(RememberTerrainData a, RememberTerrainData b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null || b == null)
            {
                return false;
            }

            bool floatsEqual = Mathf.Abs(a.HeightmapPixelError - b.HeightmapPixelError) <= FloatTolerance &&
                               Mathf.Abs(a.BasemapDistance - b.BasemapDistance) <= FloatTolerance &&
                               Mathf.Abs(a.TreeDistance - b.TreeDistance) <= FloatTolerance &&
                               Mathf.Abs(a.TreeBillboardDistance - b.TreeBillboardDistance) <= FloatTolerance &&
                               Mathf.Abs(a.TreeCrossFadeLength - b.TreeCrossFadeLength) <= FloatTolerance &&
                               Mathf.Abs(a.DetailObjectDistance - b.DetailObjectDistance) <= FloatTolerance &&
                               Mathf.Abs(a.DetailObjectDensity - b.DetailObjectDensity) <= FloatTolerance;

            if (!floatsEqual)
            {
                return false;
            }

            bool primitivesEqual =
                a.AllowAutoConnect == b.AllowAutoConnect &&
                a.GroupingID == b.GroupingID &&
                a.DrawHeightmap == b.DrawHeightmap &&
                a.DrawTreesAndFoliage == b.DrawTreesAndFoliage &&
                a.TreeMaximumFullLODCount == b.TreeMaximumFullLODCount &&
                a.DrawInstanced == b.DrawInstanced &&
                a.ShadowCastingMode == b.ShadowCastingMode &&
                a.LightmapIndex == b.LightmapIndex &&
                a.RealtimeLightmapIndex == b.RealtimeLightmapIndex;

            if (!primitivesEqual)
            {
                return false;
            }

            return AreMaterialsEquivalent(a.MaterialTemplate, b.MaterialTemplate);
        }

        private bool AreMaterialsEquivalent(MaterialWrapper a, MaterialWrapper b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null || b == null)
            {
                return a == null && b == null;
            }

            if (!string.Equals(a.MaterialName, b.MaterialName, StringComparison.Ordinal) ||
                !string.Equals(a.ShaderName, b.ShaderName, StringComparison.Ordinal))
            {
                return false;
            }

            var aTextures = a.TextureNames ?? new Dictionary<string, string>();
            var bTextures = b.TextureNames ?? new Dictionary<string, string>();

            if (aTextures.Count != bTextures.Count)
            {
                return false;
            }

            foreach (var kvp in aTextures)
            {
                if (!bTextures.TryGetValue(kvp.Key, out var value) ||
                    !string.Equals(kvp.Value, value, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }

    [MemoryPackable]
    public partial class RememberTerrainData
    {
        public bool AllowAutoConnect { get; set; }
        public int GroupingID { get; set; }
        public bool DrawHeightmap { get; set; }
        public bool DrawTreesAndFoliage { get; set; }
        public float HeightmapPixelError { get; set; }
        public float BasemapDistance { get; set; }
        public float TreeDistance { get; set; }
        public float TreeBillboardDistance { get; set; }
        public float TreeCrossFadeLength { get; set; }
        public int TreeMaximumFullLODCount { get; set; }
        public float DetailObjectDistance { get; set; }
        public float DetailObjectDensity { get; set; }
        public bool DrawInstanced { get; set; }
        public ShadowCastingMode ShadowCastingMode { get; set; }
        public int LightmapIndex { get; set; }
        public int RealtimeLightmapIndex { get; set; }
        public MaterialWrapper MaterialTemplate { get; set; }

        public RememberTerrainData() { }
    }
}
#endif
