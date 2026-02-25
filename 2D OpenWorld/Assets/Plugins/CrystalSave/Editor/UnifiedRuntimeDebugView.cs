#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Collections;
using Arawn.CrystalSave.Runtime;
using Logger = Arawn.CrystalSave.Runtime.Logger;

namespace Arawn.CrystalSave.Editor
{
	public class RememberMeRuntimeDebugWindow : EditorWindow
	{
		private PrefabManager prefabManager;
		private SaveManager saveManager;

		// Scroll positions for scrollable areas
		private Vector2 prefabScrollPos;
		private Vector2 instantiatedPrefabsScrollPos;
		private Vector2 saveManagerScrollPos;
		private Vector2 trackedObjectsScrollPos;
                private Vector2 persistentVisibilityScrollPos;
                private Vector2 gameObjectStatesScrollPos;

		// Cached data snapshots
		private List<SaveablePrefab> cachedSaveablePrefabs = new List<SaveablePrefab>();
		private Dictionary<string, GameObject> cachedInstantiatedPrefabs = new Dictionary<string, GameObject>();
		private List<string> cachedBuildSceneNames = new List<string>();
                private List<string> cachedDestroyedGameObjectIDs = new List<string>();
                private Dictionary<string, TrackedGameObject> cachedTrackedGameObjects = new Dictionary<string, TrackedGameObject>();
                private List<GameObjectState> cachedGameObjectStates = new List<GameObjectState>();

		// Cached scene lookups – refreshed on demand, not every OnGUI repaint
		private PersistentVisibilityController[] cachedPersistentVisibilityControllers = System.Array.Empty<PersistentVisibilityController>();
		private PrefabManager cachedPrefabManagerForViewer;
		private readonly Dictionary<int, SerializedObject> cachedSerializedObjects = new Dictionary<int, SerializedObject>();

		// Search queries
		private string prefabSearchQuery = "";
		private string trackedObjectSearchQuery = "";

		// Foldout states
		private bool showSaveablePrefabs = true;
		private bool showInstantiatedPrefabs = true;
		private bool showBuildSceneNames = true;
                private bool showDestroyedGameObjectIDs = true;
                private bool showTrackedGameObjects = true;
                private bool showPersistentVisibilityController = true; // This field is now used
                private bool showGameObjectStates = true;

		// GUI Styles (Optional Enhancements)
		private GUIStyle headerStyle;
		private GUIStyle labelStyle;

		// Tabs
		private int selectedTab = 0;
		private string[] tabTitles = new string[] { "Overview", "Persistent Visibility", "Runtime Data Viewer" };

		[MenuItem("Tools/Crystal Save/Runtime Debug/Crystal Save Debug Window")]
		public static void ShowWindow()
		{
			GetWindow<RememberMeRuntimeDebugWindow>("Crystal Save Runtime Debug");
		}

		private void OnEnable()
		{
			Logger.Log("RememberMeRuntimeDebugWindow enabled.", LogLevel.Off);

			// Initialize GUI styles
			InitializeGUIStyles();

			// Subscribe to play mode state changes
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

			if (Application.isPlaying)
			{
				InitializeRuntimeData();
			}
		}

		private void OnDisable()
		{
			Logger.Log("RememberMeRuntimeDebugWindow disabled.", LogLevel.Off);

			// Unsubscribe from runtime events and clear data
			ClearRuntimeData();

			// Unsubscribe from play mode state changes
			EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
		}

		// Handler methods
		private void HandleAllPrefabsInitialized()
		{
			Logger.Log("HandleAllPrefabsInitialized called.");
			RefreshAllData();
			Repaint();
		}

		private void HandleSaveLoadCompleted(object sender, SaveLoadEventArgs e)
		{
			Logger.Log("HandleSaveLoadCompleted called.");
			RefreshAllData();
			Repaint();
		}

		private void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			Logger.Log($"PlayModeStateChanged: {state}");

