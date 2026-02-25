#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using System.Collections;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	[AddComponentMenu("Crystal Save/Remember Components/Remember Collider")]
	[DisallowMultipleComponent]
	[RememberTarget(typeof(Collider))]
	[RememberIcon("SphereCollider Icon")]
	public sealed class RememberCollider : SaveableComponent
	{
		[Header("Performance")]
		[Tooltip("Enable lightweight caching of the Collider reference to avoid repeated GetComponent calls.")]
		[SerializeField] private bool enablePerformanceCaching = false;
		[Header("Collider properties to save")]

		[Tooltip("Save the Collider’s enabled state.")]
		[SerializeField] private bool rememberEnabled = true;

		[Tooltip("Save whether the Collider is marked as Trigger.")]
		[SerializeField] private bool rememberIsTrigger = true;

		[Tooltip("Save the Collider’s PhysicMaterial (stored by name).")]
		[SerializeField] private bool rememberMaterial = true;

		[Header("Save Optimization")]
		[Tooltip("Skip serializing when the collider state hasn't changed since the previous save.")]
		[SerializeField] private bool skipSavingWhenUnchanged = false;

		private Collider targetCollider;
		private ColliderData cachedSnapshot;
		private bool hasCachedSnapshot;
		private byte[] cachedSerializedData;
		private const float FloatTolerance = 0.0001f;
		
		// Queue collider data for LateUpdate application (like RememberTransform)
		private ColliderData pendingColliderData;
		private bool shouldApplyCollider = false;

		/*─────────────────────────── LIFECYCLE ───────────────────────────*/
		protected override void Awake()
		{
			base.Awake();

			// Always get the Collider component, regardless of caching setting
                        targetCollider = GetComponent<Collider>();
                        if (targetCollider == null)
                        {
                                Logger.Log($"{nameof(RememberCollider)} requires a Collider on '{gameObject.name}'.", LogCategory.RememberCollider, LogLevel.Error);
                                enabled = false;
                                return;
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

                /*────────────────────── SERIALISE OUT ───────────────────────*/
                protected override byte[] SerializeComponentData()
                {
                        if (!TryCaptureCurrentState(out var snapshot) || snapshot == null)
                                return null;

                        if (skipSavingWhenUnchanged && hasCachedSnapshot && AreEquivalent(cachedSnapshot, snapshot))
                        {
                                if (cachedSerializedData != null && cachedSerializedData.Length > 0)
                                {
                                        Logger.Log($"RememberCollider: Returning cached serialized data for '{gameObject.name}' (unchanged).", LogCategory.RememberCollider, LogLevel.Off);
                                        return cachedSerializedData;
                                }
                                
                                Logger.Log($"RememberCollider: Data unchanged but no cached serialized data for '{gameObject.name}' - will serialize fresh.", LogCategory.RememberCollider, LogLevel.Off);
                        }

                        var serialized = Serializer.Serialize(snapshot);

                        if (skipSavingWhenUnchanged)
                        {
                                cachedSnapshot = snapshot;
                                hasCachedSnapshot = true;
                                cachedSerializedData = serialized;
                        }

                        return serialized;
                }

	/*────────────────────── SERIALISE  IN ───────────────────────*/
	protected override void DeserializeComponentData(byte[] bytes)
	{
		if (bytes == null || bytes.Length == 0)
		{
			Logger.Log("DeserializeComponentData failed: data is null or empty.", LogCategory.RememberCollider, LogLevel.Warning);
			return;
		}

		try
		{
			var data = Serializer.Deserialize<ColliderData>(bytes);
			if (data == null)
			{
				Logger.Log("DeserializeComponentData failed: deserialized data is null.", LogCategory.RememberCollider, LogLevel.Warning);
                return;
                        }

                        // Queue the Collider data for application in LateUpdate
                        pendingColliderData = data;
                        shouldApplyCollider = true;			if (skipSavingWhenUnchanged)
			{
				cachedSnapshot = data;
				hasCachedSnapshot = true;
			}
		}
		catch (Exception ex)
		{
			Logger.Log($"DeserializeComponentData encountered an error for '{gameObject.name}': {ex.Message}", LogCategory.RememberCollider, LogLevel.Error);
		}
	}

	void LateUpdate()
	{
		if (shouldApplyCollider && pendingColliderData != null)
		{
			ApplyColliderData(pendingColliderData);
			shouldApplyCollider = false;
			pendingColliderData = null;
		}
	}

	/*────────────────────── DESERIALISE IN ───────────────────────*/

	private void ApplyColliderData(ColliderData data)
	{
		if (data == null)
		{
			Logger.Log($"RememberCollider: Received null ColliderData for '{gameObject.name}'.", LogCategory.RememberCollider, LogLevel.Warning);
			return;
		}

		try
		{
			// Use cached reference if caching enabled, otherwise get component each time
			Collider collider = enablePerformanceCaching ? targetCollider : GetComponent<Collider>();

			if (collider == null)
			{
				Logger.Log($"RememberCollider: Collider component not found on '{gameObject.name}'.", LogCategory.RememberCollider, LogLevel.Error);
				return;
			}

			// Apply collider state
			if (rememberEnabled) collider.enabled = data.Enabled;
			if (rememberIsTrigger) collider.isTrigger = data.IsTrigger;

			Logger.Log($"Applied Collider data for '{gameObject.name}': Enabled={data.Enabled}, IsTrigger={data.IsTrigger}", LogCategory.RememberCollider, LogLevel.Info);

			// Apply material if configured
			if (rememberMaterial && !string.IsNullOrEmpty(data.MaterialName))
			{
				#if UNITY_6000_0_OR_NEWER
					var mat = AssetProvider.Load<PhysicsMaterial>(data.MaterialName);
				#else
					var mat = AssetProvider.Load<PhysicMaterial>(data.MaterialName);
				#endif

				if (mat != null)
				{
					collider.sharedMaterial = mat;
				}
				else
				{
					Logger.Log($"Material '{data.MaterialName}' not found for '{gameObject.name}'.", LogCategory.RememberCollider, LogLevel.Warning);
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Log($"ApplyColliderData encountered an error for '{gameObject.name}': {ex.Message}", LogCategory.RememberCollider, LogLevel.Error);
		}
	}                private bool TryCaptureCurrentState(out ColliderData snapshot)
                {
                        snapshot = null;

                        if (!rememberEnabled && !rememberIsTrigger && !rememberMaterial)
                        {
                                return false;
                        }

                        Collider collider = enablePerformanceCaching ? targetCollider : GetComponent<Collider>();
                        if (collider == null)
                        {
                                return false;
                        }

                        var data = new ColliderData();
                        if (rememberEnabled)
                        {
                                data.Enabled = collider.enabled;
                        }

                        if (rememberIsTrigger)
                        {
                                data.IsTrigger = collider.isTrigger;
                        }

                        if (rememberMaterial)
                        {
                                data.MaterialName = collider.sharedMaterial != null
                                        ? collider.sharedMaterial.name
                                        : null;
                        }

                        snapshot = data;
                        return true;
                }

                private bool AreEquivalent(ColliderData a, ColliderData b)
                {
                        if (a == null || b == null) return false;

                        if (rememberEnabled && a.Enabled != b.Enabled) return false;
                        if (rememberIsTrigger && a.IsTrigger != b.IsTrigger) return false;

                        if (rememberMaterial)
                        {
                                if (!string.Equals(a.MaterialName, b.MaterialName, StringComparison.Ordinal))
                                {
                                        return false;
                                }
                        }

                        return true;
                }
	}

	/*─────────────────────────── DATA POCO ──────────────────────────────*/
	[MemoryPackable]
	public partial class ColliderData
	{
		public bool Enabled;
		public bool IsTrigger;
		public string MaterialName;
	}
}
#endif