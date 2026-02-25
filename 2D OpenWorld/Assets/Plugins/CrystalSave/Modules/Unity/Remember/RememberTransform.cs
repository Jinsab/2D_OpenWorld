#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using UnityEngine;
using UnityEngine.AI;

namespace Arawn.CrystalSave.Runtime
{
	[AddComponentMenu("Crystal Save/Remember Components/Remember Transform")]
	[DisallowMultipleComponent]
	[RememberTarget(typeof(Transform))]
	public class RememberTransform : SaveableComponent, ITransformAppliable
	{
		[Header("Performance")]
		[Tooltip("Enable lightweight caching of frequently-used components to avoid repeated GetComponent calls.")]
		[SerializeField] private bool enablePerformanceCaching = false;

		[Tooltip("Batch registration: when enabled, SaveManager will optionally batch-register SaveableComponents in this subtree using a single GetComponentsInChildren sweep and spread work across frames. Use with care if you depend on strict parent-before-child registration ordering.")]
		[SerializeField] private bool enableBatchRegistration = false;

		public bool EnablePerformanceCaching => enablePerformanceCaching;
		public bool EnableBatchRegistration => enableBatchRegistration;
		[Header("Transform Properties to Save")]
		[SerializeField]
		private TransformPropertySettings rememberPositionSettings = new TransformPropertySettings { Save = false, SpaceMode = SpaceMode.Local };

		[SerializeField]
		private TransformPropertySettings rememberRotationSettings = new TransformPropertySettings { Save = false, SpaceMode = SpaceMode.Local };

                [SerializeField]
                private TransformPropertySettings rememberScaleSettings = new TransformPropertySettings { Save = false, SpaceMode = SpaceMode.Local };

                [Header("Save Optimization")]
                [Tooltip("Skip serialization when the captured transform matches the last saved state.")]
                [SerializeField]
                private bool skipSavingWhenUnchanged = false;

                [Header("CharacterController Handling")]
                [Tooltip("If true, temporarily disable any attached CharacterController when restoring transform.")]
                [SerializeField]
                private bool disableControllerOnApply = false;

		// Queued TransformData to be applied in LateUpdate
		private bool shouldApplyTransform = false;
		private TransformData pendingTransformData;
		private int retryCount = 0;
		private const int maxRetries = 3;

		// Cache a reference to any CharacterController on this GameObject
		private CharacterController cachedController;

                private const float PositionTolerance = 0.0001f;
                private const float RotationTolerance = 0.0001f;

                private TransformData initialState;
                private bool hasInitialState;
                private byte[] cachedSerializedData;

                protected override void Awake()
                {
                        base.Awake();
                        Logger.Log($"RememberTransform Awake called for '{gameObject.name}'", LogCategory.RememberTransform, LogLevel.Off);

                        // Always detect CharacterController if present, regardless of caching setting
                        cachedController = GetComponent<CharacterController>();

                        if (skipSavingWhenUnchanged)
                        {
                                hasInitialState = TryCaptureCurrentState(out initialState, logSerialization: false);
                        }
                        else
                        {
                                hasInitialState = false;
                                initialState = null;
                        }
                }

		void LateUpdate()
		{
			if (shouldApplyTransform && pendingTransformData != null)
			{
				ApplyTransformData(pendingTransformData);
				shouldApplyTransform = false;
				pendingTransformData = null;
			}
		}

