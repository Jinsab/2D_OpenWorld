#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    [AddComponentMenu("Crystal Save/Remember Components/Remember Hinges")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HingeJoint))]
    [RememberTarget(typeof(HingeJoint))]
    public class RememberHinges : SaveableComponent
    {
        [Header("Hinge Joint Properties to Save")]
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
        [SerializeField] private bool rememberUseSpring = true;
        [SerializeField] private bool rememberSpring = true;
        [SerializeField] private bool rememberUseMotor = true;
        [SerializeField] private bool rememberMotor = true;
        [SerializeField] private bool rememberUseLimits = true;
        [SerializeField] private bool rememberLimits = true;

        [Header("Save Optimization")]
        [SerializeField] private bool skipSavingWhenUnchanged = false;

        private const float FloatTolerance = 0.0001f;
        private const float VectorToleranceSqr = 0.0001f;

        private HingeJoint hinge;
        private HingeJointData cachedSnapshot;
        private bool hasCachedSnapshot;
        private byte[] cachedSerializedData;

        protected override void Awake()
        {
            base.Awake();
            hinge = GetComponent<HingeJoint>();
            if (hinge == null)
            {
                Logger.Log($"{nameof(RememberHinges)} requires a HingeJoint component on the same GameObject.", LogCategory.RememberHinges, LogLevel.Error);
                enabled = false;
                hasCachedSnapshot = false;
                cachedSnapshot = null;
                return;
            }

            if (skipSavingWhenUnchanged && TryCaptureCurrentState(out var snapshot))
            {
                CacheSnapshot(snapshot);
            }
            else if (!skipSavingWhenUnchanged)
            {
                hasCachedSnapshot = false;
                cachedSnapshot = null;
            }
        }

        protected override byte[] SerializeComponentData()
        {
            if (!TryCaptureCurrentState(out var snapshot))
                return null;

            if (skipSavingWhenUnchanged && hasCachedSnapshot && AreEquivalent(cachedSnapshot, snapshot))
            {
                if (cachedSerializedData != null && cachedSerializedData.Length > 0)
                {
                    return cachedSerializedData;
                }
            }

            var data = SaveDataSerializer.Instance.Serialize(snapshot);

            if (skipSavingWhenUnchanged)
            {
                CacheSnapshot(snapshot);
                cachedSerializedData = data;
            }

            return data;
        }

        protected override void DeserializeComponentData(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || hinge == null) return;

            try
            {
                var data = SaveDataSerializer.Instance.Deserialize<HingeJointData>(bytes);
                if (data == null) return;

                if (rememberConnectedBody && !string.IsNullOrEmpty(data.ConnectedBodyID))
                {
                    GameObject go = SaveManager.Instance?.FindGameObjectByUniqueID(data.ConnectedBodyID, SaveManager.IdentifierType.UniqueID);
                    hinge.connectedBody = go != null ? go.GetComponent<Rigidbody>() : null;
                }
                if (rememberAnchor) hinge.anchor = data.Anchor;
                if (rememberAxis) hinge.axis = data.Axis;
                if (rememberAutoConfigureConnectedAnchor) hinge.autoConfigureConnectedAnchor = data.AutoConfigureConnectedAnchor;
                if (rememberConnectedAnchor) hinge.connectedAnchor = data.ConnectedAnchor;
                if (rememberBreakForce) hinge.breakForce = data.BreakForce;
                if (rememberBreakTorque) hinge.breakTorque = data.BreakTorque;
                if (rememberEnableCollision) hinge.enableCollision = data.EnableCollision;
                if (rememberEnablePreprocessing) hinge.enablePreprocessing = data.EnablePreprocessing;
                if (rememberMassScale) hinge.massScale = data.MassScale;
                if (rememberConnectedMassScale) hinge.connectedMassScale = data.ConnectedMassScale;
                if (rememberUseSpring) hinge.useSpring = data.UseSpring;
                if (rememberSpring) hinge.spring = data.Spring;
                if (rememberUseMotor) hinge.useMotor = data.UseMotor;
                if (rememberMotor) hinge.motor = data.Motor;
                if (rememberUseLimits) hinge.useLimits = data.UseLimits;
                if (rememberLimits) hinge.limits = data.Limits;

                if (skipSavingWhenUnchanged && TryCaptureCurrentState(out var refreshed))
                {
                    CacheSnapshot(refreshed);
                }
                else if (skipSavingWhenUnchanged)
                {
                    hasCachedSnapshot = false;
                    cachedSnapshot = null;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"{nameof(RememberHinges)} failed to deserialize: {ex.Message}", LogCategory.RememberHinges, LogLevel.Error);
            }
        }

        private bool TryCaptureCurrentState(out HingeJointData snapshot)
        {
            snapshot = null;

            if (hinge == null)
                return false;

            if (!HasAnyToggleEnabled())
                return false;

            snapshot = new HingeJointData();
            bool hasData = false;

            if (rememberConnectedBody)
            {
                snapshot.ConnectedBodyID = hinge.connectedBody != null ?
                    GameObjectUtilities.GetUniqueID(hinge.connectedBody.gameObject) : null;
                hasData = true;
            }

            if (rememberAnchor)
            {
                snapshot.Anchor = hinge.anchor;
                hasData = true;
            }

            if (rememberAxis)
            {
                snapshot.Axis = hinge.axis;
                hasData = true;
            }

            if (rememberAutoConfigureConnectedAnchor)
            {
                snapshot.AutoConfigureConnectedAnchor = hinge.autoConfigureConnectedAnchor;
                hasData = true;
            }

            if (rememberConnectedAnchor)
            {
                snapshot.ConnectedAnchor = hinge.connectedAnchor;
                hasData = true;
            }

            if (rememberBreakForce)
            {
                snapshot.BreakForce = hinge.breakForce;
                hasData = true;
            }

            if (rememberBreakTorque)
            {
                snapshot.BreakTorque = hinge.breakTorque;
                hasData = true;
            }

            if (rememberEnableCollision)
            {
                snapshot.EnableCollision = hinge.enableCollision;
                hasData = true;
            }

            if (rememberEnablePreprocessing)
            {
                snapshot.EnablePreprocessing = hinge.enablePreprocessing;
                hasData = true;
            }

            if (rememberMassScale)
            {
                snapshot.MassScale = hinge.massScale;
                hasData = true;
            }

            if (rememberConnectedMassScale)
            {
                snapshot.ConnectedMassScale = hinge.connectedMassScale;
                hasData = true;
            }

            if (rememberUseSpring)
            {
                snapshot.UseSpring = hinge.useSpring;
                hasData = true;
            }

            if (rememberSpring)
            {
                snapshot.Spring = hinge.spring;
                hasData = true;
            }

            if (rememberUseMotor)
            {
                snapshot.UseMotor = hinge.useMotor;
                hasData = true;
            }

            if (rememberMotor)
            {
                snapshot.Motor = hinge.motor;
                hasData = true;
            }

            if (rememberUseLimits)
            {
                snapshot.UseLimits = hinge.useLimits;
                hasData = true;
            }

            if (rememberLimits)
            {
                snapshot.Limits = hinge.limits;
                hasData = true;
            }

            if (!hasData)
            {
                snapshot = null;
                return false;
            }

            return true;
        }

        private bool HasAnyToggleEnabled()
        {
            return rememberConnectedBody || rememberAnchor || rememberAxis || rememberAutoConfigureConnectedAnchor ||
                   rememberConnectedAnchor || rememberBreakForce || rememberBreakTorque || rememberEnableCollision ||
                   rememberEnablePreprocessing || rememberMassScale || rememberConnectedMassScale || rememberUseSpring ||
                   rememberSpring || rememberUseMotor || rememberMotor || rememberUseLimits || rememberLimits;
        }

        private bool AreEquivalent(HingeJointData a, HingeJointData b)
        {
            if (a == null || b == null)
                return false;

            if (!string.Equals(a.ConnectedBodyID, b.ConnectedBodyID, StringComparison.Ordinal))
                return false;

            if (!AreVectorsApproximatelyEqual(a.Anchor, b.Anchor))
                return false;

            if (!AreVectorsApproximatelyEqual(a.Axis, b.Axis))
                return false;

            if (a.AutoConfigureConnectedAnchor != b.AutoConfigureConnectedAnchor)
                return false;

            if (!AreVectorsApproximatelyEqual(a.ConnectedAnchor, b.ConnectedAnchor))
                return false;

            if (!AreFloatsApproximatelyEqual(a.BreakForce, b.BreakForce))
                return false;

            if (!AreFloatsApproximatelyEqual(a.BreakTorque, b.BreakTorque))
                return false;

            if (a.EnableCollision != b.EnableCollision)
                return false;

            if (a.EnablePreprocessing != b.EnablePreprocessing)
                return false;

            if (!AreFloatsApproximatelyEqual(a.MassScale, b.MassScale))
                return false;

            if (!AreFloatsApproximatelyEqual(a.ConnectedMassScale, b.ConnectedMassScale))
                return false;

            if (a.UseSpring != b.UseSpring)
                return false;

            if (!AreJointSpringsEquivalent(a.Spring, b.Spring))
                return false;

            if (a.UseMotor != b.UseMotor)
                return false;

            if (!AreJointMotorsEquivalent(a.Motor, b.Motor))
                return false;

            if (a.UseLimits != b.UseLimits)
                return false;

            if (!AreJointLimitsEquivalent(a.Limits, b.Limits))
                return false;

            return true;
        }

        private bool AreVectorsApproximatelyEqual(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude <= VectorToleranceSqr;
        }

        private bool AreFloatsApproximatelyEqual(float a, float b)
        {
            if (float.IsNaN(a) || float.IsNaN(b))
                return float.IsNaN(a) && float.IsNaN(b);

            if (float.IsInfinity(a) || float.IsInfinity(b))
                return float.IsInfinity(a) == float.IsInfinity(b) && Mathf.Sign(a) == Mathf.Sign(b);

            return Mathf.Abs(a - b) <= FloatTolerance;
        }

        private bool AreJointSpringsEquivalent(JointSpring a, JointSpring b)
        {
            return AreFloatsApproximatelyEqual(a.spring, b.spring) &&
                   AreFloatsApproximatelyEqual(a.damper, b.damper) &&
                   AreFloatsApproximatelyEqual(a.targetPosition, b.targetPosition);
        }

        private bool AreJointMotorsEquivalent(JointMotor a, JointMotor b)
        {
            return AreFloatsApproximatelyEqual(a.force, b.force) &&
                   AreFloatsApproximatelyEqual(a.targetVelocity, b.targetVelocity) &&
                   a.freeSpin == b.freeSpin;
        }

        private bool AreJointLimitsEquivalent(JointLimits a, JointLimits b)
        {
            return AreFloatsApproximatelyEqual(a.min, b.min) &&
                   AreFloatsApproximatelyEqual(a.max, b.max) &&
                   AreFloatsApproximatelyEqual(a.bounciness, b.bounciness) &&
                   AreFloatsApproximatelyEqual(a.bounceMinVelocity, b.bounceMinVelocity) &&
                   AreFloatsApproximatelyEqual(a.contactDistance, b.contactDistance);
        }

        private void CacheSnapshot(HingeJointData snapshot)
        {
            if (snapshot == null)
            {
                cachedSnapshot = null;
                hasCachedSnapshot = false;
                return;
            }

            cachedSnapshot = CloneSnapshot(snapshot);
            hasCachedSnapshot = cachedSnapshot != null;
        }

        private HingeJointData CloneSnapshot(HingeJointData source)
        {
            if (source == null)
                return null;

            return new HingeJointData
            {
                ConnectedBodyID = source.ConnectedBodyID,
                Anchor = source.Anchor,
                Axis = source.Axis,
                AutoConfigureConnectedAnchor = source.AutoConfigureConnectedAnchor,
                ConnectedAnchor = source.ConnectedAnchor,
                BreakForce = source.BreakForce,
                BreakTorque = source.BreakTorque,
                EnableCollision = source.EnableCollision,
                EnablePreprocessing = source.EnablePreprocessing,
                MassScale = source.MassScale,
                ConnectedMassScale = source.ConnectedMassScale,
                UseSpring = source.UseSpring,
                Spring = source.Spring,
                UseMotor = source.UseMotor,
                Motor = source.Motor,
                UseLimits = source.UseLimits,
                Limits = source.Limits
            };
        }
    }

    [MemoryPackable]
    public partial class HingeJointData
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
        public bool UseSpring { get; set; }
        public JointSpring Spring { get; set; }
        public bool UseMotor { get; set; }
        public JointMotor Motor { get; set; }
        public bool UseLimits { get; set; }
        public JointLimits Limits { get; set; }
    }
}
#endif
