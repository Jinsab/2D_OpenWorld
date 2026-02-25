#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Arawn.CrystalSave.Runtime
{
	[AddComponentMenu("Crystal Save/Remember Components/Remember NavMeshAgent")]
	[DisallowMultipleComponent]
	[RememberTarget(typeof(NavMeshAgent))]
	public class RememberNavmeshAgent : SaveableComponent
	{
		[Header("NavMeshAgent Properties to Save")]

		[Tooltip("Serialize whether the NavMeshAgent component is enabled.")]
		[SerializeField]
		private bool rememberEnabled = true;

		[Tooltip("Serialize the NavMeshAgent's destination.")]
		[SerializeField]
		private bool rememberDestination = true;

		[Tooltip("Serialize the NavMeshAgent's speed.")]
		[SerializeField]
		private bool rememberSpeed = true;

		[Tooltip("Serialize the NavMeshAgent's angular speed.")]
		[SerializeField]
		private bool rememberAngularSpeed = true;

		[Tooltip("Serialize the NavMeshAgent's acceleration.")]
		[SerializeField]
		private bool rememberAcceleration = true;

		[Tooltip("Serialize the NavMeshAgent's stopping distance.")]
		[SerializeField]
		private bool rememberStoppingDistance = true;

		[Tooltip("Serialize whether the NavMeshAgent updates its position automatically.")]
		[SerializeField]
		private bool rememberUpdatePosition = true;

		[Tooltip("Serialize whether the NavMeshAgent updates its rotation automatically.")]
		[SerializeField]
		private bool rememberUpdateRotation = true;

		[Tooltip("Serialize the NavMeshAgent's radius.")]
		[SerializeField]
		private bool rememberRadius = true;

		[Tooltip("Serialize the NavMeshAgent's height.")]
		[SerializeField]
		private bool rememberHeight = true;

		[Tooltip("Serialize the NavMeshAgent's obstacle avoidance type.")]
		[SerializeField]
		private bool rememberObstacleAvoidanceType = true;

		[Tooltip("Serialize the NavMeshAgent's avoidance priority.")]
		[SerializeField]
		private bool rememberAvoidancePriority = true;

		[Tooltip("Serialize whether the NavMeshAgent is stopped.")]
		[SerializeField]
		private bool rememberIsStopped = true;

		[Tooltip("Serialize whether the NavMeshAgent automatically traverses off-mesh links.")]
		[SerializeField]
		private bool rememberAutoTraverseOffMeshLink = true;

                [Tooltip("Serialize the NavMeshAgent's base offset.")]
                [SerializeField]
                private bool rememberBaseOffset = true;

                [Header("Save Optimization")]
                [SerializeField]
                private bool skipSavingWhenUnchanged;

                private NavMeshAgent targetNavMeshAgent;
                private NavMeshAgentSnapshot cachedSnapshot;
                private bool hasCachedSnapshot;
                private byte[] cachedSerializedData;

                private const float FloatTolerance = 0.0001f;

		protected override void Awake()
		{
			base.Awake();
                        targetNavMeshAgent = GetComponent<NavMeshAgent>();

                        if (targetNavMeshAgent == null)
                        {
                                Logger.Log($"{nameof(RememberNavmeshAgent)} requires a NavMeshAgent component on the same GameObject. None was found on '{gameObject.name}'.", LogCategory.RememberNavmeshAgent, LogLevel.Error);
                                hasCachedSnapshot = false;
                                cachedSnapshot = default;
                                return;
                        }

                        if (skipSavingWhenUnchanged && TryCaptureCurrentState(out var snapshot))
                        {
                                cachedSnapshot = snapshot;
                                hasCachedSnapshot = true;
                        }
                        else
                        {
                                hasCachedSnapshot = false;
                                cachedSnapshot = default;
                        }
                }

                /// <summary>
                /// Serializes the NavMeshAgent's data into a byte array using MemoryPack.
                /// </summary>
                protected override byte[] SerializeComponentData()
                {
                        if (targetNavMeshAgent == null)
                        {
                                Logger.Log("SerializeComponentData failed: NavMeshAgent component not found.", LogCategory.RememberNavmeshAgent, LogLevel.Warning);
                                return null;
                        }

                        if (!TryCaptureCurrentState(out var snapshot))
                        {
                                return null;
                        }

                        if (skipSavingWhenUnchanged && hasCachedSnapshot && AreEquivalent(snapshot, cachedSnapshot))
                        {
                                if (cachedSerializedData != null && cachedSerializedData.Length > 0)
                                {
                                        return cachedSerializedData;
                                }
                        }

                        try
                        {
                                var data = new RememberNavmeshAgentData
                                {
                                        Enabled = snapshot.Enabled,
                                        Destination = snapshot.Destination,
                                        Speed = snapshot.Speed,
                                        AngularSpeed = snapshot.AngularSpeed,
                                        Acceleration = snapshot.Acceleration,
                                        StoppingDistance = snapshot.StoppingDistance,
                                        UpdatePosition = snapshot.UpdatePosition,
                                        UpdateRotation = snapshot.UpdateRotation,
                                        Radius = snapshot.Radius,
                                        Height = snapshot.Height,
                                        ObstacleAvoidanceType = snapshot.ObstacleAvoidanceType,
                                        AvoidancePriority = snapshot.AvoidancePriority,
                                        IsStopped = snapshot.IsStopped,
                                        AutoTraverseOffMeshLink = snapshot.AutoTraverseOffMeshLink,
                                        BaseOffset = snapshot.BaseOffset
                                };

                                var serialized = SaveDataSerializer.Instance.Serialize(data);

                                if (skipSavingWhenUnchanged)
                                {
                                        cachedSnapshot = snapshot;
                                        hasCachedSnapshot = true;
                                        cachedSerializedData = serialized;
                                }

                                return serialized;
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"Serialization failed for '{gameObject.name}': {ex.Message}", LogCategory.RememberNavmeshAgent, LogLevel.Error);
                                return null;
                        }
                }

		/// <summary>
		/// Deserializes the data from a byte array and applies it to the NavMeshAgent.
		/// </summary>
		protected override void DeserializeComponentData(byte[] data)
		{
                        if (data == null || data.Length == 0)
                        {
                                Logger.Log("DeserializeComponentData failed: Data is null or empty.", LogCategory.RememberNavmeshAgent, LogLevel.Warning);
                                return;
                        }

			if (targetNavMeshAgent == null)
			{
				Logger.Log("DeserializeComponentData failed: NavMeshAgent component not found.", LogCategory.RememberNavmeshAgent, LogLevel.Warning);
				return;
			}

			try
			{
				var deserializedData = SaveDataSerializer.Instance.Deserialize<RememberNavmeshAgentData>(data);

				if (deserializedData == null)
				{
					Logger.Log("Deserialization resulted in null or empty data.", LogCategory.RememberNavmeshAgent, LogLevel.Warning);
					return;
				}

				if (rememberEnabled)
				{
					targetNavMeshAgent.enabled = deserializedData.Enabled;
				}

				if (rememberDestination)
				{
					targetNavMeshAgent.destination = deserializedData.Destination;
				}

				if (rememberSpeed)
				{
					targetNavMeshAgent.speed = deserializedData.Speed;
				}

				if (rememberAngularSpeed)
				{
					targetNavMeshAgent.angularSpeed = deserializedData.AngularSpeed;
				}

				if (rememberAcceleration)
				{
					targetNavMeshAgent.acceleration = deserializedData.Acceleration;
				}

				if (rememberStoppingDistance)
				{
					targetNavMeshAgent.stoppingDistance = deserializedData.StoppingDistance;
				}

				if (rememberUpdatePosition)
				{
					targetNavMeshAgent.updatePosition = deserializedData.UpdatePosition;
				}

				if (rememberUpdateRotation)
				{
					targetNavMeshAgent.updateRotation = deserializedData.UpdateRotation;
				}

				if (rememberRadius)
				{
					targetNavMeshAgent.radius = deserializedData.Radius;
				}

				if (rememberHeight)
				{
					targetNavMeshAgent.height = deserializedData.Height;
				}

				if (rememberObstacleAvoidanceType)
				{
					targetNavMeshAgent.obstacleAvoidanceType = deserializedData.ObstacleAvoidanceType;
				}

				if (rememberAvoidancePriority)
				{
					targetNavMeshAgent.avoidancePriority = deserializedData.AvoidancePriority;
				}

				if (rememberIsStopped)
				{
					targetNavMeshAgent.isStopped = deserializedData.IsStopped;
				}

				if (rememberAutoTraverseOffMeshLink)
				{
					targetNavMeshAgent.autoTraverseOffMeshLink = deserializedData.AutoTraverseOffMeshLink;
				}

                                if (rememberBaseOffset)
                                {
                                        targetNavMeshAgent.baseOffset = deserializedData.BaseOffset;
                                }

                                // Removed WarpPosition deserialization

                                Logger.Log($"Successfully loaded NavMeshAgent data for GameObject '{gameObject.name}'.", LogCategory.RememberNavmeshAgent, LogLevel.Info);

                                if (skipSavingWhenUnchanged)
                                {
                                        if (TryCaptureCurrentState(out var snapshot))
                                        {
                                                cachedSnapshot = snapshot;
                                                hasCachedSnapshot = true;
                                        }
                                        else
                                        {
                                                hasCachedSnapshot = false;
                                                cachedSnapshot = default;
                                        }
                                }
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"Deserialization failed for '{gameObject.name}': {ex.Message}", LogCategory.RememberNavmeshAgent, LogLevel.Error);
                        }
                }

                private bool TryCaptureCurrentState(out NavMeshAgentSnapshot snapshot)
                {
                        snapshot = default;

                        if (targetNavMeshAgent == null)
                        {
                                return false;
                        }

                        var anyCaptured = false;

                        if (rememberEnabled)
                        {
                                snapshot.Enabled = targetNavMeshAgent.enabled;
                                snapshot.HasEnabled = true;
                                anyCaptured = true;
                        }

                        if (rememberDestination)
                        {
                                snapshot.Destination = targetNavMeshAgent.destination;
                                snapshot.HasDestination = true;
                                anyCaptured = true;
                        }

                        if (rememberSpeed)
                        {
                                snapshot.Speed = targetNavMeshAgent.speed;
                                snapshot.HasSpeed = true;
                                anyCaptured = true;
                        }

                        if (rememberAngularSpeed)
                        {
                                snapshot.AngularSpeed = targetNavMeshAgent.angularSpeed;
                                snapshot.HasAngularSpeed = true;
                                anyCaptured = true;
                        }

                        if (rememberAcceleration)
                        {
                                snapshot.Acceleration = targetNavMeshAgent.acceleration;
                                snapshot.HasAcceleration = true;
                                anyCaptured = true;
                        }

                        if (rememberStoppingDistance)
                        {
                                snapshot.StoppingDistance = targetNavMeshAgent.stoppingDistance;
                                snapshot.HasStoppingDistance = true;
                                anyCaptured = true;
                        }

                        if (rememberUpdatePosition)
                        {
                                snapshot.UpdatePosition = targetNavMeshAgent.updatePosition;
                                snapshot.HasUpdatePosition = true;
                                anyCaptured = true;
                        }

                        if (rememberUpdateRotation)
                        {
                                snapshot.UpdateRotation = targetNavMeshAgent.updateRotation;
                                snapshot.HasUpdateRotation = true;
                                anyCaptured = true;
                        }

                        if (rememberRadius)
                        {
                                snapshot.Radius = targetNavMeshAgent.radius;
                                snapshot.HasRadius = true;
                                anyCaptured = true;
                        }

                        if (rememberHeight)
                        {
                                snapshot.Height = targetNavMeshAgent.height;
                                snapshot.HasHeight = true;
                                anyCaptured = true;
                        }

                        if (rememberObstacleAvoidanceType)
                        {
                                snapshot.ObstacleAvoidanceType = targetNavMeshAgent.obstacleAvoidanceType;
                                snapshot.HasObstacleAvoidanceType = true;
                                anyCaptured = true;
                        }

                        if (rememberAvoidancePriority)
                        {
                                snapshot.AvoidancePriority = targetNavMeshAgent.avoidancePriority;
                                snapshot.HasAvoidancePriority = true;
                                anyCaptured = true;
                        }

                        if (rememberIsStopped)
                        {
                                snapshot.IsStopped = targetNavMeshAgent.isStopped;
                                snapshot.HasIsStopped = true;
                                anyCaptured = true;
                        }

                        if (rememberAutoTraverseOffMeshLink)
                        {
                                snapshot.AutoTraverseOffMeshLink = targetNavMeshAgent.autoTraverseOffMeshLink;
                                snapshot.HasAutoTraverseOffMeshLink = true;
                                anyCaptured = true;
                        }

                        if (rememberBaseOffset)
                        {
                                snapshot.BaseOffset = targetNavMeshAgent.baseOffset;
                                snapshot.HasBaseOffset = true;
                                anyCaptured = true;
                        }

                        return anyCaptured;
                }

                private static bool AreEquivalent(in NavMeshAgentSnapshot a, in NavMeshAgentSnapshot b)
                {
                        if (!CompareBools(a.HasEnabled, b.HasEnabled, a.Enabled, b.Enabled))
                        {
                                return false;
                        }

                        if (!CompareVectors(a.HasDestination, b.HasDestination, a.Destination, b.Destination))
                        {
                                return false;
                        }

                        if (!CompareFloats(a.HasSpeed, b.HasSpeed, a.Speed, b.Speed))
                        {
                                return false;
                        }

                        if (!CompareFloats(a.HasAngularSpeed, b.HasAngularSpeed, a.AngularSpeed, b.AngularSpeed))
                        {
                                return false;
                        }

                        if (!CompareFloats(a.HasAcceleration, b.HasAcceleration, a.Acceleration, b.Acceleration))
                        {
                                return false;
                        }

                        if (!CompareFloats(a.HasStoppingDistance, b.HasStoppingDistance, a.StoppingDistance, b.StoppingDistance))
                        {
                                return false;
                        }

                        if (!CompareBools(a.HasUpdatePosition, b.HasUpdatePosition, a.UpdatePosition, b.UpdatePosition))
                        {
                                return false;
                        }

                        if (!CompareBools(a.HasUpdateRotation, b.HasUpdateRotation, a.UpdateRotation, b.UpdateRotation))
                        {
                                return false;
                        }

                        if (!CompareFloats(a.HasRadius, b.HasRadius, a.Radius, b.Radius))
                        {
                                return false;
                        }

                        if (!CompareFloats(a.HasHeight, b.HasHeight, a.Height, b.Height))
                        {
                                return false;
                        }

                        if (!CompareEnums(a.HasObstacleAvoidanceType, b.HasObstacleAvoidanceType, a.ObstacleAvoidanceType, b.ObstacleAvoidanceType))
                        {
                                return false;
                        }

                        if (!CompareInts(a.HasAvoidancePriority, b.HasAvoidancePriority, a.AvoidancePriority, b.AvoidancePriority))
                        {
                                return false;
                        }

                        if (!CompareBools(a.HasIsStopped, b.HasIsStopped, a.IsStopped, b.IsStopped))
                        {
                                return false;
                        }

                        if (!CompareBools(a.HasAutoTraverseOffMeshLink, b.HasAutoTraverseOffMeshLink, a.AutoTraverseOffMeshLink, b.AutoTraverseOffMeshLink))
                        {
                                return false;
                        }

                        if (!CompareFloats(a.HasBaseOffset, b.HasBaseOffset, a.BaseOffset, b.BaseOffset))
                        {
                                return false;
                        }

                        return true;
                }

                private static bool CompareBools(bool hasA, bool hasB, bool valueA, bool valueB)
                {
                        if (!hasA && !hasB)
                        {
                                return true;
                        }

                        if (hasA != hasB)
                        {
                                return false;
                        }

                        return valueA == valueB;
                }

                private static bool CompareFloats(bool hasA, bool hasB, float valueA, float valueB)
                {
                        if (!hasA && !hasB)
                        {
                                return true;
                        }

                        if (hasA != hasB)
                        {
                                return false;
                        }

                        return Mathf.Approximately(valueA, valueB);
                }

                private static bool CompareVectors(bool hasA, bool hasB, Vector3 valueA, Vector3 valueB)
                {
                        if (!hasA && !hasB)
                        {
                                return true;
                        }

                        if (hasA != hasB)
                        {
                                return false;
                        }

                        return (valueA - valueB).sqrMagnitude <= FloatTolerance;
                }

                private static bool CompareEnums<T>(bool hasA, bool hasB, T valueA, T valueB) where T : struct
                {
                        if (!hasA && !hasB)
                        {
                                return true;
                        }

                        if (hasA != hasB)
                        {
                                return false;
                        }

                        return EqualityComparer<T>.Default.Equals(valueA, valueB);
                }

                private static bool CompareInts(bool hasA, bool hasB, int valueA, int valueB)
                {
                        if (!hasA && !hasB)
                        {
                                return true;
                        }

                        if (hasA != hasB)
                        {
                                return false;
                        }

                        return valueA == valueB;
                }

                private struct NavMeshAgentSnapshot
                {
                        public bool HasEnabled;
                        public bool Enabled;

                        public bool HasDestination;
                        public Vector3 Destination;

                        public bool HasSpeed;
                        public float Speed;

                        public bool HasAngularSpeed;
                        public float AngularSpeed;

                        public bool HasAcceleration;
                        public float Acceleration;

                        public bool HasStoppingDistance;
                        public float StoppingDistance;

                        public bool HasUpdatePosition;
                        public bool UpdatePosition;

                        public bool HasUpdateRotation;
                        public bool UpdateRotation;

                        public bool HasRadius;
                        public float Radius;

                        public bool HasHeight;
                        public float Height;

                        public bool HasObstacleAvoidanceType;
                        public ObstacleAvoidanceType ObstacleAvoidanceType;

                        public bool HasAvoidancePriority;
                        public int AvoidancePriority;

                        public bool HasIsStopped;
                        public bool IsStopped;

                        public bool HasAutoTraverseOffMeshLink;
                        public bool AutoTraverseOffMeshLink;

                        public bool HasBaseOffset;
                        public float BaseOffset;
                }
        }

	[MemoryPackable]
	public partial class RememberNavmeshAgentData : IMemoryPackable<RememberNavmeshAgentData>
	{
		public bool Enabled { get; set; } // Newly added property
		public Vector3 Destination { get; set; }
		public float Speed { get; set; }
		public float AngularSpeed { get; set; }
		public float Acceleration { get; set; }
		public float StoppingDistance { get; set; }
		public bool UpdatePosition { get; set; }
		public bool UpdateRotation { get; set; }
		public float Radius { get; set; }
		public float Height { get; set; }
		public ObstacleAvoidanceType ObstacleAvoidanceType { get; set; }
		public int AvoidancePriority { get; set; }
		public bool IsStopped { get; set; }
		public bool AutoTraverseOffMeshLink { get; set; }
		public float BaseOffset { get; set; }
	}
}
#endif