			switch (state)
			{
				case PlayModeStateChange.EnteredPlayMode:
					Logger.Log("Entered Playmode. Initializing UnifiedRuntimeDebugWindow data.");
					InitializeRuntimeData();
					Repaint();
					break;
				case PlayModeStateChange.ExitingPlayMode:
					Logger.Log("Exiting Playmode. Clearing UnifiedRuntimeDebugWindow data.");
					ClearRuntimeData();
					Repaint();
					break;
				case PlayModeStateChange.EnteredEditMode:
					Logger.Log("Returned to Edit mode. Clearing UnifiedRuntimeDebugWindow data.");
					ClearRuntimeData();
					Repaint();
					break;
				case PlayModeStateChange.ExitingEditMode:
					Logger.Log("Exiting Edit mode.");
					break;
			}
		}

		private void InitializeRuntimeData()
		{
			try
			{
				// Access the singleton instances
				saveManager = SaveManager.Instance;
				if (saveManager != null)
				{
					prefabManager = saveManager.GetPrefabManager;
				}

				// Subscribe to events to repaint the window when data changes
				if (prefabManager != null)
				{
					prefabManager.OnAllPrefabsInitialized += HandleAllPrefabsInitialized;
					// Subscribe to other relevant events if necessary
				}

				if (saveManager != null)
				{
					saveManager.OnLoadCompleted += HandleSaveLoadCompleted;
					saveManager.OnSaveCompleted += HandleSaveLoadCompleted;
					// Subscribe to other relevant events if necessary
				}

				// Initial data refresh
				RefreshAllData();
			}
			catch (System.Exception ex)
			{
				Debug.LogError($"Error during InitializeRuntimeData: {ex.Message}");
			}
		}

		private void ClearRuntimeData()
		{
			try
			{
				// Unsubscribe from events to prevent memory leaks
				if (prefabManager != null)
				{
					prefabManager.OnAllPrefabsInitialized -= HandleAllPrefabsInitialized;
					prefabManager = null;
				}

				if (saveManager != null)
				{
					saveManager.OnLoadCompleted -= HandleSaveLoadCompleted;
					saveManager.OnSaveCompleted -= HandleSaveLoadCompleted;
					saveManager = null;
				}

				// Clear cached data
				cachedSaveablePrefabs.Clear();
				cachedInstantiatedPrefabs.Clear();
                                cachedBuildSceneNames.Clear();
                                cachedDestroyedGameObjectIDs.Clear();
                                cachedTrackedGameObjects.Clear();
                                cachedGameObjectStates.Clear();
				cachedPersistentVisibilityControllers = System.Array.Empty<PersistentVisibilityController>();
				cachedPrefabManagerForViewer = null;
				cachedSerializedObjects.Clear();
			}
			catch (System.Exception ex)
			{
				Debug.LogError($"Error during ClearRuntimeData: {ex.Message}");
			}
		}

		// Updated RefreshAllData with Play mode check
		private void RefreshAllData()
		{
			try
			{
				Logger.Log("Refreshing all data..."); // Diagnostic log

				// Ensure that runtime managers are available
				if (prefabManager == null || saveManager == null)
				{
					Debug.LogWarning("Cannot refresh data: PrefabManager or SaveManager is null.");
					return;
				}

				// Refresh cached data
				RefreshPrefabManagerData();
				RefreshSaveManagerData();
				RefreshSceneLookups();

				Logger.Log("Data refresh complete."); // Diagnostic log
			}
			catch (System.Exception ex)
			{
				Debug.LogError($"Error during RefreshAllData: {ex.Message}");
			}
		}

		private void RefreshPrefabManagerData()
		{
			if (prefabManager != null)
			{
				cachedSaveablePrefabs = new List<SaveablePrefab>(prefabManager.GetSaveablePrefabs());
				cachedInstantiatedPrefabs = new Dictionary<string, GameObject>(prefabManager.GetInstantiatedPrefabs());
				Logger.Log("PrefabManager data refreshed.");
			}
			else
			{
				Debug.LogWarning("PrefabManager is null during RefreshPrefabManagerData.");
			}
		}

