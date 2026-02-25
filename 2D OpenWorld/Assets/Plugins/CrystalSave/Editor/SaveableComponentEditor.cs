#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using Logger = Arawn.CrystalSave.Runtime.Logger;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Arawn.CrystalSave.Runtime;
using HomeSceneMode = global::Arawn.CrystalSave.Runtime.HomeSceneMode;

namespace Arawn.CrystalSave.Editor
{
	/// <summary>
	/// Custom Editor for SaveableComponent and all derived classes.
	/// Supports multi-object editing in the Unity Inspector.
	/// </summary>
	[CustomEditor(typeof(SaveableComponent), true)]
	[CanEditMultipleObjects]
	public class SaveableComponentEditor : UnityEditor.Editor
	{
                private static readonly HideFlags kDesiredFlags =
                HideFlags.HideInInspector | HideFlags.NotEditable;

		// Serialized properties
                protected SerializedProperty componentIDProperty;
                protected SerializedProperty saveParentReferenceProperty;
                protected SerializedProperty keepAcrossScenesProperty;
                private SerializedProperty rememberHomeSceneProperty;
                private SerializedProperty homeSceneModeProperty;
                private SerializedProperty homeScenePrefabIDProperty;
                private SerializedProperty loadPriorityProperty;
                private SerializedProperty deferLowPriorityUntilRequestedProperty;

                private SerializedProperty offScreenMaskProperty;
                private SerializedProperty applySavedDataOnRestoreProperty;

#if CRYSTALSAVE_TIMEMACHINE
                // TimeMachine properties
                private SerializedProperty enableTimeMachineRecordingProperty;
                private SerializedProperty autoStartRecordingProperty;
                private SerializedProperty overrideRecordingSettingsProperty;
                private SerializedProperty useIntervalProperty;
                private SerializedProperty snapshotIntervalProperty;
                private SerializedProperty maxSnapshotsProperty;
                
                // TimeMachine Persistence properties
                private SerializedProperty saveTimeMachineSnapshotsProperty;
                private SerializedProperty overrideMaxSaveDurationProperty;
                private SerializedProperty maxSaveDurationProperty;
#endif

		// SerializedProperty for VisibleInScenes
		private SerializedProperty visibleInScenesProperty;

		// Foldout states
		private bool showOffScreenFoldout = true;
		private bool showVisibleInScenesFoldout = true;

		// Flags and variables for editing state
		private bool isEditing = false;
		private string newComponentID = "";

		// Mapping from GameObject to its designated SaveableComponent for editing
		private Dictionary<GameObject, SaveableComponent> designatedComponentsMap;

		// Dictionary to keep track of logged warnings to prevent spamming
		private static readonly Dictionary<string, DateTime> lastLoggedWarnings = new Dictionary<string, DateTime>();
		private const int WarningIntervalSeconds = 10;

                // PrefabRegistry reference (if applicable)
                private PrefabRegistry prefabRegistry;

		protected virtual void OnEnable()
		{
			// Skip if Unity is still reloading scripts or one of the targets is already null
			if (targets == null || targets.Length == 0 || targets.Any(t => t == null))
				return;

			// Force Unity to create the SerializedObject once – it will throw here if it can’t
			try
			{
				_ = serializedObject;
			}
			// Catch *any* editor exception and only swallow the one Unity uses internally
			catch (Exception ex) when (ex.GetType().Name == "SerializedObjectNotCreatableException")
			{
				// Unity will call OnEnable again on the next editor tick
				return;
			}

			/* ---------- your normal initialisation ---------- */
			componentIDProperty = serializedObject.FindProperty("componentID");
                        saveParentReferenceProperty = serializedObject.FindProperty("saveParentReference");
                        keepAcrossScenesProperty = serializedObject.FindProperty("keepAcrossScenes");
                        rememberHomeSceneProperty = serializedObject.FindProperty("rememberHomeScene");
                        homeSceneModeProperty = serializedObject.FindProperty("homeSceneMode");
                        homeScenePrefabIDProperty = serializedObject.FindProperty("homeScenePrefabID");
                        loadPriorityProperty = serializedObject.FindProperty("loadPriority");
                        deferLowPriorityUntilRequestedProperty = serializedObject.FindProperty("deferLowPriorityUntilRequested");

                        offScreenMaskProperty = serializedObject.FindProperty("offScreenMask");
                        applySavedDataOnRestoreProperty = serializedObject.FindProperty("applySavedDataOnRestore");
                        visibleInScenesProperty = serializedObject.FindProperty("VisibleInScenes");

#if CRYSTALSAVE_TIMEMACHINE
                        // Find TimeMachine properties
                        enableTimeMachineRecordingProperty = serializedObject.FindProperty("enableTimeMachineRecording");
                        autoStartRecordingProperty = serializedObject.FindProperty("autoStartRecording");
                        overrideRecordingSettingsProperty = serializedObject.FindProperty("overrideRecordingSettings");
                        useIntervalProperty = serializedObject.FindProperty("useInterval");
                        snapshotIntervalProperty = serializedObject.FindProperty("snapshotInterval");
                        maxSnapshotsProperty = serializedObject.FindProperty("maxSnapshots");
                        
                        // Find TimeMachine Persistence properties
                        saveTimeMachineSnapshotsProperty = serializedObject.FindProperty("saveTimeMachineSnapshots");
                        overrideMaxSaveDurationProperty = serializedObject.FindProperty("overrideMaxSaveDuration");
                        maxSaveDurationProperty = serializedObject.FindProperty("maxSaveDuration");
#endif

                        prefabRegistry = Resources.Load<PrefabRegistry>("PrefabRegistry");

                        designatedComponentsMap = new Dictionary<GameObject, SaveableComponent>();
                        DetermineEditableComponents();
                }

		// ------------------------------------------------------------------ //
		//  Ensure a controller (if present) is hidden
		// ------------------------------------------------------------------ //
                private static void EnsureHidden(PersistentVisibilityController pvc)
                {
                        if (pvc == null) return;

                        // Apply the desired flags if they are missing
                        if ((pvc.hideFlags & kDesiredFlags) != kDesiredFlags)
                        {
                                pvc.hideFlags |= kDesiredFlags;
                                EditorUtility.SetDirty(pvc);               // mark dirty so Unity saves the change
                        }
                }

                private static bool HasDestroyedObjectRestoreMarker(Type type)
                {
                        if (type == null)
                                return false;

                        return Attribute.IsDefined(type, typeof(DestroyedObjectRestoreAttribute), inherit: true);
                }

                private bool ShouldShowDestroyedObjectRestoreSection()
                {
                        if (targets == null || targets.Length == 0)
                                return false;

                        foreach (var obj in targets)
                        {
                                if (obj is not SaveableComponent component)
                                        return false;

                                if (!HasDestroyedObjectRestoreMarker(component.GetType()))
                                        return false;
                        }

                        return true;
                }

