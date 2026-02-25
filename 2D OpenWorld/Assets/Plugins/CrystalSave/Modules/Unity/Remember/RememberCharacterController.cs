#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	[AddComponentMenu("Crystal Save/Remember Components/Remember CharacterController")]
	[DisallowMultipleComponent]
	[RememberTarget(typeof(CharacterController))]
	public class RememberCharacterController : SaveableComponent
	{
		[Header("Performance")]
		[Tooltip("Enable lightweight caching of the CharacterController reference to avoid repeated GetComponent calls.")]
		[SerializeField] private bool enablePerformanceCaching = false;
                [Header("Serialization Settings")]

                [Tooltip("When enabled, the CharacterController's enabled state will be serialized and saved.")]
                [SerializeField]
                private bool saveCharacterEnabledState = true;

                [Header("Save Optimization")]
                [Tooltip("Avoid serializing CharacterController data when no tracked values have changed since the last save.")]
                [SerializeField] private bool skipSavingWhenUnchanged;

                [Header("CharacterController Properties to Save")]

                [Tooltip("Serialize the CharacterController's height property.")]
                [SerializeField]
                private bool rememberHeight = true;

		[Tooltip("Serialize the CharacterController's radius property.")]
		[SerializeField]
		private bool rememberRadius = true;

		[Tooltip("Serialize the CharacterController's center property.")]
		[SerializeField]
		private bool rememberCenter = true;

		[Tooltip("Serialize the CharacterController's slope limit property.")]
		[SerializeField]
		private bool rememberSlopeLimit = true;

		[Tooltip("Serialize the CharacterController's step offset property.")]
		[SerializeField]
		private bool rememberStepOffset = true;

		[Tooltip("Serialize the CharacterController's skin width property.")]
		[SerializeField]
		private bool rememberSkinWidth = true;

		[Tooltip("Serialize the CharacterController's minimum move distance.")]
		[SerializeField]
		private bool rememberMinMoveDistance = true;

		[Tooltip("Serialize whether the CharacterController detects collisions.")]
		[SerializeField]
		private bool rememberDetectCollisions = true;

		[Tooltip("Serialize whether the CharacterController enables overlap recovery.")]
		[SerializeField]
		private bool rememberEnableOverlapRecovery = true;

                private CharacterController targetCharacterController;
                private RememberCharacterControllerData cachedSnapshot;
                private bool hasCachedSnapshot;
                private byte[] cachedSerializedData;

                private const float FloatComparisonTolerance = 1e-5f;
                private const float Vector3ComparisonToleranceSqr = FloatComparisonTolerance * FloatComparisonTolerance;

                protected override void Awake()
                {
                        base.Awake();
                        // Always get the CharacterController component, regardless of caching setting
                        targetCharacterController = GetComponent<CharacterController>();

                        if (targetCharacterController == null)
                        {
                                Logger.Log($"{nameof(RememberCharacterController)} requires a CharacterController component on the same GameObject. None was found on '{gameObject.name}'.", LogCategory.RememberCharacterController, LogLevel.Error);
                                hasCachedSnapshot = false;
                                cachedSnapshot = null;
                                return;
                        }

                        if (skipSavingWhenUnchanged)
                        {
                                if (TryCaptureCurrentState(targetCharacterController, out var snapshot))
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

                /// <summary>
                /// Serializes the CharacterController's data into a byte array using MemoryPack.
                /// </summary>
                protected override byte[] SerializeComponentData()
                {
                        // Use cached reference if caching enabled, otherwise get component each time
                        CharacterController controller = enablePerformanceCaching ? targetCharacterController : GetComponent<CharacterController>();

                        if (controller == null)
                        {
                                Logger.Log("SerializeComponentData failed: CharacterController component not found.", LogCategory.RememberCharacterController, LogLevel.Warning);
                                return null;
                        }

                        if (!TryCaptureCurrentState(controller, out var snapshot) || snapshot == null)
                        {
                                if (skipSavingWhenUnchanged)
                                {
                                        cachedSnapshot = null;
                                        hasCachedSnapshot = false;
                                }
                                return null;
                        }

                        try
                        {
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
                                        cachedSnapshot = snapshot;
                                        hasCachedSnapshot = true;
                                        cachedSerializedData = serialized;
                                }

                                return serialized;
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"Serialization failed for '{gameObject.name}': {ex.Message}", LogCategory.RememberCharacterController, LogLevel.Error);
                                return null;
                        }
                }

                /// <summary>
                /// Deserializes the data from a byte array and applies it to the CharacterController.
                /// </summary>
		protected override void DeserializeComponentData(byte[] data)
		{
			if (data == null || data.Length == 0)
			{
				Logger.Log("DeserializeComponentData failed: Data is null or empty.", LogCategory.RememberCharacterController, LogLevel.Warning);
				return;
			}

			// Use cached reference if caching enabled, otherwise get component each time
                        CharacterController controller = enablePerformanceCaching ? targetCharacterController : GetComponent<CharacterController>();

                        if (controller == null)
                        {
                                Logger.Log("DeserializeComponentData failed: CharacterController component not found.", LogCategory.RememberCharacterController, LogLevel.Warning);
                                return;
			}

			try
			{
				var deserializedData = SaveDataSerializer.Instance.Deserialize<RememberCharacterControllerData>(data);

				if (deserializedData == null)
				{
					Logger.Log("Deserialization resulted in null or empty data.", LogCategory.RememberCharacterController, LogLevel.Warning);
					return;
				}

				if (saveCharacterEnabledState)
				{
					controller.enabled = deserializedData.Enabled;
				}

				if (rememberHeight)
				{
					controller.height = deserializedData.Height;
				}

				if (rememberRadius)
				{
					controller.radius = deserializedData.Radius;
				}

				if (rememberCenter)
				{
					controller.center = deserializedData.Center;
				}

				if (rememberSlopeLimit)
				{
					controller.slopeLimit = deserializedData.SlopeLimit;
				}

				if (rememberStepOffset)
				{
					controller.stepOffset = deserializedData.StepOffset;
				}

				if (rememberSkinWidth)
				{
					controller.skinWidth = deserializedData.SkinWidth;
				}

				if (rememberMinMoveDistance)
				{
					controller.minMoveDistance = deserializedData.MinMoveDistance;
				}

				if (rememberDetectCollisions)
				{
					controller.detectCollisions = deserializedData.DetectCollisions;
				}

                                if (rememberEnableOverlapRecovery)
                                {
                                        controller.enableOverlapRecovery = deserializedData.EnableOverlapRecovery;
                                }

                                Logger.Log($"Successfully loaded CharacterController data for GameObject '{gameObject.name}'.", LogCategory.RememberCharacterController, LogLevel.Info);

                                if (skipSavingWhenUnchanged)
                                {
                                        if (TryCaptureCurrentState(controller, out var snapshot) && snapshot != null)
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
                                Logger.Log($"Deserialization failed for '{gameObject.name}': {ex.Message}", LogCategory.RememberCharacterController, LogLevel.Error);
                        }
                }

                private bool TryCaptureCurrentState(CharacterController controller, out RememberCharacterControllerData snapshot)
                {
                        snapshot = null;

                        if (controller == null)
                        {
                                return false;
                        }

                        bool anyFieldTracked = false;
                        var data = new RememberCharacterControllerData();

                        if (saveCharacterEnabledState)
                        {
                                data.Enabled = controller.enabled;
                                anyFieldTracked = true;
                        }

                        if (rememberHeight)
                        {
                                data.Height = controller.height;
                                anyFieldTracked = true;
                        }

                        if (rememberRadius)
                        {
                                data.Radius = controller.radius;
                                anyFieldTracked = true;
                        }

                        if (rememberCenter)
                        {
                                data.Center = controller.center;
                                anyFieldTracked = true;
                        }

                        if (rememberSlopeLimit)
                        {
                                data.SlopeLimit = controller.slopeLimit;
                                anyFieldTracked = true;
                        }

                        if (rememberStepOffset)
                        {
                                data.StepOffset = controller.stepOffset;
                                anyFieldTracked = true;
                        }

                        if (rememberSkinWidth)
                        {
                                data.SkinWidth = controller.skinWidth;
                                anyFieldTracked = true;
                        }

                        if (rememberMinMoveDistance)
                        {
                                data.MinMoveDistance = controller.minMoveDistance;
                                anyFieldTracked = true;
                        }

                        if (rememberDetectCollisions)
                        {
                                data.DetectCollisions = controller.detectCollisions;
                                anyFieldTracked = true;
                        }

                        if (rememberEnableOverlapRecovery)
                        {
                                data.EnableOverlapRecovery = controller.enableOverlapRecovery;
                                anyFieldTracked = true;
                        }

                        if (!anyFieldTracked)
                        {
                                return false;
                        }

                        snapshot = data;
                        return true;
                }

                private bool AreEquivalent(RememberCharacterControllerData baseline, RememberCharacterControllerData candidate)
                {
                        if (baseline == null || candidate == null)
                        {
                                return false;
                        }

                        if (saveCharacterEnabledState && baseline.Enabled != candidate.Enabled)
                        {
                                return false;
                        }

                        if (rememberHeight && !Mathf.Approximately(baseline.Height, candidate.Height) && Mathf.Abs(baseline.Height - candidate.Height) > FloatComparisonTolerance)
                        {
                                return false;
                        }

                        if (rememberRadius && !Mathf.Approximately(baseline.Radius, candidate.Radius) && Mathf.Abs(baseline.Radius - candidate.Radius) > FloatComparisonTolerance)
                        {
                                return false;
                        }

                        if (rememberCenter && (baseline.Center - candidate.Center).sqrMagnitude > Vector3ComparisonToleranceSqr)
                        {
                                return false;
                        }

                        if (rememberSlopeLimit && !Mathf.Approximately(baseline.SlopeLimit, candidate.SlopeLimit) && Mathf.Abs(baseline.SlopeLimit - candidate.SlopeLimit) > FloatComparisonTolerance)
                        {
                                return false;
                        }

                        if (rememberStepOffset && !Mathf.Approximately(baseline.StepOffset, candidate.StepOffset) && Mathf.Abs(baseline.StepOffset - candidate.StepOffset) > FloatComparisonTolerance)
                        {
                                return false;
                        }

                        if (rememberSkinWidth && !Mathf.Approximately(baseline.SkinWidth, candidate.SkinWidth) && Mathf.Abs(baseline.SkinWidth - candidate.SkinWidth) > FloatComparisonTolerance)
                        {
                                return false;
                        }

                        if (rememberMinMoveDistance && !Mathf.Approximately(baseline.MinMoveDistance, candidate.MinMoveDistance) && Mathf.Abs(baseline.MinMoveDistance - candidate.MinMoveDistance) > FloatComparisonTolerance)
                        {
                                return false;
                        }

                        if (rememberDetectCollisions && baseline.DetectCollisions != candidate.DetectCollisions)
                        {
                                return false;
                        }

                        if (rememberEnableOverlapRecovery && baseline.EnableOverlapRecovery != candidate.EnableOverlapRecovery)
                        {
                                return false;
                        }

                        return true;
                }
	}
	[MemoryPackable]
	public partial class RememberCharacterControllerData : IMemoryPackable<RememberCharacterControllerData>
	{
		public bool Enabled { get; set; }
		public float Height { get; set; }
		public float Radius { get; set; }
		public Vector3 Center { get; set; }
		public float SlopeLimit { get; set; }
		public float StepOffset { get; set; }
		public float SkinWidth { get; set; }
		public float MinMoveDistance { get; set; }
		public bool DetectCollisions { get; set; }
		public bool EnableOverlapRecovery { get; set; }
	}
}
#endif
