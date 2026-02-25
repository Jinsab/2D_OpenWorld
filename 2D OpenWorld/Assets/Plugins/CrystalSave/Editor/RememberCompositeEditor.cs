#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
// ======================================================================
//  Custom Inspector (multi-object editable) – fold-out list + inspector
// ======================================================================
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Arawn.CrystalSave.Runtime;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
	[CustomEditor(typeof(RememberComposite))]
	[CanEditMultipleObjects]
	public class RememberCompositeEditor : UnityEditor.Editor
	{
		private const float ICON_W = 18f;
		private const float HANDLE_W = 16f;
		private const float HOME_SCENE_ICON_W = 16f;
		private const float FEATURE_ICON_W = 16f;
		private const float ICON_SPACING = 2f;
		private GameObject _targetGO;
	    private bool _isPrefabAsset;

		/*────────────────────────────  DATA  ────────────────────────────*/
		private SerializedObject _soForList;   // our own SO copy (re-creatable)
		private SerializedProperty _typesProp; // = _soForList.FindProperty("_rememberTypes")
		private ReorderableList _list;

		private string[] _displayNames;
		private string[] _typeNames;
		private Dictionary<Type, Texture> _iconCache;
		private Texture _rememberHomeSceneIcon;
#if CRYSTALSAVE_TIMEMACHINE
		private Texture _timeMachineIcon;
#endif
		private Texture _skipSavingWhenUnchangedIcon;

		// currently opened row (only one at a time)
		private string _openFq;
                private UnityEditor.Editor _openEditor;


		/*──────────────────────────  INIT / CLEANUP  ────────────────────*/
		private void OnEnable()
		{
			// Bail out when Unity handed us at least one null target
			if (targets == null || targets.Any(t => t == null))
				return;

			_targetGO = ((RememberComposite)target).gameObject;
#if UNITY_EDITOR
			_isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(_targetGO);
#endif

			EnsureInitialised();
		}
		private void OnDisable()
   {
       DestroyOpenEditor();

#if UNITY_EDITOR
       // called when the component is removed OR inspector is closed.
       // we only care about the "component removed on prefab asset" case.
       if (_isPrefabAsset && _targetGO != null &&
           !_targetGO.GetComponent<RememberComposite>())          // gone?
       {
           bool helpersLeft =
_targetGO.GetComponents<SaveableComponent>()
                        .Length > 0;
           bool uidLeft = _targetGO.GetComponent<UniqueID>() != null;

           if (helpersLeft || uidLeft)
           {
               // open the cleanup window immediately – we are in editor code
               RememberCompositeCleanupWindow.Open(_targetGO);
           }
       }
#endif
   }

	/// <summary>Initialises caches and UI helpers exactly once.</summary>
	private void EnsureInitialised()
	{
		if (_list != null) return;                       // already done

		_soForList = new SerializedObject(targets);
		_typesProp = _soForList.FindProperty("_rememberTypes");
		if (_typesProp == null) return;                  // component not ready

		BuildTypeCache();
		BuildIconCache();
		LoadFeatureIcons();
		BuildReorderableList();
	}        /*──────────────────────────  F I X   B L O C K  ──────────────────────────*/

        // Check if GameObject is a child of a SaveablePrefab (not the root itself)
		private static bool IsChildOfSaveablePrefab(GameObject go)
		{
			if (go == null) return false;
			
			// If this GameObject itself has a SaveablePrefab, it's the root, not a child
			if (go.GetComponent<SaveablePrefab>() != null) return false;
			
			// Check if any parent has a SaveablePrefab component
			Transform parent = go.transform.parent;
			while (parent != null)
			{
				if (parent.GetComponent<SaveablePrefab>() != null)
					return true;
				parent = parent.parent;
			}
			
			return false;
		}

		// Check if UniqueID should be removed (child of SaveablePrefab has UniqueID)
		private static bool NeedsUidRemoval(GameObject go)
		{
			return go != null &&
				   go.GetComponent<UniqueID>() != null &&
				   IsChildOfSaveablePrefab(go);
		}

        // Show banner only when UniqueID is missing (and NOT a child of SaveablePrefab)
		private static bool NeedsUidFix(GameObject go)
		{
			// Don't show fix banner for children of SaveablePrefabs - they shouldn't have UniqueID
			if (IsChildOfSaveablePrefab(go))
				return false;
			
			return go != null &&
				(go.GetComponent<UniqueID>()      == null ||   // UID gone
					go.GetComponent<SaveablePrefab>() == null);   // OR SaveablePrefab gone
		}

        // Adds a hidden UniqueID component (Undo-aware, prefab-asset safe)
        private static void AddUniqueId(GameObject go)
        {
            if (go == null || go.GetComponent<UniqueID>() != null) return;

            var uid = Undo.AddComponent<UniqueID>(go);
            uid.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;

            if (go.scene.IsValid())                             // normal scene object
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            else                                                // prefab-asset editing stage
                EditorUtility.SetDirty(go);
        }

		// Removes UniqueID component from GameObject (Undo-aware)
		private static void RemoveUniqueId(GameObject go)
		{
			if (go == null) return;
			
			UniqueID uid = go.GetComponent<UniqueID>();
			if (uid == null) return;
			
			Undo.DestroyObjectImmediate(uid);
			
			if (go.scene.IsValid())
				UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
			else
				EditorUtility.SetDirty(go);
			
			Debug.Log($"[Crystal Save] Removed UniqueID component from '{go.name}' (child of SaveablePrefab)");
		}

        /*────────────────────  INSPECTOR EXTENSION  ────────────────────*/
		private void DrawFixUI()
		{
			// banner shows only if every selected GO lacks both components
			bool show = targets.OfType<RememberComposite>()
							.Any(rc => NeedsUidFix(rc?.gameObject));
			if (!show) return;

			EditorGUILayout.Space(8);
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				// ── headline with red error symbol ──────────────────────────
				var icon = EditorGUIUtility.IconContent("console.erroricon").image;
				EditorGUILayout.BeginHorizontal();
				GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
				EditorGUILayout.LabelField(
					"Unique ID needs to be fixed.",
					EditorStyles.wordWrappedLabel);
                        EditorGUILayout.EndHorizontal();


				// ── Fix button ───────────────────────────────────────────────
				if (GUILayout.Button("Fix", GUILayout.Height(22)))
				{
					foreach (var rc in targets.OfType<RememberComposite>())
						AddUniqueId(rc?.gameObject);
				}
			}
		}

		private void DrawRemoveUniqueIdUI()
		{
			// Check if any target is a child of SaveablePrefab and has UniqueID
			bool needsRemoval = targets.OfType<RememberComposite>()
								.Any(rc => NeedsUidRemoval(rc?.gameObject));
			
			if (!needsRemoval) return;

			EditorGUILayout.Space(8);
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				// Warning icon
				var icon = EditorGUIUtility.IconContent("console.warnicon").image;
				EditorGUILayout.BeginHorizontal();
				GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
				EditorGUILayout.LabelField(
					"⚠️ UniqueID Component Detected on Child of SaveablePrefab",
					EditorStyles.boldLabel);
				EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space(4);
			EditorGUILayout.HelpBox(
				"This GameObject is a child of a SaveablePrefab and has a UniqueID component.\n\n" +
				"📌 WHAT THIS MEANS:\n" +
				"When you save a prefab in your Project folder, child objects should NOT have UniqueID components. " +
				"The child automatically uses the parent SaveablePrefab's ID to save and load its data.\n\n" +
				"🎮 WHEN TO FIX:\n" +
				"• Fix this if you see it while editing a prefab in the Project folder\n" +
				"• Fix this if you see it on a prefab instance you just placed in the scene\n\n" +
				"✅ WHEN TO IGNORE:\n" +
				"• Ignore this warning if it appears during play mode\n" +
				"• Ignore this if a scene object (not from a prefab) was parented to this SaveablePrefab at runtime\n" +
				"• Runtime hierarchy changes are handled automatically - no action needed!\n\n" +
				"Click the button below to remove the UniqueID component and fix the prefab setup.",
				MessageType.Warning);				EditorGUILayout.Space(4);
				GUI.backgroundColor = Color.red;
				if (GUILayout.Button("🗑️ Remove UniqueID Component", GUILayout.Height(25)))
				{
					int count = 0;
					foreach (var rc in targets.OfType<RememberComposite>())
					{
						if (NeedsUidRemoval(rc?.gameObject))
						{
							RemoveUniqueId(rc?.gameObject);
							count++;
						}
					}
					
					if (count > 0)
					{
						string msg = count == 1 
							? "Removed UniqueID from 1 GameObject"
							: $"Removed UniqueID from {count} GameObjects";
						Debug.Log($"[Crystal Save] {msg}");
					}
				}
				GUI.backgroundColor = Color.white;
			}
		}

		/*──────────────────────────  INSPECTOR  ─────────────────────────*/
                public override void OnInspectorGUI()
                {
                        // Rebuild caches if they got lost (can happen when the
                        // component is hidden by third party tools)
                        if (_list == null || _soForList == null)
                        {
                                EnsureInitialised();
                                if (_list == null || _soForList == null)
                                {
                                        base.OnInspectorGUI();
                                        return;
                                }
                        }

                        // draw the ReorderableList (uses _soForList)
                        _soForList.Update();
                        _list.DoLayoutList();
                        _soForList.ApplyModifiedProperties();

			DrawOpenInspector();

			// ───── Add-&-Help buttons (90 % / 10 %) ───────────────────────────────
			EditorGUILayout.Space();
			EditorGUILayout.BeginHorizontal();

                        float totalW = EditorGUIUtility.currentViewWidth;
                        float helpW = Mathf.Clamp(totalW * 0.1f, 28f, 60f); // min/max guard
                        float editW = 70f;
                        float addW = totalW - helpW - editW - 12f;  // gutter padding

                        // main "Add Remember Component…" button
                        if (GUILayout.Button("Add Remember Component…", GUILayout.Width(addW), GUILayout.Height(22)))
                        {
                                Rect buttonRect = GUILayoutUtility.GetLastRect();
                                ShowAddMenu(buttonRect);
                        }

                        // edit UniqueID
                        if (GUILayout.Button("Unique ID", GUILayout.Width(editW), GUILayout.Height(22)))
                        {
                                EditUniqueID();
                        }

			// (2) compact “ Help” button  ── ~10 % width, identical to SaveablePrefabEditor
			var helpContent = new GUIContent(" Help",
				EditorGUIUtility.IconContent("_Help").image,
				"Open a quick reference describing SaveableComponents and RememberComposite limits");

			if (GUILayout.Button(helpContent, GUILayout.Height(24), GUILayout.Width(60)))
			{
				SaveableComponentHelpWindow.ShowWindow();
			}

                        EditorGUILayout.EndHorizontal();

                        // Check for UniqueID that needs removal (child of SaveablePrefab)
			DrawRemoveUniqueIdUI();

                        // Draw the banner ONLY when the GameObject has neither component.
			bool needFix = targets.OfType<RememberComposite>()
								.Any(rc =>
								{
									var go = rc?.gameObject;
									return go != null &&
											go.GetComponent<UniqueID>()      == null &&
											go.GetComponent<SaveablePrefab>() == null;
								});

			if (needFix)
				DrawFixUI();
		}

		/*──────────────────────  REORDERABLE LIST  ──────────────────────*/
		private void BuildReorderableList()
		{
			_list = new ReorderableList(_soForList, _typesProp,
										draggable: true, displayHeader: true,
										displayAddButton: false, displayRemoveButton: true);

			_list.drawHeaderCallback =
				r => EditorGUI.LabelField(r, new GUIContent("Remember Components"));

			// fixed row height – we no longer embed inspectors in rows
			_list.elementHeightCallback = _ => EditorGUIUtility.singleLineHeight + 4f;
			_list.drawElementCallback = DrawElement;

			_list.onRemoveCallback = l =>
			{
				if (l.index < 0 || l.index >= _typesProp.arraySize) return;

				string fqRemove = _typesProp.GetArrayElementAtIndex(l.index).stringValue;
				Type tRemove = Type.GetType(fqRemove);

				_typesProp.DeleteArrayElementAtIndex(l.index);
				DestroyAndResync(tRemove);

				if (fqRemove == _openFq) CloseFoldout();   // close inspector
			};
		}

	private void DrawElement(Rect rect, int index, bool _a, bool _f)
	{
		if (index >= _typesProp.arraySize) return;

		SerializedProperty prop = _typesProp.GetArrayElementAtIndex(index);
		string fq = prop.stringValue;
		Type tp = Type.GetType(fq);

		float x0 = rect.x + HANDLE_W;
		float y = rect.y + 1;
		float h = EditorGUIUtility.singleLineHeight;

		// Calculate dynamic space needed for visible feature icons
		int visibleIconCount = CountVisibleFeatureIcons(tp);
		float totalIconSpace = visibleIconCount > 0 
			? (FEATURE_ICON_W * visibleIconCount) + (ICON_SPACING * (visibleIconCount + 1))
			: 0f;
		
		Rect foldR = new Rect(x0, y, 12, h);
		Rect iconR = new Rect(x0 + 14, y, ICON_W, h);
		Rect popupR = new Rect(x0 + 14 + ICON_W + 6, y,
							   rect.width - HANDLE_W - (14 + ICON_W + 6) - totalIconSpace, h);
		
		// Feature icons start position (right side)
		float featureIconsStartX = rect.xMax - totalIconSpace + ICON_SPACING;

		// fold-out toggle
		bool wasOpen = (fq == _openFq);
		bool nowOpen = EditorGUI.Foldout(foldR, wasOpen, GUIContent.none, true);
		if (nowOpen != wasOpen)
		{
			if (nowOpen) { _openFq = fq; RebuildOpenEditor(); }
			else { CloseFoldout(); }
		}

		// icon – guarded against nulls
		if (tp != null && _iconCache != null &&
			_iconCache.TryGetValue(tp, out var tex) && tex != null)
		{
			GUI.DrawTexture(iconR, tex, ScaleMode.ScaleToFit);
		}

		// type drop-down
		DrawTypePopup(popupR, prop, fq);
		
		// Draw feature icons horizontally (only if there are any to draw)
		if (visibleIconCount > 0)
			DrawFeatureIcons(featureIconsStartX, y, h, tp);
	}

	/// <summary>Counts how many feature icons will be visible for this component type.</summary>
	private int CountVisibleFeatureIcons(Type componentType)
	{
		if (componentType == null)
			return 0;
		
		int count = 0;
		
		// Check RememberHomeScene
		if (_rememberHomeSceneIcon != null && HasFeatureEnabled(componentType, CheckRememberHomeScene))
			count++;
		
#if CRYSTALSAVE_TIMEMACHINE
		// Check TimeMachine
		if (_timeMachineIcon != null && HasFeatureEnabled(componentType, CheckTimeMachine))
			count++;
#endif
		
		// Check SkipSavingWhenUnchanged
		if (_skipSavingWhenUnchangedIcon != null && HasFeatureEnabled(componentType, CheckSkipSaving))
			count++;
		
		return count;
	}

	/// <summary>Helper to check if any target has a specific feature enabled.</summary>
	private bool HasFeatureEnabled(Type componentType, System.Func<Component, bool> checkFunc)
	{
		foreach (var obj in targets)
		{
			if (obj is RememberComposite rc && rc != null)
			{
				var component = rc.GetComponent(componentType);
				if (component != null && checkFunc(component))
					return true;
			}
		}
		return false;
	}

	private bool CheckRememberHomeScene(Component component)
	{
		return component is SaveableComponent sc && sc.RememberHomeScene;
	}