                protected override byte[] SerializeComponentData()
                {
                        if (!TryCaptureCurrentState(out var transformData, logSerialization: true))
                                return null;

                        // During snapshot capture, we still compare against cached state to avoid
                        // unnecessary serialization, but we DON'T return null for unchanged data.
                        // Instead, we serialize the cached state to ensure snapshot completeness.
                        bool isSnapshot = IsCapturingSnapshot;
                        
                        if (skipSavingWhenUnchanged && hasInitialState && AreEquivalent(transformData, initialState))
                        {
                                if (isSnapshot)
                                {
                                        // Data unchanged: for snapshot, serialize the existing cached state
                                        Logger.Log($"RememberTransform: Snapshot capture for '{gameObject.name}' using cached state (unchanged).", LogCategory.RememberTransform, LogLevel.Off);
                                        return Serializer.Serialize(initialState);
                                }
                                else
                                {
                                        // Data unchanged: return cached serialized data instead of null to preserve save integrity
                                        if (cachedSerializedData != null && cachedSerializedData.Length > 0)
                                        {
                                                Logger.Log($"RememberTransform: Returning cached serialized data for '{gameObject.name}' (unchanged).", LogCategory.RememberTransform, LogLevel.Off);
                                                return cachedSerializedData;
                                        }
                                        
                                        Logger.Log($"RememberTransform: Data unchanged but no cached serialized data for '{gameObject.name}' - will serialize fresh.", LogCategory.RememberTransform, LogLevel.Off);
                                }
                        }

                        byte[] serialized = Serializer.Serialize(transformData);
                        // Note: Removed frequent logging to improve performance during TimeMachine recording
                        // Logger.Log($"TransformData serialized for '{gameObject.name}'. Byte length: {serialized.Length}", LogCategory.RememberTransform, LogLevel.Info);
                        if (skipSavingWhenUnchanged)
                        {
                                initialState = transformData;
                                hasInitialState = true;
                                cachedSerializedData = serialized;
                        }
                        return serialized;
                }

		protected override void DeserializeComponentData(byte[] data)
		{
			if (data == null || data.Length == 0)
			{
				Logger.Log("DeserializeComponentData failed: data is null or empty.", gameObject, LogCategory.RememberTransform, LogLevel.Warning);
				return;
			}

			try
			{
				TransformData deserializedData = Serializer.Deserialize<TransformData>(data);

				if (deserializedData == null)
				{
					Logger.Log("DeserializeComponentData failed: deserialized data is null.", gameObject, LogCategory.RememberTransform, LogLevel.Warning);
					return;
				}

				Logger.Log($"Deserialized TransformData for '{gameObject.name}': Position={deserializedData.Position}, Rotation={deserializedData.Rotation}, Scale={deserializedData.Scale}", LogCategory.RememberTransform, LogLevel.Info);

				// Queue the Transform data for application in LateUpdate
				pendingTransformData = deserializedData;
				shouldApplyTransform = true;
                                Logger.Log($"Queued Transform data for LateUpdate application on '{gameObject.name}'.", LogCategory.RememberTransform, LogLevel.Info);

                                if (skipSavingWhenUnchanged)
                                {
                                        initialState = deserializedData;
                                        hasInitialState = true;
                                }
                        }
			catch (Exception ex)
			{
				Logger.Log($"DeserializeComponentData encountered an error for '{gameObject.name}': {ex.Message}", gameObject, LogCategory.RememberTransform, LogLevel.Error);
			}
		}

