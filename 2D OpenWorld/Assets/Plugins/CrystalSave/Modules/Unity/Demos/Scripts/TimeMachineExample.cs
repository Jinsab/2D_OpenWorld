#if MEMORYPACK && ARAWN_REMEMBERME && CRYSTALSAVE_TIMEMACHINE
using System.Collections.Generic;
using UnityEngine;
using Arawn.CrystalSave.Runtime.TimeMachine;
using Arawn.CrystalSave.Runtime;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Arawn.CrystalSave.Examples
{
	/// <summary>
	/// Example script demonstrating how to use the GameObject Time Machine.
	/// Attach this to a GameObject to see Time Machine in action.
	/// </summary>
	[AddComponentMenu("Crystal Save/Examples/Time Machine Example")]
	public class TimeMachineExample : MonoBehaviour
	{
		[Header("Time Machine Settings")]
		[Tooltip("Enable automatic recording")]
		[SerializeField] private bool enableRecording = true;

		[Header("Demo Controls - Recording")]
		[Tooltip("Key to manually record a snapshot")]
		[SerializeField] private KeyCode recordSnapshotKey = KeyCode.R;

		[Tooltip("Key to tag current state")]
		[SerializeField] private KeyCode tagKey = KeyCode.Y;

		[Tooltip("Key to clear all snapshots")]
		[SerializeField] private KeyCode clearKey = KeyCode.C;

		[Header("Demo Controls - Playback")]
		[Tooltip("Key to play/pause timeline playback")]
		[SerializeField] private KeyCode playPauseKey = KeyCode.P;

		[Tooltip("Key to stop playback and return to live")]
		[SerializeField] private KeyCode stopKey = KeyCode.O;

		[Tooltip("Key to step backward one snapshot")]
		[SerializeField] private KeyCode stepBackwardKey = KeyCode.Comma;

		[Tooltip("Key to step forward one snapshot")]
		[SerializeField] private KeyCode stepForwardKey = KeyCode.Period;

		[Tooltip("Key to jump to timeline start")]
		[SerializeField] private KeyCode jumpToStartKey = KeyCode.LeftBracket;

		[Tooltip("Key to jump to timeline end")]
		[SerializeField] private KeyCode jumpToEndKey = KeyCode.RightBracket;

		[Tooltip("Key to increase playback speed")]
		[SerializeField] private KeyCode speedUpKey = KeyCode.Equals;

		[Tooltip("Key to decrease playback speed")]
		[SerializeField] private KeyCode speedDownKey = KeyCode.Minus;

		[Tooltip("Key to toggle interpolation on/off")]
		[SerializeField] private KeyCode toggleInterpolationKey = KeyCode.I;

		[Header("Demo Controls - Timeline Branching")]
		[Tooltip("Key to create alternative branch from current state")]
		[SerializeField] private KeyCode createBranchKey = KeyCode.B;

		[Tooltip("Key to switch between Original and Alternative timelines")]
		[SerializeField] private KeyCode switchBranchKey = KeyCode.T;

		[Tooltip("Key to merge Alternative timeline into Original")]
		[SerializeField] private KeyCode mergeTimelinesKey = KeyCode.M;

		[Header("Branching Settings")]
		[Tooltip("Merge strategy when timelines conflict")]
		[SerializeField] private TimelineMergeStrategy mergeStrategy = TimelineMergeStrategy.OriginalWins;

		[Header("Playback Settings")]
		[Tooltip("Playback speed multiplier")]
		[SerializeField] private float playbackSpeed = 1f;

		[Tooltip("Reverse playback direction")]
		[SerializeField] private bool playbackReverse = false;

		[Tooltip("Enable smooth interpolation between snapshots")]
		[SerializeField] private bool enableInterpolation = true;

		[Tooltip("Pause recording during playback (prevents timeline pollution)")]
		[SerializeField] private bool pauseRecordingDuringPlayback = true;

		[Header("Movement (for testing)")]
		[Tooltip("Move the object for testing")]
		[SerializeField] private bool enableMovement = true;
		[SerializeField] private float moveSpeed = 5f;
		[SerializeField] private float rotateSpeed = 50f;

		private TimeMachineRecorder recorder;
		private string uniqueID;
		private bool wasRecordingBeforePlayback = false;
		
		// Playback state
		private bool isPlaying = false;
		private float currentPlaybackTime = 0f;
		private int currentSnapshotIndex = 0;

		#region Input System Compatibility

		// Helper methods for cross-platform input support (Legacy Input Manager + New Input System)
		bool GetKeyDown(KeyCode key)
		{
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
			return Keyboard.current != null && Keyboard.current[ConvertKeyCode(key)].wasPressedThisFrame;
#else
			return Input.GetKeyDown(key);
#endif
		}

		bool GetKey(KeyCode key)
		{
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
			return Keyboard.current != null && Keyboard.current[ConvertKeyCode(key)].isPressed;
#else
			return Input.GetKey(key);
#endif
		}

		float GetAxis(string axisName)
		{
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
			if (Keyboard.current == null) return 0f;
			
			if (axisName == "Horizontal")
			{
				float value = 0f;
				if (Keyboard.current.aKey.isPressed) value -= 1f;
				if (Keyboard.current.dKey.isPressed) value += 1f;
				return value;
			}
			else if (axisName == "Vertical")
			{
				float value = 0f;
				if (Keyboard.current.sKey.isPressed) value -= 1f;
				if (Keyboard.current.wKey.isPressed) value += 1f;
				return value;
			}
			return 0f;
#else
			return Input.GetAxis(axisName);
#endif
		}

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
		UnityEngine.InputSystem.Key ConvertKeyCode(KeyCode keyCode)
		{
			// Convert common KeyCodes to new Input System Keys
			switch (keyCode)
			{
				case KeyCode.A: return UnityEngine.InputSystem.Key.A;
				case KeyCode.B: return UnityEngine.InputSystem.Key.B;
				case KeyCode.C: return UnityEngine.InputSystem.Key.C;
				case KeyCode.D: return UnityEngine.InputSystem.Key.D;
				case KeyCode.E: return UnityEngine.InputSystem.Key.E;
				case KeyCode.F: return UnityEngine.InputSystem.Key.F;
				case KeyCode.G: return UnityEngine.InputSystem.Key.G;
				case KeyCode.H: return UnityEngine.InputSystem.Key.H;
				case KeyCode.I: return UnityEngine.InputSystem.Key.I;
				case KeyCode.J: return UnityEngine.InputSystem.Key.J;
				case KeyCode.K: return UnityEngine.InputSystem.Key.K;
				case KeyCode.L: return UnityEngine.InputSystem.Key.L;
				case KeyCode.M: return UnityEngine.InputSystem.Key.M;
				case KeyCode.N: return UnityEngine.InputSystem.Key.N;
				case KeyCode.O: return UnityEngine.InputSystem.Key.O;
				case KeyCode.P: return UnityEngine.InputSystem.Key.P;
				case KeyCode.Q: return UnityEngine.InputSystem.Key.Q;
				case KeyCode.R: return UnityEngine.InputSystem.Key.R;
				case KeyCode.S: return UnityEngine.InputSystem.Key.S;
				case KeyCode.T: return UnityEngine.InputSystem.Key.T;
				case KeyCode.U: return UnityEngine.InputSystem.Key.U;
				case KeyCode.V: return UnityEngine.InputSystem.Key.V;
				case KeyCode.W: return UnityEngine.InputSystem.Key.W;
				case KeyCode.X: return UnityEngine.InputSystem.Key.X;
				case KeyCode.Y: return UnityEngine.InputSystem.Key.Y;
				case KeyCode.Z: return UnityEngine.InputSystem.Key.Z;
				case KeyCode.Space: return UnityEngine.InputSystem.Key.Space;
				case KeyCode.Return: return UnityEngine.InputSystem.Key.Enter;
				case KeyCode.Escape: return UnityEngine.InputSystem.Key.Escape;
				case KeyCode.LeftShift: return UnityEngine.InputSystem.Key.LeftShift;
				case KeyCode.RightShift: return UnityEngine.InputSystem.Key.RightShift;
				case KeyCode.LeftControl: return UnityEngine.InputSystem.Key.LeftCtrl;
				case KeyCode.RightControl: return UnityEngine.InputSystem.Key.RightCtrl;
				case KeyCode.LeftAlt: return UnityEngine.InputSystem.Key.LeftAlt;
				case KeyCode.RightAlt: return UnityEngine.InputSystem.Key.RightAlt;
				case KeyCode.Tab: return UnityEngine.InputSystem.Key.Tab;
				case KeyCode.Backspace: return UnityEngine.InputSystem.Key.Backspace;
				case KeyCode.Delete: return UnityEngine.InputSystem.Key.Delete;
				case KeyCode.UpArrow: return UnityEngine.InputSystem.Key.UpArrow;
				case KeyCode.DownArrow: return UnityEngine.InputSystem.Key.DownArrow;
				case KeyCode.LeftArrow: return UnityEngine.InputSystem.Key.LeftArrow;
				case KeyCode.RightArrow: return UnityEngine.InputSystem.Key.RightArrow;
				case KeyCode.Alpha0: return UnityEngine.InputSystem.Key.Digit0;
				case KeyCode.Alpha1: return UnityEngine.InputSystem.Key.Digit1;
				case KeyCode.Alpha2: return UnityEngine.InputSystem.Key.Digit2;
				case KeyCode.Alpha3: return UnityEngine.InputSystem.Key.Digit3;
				case KeyCode.Alpha4: return UnityEngine.InputSystem.Key.Digit4;
				case KeyCode.Alpha5: return UnityEngine.InputSystem.Key.Digit5;
				case KeyCode.Alpha6: return UnityEngine.InputSystem.Key.Digit6;
				case KeyCode.Alpha7: return UnityEngine.InputSystem.Key.Digit7;
				case KeyCode.Alpha8: return UnityEngine.InputSystem.Key.Digit8;
				case KeyCode.Alpha9: return UnityEngine.InputSystem.Key.Digit9;
				default: return UnityEngine.InputSystem.Key.Space; // Fallback
			}
		}
#endif

		#endregion

		void Start()
		{
			Debug.Log("[TimeMachineExample] Start() called");
			
			// Check if the Time Machine code is compiled
#if !MEMORYPACK || !ARAWN_REMEMBERME
			Debug.LogError("[TimeMachineExample] CRITICAL: MEMORYPACK or ARAWN_REMEMBERME define is missing! Time Machine will not work.");
			Debug.LogError("[TimeMachineExample] Go to Project Settings > Player > Scripting Define Symbols and add: MEMORYPACK;ARAWN_REMEMBERME");
			return;
#endif

			// Log which input system is active
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
			Debug.Log("[TimeMachineExample] Using NEW Input System");
			if (Keyboard.current == null)
			{
				Debug.LogError("[TimeMachineExample] Keyboard.current is NULL! New Input System may not be initialized.");
			}
#else
			Debug.Log("[TimeMachineExample] Using LEGACY Input Manager");
#endif

			// Check if Time Machine exists
			Debug.Log($"[TimeMachineExample] IsInitialized check: {GameObjectTimeMachine.IsInitialized}");
			
			// Try to access Instance to force initialization
			if (GameObjectTimeMachine.Instance == null)
			{
				Debug.LogError("[TimeMachineExample] GameObjectTimeMachine.Instance is NULL after access!");
			}
			else
			{
				Debug.Log("[TimeMachineExample] GameObjectTimeMachine.Instance successfully created!");
			}

			// Add and configure the recorder component
			if (enableRecording)
			{
				SetupRecorder();
			}

			// Get our unique ID for direct API access
			uniqueID = GameObjectUtilities.GetUniqueID(gameObject);

			Debug.Log($"[TimeMachineExample] Started on '{gameObject.name}'");
			Debug.Log($"Recording: [{recordSnapshotKey}]=Snapshot [{tagKey}]=Tag [{clearKey}]=Clear");
			Debug.Log($"Playback: [{playPauseKey}]=Play/Pause [{stopKey}]=Stop [{stepBackwardKey}/<{stepForwardKey}>]=Step [{jumpToStartKey}/{jumpToEndKey}]=Jump");
			Debug.Log($"Speed: [{speedDownKey}/-{speedUpKey}+]=Adjust [{toggleInterpolationKey}]=Smooth");
		}

		void Update()
		{
			// Debug: Test input detection every frame
			if (GetKeyDown(KeyCode.R))
			{
				Debug.Log("[TimeMachineExample] R key detected in Update!");
			}
			if (GetKeyDown(KeyCode.T))
			{
				Debug.Log("[TimeMachineExample] T key detected in Update!");
			}

			// Demo movement
			if (enableMovement)
			{
				HandleMovement();
			}

			// Demo controls
			HandleInput();

			// Display stats
			if (GetKeyDown(KeyCode.I))
			{
				DisplayInfo();
			}
		}

	void OnGUI()
	{
		// Simple on-screen instructions
		GUILayout.BeginArea(new Rect(10, 10, 400, 350));
		GUILayout.Label($"<b>Time Machine Example</b>");
		
		GUILayout.Label($"<b>Recording:</b>");
		GUILayout.Label($"  [{recordSnapshotKey}] Manual Snapshot");
		GUILayout.Label($"  [{tagKey}] Tag State");
		GUILayout.Label($"  [{clearKey}] Clear History");
		
		GUILayout.Label($"<b>Playback:</b>");
		GUILayout.Label($"  [{playPauseKey}] Play/Pause");
		GUILayout.Label($"  [{stopKey}] Stop");
		GUILayout.Label($"  [{stepBackwardKey}/<{stepForwardKey}>] Step");
		GUILayout.Label($"  [{jumpToStartKey}/{jumpToEndKey}] Jump Start/End");
		GUILayout.Label($"  [{speedDownKey}/{speedUpKey}] Speed -/+");
		GUILayout.Label($"  [{toggleInterpolationKey}] Toggle Smooth");

		GUILayout.Label($"<b>Branching:</b>");
		GUILayout.Label($"  [{createBranchKey}] Create Alt Branch");
		GUILayout.Label($"  [{switchBranchKey}] Switch Branch");
		GUILayout.Label($"  [{mergeTimelinesKey}] Merge Timelines");
		
		if (recorder != null)
		{
			GUILayout.Space(10);
			string recColor = recorder.IsRecording ? "red" : "gray";
			string recStatus = recorder.IsRecording ? "🔴 RECORDING" : "⚫ PAUSED";
			GUILayout.Label($"<color={recColor}><b>{recStatus}</b></color>");
			GUILayout.Label($"Snapshots: {recorder.GetSnapshotCount()}");
		}			if (GameObjectTimeMachine.IsInitialized)
		{
			GUILayout.Label($"Total Snapshots: {GameObjectTimeMachine.Instance.TotalSnapshotCount}");
			GUILayout.Label($"Memory: {FormatBytes(GameObjectTimeMachine.Instance.TotalMemoryUsed)}");
			
			// Active branch indicator
			var branch = GameObjectTimeMachine.Instance.ActiveBranch;
			string branchColor = branch == TimelineBranch.Original ? "cyan" : "yellow";
			GUILayout.Label($"Branch: <color={branchColor}><b>{branch}</b></color>");
			
			// Interpolation status
			string interpColor = enableInterpolation ? "cyan" : "gray";
			GUILayout.Label($"Interpolation: <color={interpColor}>{(enableInterpolation ? "SMOOTH" : "SNAPPY")}</color>");
			
			// Playback status
			if (isPlaying)
			{
				GUILayout.Space(5);
				GUILayout.Label($"<b><color=green>▶ PLAYING</color></b> ({(playbackReverse ? "Reverse" : "Forward")})");
				GUILayout.Label($"Speed: {playbackSpeed:F1}x");
				GUILayout.Label($"Time: {currentPlaybackTime:F2}s");
			}
			else if (currentSnapshotIndex > 0)
			{
				GUILayout.Label($"<b><color=yellow>⏸ PAUSED</color></b>");
				GUILayout.Label($"Snapshot: {currentSnapshotIndex}");
			}
		}			GUILayout.EndArea();
		}

		#region Setup

		void SetupRecorder()
		{
			// Add recorder component if not already present
			recorder = GetComponent<TimeMachineRecorder>();
			if (recorder == null)
			{
				recorder = gameObject.AddComponent<TimeMachineRecorder>();
			}

			// Configure recorder
			recorder.isRecording = true;

			Debug.Log($"[TimeMachineExample] Recorder setup complete on '{gameObject.name}'");
		}

		#endregion

		#region Input Handling

		void HandleInput()
		{
			if (!GameObjectTimeMachine.IsInitialized)
			{
				Debug.LogWarning("[TimeMachineExample] GameObjectTimeMachine not initialized!");
				return;
			}

			// Manual snapshot
			if (GetKeyDown(recordSnapshotKey))
			{
				Debug.Log($"[TimeMachineExample] {recordSnapshotKey} key pressed - Recording snapshot");
				RecordManualSnapshot();
			}

			// Tag current state
			if (GetKeyDown(tagKey))
			{
				Debug.Log($"[TimeMachineExample] {tagKey} key pressed - Tagging state");
				TagCurrentState();
			}

			// Clear history
			if (GetKeyDown(clearKey))
			{
				Debug.Log($"[TimeMachineExample] {clearKey} key pressed - Clearing history");
				ClearHistory();
			}

			// === PLAYBACK CONTROLS ===

			// Play/Pause
			if (GetKeyDown(playPauseKey))
			{
				TogglePlayback();
			}

			// Stop
			if (GetKeyDown(stopKey))
			{
				StopPlayback();
			}

			// Step backward
			if (GetKeyDown(stepBackwardKey))
			{
				StepBackward();
			}

			// Step forward
			if (GetKeyDown(stepForwardKey))
			{
				StepForward();
			}

			// Jump to start
			if (GetKeyDown(jumpToStartKey))
			{
				JumpToStart();
			}

			// Jump to end
			if (GetKeyDown(jumpToEndKey))
			{
				JumpToEnd();
			}

			// Speed up
			if (GetKeyDown(speedUpKey))
			{
				AdjustSpeed(0.25f);
			}

		// Speed down
		if (GetKeyDown(speedDownKey))
		{
			AdjustSpeed(-0.25f);
		}

		// Toggle interpolation
		if (GetKeyDown(toggleInterpolationKey))
		{
			ToggleInterpolation();
		}

		// === TIMELINE BRANCHING CONTROLS ===

		// Create alternative branch
		if (GetKeyDown(createBranchKey))
		{
			CreateBranch();
		}

		// Switch between branches
		if (GetKeyDown(switchBranchKey))
		{
			SwitchBranch();
		}

		// Merge timelines
		if (GetKeyDown(mergeTimelinesKey))
		{
			MergeTimelines();
		}

		// Update playback if playing
		if (isPlaying)
		{
			UpdatePlayback();
		}
	}		void HandleMovement()
		{
			// Don't allow movement during playback
			if (isPlaying || currentSnapshotIndex > 0)
			{
				return;
			}

			// WASD movement
			float h = GetAxis("Horizontal");
			float v = GetAxis("Vertical");
			
			Vector3 movement = new Vector3(h, 0, v) * moveSpeed * Time.deltaTime;
			transform.position += movement;

			// QE rotation
			if (GetKey(KeyCode.Q))
			{
				transform.Rotate(Vector3.up, -rotateSpeed * Time.deltaTime);
			}
			if (GetKey(KeyCode.E))
			{
				transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
			}

			// Space to jump
			if (GetKeyDown(KeyCode.Space))
			{
				transform.position += Vector3.up * 2f;
				if (recorder != null)
				{
					recorder.TagCurrentState("Jump");
				}
			}
		}

		#endregion

		#region Time Machine Operations

		void RecordManualSnapshot()
		{
			if (recorder != null)
			{
				recorder.RecordCurrentState($"Manual-{Time.time:F2}");
				Debug.Log($"[TimeMachineExample] Recorded manual snapshot at {Time.time:F2}s");
			}
			else
			{
				// Direct API usage
				var snapshot = GameObjectTimeMachine.Instance.RecordSnapshot(
					gameObject, 
					$"Manual-{Time.time:F2}"
				);
				
				if (snapshot != null)
				{
					Debug.Log($"[TimeMachineExample] Recorded snapshot via API at {Time.time:F2}s");
				}
			}
		}

		void TagCurrentState()
		{
			string tag = $"UserTag-{Time.time:F2}";
			
			if (recorder != null)
			{
				recorder.TagCurrentState(tag);
			}
			else
			{
				GameObjectTimeMachine.Instance.RecordSnapshot(gameObject, tag);
			}
			
			Debug.Log($"[TimeMachineExample] Tagged current state: {tag}");
		}

		void ClearHistory()
		{
			if (recorder != null)
			{
				recorder.ClearHistory();
			}
			else
			{
				GameObjectTimeMachine.Instance.ClearTimeline(uniqueID);
			}
			
			Debug.Log($"[TimeMachineExample] Cleared snapshot history");
		}

		void DisplayInfo()
		{
			if (!GameObjectTimeMachine.IsInitialized)
		{
			Debug.LogWarning("[TimeMachineExample] Time Machine not initialized");
			return;
		}

		var timeline = GameObjectTimeMachine.Instance.GetTimelineForObject(uniqueID);
		var timeRange = GameObjectTimeMachine.Instance.GetTimelineRange();			Debug.Log($"=== Time Machine Info for '{gameObject.name}' ===");
			Debug.Log($"Unique ID: {uniqueID}");
			Debug.Log($"Snapshot Count: {timeline.Count}");
			Debug.Log($"Time Range: {timeRange.minTime:F2}s to {timeRange.maxTime:F2}s");
			Debug.Log($"Duration: {timeRange.maxTime - timeRange.minTime:F2}s");
			
			// Recording status warning
			if (recorder != null && recorder.IsRecording)
			{
				Debug.LogWarning("⚠️ RECORDING IS ACTIVE - New snapshots will be added to timeline!");
				Debug.LogWarning($"   Max Snapshots: {GameObjectTimeMachine.Instance.Settings.maxSnapshotsPerObject}");
				Debug.LogWarning("   Oldest snapshots will be pruned when limit is reached.");
				Debug.LogWarning($"   TIP: Enable 'Pause Recording During Playback' to prevent timeline pollution!");
			}
			
			// Count tagged snapshots
			int taggedCount = 0;
			foreach (var snapshot in timeline)
			{
				if (snapshot.Metadata != null && snapshot.Metadata.ContainsKey("tag"))
				{
					taggedCount++;
				}
			}
			Debug.Log($"Tagged Snapshots: {taggedCount}");

			// Show first and last snapshots
			if (timeline.Count > 0)
			{
				var first = timeline[0];
				var last = timeline[timeline.Count - 1];
				
				Debug.Log($"First Snapshot: {first.Timestamp:F2}s at {first.PrefabData?.Position}");
				Debug.Log($"Last Snapshot: {last.Timestamp:F2}s at {last.PrefabData?.Position}");
			}

			// Total memory
			long totalMemory = 0;
			foreach (var snapshot in timeline)
			{
				totalMemory += snapshot.EstimateMemorySize();
			}
			Debug.Log($"Estimated Memory: {FormatBytes(totalMemory)}");
		}

		#endregion

		#region Playback Controls

		void TogglePlayback()
		{
			if (!GameObjectTimeMachine.IsInitialized)
				return;

			var timeline = GameObjectTimeMachine.Instance.GetTimelineForObject(uniqueID);
			if (timeline.Count == 0)
			{
				Debug.LogWarning("[TimeMachineExample] No snapshots to play!");
				return;
			}

			isPlaying = !isPlaying;

			if (isPlaying)
			{
				// Start playing
				if (currentSnapshotIndex == 0)
				{
					currentSnapshotIndex = playbackReverse ? timeline.Count - 1 : 0;
					currentPlaybackTime = timeline[currentSnapshotIndex].Timestamp;
				}
				
				// Pause recording during playback to prevent timeline pollution
				if (pauseRecordingDuringPlayback && recorder != null && recorder.IsRecording)
				{
					wasRecordingBeforePlayback = true;
					recorder.isRecording = false;
					Debug.Log("[TimeMachineExample] 🔴 Recording PAUSED during playback");
				}
				
				Debug.Log($"[TimeMachineExample] ▶ PLAY ({(playbackReverse ? "Reverse" : "Forward")}) Speed: {playbackSpeed}x");
			}
			else
			{
				Debug.Log($"[TimeMachineExample] ⏸ PAUSE at snapshot {currentSnapshotIndex}");
			}
		}

		void StopPlayback()
		{
			if (!isPlaying && currentSnapshotIndex == 0)
			{
				Debug.Log("[TimeMachineExample] Already stopped");
				return;
			}

			isPlaying = false;
			currentSnapshotIndex = 0;
			currentPlaybackTime = 0f;
			
			// Resume recording if it was paused
			if (pauseRecordingDuringPlayback && wasRecordingBeforePlayback && recorder != null)
			{
				recorder.isRecording = true;
				wasRecordingBeforePlayback = false;
				Debug.Log("[TimeMachineExample] 🟢 Recording RESUMED");
			}
			
			Debug.Log("[TimeMachineExample] ⏹ STOP - Returned to live mode");
		}

		void StepBackward()
		{
			if (!GameObjectTimeMachine.IsInitialized)
				return;

			var timeline = GameObjectTimeMachine.Instance.GetTimelineForObject(uniqueID);
			if (timeline.Count == 0)
				return;

			isPlaying = false;

			if (currentSnapshotIndex > 0)
			{
				currentSnapshotIndex--;
			}
			else
			{
				currentSnapshotIndex = timeline.Count - 1;
			}

			ApplySnapshotAtIndex(currentSnapshotIndex);
			Debug.Log($"[TimeMachineExample] ◀ Step back to snapshot {currentSnapshotIndex}");
		}

		void StepForward()
		{
			if (!GameObjectTimeMachine.IsInitialized)
				return;

			var timeline = GameObjectTimeMachine.Instance.GetTimelineForObject(uniqueID);
			if (timeline.Count == 0)
				return;

			isPlaying = false;

			if (currentSnapshotIndex < timeline.Count - 1)
			{
				currentSnapshotIndex++;
			}
			else
			{
				currentSnapshotIndex = 0;
			}

			ApplySnapshotAtIndex(currentSnapshotIndex);
			Debug.Log($"[TimeMachineExample] ▶ Step forward to snapshot {currentSnapshotIndex}");
		}

		void JumpToStart()
		{
			if (!GameObjectTimeMachine.IsInitialized)
				return;

			var timeline = GameObjectTimeMachine.Instance.GetTimelineForObject(uniqueID);
			if (timeline.Count == 0)
				return;

			isPlaying = false;
			currentSnapshotIndex = 0;
			ApplySnapshotAtIndex(currentSnapshotIndex);
			Debug.Log($"[TimeMachineExample] ⏮ Jump to START");
		}

		void JumpToEnd()
		{
			if (!GameObjectTimeMachine.IsInitialized)
				return;

			var timeline = GameObjectTimeMachine.Instance.GetTimelineForObject(uniqueID);
			if (timeline.Count == 0)
				return;

			isPlaying = false;
			currentSnapshotIndex = timeline.Count - 1;
			ApplySnapshotAtIndex(currentSnapshotIndex);
			Debug.Log($"[TimeMachineExample] ⏭ Jump to END");
		}

		void AdjustSpeed(float delta)
		{
			playbackSpeed = Mathf.Clamp(playbackSpeed + delta, 0.1f, 5f);
			Debug.Log($"[TimeMachineExample] Speed: {playbackSpeed}x");
		}

		void ToggleInterpolation()
		{
			enableInterpolation = !enableInterpolation;
			Debug.Log($"[TimeMachineExample] Interpolation: {(enableInterpolation ? "ON (Smooth)" : "OFF (Snappy)")}");
		}

		#region Timeline Branching

		void CreateBranch()
		{
			if (!GameObjectTimeMachine.IsInitialized)
				return;

			// Generate a unique branch name using Alt# naming
			string newBranchName = GenerateUniqueBranchName();
			float currentTime = GameObjectTimeMachine.Instance.CurrentTimelinePosition;
			string sourceBranch = GameObjectTimeMachine.Instance.GetActiveBranchName();

			if (GameObjectTimeMachine.Instance.CreateBranchFrom(newBranchName, sourceBranch, currentTime, true))
			{
				Debug.Log($"[TimeMachineExample] Created branch '{newBranchName}' from '{sourceBranch}' at {currentTime:F2}s");
			}
			else
			{
				Debug.LogWarning($"[TimeMachineExample] Failed to create branch from '{sourceBranch}'");
			}
		}

		private string GenerateUniqueBranchName()
		{
			// Use Alt# naming to match the auto-branch system
			int altNumber = 1;
			while (GameObjectTimeMachine.Instance.BranchExists($"Alt{altNumber}"))
			{
				altNumber++;
			}
			return $"Alt{altNumber}";
		}

		void SwitchBranch()
		{
		if (!GameObjectTimeMachine.IsInitialized)
			return;

		var currentBranch = GameObjectTimeMachine.Instance.ActiveBranch;
		var targetBranch = currentBranch == TimelineBranch.Original 
			? TimelineBranch.Alternative 
			: TimelineBranch.Original;

		string targetBranchName = targetBranch == TimelineBranch.Original ? "Original" : "Alternative";
		GameObjectTimeMachine.Instance.SwitchToBranch(targetBranchName);
		Debug.Log($"[TimeMachineExample] Switched from {currentBranch} to {targetBranchName} timeline");
	}		void MergeTimelines()
		{
			if (!GameObjectTimeMachine.IsInitialized)
				return;

			GameObjectTimeMachine.Instance.MergeTimelines(mergeStrategy);
			Debug.Log($"[TimeMachineExample] Merged Alternative timeline into Original using strategy: {mergeStrategy}");
		}

		#endregion

		void UpdatePlayback()
		{
			if (!GameObjectTimeMachine.IsInitialized)
			{
				isPlaying = false;
				return;
			}

			var timeline = GameObjectTimeMachine.Instance.GetTimelineForObject(uniqueID);
			if (timeline.Count == 0)
			{
				isPlaying = false;
				return;
			}

			// Advance time
			float direction = playbackReverse ? -1f : 1f;
			currentPlaybackTime += Time.deltaTime * playbackSpeed * direction;

			// Apply snapshot with or without interpolation
			if (enableInterpolation)
			{
				ApplyInterpolatedSnapshot(timeline, currentPlaybackTime);
			}
			else
			{
				// Find and apply the snapshot at the current playback time (no interpolation)
				var snapshot = GameObjectTimeMachine.Instance.GetSnapshotAtTime(uniqueID, currentPlaybackTime);
				
				if (snapshot != null)
				{
					GameObjectTimeMachine.Instance.ApplySnapshot(gameObject, snapshot);
					
					// Update index
					currentSnapshotIndex = timeline.IndexOf(snapshot);
				}
			}

			// Loop at boundaries
			if (playbackReverse && currentPlaybackTime < timeline[0].Timestamp)
			{
				currentPlaybackTime = timeline[timeline.Count - 1].Timestamp;
				Debug.Log("[TimeMachineExample] ↻ Looped to end");
			}
			else if (!playbackReverse && currentPlaybackTime > timeline[timeline.Count - 1].Timestamp)
			{
				currentPlaybackTime = timeline[0].Timestamp;
				Debug.Log("[TimeMachineExample] ↻ Looped to start");
			}
		}

		void ApplySnapshotAtIndex(int index)
		{
			if (!GameObjectTimeMachine.IsInitialized)
				return;

			var timeline = GameObjectTimeMachine.Instance.GetTimelineForObject(uniqueID);
			if (index < 0 || index >= timeline.Count)
				return;

			var snapshot = timeline[index];
			GameObjectTimeMachine.Instance.ApplySnapshot(gameObject, snapshot);
			currentPlaybackTime = snapshot.Timestamp;
		}

		void ApplyInterpolatedSnapshot(List<GameObjectSnapshot> timeline, float time)
		{
			if (timeline.Count == 0)
				return;

			// Find the two snapshots to interpolate between
			GameObjectSnapshot before = null;
			GameObjectSnapshot after = null;
			float t = 0f; // Interpolation factor (0-1)

			// Find surrounding snapshots
			for (int i = 0; i < timeline.Count - 1; i++)
			{
				if (time >= timeline[i].Timestamp && time <= timeline[i + 1].Timestamp)
				{
					before = timeline[i];
					after = timeline[i + 1];
					
					// Calculate interpolation factor
					float duration = after.Timestamp - before.Timestamp;
					if (duration > 0)
					{
						t = (time - before.Timestamp) / duration;
					}
					
					currentSnapshotIndex = i;
					break;
				}
			}

			// Handle edge cases
			if (before == null)
			{
				// Before first snapshot or after last
				if (time < timeline[0].Timestamp)
				{
					GameObjectTimeMachine.Instance.ApplySnapshot(gameObject, timeline[0]);
					currentSnapshotIndex = 0;
				}
				else
				{
					GameObjectTimeMachine.Instance.ApplySnapshot(gameObject, timeline[timeline.Count - 1]);
					currentSnapshotIndex = timeline.Count - 1;
				}
				return;
			}

			// Interpolate transform only (components are applied from 'before' snapshot)
			if (before.PrefabData != null && after.PrefabData != null)
			{
				// Interpolate position
				Vector3 position = Vector3.Lerp(before.PrefabData.Position, after.PrefabData.Position, t);
				
				// Interpolate rotation (using Slerp for smooth rotation)
				Quaternion rotation = Quaternion.Slerp(before.PrefabData.Rotation, after.PrefabData.Rotation, t);
				
				// Interpolate scale
				Vector3 scale = Vector3.Lerp(before.PrefabData.Scale, after.PrefabData.Scale, t);

				// Apply interpolated transform
				transform.position = position;
				transform.rotation = rotation;
				transform.localScale = scale;

				// Apply component data from the 'before' snapshot
				// (We don't interpolate component values, just use the nearest one)
				if (before.ComponentData != null)
				{
					var components = GetComponents<SaveableComponent>();
					foreach (var component in components)
					{
						string typeKey = component.GetType().FullName;
						if (before.ComponentData.TryGetValue(typeKey, out byte[] data))
						{
							component.LoadData(data);
						}
					}
				}

				// Apply active state from 'before'
				gameObject.SetActive(before.ActiveSelf);
			}
			else
			{
				// Fallback: just apply the 'before' snapshot
				GameObjectTimeMachine.Instance.ApplySnapshot(gameObject, before);
			}
		}

		#endregion

		#region Utilities

		string FormatBytes(long bytes)
		{
			string[] sizes = { "B", "KB", "MB", "GB" };
			double len = bytes;
			int order = 0;
			
			while (len >= 1024 && order < sizes.Length - 1)
			{
				order++;
				len /= 1024;
			}

			return $"{len:0.##} {sizes[order]}";
		}

		#endregion

		#region Advanced Examples

		/// <summary>
		/// Example: Record snapshots at specific intervals
		/// </summary>
		public void RecordAtIntervals(float interval, int count)
		{
			StartCoroutine(RecordAtIntervalsCoroutine(interval, count));
		}

		private System.Collections.IEnumerator RecordAtIntervalsCoroutine(float interval, int count)
		{
			for (int i = 0; i < count; i++)
			{
				GameObjectTimeMachine.Instance.RecordSnapshot(gameObject, $"Interval-{i}");
				Debug.Log($"[TimeMachineExample] Recorded interval snapshot {i + 1}/{count}");
				yield return new WaitForSeconds(interval);
			}
		}

		/// <summary>
		/// Example: Find snapshots with specific tag
		/// </summary>
		public void FindSnapshotsWithTag(string tag)
		{
			var timeline = GameObjectTimeMachine.Instance.GetTimelineForObject(uniqueID);
			var matching = new System.Collections.Generic.List<GameObjectSnapshot>();

			foreach (var snapshot in timeline)
			{
				if (snapshot.Metadata != null && 
				    snapshot.Metadata.TryGetValue("tag", out string snapshotTag) &&
				    snapshotTag.Contains(tag))
				{
					matching.Add(snapshot);
				}
			}

			Debug.Log($"[TimeMachineExample] Found {matching.Count} snapshots with tag '{tag}'");
			
			foreach (var snapshot in matching)
			{
				Debug.Log($"  - {snapshot.Timestamp:F2}s: {snapshot.Metadata["tag"]}");
			}
		}

		/// <summary>
		/// Example: Export and import timeline
		/// </summary>
		public void ExportImportExample()
		{
			// Export
			byte[] data = GameObjectTimeMachine.Instance.ExportTimeline(uniqueID);
			Debug.Log($"[TimeMachineExample] Exported {data.Length} bytes of timeline data");

			// Clear timeline
			GameObjectTimeMachine.Instance.ClearTimeline(uniqueID);
			Debug.Log($"[TimeMachineExample] Cleared timeline");

			// Re-import
			bool success = GameObjectTimeMachine.Instance.ImportTimeline(uniqueID, data);
			Debug.Log($"[TimeMachineExample] Import {(success ? "succeeded" : "failed")}");
		}

		/// <summary>
		/// Example: Subscribe to events
		/// </summary>
		void OnEnable()
		{
			if (GameObjectTimeMachine.IsInitialized)
			{
				SubscribeToEvents();
			}
		}

		void OnDisable()
		{
			if (GameObjectTimeMachine.IsInitialized)
			{
				UnsubscribeFromEvents();
			}
		}

		void SubscribeToEvents()
		{
			var tm = GameObjectTimeMachine.Instance;
			tm.OnSnapshotRecorded += HandleSnapshotRecorded;
			tm.OnSnapshotApplied += HandleSnapshotApplied;
			tm.OnSnapshotsPruned += HandleSnapshotsPruned;
		}

		void UnsubscribeFromEvents()
		{
			if (!GameObjectTimeMachine.IsInitialized)
				return;

			var tm = GameObjectTimeMachine.Instance;
			tm.OnSnapshotRecorded -= HandleSnapshotRecorded;
			tm.OnSnapshotApplied -= HandleSnapshotApplied;
			tm.OnSnapshotsPruned -= HandleSnapshotsPruned;
		}

		void HandleSnapshotRecorded(string id, GameObjectSnapshot snapshot)
		{
			if (id == uniqueID)
			{
				// Debug.Log($"[TimeMachineExample] Snapshot recorded at {snapshot.Timestamp:F2}s");
			}
		}

		void HandleSnapshotApplied(string id, GameObjectSnapshot snapshot)
		{
			if (id == uniqueID)
			{
				Debug.Log($"[TimeMachineExample] Rewound to {snapshot.Timestamp:F2}s");
			}
		}

		void HandleSnapshotsPruned(string id, int count)
		{
			if (id == uniqueID)
			{
				Debug.Log($"[TimeMachineExample] Pruned {count} old snapshots");
			}
		}

		#endregion
	}
}
#else
// If MEMORYPACK or ARAWN_REMEMBERME are not defined, provide a stub class with error message
namespace Arawn.CrystalSave.Examples
{
	using UnityEngine;
	
	[AddComponentMenu("Crystal Save/Examples/Time Machine Example")]
	public class TimeMachineExample : MonoBehaviour
	{
		void Start()
		{
			Debug.LogError("[TimeMachineExample] Time Machine requires MEMORYPACK and ARAWN_REMEMBERME scripting defines!");
			Debug.LogError("[TimeMachineExample] Add these to: Project Settings > Player > Scripting Define Symbols");
			Debug.LogError("[TimeMachineExample] Expected: MEMORYPACK;ARAWN_REMEMBERME");
		}
	}
}
#endif
