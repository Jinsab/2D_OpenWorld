#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    [AddComponentMenu("Crystal Save/Remember Components/Remember Joint")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Joint))]
    [RememberTarget(typeof(Joint))]
    [RememberIcon("HingeJoint Icon")]
    public class RememberJoint : SaveableComponent
    {
        [Header("Joint Properties to Save")]
        [SerializeField] private bool rememberConnectedBody = true;
        [SerializeField] private bool rememberAnchor = true;
        [SerializeField] private bool rememberAxis = true;
        [SerializeField] private bool rememberAutoConfigureConnectedAnchor = true;
        [SerializeField] private bool rememberConnectedAnchor = true;
        [SerializeField] private bool rememberBreakForce = true;
        [SerializeField] private bool rememberBreakTorque = true;
        [SerializeField] private bool rememberEnableCollision = true;
        [SerializeField] private bool rememberEnablePreprocessing = true;
        [SerializeField] private bool rememberMassScale = true;
        [SerializeField] private bool rememberConnectedMassScale = true;

        [Header("Save Optimization")]
        [SerializeField] private bool skipSavingWhenUnchanged;

        private JointData cachedSnapshot;
        private bool hasCachedSnapshot;
        private byte[] cachedSerializedData;
        private const float FloatTolerance = 0.0001f;

        private Joint targetJoint;

        protected override void Awake()
        {
            base.Awake();
            targetJoint = GetComponent<Joint>();
            if (targetJoint == null)
            {
                Logger.Log($"{nameof(RememberJoint)} requires a Joint component on the same GameObject.", LogCategory.RememberJoint, LogLevel.Error);
                enabled = false;
                cachedSnapshot = null;
                hasCachedSnapshot = false;
                return;
            }

            if (skipSavingWhenUnchanged && TryCaptureCurrentState(out var snapshot, false))
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

        protected override byte[] SerializeComponentData()
        {
            if (!TryCaptureCurrentState(out var snapshot, true))
            {
                if (skipSavingWhenUnchanged)
                {
                    cachedSnapshot = null;
                    hasCachedSnapshot = false;
                }
                return null;
            }

            if (skipSavingWhenUnchanged && hasCachedSnapshot && AreEquivalent(cachedSnapshot, snapshot))
            {
                if (cachedSerializedData != null && cachedSerializedData.Length > 0)
                {
                    return cachedSerializedData;
                }
            }

            byte[] serialized = SaveDataSerializer.Instance.Serialize(snapshot);

            if (skipSavingWhenUnchanged)
            {
                cachedSnapshot = snapshot;
                hasCachedSnapshot = true;
                cachedSerializedData = serialized;
            }

            return serialized;
        }

        protected override void DeserializeComponentData(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || targetJoint == null) return;

            try
            {
                var data = SaveDataSerializer.Instance.Deserialize<JointData>(bytes);
                if (data == null) return;

                if (rememberConnectedBody && !string.IsNullOrEmpty(data.ConnectedBodyID))
                {
                    GameObject go = SaveManager.Instance?.FindGameObjectByUniqueID(data.ConnectedBodyID, SaveManager.IdentifierType.UniqueID);
                    targetJoint.connectedBody = go != null ? go.GetComponent<Rigidbody>() : null;
                }
                if (rememberAnchor) targetJoint.anchor = data.Anchor;
                if (rememberAxis) targetJoint.axis = data.Axis;
                if (rememberAutoConfigureConnectedAnchor) targetJoint.autoConfigureConnectedAnchor = data.AutoConfigureConnectedAnchor;
                if (rememberConnectedAnchor) targetJoint.connectedAnchor = data.ConnectedAnchor;
                if (rememberBreakForce) targetJoint.breakForce = data.BreakForce;
                if (rememberBreakTorque) targetJoint.breakTorque = data.BreakTorque;
                if (rememberEnableCollision) targetJoint.enableCollision = data.EnableCollision;
                if (rememberEnablePreprocessing) targetJoint.enablePreprocessing = data.EnablePreprocessing;
                if (rememberMassScale) targetJoint.massScale = data.MassScale;
                if (rememberConnectedMassScale) targetJoint.connectedMassScale = data.ConnectedMassScale;

                if (skipSavingWhenUnchanged)
                {
                    if (TryCaptureCurrentState(out var snapshot, false))
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
            }
            catch (Exception ex)
            {
                Logger.Log($"{nameof(RememberJoint)} failed to deserialize: {ex.Message}", LogCategory.RememberJoint, LogLevel.Error);
            }
        }

        private bool TryCaptureCurrentState(out JointData snapshot, bool logWarnings)
        {
            snapshot = null;

            if (targetJoint == null)
            {
                if (logWarnings)
                {
                    Logger.Log($"{nameof(RememberJoint)} requires a Joint component on the same GameObject.", LogCategory.RememberJoint, LogLevel.Warning);
                }
                return false;
            }

            bool anyPropertySelected = rememberConnectedBody || rememberAnchor || rememberAxis ||
                rememberAutoConfigureConnectedAnchor || rememberConnectedAnchor || rememberBreakForce ||
                rememberBreakTorque || rememberEnableCollision || rememberEnablePreprocessing ||
                rememberMassScale || rememberConnectedMassScale;

            if (!anyPropertySelected)
            {
                if (logWarnings)
                {
                    Logger.Log($"{nameof(RememberJoint)} has no properties selected for saving.", LogCategory.RememberJoint, LogLevel.Warning);
                }
                return false;
            }

            snapshot = new JointData
            {
                ConnectedBodyID = rememberConnectedBody && targetJoint.connectedBody != null ?
                    GameObjectUtilities.GetUniqueID(targetJoint.connectedBody.gameObject) : null,
                Anchor = rememberAnchor ? targetJoint.anchor : default,
                Axis = rememberAxis ? targetJoint.axis : default,
                AutoConfigureConnectedAnchor = rememberAutoConfigureConnectedAnchor ? targetJoint.autoConfigureConnectedAnchor : default,
                ConnectedAnchor = rememberConnectedAnchor ? targetJoint.connectedAnchor : default,
                BreakForce = rememberBreakForce ? targetJoint.breakForce : default,
                BreakTorque = rememberBreakTorque ? targetJoint.breakTorque : default,
                EnableCollision = rememberEnableCollision && targetJoint.enableCollision,
                EnablePreprocessing = rememberEnablePreprocessing && targetJoint.enablePreprocessing,
                MassScale = rememberMassScale ? targetJoint.massScale : default,
                ConnectedMassScale = rememberConnectedMassScale ? targetJoint.connectedMassScale : default
            };

            return true;
        }

        private bool AreEquivalent(JointData a, JointData b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;

            float sqrTolerance = FloatTolerance * FloatTolerance;

            return string.Equals(a.ConnectedBodyID, b.ConnectedBodyID, StringComparison.Ordinal) &&
                (a.Anchor - b.Anchor).sqrMagnitude <= sqrTolerance &&
                (a.Axis - b.Axis).sqrMagnitude <= sqrTolerance &&
                a.AutoConfigureConnectedAnchor == b.AutoConfigureConnectedAnchor &&
                (a.ConnectedAnchor - b.ConnectedAnchor).sqrMagnitude <= sqrTolerance &&
                Mathf.Approximately(a.BreakForce, b.BreakForce) &&
                Mathf.Approximately(a.BreakTorque, b.BreakTorque) &&
                a.EnableCollision == b.EnableCollision &&
                a.EnablePreprocessing == b.EnablePreprocessing &&
                Mathf.Approximately(a.MassScale, b.MassScale) &&
                Mathf.Approximately(a.ConnectedMassScale, b.ConnectedMassScale);
        }
    }

    [MemoryPackable]
    public partial class JointData
    {
        public string ConnectedBodyID { get; set; }
        public Vector3 Anchor { get; set; }
        public Vector3 Axis { get; set; }
        public bool AutoConfigureConnectedAnchor { get; set; }
        public Vector3 ConnectedAnchor { get; set; }
        public float BreakForce { get; set; }
        public float BreakTorque { get; set; }
        public bool EnableCollision { get; set; }
        public bool EnablePreprocessing { get; set; }
        public float MassScale { get; set; }
        public float ConnectedMassScale { get; set; }
    }
}
#endif