#if CRYSTALSAVE_TIMEMACHINE
	private bool CheckTimeMachine(Component component)
	{
		return component is SaveableComponent sc && sc.EnableTimeMachineRecording;
	}
#endif

	private bool CheckSkipSaving(Component component)
	{
		var so = new SerializedObject(component);
		var prop = so.FindProperty("skipSavingWhenUnchanged");
		return prop != null && prop.boolValue;
	}

	/// <summary>Draws all feature icons horizontally.</summary>
	private void DrawFeatureIcons(float startX, float y, float h, Type componentType)
	{
		if (componentType == null)
			return;
		
		float currentX = startX;
		
		// Draw RememberHomeScene icon - only advance X if icon was drawn
		if (DrawRememberHomeSceneIcon(new Rect(currentX, y, FEATURE_ICON_W, h), componentType))
			currentX += FEATURE_ICON_W + ICON_SPACING;
		
#if CRYSTALSAVE_TIMEMACHINE
		// Draw TimeMachine icon - only advance X if icon was drawn
		if (DrawTimeMachineIcon(new Rect(currentX, y, FEATURE_ICON_W, h), componentType))
			currentX += FEATURE_ICON_W + ICON_SPACING;
#endif
		
		// Draw SkipSavingWhenUnchanged icon
		DrawSkipSavingWhenUnchangedIcon(new Rect(currentX, y, FEATURE_ICON_W, h), componentType);
	}

	/// <summary>Draws the RememberHomeScene icon if the component has it enabled.</summary>
	/// <returns>True if the icon was drawn, false otherwise.</returns>
	private bool DrawRememberHomeSceneIcon(Rect rect, Type componentType)
	{
		if (componentType == null || _rememberHomeSceneIcon == null)
			return false;

		// Check if any of the selected RememberComposite objects has this component with RememberHomeScene enabled
		bool hasRememberHomeScene = false;
		
		foreach (var obj in targets)
		{
			if (obj is RememberComposite rc && rc != null)
			{
				var component = rc.GetComponent(componentType) as SaveableComponent;
				if (component != null && component.RememberHomeScene)
				{
					hasRememberHomeScene = true;
					break;
				}
			}
		}

		// Draw the icon if RememberHomeScene is enabled
		if (hasRememberHomeScene)
		{
			var content = new GUIContent(_rememberHomeSceneIcon, "Remember Home Scene is enabled for this component");
			GUI.Label(rect, content);
			return true;
		}
		
		return false;
	}