                public void ApplyTransformData(TransformData data)
                {
                        if (data == null)
                        {
                                Logger.Log($"RememberTransform: Received null TransformData for '{gameObject.name}'.", gameObject, LogCategory.RememberTransform, LogLevel.Warning);
                                return;
                        }

                        // Variables for Rigidbody state management (need to be outside try block for finally access)
                        Rigidbody rb = null;
                        bool hadRigidbody = false;
                        bool wasKinematic = false;

                        try
                        {
                                // Temporarily make Rigidbody kinematic to prevent physics interference during transform application
                                // This is especially important for RememberHomeScene objects that load with physics enabled
                                rb = GetComponent<Rigidbody>();
                                hadRigidbody = rb != null;
                                wasKinematic = hadRigidbody && rb.isKinematic;
                                
                                if (hadRigidbody && !wasKinematic)
                                {
                                        rb.isKinematic = true;
                                }

                                // If configured, disable CharacterController to prevent it from overriding the transform
                                // Use cached reference if caching enabled, otherwise get component each time
                                CharacterController controller = enablePerformanceCaching ? cachedController : GetComponent<CharacterController>();

                                if (disableControllerOnApply && controller != null)
                                {
                                        controller.enabled = false;
                                }

                                // Restore Position
                                if (rememberPositionSettings.Save && data.Position.HasValue)
                                {
                                        if (rememberPositionSettings.SpaceMode == SpaceMode.Local)
                                                transform.localPosition = data.Position.Value;
                                        else
                                                transform.position = data.Position.Value;

                                        Logger.Log($"Applied Position for '{gameObject.name}': {transform.position}", LogCategory.RememberTransform, LogLevel.Info);
                                }

                                // Restore Rotation
                                if (rememberRotationSettings.Save && data.Rotation.HasValue)
                                {
                                        if (rememberRotationSettings.SpaceMode == SpaceMode.Local)
                                                transform.localRotation = data.Rotation.Value;
                                        else
                                                transform.rotation = data.Rotation.Value;

                                        Logger.Log($"Applied Rotation for '{gameObject.name}': {transform.rotation}", LogCategory.RememberTransform, LogLevel.Info);
                                }

                                // Restore Scale
                                if (rememberScaleSettings.Save && data.Scale.HasValue)
                                {
                                        if (rememberScaleSettings.SpaceMode == SpaceMode.Local)
                                        {
                                                transform.localScale = data.Scale.Value;
                                        }
                                        else
                                        {
                                                if (transform.parent != null)
                                                {
                                                        Vector3 parentLossyScale = transform.parent.lossyScale;
                                                        if (parentLossyScale.x == 0 || parentLossyScale.y == 0 || parentLossyScale.z == 0)
                                                        {
                                                                Logger.Log($"Cannot apply world scale for '{gameObject.name}' because parent has a scale of zero.", gameObject, LogCategory.RememberTransform, LogLevel.Error);
                                                                return;
                                                        }

                                                        Vector3 desiredLocalScale = new Vector3(
                                                                data.Scale.Value.x / parentLossyScale.x,
                                                                data.Scale.Value.y / parentLossyScale.y,
                                                                data.Scale.Value.z / parentLossyScale.z
                                                        );

                                                        transform.localScale = desiredLocalScale;
                                                }
                                                else
                                                {
                                                        transform.localScale = data.Scale.Value;
                                                }
                                        }

                                        Logger.Log($"Applied Scale for '{gameObject.name}': {transform.localScale}", LogCategory.RememberTransform, LogLevel.Info);
                                }

                                // Sync physics transforms to prevent physics interference with validation
                                // This is especially important for RememberHomeScene objects with Rigidbodies
                                if (hadRigidbody)
                                {
                                        Physics.SyncTransforms();
                                }

                                // Validation
                                bool positionCorrect = !rememberPositionSettings.Save ||
                                        (rememberPositionSettings.SpaceMode == SpaceMode.Local ?
                                                transform.localPosition.Equals(data.Position.Value) :
                                                transform.position.Equals(data.Position.Value));

                                bool rotationCorrect = !rememberRotationSettings.Save ||
                                        (rememberRotationSettings.SpaceMode == SpaceMode.Local ?
                                                transform.localRotation.Equals(data.Rotation.Value) :
                                                transform.rotation.Equals(data.Rotation.Value));

                                bool scaleCorrect = !rememberScaleSettings.Save ||
                                        (rememberScaleSettings.SpaceMode == SpaceMode.Local ?
                                                transform.localScale.Equals(data.Scale.Value) :
                                                Mathf.Approximately(transform.lossyScale.x, data.Scale.Value.x) &&
                                                Mathf.Approximately(transform.lossyScale.y, data.Scale.Value.y) &&
                                                Mathf.Approximately(transform.lossyScale.z, data.Scale.Value.z));

                                if (!positionCorrect || !rotationCorrect || !scaleCorrect)
                                {
                                        Logger.Log($"Validation failed after applying Transform for '{gameObject.name}'. Retrying... ({retryCount + 1}/{maxRetries})", gameObject, LogCategory.RememberTransform, LogLevel.Info);
                                        retryCount++;
                                        if (retryCount < maxRetries)
                                        {
                                                shouldApplyTransform = true; // Reapply in the next LateUpdate
                                                pendingTransformData = data; // Reassign the data
                                        }
                                        else
                                        {
                                                Logger.Log($"Failed to apply Transform correctly for '{gameObject.name}' after {maxRetries} retries.", gameObject, LogCategory.RememberTransform, LogLevel.Error);
                                                // Optionally, notify the SaveManager or user
                                        }
                                }
                                else
                                {
                                        Logger.Log($"Successfully applied and validated Transform for '{gameObject.name}'.", LogCategory.RememberTransform, LogLevel.Info);
                                        retryCount = 0; // Reset retry count on success
                                }
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"Exception while applying Transform for '{gameObject.name}': {ex.Message}", gameObject, LogCategory.RememberTransform, LogLevel.Error);
                        }
                        finally
                        {
                                // Restore Rigidbody kinematic state if we changed it
                                // We always restore to the original state since RememberRigidbody (if present)
                                // has already set the correct state during its earlier deserialization
                                if (hadRigidbody && !wasKinematic && rb != null)
                                {
                                        rb.isKinematic = false;
                                }

                                // Re-enable the CharacterController if it was disabled
                                // Use cached reference if caching enabled, otherwise get component each time
                                CharacterController controller = enablePerformanceCaching ? cachedController : GetComponent<CharacterController>();

                                if (disableControllerOnApply && controller != null)
                                {
                                        controller.enabled = true;
                                }
                        }
                }

