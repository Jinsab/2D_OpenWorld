#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using UnityEngine;
using UnityEngine.AI;

namespace Arawn.CrystalSave.Runtime
{
	[AddComponentMenu("Crystal Save/Remember Components/Remember Rigidbody")]
	[DisallowMultipleComponent]
	[RememberTarget(typeof(Rigidbody))]
	public class RememberRigidbody : SaveableComponent
	{
        [Header("Performance")]
        [Tooltip("Enable lightweight caching of the Rigidbody reference to avoid repeated GetComponent calls.")]
        [SerializeField] private bool enablePerformanceCaching = false;

        [Header("Save Optimization")]
        [SerializeField] private bool skipSavingWhenUnchanged;

        private RememberRigidbodyData cachedSnapshot;
        private bool hasCachedSnapshot;

        private const float FloatTolerance = 0.0001f;
        private const float VectorToleranceSqr = 0.0001f;

                [Header("Rigidbody Properties to Save")]

		[Tooltip("Serialize whether the Rigidbody is kinematic.")]
		[SerializeField]
		private bool rememberIsKinematic = true;

		[Tooltip("Serialize whether the Rigidbody uses gravity.")]
		[SerializeField]
		private bool rememberUseGravity = true;

		[Tooltip("Serialize the Rigidbody's mass.")]
		[SerializeField]
		private bool rememberMass = true;

		[Tooltip("Serialize the Rigidbody's linear drag.")]
		[SerializeField]
		private bool rememberDrag = true;

		[Tooltip("Serialize the Rigidbody's angular drag.")]
		[SerializeField]
		private bool rememberAngularDrag = true;

		[Tooltip("Serialize the Rigidbody's constraints.")]
		[SerializeField]
		private bool rememberConstraints = true;

		[Tooltip("Serialize the Rigidbody's linear velocity.")]
		[SerializeField]
		private bool rememberVelocity = true;

		[Tooltip("Serialize the Rigidbody's angular velocity.")]
		[SerializeField]
		private bool rememberAngularVelocity = true;

		[Tooltip("Serialize whether the Rigidbody detects collisions.")]
		[SerializeField]
		private bool rememberDetectCollisions = true;

		private Rigidbody targetRigidbody;
		private byte[] cachedSerializedData;

		protected override void Awake()
		{
			base.Awake();
			// Always get the Rigidbody component, regardless of caching setting
			targetRigidbody = GetComponent<Rigidbody>();

                        if (targetRigidbody == null)
                        {
                                Logger.Log($"{nameof(RememberRigidbody)} requires a Rigidbody component on the same GameObject. None was found on '{gameObject.name}'.", gameObject, LogCategory.RememberRigidbody, LogLevel.Error);
                                cachedSnapshot = null;
                                hasCachedSnapshot = false;
                                return;
                        }

                        if (skipSavingWhenUnchanged && TryCaptureCurrentState(out var initialSnapshot))
                        {
                                cachedSnapshot = CloneSnapshot(initialSnapshot);
                                hasCachedSnapshot = true;
                        }
                        else
                        {
                                cachedSnapshot = null;
                                hasCachedSnapshot = false;
                        }
                }

                /// <summary>
                /// Serializes the Rigidbody's data into a byte array using MemoryPack.
                /// </summary>
                protected override byte[] SerializeComponentData()
                {
                        if (!TryCaptureCurrentState(out var snapshot))
                        {
                                return null;
                        }

                        if (skipSavingWhenUnchanged && hasCachedSnapshot && AreEquivalent(cachedSnapshot, snapshot))
                        {
                                if (cachedSerializedData != null && cachedSerializedData.Length > 0)
                                {
                                        return cachedSerializedData;
                                }
                        }

                        try
                        {
                                var bytes = SaveDataSerializer.Instance.Serialize(snapshot);

                                if (skipSavingWhenUnchanged)
                                {
                                        cachedSnapshot = CloneSnapshot(snapshot);
                                        hasCachedSnapshot = true;
                                        cachedSerializedData = bytes;
                                }

                                return bytes;
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"Serialization failed for '{gameObject.name}': {ex.Message}", gameObject, LogCategory.RememberRigidbody, LogLevel.Error);
                                return null;
                        }
                }

