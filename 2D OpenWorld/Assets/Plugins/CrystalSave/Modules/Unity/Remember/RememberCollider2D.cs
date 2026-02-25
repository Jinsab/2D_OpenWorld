#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    [AddComponentMenu("Crystal Save/Remember Components/Remember Collider 2D")]
    [DisallowMultipleComponent]
    [RememberTarget(typeof(Collider2D))]
    [RememberIcon("BoxCollider2D Icon")]
    public sealed class RememberCollider2D : SaveableComponent
    {
        /*───────────── Inspector ─────────────*/
        [Header("2D Collider properties to save")]

        [Tooltip("Save the Collider2D’s enabled state.")]
        [SerializeField] private bool rememberEnabled = true;

        [Tooltip("Save whether the Collider2D is marked as Trigger.")]
        [SerializeField] private bool rememberIsTrigger = true;

        [Tooltip("Save the Collider2D’s PhysicsMaterial2D (stored by name).")]
        [SerializeField] private bool rememberMaterial = true;

        [Header("Save Optimization")]
        [SerializeField] private bool skipSavingWhenUnchanged;

        /*───────────── Internals ─────────────*/
        private Collider2D targetCollider;
        private Collider2DData cachedSnapshot;
        private bool hasCachedSnapshot;
        private byte[] cachedSerializedData;

        private const float FloatTolerance = 0.0001f;

        /*───────────── Lifecycle ─────────────*/
        protected override void Awake()
        {
            base.Awake();

            targetCollider = GetComponent<Collider2D>();
            if (targetCollider == null)
            {
                Logger.Log($"{nameof(RememberCollider2D)} requires a Collider2D on '{gameObject.name}'.", LogCategory.RememberCollider2D, LogLevel.Error);
                enabled = false;
            }

            if (skipSavingWhenUnchanged)
            {
                if (TryCaptureCurrentState(out var snapshot))
                {
                    cachedSnapshot = snapshot;
                    hasCachedSnapshot = true;
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

        /*───────────── SERIALISE OUT ─────────────*/
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

            if (skipSavingWhenUnchanged && hasCachedSnapshot && AreEquivalent(snapshot, cachedSnapshot))
            {
                if (cachedSerializedData != null && cachedSerializedData.Length > 0)
                {
                    return cachedSerializedData;
                }
            }

            byte[] serialized = Serializer.Serialize(snapshot);

            if (skipSavingWhenUnchanged)
            {
                cachedSnapshot = snapshot;
                hasCachedSnapshot = true;
                cachedSerializedData = serialized;
            }

            return serialized;
        }

        /*───────────── SERIALISE  IN ─────────────*/
        protected override void DeserializeComponentData(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || targetCollider == null) return;

            try
            {
                var data = Serializer.Deserialize<Collider2DData>(bytes);
                var materialApplied = true;

                if (rememberEnabled) targetCollider.enabled = data.Enabled;
                if (rememberIsTrigger) targetCollider.isTrigger = data.IsTrigger;

                if (rememberMaterial)
                {
                    if (!string.IsNullOrEmpty(data.MaterialName))
                    {
                        var mat = AssetProvider.Load<PhysicsMaterial2D>(data.MaterialName);
                        if (mat)
                        {
                            targetCollider.sharedMaterial = mat;
                        }
                        else
                        {
                            Logger.Log($"RememberCollider2D: PhysicsMaterial2D '{data.MaterialName}' not found.", LogCategory.RememberCollider2D, LogLevel.Warning);
                            materialApplied = false;
                        }
                    }
                    else
                    {
                        targetCollider.sharedMaterial = null;
                    }
                }

                Logger.Log($"RememberCollider2D: Restored data for '{gameObject.name}'.", LogCategory.RememberCollider2D, LogLevel.Info);

                if (skipSavingWhenUnchanged && materialApplied)
                {
                    cachedSnapshot = new Collider2DData
                    {
                        Enabled = data.Enabled,
                        IsTrigger = data.IsTrigger,
                        MaterialName = data.MaterialName
                    };
                    hasCachedSnapshot = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"RememberCollider2D deserialisation error on '{gameObject.name}': {ex.Message}", LogCategory.RememberCollider2D, LogLevel.Error);
            }
        }

        private bool TryCaptureCurrentState(out Collider2DData snapshot)
        {
            snapshot = null;

            if (targetCollider == null) return false;

            if (!rememberEnabled && !rememberIsTrigger && !rememberMaterial) return false;

            var data = new Collider2DData();

            if (rememberEnabled) data.Enabled = targetCollider.enabled;
            if (rememberIsTrigger) data.IsTrigger = targetCollider.isTrigger;
            if (rememberMaterial)
            {
                data.MaterialName = targetCollider.sharedMaterial != null
                    ? targetCollider.sharedMaterial.name
                    : null;
            }

            snapshot = data;
            return true;
        }

        private bool AreEquivalent(Collider2DData current, Collider2DData cached)
        {
            if (current == null || cached == null) return false;

            if (rememberEnabled && current.Enabled != cached.Enabled) return false;
            if (rememberIsTrigger && current.IsTrigger != cached.IsTrigger) return false;
            if (rememberMaterial && !string.Equals(current.MaterialName, cached.MaterialName, StringComparison.Ordinal)) return false;

            return true;
        }
    }

    /*───────────── DATA POD ─────────────*/
    [MemoryPackable]
    public partial class Collider2DData
    {
        public bool   Enabled;
        public bool   IsTrigger;
        public string MaterialName;
    }
}
#endif