                private bool TryCaptureCurrentState(out TransformData data, bool logSerialization)
                {
                        bool hasAnyData = false;
                        data = new TransformData();

                        if (rememberPositionSettings.Save)
                        {
                                data.Position = rememberPositionSettings.SpaceMode == SpaceMode.Local ? transform.localPosition : transform.position;
                                hasAnyData = true;
                                if (logSerialization)
                                        Logger.Log($"Serialized Position for '{gameObject.name}': {data.Position}", LogCategory.RememberTransform, LogLevel.Off); // Changed to LogLevel.Off for performance
                        }

                        if (rememberRotationSettings.Save)
                        {
                                data.Rotation = rememberRotationSettings.SpaceMode == SpaceMode.Local ? transform.localRotation : transform.rotation;
                                hasAnyData = true;
                                if (logSerialization)
                                        Logger.Log($"Serialized Rotation for '{gameObject.name}': {data.Rotation}", LogCategory.RememberTransform, LogLevel.Info);
                        }

                        if (rememberScaleSettings.Save)
                        {
                                if (rememberScaleSettings.SpaceMode == SpaceMode.Local)
                                {
                                        data.Scale = transform.localScale;
                                }
                                else
                                {
                                        data.Scale = transform.lossyScale;
                                }

                                hasAnyData = true;
                                if (logSerialization)
                                        Logger.Log($"Serialized Scale for '{gameObject.name}': {data.Scale}", LogCategory.RememberTransform, LogLevel.Info);
                        }

                        if (!hasAnyData)
                                data = null;

                        return hasAnyData;
                }

                private static bool AreEquivalent(TransformData a, TransformData b)
                {
                        return AreApproximatelyEqual(a?.Position, b?.Position) &&
                               AreApproximatelyEqual(a?.Rotation, b?.Rotation) &&
                               AreApproximatelyEqual(a?.Scale, b?.Scale);
                }

                private static bool AreApproximatelyEqual(Vector3? a, Vector3? b)
                {
                        if (!a.HasValue && !b.HasValue)
                                return true;

                        if (a.HasValue != b.HasValue)
                                return false;

                        return (a.Value - b.Value).sqrMagnitude <= PositionTolerance * PositionTolerance;
                }

                private static bool AreApproximatelyEqual(Quaternion? a, Quaternion? b)
                {
                        if (!a.HasValue && !b.HasValue)
                                return true;

                        if (a.HasValue != b.HasValue)
                                return false;

                        float dot = Mathf.Abs(Quaternion.Dot(a.Value, b.Value));
                        return 1f - dot <= RotationTolerance;
                }
        }

	// Define the ITransformAppliable interface if needed
	public interface ITransformAppliable
	{
		void ApplyTransformData(TransformData data);
	}

	[MemoryPackable]
	public partial class TransformData
	{
		//[MemoryPackOrder(0)]
		public Vector3? Position { get; set; }
		//[MemoryPackOrder(1)]
		public Quaternion? Rotation { get; set; }
		//[MemoryPackOrder(2)]
		public Vector3? Scale { get; set; }

		// Default constructor for MemoryPack
		public TransformData()
		{
			Position = null;
			Rotation = null;
			Scale = null;
		}

		// MemoryPack constructor
		[MemoryPackConstructor]
		public TransformData(Vector3? position, Quaternion? rotation, Vector3? scale)
		{
			Position = position;
			Rotation = rotation;
			Scale = scale;
		}
	}

	[Serializable]
	public class TransformPropertySettings
	{
		[Tooltip("Enable saving this Transform property.")]
		public bool Save = false;

		[Tooltip("Select the coordinate space for this property.")]
		public SpaceMode SpaceMode = SpaceMode.Local;
	}

	public enum SpaceMode
	{
		Local,
		World
	}
}
#endif