                protected override void DeserializeComponentData(byte[] bytes)
                {
			if (bytes == null || bytes.Length == 0)
			{
                                Logger.Log("DeserializeComponentData failed: Data is null or empty.", gameObject, LogCategory.RememberRigidbody, LogLevel.Warning);
                                return;
                        }

                        // Use cached reference if caching enabled AND cached reference is valid
                        // If cached reference is null, re-fetch the component (handles RememberHomeScene timing issues)
                        Rigidbody rigidbody = enablePerformanceCaching && targetRigidbody != null ? targetRigidbody : GetComponent<Rigidbody>();
			
			if (rigidbody == null)
			{
				Logger.Log("DeserializeComponentData failed: Rigidbody component not found.", gameObject, LogCategory.RememberRigidbody, LogLevel.Warning);
				return;
			}
			
			// Update cached reference if we had to re-fetch
			if (enablePerformanceCaching && targetRigidbody == null)
			{
				targetRigidbody = rigidbody;
			}

		try
		{
			var data = SaveDataSerializer.Instance.Deserialize<RememberRigidbodyData>(bytes);
			if (data == null)
			{
				Logger.Log("Deserialization resulted in null or empty data.", gameObject, LogCategory.RememberRigidbody, LogLevel.Warning);
				return;
			}

			// Check if we're in TimeMachine playback mode
#if CRYSTALSAVE_TIMEMACHINE
			bool isTimeMachinePlayback = TimeMachine.TimeMachinePlaybackContext.HasSample;
#else
			bool isTimeMachinePlayback = false;
#endif

			// During TimeMachine playback: Force kinematic to prevent physics conflicts with transform restore
			// This ensures the replayed positions are exact without physics simulation interference
			if (isTimeMachinePlayback)
			{
				if (rememberIsKinematic)
				{
					// Always make kinematic during playback to disable physics simulation
					rigidbody.isKinematic = true;
				}
			}
			else
			{
				// Normal save/load: Restore the saved kinematic state
				if (rememberIsKinematic)
					rigidbody.isKinematic = data.IsKinematic;
			}

			if (rememberUseGravity)
				rigidbody.useGravity = data.UseGravity;

			if (rememberMass)
				rigidbody.mass = data.Mass;

			if (rememberDrag)
			{
#if UNITY_6000_0_OR_NEWER
				rigidbody.linearDamping = data.Drag;
#else
				rigidbody.drag = data.Drag;
#endif
			}

			if (rememberAngularDrag)
			{
#if UNITY_6000_0_OR_NEWER
				rigidbody.angularDamping = data.AngularDrag;
#else
				rigidbody.angularDrag = data.AngularDrag;
#endif
			}

			if (rememberConstraints)
				rigidbody.constraints = data.Constraints;

			// velocity ↔ linearVelocity
			// During TimeMachine playback, don't restore velocity since rigidbody is kinematic
			// Velocity is only restored during normal save/load
			if (rememberVelocity && !rigidbody.isKinematic)
			{
#if UNITY_6000_0_OR_NEWER
				rigidbody.linearVelocity = data.Velocity;
#else
				rigidbody.velocity = data.Velocity;
#endif
			}

			// angularVelocity is unchanged
			if (rememberAngularVelocity && !rigidbody.isKinematic)
				rigidbody.angularVelocity = data.AngularVelocity;

                        if (rememberDetectCollisions)
                                rigidbody.detectCollisions = data.DetectCollisions;                                if (skipSavingWhenUnchanged && TryCaptureCurrentState(out var refreshedSnapshot))
                                {
                                        cachedSnapshot = CloneSnapshot(refreshedSnapshot);
                                        hasCachedSnapshot = true;
                                }

                                Logger.Log($"Successfully loaded Rigidbody data for GameObject '{gameObject.name}'.", LogCategory.RememberRigidbody, LogLevel.Info);
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"Deserialization failed for '{gameObject.name}': {ex.Message}", LogCategory.RememberRigidbody, LogLevel.Error);
                        }
                }

                private bool TryCaptureCurrentState(out RememberRigidbodyData snapshot)
                {
                        snapshot = null;

                        // Use cached reference if caching enabled AND cached reference is valid
                        // If cached reference is null, re-fetch the component (handles RememberHomeScene timing issues)
                        Rigidbody rigidbody = enablePerformanceCaching && targetRigidbody != null
                                ? targetRigidbody
                                : GetComponent<Rigidbody>();

                        if (rigidbody == null)
                        {
                                Logger.Log("TryCaptureCurrentState failed: Rigidbody component not found.", gameObject, LogCategory.RememberRigidbody, LogLevel.Warning);
                                return false;
                        }
                        
                        // Update cached reference if we had to re-fetch
                        if (enablePerformanceCaching && targetRigidbody == null)
                        {
                                targetRigidbody = rigidbody;
                        }

                        if (!rememberIsKinematic && !rememberUseGravity && !rememberMass && !rememberDrag && !rememberAngularDrag &&
                            !rememberConstraints && !rememberVelocity && !rememberAngularVelocity && !rememberDetectCollisions)
                        {
                                return false;
                        }

                        var data = new RememberRigidbodyData();

                        if (rememberIsKinematic)
                                data.IsKinematic = rigidbody.isKinematic;

                        if (rememberUseGravity)
                                data.UseGravity = rigidbody.useGravity;

                        if (rememberMass)
                                data.Mass = rigidbody.mass;

                        if (rememberDrag)
                        {
#if UNITY_6000_0_OR_NEWER
                                data.Drag = rigidbody.linearDamping;
#else
                                data.Drag = rigidbody.drag;
#endif
                        }

                        if (rememberAngularDrag)
                        {
#if UNITY_6000_0_OR_NEWER
                                data.AngularDrag = rigidbody.angularDamping;
#else
                                data.AngularDrag = rigidbody.angularDrag;
#endif
                        }

                        if (rememberConstraints)
                                data.Constraints = rigidbody.constraints;

                        if (rememberVelocity)
                        {
#if UNITY_6000_0_OR_NEWER
                                data.Velocity = rigidbody.linearVelocity;
#else
                                data.Velocity = rigidbody.velocity;
#endif
                        }

                        if (rememberAngularVelocity)
                                data.AngularVelocity = rigidbody.angularVelocity;

                        if (rememberDetectCollisions)
                                data.DetectCollisions = rigidbody.detectCollisions;

                        snapshot = data;
                        return true;
                }

                private bool AreEquivalent(RememberRigidbodyData first, RememberRigidbodyData second)
                {
                        if (first == null || second == null)
                                return false;

                        if (rememberIsKinematic && first.IsKinematic != second.IsKinematic)
                                return false;

                        if (rememberUseGravity && first.UseGravity != second.UseGravity)
                                return false;

                        if (rememberMass && !AreFloatsEquivalent(first.Mass, second.Mass))
                                return false;

                        if (rememberDrag && !AreFloatsEquivalent(first.Drag, second.Drag))
                                return false;

                        if (rememberAngularDrag && !AreFloatsEquivalent(first.AngularDrag, second.AngularDrag))
                                return false;

                        if (rememberConstraints && first.Constraints != second.Constraints)
                                return false;

                        if (rememberVelocity && !AreVectorsEquivalent(first.Velocity, second.Velocity))
                                return false;

                        if (rememberAngularVelocity && !AreVectorsEquivalent(first.AngularVelocity, second.AngularVelocity))
                                return false;

                        if (rememberDetectCollisions && first.DetectCollisions != second.DetectCollisions)
                                return false;

                        return true;
                }

                private static bool AreFloatsEquivalent(float a, float b)
                {
                        return Mathf.Approximately(a, b) || Mathf.Abs(a - b) <= FloatTolerance;
                }

                private static bool AreVectorsEquivalent(Vector3 a, Vector3 b)
                {
                        return (a - b).sqrMagnitude <= VectorToleranceSqr;
                }

                private static RememberRigidbodyData CloneSnapshot(RememberRigidbodyData source)
                {
                        if (source == null)
                                return null;

                        return new RememberRigidbodyData
                        {
                                IsKinematic = source.IsKinematic,
                                UseGravity = source.UseGravity,
                                Mass = source.Mass,
                                Drag = source.Drag,
                                AngularDrag = source.AngularDrag,
                                Constraints = source.Constraints,
                                Velocity = source.Velocity,
                                AngularVelocity = source.AngularVelocity,
                                DetectCollisions = source.DetectCollisions
                        };
                }

	}
	[MemoryPackable]
	public partial class RememberRigidbodyData : IMemoryPackable<RememberRigidbodyData>
	{
		public bool IsKinematic { get; set; }
		public bool UseGravity { get; set; }
		public float Mass { get; set; }
		public float Drag { get; set; }
		public float AngularDrag { get; set; }
		public RigidbodyConstraints Constraints { get; set; }
		public Vector3 Velocity { get; set; }
		public Vector3 AngularVelocity { get; set; }
		public bool DetectCollisions { get; set; }
	}
}
#endif