		private void RefreshSaveManagerData()
		{
			if (saveManager != null)
			{
                                cachedBuildSceneNames = new List<string>(saveManager.GetBuildSceneNames());
                                cachedDestroyedGameObjectIDs = new List<string>(saveManager.GetDestroyedGameObjectIDs());
                                cachedTrackedGameObjects = new Dictionary<string, TrackedGameObject>(saveManager.GetTrackedGameObjects());
                                if (saveManager.CurrentSaveData != null && saveManager.CurrentSaveData.GameObjectStates != null)
                                        cachedGameObjectStates = new List<GameObjectState>(saveManager.CurrentSaveData.GameObjectStates);
                                else
                                        cachedGameObjectStates.Clear();
                                Logger.Log("SaveManager data refreshed.");
			}
			else
			{
				Debug.LogWarning("SaveManager is null during RefreshSaveManagerData.");
			}
		}

		/// <summary>
		/// Refreshes cached scene lookups (PersistentVisibilityControllers, PrefabManager for viewer).
		/// </summary>
		private void RefreshSceneLookups()
		{
#pragma warning disable CS0618
			cachedPersistentVisibilityControllers = FindObjectsByType<PersistentVisibilityController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
				?? System.Array.Empty<PersistentVisibilityController>();
#pragma warning restore CS0618

			cachedPrefabManagerForViewer = FindFirstObjByType<PrefabManager>();
			cachedSerializedObjects.Clear();
		}

		private void OnGUI()
		{
			// Initialize GUI styles if not already done
			if (headerStyle == null || labelStyle == null)
			{
				InitializeGUIStyles();
			}

			// Wrap the entire GUI rendering in a try-catch to handle unexpected exceptions
			try
			{
				// Begin a vertical layout for better organization
				GUILayout.BeginVertical();

				// Add a horizontal toolbar with the Refresh button
				GUILayout.BeginHorizontal(EditorStyles.toolbar);
				GUILayout.FlexibleSpace(); // Push the button to the right
				if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
				{
					Logger.Log("Manual Refresh button clicked.");
					RefreshAllData();
					Repaint();
				}
				GUILayout.EndHorizontal();

				GUILayout.Space(10); // Add some spacing after the toolbar

				// Tabs for organizing different sections
				selectedTab = GUILayout.Toolbar(selectedTab, tabTitles);

				GUILayout.Space(10);

				// Tab Content
				switch (selectedTab)
				{
					case 0: // Overview
						DrawOverviewSection();
						break;
					case 1: // Persistent Visibility
						DrawPersistentVisibilityControllerSection();
						break;
					case 2: // Runtime Data Viewer
						DrawRuntimeDataViewerSection();
						break;
				}

				GUILayout.EndVertical();
			}
			catch (System.Exception e)
			{
				// Log the exception to help debug issues
				Debug.LogError($"An error occurred in RememberRuntimeDebugWindow: {e.Message}");
				EditorGUILayout.HelpBox($"An error occurred: {e.Message}", MessageType.Error);
			}
		}

