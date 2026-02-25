#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	[AddComponentMenu("Crystal Save/Remember Components/Remember Animator")]
	[DisallowMultipleComponent]
	[RequireComponent(typeof(Animator))]
	[RememberTarget(typeof(Animator))]
	public class RememberAnimator : SaveableComponent
	{
                [Header("Animator Properties to Save")]
                [Tooltip("If true, all Animator parameters (Float, Int, Bool, Trigger) will be saved and restored.")]
                [SerializeField] private bool saveAnimatorParameters = true;

                [Tooltip("If true, we also capture each layer's current/next state and normalized time for partial progress.")]
                [SerializeField] private bool saveLayerStateInfo = true;

                [Header("Save Optimization")]
                [Tooltip("Skip saving when the captured Animator data matches the previous snapshot.")]
                [SerializeField] private bool skipSavingWhenUnchanged = false;

                [Header("Debug")]
                [Tooltip("Enable detailed logging to the console for debugging.")]
                [SerializeField] private bool enableDebugLogging = false;

                private Animator targetAnimator;
                private AnimatorData cachedAnimatorSnapshot;
                private bool cachedAnimatorSnapshotCaptured;
                private byte[] cachedSerializedData;
                
                // Trigger tracking - records which triggers were set this frame
                private HashSet<string> triggersSetThisFrame = new HashSet<string>();
                private HashSet<int> triggerParameterHashes = new HashSet<int>();

                protected override void Awake()
                {
                        base.Awake();
                        targetAnimator = GetComponent<Animator>();
			if (targetAnimator == null)
			{
                                Logger.Log("RememberAnimator requires an Animator component on the same GameObject.", LogCategory.RememberAnimator, LogLevel.Error);
                                enabled = false;
                                return;
                        }
                        
                        // Build list of trigger parameter hashes for quick lookup
                        foreach (var param in targetAnimator.parameters)
                        {
                                if (param.type == AnimatorControllerParameterType.Trigger)
                                {
                                        triggerParameterHashes.Add(param.nameHash);
                                }
                        }
                        
                        if (skipSavingWhenUnchanged && TryCaptureCurrentState(out var snapshot, false))
                        {
                                CacheAnimatorSnapshot(snapshot);
                        }
                }
                
                /// <summary>
                /// Call this method from your game code when setting a trigger on the animator.
                /// Example: rememberAnimator.NotifyTriggerSet("Jump"); animator.SetTrigger("Jump");
                /// </summary>
                public void NotifyTriggerSet(string triggerName)
                {
                        triggersSetThisFrame.Add(triggerName);
                        
                        if (enableDebugLogging)
                        {
                                Logger.Log($"RememberAnimator: Trigger '{triggerName}' notification received on '{gameObject.name}'.", LogCategory.RememberAnimator, LogLevel.Info);
                        }
                }
                
                /// <summary>
                /// Call this method from your game code when setting a trigger on the animator (hash version).
                /// Example: rememberAnimator.NotifyTriggerSet(triggerHash); animator.SetTrigger(triggerHash);
                /// </summary>
                public void NotifyTriggerSet(int triggerHash)
                {
                        // Find the trigger name from hash
                        foreach (var param in targetAnimator.parameters)
                        {
                                if (param.nameHash == triggerHash && param.type == AnimatorControllerParameterType.Trigger)
                                {
                                        triggersSetThisFrame.Add(param.name);
                                        
                                        if (enableDebugLogging)
                                        {
                                                Logger.Log($"RememberAnimator: Trigger '{param.name}' notification received on '{gameObject.name}'.", LogCategory.RememberAnimator, LogLevel.Info);
                                        }
                                        break;
                                }
                        }
                }

		/// <summary>
		/// Serializes the Animator's parameters and (optionally) per-layer state info.
		/// </summary>
		/// <returns>Serialized byte array of AnimatorData.</returns>
		protected override byte[] SerializeComponentData()
		{
                        if (targetAnimator == null)
                        {
                                // No Animator on this object (or disabled).
                                return Array.Empty<byte>();
                        }

                        if (!TryCaptureCurrentState(out var snapshot, true))
                        {
                                return null;
                        }

                        if (skipSavingWhenUnchanged && cachedAnimatorSnapshotCaptured && cachedAnimatorSnapshot != null)
                        {
                                if (AreEquivalent(cachedAnimatorSnapshot, snapshot))
                                {
                                        if (cachedSerializedData != null && cachedSerializedData.Length > 0)
                                        {
                                                // Clear trigger tracking after serialization
                                                triggersSetThisFrame.Clear();
                                                return cachedSerializedData;
                                        }
                                }
                        }

                        byte[] serialized = SaveDataSerializer.Instance.Serialize(snapshot);

                        if (skipSavingWhenUnchanged)
                        {
                                CacheAnimatorSnapshot(snapshot);
                                cachedSerializedData = serialized;
                        }

                        if (enableDebugLogging)
                        {
                            Logger.Log($"[RememberAnimator] Serializing for UniqueIdentifier: '{UniqueIdentifier}'. {serialized.Length} bytes. Params: {saveAnimatorParameters}, Layers: {saveLayerStateInfo}.", LogCategory.RememberAnimator, LogLevel.Info);
                        }

                        // Clear trigger tracking after serialization - ready for next frame
                        triggersSetThisFrame.Clear();

                        return serialized;
                }

	/// <summary>
	/// Deserializes and applies the saved Animator parameters + partial progress if available.
	/// </summary>
	/// <param name="data">Serialized byte array of AnimatorData.</param>
	protected override void DeserializeComponentData(byte[] data)
	{
		// DEBUG: Log every deserialization call to verify TimeMachine is calling this
		Debug.Log($"[RememberAnimator] DeserializeComponentData called on '{gameObject.name}' (data: {data?.Length ?? 0} bytes, animator: {targetAnimator != null})");
		
		if (data == null || data.Length == 0)
		{
			Logger.Log("RememberAnimator: data is null or empty. Skipping load.", LogCategory.RememberAnimator, LogLevel.Warning);
			return;
		}
		if (targetAnimator == null)
		{
			Logger.Log("RememberAnimator: Animator is missing.", LogCategory.RememberAnimator, LogLevel.Warning);
			return;
		}			try
			{
				AnimatorData deserialized = SaveDataSerializer.Instance.Deserialize<AnimatorData>(data);
				if (deserialized == null)
				{
					Logger.Log("RememberAnimator: Deserialized AnimatorData is null.", LogCategory.RememberAnimator, LogLevel.Warning);
					return;
				}

			// 1) Restore parameters if that was saved
			if (deserialized.SaveParameters && deserialized.Parameters != null)
			{
				// DEBUG: Log parameter restoration
				Debug.Log($"[RememberAnimator] Restoring {deserialized.Parameters.Count} parameters on '{gameObject.name}'");
				
				int restoredCount = 0;
				foreach (var p in deserialized.Parameters)
				{
					int paramHash = Animator.StringToHash(p.Name);
					if (!AnimatorHasParameter(paramHash))
					{
						Logger.Log($"RememberAnimator: Animator has no parameter '{p.Name}'. Skipping.", LogCategory.RememberAnimator, LogLevel.Warning);
						continue;
					}

					switch (p.Type)
					{
						case AnimatorControllerParameterType.Float:
							targetAnimator.SetFloat(p.Name, p.FloatValue);
							restoredCount++;
							break;
						case AnimatorControllerParameterType.Int:
							targetAnimator.SetInteger(p.Name, p.IntValue);
							restoredCount++;
							break;
						case AnimatorControllerParameterType.Bool:
							targetAnimator.SetBool(p.Name, p.BoolValue);
							restoredCount++;
							break;
						case AnimatorControllerParameterType.Trigger:
							if (p.TriggerWasSet)
							{
								targetAnimator.SetTrigger(p.Name);
								restoredCount++;
							}
							break;
					}
				}
				
				Debug.Log($"[RememberAnimator] Successfully restored {restoredCount}/{deserialized.Parameters.Count} parameters on '{gameObject.name}'");
			}
			else
			{
				Debug.LogWarning($"[RememberAnimator] No parameters to restore on '{gameObject.name}' (SaveParameters: {deserialized.SaveParameters}, Parameters: {deserialized.Parameters?.Count ?? 0})");
			}				// 2) Restore layer states if we saved them
                                // During TimeMachine playback, skip state restoration to let animator play naturally
#if CRYSTALSAVE_TIMEMACHINE
                                bool isTimelinePlaying = TimeMachine.GameObjectTimeMachine.IsInitialized && 
                                                        TimeMachine.GameObjectTimeMachine.Instance.IsPlaying;
#else
                                bool isTimelinePlaying = false;
#endif
                                
                                if (saveLayerStateInfo && deserialized.Layers != null && deserialized.Layers.Count > 0 && !isTimelinePlaying)
                                {
                                        float originalSpeed = targetAnimator.speed;
                                        // Pause the Animator so we can forcibly set states at the correct times
                                        targetAnimator.speed = 0f;

					foreach (var layerData in deserialized.Layers)
					{
						int layerIndex = layerData.LayerIndex;
						if (layerIndex < 0 || layerIndex >= targetAnimator.layerCount)
						{
							Logger.Log($"RememberAnimator: Invalid layer index {layerIndex}. Skipping.", LogCategory.RememberAnimator, LogLevel.Warning);
							continue;
						}

						// Force the current state at the stored normalized time
						targetAnimator.Play(layerData.CurrentStateHash, layerIndex, layerData.CurrentStateNormalizedTime);

						// If it was in transition, also forcibly set the next state
						if (layerData.IsInTransition)
						{
							targetAnimator.Play(layerData.NextStateHash, layerIndex, layerData.NextStateNormalizedTime);
						}
					}

                                        // Resume normal speed
                                        targetAnimator.speed = originalSpeed;
                                }

                                Logger.Log($"RememberAnimator: Successfully loaded Animator data for '{gameObject.name}'.", LogCategory.RememberAnimator, LogLevel.Info);

                                if (skipSavingWhenUnchanged && TryCaptureCurrentState(out var refreshedSnapshot, false))
                                {
                                        CacheAnimatorSnapshot(refreshedSnapshot);
                                }
                                else if (skipSavingWhenUnchanged)
                                {
                                        cachedAnimatorSnapshotCaptured = false;
                                        cachedAnimatorSnapshot = null;
                                }
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"RememberAnimator: Error deserializing Animator data: {ex.Message}", LogCategory.RememberAnimator, LogLevel.Error);
                        }
                }

                /// <summary>
                /// Utility method to confirm if the parameter actually exists on this Animator.
                /// </summary>
		private bool AnimatorHasParameter(int paramHash)
		{
			foreach (var param in targetAnimator.parameters)
			{
				if (Animator.StringToHash(param.name) == paramHash)
				{
					return true;
				}
                        }
                        return false;
                }

                private bool TryCaptureCurrentState(out AnimatorData snapshot, bool log)
                {
                        snapshot = null;

                        if (targetAnimator == null)
                        {
                                if (log)
                                {
                                        Logger.Log("RememberAnimator: Animator component missing during capture.", LogCategory.RememberAnimator, LogLevel.Warning);
                                }
                                return false;
                        }

                        if (!saveAnimatorParameters && !saveLayerStateInfo)
                        {
                                if (log)
                                {
                                        Logger.Log("RememberAnimator: No Animator data configured to be saved.", LogCategory.RememberAnimator, LogLevel.Info);
                                }
                                return false;
                        }

                        AnimatorData animatorData = new AnimatorData
                        {
                                SaveParameters = saveAnimatorParameters,
                                Parameters = saveAnimatorParameters ? new List<AnimatorParameterData>() : null,
                                Layers = saveLayerStateInfo ? new List<AnimatorLayerData>() : null,
                        };

                        if (saveAnimatorParameters)
                        {
                                var parameters = targetAnimator.parameters;
                                foreach (var param in parameters)
                                {
                                        AnimatorParameterData paramData = new AnimatorParameterData
                                        {
                                                Name = param.name,
                                                Type = param.type
                                        };

                                        switch (param.type)
                                        {
                                                case AnimatorControllerParameterType.Float:
                                                        paramData.FloatValue = targetAnimator.GetFloat(param.name);
                                                        break;
                                                case AnimatorControllerParameterType.Int:
                                                        paramData.IntValue = targetAnimator.GetInteger(param.name);
                                                        break;
                                                case AnimatorControllerParameterType.Bool:
                                                        paramData.BoolValue = targetAnimator.GetBool(param.name);
                                                        break;
                                                case AnimatorControllerParameterType.Trigger:
                                                        // Triggers don't need to be recorded for continuous snapshot replay.
                                                        // The layer state info already captures the animation state that resulted
                                                        // from the trigger. We only track triggers for save/load scenarios.
                                                        bool triggerWasSet = triggersSetThisFrame.Contains(param.name);
                                                        paramData.TriggerWasSet = triggerWasSet;
                                                        
                                                        if (triggerWasSet && enableDebugLogging)
                                                        {
                                                                Logger.Log($"RememberAnimator: Trigger '{param.name}' captured as SET (via NotifyTriggerSet).", LogCategory.RememberAnimator, LogLevel.Info);
                                                        }
                                                        break;
                                        }

                                        animatorData.Parameters.Add(paramData);
                                }
                        }

                        if (saveLayerStateInfo)
                        {
                                int layerCount = targetAnimator.layerCount;
                                for (int i = 0; i < layerCount; i++)
                                {
                                        AnimatorLayerData layerData = new AnimatorLayerData
                                        {
                                                LayerIndex = i
                                        };

                                        var currentStateInfo = targetAnimator.GetCurrentAnimatorStateInfo(i);
                                        layerData.CurrentStateHash = currentStateInfo.fullPathHash;
                                        layerData.CurrentStateNormalizedTime = currentStateInfo.normalizedTime;

                                        layerData.IsInTransition = targetAnimator.IsInTransition(i);
                                        if (layerData.IsInTransition)
                                        {
                                                var nextStateInfo = targetAnimator.GetNextAnimatorStateInfo(i);
                                                layerData.NextStateHash = nextStateInfo.fullPathHash;
                                                layerData.NextStateNormalizedTime = nextStateInfo.normalizedTime;
                                        }

                                        animatorData.Layers.Add(layerData);
                                }
                        }

                        snapshot = animatorData;
                        return true;
                }

                private void CacheAnimatorSnapshot(AnimatorData snapshot)
                {
                        if (snapshot == null)
                        {
                                cachedAnimatorSnapshotCaptured = false;
                                cachedAnimatorSnapshot = null;
                                return;
                        }

                        cachedAnimatorSnapshot = CloneAnimatorData(snapshot);
                        cachedAnimatorSnapshotCaptured = true;
                }

                private AnimatorData CloneAnimatorData(AnimatorData source)
                {
                        if (source == null)
                        {
                                return null;
                        }

                        AnimatorData clone = new AnimatorData
                        {
                                SaveParameters = source.SaveParameters,
                                Parameters = source.Parameters != null ? new List<AnimatorParameterData>(source.Parameters.Count) : null,
                                Layers = source.Layers != null ? new List<AnimatorLayerData>(source.Layers.Count) : null,
                        };

                        if (source.Parameters != null)
                        {
                                foreach (var parameter in source.Parameters)
                                {
                                        clone.Parameters.Add(new AnimatorParameterData
                                        {
                                                Name = parameter.Name,
                                                Type = parameter.Type,
                                                FloatValue = parameter.FloatValue,
                                                IntValue = parameter.IntValue,
                                                BoolValue = parameter.BoolValue,
                                                TriggerWasSet = parameter.TriggerWasSet
                                        });
                                }
                        }

                        if (source.Layers != null)
                        {
                                foreach (var layer in source.Layers)
                                {
                                        clone.Layers.Add(new AnimatorLayerData
                                        {
                                                LayerIndex = layer.LayerIndex,
                                                CurrentStateHash = layer.CurrentStateHash,
                                                CurrentStateNormalizedTime = layer.CurrentStateNormalizedTime,
                                                IsInTransition = layer.IsInTransition,
                                                NextStateHash = layer.NextStateHash,
                                                NextStateNormalizedTime = layer.NextStateNormalizedTime
                                        });
                                }
                        }

                        return clone;
                }

                private bool AreEquivalent(AnimatorData baseline, AnimatorData snapshot)
                {
                        if (baseline == null || snapshot == null)
                        {
                                return false;
                        }

                        if (baseline.SaveParameters != snapshot.SaveParameters)
                        {
                                return false;
                        }

                        if (baseline.SaveParameters)
                        {
                                if (baseline.Parameters == null || snapshot.Parameters == null)
                                {
                                        return false;
                                }

                                if (baseline.Parameters.Count != snapshot.Parameters.Count)
                                {
                                        return false;
                                }

                                var snapshotParameters = new Dictionary<string, AnimatorParameterData>(snapshot.Parameters.Count);
                                foreach (var parameter in snapshot.Parameters)
                                {
                                        if (!snapshotParameters.ContainsKey(parameter.Name))
                                        {
                                                snapshotParameters.Add(parameter.Name, parameter);
                                        }
                                }

                                foreach (var baselineParameter in baseline.Parameters)
                                {
                                        if (!snapshotParameters.TryGetValue(baselineParameter.Name, out var snapshotParameter))
                                        {
                                                return false;
                                        }

                                        if (baselineParameter.Type != snapshotParameter.Type)
                                        {
                                                return false;
                                        }

                                        switch (baselineParameter.Type)
                                        {
                                                case AnimatorControllerParameterType.Float:
                                                        if (!Mathf.Approximately(baselineParameter.FloatValue, snapshotParameter.FloatValue))
                                                        {
                                                                return false;
                                                        }
                                                        break;
                                                case AnimatorControllerParameterType.Int:
                                                        if (baselineParameter.IntValue != snapshotParameter.IntValue)
                                                        {
                                                                return false;
                                                        }
                                                        break;
                                                case AnimatorControllerParameterType.Bool:
                                                        if (baselineParameter.BoolValue != snapshotParameter.BoolValue)
                                                        {
                                                                return false;
                                                        }
                                                        break;
                                                case AnimatorControllerParameterType.Trigger:
                                                        if (baselineParameter.TriggerWasSet != snapshotParameter.TriggerWasSet)
                                                        {
                                                                return false;
                                                        }
                                                        break;
                                        }
                                }
                        }

                        bool baselineHasLayers = baseline.Layers != null && baseline.Layers.Count > 0;
                        bool snapshotHasLayers = snapshot.Layers != null && snapshot.Layers.Count > 0;

                        if (baselineHasLayers != snapshotHasLayers)
                        {
                                return false;
                        }

                        if (baselineHasLayers)
                        {
                                if (baseline.Layers.Count != snapshot.Layers.Count)
                                {
                                        return false;
                                }

                                var snapshotLayers = new Dictionary<int, AnimatorLayerData>(snapshot.Layers.Count);
                                foreach (var layer in snapshot.Layers)
                                {
                                        if (!snapshotLayers.ContainsKey(layer.LayerIndex))
                                        {
                                                snapshotLayers.Add(layer.LayerIndex, layer);
                                        }
                                }

                                foreach (var baselineLayer in baseline.Layers)
                                {
                                        if (!snapshotLayers.TryGetValue(baselineLayer.LayerIndex, out var snapshotLayer))
                                        {
                                                return false;
                                        }

                                        if (baselineLayer.CurrentStateHash != snapshotLayer.CurrentStateHash)
                                        {
                                                return false;
                                        }

                                        if (!Mathf.Approximately(baselineLayer.CurrentStateNormalizedTime, snapshotLayer.CurrentStateNormalizedTime))
                                        {
                                                return false;
                                        }

                                        if (baselineLayer.IsInTransition != snapshotLayer.IsInTransition)
                                        {
                                                return false;
                                        }

                                        if (baselineLayer.IsInTransition)
                                        {
                                                if (baselineLayer.NextStateHash != snapshotLayer.NextStateHash)
                                                {
                                                        return false;
                                                }

                                                if (!Mathf.Approximately(baselineLayer.NextStateNormalizedTime, snapshotLayer.NextStateNormalizedTime))
                                                {
                                                        return false;
                                                }
                                        }
                                }
                        }

                        return true;
                }
        }

	[MemoryPackable]
	public partial class AnimatorData
	{
		public bool SaveParameters { get; set; }
		public List<AnimatorParameterData> Parameters { get; set; }
		public List<AnimatorLayerData> Layers { get; set; }

		public AnimatorData() { }
	}

	[MemoryPackable]
	public partial class AnimatorLayerData
	{
		public int LayerIndex { get; set; }

		// Current state
		public int CurrentStateHash { get; set; }
		public float CurrentStateNormalizedTime { get; set; }

		// If the layer is in a transition
		public bool IsInTransition { get; set; }
		public int NextStateHash { get; set; }
		public float NextStateNormalizedTime { get; set; }
	}

	[MemoryPackable]
	public partial class AnimatorParameterData
	{
		public string Name { get; set; }  // Name of the parameter
		public AnimatorControllerParameterType Type { get; set; }

		// Potential parameter types
		public float FloatValue { get; set; }
		public int IntValue { get; set; }
		public bool BoolValue { get; set; }

		// If this is a Trigger, store whether it was set (true) or not (false).
		public bool TriggerWasSet { get; set; }

		public AnimatorParameterData() { }
	}
}
#endif