		/// <summary>
		/// Determines which SaveableComponents can edit the KeepAcrossScenes property.
		/// Only one SaveableComponent per GameObject is allowed to edit this property.
		/// </summary>
		private void DetermineEditableComponents()
		{
			designatedComponentsMap.Clear();

			// Group the selected components by their GameObjects
			var groupedByGameObject = targets
				.OfType<SaveableComponent>()
				.GroupBy(sc => sc.gameObject);

			foreach (var group in groupedByGameObject)
			{
				GameObject go = group.Key;
				SaveableComponent[] allComponents = go.GetComponents<SaveableComponent>();

				// Designate the first active and enabled SaveableComponent as editable
				SaveableComponent designated = allComponents.FirstOrDefault(sc => sc.enabled && sc.gameObject.activeInHierarchy);

				// Fallback to the first component if none are active and enabled
				if (designated == null && allComponents.Length > 0)
					designated = allComponents[0];

				if (designated != null)
				{
					designatedComponentsMap[go] = designated;
				}
			}
		}

		public override void OnInspectorGUI()
		{
			if (targets == null || targets.Any(t => t == null))
				return;

			foreach (var sc in targets.OfType<SaveableComponent>())
			{
				EnsureHidden(sc ? sc.GetComponent<PersistentVisibilityController>() : null);
			}

                       // Update the serialized object to reflect current values
                       serializedObject.Update();

                       // Re-determine editable components in case of changes
                       DetermineEditableComponents();

                       // ─────────────────────────────────────────────────────
                       //  Notify if RememberGameObject is added to SaveablePrefab
                       // ─────────────────────────────────────────────────────
                       bool anyRememberGO = targets.OfType<RememberGameObject>().Any();
                       if (anyRememberGO)
                       {
                               bool anyPrefabRoot = targets.OfType<RememberGameObject>()
                                       .Any(rg => rg.GetComponent<SaveablePrefab>() != null);
                               bool anyPrefabChild = targets.OfType<RememberGameObject>()
                                       .Any(rg => rg.GetComponent<SaveablePrefab>() == null &&
                                                  rg.GetComponentInParent<SaveablePrefab>(true) != null);

                               if (anyPrefabRoot)
                               {
                                       EditorGUILayout.HelpBox(
                                               "This GameObject is a SaveablePrefab instance. " +
                                               "Its GameObject properties are already tracked, so " +
                                               "RememberGameObject is redundant.",
                                               MessageType.Info);
                               }
                               else if (anyPrefabChild)
                               {
                                       EditorGUILayout.HelpBox(
                                               "This GameObject is a child of a SaveablePrefab. " +
                                               "SaveablePrefab automatically records child state; " +
                                               "RememberGameObject is not needed.",
                                               MessageType.Info);
                               }
                       }

			// --- New Code: Check if any selected GameObject has a parent ---
			bool anyHasParent = targets
				.OfType<SaveableComponent>()
				.Any(sc => sc.transform.parent != null);
			// ------------------------------------------------------------

			// --- New Code: Check if any selected GameObject has a SaveablePrefab ---
			bool anyHasSaveablePrefab = targets
				.OfType<SaveableComponent>()
				.Any(sc => sc.GetComponent<SaveablePrefab>() != null);
			// ------------------------------------------------------------

			// Begin drawing the Inspector
			EditorGUILayout.BeginVertical();

			// --- Core SaveableComponent Properties ---
			EditorGUILayout.LabelField("Saveable Settings", EditorStyles.boldLabel);

			// Removed the editable field for Component ID to prevent duplication

			// --- Keep Across Scenes Property ---
			EditorGUI.BeginChangeCheck();

			// Determine if all selected components are designated to edit KeepAcrossScenes
			bool allDesignated = true;
			foreach (var sc in targets.OfType<SaveableComponent>())
			{
				if (sc == null) continue;
				if (!designatedComponentsMap.TryGetValue(sc.gameObject, out SaveableComponent designatedComp) || designatedComp != sc)
				{
					allDesignated = false;
					break;
				}
			}

			// Determine if any selected components are designated
			bool anyDesignated = targets.OfType<SaveableComponent>().Any(sc =>
				designatedComponentsMap.TryGetValue(sc.gameObject, out SaveableComponent designatedComp) && designatedComp == sc);

			// Label for the Keep Across Scenes property
			GUIContent keepAcrossScenesLabel = new GUIContent("Keep Across Scenes", "If true, this GameObject is preserved across scene loads (DontDestroyOnLoad). Only works if this is a root GameObject.");

			// --- Modified Code: Incorporate parent check and SaveablePrefab check into edit condition ---
			// canEditKeepAcrossScenes is true only if all designated, any designated, no parent, and no SaveablePrefab
			bool canEditKeepAcrossScenes = allDesignated && anyDesignated && !anyHasParent && !anyHasSaveablePrefab;
			// ------------------------------------------------------------

			// Begin disabled group based on whether the property can be edited, and disable when Remember Home Scene is on
			EditorGUI.BeginDisabledGroup(!canEditKeepAcrossScenes || (rememberHomeSceneProperty != null && rememberHomeSceneProperty.boolValue));

			// Draw the Keep AcrossScenes property field
			EditorGUILayout.PropertyField(keepAcrossScenesProperty, keepAcrossScenesLabel);

			// End disabled group
			EditorGUI.EndDisabledGroup();

			// If not all designated, inform the user
			if (!allDesignated)
			{
				EditorGUILayout.HelpBox("Only one SaveableComponent per GameObject can modify 'Keep Across Scenes'. The field is disabled for non-designated components.", MessageType.Info);
			}

			// --- New Code: Inform the user if any selected GameObject has a parent ---
			if (anyHasParent)
			{
				EditorGUILayout.HelpBox("Keep Across Scenes is disabled because one or more selected GameObjects have a parent. Only root GameObjects can use 'Keep Across Scenes'.", MessageType.Info);
			}
			// ------------------------------------------------------------

			// --- New Code: Inform the user if any selected GameObject has a SaveablePrefab ---
			if (anyHasSaveablePrefab)
			{
				EditorGUILayout.HelpBox("Keep Across Scenes is disabled because one or more selected GameObjects have a SaveablePrefab component attached.", MessageType.Info);
			}
			// ------------------------------------------------------------

                        if (EditorGUI.EndChangeCheck())
                        {
                                // Apply changes to serialized properties
                                serializedObject.ApplyModifiedProperties();

				// Iterate through all selected objects
				foreach (var obj in targets)
				{
					SaveableComponent saveableComp = obj as SaveableComponent;
					if (saveableComp == null) continue;

					bool keepAcrossScenes = saveableComp.KeepAcrossScenes;
					bool isRootObject = saveableComp.transform.root == saveableComp.transform;

					if (keepAcrossScenes)
					{
						if (!isRootObject)
						{
							// Log a warning and set to false
							Logger.Log($"'{saveableComp.gameObject.name}': 'Keep Across Scenes' enabled but is not a root object. Setting to false.", LogLevel.Warning);
							Undo.RecordObject(saveableComp, "Set Keep Across Scenes to false");
							saveableComp.KeepAcrossScenes = false;
						}
						else
						{
							// Mutual exclusivity: turning on Keep Across Scenes disables Remember Home Scene
							var so = new SerializedObject(saveableComp);
							var rem = so.FindProperty("rememberHomeScene");
							if (rem != null && rem.boolValue)
							{
								Undo.RecordObject(saveableComp, "Disable Remember Home Scene");
								rem.boolValue = false;
								so.ApplyModifiedProperties();
							}
							PersistentVisibilityController pvc = saveableComp.GetComponent<PersistentVisibilityController>();
							if (pvc == null)
							{
								// Record Undo operation
								Undo.AddComponent<PersistentVisibilityController>(saveableComp.gameObject);
								Logger.Log($"Added PersistentVisibilityController to '{saveableComp.gameObject.name}' as 'Keep Across Scenes' was enabled.");
							}

							EnsureHidden(pvc);
						}
					}
					else
					{
						PersistentVisibilityController pvc = saveableComp.GetComponent<PersistentVisibilityController>();
						if (pvc != null)
						{
							// Record Undo operation
							Undo.DestroyObjectImmediate(pvc);
							Logger.Log($"Removed PersistentVisibilityController from '{saveableComp.gameObject.name}' as 'Keep Across Scenes' was disabled.");
						}
					}
				}

                                // Optionally, show a single dialog summarizing the changes
                                EditorUtility.DisplayDialog("Keep Across Scenes Updated", "Updated 'Keep Across Scenes' and related components for all selected objects.", "OK");
                        }

                        // --- Off-Screen Behaviour / Guidance ---
                        bool anyKeepAcrossScenes = targets.OfType<SaveableComponent>().Any(sc => sc.KeepAcrossScenes);

                        if (anyKeepAcrossScenes)
                        {
                                EditorGUILayout.Space();
                                EditorGUILayout.LabelField("Off-Screen Behaviour", EditorStyles.boldLabel);

                                showOffScreenFoldout = EditorGUILayout.Foldout(
                                        showOffScreenFoldout,
                                        "Components / Rigidbody state when this GameObject is not visible");

                                if (showOffScreenFoldout)
                                {
                                        EditorGUI.indentLevel++;
                                        EditorGUI.BeginChangeCheck();

                                        var newMask = (OffScreenDeactivation)EditorGUILayout.EnumFlagsField(
                                                new GUIContent("Deactivate"),
                                                (OffScreenDeactivation)offScreenMaskProperty.enumValueFlag);

                                        if (EditorGUI.EndChangeCheck())
                                        {
                                                Undo.RecordObjects(targets, "Change Off-Screen Behaviour");
                                                foreach (var obj in targets)
                                                {
                                                        var so = new SerializedObject(obj);
                                                        var prop = so.FindProperty("offScreenMask");
                                                        prop.enumValueFlag = (int)newMask;
                                                        so.ApplyModifiedProperties();
                                                }
                                        }
                                        EditorGUI.indentLevel--;
                                }
                        }
                        else
                        {
                                EditorGUILayout.HelpBox(
                                        "Enable 'Keep Across Scenes' to configure Off-Screen Behaviour for this GameObject.",
                                        MessageType.Info);
                        }

                        bool showDestroyedObjectRestore = ShouldShowDestroyedObjectRestoreSection();
                        if (showDestroyedObjectRestore && applySavedDataOnRestoreProperty != null)
                        {
                                EditorGUILayout.Space();
                                EditorGUILayout.LabelField("Destroyed Object Restore", EditorStyles.boldLabel);
                                EditorGUILayout.PropertyField(
                                        applySavedDataOnRestoreProperty,
                                        new GUIContent(
                                                "Apply Saved Data On Restore",
                                                "Re-apply this component's saved data when its GameObject is restored after being destroyed."
                                        ));
                        }

                        // --- Remember Home Scene (directly after Keep Across Scenes) ---
                        if (rememberHomeSceneProperty != null)
                        {
                                EditorGUILayout.Space();
				EditorGUI.BeginChangeCheck();
				// Each SaveableComponent can independently control its own Remember Home Scene setting
				// When a SaveablePrefab is present, show the synced value as read-only
				var tip =
					"Remember Home Scene\n\n" +
					"- When enabled, this component snapshots its runtime state when leaving a Home Scene and re-applies it when you return.\n" +
					"- Home Scene Mode chooses how the home scene is determined.\n" +
					"- Keep Across Scenes is mutually exclusive: enabling this turns it off and removes the PersistentVisibilityController.";

				if (anyHasSaveablePrefab)
				{
					// Show the actual synced value (read-only) – the prefab editor propagates it
					using (new EditorGUI.DisabledScope(true))
					{
						EditorGUILayout.PropertyField(rememberHomeSceneProperty, new GUIContent("Remember Home Scene", tip));
					}
					EditorGUILayout.HelpBox(
						"Remember Home Scene is synced from the SaveablePrefab on this GameObject. Toggle it on the SaveablePrefab component.",
						MessageType.Info);
				}
				else
				{
					using (new EditorGUI.DisabledScope(keepAcrossScenesProperty.boolValue))
					{
						EditorGUILayout.PropertyField(rememberHomeSceneProperty, new GUIContent("Remember Home Scene", tip));
					}
				}

				if (EditorGUI.EndChangeCheck())
				{
					serializedObject.ApplyModifiedProperties();

					foreach (var obj in targets)
					{
						var sc = obj as SaveableComponent;
						if (sc == null) continue;

						var so = new SerializedObject(sc);
						var rem = so.FindProperty("rememberHomeScene");
						var keep = so.FindProperty("keepAcrossScenes");
						if (rem != null && rem.boolValue)
						{
							// Turn off Keep Across Scenes and remove PVC when enabling Remember Home Scene
							if (keep != null && keep.boolValue)
							{
								Undo.RecordObject(sc, "Disable Keep Across Scenes");
								keep.boolValue = false;
							}
							so.ApplyModifiedProperties();

							var pvc = sc.GetComponent<PersistentVisibilityController>();
							if (pvc != null)
							{
								Undo.DestroyObjectImmediate(pvc);
							}
						}
					}
				}

                                // Home Scene mode appears when Remember Home Scene is enabled
                                bool anyRememberHome = targets.OfType<SaveableComponent>().Any(s => s != null && s.RememberHomeScene);
                                if (anyRememberHome && !anyHasSaveablePrefab)
                                {
                                        EditorGUILayout.PropertyField(homeSceneModeProperty, new GUIContent("Home Scene Mode", "Design Scene – use the scene where this object resides in the editor.\nLast Snapshot Scene – update to the scene captured during the last snapshot."));

                                        if (rememberHomeSceneProperty.boolValue &&
                                                (HomeSceneMode)homeSceneModeProperty.enumValueIndex == HomeSceneMode.LastSnapshotScene)
                                        {
                                               EditorGUILayout.BeginHorizontal();

                                               EditorGUILayout.LabelField(new GUIContent("Prefab ID"), GUILayout.Width(EditorGUIUtility.labelWidth));
                                               homeScenePrefabIDProperty.stringValue = EditorGUILayout.TextField(homeScenePrefabIDProperty.stringValue, GUILayout.ExpandWidth(true));

                                               var options = prefabRegistry != null
                                                       ? prefabRegistry.prefabEntries.Select(e => e.prefab != null ? e.prefab.name : "(Missing)").Append("None").ToArray()
                                                       : new[] { "None" };

                                               int currentIndex = -1;
                                               if (prefabRegistry != null)
                                               {
                                                       currentIndex = prefabRegistry.prefabEntries.FindIndex(e => e.uniqueID == homeScenePrefabIDProperty.stringValue);
                                               }
                                               if (currentIndex < 0) currentIndex = options.Length - 1;

                                               int selectedIndex = EditorGUILayout.Popup(currentIndex, options, GUILayout.ExpandWidth(true));
                                               if (selectedIndex != currentIndex)
                                               {
                                                       if (selectedIndex < options.Length - 1 && prefabRegistry != null)
                                                       {
                                                               homeScenePrefabIDProperty.stringValue = prefabRegistry.prefabEntries[selectedIndex].uniqueID;
                                                       }
                                                       else
                                                       {
                                                               homeScenePrefabIDProperty.stringValue = string.Empty;
                                                       }
                                               }

                                               EditorGUILayout.EndHorizontal();
                                        }
                                }
                    }

                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Load Scheduling", EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.IntSlider(loadPriorityProperty, 0, 100, new GUIContent("Load Priority", "Higher priorities (closer to 100) are restored during the initial load batch. Lower values can be deferred for streaming or progressive reveal."));
                        EditorGUILayout.PropertyField(deferLowPriorityUntilRequestedProperty, new GUIContent("Defer Until Requested", "When enabled, this component's saved state is staged for deferred processing and must be applied manually via the ComponentManager."));
                        EditorGUI.indentLevel--;

#if CRYSTALSAVE_TIMEMACHINE
                        // --- TimeMachine Recording Settings ---
                        if (enableTimeMachineRecordingProperty != null)
                        {
                                EditorGUILayout.Space();

                                // Determine which component on each GameObject is the "first" one with TimeMachine enabled
                                // to decide who owns the settings (similar to Keep Across Scenes logic)
                                Dictionary<GameObject, SaveableComponent> timeMachineDesignatedMap = new Dictionary<GameObject, SaveableComponent>();
                                
                                var groupedByGameObject = targets
                                        .OfType<SaveableComponent>()
                                        .GroupBy(sc => sc.gameObject);

                                foreach (var group in groupedByGameObject)
                                {
                                        GameObject go = group.Key;
                                        SaveableComponent[] allComponents = go.GetComponents<SaveableComponent>();

                                        // Find the first component with enableTimeMachineRecording = true
                                        SaveableComponent firstEnabled = allComponents.FirstOrDefault(sc => 
                                        {
                                                var so = new SerializedObject(sc);
                                                var prop = so.FindProperty("enableTimeMachineRecording");
                                                return prop != null && prop.boolValue;
                                        });

                                        if (firstEnabled != null)
                                        {
                                                timeMachineDesignatedMap[go] = firstEnabled;
                                        }
                                }

                                // Check if all selected components are designated for TimeMachine settings
                                bool allTimeMachineDesignated = true;
                                bool anyTimeMachineDesignated = false;

                                foreach (var sc in targets.OfType<SaveableComponent>())
                                {
                                        if (sc == null) continue;
                                        
                                        if (timeMachineDesignatedMap.TryGetValue(sc.gameObject, out SaveableComponent designatedComp))
                                        {
                                                if (designatedComp == sc)
                                                {
                                                        anyTimeMachineDesignated = true;
                                                }
                                                else
                                                {
                                                        allTimeMachineDesignated = false;
                                                }
                        }
                }

                // Check if this is RememberTimeMachine - if so, disable the checkbox
                bool isRememberTimeMachine = targets.OfType<SaveableComponent>()
                        .All(sc => sc.GetType().Name == "RememberTimeMachine");

                // Enable Time Machine Recording checkbox is always editable per component (except for RememberTimeMachine)
                using (new EditorGUI.DisabledScope(isRememberTimeMachine))
                {
                        EditorGUILayout.PropertyField(enableTimeMachineRecordingProperty, 
                                new GUIContent("Enable Time Machine Recording", 
                                isRememberTimeMachine 
                                        ? "This setting is disabled for RememberTimeMachine to prevent snapshot corruption. RememberTimeMachine only saves/restores the recording history."
                                        : "Enable TimeMachine recording for this component. When multiple SaveableComponents on the same GameObject enable this, they share a single TimeMachineRecorder."));
                }                                // Settings fields are only editable by the designated (first enabled) component
                                bool canEditTimeMachineSettings = allTimeMachineDesignated && anyTimeMachineDesignated;
                                bool anyTimeMachineEnabled = targets.OfType<SaveableComponent>().Any(sc =>
                                {
                                        var so = new SerializedObject(sc);
                                        var prop = so.FindProperty("enableTimeMachineRecording");
                                        return prop != null && prop.boolValue;
                                });

                                // Only show settings if at least one component has recording enabled
                                if (anyTimeMachineEnabled)
                                {
                                        EditorGUI.indentLevel++;
                                        
                                        using (new EditorGUI.DisabledScope(!canEditTimeMachineSettings))
                                        {
                                                EditorGUILayout.PropertyField(autoStartRecordingProperty, 
                                                        new GUIContent("Register On Enable", 
                                                                "Automatically register this GameObject with the TimeMachine system when enabled.\n\n" +
                                                                "✅ ENABLED (default): GameObject auto-registers when component enables\n" +
                                                                "❌ DISABLED: You must manually call StartRecording() via script\n\n" +
                                                                "⚠️ TWO-TIER SYSTEM:\n" +
                                                                "• This flag controls PER-OBJECT registration (Tier 1)\n" +
                                                                "• GameObjectTimeMachine.recordingEnabled controls GLOBAL snapshot capture (Tier 2, defaults to TRUE)\n" +
                                                                "• BOTH must be true for actual recording to happen!\n\n" +
                                                                "When you enter Play Mode with default settings:\n" +
                                                                "1. This object registers itself (autoStartRecording = true)\n" +
                                                                "2. Global recording is already enabled (recordingEnabled = true)\n" +
                                                                "3. Recording starts IMMEDIATELY (snapshots captured every frame/interval)"));
                                                
                                                EditorGUILayout.Space(5);
                                                EditorGUILayout.PropertyField(overrideRecordingSettingsProperty,
                                                        new GUIContent("Override Recording Settings",
                                                                "Override the global recording settings from SaveSettings. When disabled, uses global defaults for Use Interval, Snapshot Interval, and Max Snapshots."));
                                                
                                                // Indent and show recording settings based on override
                                                EditorGUI.indentLevel++;
                                                
                                                // Get global values for display
                                                SaveSettings saveSettings = SaveManager.Instance?.SaveSettings;
                                                bool globalUseInterval = saveSettings != null ? saveSettings.timeMachineUseInterval : true;
                                                float globalSnapshotInterval = saveSettings != null ? saveSettings.timeMachineSnapshotInterval : 0.1f;
                                                int globalMaxSnapshots = saveSettings != null ? saveSettings.timeMachineMaxSnapshots : 500;
                                                
                                                // Show effective values
                                                if (overrideRecordingSettingsProperty.boolValue)
                                                {
                                                        // Show editable fields when overriding
                                                        EditorGUILayout.PropertyField(useIntervalProperty, 
                                                                new GUIContent("Use Interval", "Record snapshots at regular intervals instead of every frame."));
                                                        EditorGUILayout.PropertyField(snapshotIntervalProperty, 
                                                                new GUIContent("Snapshot Interval", 
                                                                        "Time between snapshots in seconds (only used if 'Use Interval' is enabled).\n\n" +
                                                                        "Common Settings:\n" +
                                                                        "• 0.05s = 20 snapshots/sec (very smooth, debug mode)\n" +
                                                                        "• 0.1s = 10 snapshots/sec (recommended for gameplay)\n" +
                                                                        "• 0.2s = 5 snapshots/sec (sufficient for replays)\n" +
                                                                        "• 0.5s = 2 snapshots/sec (slow-moving objects)\n\n" +
                                                                        "Tip: Combine with Max Snapshots to control rewind window.\n" +
                                                                        "Example: 500 snapshots @ 0.1s = 50 seconds of history."));
                                                        EditorGUILayout.PropertyField(maxSnapshotsProperty, 
                                                                new GUIContent("Max Snapshots", 
                                                                        "Maximum number of snapshots to keep (oldest are discarded automatically).\n\n" +
                                                                        "Memory Guide (per GameObject):\n" +
                                                                        "• 500 snapshots @ 0.1s = 50 sec rewind ≈ 300-600 KB\n" +
                                                                        "• 1000 snapshots @ 0.1s = 100 sec rewind ≈ 600-1200 KB\n" +
                                                                        "• 3000 snapshots @ 0.1s = 5 min rewind ≈ 1.8-3.6 MB\n\n" +
                                                                        "CPU Performance (Replay Cost):\n" +
                                                                        "• 100 snapshots replay: ~5-30ms total (negligible!)\n" +
                                                                        "• Real-time playback: ~0.1-0.5ms/frame (no impact)\n" +
                                                                        "• Fast-forward (10x): ~3-5ms/frame (smooth at 60 FPS)\n" +
                                                                        "Replaying snapshots is VERY fast - CPU is not a bottleneck!\n\n" +
                                                                        "Recommendations:\n" +
                                                                        "• Player/Debug: 500-1000 snapshots (very low cost)\n" +
                                                                        "• Replay System: 3000-6000 snapshots (~2-6 MB)\n" +
                                                                        "• Intensive Recording: 10000+ snapshots (~6-12 MB)\n\n" +
                                                                        "On modern systems (32-64 GB RAM), even 10,000 snapshots per object is negligible. " +
                                                                        "Use the TimeMachine Player window to monitor actual memory usage."));
                                                }
                                                else
                                                {
                                                        // Show read-only global values when not overriding
                                                        using (new EditorGUI.DisabledScope(true))
                                                        {
                                                                EditorGUILayout.Toggle(new GUIContent("Use Interval", $"Using global default: {globalUseInterval}"), globalUseInterval);
                                                                EditorGUILayout.FloatField(new GUIContent("Snapshot Interval", $"Using global default: {globalSnapshotInterval}s"), globalSnapshotInterval);
                                                                EditorGUILayout.IntField(new GUIContent("Max Snapshots", $"Using global default: {globalMaxSnapshots}"), globalMaxSnapshots);
                                                        }
                                                }
                                                
                                                // Preview calculation (use effective values)
                                                bool effectiveUseInterval = overrideRecordingSettingsProperty.boolValue ? useIntervalProperty.boolValue : globalUseInterval;
                                                float effectiveInterval = overrideRecordingSettingsProperty.boolValue ? snapshotIntervalProperty.floatValue : globalSnapshotInterval;
                                                int effectiveMaxSnapshots = overrideRecordingSettingsProperty.boolValue ? maxSnapshotsProperty.intValue : globalMaxSnapshots;
                                                
                                                if (effectiveUseInterval)
                                                {
                                                        if (effectiveInterval > 0 && effectiveMaxSnapshots > 0)
                                                        {
                                                                float totalSeconds = effectiveInterval * effectiveMaxSnapshots;
                                                                
                                                                // Calculate estimated memory (using median values from analysis)
                                                                // Simple object: ~0.45 KB, Complex object: ~1.2 KB, using 0.75 KB average
                                                                float estimatedMemoryKB = effectiveMaxSnapshots * 0.75f;
                                                                
                                                                // Format time display
                                                                string timeDisplay;
                                                                if (totalSeconds < 60)
                                                                {
                                                                        timeDisplay = $"{totalSeconds:F1} seconds";
                                                                }
                                                                else if (totalSeconds < 3600)
                                                                {
                                                                        float minutes = totalSeconds / 60f;
                                                                        timeDisplay = $"{minutes:F1} minutes ({totalSeconds:F0} sec)";
                                                                }
                                                                else
                                                                {
                                                                        float hours = totalSeconds / 3600f;
                                                                        float minutes = (totalSeconds % 3600) / 60f;
                                                                        timeDisplay = $"{hours:F1} hours ({minutes:F0} min)";
                                                                }
                                                                
                                                                // Format memory display
                                                                string memoryDisplay;
                                                                if (estimatedMemoryKB < 1024)
                                                                {
                                                                        memoryDisplay = $"{estimatedMemoryKB:F0} KB";
                                                                }
                                                                else
                                                                {
                                                                        float memoryMB = estimatedMemoryKB / 1024f;
                                                                        memoryDisplay = $"{memoryMB:F2} MB";
                                                                }
                                                                
                                                                // Calculate CPU performance estimates
                                                                // Assume average 0.25ms per snapshot for medium complexity
                                                                float replayTimeMs = effectiveMaxSnapshots * 0.25f;
                                                                string cpuDisplay;
                                                                if (replayTimeMs < 16.67f) // Single frame at 60 FPS
                                                                {
                                                                        cpuDisplay = $"~{replayTimeMs:F1}ms (instant replay ✅)";
                                                                }
                                                                else
                                                                {
                                                                        int framesNeeded = Mathf.CeilToInt(replayTimeMs / 10f); // 10ms per frame target
                                                                        cpuDisplay = $"~{replayTimeMs:F1}ms (spread across {framesNeeded} frames)";
                                                                }
                                                                
                                                                string playbackCostDisplay = $"{0.25f:F2}ms/frame";
                                                                
                                                                EditorGUILayout.Space(3);
                                                                EditorGUILayout.HelpBox(
                                                                        $"📊 Rewind Window Preview:\n" +
                                                                        $"⏱️ Duration: {timeDisplay}\n" +
                                                                        $"💾 Est. Memory: {memoryDisplay} (simple: ~{estimatedMemoryKB * 0.6f:F0} KB, complex: ~{estimatedMemoryKB * 1.6f:F0} KB)\n" +
                                                                        $"📸 Capture Rate: {(1f / effectiveInterval):F1} snapshots/second\n" +
                                                                        $"⚡ Replay All: {cpuDisplay}\n" +
                                                                        $"🎮 Real-time Playback: {playbackCostDisplay} (negligible at 60 FPS)",
                                                                        MessageType.None);
                                                        }
                                                }
                                                
                                                EditorGUI.indentLevel--;
                                        }
                                        
                                        EditorGUI.indentLevel--;

                                        // Show info message if settings are disabled
                                        if (!canEditTimeMachineSettings && anyTimeMachineEnabled)
                                        {
                                                EditorGUILayout.HelpBox(
                                                        "TimeMachine settings (Register On Enable, Use Interval, etc.) are controlled by the first SaveableComponent on this GameObject that has 'Enable Time Machine Recording' checked. " +
                                                        "You can still enable/disable recording for this component, but settings are shared.",
                                                        MessageType.Info);
                                        }

                                        // Show recording status in Play Mode
                                        if (Application.isPlaying && enableTimeMachineRecordingProperty.boolValue)
                                        {
                                                bool isGlobalRecordingEnabled = false;
                                                bool isObjectRegistered = false;
                                                
                                                #if CRYSTALSAVE_TIMEMACHINE
                                                if (Arawn.CrystalSave.Runtime.TimeMachine.GameObjectTimeMachine.IsInitialized)
                                                {
                                                        var timeMachine = Arawn.CrystalSave.Runtime.TimeMachine.GameObjectTimeMachine.Instance;
                                                        // Check if global recording is enabled via reflection (recordingEnabled is private)
                                                        var recordingField = timeMachine.GetType().GetField("recordingEnabled", 
                                                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                                        if (recordingField != null)
                                                        {
                                                                isGlobalRecordingEnabled = (bool)recordingField.GetValue(timeMachine);
                                                        }
                                                        
                                                        // Check if this object is registered
                                                        var comp = (SaveableComponent)target;
                                                        isObjectRegistered = timeMachine.IsTracking(comp.gameObject);
                                                }
                                                #endif
                                                
                                                if (isObjectRegistered && isGlobalRecordingEnabled)
                                                {
                                                        EditorGUILayout.HelpBox(
                                                                "🔴 RECORDING ACTIVE - Snapshots are being captured right now!\n" +
                                                                "Both conditions met: Object registered + Global recording enabled",
                                                                MessageType.Warning);
                                                }
                                                else if (isObjectRegistered && !isGlobalRecordingEnabled)
                                                {
                                                        EditorGUILayout.HelpBox(
                                                                "⏸️ REGISTERED BUT PAUSED - Object is tracked but not recording\n" +
                                                                "Call GameObjectTimeMachine.Instance.SetRecordingEnabled(true) to start",
                                                                MessageType.Info);
                                                }
                                                else if (!isObjectRegistered)
                                                {
                                                        EditorGUILayout.HelpBox(
                                                                "⚠️ NOT REGISTERED - TimeMachineRecorder.StartRecording() has not been called",
                                                                MessageType.Warning);
                                                }
                                        }
                                        
                                        // --- TimeMachine Persistence Settings ---
                                        EditorGUILayout.Space();
                                        EditorGUI.indentLevel++;
                                        
                                        EditorGUILayout.PropertyField(saveTimeMachineSnapshotsProperty,
                                                new GUIContent("Save Time Machine Snapshots",
                                                        "Save this GameObject's current state snapshot at save time (position, rotation, scale). This captures the CURRENT STATE only, not the entire recording history.\n\n" +
                                                        "DIFFERENCE FROM RememberTimeMachine:\n" +
                                                        "• This setting: Saves current snapshot (1 point in time)\n" +
                                                        "• RememberTimeMachine: Saves entire recording history (all snapshots for ghost playback)\n\n" +
                                                        "Use this for simple position/state persistence. Use RememberTimeMachine for ghost/clone replay functionality."));
                                        
                                        // Only show override and duration if saving is enabled
                                        if (saveTimeMachineSnapshotsProperty.boolValue)
                                        {
                                                EditorGUI.indentLevel++;
                                                
                                                EditorGUILayout.PropertyField(overrideMaxSaveDurationProperty,
                                                        new GUIContent("Override Max Save Duration",
                                                                "Override the global Max Save Duration setting from SaveSettings. When disabled, uses SaveSettings.timeMachineMaxSaveDuration."));
                                                
                                                // Only show duration field if override is enabled
                                                if (overrideMaxSaveDurationProperty.boolValue)
                                                {
                                                        EditorGUILayout.PropertyField(maxSaveDurationProperty,
                                                                new GUIContent("Max Save Duration",
                                                                        "Maximum duration (seconds) of timeline snapshots to save.\n" +
                                                                        "• Positive value: Save last N seconds\n" +
                                                                        "• 0: Don't save any snapshots\n" +
                                                                        "• -1: Save entire timeline (unlimited)\n" +
                                                                        "Only applies if Override Max Save Duration is enabled."));
                                                        
                                                        // Show help box based on duration value
                                                        float duration = maxSaveDurationProperty.floatValue;
                                                        if (duration < 0)
                                                        {
                                                                EditorGUILayout.HelpBox(
                                                                        "⚠️ UNLIMITED MODE: Entire timeline will be saved (may result in large save files!)",
                                                                        MessageType.Warning);
                                                        }
                                                        else if (duration == 0)
                                                        {
                                                                EditorGUILayout.HelpBox(
                                                                        "❌ PERSISTENCE DISABLED: Recording works but snapshots won't be saved to file.",
                                                                        MessageType.Info);
                                                        }
                                                        else
                                                        {
                                                                EditorGUILayout.HelpBox(
                                                                        $"💾 Will save last {duration} seconds of timeline snapshots.",
                                                                        MessageType.None);
                                                        }
                                                }
                                                else
                                                {
                                                        // Show global setting info
                                                        EditorGUILayout.HelpBox(
                                                                "Using global SaveSettings.timeMachineMaxSaveDuration (default: 30 seconds)",
                                                                MessageType.None);
                                                }
                                                
                                                EditorGUI.indentLevel--;
                                        }
                                        
                                        EditorGUI.indentLevel--;
                                }
                        }
#endif

                        // --- Integrate Visible In Scenes Selection ---
                        // Only show Scene Visibility Settings if all selected objects have Keep Across Scenes enabled
                        bool allKeepAcrossScenes = targets.OfType<SaveableComponent>().All(sc => sc.KeepAcrossScenes);

                        if (allKeepAcrossScenes)
                        {
                                EditorGUILayout.Space();
                                EditorGUILayout.LabelField("Scene Visibility Settings", EditorStyles.boldLabel);
                                EditorGUI.indentLevel++;

                                // Display the scene selection UI
                                DrawVisibleInScenesSection();

                                EditorGUI.indentLevel++;
                        }

                        // --- Drawing Remaining Properties ---
                        // Exclude 'VisibleInScenes' and any subclass-specific properties from automatic drawing
                        var excludeProps = new List<string>
                        {
                                "m_Script", "componentID", "uniqueID",
                                "saveParentReference", "keepAcrossScenes",
                                "applySavedDataOnRestore",
                                "offScreenMask",
                                "VisibleInScenes",
                                // prevent auto-draw of new fields we render manually
                                "rememberHomeScene", "homeScene", "homeSceneMode", "homeScenePrefabID",
                                "loadPriority", "deferLowPriorityUntilRequested",
#if CRYSTALSAVE_TIMEMACHINE
                                // TimeMachine fields - rendered manually
                                "enableTimeMachineRecording", "autoStartRecording", "overrideRecordingSettings", "useInterval",
                                "snapshotInterval", "maxSnapshots",
                                // TimeMachine Persistence fields - rendered manually
                                "saveTimeMachineSnapshots", "overrideMaxSaveDuration", "maxSaveDuration"
#endif
                        };

                        string[] extra = AdditionalExclusions();
                        if (extra != null && extra.Length > 0)
                                excludeProps.AddRange(extra);

                        DrawPropertiesExcluding(serializedObject, excludeProps.ToArray());

                        // Draw properties specific to derived classes before the Component ID section
                        DrawDerivedProperties();

                        // Add spacing for clarity
                        EditorGUILayout.Space();

			// --- Component ID Display ---
			// Display 'componentID' as a read-only field with a bold label
			GUIStyle boldStyle = new GUIStyle(EditorStyles.label)
			{
				fontStyle = FontStyle.Bold
			};

			bool multipleIDs = false;
			string firstID = ((SaveableComponent)targets[0]).ComponentID;

			for (int i = 1; i < targets.Length; i++)
			{
				if (((SaveableComponent)targets[i]).ComponentID != firstID)
				{
					multipleIDs = true;
					break;
				}
			}

			if (multipleIDs)
			{
				EditorGUILayout.LabelField("Component ID", "<Multiple Values>", boldStyle);
			}
			else
			{
				EditorGUILayout.LabelField("Component ID", firstID, boldStyle);
			}

			// Add spacing
			EditorGUILayout.Space();

			// --- Action Buttons ---
			// Begin a horizontal group for buttons
			EditorGUILayout.BeginHorizontal();

			// Add FlexibleSpace to center the buttons
			GUILayout.FlexibleSpace();

			// Show "Edit" button only if a single object is selected
			if (targets.Length == 1)
			{
				if (GUILayout.Button("Edit", GUILayout.Width(100)))
				{
					// Initialize 'newComponentID' with the current value
					newComponentID = componentIDProperty.stringValue;
					isEditing = true;
				}
			}
			else
			{
				EditorGUILayout.HelpBox("Edit button is only available for single selections.", MessageType.Info);
			}

			// Show "Generate New ID" button
			if (GUILayout.Button("Generate New ID", GUILayout.Width(150)))
			{
				// Confirm action with the user
				if (EditorUtility.DisplayDialog("Generate New Component IDs",
					"Are you sure you want to generate new Component IDs for all selected objects? This action cannot be undone and may affect data associations.",
					"Yes", "No"))
				{
					foreach (var obj in targets)
					{
						SaveableComponent saveableComp = obj as SaveableComponent;
						if (saveableComp == null) continue;

						string generatedID = Guid.NewGuid().ToString();
						Undo.RecordObject(saveableComp, "Generate New Component ID");
						saveableComp.ComponentID = generatedID;
						EditorUtility.SetDirty(saveableComp);
						Logger.Log($"Generated new ComponentID '{generatedID}' for '{saveableComp.gameObject.name}'.");
					}

					EditorUtility.DisplayDialog("Component IDs Generated", "New Component IDs have been generated for all selected objects.", "OK");
				}
			}

			// Show "Copy ID" button
			if (GUILayout.Button("Copy ID", GUILayout.Width(100)))
			{
				if (targets.Length == 1)
				{
					EditorGUIUtility.systemCopyBuffer = ((SaveableComponent)targets[0]).ComponentID;
					EditorUtility.DisplayDialog("Copied", "Component ID copied to clipboard.", "OK");
				}
				else
				{
					string allIDs = string.Join("\n", targets.OfType<SaveableComponent>().Select(sc => sc.ComponentID));
					EditorGUIUtility.systemCopyBuffer = allIDs;
					EditorUtility.DisplayDialog("Copied", "Component IDs of all selected objects have been copied to clipboard.", "OK");
				}
			}

			// Add FlexibleSpace to center the buttons
			GUILayout.FlexibleSpace();

			EditorGUILayout.EndHorizontal();

			// --- Editing Interface ---
			// Only allow editing if a single object is selected
			if (isEditing && targets.Length == 1)
			{
				// Begin a vertical group with a box style for better visibility
				EditorGUILayout.BeginVertical("box");
				EditorGUILayout.LabelField("Edit Component ID", EditorStyles.boldLabel);

				// Input field for the new 'componentID'
				newComponentID = EditorGUILayout.TextField("New Component ID", newComponentID);

				// Real-time validation feedback
				if (!IsValidComponentID(newComponentID))
				{
					EditorGUILayout.HelpBox("Component ID cannot be empty or whitespace.", MessageType.Error);
				}

				// Add an "Auto-Generate ID" button within the editing interface
				if (GUILayout.Button("Auto-Generate ID", GUILayout.Width(150)))
				{
					newComponentID = Guid.NewGuid().ToString();
				}

				// Buttons for confirmation
				EditorGUILayout.BeginHorizontal();

				if (GUILayout.Button("OK"))
				{
					// Validate the new 'componentID'
					if (IsValidComponentID(newComponentID))
					{
						// Register Undo operation
						Undo.RecordObject(target, "Edit Component ID");

						// Assign the new 'componentID'
						componentIDProperty.stringValue = newComponentID;
						serializedObject.ApplyModifiedProperties();
						isEditing = false;

						// Mark the target object as dirty to ensure changes are saved
						EditorUtility.SetDirty(target);

						// Log the change
						SaveableComponent saveableComponent = (SaveableComponent)target;
						Logger.Log($"SaveableComponentEditor: Edited ComponentID to '{newComponentID}' for '{saveableComponent.gameObject.name}'.");

						// Notify the user
						EditorUtility.DisplayDialog("Component ID Edited", $"Component ID has been updated to:\n{newComponentID}", "OK");
					}
					else
					{
						EditorUtility.DisplayDialog("Invalid Component ID",
							"The Component ID cannot be null, empty, or consist solely of whitespace.", "OK");
					}
				}

				if (GUILayout.Button("Cancel"))
				{
					// Cancel editing
					isEditing = false;
				}

				EditorGUILayout.EndHorizontal();

				EditorGUILayout.EndVertical();
			}
			else if (isEditing)
			{
				EditorGUILayout.HelpBox("Editing Component ID is only available for single selections.", MessageType.Info);
			}

                        // --- Checking for Duplicate Component IDs ---
                        CheckForDuplicateComponentIDs();

			// Apply any changes to the serialized object
			serializedObject.ApplyModifiedProperties();

			EditorGUILayout.EndVertical();
		}

		/// <summary>
		/// Draws the "Visible In Scenes" section in the Inspector.
		/// Allows users to select which scenes the component should remain visible in.
		/// </summary>
		private void DrawVisibleInScenesSection()
		{
			if (visibleInScenesProperty == null)
			{
				EditorGUILayout.HelpBox("VisibleInScenes property not found on SaveableComponent.", MessageType.Error);
				return;
			}

			// Foldout for better organization
			showVisibleInScenesFoldout = EditorGUILayout.Foldout(showVisibleInScenesFoldout, "Visible In Scenes", true);
			if (!showVisibleInScenesFoldout)
				return;

			EditorGUI.BeginChangeCheck();

			// Get all scene names from build settings
			List<string> allScenes = GetAllSceneNames();

			if (allScenes.Count == 0)
			{
				EditorGUILayout.HelpBox("No scenes found in Build Settings.", MessageType.Warning);
				return;
			}

			EditorGUILayout.LabelField("Select the scenes where this component should remain visible:");

			foreach (string sceneName in allScenes)
			{
				bool allHaveScene = true;
				bool noneHaveScene = true;

				// Check each selected object
				foreach (var obj in targets)
				{
					SaveableComponent sc = obj as SaveableComponent;
					if (sc != null)
					{
						if (sc.VisibleInScenes.Contains(sceneName))
						{
							noneHaveScene = false;
						}
						else
						{
							allHaveScene = false;
						}
					}
				}

				// Determine the toggle state
				bool toggleState;
				if (allHaveScene)
				{
					toggleState = true;
				}
				else if (noneHaveScene)
				{
					toggleState = false;
				}
				else
				{
					// Mixed state
					EditorGUI.showMixedValue = true;
					toggleState = false;
				}

				EditorGUI.BeginChangeCheck();
				bool newToggleState = EditorGUILayout.ToggleLeft(sceneName, toggleState);
				if (EditorGUI.EndChangeCheck())
				{
					foreach (var obj in targets)
					{
						SaveableComponent sc = obj as SaveableComponent;
						if (sc != null)
						{
							SerializedObject so = new SerializedObject(sc);
							SerializedProperty visibleScenesProp = so.FindProperty("VisibleInScenes");
							if (newToggleState)
							{
								if (!sc.VisibleInScenes.Contains(sceneName))
								{
									Undo.RecordObject(sc, "Add Visible Scene");
									visibleScenesProp.arraySize++;
									visibleScenesProp.GetArrayElementAtIndex(visibleScenesProp.arraySize - 1).stringValue = sceneName;
								}
							}
							else
							{
								for (int i = 0; i < visibleScenesProp.arraySize; i++)
								{
									if (visibleScenesProp.GetArrayElementAtIndex(i).stringValue.Equals(sceneName, StringComparison.OrdinalIgnoreCase))
									{
										Undo.RecordObject(sc, "Remove Visible Scene");
										visibleScenesProp.DeleteArrayElementAtIndex(i);
										break;
									}
								}
							}
							so.ApplyModifiedProperties();
							EditorUtility.SetDirty(sc);
						}
					}
				}

				EditorGUI.showMixedValue = false;
			}

			if (EditorGUI.EndChangeCheck())
			{
				// Any additional actions on change can be handled here
			}
		}

		/// </summary>
		/// <returns>List of scene names.</returns>
		private List<string> GetAllSceneNames()
		{
			List<string> sceneNames = new List<string>();
			int sceneCount = EditorBuildSettings.scenes.Length;

			for (int i = 0; i < sceneCount; i++)
			{
				string scenePath = EditorBuildSettings.scenes[i].path;
				string sceneName = Path.GetFileNameWithoutExtension(scenePath);
				sceneNames.Add(sceneName);
			}

			return sceneNames;
		}

		/// <summary>
		/// Checks for duplicate or missing ComponentIDs.
		/// </summary>
		private void CheckForDuplicateComponentIDs()
		{
#pragma warning disable CS0618 // Suppress FindObjectsByType deprecation warning for cross-version compatibility
#if UNITY_2023_1_OR_NEWER
			SaveableComponent[] allSaveableComponents = UnityEngine.Object.FindObjectsByType<SaveableComponent>(
				FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            // For older Unity versions, use FindObjectsOfTypeAll or similar:
            SaveableComponent[] allSaveableComponents = Resources.FindObjectsOfTypeAll<SaveableComponent>();
#endif
#pragma warning restore CS0618

			foreach (var obj in targets)
			{
				SaveableComponent sc = obj as SaveableComponent;
				if (sc != null)
				{
					string currentComponentID = sc.ComponentID;
					if (!string.IsNullOrEmpty(currentComponentID))
					{
						int duplicateCount = allSaveableComponents.Count(p => p.ComponentID == currentComponentID);
						if (duplicateCount > 1)
						{
							/*
							EditorGUILayout.HelpBox(
								$"Warning: The Component ID '{currentComponentID}' is duplicated in the scene. " +
								$"This may cause issues with the save system. Ensure all Component IDs are unique.",
								MessageType.Warning
							);

							string warningKey = $"componentID-{currentComponentID}";
							if (ShouldLogWarning(warningKey))
							{
								Debug.LogWarning($"Duplicate Component ID detected: '{currentComponentID}' is used by multiple SaveableComponent instances in the scene.");
								UpdateLastLoggedTime(warningKey);
							}
							*/
						}
					}
				}
			}
		}

		/// <summary>
		/// Determines if a warning should be logged based on the last logged time.
		/// </summary>
		/// <param name="warningKey">Unique key for the warning.</param>
		/// <returns>True if the warning should be logged.</returns>
		private bool ShouldLogWarning(string warningKey)
		{
			if (!lastLoggedWarnings.TryGetValue(warningKey, out var lastLoggedTime))
			{
				return true;
			}

			return (DateTime.Now - lastLoggedTime).TotalSeconds >= WarningIntervalSeconds;
		}

		/// <summary>
		/// Updates the last logged time for a specific warning key.
		/// </summary>
		/// <param name="warningKey">Unique key for the warning.</param>
		private void UpdateLastLoggedTime(string warningKey)
		{
			lastLoggedWarnings[warningKey] = DateTime.Now;
		}

		/// <summary>
		/// Helper method to display a non-editable text field.
		/// </summary>
		/// <param name="label">The label for the field.</param>
		/// <param name="value">The value to display.</param>
		protected void DisplayNonEditableField(string label, string value)
		{
			EditorGUI.BeginDisabledGroup(true);
			EditorGUILayout.TextField(label, value);
			EditorGUI.EndDisabledGroup();
		}

		/// <summary>
		/// Validates the new Component ID to ensure it's not null, empty, or whitespace.
		/// </summary>
		/// <param name="id">The new Component ID string.</param>
		/// <returns>True if valid; otherwise, false.</returns>
                protected bool IsValidComponentID(string id)
                {
                        return !string.IsNullOrWhiteSpace(id);
                }

                /// <summary>
                /// Specifies additional serialized property names to exclude from automatic drawing.
                /// Derived classes can override to hide their custom-drawn properties.
                /// </summary>
                /// <returns>Array of property names to exclude.</returns>
                protected virtual string[] AdditionalExclusions()
                {
                        return Array.Empty<string>();
                }

                /// <summary>
                /// Draws serialized properties specific to derived classes.
                /// Override this method in derived Editors if additional customization is needed.
                /// </summary>
                protected virtual void DrawDerivedProperties()
		{
			// Example: Draw a property that might be unique per object
			SerializedProperty someDerivedProperty = serializedObject.FindProperty("someDerivedProperty");
			if (someDerivedProperty != null)
			{
				EditorGUILayout.PropertyField(someDerivedProperty, new GUIContent("Some Derived Property"));
			}
		}
	}
}
#endif
#endif