		/// <summary>
		/// Draws the Overview section combining PrefabManager and SaveManager data.
		/// </summary>
		private void DrawOverviewSection()
		{
			if (!Application.isPlaying)
			{
				EditorGUILayout.HelpBox("Runtime data is only available in Play mode.", MessageType.Info);
				return;
			}

			GUILayout.Label("Prefab Manager Data", headerStyle);

			if (prefabManager != null)
			{
				// Search bar for Saveable Prefabs
				prefabSearchQuery = EditorGUILayout.TextField("Search Saveable Prefabs:", prefabSearchQuery);

				// Filtered Saveable Prefabs
				var filteredPrefabs = string.IsNullOrEmpty(prefabSearchQuery) ?
					cachedSaveablePrefabs :
					cachedSaveablePrefabs.FindAll(p => p != null &&
														(p.gameObject.name.ToLower().Contains(prefabSearchQuery.ToLower()) ||
														 p.UniqueID.ToLower().Contains(prefabSearchQuery.ToLower())));

				// Foldout for Saveable Prefabs
				showSaveablePrefabs = EditorGUILayout.Foldout(showSaveablePrefabs, "Registered Saveable Prefabs:");
				if (showSaveablePrefabs)
				{
					prefabScrollPos = EditorGUILayout.BeginScrollView(prefabScrollPos, GUILayout.Height(150));
                                        foreach (var prefab in filteredPrefabs)
                                        {
                                                if (prefab != null && prefab.gameObject != null)
                                                {
                                                        EditorGUILayout.BeginHorizontal();
                                                        EditorGUILayout.LabelField($"Name: {prefab.gameObject.name}", GUILayout.Width(200));
                                                        EditorGUILayout.LabelField($"ID: {prefab.UniqueID}", GUILayout.Width(250));
                                                        if (GUILayout.Button("Copy", GUILayout.Width(50)))
                                                        {
                                                                EditorGUIUtility.systemCopyBuffer = prefab.UniqueID;
                                                                EditorWindow.focusedWindow?.ShowNotification(new GUIContent($"Copied ID: {prefab.UniqueID}"));
                                                        }
                                                        EditorGUILayout.EndHorizontal();
                                                }
						else
						{
							EditorGUILayout.BeginHorizontal();
							EditorGUILayout.LabelField("Destroyed or Missing Prefab", labelStyle);
							EditorGUILayout.EndHorizontal();
						}
					}
					EditorGUILayout.EndScrollView();
				}

				// Foldout for Instantiated Prefabs
				showInstantiatedPrefabs = EditorGUILayout.Foldout(showInstantiatedPrefabs, "Instantiated Prefabs:");
				if (showInstantiatedPrefabs)
				{
					instantiatedPrefabsScrollPos = EditorGUILayout.BeginScrollView(instantiatedPrefabsScrollPos, GUILayout.Height(150));
					foreach (var kvp in cachedInstantiatedPrefabs)
					{
                                                if (kvp.Value != null)
                                                {
                                                        EditorGUILayout.BeginHorizontal();
                                                        EditorGUILayout.LabelField($"ID: {kvp.Key}", GUILayout.Width(150));
                                                        EditorGUILayout.LabelField($"GameObject: {kvp.Value.name}", GUILayout.Width(200));
                                                        if (GUILayout.Button("Ping", GUILayout.Width(50)))
                                                        {
                                                                EditorGUIUtility.PingObject(kvp.Value);
                                                        }
                                                        if (GUILayout.Button("Copy", GUILayout.Width(50)))
                                                        {
                                                                EditorGUIUtility.systemCopyBuffer = kvp.Key;
                                                                EditorWindow.focusedWindow?.ShowNotification(new GUIContent($"Copied ID: {kvp.Key}"));
                                                        }
                                                        EditorGUILayout.EndHorizontal();
                                                }
                                                else
                                                {
                                                        EditorGUILayout.BeginHorizontal();
                                                        EditorGUILayout.LabelField($"ID: {kvp.Key}");
                                                        EditorGUILayout.LabelField("Destroyed GameObject", labelStyle);
                                                        if (GUILayout.Button("Copy", GUILayout.Width(50)))
                                                        {
                                                                EditorGUIUtility.systemCopyBuffer = kvp.Key;
                                                                EditorWindow.focusedWindow?.ShowNotification(new GUIContent($"Copied ID: {kvp.Key}"));
                                                        }
                                                        EditorGUILayout.EndHorizontal();
                                                }
					}
					EditorGUILayout.EndScrollView();
				}

				GUILayout.Label($"All Prefabs Initialized: {prefabManager.AreAllPrefabsInitialized}", EditorStyles.label);
			}
			else
			{
				EditorGUILayout.HelpBox("PrefabManager not found in the scene.", MessageType.Warning);
			}

			GUILayout.Space(20);
			GUILayout.Label("Save Manager Data", headerStyle);

			if (saveManager != null)
			{
				// Search bar for Tracked GameObjects
				trackedObjectSearchQuery = EditorGUILayout.TextField("Search Tracked GameObjects:", trackedObjectSearchQuery);

				// Filtered Tracked GameObjects
				var filteredTrackedObjects = string.IsNullOrEmpty(trackedObjectSearchQuery) ?
					new Dictionary<string, TrackedGameObject>(cachedTrackedGameObjects) :
					cachedTrackedGameObjects
						.Where(kvp => kvp.Value != null && kvp.Value.GameObject != null &&
									  (kvp.Value.GameObject.name.ToLower().Contains(trackedObjectSearchQuery.ToLower()) ||
									   kvp.Key.ToLower().Contains(trackedObjectSearchQuery.ToLower())))
						.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

				// Foldout for Build Scene Names
				showBuildSceneNames = EditorGUILayout.Foldout(showBuildSceneNames, "Build Scene Names:");
				if (showBuildSceneNames)
				{
					saveManagerScrollPos = EditorGUILayout.BeginScrollView(saveManagerScrollPos, GUILayout.Height(100));
					foreach (var sceneName in cachedBuildSceneNames)
					{
						EditorGUILayout.LabelField(sceneName);
					}
					EditorGUILayout.EndScrollView();
				}

				// Foldout for Destroyed GameObject IDs
				showDestroyedGameObjectIDs = EditorGUILayout.Foldout(showDestroyedGameObjectIDs, "Destroyed GameObject IDs:");
				if (showDestroyedGameObjectIDs)
				{
					saveManagerScrollPos = EditorGUILayout.BeginScrollView(saveManagerScrollPos, GUILayout.Height(100));
                                        foreach (var id in cachedDestroyedGameObjectIDs)
                                        {
                                                EditorGUILayout.BeginHorizontal();
                                                EditorGUILayout.LabelField(id);
                                                if (GUILayout.Button("Copy", GUILayout.Width(50)))
                                                {
                                                        EditorGUIUtility.systemCopyBuffer = id;
                                                        EditorWindow.focusedWindow?.ShowNotification(new GUIContent($"Copied ID: {id}"));
                                                }
                                                EditorGUILayout.EndHorizontal();
                                        }
					EditorGUILayout.EndScrollView();
				}

				// Foldout for Tracked GameObjects
				showTrackedGameObjects = EditorGUILayout.Foldout(showTrackedGameObjects, "Tracked GameObjects:");
				if (showTrackedGameObjects)
				{
					trackedObjectsScrollPos = EditorGUILayout.BeginScrollView(trackedObjectsScrollPos, GUILayout.Height(150));
					foreach (var kvp in filteredTrackedObjects)
					{
                                                if (kvp.Value != null && kvp.Value.GameObject != null)
                                                {
                                                        EditorGUILayout.BeginVertical("box");
                                                        EditorGUILayout.BeginHorizontal();
                                                        EditorGUILayout.LabelField($"ID: {kvp.Key}");
                                                        EditorGUILayout.LabelField($"Name: {kvp.Value.GameObject.name}", GUILayout.Width(200));
                                                        if (GUILayout.Button("Ping", GUILayout.Width(50)))
                                                        {
                                                                EditorGUIUtility.PingObject(kvp.Value.GameObject);
                                                        }
                                                        if (GUILayout.Button("Copy", GUILayout.Width(50)))
                                                        {
                                                                EditorGUIUtility.systemCopyBuffer = kvp.Key;
                                                                EditorWindow.focusedWindow?.ShowNotification(new GUIContent($"Copied ID: {kvp.Key}"));
                                                        }
                                                        EditorGUILayout.EndHorizontal();

							// Display additional details
							GUILayout.Label($"Active: {kvp.Value.GameObject.activeSelf}", GUILayout.Width(150));
							GUILayout.Label($"Destroyed: {cachedDestroyedGameObjectIDs.Contains(kvp.Key)}", GUILayout.Width(150));
							GUILayout.EndVertical();
						}
                                                else
                                                {
                                                        EditorGUILayout.BeginHorizontal();
                                                        EditorGUILayout.LabelField($"ID: {kvp.Key}");
                                                        EditorGUILayout.LabelField("Destroyed GameObject", labelStyle);
                                                        if (GUILayout.Button("Copy", GUILayout.Width(50)))
                                                        {
                                                                EditorGUIUtility.systemCopyBuffer = kvp.Key;
                                                                EditorWindow.focusedWindow?.ShowNotification(new GUIContent($"Copied ID: {kvp.Key}"));
                                                        }
                                                        EditorGUILayout.EndHorizontal();
                                                }
					}
					EditorGUILayout.EndScrollView();
				}
			}
			else
			{
				EditorGUILayout.HelpBox("SaveManager not found in the scene.", MessageType.Warning);
			}
		}

