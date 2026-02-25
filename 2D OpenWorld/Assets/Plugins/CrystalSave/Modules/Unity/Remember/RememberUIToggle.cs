#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Arawn.CrystalSave.Runtime
{
        [AddComponentMenu("Crystal Save/Remember Components/Remember UI Toggle")]
        [DisallowMultipleComponent]
        [RequireComponent(typeof(Toggle))]
        [RememberTarget(typeof(Toggle))]
        public class RememberUIToggle : SaveableComponent
        {
                [Header("Save Optimization")]
                [Tooltip("Skip serialization when the captured Toggle data did not change since the last save.")]
                [SerializeField]
                private bool skipSavingWhenUnchanged = false;

                [Header("Toggle Properties to Save")]
                [Tooltip("Serialize the toggle's interactable state alongside its on/off value.")]
                [SerializeField]
                private bool rememberInteractable = false;

                [Header("Load Behaviour")]
                [Tooltip("Apply the loaded value using Toggle.SetIsOnWithoutNotify to avoid triggering OnValueChanged events.")]
                [SerializeField]
                private bool applyWithoutNotify = true;

                private Toggle targetToggle;
                private RememberUIToggleData cachedSnapshot;
                private bool hasCachedSnapshot;
                private byte[] cachedSerializedData;

                protected override void Awake()
                {
                        base.Awake();
                        targetToggle = GetComponent<Toggle>();

                        if (targetToggle == null)
                        {
                                Logger.Log($"{nameof(RememberUIToggle)} requires a Toggle component on '{gameObject.name}'.", LogCategory.RememberUIToggle, LogLevel.Error);
                                enabled = false;
                                return;
                        }

                        if (skipSavingWhenUnchanged && TryCaptureCurrentState(out var snapshot))
                        {
                                CacheSnapshot(snapshot);
                        }
                }

                protected override byte[] SerializeComponentData()
                {
                        if (targetToggle == null)
                        {
                                Logger.Log("RememberUIToggle: Toggle component not found during serialization.", LogCategory.RememberUIToggle, LogLevel.Warning);
                                return Array.Empty<byte>();
                        }

                        if (!TryCaptureCurrentState(out var snapshot))
                        {
                                return null;
                        }

                        if (skipSavingWhenUnchanged && hasCachedSnapshot && cachedSnapshot != null && AreEquivalent(cachedSnapshot, snapshot))
                        {
                                if (cachedSerializedData != null && cachedSerializedData.Length > 0)
                                {
                                        return cachedSerializedData;
                                }
                        }

                        byte[] serialized = SaveDataSerializer.Instance.Serialize(snapshot);

                        if (skipSavingWhenUnchanged)
                        {
                                CacheSnapshot(snapshot);
                                cachedSerializedData = serialized;
                        }

                        return serialized;
                }

                protected override void DeserializeComponentData(byte[] data)
                {
                        if (data == null || data.Length == 0)
                        {
                                Logger.Log("RememberUIToggle: Deserialization data is null or empty.", LogCategory.RememberUIToggle, LogLevel.Warning);
                                return;
                        }

                        if (targetToggle == null)
                        {
                                Logger.Log("RememberUIToggle: Toggle component missing during deserialization.", LogCategory.RememberUIToggle, LogLevel.Warning);
                                return;
                        }

                        try
                        {
                                var snapshot = SaveDataSerializer.Instance.Deserialize<RememberUIToggleData>(data);
                                if (snapshot == null)
                                {
                                        Logger.Log("RememberUIToggle: Deserialized toggle data is null.", LogCategory.RememberUIToggle, LogLevel.Warning);
                                        return;
                                }

                                ApplySnapshot(snapshot);

                                if (skipSavingWhenUnchanged && TryCaptureCurrentState(out var refreshedSnapshot))
                                {
                                        CacheSnapshot(refreshedSnapshot);
                                }
                                else if (skipSavingWhenUnchanged)
                                {
                                        hasCachedSnapshot = false;
                                        cachedSnapshot = null;
                                }
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"RememberUIToggle: Error deserializing toggle data: {ex.Message}", LogCategory.RememberUIToggle, LogLevel.Error);
                        }
                }

                private bool TryCaptureCurrentState(out RememberUIToggleData snapshot)
                {
                        snapshot = null;

                        if (targetToggle == null)
                        {
                                return false;
                        }

                        snapshot = new RememberUIToggleData
                        {
                                IsOn = targetToggle.isOn,
                                HasInteractable = rememberInteractable,
                                Interactable = rememberInteractable ? targetToggle.interactable : false
                        };

                        return true;
                }

                private void ApplySnapshot(RememberUIToggleData snapshot)
                {
                        if (snapshot.HasInteractable)
                        {
                                targetToggle.interactable = snapshot.Interactable;
                        }

                        if (applyWithoutNotify)
                        {
                                targetToggle.SetIsOnWithoutNotify(snapshot.IsOn);
                        }
                        else
                        {
                                targetToggle.isOn = snapshot.IsOn;
                        }
                }

                private void CacheSnapshot(RememberUIToggleData snapshot)
                {
                        if (snapshot == null)
                        {
                                hasCachedSnapshot = false;
                                cachedSnapshot = null;
                                return;
                        }

                        cachedSnapshot = snapshot.Clone();
                        hasCachedSnapshot = true;
                }

                private bool AreEquivalent(RememberUIToggleData first, RememberUIToggleData second)
                {
                        if (first == null || second == null)
                        {
                                return false;
                        }

                        if (first.IsOn != second.IsOn)
                        {
                                return false;
                        }

                        if (first.HasInteractable != second.HasInteractable)
                        {
                                return false;
                        }

                        if (first.HasInteractable && second.HasInteractable && first.Interactable != second.Interactable)
                        {
                                return false;
                        }

                        return true;
                }
        }

        [MemoryPackable]
        public partial class RememberUIToggleData
        {
                public bool IsOn { get; set; }
                public bool HasInteractable { get; set; }
                public bool Interactable { get; set; }

                public RememberUIToggleData Clone()
                {
                        return new RememberUIToggleData
                        {
                                IsOn = IsOn,
                                HasInteractable = HasInteractable,
                                Interactable = Interactable
                        };
                }
        }
}
#endif
