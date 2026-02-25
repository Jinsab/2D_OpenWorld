#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Arawn.CrystalSave.Runtime
{
        [AddComponentMenu("Crystal Save/Remember Components/Remember UI Slider")]
        [DisallowMultipleComponent]
        [RequireComponent(typeof(Slider))]
        [RememberTarget(typeof(Slider))]
        public class RememberUISlider : SaveableComponent
        {
                [Header("Save Optimization")]
                [Tooltip("Skip serialization when the captured Slider data did not change since the last save.")]
                [SerializeField]
                private bool skipSavingWhenUnchanged = false;

                [Header("Slider Properties to Save")]
                [Tooltip("Serialize the slider's interactable state in addition to its value.")]
                [SerializeField]
                private bool rememberInteractable = false;

                [Header("Load Behaviour")]
                [Tooltip("Apply the loaded value without notifying listeners to avoid triggering OnValueChanged events.")]
                [SerializeField]
                private bool applyWithoutNotify = true;

                private Slider targetSlider;
                private RememberUISliderData cachedSnapshot;
                private bool hasCachedSnapshot;
                private byte[] cachedSerializedData;

                protected override void Awake()
                {
                        base.Awake();
                        targetSlider = GetComponent<Slider>();

                        if (targetSlider == null)
                        {
                                Logger.Log($"{nameof(RememberUISlider)} requires a Slider component on '{gameObject.name}'.", LogCategory.RememberUISlider, LogLevel.Error);
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
                        if (targetSlider == null)
                        {
                                Logger.Log("RememberUISlider: Slider component not found during serialization.", LogCategory.RememberUISlider, LogLevel.Warning);
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
                                Logger.Log("RememberUISlider: Deserialization data is null or empty.", LogCategory.RememberUISlider, LogLevel.Warning);
                                return;
                        }

                        if (targetSlider == null)
                        {
                                Logger.Log("RememberUISlider: Slider component missing during deserialization.", LogCategory.RememberUISlider, LogLevel.Warning);
                                return;
                        }

                        try
                        {
                                var snapshot = SaveDataSerializer.Instance.Deserialize<RememberUISliderData>(data);
                                if (snapshot == null)
                                {
                                        Logger.Log("RememberUISlider: Deserialized slider data is null.", LogCategory.RememberUISlider, LogLevel.Warning);
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
                                Logger.Log($"RememberUISlider: Error deserializing slider data: {ex.Message}", LogCategory.RememberUISlider, LogLevel.Error);
                        }
                }

                private bool TryCaptureCurrentState(out RememberUISliderData snapshot)
                {
                        snapshot = null;

                        if (targetSlider == null)
                        {
                                return false;
                        }

                        snapshot = new RememberUISliderData
                        {
                                Value = targetSlider.value,
                                HasInteractable = rememberInteractable,
                                Interactable = rememberInteractable ? targetSlider.interactable : false
                        };

                        return true;
                }

                private void ApplySnapshot(RememberUISliderData snapshot)
                {
                        if (snapshot.HasInteractable)
                        {
                                targetSlider.interactable = snapshot.Interactable;
                        }

                        if (applyWithoutNotify)
                        {
                                targetSlider.SetValueWithoutNotify(snapshot.Value);
                        }
                        else
                        {
                                targetSlider.value = snapshot.Value;
                        }
                }

                private void CacheSnapshot(RememberUISliderData snapshot)
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

                private bool AreEquivalent(RememberUISliderData first, RememberUISliderData second)
                {
                        if (first == null || second == null)
                        {
                                return false;
                        }

                        if (!Mathf.Approximately(first.Value, second.Value))
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
        public partial class RememberUISliderData
        {
                public float Value { get; set; }
                public bool HasInteractable { get; set; }
                public bool Interactable { get; set; }

                public RememberUISliderData Clone()
                {
                        return new RememberUISliderData
                        {
                                Value = Value,
                                HasInteractable = HasInteractable,
                                Interactable = Interactable
                        };
                }
        }
}
#endif