		/// <summary>
		/// Draws the Persistent Visibility Controller section.
		/// </summary>
		private void DrawPersistentVisibilityControllerSection()
		{
			if (!Application.isPlaying)
			{
				EditorGUILayout.HelpBox("Persistent Visibility Controllers are only available in Play mode.", MessageType.Info);
				return;
			}

			// Implementing the foldout using showPersistentVisibilityController
			showPersistentVisibilityController = EditorGUILayout.Foldout(showPersistentVisibilityController, "Persistent Visibility Controllers:");
			if (showPersistentVisibilityController)
			{
				GUILayout.Space(5);
				// Use cached array instead of scanning every repaint
				PersistentVisibilityController[] controllers = cachedPersistentVisibilityControllers;

				if (controllers.Length == 0)
				{
					EditorGUILayout.LabelField("No PersistentVisibilityController instances found.");
					return;
				}

				persistentVisibilityScrollPos = EditorGUILayout.BeginScrollView(persistentVisibilityScrollPos, GUILayout.Height(200));

				foreach (var controller in controllers)
				{
					EditorGUILayout.BeginVertical("box");
					EditorGUILayout.LabelField("GameObject:", controller.gameObject.name);

					// Accessing Dictionaries via Reflection
					// Example: originalCanvasStates, originalColliderStates, originalRendererStates

					DrawDictionary(controller, "originalCanvasStates", typeof(Canvas), typeof(bool));
					DrawDictionary(controller, "originalColliderStates", typeof(Collider), typeof(bool));
					DrawDictionary(controller, "originalRendererStates", typeof(Renderer), typeof(bool));

					// Display other fields as needed
					EditorGUILayout.EndVertical();
				}

				EditorGUILayout.EndScrollView();
			}
		}

