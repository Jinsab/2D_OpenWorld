#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using MemoryPack;
using UnityEngine.AI;

namespace Arawn.CrystalSave.Runtime
{
        [AddComponentMenu("Crystal Save/Remember Components/Remember Scenes")]
        [DisallowMultipleComponent]
        [RememberCustomIcon("Assets/Plugins/CrystalSave/Editor/Gizmos/Scene.png")]
        public sealed class RememberScenes : SaveableComponent
        {
                [Header("Save Optimization")]
                [SerializeField] private bool skipSavingWhenUnchanged;

                private SceneListData cachedSnapshot;
                private bool hasCachedSnapshot;
                private byte[] cachedSerializedData;

                protected override void Awake()
                {
                        base.Awake();

                        if (skipSavingWhenUnchanged && TryCaptureCurrentState(out var snapshot))
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

                /* ─────────────────────────────────────────────────────────────── */
                #region SERIALIZATION

                protected override byte[] SerializeComponentData()
                {
                        if (!TryCaptureCurrentState(out var snapshot))
                        {
                                if (skipSavingWhenUnchanged)
                                {
                                        cachedSnapshot = null;
                                        hasCachedSnapshot = false;
                                }

                                return null;
                        }

                        if (skipSavingWhenUnchanged)
                        {
                                if (hasCachedSnapshot && AreEquivalent(snapshot, cachedSnapshot))
                                {
                                        if (cachedSerializedData != null && cachedSerializedData.Length > 0)
                                        {
                                                return cachedSerializedData;
                                        }
                                }

                                cachedSnapshot = CloneSnapshot(snapshot);
                                hasCachedSnapshot = cachedSnapshot != null;
                        }

                        byte[] serialized = Serializer.Serialize(snapshot);
                        
                        if (skipSavingWhenUnchanged)
                        {
                                cachedSerializedData = serialized;
                        }

                        return serialized;
                }

                protected override void DeserializeComponentData(byte[] bytes)
                {
                        if (bytes == null || bytes.Length == 0) return;

                        SceneListData data = Serializer.Deserialize<SceneListData>(bytes);
                        if (data?.SceneNames == null || data.SceneNames.Count == 0) return;

                        foreach (string sceneName in data.SceneNames)
                        {
				Scene s = SceneManager.GetSceneByName(sceneName);
				if (s.IsValid() && s.isLoaded) continue; // already there

				try
				{
					SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
					Logger.Log($"RememberScenes: loading additive scene '{sceneName}'.", LogCategory.RememberScenes, LogLevel.Info);
				}
                                catch (Exception ex)
                                {
                                        Logger.Log($"RememberScenes: failed to load '{sceneName}': {ex.Message}", LogCategory.RememberScenes, LogLevel.Warning);
                                }
                        }

                        if (skipSavingWhenUnchanged)
                        {
                                cachedSnapshot = CloneSnapshot(data);
                                hasCachedSnapshot = cachedSnapshot != null;
                        }
                }

                #endregion

                private bool TryCaptureCurrentState(out SceneListData snapshot)
                {
                        var hostScene = gameObject.scene;
                        var sceneNames = new List<string>();

                        for (int i = 0; i < SceneManager.sceneCount; i++)
                        {
                                Scene scene = SceneManager.GetSceneAt(i);
                                if (!scene.isLoaded) continue;
                                if (scene == hostScene) continue;
                                if (string.IsNullOrEmpty(scene.name)) continue;

                                sceneNames.Add(scene.name);
                        }

                        if (sceneNames.Count == 0)
                        {
                                snapshot = null;
                                return false;
                        }

                        sceneNames.Sort(StringComparer.Ordinal);
                        snapshot = new SceneListData(sceneNames);
                        return true;
                }

                private static bool AreEquivalent(SceneListData a, SceneListData b)
                {
                        if (ReferenceEquals(a, b)) return true;
                        if (a == null || b == null)
                                return (a?.SceneNames == null || a.SceneNames.Count == 0) &&
                                       (b?.SceneNames == null || b.SceneNames.Count == 0);

                        IReadOnlyList<string> aNames = (IReadOnlyList<string>)a.SceneNames ?? Array.Empty<string>();
                        IReadOnlyList<string> bNames = (IReadOnlyList<string>)b.SceneNames ?? Array.Empty<string>();
                        if (aNames.Count != bNames.Count) return false;

                        for (int i = 0; i < aNames.Count; i++)
                        {
                                if (!string.Equals(aNames[i], bNames[i], StringComparison.Ordinal))
                                        return false;
                        }

                        return true;
                }

                private static SceneListData CloneSnapshot(SceneListData source)
                {
                        if (source == null) return null;

                        var clone = new SceneListData(source.SceneNames);
                        clone.SceneNames?.Sort(StringComparer.Ordinal);
                        return clone;
                }
        }

	/* ─────────────────────────────────────────────────────────────── */

	[MemoryPackable]
	public partial class SceneListData
	{
		public List<string> SceneNames { get; set; } = new List<string>();

		// Needed by MemoryPack
		[MemoryPackConstructor] public SceneListData() { }

		public SceneListData(IEnumerable<string> names)
			=> SceneNames = new List<string>(names ?? Array.Empty<string>());
	}
}
#endif