#if CRYSTALSAVE_TIMEMACHINE
	/// <summary>Draws the TimeMachine icon if the component has it enabled.</summary>
	/// <returns>True if the icon was drawn, false otherwise.</returns>
	private bool DrawTimeMachineIcon(Rect rect, Type componentType)
	{
		if (componentType == null || _timeMachineIcon == null)
			return false;
		
		bool hasTimeMachine = false;
		foreach (var obj in targets)
		{
			if (obj is RememberComposite rc && rc != null)
			{
				var component = rc.GetComponent(componentType) as SaveableComponent;
				if (component != null && component.EnableTimeMachineRecording)
				{
					hasTimeMachine = true;
					break;
				}
			}
		}
		
		if (hasTimeMachine)
		{
			var content = new GUIContent(_timeMachineIcon, "Time Machine Recording is enabled for this component");
			GUI.Label(rect, content);
			return true;
		}
		
		return false;
	}
#endif

	/// <summary>Draws the SkipSavingWhenUnchanged icon if the component has it enabled.</summary>
	/// <returns>True if the icon was drawn, false otherwise.</returns>
	private bool DrawSkipSavingWhenUnchangedIcon(Rect rect, Type componentType)
	{
		if (componentType == null || _skipSavingWhenUnchangedIcon == null)
			return false;
		
		bool hasSkipSaving = false;
		foreach (var obj in targets)
		{
			if (obj is RememberComposite rc && rc != null)
			{
				var component = rc.GetComponent(componentType);
				if (component != null)
				{
					// Use SerializedProperty to access the private skipSavingWhenUnchanged field
					var so = new SerializedObject(component);
					var prop = so.FindProperty("skipSavingWhenUnchanged");
					if (prop != null && prop.boolValue)
					{
						hasSkipSaving = true;
						break;
					}
				}
			}
		}
		
		if (hasSkipSaving)
		{
			var content = new GUIContent(_skipSavingWhenUnchangedIcon, "Skip Saving When Unchanged is enabled for this component");
			GUI.Label(rect, content);
			return true;
		}
		
		return false;
	}
		/*──────────────────────  TYPE POPUP  ────────────────────────────*/
		private void DrawTypePopup(Rect r, SerializedProperty slot, string curFq)
		{
			int cur = Array.IndexOf(_typeNames, curFq);
			
			// Draw a button that looks like a popup
			string displayText = cur >= 0 && cur < _displayNames.Length ? _displayNames[cur] : "Select Component";
			
			if (GUI.Button(r, displayText, EditorStyles.popup))
			{
				// Show searchable dropdown
				SearchableComponentDropdown.Show(
					r,
					_typeNames,
					_displayNames,
					_iconCache,
					selectedIndex =>
					{
						slot.stringValue = _typeNames[selectedIndex];
						SyncTypesToAllTargets();
						
						if (_openFq == curFq) 
						{ 
							_openFq = _typeNames[selectedIndex]; 
							RebuildOpenEditor(); 
						}
					},
					fq => false // Not checking if already added for the edit dropdown
				);
			}
		}

		/*────────────────  BELOW-LIST INSPECTOR AREA  ───────────────────*/
		private void DrawOpenInspector()
		{
			if (_openEditor == null) return;

			EditorGUILayout.Space(6);
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				EditorGUILayout.LabelField("Selected Remember Component", EditorStyles.boldLabel);
				_openEditor.OnInspectorGUI();
			}
		}
		private void RebuildOpenEditor()
		{
			DestroyOpenEditor();
			if (string.IsNullOrEmpty(_openFq)) return;

			var comps = targets
				.Cast<RememberComposite>()
				.Select(rc => rc?.GetInnerComponent(_openFq))
				.Where(c => c != null)
				.Cast<UnityEngine.Object>()
				.ToArray();

			if (comps.Length > 0) _openEditor = CreateEditor(comps);
		}
		private void DestroyOpenEditor()
		{
			if (_openEditor != null) DestroyImmediate(_openEditor);
			_openEditor = null;
		}
                private void CloseFoldout()
                {
                        _openFq = null;
                        DestroyOpenEditor();
                }

                private void EditUniqueID()
                {
                        var go = ((RememberComposite)target).gameObject;
                        var uid = go.GetComponent<UniqueID>();
                        if (uid == null)
                        {
                                EditorUtility.DisplayDialog("Unique ID Missing", "No UniqueID component found on this GameObject.", "OK");
                                return;
                        }
                        UniqueIDEditWindow.Open(uid);
                }

		/*────────────────────────  ADD MENU  ────────────────────────────*/
		private void ShowAddMenu(Rect buttonRect)
		{
			// Use the searchable dropdown instead of GenericMenu
			SearchableComponentDropdown.Show(
				buttonRect,
				_typeNames,
				_displayNames,
				_iconCache,
				selectedIndex =>
				{
					string fq = _typeNames[selectedIndex];
					bool has = ContainsType(_typesProp, fq);
					
					if (has) return;

					int idx = _typesProp.arraySize;
					_typesProp.InsertArrayElementAtIndex(idx);
					_typesProp.GetArrayElementAtIndex(idx).stringValue = fq;
					_soForList.ApplyModifiedProperties();

					foreach (RememberComposite rc in targets)
					{
						if (rc == null) continue;
						if (rc.gameObject.GetComponent(Type.GetType(fq)) == null)
							Undo.AddComponent(rc.gameObject, Type.GetType(fq));
						rc.ScheduleRefresh();
					}
					ApplyAndScheduleRefreshOnPrimary();
				},
				fq => ContainsType(_typesProp, fq) // Check if already added
			);
		}

		/*─────────────────  REMOVE / SYNC HELPERS  ──────────────────────*/
		private void DestroyAndResync(Type removeType)
		{
			// 1) actually destroy the component(s)
			foreach (RememberComposite rc in targets)
			{
				var comp = rc.GetComponent(removeType);
				if (comp) Undo.DestroyObjectImmediate(comp);
			}

			// 2) delay the resync so SerializedObject is recreated next frame
			EditorApplication.delayCall += RefreshSerializedObjectAndList;
		}

		/*───────────────────────────────────────────────────────────
         * Re-creates SerializedObject, _typesProp, and the list
         *──────────────────────────────────────────────────────────*/
		private void RefreshSerializedObjectAndList()
		{
			if (this == null) return;                 // inspector closed?

			_soForList = new SerializedObject(targets);
			_typesProp = _soForList.FindProperty("_rememberTypes");

			BuildReorderableList();
			_soForList.Update();
			Repaint();
		}

		private static bool ContainsType(SerializedProperty arr, string fq)
		{
			for (int i = 0; i < arr.arraySize; i++)
				if (arr.GetArrayElementAtIndex(i).stringValue == fq) return true;
			return false;
		}

		private void SyncTypesToAllTargets()
		{
			_soForList.ApplyModifiedProperties();

			var desired = new List<string>();
			for (int i = 0; i < _typesProp.arraySize; i++)
				desired.Add(_typesProp.GetArrayElementAtIndex(i).stringValue);

			foreach (var obj in targets)
			{
				if (obj == target) continue;
				if (obj is not RememberComposite rc) continue;

				Undo.RecordObject(rc, "Sync Remember Components");
				rc.GetType()
				  .GetField("_rememberTypes", BindingFlags.NonPublic | BindingFlags.Instance)
				  ?.SetValue(rc, new List<string>(desired));
				EditorUtility.SetDirty(rc);
				rc.ScheduleRefresh();
			}
		}

		private void ApplyAndScheduleRefreshOnPrimary()
		{
			if (target is not RememberComposite rc) return;
			if (Application.isPlaying) rc.ScheduleRefresh();
			else rc.RefreshComponents();
		}

		/*────────────────────────  TYPE CACHE  ──────────────────────────*/
		private void BuildTypeCache()
		{
			var all = AppDomain.CurrentDomain.GetAssemblies()
					   .SelectMany(a => a.GetTypes())
					   .Where(t => typeof(SaveableComponent).IsAssignableFrom(t) &&
								   t != typeof(RememberComposite) &&
								   !t.IsAbstract && !t.IsGenericType)
					   .OrderBy(t => t.Name)
					   .ToArray();

			_typeNames = all.Select(t => t.AssemblyQualifiedName).ToArray();
			_displayNames = all.Select(t => ObjectNames.NicifyVariableName(t.Name)).ToArray();
		}

		/*────────────────────────  ICON CACHE  ──────────────────────────*/
		private void BuildIconCache()
		{
			_iconCache = new Dictionary<Type, Texture>();
			var inlineC = new Dictionary<Type, Texture>();   // cache per ByteSourceType

			foreach (string fq in _typeNames)
			{
				Type t = Type.GetType(fq);
				if (t == null) continue;

				Texture tex = null;

				// 0️⃣ inline byte array via [RememberInlineIcon]
				if (Attribute.GetCustomAttribute(t, typeof(RememberInlineIconAttribute))
						is RememberInlineIconAttribute inl &&
					inl.ByteSourceType != null)
				{
					if (!inlineC.TryGetValue(inl.ByteSourceType, out tex) || tex == null)
					{
						var dataP = inl.ByteSourceType.GetProperty("Data",
										BindingFlags.Public | BindingFlags.Static);
						var wF = inl.ByteSourceType.GetField("Width",
										BindingFlags.Public | BindingFlags.Static);
						var hF = inl.ByteSourceType.GetField("Height",
										BindingFlags.Public | BindingFlags.Static);

						if (dataP != null && wF != null && hF != null)
						{
							object buf = dataP.GetValue(null);
							ReadOnlySpan<byte> span = buf switch
							{
								byte[] arr => arr,
								ArraySegment<byte> s => s.AsSpan(),
								ReadOnlyMemory<byte> m => m.Span,
								_ => ReadOnlySpan<byte>.Empty
							};
							if (!span.IsEmpty)
							{
								var t2d = new Texture2D((int)wF.GetValue(null),
														(int)hF.GetValue(null),
														TextureFormat.RGBA32, false)
								{ hideFlags = HideFlags.HideAndDontSave };
								t2d.LoadRawTextureData(span.ToArray());
								t2d.Apply(false, true);
								tex = t2d;
								inlineC[inl.ByteSourceType] = tex;
							}
						}
					}
				}

				// 1️⃣ custom asset via [RememberCustomIcon]
				if (tex == null &&
					Attribute.GetCustomAttribute(t, typeof(RememberCustomIconAttribute))
						is RememberCustomIconAttribute cust)
				{
					string path = AssetDatabase.GUIDToAssetPath(cust.AssetPathOrGuid);
					if (string.IsNullOrEmpty(path)) path = cust.AssetPathOrGuid;
                                        tex = AssetDatabase.LoadAssetAtPath<Texture>(path);
                                        if (tex == null)
                                        {
                                                var loaded = EditorGUIUtility.Load(path);
                                                if (loaded is Sprite sprite)
                                                        tex = sprite.texture;
                                                else
                                                        tex = loaded as Texture;
                                        }
				}

				// 2️⃣ explicit built-in icon via [RememberIcon]
				if (tex == null &&
					Attribute.GetCustomAttribute(t, typeof(RememberIconAttribute))
						is RememberIconAttribute icn)
				{
					tex = EditorGUIUtility.IconContent(icn.IconName).image;
				}

				// 3️⃣ thumbnail of the wrapped component via [RememberTarget]
				if (tex == null &&
					Attribute.GetCustomAttribute(t, typeof(RememberTargetAttribute))
						is RememberTargetAttribute targ &&
					targ.TargetType != null)
				{
					tex = EditorGUIUtility.ObjectContent(null, targ.TargetType).image;
				}

			// 4️⃣ fallback: thumbnail of the Remember-script itself
			tex ??= EditorGUIUtility.ObjectContent(null, t).image;

			_iconCache[t] = tex;
		}
	}

	/// <summary>Loads the RememberHomeScene icon from the Gizmos folder.</summary>
	private void LoadFeatureIcons()
	{
		// Load RememberHomeScene icon
		string homeScenePath = "Assets/Plugins/CrystalSave/Editor/Gizmos/RememberHomeScene.png";
		_rememberHomeSceneIcon = AssetDatabase.LoadAssetAtPath<Texture>(homeScenePath);
		if (_rememberHomeSceneIcon == null)
		{
			var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(homeScenePath);
			if (sprite != null)
				_rememberHomeSceneIcon = sprite.texture;
		}
		
#if CRYSTALSAVE_TIMEMACHINE
		// Load TimeMachine icon
		string timeMachinePath = "Assets/Plugins/CrystalSave/Editor/Gizmos/TimeMachine.png";
		_timeMachineIcon = AssetDatabase.LoadAssetAtPath<Texture>(timeMachinePath);
		if (_timeMachineIcon == null)
		{
			var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(timeMachinePath);
			if (sprite != null)
				_timeMachineIcon = sprite.texture;
		}
#endif
		
		// Load SkipSavingWhenUnchanged icon
		string skipPath = "Assets/Plugins/CrystalSave/Editor/Gizmos/SkipSavingWhenUnchanged.png";
		_skipSavingWhenUnchangedIcon = AssetDatabase.LoadAssetAtPath<Texture>(skipPath);
		if (_skipSavingWhenUnchangedIcon == null)
		{
			var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(skipPath);
			if (sprite != null)
				_skipSavingWhenUnchangedIcon = sprite.texture;
		}
	}
}
}
#endif
#endif