		/// <summary>
		/// Draws the Runtime Data Viewer section, providing detailed runtime data.
		/// </summary>
		private void DrawRuntimeDataViewerSection()
		{
			if (!Application.isPlaying)
			{
				EditorGUILayout.HelpBox("Runtime Data Viewer is only available in Play mode.", MessageType.Info);
				return;
			}

			GUILayout.Label("Runtime Data Viewer", headerStyle);

			DrawPrefabManagerDataViewer();
			DrawSaveManagerDataViewer();
		}

		#region Overview Section Methods

		// Already handled above

		#endregion

		#region Persistent Visibility Controller Section Methods

		// Already handled above

		#endregion

		#region Runtime Data Viewer Section Methods

		private void DrawPrefabManagerDataViewer()
		{
			GUILayout.Label("PrefabManager", EditorStyles.boldLabel);

			// Use cached reference instead of FindFirstObjByType every repaint
			PrefabManager manager = cachedPrefabManagerForViewer;

			if (manager == null)
			{
				EditorGUILayout.LabelField("No PrefabManager instance found.");
				return;
			}

			EditorGUILayout.BeginVertical("box");
			EditorGUILayout.LabelField("GameObject:", manager.gameObject.name);

			// Display saveablePrefabs list (UnityEngine.Object-derived)
			DrawUnityObjectList(manager, "saveablePrefabs", "Saveable Prefabs");

			// Access instantiatedPrefabs dictionary via Reflection
			DrawDictionary(manager, "instantiatedPrefabs", typeof(string), typeof(GameObject));

			EditorGUILayout.EndVertical();
		}

		private void DrawSaveManagerDataViewer()
		{
			GUILayout.Label("SaveManager", EditorStyles.boldLabel);

			// Access SaveManager via Singleton Instance
			SaveManager manager = SaveManager.Instance;

			if (manager == null)
			{
				EditorGUILayout.LabelField("SaveManager instance not found.");
				return;
			}

			EditorGUILayout.BeginVertical("box");
			EditorGUILayout.LabelField("GameObject:", manager.gameObject.name);

			// Display saveSlots list (Pure C# objects)
			DrawGenericList(manager, "saveSlots", "Save Slots");

                        // Access trackedGameObjects dictionary via Reflection
                        DrawDictionary(manager, "trackedGameObjects", typeof(string), typeof(TrackedGameObject));

                        // Display prefab data stored in CurrentSaveData
                        SaveData data = manager.CurrentSaveData;
                        if (data != null)
                        {
                                EditorGUILayout.Space();
                                int count = data.Prefabs != null ? data.Prefabs.Count : 0;
                                EditorGUILayout.LabelField($"CurrentSaveData Prefabs ({count}):");
                                EditorGUI.indentLevel++;
                                if (count > 0)
                                {
                                        foreach (var pd in data.Prefabs)
                                        {
                                                if (pd != null)
                                                        EditorGUILayout.LabelField($"InstanceID: {pd.InstanceID}, PrefabID: {pd.PrefabID}");
                                        }
                                }
                                else
                                {
                                        EditorGUILayout.LabelField("No prefab entries.");
                                }
                                EditorGUI.indentLevel--;
                        }
                        else
                        {
                                EditorGUILayout.LabelField("CurrentSaveData is null.");
                        }

                        // Display GameObject states stored in CurrentSaveData
                        EditorGUILayout.Space();
                        int stateCount = cachedGameObjectStates != null ? cachedGameObjectStates.Count : 0;
                        showGameObjectStates = EditorGUILayout.Foldout(showGameObjectStates, $"GameObject States ({stateCount}):");
                        if (showGameObjectStates)
                        {
                                gameObjectStatesScrollPos = EditorGUILayout.BeginScrollView(gameObjectStatesScrollPos, GUILayout.Height(150));
                                foreach (var state in cachedGameObjectStates)
                                {
                                        if (state == null)
                                                continue;

                                        GameObject go = SaveManager.Instance != null ? SaveManager.Instance.FindGameObjectByUniqueID(state.UniqueID, SaveManager.IdentifierType.UniqueID) : null;
                                        string goName = go != null ? go.name : "Missing";
                                        string idLabel = state.UniqueID;
                                        if (go != null)
                                        {
                                                SaveablePrefab sp = go.GetComponent<SaveablePrefab>();
                                                if (sp != null && !string.IsNullOrEmpty(sp.PrefabAssetID))
                                                        idLabel = sp.PrefabAssetID;
                                        }

                                        EditorGUILayout.BeginHorizontal();
                                        EditorGUILayout.LabelField($"Name: {goName}", GUILayout.Width(180));
                                        EditorGUILayout.LabelField($"ID: {idLabel}", GUILayout.Width(220));
                                        EditorGUILayout.LabelField($"Active: {(state.IsActive.HasValue ? state.IsActive.Value.ToString() : "null")}", GUILayout.Width(80));
                                        if (go != null && GUILayout.Button("Ping", GUILayout.Width(40)))
                                        {
                                                EditorGUIUtility.PingObject(go);
                                        }
                                        EditorGUILayout.EndHorizontal();
                                }
                                EditorGUILayout.EndScrollView();
                        }

                        // Display other relevant fields as needed

                        EditorGUILayout.EndVertical();
                }

		#endregion

		#region Helper Methods

		/// <summary>
		/// Draws a dictionary field from a given object using reflection.
		/// </summary>
		/// <param name="obj">The object containing the dictionary.</param>
		/// <param name="fieldName">The name of the dictionary field.</param>
		/// <param name="keyType">Type of the dictionary keys.</param>
		/// <param name="valueType">Type of the dictionary values.</param>
		private void DrawDictionary(UnityEngine.Object obj, string fieldName, System.Type keyType, System.Type valueType)
		{
			System.Type type = obj.GetType();
			FieldInfo field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

			if (field == null)
			{
				EditorGUILayout.LabelField($"Field '{fieldName}' not found.");
				return;
			}

			var dict = field.GetValue(obj) as IDictionary;

			if (dict == null)
			{
				EditorGUILayout.LabelField($"Field '{fieldName}' is not a dictionary.");
				return;
			}

			EditorGUILayout.LabelField($"{fieldName} ({dict.Count} items):");
			EditorGUI.indentLevel++;

			foreach (var key in dict.Keys)
			{
				var value = dict[key];
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField($"Key: {key}", GUILayout.MaxWidth(200));
				EditorGUILayout.LabelField($"Value: {ValueToString(value)}");
				EditorGUILayout.EndHorizontal();
			}

			EditorGUI.indentLevel--;
		}

		/// <summary>
		/// Draws a list field containing UnityEngine.Object-derived objects using SerializedObject and SerializedProperty.
		/// </summary>
		/// <param name="obj">The object containing the list. Must inherit from UnityEngine.Object.</param>
		/// <param name="fieldName">The name of the list field.</param>
		/// <param name="displayName">Display name for the list.</param>
		private void DrawUnityObjectList(UnityEngine.Object obj, string fieldName, string displayName)
		{
			// Cache SerializedObject per instance to avoid allocation every repaint
			int key = obj.GetInstanceID();
			if (!cachedSerializedObjects.TryGetValue(key, out SerializedObject so) || so == null)
			{
				so = new SerializedObject(obj);
				cachedSerializedObjects[key] = so;
			}
			SerializedProperty listProp = so.FindProperty(fieldName);

			EditorGUILayout.LabelField(displayName + ":", EditorStyles.label);

			if (listProp != null && listProp.isArray)
			{
				so.Update(); // Ensure SerializedObject is up to date
				EditorGUI.indentLevel++;
				for (int i = 0; i < listProp.arraySize; i++)
				{
					SerializedProperty element = listProp.GetArrayElementAtIndex(i);
					string elementName = element.objectReferenceValue != null ? element.objectReferenceValue.name : "Null";
					EditorGUILayout.LabelField($"Element {i + 1}: {elementName}");
				}
				EditorGUI.indentLevel--;
			}
			else
			{
				EditorGUILayout.LabelField($"List '{fieldName}' not found or is not an array.");
			}
		}

		/// <summary>
		/// Draws a list field containing pure C# objects using reflection.
		/// </summary>
		/// <param name="obj">The object containing the list.</param>
		/// <param name="fieldName">The name of the list field.</param>
		/// <param name="displayName">Display name for the list.</param>
		private void DrawGenericList(UnityEngine.Object obj, string fieldName, string displayName)
		{
			System.Type type = obj.GetType();
			FieldInfo field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

			if (field == null)
			{
				EditorGUILayout.LabelField($"Field '{fieldName}' not found.");
				return;
			}

			var list = field.GetValue(obj) as IList;

			if (list == null)
			{
				EditorGUILayout.LabelField($"Field '{fieldName}' is not a list.");
				return;
			}

			EditorGUILayout.LabelField($"{displayName} ({list.Count} items):");
			EditorGUI.indentLevel++;

			for (int i = 0; i < list.Count; i++)
			{
				var element = list[i];
				EditorGUILayout.LabelField($"Element {i + 1}: {ValueToString(element)}");
			}

			EditorGUI.indentLevel--;
		}

		/// <summary>
		/// Converts a value to a string representation, handling nulls.
		/// </summary>
		/// <param name="value">The value to convert.</param>
		/// <returns>String representation of the value.</returns>
		private string ValueToString(object value)
		{
			if (value == null)
				return "Null";

			return value.ToString();
		}

		/// <summary>
		/// Initializes custom GUI styles for better aesthetics.
		/// </summary>
		private void InitializeGUIStyles()
		{
			if (headerStyle == null)
			{
				// Create a custom header style
				headerStyle = new GUIStyle()
				{
					fontSize = 14,
					fontStyle = FontStyle.Bold,
					normal = { textColor = Color.white },
					alignment = TextAnchor.MiddleCenter
				};
				Debug.Log("HeaderStyle initialized.");
			}

			if (labelStyle == null)
			{
				// Create a custom label style
				labelStyle = new GUIStyle()
				{
					fontSize = 12,
					normal = { textColor = Color.red }
				};
				Debug.Log("LabelStyle initialized.");
			}
		}

		/// <summary>
		/// Finds the first active or inactive object of the specified type.
		/// </summary>
		/// <typeparam name="T">Type of the object to find.</typeparam>
		/// <returns>First instance of type T found, or null.</returns>
		private T FindFirstObjByType<T>() where T : UnityEngine.Object
		{
#pragma warning disable CS0618 // Suppress FindObjectsByType deprecation warning for cross-version compatibility
			T[] objects = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#pragma warning restore CS0618
			return objects.Length > 0 ? objects[0] : null;
		}

		#endregion
	}
}
#endif