#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR && REMEMBERME_EDITOR_COROUTINES_PRESENT
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System;
using System.Security.Cryptography;
using System.Diagnostics;
using Logger = Arawn.CrystalSave.Runtime.Logger;
using Unity.EditorCoroutines.Editor;
using System.Collections;
using Arawn.CrystalSave.Runtime;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Linq;
#if CRYSTALSAVE_TIMEMACHINE
using Arawn.CrystalSave.Runtime.TimeMachine;
#endif

namespace Arawn.CrystalSave.Editor
{
	public class RememberMeSettingsWindow : EditorWindow
	{
		private SaveSettings saveSettings;
		private PrefabRegistry prefabRegistry;
		private TagRegistry tagRegistry;
                private SceneObjectRegistry sceneObjectRegistry;
		private MigrationManager migrationManager;
                private StaticMasterSecret staticMasterSecret;
                private SaveSlotMetadataSO saveSlotMetadata;
		private CrystalSaveAssetOverrides assetOverrides;
		private LoggerConfig loggerConfig;
		private bool anyAssetCreated = false; // Track whether we created required assets during this session

		// Cached SerializedObjects – created once in OnEnable, reused every OnGUI repaint
		private SerializedObject serializedSaveSettings;
		private SerializedObject serializedPrefabRegistry;
		private SerializedObject serializedTagRegistry;
		private SerializedObject serializedSceneObjectRegistry;
		private SerializedObject serializedSaveSlotMetadata;
		private SerializedObject serializedLoggerConfig;
		private SerializedObject serializedAssetOverrides;

		private static bool CloudSdkPresent =>
			Type.GetType("Unity.Services.CloudSave.CloudSaveService, Unity.Services.CloudSave") != null;
		private static bool AuthSdkPresent =>
			Type.GetType("Unity.Services.Authentication.AuthenticationService, Unity.Services.Authentication") != null;
		
		private bool showSaveSettings = true;
		private bool showAssetOverrides = false;
		private bool showPrefabRegistry = true;
		private bool showTagRegistry = true;
		private bool showSceneObjectRegistry = true;

		private string saveSettingsPath = "Assets/Plugins/CrystalSave/Resources/SaveSettings.asset";
		private string prefabRegistryPath = "Assets/Plugins/CrystalSave/Resources/PrefabRegistry.asset";
		private string tagRegistryPath = "Assets/Plugins/CrystalSave/Resources/TagRegistry.asset";
		private string sceneObjectRegistryPath = "Assets/Plugins/CrystalSave/Resources/SceneObjectRegistry.asset";
		private string migrationManagerPath = "Assets/Plugins/CrystalSave/Resources/MigrationManager.asset";
		private string saveSlotMetadataPath = "Assets/Plugins/CrystalSave/Resources/SaveSlotMetadata.asset";
		private string assetOverridesPath = "Assets/Plugins/CrystalSave/Settings/CrystalSaveAssetOverrides.asset";
		private string loggerConfigPath = "Assets/Plugins/CrystalSave/Resources/LoggerConfig.asset";

		private double lastTagRegistrationTime = 0;
		//private double lastSceneObjectRegistrationTime = 0;
		private double registrationCooldown = 1.0;

                private Vector2 scrollPos;
                private EditorCoroutine sceneObjectRegistrationCoroutine;

                private Texture2D logoTexture;
                private string logoPath = "Assets/Plugins/CrystalSave/Editor/Logo/RememberMeLogo.PNG";

                // ────────────── button tool-tips (cached GUIContent) ──────────────
		static readonly GUIContent GC_ValidateIDs = new GUIContent(
			"Validate UniqueIDs",
			"Checks Prefab Registry entries for missing or duplicate UniqueID strings " +
			"and offers to auto-fix problems.");

		static readonly GUIContent GC_CleanDupes = new GUIContent(
			"Clean Duplicates",
			"Removes duplicate prefab rows (same asset GUID) from the Prefab Registry, " +
			"keeping the first occurrence.");
		
		static readonly GUIContent GC_DeregisterPrefabs = new GUIContent(
			"Deregister & Purge Prefabs",
			"Removes all Crystal Save components from all registered prefabs and wipes the registry.");

		static readonly GUIContent GC_AutoRegTags = new GUIContent(
			"Auto-Register Tags",
			"Copies the current Project Settings ▶ Tags list into the Tag Registry.");

			private static void ExportOneTimeServerKey()
			{
			if (!EditorUtility.DisplayDialog(
					"Export One-Time Server Key",
					"This will generate a new 32-byte key, copy it to your clipboard, and not store it in the project.\n\n" +
					"Store this key securely on your server. It must never ship in the build.\n\n" +
					"Continue?",
					"Copy Key",
					"Cancel"))
			{
				return;
			}

			byte[] secret = new byte[32];
			using (var rng = RandomNumberGenerator.Create())
				rng.GetBytes(secret);

			string base64 = Convert.ToBase64String(secret);
			GUIUtility.systemCopyBuffer = base64;

			EditorUtility.DisplayDialog(
				"Key Copied",
				"The server key has been copied to your clipboard.\n" +
					"Store it securely on your server before continuing.",
					"OK");
			}

#if NANINOVEL && REMEMBERME_NANINOVEL_PRESENT
			private static void ConfigureForNaninovel(SerializedObject ss)
			{
				var stateCfg = Naninovel.ProjectConfigurationProvider.LoadOrDefault<Naninovel.StateConfiguration>();
				if (stateCfg == null)
				{
					EditorUtility.DisplayDialog(
						"Naninovel Configuration",
						"Could not load Naninovel StateConfiguration.\n" +
						"Make sure Naninovel is properly installed.",
						"OK");
					return;
				}

				int saveLimit = Math.Max(1, stateCfg.SaveSlotLimit);
				int quickLimit = Math.Max(0, stateCfg.QuickSaveSlotLimit);
				int autoLimit = Math.Max(0, stateCfg.AutoSaveSlotLimit);

				// Non-overlapping ranges:
				// regular saves:  1 .. saveLimit
				// quick saves:    saveLimit+1 .. saveLimit+quickLimit
				// auto saves:     saveLimit+quickLimit+1 .. saveLimit+quickLimit+autoLimit
				int quickOffset = saveLimit;
				int autoOffset = saveLimit + quickLimit;
				int firstAutoSlot = autoOffset + 1;

				// Number Of Save Slots must include the highest slot touched by regular/quick/auto saves.
				int quickRangeEnd = quickOffset + quickLimit;
				int autoRangeEnd = autoLimit > 0 ? autoOffset + autoLimit : firstAutoSlot;
				int totalSlotCount = Math.Max(saveLimit, Math.Max(quickRangeEnd, autoRangeEnd));

				ss.FindProperty("numberOfSaveSlots").intValue = totalSlotCount;
				ss.FindProperty("numberOfQuickSaveSlots").intValue = quickLimit;
				ss.FindProperty("quickSaveSlotOffset").intValue = quickOffset;
				// Keep legacy single-slot autosave enabled as a non-overlapping fallback.
				ss.FindProperty("autoSaveSlotNumber").intValue = firstAutoSlot;
				ss.FindProperty("numberOfAutoSaveSlots").intValue = autoLimit;
				ss.FindProperty("autoSaveSlotOffset").intValue = autoOffset;

				ss.ApplyModifiedProperties();

				EditorUtility.DisplayDialog(
					"Naninovel Configuration Applied",
					$"Crystal Save slot settings updated to match Naninovel:\n\n" +
					$"  Naninovel Save Slots: {saveLimit}\n" +
					$"  Crystal Save Slots:   {totalSlotCount}\n" +
					$"  Quick Save Slots:    {quickLimit}  (offset {quickOffset})\n" +
					$"  Auto Save Slots:     {autoLimit}  (offset {autoOffset})\n" +
					$"  Auto Save Slot #:    {firstAutoSlot}  (non-overlapping fallback)",
					"OK");
				}
#endif

			static readonly GUIContent GC_AutoPopulateScene = new GUIContent(
				"Auto-Populate Scene Objects",
				"Scans scenes based on Scene Scan Mode, finds objects with a UniqueID component, and " +
				"maps them to prefab assets that use the same UniqueID.");

		[MenuItem("Tools/Crystal Save/Settings/Crystal Save Settings")]
		public static void ShowWindow()
		{
			// Ensure core assets exist first, then check onboarding flag.
			EnsureCoreAssetsPresent();
			var save = FindExistingSaveSettings();
			bool needsOnboarding = (save != null && !save.onboardingCompleted);
			if (needsOnboarding)
			{
				var wizType = Type.GetType("Arawn.CrystalSave.Editor.SettingsWizard");
				if (wizType == null)
				{
					foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
					{
						wizType = asm.GetType("Arawn.CrystalSave.Editor.SettingsWizard");
						if (wizType != null) break;
					}
				}
				var open = wizType?.GetMethod("OpenWizard", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
				open?.Invoke(null, null);
				return;
			}

			GetWindow<RememberMeSettingsWindow>("Crystal Save Settings");
		}

		private static SaveSettings FindExistingSaveSettings()
		{
			try
			{
				string[] guids = AssetDatabase.FindAssets("t:SaveSettings");
				foreach (string guid in guids)
				{
					string path = AssetDatabase.GUIDToAssetPath(guid);
					var asset = AssetDatabase.LoadAssetAtPath<SaveSettings>(path);
					if (asset != null) return asset;
				}
			}
			catch {}
			return null;
		}

		// Ensures SaveSettings and related assets exist anywhere in the project. Returns true if any were created.
		private static bool EnsureCoreAssetsPresent()
		{
			bool created = false;

			T FindAnywhere<T>(string preferredName = null) where T : ScriptableObject
			{
				try
				{
					string filter = $"t:{typeof(T).Name}";
					string[] guids = AssetDatabase.FindAssets(filter);
					foreach (string guid in guids)
					{
						string path = AssetDatabase.GUIDToAssetPath(guid);
						var asset = AssetDatabase.LoadAssetAtPath<T>(path);
						if (asset == null) continue;
						if (!string.IsNullOrEmpty(preferredName))
						{
							string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
							if (!string.Equals(asset.name, preferredName, StringComparison.OrdinalIgnoreCase) &&
								!string.Equals(fileName, preferredName, StringComparison.OrdinalIgnoreCase))
							{
								continue;
							}
						}
						return asset;
					}
					return null;
				}
				catch { return null; }
			}

			string resDir = "Assets/Plugins/CrystalSave/Resources";
			if (!AssetDatabase.IsValidFolder(resDir))
			{
				string[] parts = resDir.Split('/');
				string curr = parts[0];
				for (int i = 1; i < parts.Length; i++)
				{
					string next = curr + "/" + parts[i];
					if (!AssetDatabase.IsValidFolder(next))
						AssetDatabase.CreateFolder(curr, parts[i]);
					curr = next;
				}
			}

			// SaveSettings
			var save = FindAnywhere<SaveSettings>("SaveSettings");
			if (save == null)
			{
				save = ScriptableObject.CreateInstance<SaveSettings>();
				save.name = "SaveSettings";
				AssetDatabase.CreateAsset(save, resDir + "/SaveSettings.asset");
				created = true;
			}

			// PrefabRegistry
			var preg = FindAnywhere<PrefabRegistry>("PrefabRegistry");
			if (preg == null)
			{
				preg = ScriptableObject.CreateInstance<PrefabRegistry>();
				preg.name = "PrefabRegistry";
				AssetDatabase.CreateAsset(preg, resDir + "/PrefabRegistry.asset");
				created = true;
			}

			// TagRegistry
			var treg = FindAnywhere<TagRegistry>("TagRegistry");
			if (treg == null)
			{
				treg = ScriptableObject.CreateInstance<TagRegistry>();
				treg.name = "TagRegistry";
				AssetDatabase.CreateAsset(treg, resDir + "/TagRegistry.asset");
				created = true;
			}

			// SceneObjectRegistry
			var sreg = FindAnywhere<SceneObjectRegistry>("SceneObjectRegistry");
			if (sreg == null)
			{
				sreg = ScriptableObject.CreateInstance<SceneObjectRegistry>();
				sreg.name = "SceneObjectRegistry";
				AssetDatabase.CreateAsset(sreg, resDir + "/SceneObjectRegistry.asset");
				created = true;
			}

			// MigrationManager
			var mm = FindAnywhere<MigrationManager>("MigrationManager");
			if (mm == null)
			{
				mm = ScriptableObject.CreateInstance<MigrationManager>();
				mm.name = "MigrationManager";
				AssetDatabase.CreateAsset(mm, resDir + "/MigrationManager.asset");
				created = true;
			}

			// SaveSlotMetadata
			var meta = FindAnywhere<SaveSlotMetadataSO>("SaveSlotMetadata");
			if (meta == null)
			{
				meta = ScriptableObject.CreateInstance<SaveSlotMetadataSO>();
				meta.name = "SaveSlotMetadata";
				AssetDatabase.CreateAsset(meta, resDir + "/SaveSlotMetadata.asset");
				created = true;
			}

			if (created)
			{
				// Ensure defaultSlotMetadata is set
				if (save != null && save.defaultSlotMetadata == null)
				{
					save.defaultSlotMetadata = meta;
					EditorUtility.SetDirty(save);
				}
				AssetDatabase.SaveAssets();
			}

			return created;
		}

		private void OnEnable()
		{
			LoadOrCreateAssets();
			LoadLogoTexture();
			CacheSerializedObjects();

			// Defer the heavy scene scan so it doesn't block OnEnable
			EditorApplication.delayCall += CheckAndPromptUniqueIDFix;

			AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
			EditorApplication.projectChanged += OnProjectChanged;
		}

		private void OnDisable()
		{
			AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
			EditorApplication.projectChanged -= OnProjectChanged;

			if (sceneObjectRegistrationCoroutine != null)
			{
				EditorCoroutineUtility.StopCoroutine(sceneObjectRegistrationCoroutine);
				sceneObjectRegistrationCoroutine = null;
                                Logger.Log("Scene object registration coroutine stopped.", LogLevel.Info);
			}
		}

		// Finds an existing asset of type T anywhere in the project. If preferredName is provided,
		// returns the asset whose name or file name matches; otherwise returns the first match.
		private T FindExistingAsset<T>(string preferredName = null) where T : ScriptableObject
		{
			try
			{
				string filter = $"t:{typeof(T).Name}";
				string[] guids = AssetDatabase.FindAssets(filter);
				T first = null;
				foreach (string guid in guids)
				{
					string path = AssetDatabase.GUIDToAssetPath(guid);
					var asset = AssetDatabase.LoadAssetAtPath<T>(path);
					if (asset == null) continue;
					if (first == null) first = asset;

					if (!string.IsNullOrEmpty(preferredName))
					{
						string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
						if (string.Equals(asset.name, preferredName, StringComparison.OrdinalIgnoreCase) ||
						    string.Equals(fileName, preferredName, StringComparison.OrdinalIgnoreCase))
						{
							return asset;
						}
					}
				}
				return first;
			}
			catch
			{
				return null;
			}
		}

		// Loads an asset if present anywhere; otherwise creates it at defaultPath.
		// actualPath returns the resolved asset path used.
		private T LoadOrCreateAssetAnywhere<T>(string defaultPath, string preferredName, out string actualPath) where T : ScriptableObject
		{
			var existing = FindExistingAsset<T>(preferredName);
			if (existing != null)
			{
				actualPath = AssetDatabase.GetAssetPath(existing);
				return existing;
			}

			Logger.Log($"{typeof(T).Name} not found. Creating a new one.", LogLevel.Info);
			var created = CreateInstance<T>();
			if (!string.IsNullOrEmpty(preferredName)) created.name = preferredName;
			EnsureDirectoryExists(Path.GetDirectoryName(defaultPath));
			AssetDatabase.CreateAsset(created, defaultPath);
			AssetDatabase.SaveAssets();
			actualPath = defaultPath;
			anyAssetCreated = true; // mark first-time creation
			EditorUtility.FocusProjectWindow();
			Selection.activeObject = created;
			return created;
		}

		private void LoadOrCreateAssets()
		{
			// Use Addressables-friendly discovery: find anywhere, create only if none found.
			if (anyAssetCreated) // If we just created the core assets, redirect to the Settings Wizard
			{
				EditorApplication.delayCall += () =>
				{
					var wizType = Type.GetType("Arawn.CrystalSave.Editor.SettingsWizard");
					if (wizType == null)
					{
						foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
						{
							wizType = asm.GetType("Arawn.CrystalSave.Editor.SettingsWizard");
							if (wizType != null) break;
						}
					}
					var open = wizType?.GetMethod("OpenWizard", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
					open?.Invoke(null, null);
				};
				Close();
				return;
			}
			saveSettings = LoadOrCreateAssetAnywhere<SaveSettings>(saveSettingsPath, "SaveSettings", out saveSettingsPath);

			prefabRegistry = LoadOrCreateAssetAnywhere<PrefabRegistry>(prefabRegistryPath, "PrefabRegistry", out prefabRegistryPath);

			tagRegistry = LoadOrCreateAssetAnywhere<TagRegistry>(tagRegistryPath, "TagRegistry", out tagRegistryPath);

			sceneObjectRegistry = LoadOrCreateAssetAnywhere<SceneObjectRegistry>(sceneObjectRegistryPath, "SceneObjectRegistry", out sceneObjectRegistryPath);

			migrationManager = LoadOrCreateAssetAnywhere<MigrationManager>(migrationManagerPath, "MigrationManager", out migrationManagerPath);

			saveSlotMetadata = LoadOrCreateAssetAnywhere<SaveSlotMetadataSO>(saveSlotMetadataPath, "SaveSlotMetadata", out saveSlotMetadataPath);

			// LoggerConfig is optional - only load if it exists, don't auto-create
			loggerConfig = FindExistingAsset<LoggerConfig>("LoggerConfig");

			// Asset overrides are optional - only load if they exist, don't auto-create
			assetOverrides = FindExistingAsset<CrystalSaveAssetOverrides>("CrystalSaveAssetOverrides");

			if (saveSettings.defaultSlotMetadata == null)
			{
					saveSettings.defaultSlotMetadata = saveSlotMetadata;
					EditorUtility.SetDirty(saveSettings);
					AssetDatabase.SaveAssets();
			}
		}
		
		private T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
		{
			var asset = AssetDatabase.LoadAssetAtPath<T>(path);
			if (asset != null) return asset;

                        Logger.Log($"{typeof(T).Name} not found. Creating a new one.", LogLevel.Info);
                        asset = CreateInstance<T>();
			EnsureDirectoryExists(Path.GetDirectoryName(path));
			AssetDatabase.CreateAsset(asset, path);
			AssetDatabase.SaveAssets();
			return asset;
		}

		private void LoadLogoTexture()
		{
			logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(logoPath);
                        if (logoTexture == null)
                        {
                                Logger.Log($"RememberMeSettingsWindow: Logo not found at path '{logoPath}'. Please ensure the logo exists.", LogLevel.Warning);
                        }
		}

		private void DrawAssetOverrides()
		{
			showAssetOverrides = EditorGUILayout.Foldout(showAssetOverrides, "Runtime Asset Overrides (Optional)", true);
			if (!showAssetOverrides) return;

			EditorGUI.indentLevel++;
			EditorGUILayout.HelpBox(
				"Optional: Directly reference core Crystal Save assets to bypass Resources/Addressables lookup. " +
				"Any field left empty falls back to AssetProvider. " +
				"To work in builds, the overrides asset must be added to Preloaded Assets.",
				MessageType.Info);

			if (assetOverrides == null)
			{
				EditorGUILayout.BeginHorizontal();
				if (GUILayout.Button("Create Overrides Asset"))
				{
					assetOverrides = CreateOverridesAsset();
					serializedAssetOverrides = new SerializedObject(assetOverrides);
				}
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.HelpBox(
					"Create the overrides asset to enable direct references.",
					MessageType.Info);
				EditorGUI.indentLevel--;
				return;
			}

			if (assetOverrides != null && serializedAssetOverrides != null)
			{
				var overridesSO = serializedAssetOverrides;
				overridesSO.Update();
				EditorGUILayout.PropertyField(overridesSO.FindProperty("saveSettings"), new GUIContent("Save Settings"));
				EditorGUILayout.PropertyField(overridesSO.FindProperty("prefabRegistry"), new GUIContent("Prefab Registry"));
				EditorGUILayout.PropertyField(overridesSO.FindProperty("tagRegistry"), new GUIContent("Tag Registry"));
				EditorGUILayout.PropertyField(overridesSO.FindProperty("sceneObjectRegistry"), new GUIContent("Scene Object Registry"));
				EditorGUILayout.PropertyField(overridesSO.FindProperty("migrationManager"), new GUIContent("Migration Manager"));
				EditorGUILayout.PropertyField(overridesSO.FindProperty("loggerConfig"), new GUIContent("Logger Config"));
				EditorGUILayout.PropertyField(overridesSO.FindProperty("saveSlotMetadata"), new GUIContent("Save Slot Metadata"));
				if (overridesSO.ApplyModifiedProperties())
				{
					EditorUtility.SetDirty(assetOverrides);
				}

				bool isPreloaded = IsPreloadedAsset(assetOverrides);
				EditorGUILayout.HelpBox(
					isPreloaded
						? "Overrides asset is listed in Player Settings → Preloaded Assets."
						: "Add this asset to Player Settings → Preloaded Assets so it loads at startup.",
					isPreloaded ? MessageType.Info : MessageType.Warning);

				EditorGUILayout.BeginHorizontal();
				if (!isPreloaded && GUILayout.Button("Add To Preloaded Assets"))
				{
					AddPreloadedAsset(assetOverrides);
				}
				if (isPreloaded && GUILayout.Button("Remove From Preloaded Assets"))
				{
					RemovePreloadedAsset(assetOverrides);
				}
				if (GUILayout.Button("Select In Project"))
				{
					Selection.activeObject = assetOverrides;
				}
				EditorGUILayout.EndHorizontal();
			}

			EditorGUI.indentLevel--;
		}

		private CrystalSaveAssetOverrides CreateOverridesAsset()
		{
			var created = ScriptableObject.CreateInstance<CrystalSaveAssetOverrides>();
			created.name = "CrystalSaveAssetOverrides";
			created.saveSettings = saveSettings;
			created.prefabRegistry = prefabRegistry;
			created.tagRegistry = tagRegistry;
			created.sceneObjectRegistry = sceneObjectRegistry;
			created.migrationManager = migrationManager;
			created.loggerConfig = loggerConfig;
			created.saveSlotMetadata = saveSlotMetadata;
			EnsureDirectoryExists(Path.GetDirectoryName(assetOverridesPath));
			AssetDatabase.CreateAsset(created, assetOverridesPath);
			EditorUtility.SetDirty(created);
			AssetDatabase.SaveAssets();
			AddPreloadedAsset(created);
			Selection.activeObject = created;
			return created;
		}

		private static bool IsPreloadedAsset(UnityEngine.Object asset)
		{
			var list = PlayerSettings.GetPreloadedAssets();
			if (list == null) return false;
			for (int i = 0; i < list.Length; i++)
			{
				if (list[i] == asset) return true;
			}
			return false;
		}

		private static void AddPreloadedAsset(UnityEngine.Object asset)
		{
			if (asset == null) return;
			var list = new List<UnityEngine.Object>(PlayerSettings.GetPreloadedAssets() ?? Array.Empty<UnityEngine.Object>());
			if (!list.Contains(asset))
			{
				list.Add(asset);
				PlayerSettings.SetPreloadedAssets(list.ToArray());
			}
		}

		private static void RemovePreloadedAsset(UnityEngine.Object asset)
		{
			if (asset == null) return;
			var list = new List<UnityEngine.Object>(PlayerSettings.GetPreloadedAssets() ?? Array.Empty<UnityEngine.Object>());
			if (list.Remove(asset))
			{
				PlayerSettings.SetPreloadedAssets(list.ToArray());
			}
		}

		private void EnsureDirectoryExists(string path)
		{
			if (!AssetDatabase.IsValidFolder(path))
			{
					string[] folders = path.Split('/');
					string currentPath = "";
					foreach (string folder in folders)
					{
							currentPath = string.IsNullOrEmpty(currentPath) ? folder : $"{currentPath}/{folder}";
							if (!AssetDatabase.IsValidFolder(currentPath))
							{
									string parentFolder = Path.GetDirectoryName(currentPath);
									string newFolderName = Path.GetFileName(currentPath);
									if (string.IsNullOrEmpty(parentFolder))
									{
											AssetDatabase.CreateFolder("Assets", newFolderName);
											currentPath = $"Assets/{newFolderName}";
									}
									else
									{
											AssetDatabase.CreateFolder(parentFolder, newFolderName);
									}
							}
					}
			}
		}

		private static string Sanitize(string key)
		{
				foreach (char c in Path.GetInvalidFileNameChars())
						key = key.Replace(c, '_');
				return key;
		}

		private static string ToGlob(string pattern)
		{
			if (string.IsNullOrEmpty(pattern)) return string.Empty;
			// Replace slot placeholder
			string glob = pattern.Replace("{n}", "*");
			// Replace any {meta:key} with a wildcard
			glob = System.Text.RegularExpressions.Regex.Replace(glob, "\\{meta:([^}]+)\\}", "*");
			return glob;
		}

		private void WipeAllLocalData()
		{
			try
			{
				PlayerPrefs.DeleteAll();
				PlayerPrefs.Save();

				var provider = saveSettings != null ?
						saveSettings.CreatePathProvider() : new DefaultStoragePathProvider();
				string root = provider.GetRootPath();

				if (Directory.Exists(root))
				{
					string sanitizedBase = Sanitize(saveSettings.saveKey ?? string.Empty);

					// Build deletion patterns including configured metadata and save stem patterns
					string metaPattern = saveSettings.metadataFileNamePattern;
					if (string.IsNullOrWhiteSpace(metaPattern) || !metaPattern.Contains("{n}"))
						metaPattern = "Slot{n}_Meta.bin"; // legacy fallback
					string metaGlob = ToGlob(metaPattern);

					string saveStem = saveSettings.saveFileName ?? string.Empty;
					string saveStemGlobBase = ToGlob(saveStem);

					string saveKeyBase = (sanitizedBase ?? string.Empty).Replace("{n}", "*");

					var patterns = new List<string>
						{
							// save file variants
							$"{saveStemGlobBase}.sav",
							$"{saveStemGlobBase}.sav.bak",
							$"{saveStemGlobBase}.tmp",
							// cloud local fallback files by key
							$"{saveKeyBase}.sav",
							$"{saveKeyBase}.json",
							// metadata caches
							metaGlob,
							// legacy metadata cache pattern for cleanup
							"Slot*_Meta.bin",
							// Supabase: be defensive and remove any local JSON metadata remnants if present
							"*.meta.json"
						};

					foreach (string pattern in patterns)
					{
						try
						{
							foreach (string file in Directory.GetFiles(root, pattern))
							{
								File.Delete(file);
							}
						}
						catch { }
					}

					// Also traverse per-slot subfolders (slotN) and delete the same patterns there
					foreach (var dir in Directory.GetDirectories(root, "slot*"))
					{
						try
						{
							string name = Path.GetFileName(dir);
							// Ensure it matches slot + digits only (e.g., slot1, slot23)
							bool looksLikeSlot = name.Length > 4 && name.StartsWith("slot", StringComparison.OrdinalIgnoreCase) && name.Skip(4).All(char.IsDigit);
							if (!looksLikeSlot) continue;

							foreach (string pattern in patterns)
							{
								try
								{
									foreach (string file in Directory.GetFiles(dir, pattern))
									{
										File.Delete(file);
									}
								}
								catch { }
							}

							// remove empty slot folder if possible
							try
							{
								if (!Directory.EnumerateFileSystemEntries(dir).Any())
									Directory.Delete(dir, false);
							}
							catch { }
						}
						catch { }
					}

					string shots = Path.Combine(root, saveSettings.screenshotFolderName);
					if (Directory.Exists(shots)) Directory.Delete(shots, true);
				}	

				var mgr = SaveManager.Instance;
				if (mgr != null)
				{
					// Use static call to ensure we don't accidentally invoke with null via extension syntax
					Arawn.CrystalSave.Runtime.SaveManagerExtensions.ResetAllSaveSlots(mgr);
				}

				AssetDatabase.Refresh();
				Logger.Log("Wiped all local save data and PlayerPrefs.", LogLevel.Info);
			}
			catch (Exception ex)
			{
					Logger.Log($"Failed to wipe local save data: {ex.Message}", LogLevel.Error);
			}
		}

		private int duplicateCount = 0;
		private int missingCount = 0;

		/// <summary>
		/// Creates or recreates cached SerializedObject wrappers for every asset
		/// so that OnGUI never allocates a new one per repaint.
		/// </summary>
		private void CacheSerializedObjects()
		{
			serializedSaveSettings        = saveSettings        != null ? new SerializedObject(saveSettings)        : null;
			serializedPrefabRegistry      = prefabRegistry      != null ? new SerializedObject(prefabRegistry)      : null;
			serializedTagRegistry         = tagRegistry         != null ? new SerializedObject(tagRegistry)         : null;
			serializedSceneObjectRegistry = sceneObjectRegistry != null ? new SerializedObject(sceneObjectRegistry) : null;
			serializedSaveSlotMetadata    = saveSlotMetadata    != null ? new SerializedObject(saveSlotMetadata)    : null;
			serializedLoggerConfig        = loggerConfig        != null ? new SerializedObject(loggerConfig)        : null;
			serializedAssetOverrides      = assetOverrides      != null ? new SerializedObject(assetOverrides)      : null;
		}

		private void CheckAndPromptUniqueIDFix()
		{
			List<UniqueID> problematicComponents = UniqueIDValidator.FindDuplicateOrMissingUniqueIDsInComponents<UniqueID>();

			duplicateCount = 0;
			missingCount = 0;

			foreach (var component in problematicComponents)
			{
				if (component == null || string.IsNullOrEmpty(component.ID))
					missingCount++;
				else
					duplicateCount++;
			}

			if (duplicateCount > 0 || missingCount > 0)
			{
				bool fixNow = EditorUtility.DisplayDialog(
					"UniqueID Issues Detected",
					$"Detected {duplicateCount} duplicate and {missingCount} missing UniqueIDs. Would you like to fix them now?",
					"Yes",
					"No"
				);

				if (fixNow)
				{
					int fixedCount = UniqueIDValidator.FixDuplicateOrMissingUniqueIDsInComponents<UniqueID>(problematicComponents);
					EditorUtility.DisplayDialog("UniqueID Fix",
						$"Fixed {fixedCount} duplicate or missing UniqueIDs.",
						"OK");
				}
			}
		}

		private void OnGUI()
		{
			float previousLabelWidth = EditorGUIUtility.labelWidth;
			EditorGUIUtility.labelWidth = 250;

			scrollPos = EditorGUILayout.BeginScrollView(
				scrollPos,
				GUILayout.Width(position.width - 20),
				GUILayout.Height(position.height - 20));

			/*──────────────────────────────────────────────────────────────*/
			/*  Header / logo                                               */
			/*──────────────────────────────────────────────────────────────*/
			if (logoTexture != null)
			{
				float maxLogoWidth = position.width - 40;
				float scale = Mathf.Min(1f, maxLogoWidth / logoTexture.width);
				float w = logoTexture.width * scale;
				float h = logoTexture.height * scale;

				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				GUILayout.Label(logoTexture, GUILayout.Width(w), GUILayout.Height(h));
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
				GUILayout.Space(20);
			}

			GUILayout.Label("Crystal Save Settings", EditorStyles.boldLabel);
			EditorGUILayout.Space();

                        if (saveSettings == null || prefabRegistry == null ||
                                tagRegistry == null || sceneObjectRegistry == null ||
                                migrationManager == null || saveSlotMetadata == null)
			{
				EditorGUILayout.HelpBox("Failed to load or create required assets.", MessageType.Error);
				if (GUILayout.Button("Retry"))
				{
					LoadOrCreateAssets();
					LoadLogoTexture();
					CacheSerializedObjects();
				}
				EditorGUILayout.EndScrollView();
				EditorGUIUtility.labelWidth = previousLabelWidth;
				return;
			}

			/*──────────────────────────────────────────────────────────────*/
			/*  SAVE-SETTINGS fold-out                                       */
			/*──────────────────────────────────────────────────────────────*/
			showSaveSettings = EditorGUILayout.Foldout(showSaveSettings, "Save Settings", true);
			if (showSaveSettings)
			{
				EditorGUI.indentLevel++;
				SerializedObject ss = serializedSaveSettings;
				if (ss == null) return;
				ss.Update();

				GUILayout.Label("Configure your save-system settings below.", EditorStyles.helpBox);

				EditorGUI.BeginChangeCheck();
				
				/* Supabase */
				SerializedProperty saveMethodProp   = ss.FindProperty("saveMethod");
				SerializedProperty backendProp      = ss.FindProperty("backend");
				SerializedProperty supaUrlProp      = ss.FindProperty("supabaseUrl");
				SerializedProperty supaKeyProp      = ss.FindProperty("supabaseAnonKey");
				SerializedProperty bucketProp       = ss.FindProperty("bucket");
				SerializedProperty firebaseBucketProp = ss.FindProperty("firebaseBucket");
				SerializedProperty firebaseIdProp     = ss.FindProperty("firebaseIdToken");
				SerializedProperty stratProp       = ss.FindProperty("userFolderStrategy");
				SerializedProperty resolverProp     = ss.FindProperty("customUserFolderResolver");
				SerializedProperty apiUrlProp      = ss.FindProperty("mySqlApiUrl");
				SerializedProperty authApiUrlProp  = ss.FindProperty("mySqlAuthApiUrl");
				SerializedProperty apiKeyProp      = ss.FindProperty("mySqlApiKey");
				SerializedProperty tableProp       = ss.FindProperty("tableName");
				SerializedProperty loginModeProp   = ss.FindProperty("mySqlLoginMode");
					SerializedProperty enableEncryptionProp  = ss.FindProperty("enableEncryption");
					SerializedProperty enableCompressionProp = ss.FindProperty("enableCompression");
					SerializedProperty masterProviderProp    = ss.FindProperty("masterSecretProvider");
					SerializedProperty keySourceProp         = ss.FindProperty("masterSecretSource");
					SerializedProperty useUserIdForEncryptionProp = ss.FindProperty("useUserIdForEncryption");
					SerializedProperty cloudCryptoModeProp   = ss.FindProperty("cloudCryptoMode");
					SerializedProperty cloudCryptoProviderProp = ss.FindProperty("cloudCryptoProvider");
					SerializedProperty enableVerificationProp = ss.FindProperty("enableSaveFileVerification");
				SerializedProperty enableLookupCacheProp = ss.FindProperty("enableLookupCache");
				SerializedProperty enableComponentLookupCacheProp = ss.FindProperty("enableComponentLookupCache");
				SerializedProperty optimizeRuntimeCaptureProp = ss.FindProperty("optimizeRuntimeCapture");
				SerializedProperty skipDuplicateIDCheckProp = ss.FindProperty("skipDuplicateIDCheck");
				SerializedProperty existingObjectBatchSizeProp = ss.FindProperty("existingObjectScanBatchSize");
				SerializedProperty prefabBatchSizeProp = ss.FindProperty("prefabInstantiationBatchSize");
				SerializedProperty groupBySceneProp = ss.FindProperty("groupInstantiationByScene");
				SerializedProperty componentBatchSizeProp = ss.FindProperty("componentApplyBatchSize");
				SerializedProperty activeStateBatchSizeProp = ss.FindProperty("activeStateApplyBatchSize");
				SerializedProperty syncTransformsProp = ss.FindProperty("syncTransformsAfterPrefabLoad");
				SerializedProperty applyParentWhenMissingProp = ss.FindProperty("applyParentWhenParentInfoMissing");
				SerializedProperty prefabPoolingProp = ss.FindProperty("usePrefabPooling");
				SerializedProperty defaultPoolSizeProp = ss.FindProperty("defaultPrefabPoolSize");
				SerializedProperty spawnPooledInSceneProp = ss.FindProperty("spawnPooledPrefabsInScene");
				SerializedProperty enablePooledPrefabBatchingProp = ss.FindProperty("enablePooledPrefabBatching");
				SerializedProperty pooledPrefabSpawnBatchSizeProp = ss.FindProperty("pooledPrefabSpawnBatchSize");
				SerializedProperty registerDisabledComponentsProp = ss.FindProperty("registerDisabledComponents");
				SerializedProperty disabledComponentScanModeProp = ss.FindProperty("disabledComponentScanMode");
				SerializedProperty scanOnlyActiveSceneProp = ss.FindProperty("scanOnlyActiveScene");
				SerializedProperty autoResolveConflictsProp = ss.FindProperty("autoResolveConflicts");
				SerializedProperty conflictPolicyProp = ss.FindProperty("autoConflictPolicy");
				SerializedProperty metadataRulesProp = ss.FindProperty("metadataRules");
				SerializedProperty overlayCanvasProp = ss.FindProperty("conflictOverlayCanvas");
				// Read early to compute disabling
				SerializedProperty enableCloud = ss.FindProperty("enableCloudSave");

				EditorGUILayout.PropertyField(
					saveMethodProp,
					new GUIContent(
						"Save Method",
						"Player Prefs is the only reliable local save option for Unity WebGL when you are not using Cloud Save, or when you rely on Keep Local Mirror and Live Conflict Resolution to keep data in sync in a WebGL project.\n" +
						"Binary File writes *.sav data to persistentDataPath and is recommended for desktop, console, and mobile builds thanks to its speed and scalability for large saves.\n" +
						"Crystal Save will also emit JSON when Cloud Save is enabled. If your cloud backend expects string-based payloads, set the Cloud Transport to JSON in the Cloud Save Settings below."));
				EditorGUILayout.PropertyField(ss.FindProperty("version"));
				// New header requested: place above Save File Name and after Version fields
				EditorGUILayout.LabelField("Save File Name Pattern", EditorStyles.boldLabel);
				SerializedProperty saveFileNameProp = ss.FindProperty("saveFileName");
				SerializedProperty saveKeyProp      = ss.FindProperty("saveKey");
				// Preview helper with {n} and {meta:key}
				string PatternPreview(string pattern)
				{
					if (string.IsNullOrEmpty(pattern)) return string.Empty;
					var dict = (saveSettings.defaultSlotMetadata != null)
						? saveSettings.defaultSlotMetadata.ToDictionary()
						: new System.Collections.Generic.Dictionary<string,string>();
					string resolved = pattern.Replace("{n}", "1");
					resolved = System.Text.RegularExpressions.Regex.Replace(resolved, "\\{meta:([^}]+)\\}", m =>
					{
						var key = m.Groups[1].Value;
						return dict != null && dict.TryGetValue(key, out var val) ? val : string.Empty;
					});
					return resolved;
				}

				// Save File Name field
				EditorGUILayout.PropertyField(
					saveFileNameProp,
					new GUIContent(
						"Save File Name",
						"File stem for .sav files. Supports {n} for slot number and {meta:key} placeholders. If omitted, the slot number will be appended (legacy behavior)."));
				// Preview Save File (slot 1) directly below the Save File Name field
				{
					var saveNamePrev = PatternPreview(saveFileNameProp.stringValue);
					if (!string.IsNullOrEmpty(saveNamePrev))
						EditorGUILayout.LabelField("Preview Save File (slot 1):", saveNamePrev + ".sav");
				}

				// Save Key field
				EditorGUILayout.PropertyField(
					saveKeyProp,
					new GUIContent(
						"Save Key",
						"Base key for PlayerPrefs and cloud keys. Supports {n} for slot number and {meta:key} placeholders (cloud metadata keys remain unchanged). If omitted, the slot number will be appended (legacy behavior)."));
				// Preview Save Key (slot 1) remains after the Save Key field
				{
					var saveKeyPrev = PatternPreview(saveKeyProp.stringValue);
					if (!string.IsNullOrEmpty(saveKeyPrev))
						EditorGUILayout.LabelField("Preview Save Key (slot 1):",  saveKeyPrev);
				}

				// Move Slot Metadata File Pattern right below the previews and before Wipe Local Save Data
				SerializedProperty metaPatternProp = ss.FindProperty("metadataFileNamePattern");
				EditorGUILayout.PropertyField(
					metaPatternProp,
					new GUIContent(
						"Slot Metadata File Pattern",
						"Local filename pattern for per-slot metadata saved on disk. Must include {n} for slot number; also supports {meta:key} placeholders."));
				// Validation + preview
				string mp = metaPatternProp.stringValue;
				bool invalidPat = string.IsNullOrWhiteSpace(mp) || !mp.Contains("{n}");
				string mpEffective = invalidPat ? "Slot{n}_Meta.bin" : mp;
				string metaPreview = PatternPreview(mpEffective);
				if (invalidPat)
				{
					EditorGUILayout.HelpBox("The pattern must include {n} to insert the slot number. Using legacy 'Slot{n}_Meta.bin' if omitted.", MessageType.Warning);
				}
				EditorGUILayout.LabelField("Preview Metadata File (slot 1):", metaPreview);
                                SerializedProperty persistentPathProp = ss.FindProperty("persistentPath");
                                SerializedProperty pathModeProp = persistentPathProp.FindPropertyRelative("mode");
                                SerializedProperty folderNameProp = persistentPathProp.FindPropertyRelative("customFolderName");
                                SerializedProperty rootProp = persistentPathProp.FindPropertyRelative("nonWebGLOutputRoot");
                                SerializedProperty runMigProp = ss.FindProperty("runPersistentPathMigrationOnStartup");
                                SerializedProperty autoSaveMigProp = ss.FindProperty("autoSaveMigratedData");
                                GUILayout.Space(5);
				EditorGUILayout.LabelField("Local Save Data", EditorStyles.boldLabel);
				EditorGUILayout.BeginHorizontal();
				if (GUILayout.Button(new GUIContent(
						"Wipe Local Save Data",
						"Deletes all locally stored save files, metadata, screenshots and PlayerPrefs keys.\n"
						+ "Use this if ghost saves appear during development or testing;\n"
						+ "such issues should not occur in built games.")))
				{
					bool confirm = EditorUtility.DisplayDialog(
						"Wipe All Save Data?",
						"This will wipe all existing save files, metadata, screenshots and PlayerPrefs keys.\nThis action cannot be undone.",
						"Wipe", "Cancel");
					if (confirm) WipeAllLocalData();
				}
				if (GUILayout.Button("Open Save Folder"))
				{
					OpenSaveFolder();
				}
				EditorGUILayout.EndHorizontal();
				GUILayout.Space(5);
				EditorGUILayout.LabelField("Persistent Path", EditorStyles.boldLabel);
				EditorGUILayout.PropertyField(
						pathModeProp,
						new GUIContent(
								"Mode",
								"How the root folder for save data is resolved.\n" +
								"Default → Application.persistentDataPath (WebGL: /idbfs/<product>).\n" +
								"Custom  → choose a folder name and optional root path.\n" +
								"          Helpful on WebGL for isolating multiple games or\n" +
								"          migrating saves between builds."));
				if ((PersistentPathMode)pathModeProp.enumValueIndex == PersistentPathMode.Custom)
				{
						folderNameProp.stringValue = System.Text.RegularExpressions.Regex.Replace(folderNameProp.stringValue.Trim(), "[\\\\/:*?\"<>|]", "");
						if (string.IsNullOrWhiteSpace(folderNameProp.stringValue))
								folderNameProp.stringValue = "CrystalSave";
						EditorGUILayout.PropertyField(folderNameProp, new GUIContent("Custom Folder Name"));
						EditorGUILayout.PropertyField(rootProp, new GUIContent("Non WebGL Output Root"));
						if (!string.IsNullOrEmpty(rootProp.stringValue) && !System.IO.Path.IsPathRooted(rootProp.stringValue))
								EditorGUILayout.HelpBox("Non WebGL Output Root must be an absolute path.", MessageType.Warning);
						string webGLPath = $"/idbfs/{folderNameProp.stringValue}";
						string nonWeb = System.IO.Path.Combine(string.IsNullOrEmpty(rootProp.stringValue) ? Application.persistentDataPath : rootProp.stringValue, folderNameProp.stringValue);
						EditorGUILayout.LabelField("Preview WebGL Path", webGLPath);
						EditorGUILayout.LabelField("Preview Non WebGL Path", nonWeb);
						if (GUILayout.Button("Test Resolve Path"))
						{
								var provider = saveSettings.CreatePathProvider();
								UnityEngine.Debug.Log($"Crystal Save path: {provider.GetRootPath()}");
						}
						if (GUILayout.Button("Move data from old path to new"))
						{
								var provider = saveSettings.CreatePathProvider();
								PersistentPathMigration.TryMigrate(Application.persistentDataPath, provider.GetRootPath());
						}
						EditorGUILayout.PropertyField(runMigProp, new GUIContent("Run Migration On Startup"));
				}
				GUILayout.Space(5);
				EditorGUILayout.LabelField("Save Migration", EditorStyles.boldLabel);
				EditorGUILayout.PropertyField(
					autoSaveMigProp,
					new GUIContent(
						"Auto Save Migrated Data",
						"After Crystal Save migrates an older slot, immediately overwrite the legacy file with the upgraded save so future loads don’t repeat the migration."));
				EditorGUILayout.PropertyField(ss.FindProperty("numberOfSaveSlots"));
				EditorGUILayout.PropertyField(
						ss.FindProperty("numberOfQuickSaveSlots"),
						new GUIContent(
								"Number Of Quick Save Slots",
								"How many quick saves to remember.\n" +
								"By default they start in a separate block so they won't clash\n" +
								"with normal or auto saves unless the slot numbers overlap.\n" +
								"New quick saves always go in the first quick save slot and push older ones up."));

				EditorGUILayout.PropertyField(
						ss.FindProperty("quickSaveSlotOffset"),
						new GUIContent(
								"Quick Save Slot Offset",
								"Slot number where quick saves begin.\n" +
								"Example: if this is 100, the latest quick save sits in slot 101.\n" +
								"Choose a high offset when you want many regular slots below it."));
					EditorGUILayout.PropertyField(
							ss.FindProperty("autoSaveSlotNumber"),
							new GUIContent(
									"Auto Save Slot Number",
									"Which slot the auto save uses. Set 0 to disable auto saves.\n" +
									"Regular or quick saves can still overwrite this slot if they\n" +
									"use the same number."));
					EditorGUILayout.PropertyField(
						ss.FindProperty("numberOfAutoSaveSlots"),
						new GUIContent(
							"Number Of Auto Save Slots",
							"How many auto save slots to remember.\n" +
							"Works like quick saves: the newest auto save goes into the\n" +
							"first slot and pushes older ones up.\n" +
							"Set 0 to disable multi-slot auto saves\n" +
							"(falls back to single Auto Save Slot Number)."));
					EditorGUILayout.PropertyField(
						ss.FindProperty("autoSaveSlotOffset"),
						new GUIContent(
							"Auto Save Slot Offset",
							"Slot number where auto saves begin.\n" +
							"Example: an offset of 200 means slot 201 holds the latest auto save.\n" +
							"Pick an offset that does not overlap with regular or quick save ranges."));

#if NANINOVEL && REMEMBERME_NANINOVEL_PRESENT
					GUILayout.Space(6);
					var naniStyle = new GUIStyle(GUI.skin.button);
					naniStyle.fontStyle = FontStyle.Bold;

					var naniColor = new Color(0.35f, 0.75f, 0.95f, 1f);
					var prevBg = GUI.backgroundColor;
					GUI.backgroundColor = naniColor;

						if (GUILayout.Button(new GUIContent(
							"Auto-Configure for Naninovel",
							"Reads Naninovel's State configuration and sets Crystal Save's\n" +
							"slot counts and offsets to match.\n\n" +
							"Save Slots -> expanded to include regular + quick + auto ranges\n" +
							"Quick Save Slots -> QuickSaveSlotLimit\n" +
							"Quick Save Offset -> SaveSlotLimit (so they don't overlap)\n" +
							"Auto Save Slots -> AutoSaveSlotLimit\n" +
							"Auto Save Offset -> SaveSlotLimit + QuickSaveSlotLimit\n" +
							"Auto Save Slot Number -> first auto-save slot (fallback, non-overlapping)"), naniStyle, GUILayout.Height(26)))
						{
							ConfigureForNaninovel(ss);
						}

					GUI.backgroundColor = prevBg;
#endif

						EditorGUILayout.PropertyField(enableEncryptionProp);
					if (enableEncryptionProp.boolValue && masterProviderProp != null)
					{
						EditorGUI.indentLevel++;
						bool cloudEnabled = enableCloud != null && enableCloud.boolValue;
						bool serverSideCloudCrypto = false;

						if (GUILayout.Button("Open Encryption Guide"))
						{
							Application.OpenURL("https://arawn-software-publishing.gitbook.io/arawn/basics/encryption");
						}

						if (cloudCryptoModeProp != null)
						{
							GUILayout.Space(4);
							EditorGUILayout.LabelField("Cloud Crypto", EditorStyles.boldLabel);
							EditorGUILayout.PropertyField(
								cloudCryptoModeProp,
								new GUIContent(
									"Cloud Crypto Mode",
									"ClientSide encrypts/decrypts on the client. ServerSide uses a server crypto provider and keeps the key off the client."));

							var cloudMode = (CloudCryptoMode)cloudCryptoModeProp.enumValueIndex;
							if (cloudMode == CloudCryptoMode.ServerSide)
							{
								var cloudProviderObj = EditorGUILayout.ObjectField(
									new GUIContent("Cloud Crypto Provider"),
									cloudCryptoProviderProp != null ? cloudCryptoProviderProp.objectReferenceValue : null,
									typeof(ServerSideCryptoProvider),
									false);
								if (cloudCryptoProviderProp != null)
									cloudCryptoProviderProp.objectReferenceValue = cloudProviderObj;

								if (cloudCryptoProviderProp == null || cloudCryptoProviderProp.objectReferenceValue == null)
								{
									EditorGUILayout.HelpBox(
										"Server-side crypto provider missing.\n\nCreate one via  Create ▸ Crystal Save ▸ Settings ▸ " +
										"Security ▸ Server-Side Crypto Provider  and drag it here.",
										MessageType.Warning);
								}

								EditorGUILayout.HelpBox(
									"Server-side crypto requires a custom backend that performs encrypt/decrypt. " +
									"The client will never receive the master key. This works for both local and cloud saves.",
									MessageType.Info);

								EditorGUILayout.HelpBox(
									"Server-side crypto requires a 32-byte base64 master key stored on your server. " +
									"Generate it on the server (recommended, e.g. `openssl rand -base64 32`), or click the one-click export below to " +
									"copy a new key to your clipboard (not stored in the project). " +
									"Each click generates a new key. Upload it to your server and never ship it in the build. " +
									"You do not need a Static Master Secret asset for ServerSide.",
									MessageType.Warning);

								if (GUILayout.Button("Export One-Time Server Key (Copy to Clipboard)"))
								{
									ExportOneTimeServerKey();
								}
							}

							serverSideCloudCrypto = cloudMode == CloudCryptoMode.ServerSide;
						}

						if (!serverSideCloudCrypto)
						{
							const int LegacyServerIndex = 1; // MasterSecretSource.Server (deprecated)
							var keySource = keySourceProp != null
								? (MasterSecretSource)keySourceProp.enumValueIndex
								: MasterSecretSource.StaticAsset;

							if (keySourceProp != null && keySourceProp.enumValueIndex == LegacyServerIndex)
							{
								EditorGUILayout.HelpBox(
									"Legacy key source detected: Server-fetched client-side keys are deprecated. " +
									"Use Cloud Crypto Mode = ServerSide if you want the key off the client.",
									MessageType.Warning);
								EditorGUILayout.BeginHorizontal();
								if (GUILayout.Button("Switch to Static Asset") && keySourceProp != null)
								{
									keySourceProp.enumValueIndex = (int)MasterSecretSource.StaticAsset;
									keySource = MasterSecretSource.StaticAsset;
								}
								if (GUILayout.Button("Switch to Passphrase") && keySourceProp != null)
								{
									keySourceProp.enumValueIndex = (int)MasterSecretSource.UserPassphrase;
									keySource = MasterSecretSource.UserPassphrase;
								}
								EditorGUILayout.EndHorizontal();
							}

							if (keySourceProp == null || keySourceProp.enumValueIndex != LegacyServerIndex)
							{
								if (keySourceProp != null)
								{
									string[] keyOptions = { "Static Asset", "User Passphrase" };
									int currentIndex = keySource == MasterSecretSource.UserPassphrase ? 1 : 0;
									int newIndex = EditorGUILayout.Popup(
										new GUIContent(
											"Key Source",
											"Where the master secret comes from at runtime (Static Asset or User Passphrase)."),
										currentIndex,
										keyOptions);
									if (newIndex != currentIndex)
									{
										keySourceProp.enumValueIndex = newIndex == 1
											? (int)MasterSecretSource.UserPassphrase
											: (int)MasterSecretSource.StaticAsset;
										keySource = newIndex == 1 ? MasterSecretSource.UserPassphrase : MasterSecretSource.StaticAsset;
									}
								}

								GUIContent providerLabel;
								Type providerType;
								if (keySource == MasterSecretSource.UserPassphrase)
								{
									providerLabel = new GUIContent("Passphrase Provider");
									providerType = typeof(PassphraseMasterSecretProvider);
								}
								else
								{
									providerLabel = new GUIContent("Static Master Secret");
									providerType = typeof(StaticMasterSecret);
								}

								var providerObj = EditorGUILayout.ObjectField(
									providerLabel,
									masterProviderProp.objectReferenceValue,
									providerType,
									false);
								masterProviderProp.objectReferenceValue = providerObj;

								if (masterProviderProp.objectReferenceValue == null)
								{
									string helpText = keySource == MasterSecretSource.UserPassphrase
										? "Passphrase provider missing.\n\nCreate one via  Create ▸ Crystal Save ▸ Settings ▸ " +
										  "Security ▸ User Passphrase Provider  and drag it here."
										: "Encryption key missing.\n\nCreate one via  Create ▸ Crystal Save ▸ Settings ▸ " +
										  "Security ▸ Static Master Secret  and drag it here.";

									EditorGUILayout.HelpBox(
										"⚠  " + helpText + " If the key changes or is lost, players can’t open existing saves.",
										MessageType.Warning);
								}

								if (keySource == MasterSecretSource.UserPassphrase)
								{
									EditorGUILayout.HelpBox(
										"Passphrase mode: the master secret is derived from a user-provided passphrase at runtime. " +
										"Prompt for the passphrase before Crystal Save initializes. Losing the passphrase means saves are lost.",
										MessageType.Info);
									EditorGUILayout.HelpBox(
										"Note: the Salt Base64 field is not the passphrase. The passphrase must be provided at runtime (e.g., login/lock screen).",
										MessageType.Info);
								}
								else
								{
									EditorGUILayout.HelpBox(
										"Static key mode: the master secret asset must be included in the build to decrypt local saves. " +
										"Keep it private and avoid changing it after release.",
										MessageType.Info);
								}
							}
						}
						else
						{
							EditorGUILayout.HelpBox(
								"Cloud Crypto Mode is set to ServerSide. Key Source and local master secret providers are ignored.",
								MessageType.Info);
						}

						if (useUserIdForEncryptionProp != null)
						{
							EditorGUILayout.PropertyField(
								useUserIdForEncryptionProp,
								new GUIContent(
									"Use User ID For Encryption",
									"When ON, derive a per-user key (master secret + user id). When OFF, all users share the same derived key."));

							if (useUserIdForEncryptionProp.boolValue)
							{
								EditorGUILayout.HelpBox(
									"Encryption derives a per-user key from master secret + user id. If Unity Authentication is installed and the player signs in, " +
									"the user id is their PlayerId; otherwise a per-install GUID (PlayerPrefs) is used. Encrypted saves will not transfer across " +
									"different user ids (e.g., Editor vs build or after reinstall) unless you keep the same user id. " +
									"Note: the per-install GUID is not stable and can change if you rename your project/company/product, delete PlayerPrefs, " +
									"or change Persistent Path Mode (save location changes can make saves appear missing).",
									MessageType.Info);
							}
							else
							{
								EditorGUILayout.HelpBox(
									"User ID is disabled for encryption: all users share the same derived key. " +
									"This makes saves portable across installs and Editor/build, but it reduces isolation. " +
									"If the master key is leaked, all saves can be decrypted. Not recommended for shared cloud environments.",
									MessageType.Warning);
							}
						}

						// New: granular encryption toggles
						SerializedProperty encMetaProp = ss.FindProperty("encryptSlotMetadata");
						SerializedProperty encShotProp = ss.FindProperty("encryptScreenshots");
						EditorGUI.BeginDisabledGroup(serverSideCloudCrypto);
						EditorGUILayout.PropertyField(encMetaProp, new GUIContent("Encrypt Slot Metadata"));
						EditorGUILayout.PropertyField(encShotProp, new GUIContent("Encrypt Screenshots"));
						EditorGUI.EndDisabledGroup();
						if (serverSideCloudCrypto)
						{
							EditorGUILayout.HelpBox(
								"Server-side crypto applies to save blobs only. Slot metadata and screenshots are stored unencrypted.",
								MessageType.Info);
						}
						EditorGUI.indentLevel--;
					}

				GUILayout.Space(10);
				EditorGUILayout.LabelField("Compression Settings", EditorStyles.boldLabel);
				EditorGUILayout.PropertyField(enableCompressionProp);

				// Backup & Verification -- placed after compression and before screenshots
				EditorGUILayout.PropertyField(enableVerificationProp);
				/*───── Screenshot block ─────────────────────────────*/
				SerializedProperty enableShots = ss.FindProperty("enableScreenshots");
				SerializedProperty folderProp = ss.FindProperty("screenshotFolderName");
				SerializedProperty formatProp = ss.FindProperty("screenshotFormat");
#if NANINOVEL
				SerializedProperty providerProp = ss.FindProperty("screenshotProvider");
#endif
				EditorGUILayout.PropertyField(enableShots);

				/* Cloud flags needed for grey-out calculation */
				// enableCloud already fetched above
				SerializedProperty transportProp = ss.FindProperty("cloudSaveTransport");
				SerializedProperty keepMirror = ss.FindProperty("keepLocalMirror");
				SerializedProperty cloudScreenshots = ss.FindProperty("cloudSaveScreenshots");
				SerializedProperty cloudMetadata   = ss.FindProperty("cloudSaveMetadata");
				SerializedProperty autoSignIn = ss.FindProperty("autoCloudSignIn");
				
				bool disableShotFields = !enableShots.boolValue;

			    EditorGUI.BeginDisabledGroup(disableShotFields);
                EditorGUILayout.PropertyField(folderProp);
#if NANINOVEL
                EditorGUILayout.PropertyField(providerProp, new GUIContent("Screenshot Provider"));
#endif
				EditorGUILayout.PropertyField(formatProp);
				// If encryption is enabled globally, offer the per-screenshot toggle nearby as well
				if (enableEncryptionProp.boolValue)
				{
					EditorGUI.indentLevel++;
					EditorGUILayout.PropertyField(ss.FindProperty("encryptScreenshots"), new GUIContent("Encrypt Screenshots"));
					EditorGUI.indentLevel--;
				}
				EditorGUI.EndDisabledGroup();

				GUILayout.Space(10);
				if (saveSlotMetadata != null && serializedSaveSlotMetadata != null)
				{
					SerializedObject metaSO = serializedSaveSlotMetadata;
					metaSO.Update();
					EditorGUILayout.LabelField("Custom Metadata", EditorStyles.boldLabel);
					EditorGUILayout.PropertyField(
						metaSO.FindProperty("entries"),
						new GUIContent(
						"Entries",
						"Custom metadata stored alongside each save slot. Use it to drive Save UI labels (e.g., player level, current quest, world location, XP) or to fuel conflict-resolution rules."),
						true);
					if (metaSO.ApplyModifiedProperties())
					{
							EditorUtility.SetDirty(saveSlotMetadata);
					}
				}

	       		// (moved) Slot metadata file pattern UI lives above the Wipe Local Save Data section now.

				/*───── Misc flags ─────────────────────────────*/
				EditorGUILayout.PropertyField(ss.FindProperty("logLevel"));
				
				// Granular Logging Configuration (only shown when LogLevel is Info)
				var logLevelProp = ss.FindProperty("logLevel");
				if (logLevelProp.enumValueIndex == (int)LogLevel.Info)
				{
					EditorGUI.indentLevel++;
					EditorGUILayout.BeginVertical(EditorStyles.helpBox);
					
					if (loggerConfig == null)
					{
						EditorGUILayout.LabelField("Granular Logging (Optional)", EditorStyles.boldLabel);
						EditorGUILayout.HelpBox(
							"Create a Logger Config to enable granular control over which components log Info messages. " +
							"Without this config, all Info logs are shown (default behavior).",
							MessageType.Info);

						if (GUILayout.Button("Create Logger Config"))
						{
							loggerConfig = ScriptableObject.CreateInstance<LoggerConfig>();
							loggerConfig.name = "LoggerConfig";
							
							string resDir = "Assets/Plugins/CrystalSave/Resources";
							if (!Directory.Exists(resDir))
							{
								Directory.CreateDirectory(resDir);
							}
							
							AssetDatabase.CreateAsset(loggerConfig, loggerConfigPath);
							AssetDatabase.SaveAssets();
							AssetDatabase.Refresh();
							
							// Update the cached SerializedObject for the newly created asset
							serializedLoggerConfig = new SerializedObject(loggerConfig);

							// Refresh Logger cache
							Logger.RefreshConfig();
							
							Logger.Log("LoggerConfig created successfully. Configure which components should log Info messages below.", LogLevel.Info);
						}
					}
					else
					{
						EditorGUILayout.LabelField("Granular Logging Configuration", EditorStyles.boldLabel);
						EditorGUILayout.HelpBox(
							"Enable/disable Info logging for specific component types. " +
							"Only enabled categories will log Info messages when LogLevel is set to Info.",
							MessageType.Info);
						
						SerializedObject loggerSO = serializedLoggerConfig;
						if (loggerSO == null) return;
						loggerSO.Update();
						
						// Remember Components - Core
						EditorGUILayout.LabelField("Remember Components - Core", EditorStyles.miniBoldLabel);
						EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberTransform"), new GUIContent("Transform"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberParent"), new GUIContent("Parent"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberGameObject"), new GUIContent("GameObject"));
						
						GUILayout.Space(5);
						
						// Remember Components - Physics
						EditorGUILayout.LabelField("Remember Components - Physics", EditorStyles.miniBoldLabel);
						EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberCollider"), new GUIContent("Collider"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberCollider2D"), new GUIContent("Collider2D"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberRigidbody"), new GUIContent("Rigidbody"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberJoint"), new GUIContent("Joint"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberHinges"), new GUIContent("Hinges"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberCharacterController"), new GUIContent("Character Controller"));
						
						GUILayout.Space(5);
						
						// Remember Components - Rendering
						EditorGUILayout.LabelField("Remember Components - Rendering", EditorStyles.miniBoldLabel);
						EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberMeshRenderer"), new GUIContent("Mesh Renderer"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberSkinnedMeshRenderer"), new GUIContent("Skinned Mesh Renderer"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberMaterial"), new GUIContent("Material"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberLight"), new GUIContent("Light"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberCamera"), new GUIContent("Camera"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberParticleSystem"), new GUIContent("Particle System"));
						
						GUILayout.Space(5);
						
						// Remember Components - Animation & Audio
						EditorGUILayout.LabelField("Remember Components - Animation & Audio", EditorStyles.miniBoldLabel);
						EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberAnimator"), new GUIContent("Animator"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberAudioSource"), new GUIContent("Audio Source"));
						
						GUILayout.Space(5);
						
						// Remember Components - Navigation & Terrain
						EditorGUILayout.LabelField("Remember Components - Navigation & Terrain", EditorStyles.miniBoldLabel);
						EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberNavmeshAgent"), new GUIContent("Navmesh Agent"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberTerrain"), new GUIContent("Terrain"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberTilemap"), new GUIContent("Tilemap"));
						
						GUILayout.Space(5);
						
						// Remember Components - UI
						EditorGUILayout.LabelField("Remember Components - UI", EditorStyles.miniBoldLabel);
						EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberUISlider"), new GUIContent("UI Slider"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberUIToggle"), new GUIContent("UI Toggle"));
						
						GUILayout.Space(5);
						
						// Remember Components - Other
						EditorGUILayout.LabelField("Remember Components - Other", EditorStyles.miniBoldLabel);
						EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberScenes"), new GUIContent("Scenes"));
				EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberComposite"), new GUIContent("Composite"));
				EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberCustomComponents"), new GUIContent("Custom Components"));
	#if REMEMBERME_GC2CORE_PRESENT || REMEMBERME_GC2MODULE_PRESENT || REMEMBERME_GC2STATS_PRESENT || REMEMBERME_GC2INVENTORY_PRESENT || REMEMBERME_GC2MELEE_PRESENT || REMEMBERME_GC2SHOOTER_PRESENT || REMEMBERME_GC2QUESTS_PRESENT || REMEMBERME_GC2DIALOGUE_PRESENT
				EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberGameCreator2"), new GUIContent("Game Creator 2"));
	#endif
				EditorGUILayout.PropertyField(loggerSO.FindProperty("rememberOther"), new GUIContent("Other"));						GUILayout.Space(5);
						
						// Core Systems
						EditorGUILayout.LabelField("Core Systems", EditorStyles.miniBoldLabel);
						EditorGUILayout.PropertyField(loggerSO.FindProperty("saveManager"), new GUIContent("Save Manager"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("componentManager"), new GUIContent("Component Manager"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("prefabManager"), new GUIContent("Prefab Manager"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("saveablePrefab"), new GUIContent("Saveable Prefab"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("saveableComponent"), new GUIContent("Saveable Component"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("saveSystem"), new GUIContent("Save System"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("trackedGameObjectProxy"), new GUIContent("Tracked GameObject Proxy"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("sceneManagement"), new GUIContent("Scene Management"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("saveSlotManager"), new GUIContent("Save Slot Manager"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("gameObjectTracker"), new GUIContent("GameObject Tracker"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("screenshotManager"), new GUIContent("Screenshot Manager"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("textureManager"), new GUIContent("Texture Manager"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("userSettingsManager"), new GUIContent("User Settings Manager"));
						
						GUILayout.Space(5);
						
						// Extensions & Utilities
						EditorGUILayout.LabelField("Extensions & Utilities", EditorStyles.miniBoldLabel);
						EditorGUILayout.PropertyField(loggerSO.FindProperty("saveManagerExtensions"), new GUIContent("Save Manager Extensions"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("serialization"), new GUIContent("Serialization"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("cryptography"), new GUIContent("Cryptography"));
						EditorGUILayout.PropertyField(loggerSO.FindProperty("cloudSave"), new GUIContent("Cloud Save"));
						
						GUILayout.Space(5);
						
						// Catch-All
						EditorGUILayout.LabelField("Catch-All", EditorStyles.miniBoldLabel);
						EditorGUILayout.PropertyField(loggerSO.FindProperty("other"), new GUIContent("Other/Uncategorized"));
						
						if (loggerSO.ApplyModifiedProperties())
						{
							EditorUtility.SetDirty(loggerConfig);
							Logger.RefreshConfig();
						}
						
						GUILayout.Space(5);
						
						EditorGUILayout.BeginHorizontal();
						if (GUILayout.Button("Enable All"))
						{
							// Core
							loggerConfig.rememberTransform = true;
							loggerConfig.rememberParent = true;
							loggerConfig.rememberGameObject = true;
							// Physics
							loggerConfig.rememberCollider = true;
							loggerConfig.rememberCollider2D = true;
							loggerConfig.rememberRigidbody = true;
							loggerConfig.rememberJoint = true;
							loggerConfig.rememberHinges = true;
							loggerConfig.rememberCharacterController = true;
							// Rendering
							loggerConfig.rememberMeshRenderer = true;
							loggerConfig.rememberSkinnedMeshRenderer = true;
							loggerConfig.rememberMaterial = true;
							loggerConfig.rememberLight = true;
							loggerConfig.rememberCamera = true;
							loggerConfig.rememberParticleSystem = true;
							// Animation & Audio
							loggerConfig.rememberAnimator = true;
							loggerConfig.rememberAudioSource = true;
							// Navigation & Terrain
							loggerConfig.rememberNavmeshAgent = true;
							loggerConfig.rememberTerrain = true;
							loggerConfig.rememberTilemap = true;
							// UI
							loggerConfig.rememberUISlider = true;
							loggerConfig.rememberUIToggle = true;
							// Other Remember
							loggerConfig.rememberScenes = true;
							loggerConfig.rememberComposite = true;
							loggerConfig.rememberCustomComponents = true;
		#if REMEMBERME_GC2CORE_PRESENT || REMEMBERME_GC2MODULE_PRESENT || REMEMBERME_GC2STATS_PRESENT || REMEMBERME_GC2INVENTORY_PRESENT || REMEMBERME_GC2MELEE_PRESENT || REMEMBERME_GC2SHOOTER_PRESENT || REMEMBERME_GC2QUESTS_PRESENT || REMEMBERME_GC2DIALOGUE_PRESENT
							loggerConfig.rememberGameCreator2 = true;
		#endif
							loggerConfig.rememberOther = true;
							// Core Systems
							loggerConfig.saveManager = true;
							loggerConfig.componentManager = true;
							loggerConfig.prefabManager = true;
							loggerConfig.saveablePrefab = true;
							loggerConfig.saveableComponent = true;
							loggerConfig.saveSystem = true;
							loggerConfig.trackedGameObjectProxy = true;
							loggerConfig.sceneManagement = true;
							loggerConfig.saveSlotManager = true;
							loggerConfig.gameObjectTracker = true;
							loggerConfig.screenshotManager = true;
							loggerConfig.textureManager = true;
							loggerConfig.userSettingsManager = true;
							// Extensions
							loggerConfig.saveManagerExtensions = true;
							loggerConfig.serialization = true;
							loggerConfig.cryptography = true;
							loggerConfig.cloudSave = true;
							loggerConfig.other = true;
							EditorUtility.SetDirty(loggerConfig);
							Logger.RefreshConfig();
						}
						
						if (GUILayout.Button("Disable All"))
						{
							// Core
							loggerConfig.rememberTransform = false;
							loggerConfig.rememberParent = false;
							loggerConfig.rememberGameObject = false;
							// Physics
							loggerConfig.rememberCollider = false;
							loggerConfig.rememberCollider2D = false;
							loggerConfig.rememberRigidbody = false;
							loggerConfig.rememberJoint = false;
							loggerConfig.rememberHinges = false;
							loggerConfig.rememberCharacterController = false;
							// Rendering
							loggerConfig.rememberMeshRenderer = false;
							loggerConfig.rememberSkinnedMeshRenderer = false;
							loggerConfig.rememberMaterial = false;
							loggerConfig.rememberLight = false;
							loggerConfig.rememberCamera = false;
							loggerConfig.rememberParticleSystem = false;
							// Animation & Audio
							loggerConfig.rememberAnimator = false;
							loggerConfig.rememberAudioSource = false;
							// Navigation & Terrain
							loggerConfig.rememberNavmeshAgent = false;
							loggerConfig.rememberTerrain = false;
							loggerConfig.rememberTilemap = false;
							// UI
							loggerConfig.rememberUISlider = false;
							loggerConfig.rememberUIToggle = false;
							// Other Remember
							loggerConfig.rememberScenes = false;
							loggerConfig.rememberComposite = false;
							loggerConfig.rememberCustomComponents = false;
		#if REMEMBERME_GC2CORE_PRESENT || REMEMBERME_GC2MODULE_PRESENT || REMEMBERME_GC2STATS_PRESENT || REMEMBERME_GC2INVENTORY_PRESENT || REMEMBERME_GC2MELEE_PRESENT || REMEMBERME_GC2SHOOTER_PRESENT || REMEMBERME_GC2QUESTS_PRESENT || REMEMBERME_GC2DIALOGUE_PRESENT
							loggerConfig.rememberGameCreator2 = false;
		#endif
							loggerConfig.rememberOther = false;
							// Core Systems
							loggerConfig.saveManager = false;
							loggerConfig.componentManager = false;
							loggerConfig.prefabManager = false;
							loggerConfig.saveablePrefab = false;
							loggerConfig.saveableComponent = false;
							loggerConfig.saveSystem = false;
							loggerConfig.trackedGameObjectProxy = false;
							loggerConfig.sceneManagement = false;
							loggerConfig.saveSlotManager = false;
							loggerConfig.gameObjectTracker = false;
							loggerConfig.screenshotManager = false;
							loggerConfig.textureManager = false;
							loggerConfig.userSettingsManager = false;
							// Extensions
							loggerConfig.saveManagerExtensions = false;
							loggerConfig.serialization = false;
							loggerConfig.cryptography = false;
							loggerConfig.cloudSave = false;
							loggerConfig.other = false;
							EditorUtility.SetDirty(loggerConfig);
							Logger.RefreshConfig();
						}
						
						if (GUILayout.Button("Delete Config"))
						{
							if (EditorUtility.DisplayDialog(
								"Delete Logger Config",
								"Are you sure you want to delete the Logger Config? All Info logs will be shown again (default behavior).",
								"Delete", "Cancel"))
							{
								string path = AssetDatabase.GetAssetPath(loggerConfig);
								AssetDatabase.DeleteAsset(path);
								loggerConfig = null;
								Logger.RefreshConfig();
								AssetDatabase.Refresh();
							}
						}
						EditorGUILayout.EndHorizontal();
					}
					
					EditorGUILayout.EndVertical();
					EditorGUI.indentLevel--;
					
					GUILayout.Space(5);
				}
				
				EditorGUILayout.PropertyField(optimizeRuntimeCaptureProp);
				EditorGUILayout.PropertyField(skipDuplicateIDCheckProp, new GUIContent("Skip Duplicate ID Check", "Disables duplicate ID validation in OnValidate for large scenes (10,000+ UniqueID components). GUID collisions are statistically impossible, but this check helps detect cloned objects. Disable only if OnValidate performance becomes an issue."));
				EditorGUILayout.PropertyField(enableLookupCacheProp, new GUIContent("Enable Lookup Cache"));
				if (EditorApplication.isPlaying && SaveManager.Instance != null)
				{
						EditorGUILayout.BeginHorizontal();
						bool runtimeCache = SaveManager.Instance.LookupCacheEnabled;
						string toggleLabel = runtimeCache ? "Disable Cache" : "Enable Cache";
						if (GUILayout.Button(toggleLabel))
						{
								SaveManager.Instance.SetLookupCacheEnabled(!runtimeCache);
								enableLookupCacheProp.boolValue = SaveManager.Instance.LookupCacheEnabled;
								ss.ApplyModifiedProperties();
								EditorUtility.SetDirty(saveSettings);
						}
						if (GUILayout.Button("Clear Cache"))
						{
								SaveManager.Instance.ClearLookupCache();
						}
						EditorGUILayout.EndHorizontal();
				}
				EditorGUILayout.PropertyField(enableComponentLookupCacheProp, new GUIContent("Enable Component Lookup Cache"));

				var scanExistingProp = ss.FindProperty("scanForExistingGameObjects");
				EditorGUILayout.PropertyField(
					scanExistingProp,
					new GUIContent(
						"Scan For Existing Game Objects",
						"When enabled, Crystal Save scans the active scene(s) at startup to find and register existing RememberGameObjects.\n\n"
						+ "Enable this if you have scene-authored objects that must be matched to previously saved data. It ensures correctness but adds startup/load cost proportional to scene size.\n\n"
						+ "Disable it to improve loading performance when most objects are spawned via SaveablePrefabFactory or otherwise registered explicitly. Objects not scanned will register on first activation and won’t be reconciled with old save data."));

				EditorGUI.indentLevel++;
				EditorGUI.BeginDisabledGroup(!scanExistingProp.boolValue);
				EditorGUILayout.PropertyField(
					existingObjectBatchSizeProp,
					new GUIContent(
						"Existing Object Scan Batch Size",
						"How many RememberGameObjects to register per frame while scanning. Higher values finish faster but may cause hitches; lower values smooth out the cost across frames. Set 0 or negative to scan in a single frame."));
				EditorGUI.EndDisabledGroup();
				EditorGUI.indentLevel--;
				EditorGUILayout.PropertyField(
					prefabBatchSizeProp,
					new GUIContent(
						"Prefab Instantiation Batch Size",
						"Number of prefabs instantiated per frame during load. 0 or negative for single-frame instantiation.\n\n"
						+ "Caution: Batching can restore objects in partial/out-of-order frames. For physics-heavy scenes (e.g., large towers),\n"
						+ "top pieces may spawn before the base, causing immediate collapse or jitter when Transforms and Rigidbodies aren’t yet fully in place.\n\n"
						+ "Recommendations:\n"
						+ "• Prefer single-frame instantiation (set 0) for physics-critical structures.\n"
						+ "• Or temporarily freeze/disable physics until all prefabs are instantiated, then unfreeze in a single step.\n"
						+ "• Optionally enable 'Sync Transforms After Prefab Load' to minimize discrepancies.\n\n"
						+ "Note: This risk is higher when 'Use Prefab Pooling' is OFF, since objects are freshly instantiated rather than reused from pools."));
				EditorGUILayout.PropertyField(
						componentBatchSizeProp,
						new GUIContent(
								"Component Apply Batch Size",
								"Number of components applied per frame during load. 0 or negative for single-frame application."));
				EditorGUILayout.PropertyField(
						activeStateBatchSizeProp,
						new GUIContent(
								"Active State Apply Batch Size",
								"Number of GameObject active states applied per frame during load. 0 or negative for single-frame application."));
				EditorGUILayout.PropertyField(
						syncTransformsProp,
						new GUIContent(
								"Sync Transforms After Prefab Load",
								"Enable to call Physics.SyncTransforms() once after all prefabs load instead of after each Rigidbody."));
				EditorGUILayout.PropertyField(
					applyParentWhenMissingProp,
					new GUIContent(
						"Apply Parent When Parent Info Missing",
						"When saved parent info is missing (ParentID/ParentStableKey/ParentPrefabAssetID), still apply parenting during the post-load pass. " +
						"This detaches the object to root. Disable to keep whatever parent is already assigned by gameplay systems (e.g., equipment)."));
				EditorGUILayout.PropertyField(
						groupBySceneProp,
						new GUIContent(
								"Group Instantiation By Scene",
								"When enabled, prefabs are instantiated grouped by their home scene to minimize SceneManager.SetActiveScene calls."));
				EditorGUILayout.PropertyField(
					prefabPoolingProp,
					new GUIContent(
						"Use Prefab Pooling",
						"Reuses inactive prefab instances instead of destroying and re-instantiating them on load.\n\n" +
						"Benefits: Dramatically reduces allocation spikes and stutter during heavy respawn scenes, keeps component state intact between saves, and speeds up restores when large pools are pre-warmed.\n" +
						"Trade-offs: Slightly higher memory footprint for the pooled inactive copies, and pooled prefabs must be written to tolerate reactivation instead of fresh Awake()/Start runs.")
				);
				if (prefabPoolingProp.boolValue)
				{
						EditorGUI.indentLevel++;
						EditorGUILayout.PropertyField(defaultPoolSizeProp,
								new GUIContent("Default Prefab Pool Size"));
						EditorGUILayout.PropertyField(
								spawnPooledInSceneProp,
								new GUIContent(
										"Spawn Pooled Prefabs In Scene",
										"When enabled, pooled SaveablePrefabs are moved to the active scene instead of DontDestroyOnLoad."));
						
						EditorGUILayout.PropertyField(
								enablePooledPrefabBatchingProp,
								new GUIContent(
										"Enable Pooled Prefab Batching",
										"When enabled, pool warming spreads creation across multiple frames to prevent hitches."));
						
						if (enablePooledPrefabBatchingProp.boolValue)
						{
								EditorGUI.indentLevel++;
								EditorGUILayout.PropertyField(
										pooledPrefabSpawnBatchSizeProp,
										new GUIContent(
												"Spawn Batch Size",
												"Number of pooled prefabs to create per frame. Lower values reduce hitches but increase loading time."));
								EditorGUI.indentLevel--;
						}
						
						EditorGUI.indentLevel--;
				}

				// ═══════════════════════════════════════════════════════════════
				// DISABLED COMPONENT REGISTRATION
				// ═══════════════════════════════════════════════════════════════
				GUILayout.Space(5);
				EditorGUILayout.LabelField("Disabled Component Registration", EditorStyles.boldLabel);
				EditorGUILayout.HelpBox(
					"When GameObjects with SaveableComponents (like talents, items, skills) start disabled in the scene, " +
					"Unity never calls Awake() or OnEnable() so they won't register automatically. Enable this to scan for and register them.",
					MessageType.Info);
				
					var registerDisabledComponentsContent = new GUIContent(
						"Register Disabled Components",
						"When enabled, Crystal Save scans for SaveableComponents on disabled GameObjects and registers them.\n\n" +
						"Enable this if you have talents, items, or other saveable data on GameObjects that start disabled.\n" +
						"Disable this for better performance if all your saveable GameObjects are active at startup.");
					registerDisabledComponentsProp.boolValue = EditorGUILayout.Toggle(
						registerDisabledComponentsContent,
						registerDisabledComponentsProp.boolValue);

				if (registerDisabledComponentsProp.boolValue)
				{
					EditorGUI.indentLevel++;
					EditorGUILayout.PropertyField(
						disabledComponentScanModeProp,
						new GUIContent(
							"Scan Mode",
							"When to scan for disabled SaveableComponents:\n\n" +
							"• OnInitialization - Scan once when Crystal Save initializes. Faster but misses additively loaded scenes.\n" +
							"• OnSceneLoad - Scan each time a scene loads. More thorough but has a slight performance cost per scene."));
					
					EditorGUILayout.PropertyField(
						scanOnlyActiveSceneProp,
						new GUIContent(
							"Scan Only Active Scene",
							"When enabled, only scan for disabled components in the newly loaded scene.\n" +
							"When disabled, scan all loaded scenes (heavier but catches cross-scene references)."));
					EditorGUI.indentLevel--;
				}

				GUILayout.Space(10);

#if CRYSTALSAVE_TIMEMACHINE
				// ═══════════════════════════════════════════════════════════════
				// CONFIGURATION PRESET SELECTOR
				// ═══════════════════════════════════════════════════════════════
				EditorGUILayout.Space(5);
				EditorGUILayout.LabelField("TimeMachine Configuration", EditorStyles.boldLabel);
				EditorGUILayout.HelpBox(
					"Select a pre-configured preset for common game mechanics, or choose 'Custom' for manual configuration.",
					MessageType.Info);
				
				SerializedProperty presetProp = ss.FindProperty("timeMachinePreset");
				if (presetProp != null)
				{
					// Store previous value to detect changes
					var previousPreset = (Arawn.CrystalSave.Runtime.TimeMachine.TimeMachinePresetType)presetProp.enumValueIndex;
					
					EditorGUILayout.PropertyField(
						presetProp,
                        new GUIContent(
                            "Configuration Preset",
                            "Pre-configured settings for common game mechanics:\n\n" +
                            "• 🏎️ Ghost Racing - Mario Kart, TrackMania style racing (Accumulative)\n" +
                            "• 🎮 Speedrunning - Time trial comparisons (FromOriginal)\n" +
                            "• 🧩 Puzzle Solver - Braid, The Witness style puzzle solving (Accumulative)\n" +
                            "• ⏰ Time Travel - Back to the Future, Chrono Trigger (MaxTwoBranches)\n" +
                            "• 🌿 Branching Story - Butterfly Effect, Life is Strange, Steins;Gate narratives (Accumulative)\n" +
                            "• 📹 Linear Replay - Simple record/playback system (FromOriginal)\n" +
                            "• 🎓 Training Mode - Tutorial/expert playback systems (FromOriginal)\n" +
                            "• 🔄 Loop Debugger - Fast iteration for development (FromOriginal)\n" +
                            "• 🧪 Experimental Divergence - Memory-optimized branching (Empty - 52% savings)\n" +
                            "• 🎯 Combo Training - Fighting game replay analysis (Accumulative)\n" +
                            "• 📚 Story Archaeology - Timeline history explorer (Accumulative)\n" +
                            "• ⚙️ Custom - Manually configure all settings below"));                                    // Auto-apply preset if it changed
					var newPreset = (Arawn.CrystalSave.Runtime.TimeMachine.TimeMachinePresetType)presetProp.enumValueIndex;
					if (newPreset != previousPreset && saveSettings != null)
					{
						// Apply changes to the SerializedObject first
						ss.ApplyModifiedProperties();
						
						// Now apply the preset (modifies the underlying ScriptableObject)
						saveSettings.ApplyPresetToSettings();
						
						// Refresh SerializedObject to reflect the preset changes
						ss.Update();
						
						// Mark as dirty to save changes
						EditorUtility.SetDirty(saveSettings);
						
						if (newPreset != Arawn.CrystalSave.Runtime.TimeMachine.TimeMachinePresetType.Custom)
						{
							UnityEngine.Debug.Log($"[RememberMe Settings] ✅ Auto-applied preset: {Arawn.CrystalSave.Runtime.TimeMachine.TimeMachinePresets.GetPresetName(newPreset)}");
						}
					}
					
					// Show preset info if not Custom
					var currentPreset = (Arawn.CrystalSave.Runtime.TimeMachine.TimeMachinePresetType)presetProp.enumValueIndex;
					if (currentPreset != Arawn.CrystalSave.Runtime.TimeMachine.TimeMachinePresetType.Custom)
					{
						var config = Arawn.CrystalSave.Runtime.TimeMachine.TimeMachinePresets.GetPresetConfig(currentPreset);
						if (config != null)
						{
							EditorGUILayout.Space(3);
							EditorGUILayout.BeginVertical(EditorStyles.helpBox);
							{
								EditorGUILayout.LabelField("📋 Current Preset Configuration:", EditorStyles.miniBoldLabel);
								
								// Description with game examples (word wrapped for readability)
								var descriptionStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
								descriptionStyle.fontSize = 11;
								descriptionStyle.padding = new RectOffset(4, 4, 4, 4);
								EditorGUILayout.LabelField(config.GetDescription(), descriptionStyle);
								
								EditorGUILayout.Space(4);
								EditorGUILayout.LabelField("⚙️ Technical Settings:", EditorStyles.miniBoldLabel);
								EditorGUILayout.LabelField($"• Resume Mode: {config.resumeMode}", EditorStyles.miniLabel);
								EditorGUILayout.LabelField($"• Branch Behavior: {config.autoBranchBehavior}", EditorStyles.miniLabel);
								EditorGUILayout.LabelField($"• Branch Copy: {config.branchCopyMode}", EditorStyles.miniLabel);
								EditorGUILayout.LabelField($"• Ghost Mode: {(config.allowRecordingDuringPlayback ? "✅ Enabled" : "❌ Disabled")}", EditorStyles.miniLabel);
								
							// Continuous Time with note for Ghost Racing and Time Travel
							string continuousTimeLabel = $"• Continuous Time: {(config.useContinuousTime ? "✅ Enabled" : "❌ Disabled")}";
							if (config.useContinuousTime && (currentPreset == Arawn.CrystalSave.Runtime.TimeMachine.TimeMachinePresetType.GhostRacing ||
																currentPreset == Arawn.CrystalSave.Runtime.TimeMachine.TimeMachinePresetType.TimeTravel))
							{
								continuousTimeLabel += " (can be overridden in code)";
							}
							EditorGUILayout.LabelField(continuousTimeLabel, EditorStyles.miniLabel);
							}
							EditorGUILayout.EndVertical();
							EditorGUILayout.Space(3);
								
							// Info box with optional Ghost Racing note
							string infoMessage = "✅ This preset configuration is automatically applied. Advanced settings are hidden and managed by the preset. " +
								"Select 'Custom' preset to manually control all settings.";
								
							if (currentPreset == Arawn.CrystalSave.Runtime.TimeMachine.TimeMachinePresetType.GhostRacing)
							{
								infoMessage += "\n\n⚠️ Ghost Racing Note: Continuous Time is enabled by default, which accumulates timestamps across recordings. " +
									"For isolated lap recordings (each starting from time 0), call SetContinuousTimeMode(false) before each recording in your code.";
							}
							else if (currentPreset == Arawn.CrystalSave.Runtime.TimeMachine.TimeMachinePresetType.TimeTravel)
							{
								infoMessage += "\n\n⏰ Time Travel Note: Limited to 2 branches (Original + Alt1). Perfect for simple time travel mechanics where you have " +
									"an original timeline and one alternate timeline. After Alt1 is created, any new divergence will overwrite Alt1 from the current position.";
							}                                            EditorGUILayout.HelpBox(infoMessage, MessageType.Info);
						}
					}
					else
					{
						// Show Custom preset description
						var config = Arawn.CrystalSave.Runtime.TimeMachine.TimeMachinePresets.GetPresetConfig(currentPreset);
						if (config != null)
						{
							EditorGUILayout.Space(3);
							EditorGUILayout.BeginVertical(EditorStyles.helpBox);
							{
								var descriptionStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
								descriptionStyle.fontSize = 11;
								descriptionStyle.padding = new RectOffset(4, 4, 4, 4);
								EditorGUILayout.LabelField(config.GetDescription(), descriptionStyle);
							}
							EditorGUILayout.EndVertical();
						}
						else
						{
							EditorGUILayout.Space(3);
							EditorGUILayout.HelpBox(
								"⚙️ Custom preset selected - you have full manual control over all advanced settings below.",
								MessageType.Info);
						}
					}
				}
                                
				// Max Save Duration - Always visible regardless of preset
				EditorGUILayout.Space(10);
				
				SerializedProperty timeMachineDurationProp = ss.FindProperty("timeMachineMaxSaveDuration");
				if (timeMachineDurationProp != null)
				{
					EditorGUILayout.PropertyField(
						timeMachineDurationProp,
						new GUIContent(
							"Max Save Duration",
							"Maximum duration (in seconds) of TimeMachine recordings to save.\n\n" +
							"• 30.0 = save last 30 seconds of history\n" +
							"• 0 = disable TimeMachine persistence\n" +
							"• -1 = save entire timeline (warning: very large save files!)\n\n" +
							"Only snapshots within this duration from the latest snapshot will be persisted."));
					
					float duration = timeMachineDurationProp.floatValue;
					if (duration < 0 && duration != -1f)
					{
						EditorGUILayout.HelpBox("Negative values other than -1 are not supported. Use -1 for unlimited or 0+ for specific duration.", MessageType.Warning);
					}
					else if (duration == 0)
					{
						EditorGUILayout.HelpBox("TimeMachine persistence is disabled (duration = 0).", MessageType.Info);
					}
					else if (duration == -1f)
					{
						EditorGUILayout.HelpBox("⚠ WARNING: Saving entire timeline! This can produce very large save files (MB to GB).\nRecommended only for replay systems.", MessageType.Warning);
					}
					else if (duration > 300f)
					{
						EditorGUILayout.HelpBox($"⚠ High duration ({duration}s = {duration/60f:F1} minutes). This may result in large save files.", MessageType.Warning);
					}
					else
					{
						EditorGUILayout.HelpBox($"✓ Saving last {duration} seconds of TimeMachine history.", MessageType.Info);
					}
				}
				
				GUILayout.Space(5);
				
				// ═══════════════════════════════════════════════════════════════
				// RECORDING DEFAULTS (Always visible)
				// Unity will automatically display the [Header] from SaveSettings.cs
				// ═══════════════════════════════════════════════════════════════
				EditorGUILayout.HelpBox(
					"Global defaults for snapshot recording. Individual SaveableComponents can override these settings per-object.",
					MessageType.Info);
				
				SerializedProperty useIntervalProp = ss.FindProperty("timeMachineUseInterval");
				if (useIntervalProp != null)
				{
					EditorGUILayout.PropertyField(
						useIntervalProp,
						new GUIContent(
							"Use Interval",
							"Default: Record snapshots at regular intervals instead of every frame.\n\n" +
							"✅ ENABLED (recommended): Records at fixed intervals (e.g., 0.1s = 10 snapshots/sec)\n" +
							"   • More predictable memory usage\n" +
							"   • Better performance for most games\n" +
							"   • Sufficient for smooth replay\n\n" +
							"❌ DISABLED: Records every frame\n" +
							"   • Very high memory usage (60+ snapshots/sec @ 60 FPS)\n" +
							"   • Only use for slow-motion replay or frame-perfect precision\n" +
							"   • May impact performance\n\n" +
							"SaveableComponents can override this setting per-object."));
				}
				
				SerializedProperty snapshotIntervalProp = ss.FindProperty("timeMachineSnapshotInterval");
				if (snapshotIntervalProp != null)
				{
					EditorGUILayout.PropertyField(
						snapshotIntervalProp,
						new GUIContent(
							"Snapshot Interval",
							"Default: Time between snapshots in seconds (only used if 'Use Interval' is enabled).\n\n" +
							"Common Settings:\n" +
							"• 0.033s = 30 snapshots/sec (very smooth, debug/replay mode)\n" +
							"• 0.05s = 20 snapshots/sec (smooth, high quality)\n" +
							"• 0.1s = 10 snapshots/sec (RECOMMENDED - good balance)\n" +
							"• 0.2s = 5 snapshots/sec (sufficient for most replays)\n" +
							"• 0.5s = 2 snapshots/sec (low quality, background objects)\n\n" +
							"Memory Impact:\n" +
							"• Lower interval = More snapshots = More memory + smoother replay\n" +
							"• Higher interval = Fewer snapshots = Less memory + choppier replay\n\n" +
							"Tip: Combine with Max Snapshots to control rewind window.\n" +
							"Example: 500 snapshots @ 0.1s = 50 seconds of history.\n\n" +
							"SaveableComponents can override this setting per-object."));
					
					float interval = snapshotIntervalProp.floatValue;
					if (interval <= 0)
					{
						EditorGUILayout.HelpBox("⚠ Interval must be greater than 0. Using 0.1s as fallback.", MessageType.Warning);
					}
					else if (interval < 0.033f)
					{
						EditorGUILayout.HelpBox($"⚠ Very low interval ({interval:F3}s = {1f/interval:F1} snapshots/sec). This will use significant memory.", MessageType.Warning);
					}
					else if (interval > 1f)
					{
						EditorGUILayout.HelpBox($"ℹ High interval ({interval}s = {1f/interval:F1} snapshots/sec). Replay may appear choppy.", MessageType.Info);
					}
				}
				
				SerializedProperty maxSnapshotsProp = ss.FindProperty("timeMachineMaxSnapshots");
				if (maxSnapshotsProp != null)
				{
					EditorGUILayout.PropertyField(
						maxSnapshotsProp,
						new GUIContent(
							"Max Snapshots",
							"Default: Maximum number of snapshots to keep per GameObject (oldest are discarded automatically).\n\n" +
							"Memory Guide (per GameObject):\n" +
							"• 200 snapshots = ~150 KB (20 seconds @ 0.1s interval)\n" +
							"• 500 snapshots = ~375 KB (50 seconds @ 0.1s interval) [RECOMMENDED]\n" +
							"• 1000 snapshots = ~750 KB (100 seconds @ 0.1s interval)\n" +
							"• 2000 snapshots = ~1.5 MB (200 seconds @ 0.1s interval)\n" +
							"• 6000 snapshots = ~4.5 MB (600 seconds @ 0.1s interval)\n\n" +
							"CPU Performance (Replay Cost):\n" +
							"Replaying snapshots is VERY fast - CPU is NOT a bottleneck!\n" +
							"• 100 snapshots replay: ~5-30ms total (negligible)\n" +
							"• Real-time playback: ~0.1-0.5ms/frame (no impact)\n" +
							"• Fast-forward (10x): ~3-5ms/frame (smooth at 60 FPS)\n\n" +
							"Recommendations:\n" +
							"• Standard Gameplay: 500-1000 snapshots (very low cost)\n" +
							"• Replay System: 3000-6000 snapshots (~2-6 MB per object)\n" +
							"• Debug/Testing: 10000+ snapshots (~6-12 MB per object)\n\n" +
							"On modern systems (32-64 GB RAM), even 10,000 snapshots per object is negligible.\n\n" +
							"SaveableComponents can override this setting per-object."));
					
					int maxSnaps = maxSnapshotsProp.intValue;
					if (maxSnaps <= 0)
					{
						EditorGUILayout.HelpBox("⚠ Max Snapshots must be greater than 0. Using 500 as fallback.", MessageType.Warning);
					}
					else
					{
						// Calculate preview with current interval setting
						SerializedProperty intervalForCalc = ss.FindProperty("timeMachineSnapshotInterval");
						float intervalValue = intervalForCalc != null ? intervalForCalc.floatValue : 0.1f;
						if (intervalValue <= 0) intervalValue = 0.1f;
						
						float totalSeconds = intervalValue * maxSnaps;
						float estimatedMemoryKB = maxSnaps * 0.75f; // Average estimate
						
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
							timeDisplay = $"{hours:F1} hours";
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
						
						EditorGUILayout.HelpBox(
							$"📊 Default Rewind Window:\n" +
							$"⏱️ Duration: {timeDisplay}\n" +
							$"💾 Est. Memory per Object: {memoryDisplay}\n" +
							$"📸 Capture Rate: {(1f / intervalValue):F1} snapshots/second\n\n" +
							$"With 100 recorded objects: ~{(estimatedMemoryKB * 100f / 1024f):F1} MB total",
							MessageType.None);
					}
				}
				
				GUILayout.Space(5);
				
				// Memory Budget Setting
				SerializedProperty memoryBudgetProp = ss.FindProperty("timeMachineMaxMemoryBudgetMB");
				if (memoryBudgetProp != null)
				{
					EditorGUILayout.PropertyField(
						memoryBudgetProp,
						new GUIContent(
							"Max Memory Budget (MB)",
							"Maximum total memory budget for ALL TimeMachine snapshots across ALL objects.\n\n" +
							"When exceeded, oldest snapshots are automatically pruned.\n\n" +
							"Memory Budget Guide:\n" +
							"• 100 MB - Light usage (~100-200 objects with 500 snapshots)\n" +
							"• 500 MB - Standard games (~500-1000 objects) [RECOMMENDED]\n" +
							"• 1000 MB - Complex simulations (~1000-2000 objects)\n" +
							"• 5000 MB - Large-scale systems (10,000+ objects)\n" +
							"• 0 MB - Unlimited (warning: can cause memory issues!)\n\n" +
							"This works together with 'Max Snapshots':\n" +
							"• Each object is limited by Max Snapshots (per-object)\n" +
							"• All objects together are limited by Memory Budget (global)\n\n" +
							"Example with 1000 objects:\n" +
							"• Max Snapshots = 500 per object\n" +
							"• Memory Budget = 500 MB\n" +
							"• Result: Each object gets ~0.5 MB (~500 snapshots)\n\n" +
							"Set to 0 for unlimited memory (use with caution)."));
					
					float budget = memoryBudgetProp.floatValue;
					if (budget < 0)
					{
						EditorGUILayout.HelpBox("⚠ Memory Budget cannot be negative. Using 0 (unlimited).", MessageType.Warning);
					}
					else if (budget == 0)
					{
						EditorGUILayout.HelpBox("⚠ Unlimited memory! Monitor memory usage carefully to avoid performance issues.", MessageType.Warning);
					}
					else if (budget < 50)
					{
						EditorGUILayout.HelpBox($"⚠ Very low memory budget ({budget} MB). May limit rewind duration significantly.", MessageType.Warning);
					}
					else if (budget >= 50 && budget <= 500)
					{
						EditorGUILayout.HelpBox($"✓ Good memory budget ({budget} MB). Suitable for most games.", MessageType.Info);
					}
					else if (budget > 5000)
					{
						EditorGUILayout.HelpBox($"ℹ Large memory budget ({budget} MB). Ensure your target platform has sufficient RAM.", MessageType.Info);
					}
				}
				
				GUILayout.Space(5);
				
				// Only show Advanced Settings if Custom preset is selected
				SerializedProperty presetCheckProp = ss.FindProperty("timeMachinePreset");
				bool isCustomPreset = presetCheckProp == null || 
					(Arawn.CrystalSave.Runtime.TimeMachine.TimeMachinePresetType)presetCheckProp.enumValueIndex == 
					Arawn.CrystalSave.Runtime.TimeMachine.TimeMachinePresetType.Custom;
				
				if (isCustomPreset)
				{
					EditorGUILayout.Space(10);
					
					// Continuous Time Mode
					SerializedProperty useContinuousTimeProp = ss.FindProperty("useContinuousTimeByDefault");
					if (useContinuousTimeProp != null)
					{
							EditorGUILayout.PropertyField(
									useContinuousTimeProp,
									new GUIContent(
											"Continuous Time Mode",
											"Timeline Mode: How timestamps are recorded.\n\n" +
											"• ON (Continuous) - Timeline continues from last point with no gaps when pausing/resuming recording. Timestamps accumulate across multiple recordings.\n" +
											"• OFF (Absolute) - Timeline resets to 0 for each recording. Each recording session starts from time 0.\n\n" +
											"⚠️ IMPORTANT FOR GHOST RACING:\n" +
											"If you want each lap/race to be independent (starting from time 0), you must either:\n" +
											"  1. Set this to OFF, OR\n" +
											"  2. Call GameObjectTimeMachine.Instance.SetContinuousTimeMode(false) in code before starting each recording\n\n" +
											"The Ghost Racing preset enables this by default, but demo code can override it for isolated lap recordings.\n\n" +
											"Default: ON (recommended for continuous gameplay recording)"));
					}
					
					// Resume Mode
					SerializedProperty resumeModeProp = ss.FindProperty("defaultResumeMode");
					if (resumeModeProp != null)
					{
							EditorGUILayout.PropertyField(
									resumeModeProp,
									new GUIContent(
											"Resume Mode",
											"What happens when you resume recording after stopping playback mid-timeline.\n\n" +
											"✂️ OVERWRITE MODE:\n" +
											"Replaces future snapshots from the resume point.\n" +
											"USE WHEN: Linear editing workflow (like video editing), single timeline sufficient, want to erase and redo mistakes, memory optimization.\n" +
											"AVOID WHEN: Want to preserve all attempts, comparing different approaches, need undo/redo between timelines.\n\n" +
											"🌿 AUTO-BRANCH MODE:\n" +
											"Non-destructive: Creates alternative timelines automatically.\n" +
											"USE WHEN: Exploring multiple solutions, need to compare approaches, want complete undo/redo history, recording gameplay variations.\n" +
											"AVOID WHEN: Memory is limited (creates many branches), only need one final timeline, simple linear workflow.\n\n" +
											"↩️ CONTINUE MODE (Default):\n" +
											"Jumps to timeline end, continues recording from there.\n" +
											"USE WHEN: Reviewing before continuing, want to inspect mid-timeline then resume, building linear sequence with review breaks, tutorial recording with pauses.\n" +
											"AVOID WHEN: Want to modify mid-timeline, need branching workflows, want to overwrite mistakes."));
					}
					
					// Auto-Branch Behavior
					SerializedProperty autoBranchProp = ss.FindProperty("defaultAutoBranchBehavior");
					if (autoBranchProp != null)
					{
							EditorGUILayout.PropertyField(
									autoBranchProp,
									new GUIContent(
											"Auto-Branch Behavior",
											"Controls what happens to Alternative branches when Auto-Branch mode is active.\n\n" +
											"🔄 OVERWRITE CURRENT BRANCH (Default):\n" +
											"Replaces Alternative branch from stop point (2 timelines max: Original + Alternative).\n" +
											"USE WHEN: Only need 2 timelines, iterating on Alternative until satisfied, memory is limited, simple A/B comparison workflow.\n" +
											"AVOID WHEN: Need to compare 3+ variations, want to preserve all attempts, exploring multiple solutions.\n\n" +
											"➡️ CONTINUE CURRENT BRANCH:\n" +
											"Jumps to Alternative's end, continues recording.\n" +
											"USE WHEN: Reviewing Alternative before extending it, want to inspect mid-timeline then resume, building linear Alternative with review breaks.\n" +
											"AVOID WHEN: Want to modify mid-Alternative, need multiple alternative branches, exploring divergent paths.\n\n" +
											"🌲 UNLIMITED BRANCHES:\n" +
											"Creates numbered branches: Alt1, Alt2, Alt3... infinite!\n" +
											"USE WHEN: Exploring multiple solutions (3+ variations), puzzle solving with many attempts, A/B/C/D testing workflow, want complete exploration history, ghost racing mechanics.\n" +
											"AVOID WHEN: Memory is very limited, only need 1-2 timelines, simple undo/redo workflow."));
					}
					
					// Branch Copy Mode
					SerializedProperty branchCopyProp = ss.FindProperty("defaultBranchCopyMode");
					if (branchCopyProp != null)
					{
							EditorGUILayout.PropertyField(
									branchCopyProp,
									new GUIContent(
											"Branch Copy Mode",
											"Controls what snapshot history gets copied when creating new Alt# branches.\n\n" +
											"🔵 FROM ORIGINAL / CLEAN (Default):\n" +
											"Alt# branches copy from Original only, never from parent. Prevents accumulation.\n" +
											"USE WHEN: Want isolated variations, each branch explores different path from Original, memory optimization, standard workflow trying different approaches independently, prevents 'snowball effect'.\n" +
											"AVOID WHEN: Need ghost racing, recursive puzzle solving, timeline archaeology mechanics.\n" +
											"Example: Alt1 copies from Original (not Alternative), giving clean slate to try different approach.\n\n" +
											"🟡 ACCUMULATIVE:\n" +
											"Alt# branches inherit ALL ancestor history (parent + grandparent...). Enables ghost racing!\n" +
											"USE WHEN: Ghost racing (see all previous race attempts), recursive puzzles (past selves activate switches), timeline archaeology, need to see/interact with all previous timelines, building on top of previous attempts.\n" +
											"AVOID WHEN: Memory is limited (accumulates all ancestor data), want isolated independent branches, snowball accumulation is undesired.\n" +
											"Example: Alt1 contains Alternative's history, Alt2 contains Alternative + Alt1 history (you see ghosts of all previous attempts).\n\n" +
											"🟢 EMPTY:\n" +
											"Alt# branches start with ZERO snapshots (pure divergence). ~52% memory savings!\n" +
											"USE WHEN: Maximum memory efficiency, only care about divergent portion, don't need to rewind before branch point, each branch completely independent, creating many branches (10+).\n" +
											"AVOID WHEN: Need to rewind to before branch point, want to see shared history, need context of how you got to branch point, comparing full timelines.\n" +
											"Example: Alt1 created at 7s has 0 snapshots initially, recording starts fresh from 7s onwards, cannot rewind before 7s."));
					}
					
					GUILayout.Space(5);
					
					// Ghost Mode
					SerializedProperty ghostModeProp = ss.FindProperty("allowRecordingDuringPlayback");
					if (ghostModeProp != null)
					{
							EditorGUILayout.PropertyField(
									ghostModeProp,
									new GUIContent(
											"Ghost Mode (Recording During Playback)",
											"Ghost Mode: Allow recording during playback (simultaneous record + playback).\n\n" +
											"WHEN TO ENABLE (Useful For):\n" +
											"✓ Ghost Racing - Record new attempt while racing against previous ghost (Mario Kart)\n" +
											"✓ Speedrunning - Compare current run against best time in real-time\n" +
											"✓ Training Mode - Record player while showing expert demonstration\n" +
											"✓ Puzzle Solver - Record new solution while replaying previous attempt\n" +
											"✓ Combo Training - Practice combos while watching tutorial playback\n" +
											"✓ Time Travel - See alternate timeline while recording new one\n\n" +
											"WHEN TO DISABLE (Not Recommended):\n" +
											"✗ Linear Replay - Simple record/playback without simultaneous operations\n" +
											"✗ Branching Story - Exclusive choices (can't replay and record at same time)\n" +
											"✗ Loop Debugger - Fast iteration without concurrent operations\n" +
											"✗ Memory-Constrained Platforms - Doubles memory usage (two timelines active)\n\n" +
											"CONFIGURATION PRESET COMPATIBILITY:\n" +
											"• Ghost Racing ✓ REQUIRED - Core mechanic\n" +
											"• Speedrunning ✓ REQUIRED - Compare runs in real-time\n" +
											"• Training Mode ✓ REQUIRED - Learn while watching\n" +
											"• Time Travel ✓ REQUIRED - View alternate timeline\n" +
											"• Puzzle Solver ✓ OPTIONAL - Helpful for comparison\n" +
											"• Branching Story ✗ NOT NEEDED - Mutually exclusive paths\n" +
											"• Linear Replay ✗ NOT NEEDED - Simple playback only\n" +
											"• Loop Debugger ✗ NOT NEEDED - Fast iteration focus\n\n" +
											"TECHNICAL IMPACT:\n" +
											"• Memory: ~2x usage (active recording + playback timelines)\n" +
											"• CPU: Minimal impact (both operations are lightweight)\n" +
											"• Complexity: Requires careful branch management\n\n" +
											"WARNING: Only enable if your game mechanic specifically requires simultaneous record + playback!"));
					}
								
				} // Close isCustomPreset if statement
                                                                
				// ═══════════════════════════════════════════════════════════════
				// GHOST ANIMATION SETTINGS (Visible for Ghost Mode presets)
				// ═══════════════════════════════════════════════════════════════
				GUILayout.Space(10);
				
				// Check if current preset uses Ghost Mode
				SerializedProperty presetForAnimCheck = ss.FindProperty("timeMachinePreset");
				bool showGhostAnimSettings = false;
				
				if (presetForAnimCheck != null)
				{
					var currentPresetType = (Arawn.CrystalSave.Runtime.TimeMachine.TimeMachinePresetType)presetForAnimCheck.enumValueIndex;
					
					// Check if this preset uses Ghost Mode
					if (currentPresetType == Arawn.CrystalSave.Runtime.TimeMachine.TimeMachinePresetType.Custom)
					{
						// For Custom preset, check the allowRecordingDuringPlayback field directly
						SerializedProperty ghostModeProp = ss.FindProperty("allowRecordingDuringPlayback");
						showGhostAnimSettings = ghostModeProp != null && ghostModeProp.boolValue;
					}
					else
					{
						// For other presets, check the preset config
						var config = Arawn.CrystalSave.Runtime.TimeMachine.TimeMachinePresets.GetPresetConfig(currentPresetType);
						showGhostAnimSettings = config != null && config.allowRecordingDuringPlayback;
					}
				}
				
				if (showGhostAnimSettings)
				{
					EditorGUILayout.Space(5);
					EditorGUILayout.LabelField("Ghost Animation Settings", EditorStyles.boldLabel);
					EditorGUILayout.HelpBox(
						"Configure animator parameters and behavior for ghost/clone animations. These settings are automatically applied to all ghosts/clones created at runtime.",
						MessageType.Info);
					
					SerializedProperty ghostAnimSettingsProp = ss.FindProperty("ghostAnimationSettings");
					if (ghostAnimSettingsProp != null)
					{
						EditorGUI.indentLevel++;
						
						// Playback Mode section
						EditorGUILayout.LabelField("Playback Mode", EditorStyles.boldLabel);
						
						SerializedProperty playbackModeProp = ghostAnimSettingsProp.FindPropertyRelative("playbackMode");
						if (playbackModeProp != null)
						{
							EditorGUILayout.PropertyField(
								playbackModeProp,
								new GUIContent(
									"Playback Mode",
									"How to replay recorded snapshots:\n\n" +
									"• Transform Interpolation (Default): Directly interpolate transform positions\n" +
									"  - Uses GhostAnimationController to drive animator based on velocity\n" +
									"  - Works with any GameObject (doesn't require NavMesh)\n" +
									"  - Replay is perfectly accurate to recording\n\n" +
									"• NavMesh Agent Path: Convert snapshots into NavMeshAgent waypoints\n" +
									"  - Ghost follows path using Unity's NavMeshAgent\n" +
									"  - Uses existing CharacterController/NPC locomotion systems\n" +
									"  - Animator driven by NavMeshAgent velocity (requires existing setup)\n" +
									"  - Path may deviate slightly due to NavMesh pathfinding\n" +
									"  - Requires NavMesh in scene"));
						}
						
						GhostPlaybackMode currentMode = (GhostPlaybackMode)playbackModeProp.enumValueIndex;
						
						// NavMesh Settings (only show if NavMeshAgentPath mode)
						if (currentMode == GhostPlaybackMode.NavMeshAgentPath)
						{
							GUILayout.Space(5);
							EditorGUILayout.LabelField("NavMesh Agent Settings", EditorStyles.boldLabel);
							
							SerializedProperty autoAddNavMesh = ghostAnimSettingsProp.FindPropertyRelative("autoAddNavMeshAgent");
							if (autoAddNavMesh != null)
							{
								EditorGUILayout.PropertyField(
									autoAddNavMesh,
									new GUIContent(
										"Auto Add NavMeshAgent",
										"Automatically add NavMeshAgent component to ghosts if not present."));
							}
							
							SerializedProperty waypointSkip = ghostAnimSettingsProp.FindPropertyRelative("waypointSkipCount");
							if (waypointSkip != null)
							{
								EditorGUILayout.PropertyField(
									waypointSkip,
									new GUIContent(
										"Waypoint Skip Count",
										"How many snapshots to skip between waypoints.\n" +
										"Higher values = fewer waypoints = smoother path but less accurate\n" +
										"0 = Use every snapshot (most accurate)"));
							}
							
							SerializedProperty minWaypointDist = ghostAnimSettingsProp.FindPropertyRelative("minWaypointDistance");
							if (minWaypointDist != null)
							{
								EditorGUILayout.PropertyField(
									minWaypointDist,
									new GUIContent(
										"Min Waypoint Distance",
										"Minimum distance between waypoints. Closer waypoints will be skipped."));
							}
							
							SerializedProperty navSpeed = ghostAnimSettingsProp.FindPropertyRelative("navMeshSpeed");
							if (navSpeed != null)
							{
								EditorGUILayout.PropertyField(
									navSpeed,
									new GUIContent(
										"NavMesh Speed",
										"Speed of the NavMeshAgent (should match average recording speed)."));
							}
							
							SerializedProperty navAngularSpeed = ghostAnimSettingsProp.FindPropertyRelative("navMeshAngularSpeed");
							if (navAngularSpeed != null)
							{
								EditorGUILayout.PropertyField(
									navAngularSpeed,
									new GUIContent(
										"Angular Speed",
										"How fast the agent can rotate (degrees per second)."));
							}
							
							SerializedProperty navAccel = ghostAnimSettingsProp.FindPropertyRelative("navMeshAcceleration");
							if (navAccel != null)
							{
								EditorGUILayout.PropertyField(
									navAccel,
									new GUIContent(
										"Acceleration",
										"NavMeshAgent acceleration (units per second²)."));
							}
							
							SerializedProperty navStopDist = ghostAnimSettingsProp.FindPropertyRelative("navMeshStoppingDistance");
							if (navStopDist != null)
							{
								EditorGUILayout.PropertyField(
									navStopDist,
									new GUIContent(
										"Stopping Distance",
										"Distance from waypoint when agent considers it reached."));
							}
							
							SerializedProperty navAutoBrake = ghostAnimSettingsProp.FindPropertyRelative("navMeshAutoBraking");
							if (navAutoBrake != null)
							{
								EditorGUILayout.PropertyField(
									navAutoBrake,
									new GUIContent(
										"Auto Braking",
										"Slow down when approaching waypoints."));
							}
							
							GUILayout.Space(5);
							
							// Advanced Features section
							EditorGUILayout.LabelField("Advanced Features", EditorStyles.boldLabel);
							
							SerializedProperty dynamicSpeed = ghostAnimSettingsProp.FindPropertyRelative("enableDynamicSpeed");
							if (dynamicSpeed != null)
							{
								EditorGUILayout.PropertyField(
									dynamicSpeed,
									new GUIContent(
										"Dynamic Speed Adjustment",
										"Dynamically adjust NavMeshAgent speed based on recorded velocity at each snapshot.\n\n" +
										"✅ Enabled: Ghost speed changes to match original recording (acceleration/deceleration)\n" +
										"❌ Disabled: Uses constant navMeshSpeed value\n\n" +
										"Use this for more realistic replays that match original movement patterns."));
							}
							
							SerializedProperty offMeshLinks = ghostAnimSettingsProp.FindPropertyRelative("enableOffMeshLinks");
							if (offMeshLinks != null)
							{
								EditorGUILayout.PropertyField(
									offMeshLinks,
									new GUIContent(
										"Off-Mesh Link Support",
										"Enable support for off-mesh links (jumping, teleporting).\n\n" +
										"✅ Enabled: Ghosts can traverse NavMesh off-mesh links\n" +
										"   • Jump between platforms\n" +
										"   • Climb ladders\n" +
										"   • Teleport through portals\n\n" +
										"❌ Disabled: Ghosts follow ground-only paths\n\n" +
										"Requires off-mesh links to be set up in your NavMesh."));
							}
							
							SerializedProperty pathViz = ghostAnimSettingsProp.FindPropertyRelative("enablePathVisualization");
							if (pathViz != null)
							{
								EditorGUILayout.PropertyField(
									pathViz,
									new GUIContent(
										"Path Visualization",
										"Visualize the NavMesh path in Scene view for debugging.\n\n" +
										"✅ Enabled: Shows waypoints as spheres and path as lines in Scene view\n" +
										"❌ Disabled: No visualization\n\n" +
										"Only visible in Scene view when ghost GameObject is selected.\n" +
										"Useful for debugging path issues or verifying waypoint placement."));
							}
							
							SerializedProperty smoothing = ghostAnimSettingsProp.FindPropertyRelative("waypointSmoothingLevel");
							if (smoothing != null)
							{
								EditorGUILayout.PropertyField(
									smoothing,
									new GUIContent(
										"Waypoint Curve Smoothing",
										"Apply Catmull-Rom spline smoothing to waypoints for more natural movement.\n\n" +
										"• 0 = No smoothing (direct waypoint following)\n" +
										"• 1-2 = Light smoothing (slightly rounded corners)\n" +
										"• 3-4 = Medium smoothing (smooth curves)\n" +
										"• 5 = Heavy smoothing (very smooth, flowing movement)\n\n" +
										"Higher values create more interpolated waypoints between original points.\n" +
										"Trade-off: Smoother movement vs. accuracy to original recording."));
								
								int smoothLevel = smoothing.intValue;
								if (smoothLevel > 0)
								{
									EditorGUILayout.HelpBox(
										$"Smoothing Level {smoothLevel}: Creates {smoothLevel} interpolated point(s) between each waypoint pair.\n" +
										$"This will increase total waypoints by ~{smoothLevel}x for smoother curves.",
										MessageType.Info);
								}
							}
							
							EditorGUILayout.HelpBox(
								"NavMesh mode uses Unity's NavMeshAgent for pathfinding. This allows ghosts to use existing CharacterController " +
								"or NPC locomotion systems with their own Animator logic. The Animator will be driven by the NavMeshAgent's " +
								"velocity automatically (no GhostAnimationController needed).",
								MessageType.Info);
						}
						SerializedProperty runtimeLine = ghostAnimSettingsProp.FindPropertyRelative("enableRuntimeLineRenderer");
						if (runtimeLine != null)
						{
							EditorGUILayout.Space(5);
							EditorGUILayout.LabelField("Runtime Path Rendering", EditorStyles.boldLabel);

							EditorGUILayout.PropertyField(
								runtimeLine,
								new GUIContent(
									"Enable Runtime Line Renderer",
									"Draw the NavMesh waypoint path during gameplay using a LineRenderer."));

							if (runtimeLine.boolValue)
							{
								EditorGUI.indentLevel++;

								SerializedProperty autoCreateLine = ghostAnimSettingsProp.FindPropertyRelative("autoCreateRuntimeLineRenderer");
								if (autoCreateLine != null)
								{
									EditorGUILayout.PropertyField(
										autoCreateLine,
										new GUIContent(
											"Auto-Create LineRenderer",
											"Automatically add a LineRenderer when runtime path rendering is enabled and no renderer override is supplied."));
								}

								SerializedProperty includeAgentPoint = ghostAnimSettingsProp.FindPropertyRelative("runtimePathIncludeAgentPosition");
								if (includeAgentPoint != null)
								{
									EditorGUILayout.PropertyField(
										includeAgentPoint,
										new GUIContent(
											"Include Agent Position",
											"Append the ghost's current NavMeshAgent position as the trailing vertex so the path follows movement."));
								}

								SerializedProperty respectToggle = ghostAnimSettingsProp.FindPropertyRelative("runtimePathRespectVisualizationToggle");
								if (respectToggle != null)
								{
									EditorGUILayout.PropertyField(
										respectToggle,
										new GUIContent(
											"Respect Scene View Toggle",
											"Hide the runtime path line when the editor-only path visualization is disabled."));
								}

								SerializedProperty lineWidthProp = ghostAnimSettingsProp.FindPropertyRelative("runtimePathLineWidth");
								if (lineWidthProp != null)
								{
									EditorGUILayout.PropertyField(
										lineWidthProp,
										new GUIContent(
											"Line Width",
											"World-space width of the runtime path line."));
								}

								SerializedProperty gradientProp = ghostAnimSettingsProp.FindPropertyRelative("runtimePathLineGradient");
								if (gradientProp != null)
								{
									EditorGUILayout.PropertyField(
										gradientProp,
										new GUIContent(
											"Color Gradient",
											"Gradient applied along the runtime path."));
								}

								SerializedProperty materialProp = ghostAnimSettingsProp.FindPropertyRelative("runtimePathLineMaterial");
								if (materialProp != null)
								{
									EditorGUILayout.PropertyField(
										materialProp,
										new GUIContent(
											"Material Override",
											"Optional material to apply to the runtime path line."));
								}

								EditorGUILayout.HelpBox(
									"Runtime path rendering lets players visualize ghost routes in-game. Disable or override these settings if you provide your own custom visualization.",
									MessageType.Info);

								EditorGUI.indentLevel--;
							}

							GUILayout.Space(5);
						}
						
						GUILayout.Space(5);
						
						// Animator Parameters section (only show for Transform Interpolation mode)
						if (currentMode == GhostPlaybackMode.TransformInterpolation)
						{
						EditorGUILayout.LabelField("Animator Parameters", EditorStyles.boldLabel);
						
						SerializedProperty speedParam = ghostAnimSettingsProp.FindPropertyRelative("speedParameterName");
						if (speedParam != null)
						{
							EditorGUILayout.PropertyField(
								speedParam,
								new GUIContent(
									"Speed Parameter Name",
									"Name of the float parameter in the Animator that controls movement speed (e.g., 'Speed', 'Velocity', 'MovementSpeed').\n\n" +
									"Leave empty to disable speed-based animation."));
						}
						
						SerializedProperty stateParam = ghostAnimSettingsProp.FindPropertyRelative("stateParameterName");
						if (stateParam != null)
						{
							EditorGUILayout.PropertyField(
								stateParam,
								new GUIContent(
									"State Parameter Name",
									"Name of the int parameter in the Animator that controls animation state (0 = Idle, 1 = Walk, 2 = Run).\n\n" +
									"Leave empty to disable state-based animation."));
						}
						
						SerializedProperty groundedParam = ghostAnimSettingsProp.FindPropertyRelative("groundedParameterName");
						if (groundedParam != null)
						{
							EditorGUILayout.PropertyField(
								groundedParam,
								new GUIContent(
									"Grounded Parameter Name",
									"Name of the bool parameter in the Animator that indicates if the character is grounded.\n\n" +
									"Leave empty to disable ground detection."));
						}
						
						GUILayout.Space(5);
						
						// Speed Thresholds section
						EditorGUILayout.LabelField("Speed Thresholds", EditorStyles.boldLabel);
						
						SerializedProperty idleThreshold = ghostAnimSettingsProp.FindPropertyRelative("idleThreshold");
						if (idleThreshold != null)
						{
							EditorGUILayout.PropertyField(
								idleThreshold,
								new GUIContent(
									"Idle Threshold",
									"Speed below this value is considered idle/stationary (MoveState = 0)."));
						}
						
						SerializedProperty walkThreshold = ghostAnimSettingsProp.FindPropertyRelative("walkThreshold");
						if (walkThreshold != null)
						{
							EditorGUILayout.PropertyField(
								walkThreshold,
								new GUIContent(
									"Walk Threshold",
									"Speed above Idle Threshold but below this value is considered walking (MoveState = 1)."));
						}
						
						SerializedProperty runThreshold = ghostAnimSettingsProp.FindPropertyRelative("runThreshold");
						if (runThreshold != null)
						{
							EditorGUILayout.PropertyField(
								runThreshold,
								new GUIContent(
									"Run Threshold",
									"Speed above this value is considered running (MoveState = 2)."));
						}
						
						GUILayout.Space(5);
						
						// Ground Detection section
						EditorGUILayout.LabelField("Ground Detection", EditorStyles.boldLabel);
						
						SerializedProperty enableGroundDetection = ghostAnimSettingsProp.FindPropertyRelative("enableGroundDetection");
						if (enableGroundDetection != null)
						{
							EditorGUILayout.PropertyField(
								enableGroundDetection,
								new GUIContent(
									"Enable Ground Detection",
									"Whether to perform ground checks using raycasts to update the Grounded parameter."));
						}
						
						if (enableGroundDetection != null && enableGroundDetection.boolValue)
						{
							SerializedProperty groundCheckDistance = ghostAnimSettingsProp.FindPropertyRelative("groundCheckDistance");
							if (groundCheckDistance != null)
							{
								EditorGUILayout.PropertyField(
									groundCheckDistance,
									new GUIContent(
										"Ground Check Distance",
										"Maximum distance to check for ground beneath the ghost (raycast length)."));
							}
							
							SerializedProperty groundLayer = ghostAnimSettingsProp.FindPropertyRelative("groundLayer");
							if (groundLayer != null)
							{
								EditorGUILayout.PropertyField(
									groundLayer,
									new GUIContent(
										"Ground Layer",
										"LayerMask to use for ground detection raycasts."));
							}
						}
						
						GUILayout.Space(5);
						
						// Additional Settings section
						EditorGUILayout.LabelField("Additional Settings", EditorStyles.boldLabel);
						
						SerializedProperty speedSmoothTime = ghostAnimSettingsProp.FindPropertyRelative("speedSmoothTime");
						if (speedSmoothTime != null)
						{
							EditorGUILayout.PropertyField(
								speedSmoothTime,
								new GUIContent(
									"Speed Smooth Time",
									"Time to smoothly interpolate speed values (prevents jittery animations)."));
						}
						
						SerializedProperty debugMode = ghostAnimSettingsProp.FindPropertyRelative("debugMode");
						if (debugMode != null)
						{
							EditorGUILayout.PropertyField(
								debugMode,
								new GUIContent(
									"Debug Mode",
									"Enable debug logging for animation state changes."));
						}
						
						} // Close if (currentMode == GhostPlaybackMode.TransformInterpolation)
						
						EditorGUI.indentLevel--;
						
						// Validation button
						GUILayout.Space(5);
						if (GUILayout.Button("Validate Settings", GUILayout.Height(25)))
						{
							if (saveSettings != null && saveSettings.ghostAnimationSettings != null)
							{
								saveSettings.ghostAnimationSettings.Validate();
							}
						}
					}
				}
				
				GUILayout.Space(10);
#endif

                bool addressablesPresent = false;
#if REMEMBERME_ADDRESSABLES_PRESENT
                addressablesPresent = true;
#endif
				var useAddrProp2 = ss.FindProperty("useAddressables");
				var gcUseAddr2 = new GUIContent(
					"Use Addressables",
					"When Addressables is installed, SaveSettings is no longer auto-created under Resources and Unity may move your Resources folder to 'Resources_moved'.\n\n" +
					"What to do:\n" +
					"• Look for your SaveSettings asset inside 'Resources_moved' (or wherever it was moved).\n" +
					"• If it wasn’t auto-moved, try manually deleting the old SaveSettings from the 'Resources' folder so the project stops treating it as a Resources asset.\n" +
					"• Keep only a single SaveSettings asset in the project. Open that asset (in Resources_moved or anywhere else) and tick its 'Use Addressables' checkbox.\n\n" +
					"Note: This window finds SaveSettings anywhere in the project, so you don’t need it to live under Resources.");

				EditorGUI.BeginDisabledGroup(!addressablesPresent);
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.PropertyField(useAddrProp2, gcUseAddr2);
				if (useAddrProp2.boolValue)
				{
					if (GUILayout.Button(AddressablesHelpWindow.HelpButtonContent, EditorStyles.miniButton, GUILayout.Width(22f)))
					{
							AddressablesHelpWindow.ShowWindow();
					}
				}
				EditorGUILayout.EndHorizontal();
				EditorGUI.EndDisabledGroup();

				if (!addressablesPresent)
				{
					// Keep the flag off when not available and inform the user
					if (useAddrProp2.boolValue)
					{
						useAddrProp2.boolValue = false;
						EditorUtility.SetDirty(saveSettings);
					}
					EditorGUILayout.HelpBox(
						"Unity Addressables package is not installed. Install it to enable Addressables integration. These settings are shown read-only to indicate support is available.",
						MessageType.Info);
				}
				
				GUILayout.Space(10);
				EditorGUILayout.LabelField("Cloud Save Settings", EditorStyles.boldLabel);

				EditorGUILayout.PropertyField(enableCloud);
				if (!enableCloud.boolValue)
				{
						backendProp.enumValueIndex = (int)SaveBackend.UnityCloudSave;
				}

				if (enableCloud.boolValue)
				{
					EditorGUILayout.PropertyField(
							keepMirror,
							new GUIContent("Keep Local Mirror",
									"Store screenshots & metadata locally even when Cloud Save is enabled."));

                                        EditorGUILayout.PropertyField(backendProp, new GUIContent("Save Backend"));

                                        var selectedBackend = (SaveBackend)backendProp.enumValueIndex;

                                        if (CloudSdkPresent)
                                        {
												if (selectedBackend == SaveBackend.UnityCloudSave)
                                                {
                                                        EditorGUILayout.PropertyField(
                                                                ss.FindProperty("defaultAuthProvider"),
                                                                new GUIContent("Default Auth Provider",
                                                                "Authentication method that will run before Cloud Save becomes available."));

                                                        EditorGUILayout.PropertyField(
                                                                transportProp,
                                                                new GUIContent("Cloud Save Transport",
                                                                        "Blob → Files API (.sav);  JsonBase64 → Data.Player API (quoted Base64)"));
                                                }
                                        }
                                        else if (selectedBackend == SaveBackend.UnityCloudSave)
                                        {
                                                EditorGUILayout.HelpBox(
                                                        "Unity Cloud Save package is not installed. Files get saved to disk Install the package to enable Cloud-Save options or untick Enable Cloud Save.",
                                                        MessageType.Info);
                                        }

					if (selectedBackend == SaveBackend.UnityCloudSave)
					{
						bool disableCloudScreenshots = !enableShots.boolValue;
						if (disableCloudScreenshots)
							cloudScreenshots.boolValue = false;
						EditorGUI.BeginDisabledGroup(disableCloudScreenshots);
						EditorGUILayout.PropertyField(
								cloudScreenshots,
								new GUIContent("Upload Screenshots",
										"Save screenshot files to Unity Cloud Save."));
						EditorGUI.EndDisabledGroup();

						EditorGUILayout.PropertyField(
								cloudMetadata,
								new GUIContent("Upload Slot Metadata",
										"Save slot metadata to Unity Cloud Save."));

						if (CloudSdkPresent && AuthSdkPresent)
						{
							EditorGUILayout.PropertyField(
									autoSignIn,
									new GUIContent("Auto-Login Unity Cloud Save",
											"If ON, SaveManager will automatically sign in to Unity Cloud Save on startup. " +
											"Turn OFF to handle login yourself via a custom UI."));
						}

						bool allowConflictUI = keepMirror.boolValue;
						using (new EditorGUI.DisabledScope(!allowConflictUI))
						{
							EditorGUILayout.PropertyField(autoResolveConflictsProp, new GUIContent("Auto Resolve Conflicts"));
							if (autoResolveConflictsProp.boolValue)
							{
								EditorGUI.indentLevel++;
								EditorGUILayout.PropertyField(
									conflictPolicyProp,
									new GUIContent(
										"Conflict Policy",
										"Latest: keep the newer file and discard the other.\n" +
										"Oldest: keep the older file, losing newer progress.\n" +
										"LocalWins: always keep the local copy and overwrite the cloud.\n" +
										"CloudWins: always keep the cloud copy and overwrite local.\n" +
										"Custom: evaluate Metadata Rules; unresolved conflicts fall back to the UI."));
								if ((AutoConflictPolicy)conflictPolicyProp.enumValueIndex == AutoConflictPolicy.Custom)
								{
									DrawMetadataRules(ss, metadataRulesProp);
								}
								EditorGUI.indentLevel--;
							}

							EditorGUILayout.PropertyField(
								overlayCanvasProp,
								new GUIContent(
									"Overlay Canvas",
									"Canvas prefab used by LiveConflictResolver when displaying the conflict resolution UI. Optional; if left unassigned, a default overlay canvas is created at runtime."));
						}

						if (!allowConflictUI)
						{
							EditorGUILayout.HelpBox(
								"Conflict resolution options require Keep Local Mirror to stay ON so the system can compare local and cloud copies.",
								MessageType.Info);
						}
					}
					else if (selectedBackend == SaveBackend.MySQL)
					{
						EditorGUI.indentLevel++;
						EditorGUILayout.PropertyField(apiUrlProp, new GUIContent("Web API URL"));
						EditorGUILayout.PropertyField(authApiUrlProp, new GUIContent("Login API URL"));
						EditorGUILayout.PropertyField(apiKeyProp, new GUIContent("API Key"));
						EditorGUILayout.PropertyField(tableProp, new GUIContent("Table Name"));
						EditorGUILayout.PropertyField(
								loginModeProp,
								new GUIContent(
										"Login Mode",
										"Anonymous: no credentials required.\n" +
										"Username Password: use SaveManager.Instance.MySqlSignUpAsync(username, password) to register,\n" +
										"SaveManager.Instance.MySqlSignInAsync(username, password) to log in and\n" +
										"SaveManager.Instance.MySqlSignOut() to log out."));
						EditorGUI.indentLevel--;
					}

					/* Supabase settings – only when backend == Supabase */
					if (selectedBackend == SaveBackend.Supabase)
					{
							EditorGUI.indentLevel++;
							EditorGUILayout.PropertyField(supaUrlProp,  new GUIContent("Supabase URL"));
							EditorGUILayout.PropertyField(supaKeyProp,  new GUIContent("Anon Key (PUBLIC)"));
							EditorGUILayout.PropertyField(bucketProp,   new GUIContent("Bucket Name"));
							EditorGUILayout.PropertyField(
									stratProp,
									new GUIContent("User-Folder Strategy",
											"How Crystal Save builds the per-user directory:\n" +
											"• Shared             → users/guest\n" +
											"• PublicPerBuild     → one global folder per buildGUID\n" +
											"• GuidPerDevice      → random GUID stored in PlayerPrefs\n" +
											"• UnityAuthentication→ auth.PlayerId (UGS Auth)\n" +
											"• Custom             → resolved via your own script"));

							if ((UserFolderStrategy)stratProp.enumValueIndex == UserFolderStrategy.Custom)
							{
								EditorGUILayout.PropertyField(
										resolverProp,
										new GUIContent("Custom Resolver",
												"Assign a ScriptableObject that implements IUserFolderResolver " +
												"used by Supabase, Firebase, and future cloud systems to resolve the folder path at runtime."));
							}
							EditorGUI.indentLevel--;
					}
					else if (selectedBackend == SaveBackend.Firebase)
					{
							EditorGUI.indentLevel++;
							EditorGUILayout.PropertyField(firebaseBucketProp, new GUIContent("Storage Bucket"));
							EditorGUILayout.PropertyField(firebaseIdProp, new GUIContent("ID Token"));
							EditorGUILayout.PropertyField(
									stratProp,
									new GUIContent("User-Folder Strategy",
											"How Crystal Save builds the per-user directory"));
							if ((UserFolderStrategy)stratProp.enumValueIndex == UserFolderStrategy.Custom)
							{
								EditorGUILayout.PropertyField(
										resolverProp,
										new GUIContent("Custom Resolver",
												"Assign a ScriptableObject that implements IUserFolderResolver " +
												"used by Supabase, Firebase, and future cloud systems."));
							}
							EditorGUI.indentLevel--;
					}
					// Cloud section completed
				}

				if (EditorGUI.EndChangeCheck())
				{
					ss.ApplyModifiedProperties();
					EditorUtility.SetDirty(saveSettings);
				}

				EditorGUI.indentLevel--;
			}

			EditorGUILayout.Space();

			DrawAssetOverrides();

			EditorGUILayout.Space();

			// Prefab Registry Section
			showPrefabRegistry = EditorGUILayout.Foldout(showPrefabRegistry, "Prefab Registry", true);
			if (showPrefabRegistry)
			{
				EditorGUI.indentLevel++;

				/* 1. Scene-instance → prefab autocorrect (keep as-is) */
				for (int i = 0; i < prefabRegistry.prefabEntries.Count; i++)
				{
					var e = prefabRegistry.prefabEntries[i];
					if (e.prefab != null && PrefabUtility.IsPartOfPrefabInstance(e.prefab))
					{
						var asset = PrefabUtility.GetCorrespondingObjectFromSource(e.prefab);
						if (asset != null)
						{
							Logger.Log($"[Crystal Save] Corrected scene object '{e.prefab.name}' to prefab asset '{asset.name}'.", LogLevel.Warning);
							e.prefab = asset;
							EditorUtility.SetDirty(prefabRegistry);
						}
					}
				}

				/* 2.  removed  */

				/* 3. Draw list */
				SerializedObject prefabRegistrySO = serializedPrefabRegistry;
				if (prefabRegistrySO == null) return;
				prefabRegistrySO.Update();
				GUILayout.Label("Manage and register all prefabs used in your save system.", EditorStyles.helpBox);
				EditorGUILayout.PropertyField(prefabRegistrySO.FindProperty("prefabEntries"), true);

				GUILayout.Space(10);
				GUILayout.BeginHorizontal();
				if (GUILayout.Button(GC_ValidateIDs))
				{
					ValidateUniqueIDs();
				}
				if (GUILayout.Button(GC_CleanDupes))
				{
					int removed = 0;
					var seen = new HashSet<GameObject>();

					for (int i = prefabRegistry.prefabEntries.Count - 1; i >= 0; i--)
					{
						var entry = prefabRegistry.prefabEntries[i];
						if (entry.prefab == null) continue;            // ignore blank rows added with “+”

						if (!seen.Add(entry.prefab))
						{
							prefabRegistry.prefabEntries.RemoveAt(i);
							removed++;
						}
					}

					if (removed > 0)
					{
						EditorUtility.SetDirty(prefabRegistry);
						Logger.Log($"[Crystal Save] Removed {removed} duplicate prefab entr{(removed == 1 ? "y" : "ies")}.", LogLevel.Warning);
					}
					else
					{
						Logger.Log("[Crystal Save] No duplicates found in PrefabRegistry.", LogLevel.Info);
					}
				}
				
				if (GUILayout.Button(GC_DeregisterPrefabs))
				{
					bool confirm = EditorUtility.DisplayDialog(
						"De-Register Prefabs – Confirm Action",
						"This will:\n\n" +
						"• Load every prefab currently listed in the Prefab Registry\n" +
						"• Remove all components from the namespace 'Arawn.CrystalSave.Runtime'\n" +
						"• Save those changes back to the prefab asset\n" +
						"• Clear the Prefab Registry afterward\n\n" +
						"• Be aware: Please be patient! The Editor becomes not responsive during the process!\n\n" +
						"⚠️ This operation is irreversible. Make sure your project is backed up.\n\n" +
						"Do you want to proceed?",
						"Yes, De-Register and Clean", "Cancel");

					if (confirm)
					{
						DeregisterAllPrefabs();
					}
				}
				
				GUILayout.EndHorizontal();

				if (prefabRegistrySO.ApplyModifiedProperties())
					EditorUtility.SetDirty(prefabRegistry);

				EditorGUI.indentLevel--;
			}

			EditorGUILayout.Space();

			// Tag Registry Section
			showTagRegistry = EditorGUILayout.Foldout(showTagRegistry, "Tag Registry", true);
			if (showTagRegistry)
			{
				EditorGUI.indentLevel++;
				SerializedObject tagRegistrySO = serializedTagRegistry;
				if (tagRegistrySO == null) return;
				tagRegistrySO.Update();
				GUILayout.Label("Manage all valid tags used within your project.", EditorStyles.helpBox);
				EditorGUILayout.PropertyField(tagRegistrySO.FindProperty("Tags"), true);
				if (tagRegistrySO.ApplyModifiedProperties())
				{
					EditorUtility.SetDirty(tagRegistry);
				}
				EditorGUILayout.Space();

				SerializedObject autoTagSettingsSO = serializedSaveSettings;
				if (autoTagSettingsSO == null) return;
				autoTagSettingsSO.Update();
				SerializedProperty autoRegisterTagsProp = autoTagSettingsSO.FindProperty("autoRegisterTags");
				EditorGUILayout.PropertyField(
					autoRegisterTagsProp,
					new GUIContent(
						"Auto Update Tags",
						"When enabled, the Tag Registry refreshes automatically after assembly reloads or project changes."));
				if (autoTagSettingsSO.ApplyModifiedProperties())
				{
					EditorUtility.SetDirty(saveSettings);
				}

				if (GUILayout.Button(GC_AutoRegTags))
				{
					AutoRegisterTags(true);
				}
				EditorGUI.indentLevel--;
			}

			EditorGUILayout.Space();

			// Scene Object Registry Section
			showSceneObjectRegistry = EditorGUILayout.Foldout(showSceneObjectRegistry, "Scene Object Registry", true);
				if (showSceneObjectRegistry)
				{
					EditorGUI.indentLevel++;
					SerializedObject sceneObjectRegistrySO = serializedSceneObjectRegistry;
					if (sceneObjectRegistrySO == null) return;
					sceneObjectRegistrySO.Update();
					GUILayout.Label(
						"Map scene-authored objects to the prefabs Crystal Save should respawn when they need rebuilding.\n" +
						"Prefabs only need a RememberGameObject component.\n" +
						"The prefab's UniqueID must exactly match the scene GameObject's UniqueID; matching IDs drive the restore even if the prefab is a different model.",
						EditorStyles.helpBox);

					// Scene Object Registry Settings
					GUILayout.Space(10);
					EditorGUILayout.LabelField("Scene Object Registry Settings", EditorStyles.boldLabel);
					SerializedObject saveSettingsSO = serializedSaveSettings;
					if (saveSettingsSO == null) return;
					saveSettingsSO.Update();
					EditorGUI.BeginChangeCheck();
					SerializedProperty sceneObjectScanModeProp = saveSettingsSO.FindProperty("sceneObjectScanMode");
					EditorGUILayout.PropertyField(
						sceneObjectScanModeProp,
						new GUIContent(
							"Scene Scan Mode",
							"Choose which scenes are scanned during auto-population."));
					var scanMode = (SceneObjectScanMode)sceneObjectScanModeProp.enumValueIndex;
					if (scanMode == SceneObjectScanMode.Custom)
					{
						EditorGUILayout.PropertyField(
							saveSettingsSO.FindProperty("scenesToScan"),
							new GUIContent("Scenes to Scan", "Custom scene paths. If empty, scans the currently opened scene."),
							true);
					}
					EditorGUILayout.PropertyField(
						sceneObjectRegistrySO.FindProperty("entries"),
						new GUIContent("Scene Object Entries"),
						true);
					if (EditorGUI.EndChangeCheck())
					{
						saveSettingsSO.ApplyModifiedProperties();
						EditorUtility.SetDirty(saveSettings);
					}

				GUILayout.Space(10);
				if (GUILayout.Button(GC_AutoPopulateScene))
				{
					if (sceneObjectRegistrationCoroutine == null)
					{
						sceneObjectRegistrationCoroutine = EditorCoroutineUtility.StartCoroutineOwnerless(AutoPopulateSceneObjectsCoroutine(true));
					}
					else
					{
						EditorUtility.DisplayDialog("Scene Object Registry", "Scene object population is already in progress.", "OK");
					}
				}
				if (sceneObjectRegistrySO.ApplyModifiedProperties())
				{
					EditorUtility.SetDirty(sceneObjectRegistry);
				}
				EditorGUI.indentLevel--;
			}

			EditorGUILayout.Space();

			// Asset Locations
			GUILayout.Label("Asset Locations", EditorStyles.boldLabel);
			GUILayout.Label($"SaveSettings: {saveSettingsPath}");
			GUILayout.Label($"PrefabRegistry: {prefabRegistryPath}");
			GUILayout.Label($"TagRegistry: {tagRegistryPath}");
			GUILayout.Label($"SceneObjectRegistry: {sceneObjectRegistryPath}");
			GUILayout.Label($"MigrationManager: {migrationManagerPath}");

			EditorGUILayout.EndScrollView();
			EditorGUIUtility.labelWidth = previousLabelWidth;
		}

		private void OpenSaveFolder()
		{
			try
			{
				var provider = saveSettings != null ? saveSettings.CreatePathProvider() : new DefaultStoragePathProvider();
				string root = provider.GetRootPath();

				if (string.IsNullOrEmpty(root))
				{
					EditorUtility.DisplayDialog("Folder Not Found", "The save folder path could not be resolved.", "OK");
					return;
				}

				if (Directory.Exists(root))
				{
#if UNITY_EDITOR_WIN
					Process.Start("explorer.exe", $"\"{root}\"");
#elif UNITY_EDITOR_OSX
					Process.Start("open", $"\"{root}\"");
#elif UNITY_EDITOR_LINUX
					Process.Start("xdg-open", $"\"{root}\"");
#else
					EditorUtility.RevealInFinder(root);
#endif
				}
				else
				{
					EditorUtility.DisplayDialog("Folder Not Found", $"The save folder does not exist:\n{root}", "OK");
				}
			}
			catch (Exception ex)
			{
				Logger.Log($"RememberMeSettingsWindow: Exception in OpenSaveFolder: {ex.Message}", LogLevel.Error);
				EditorUtility.DisplayDialog("Error", $"Failed to open save folder: {ex.Message}", "OK");
			}
		}

		private void DrawMetadataRules(SerializedObject ss, SerializedProperty rulesProp)
		{
				if (rulesProp.arraySize < 1)
						rulesProp.arraySize = 1;

				string[] keys = saveSettings.defaultSlotMetadata != null
						? saveSettings.defaultSlotMetadata.entries.Select(e => e.key).Where(k => !string.IsNullOrEmpty(k)).ToArray()
						: Array.Empty<string>();

				DrawRuleRow(rulesProp.GetArrayElementAtIndex(0), keys);

				if (rulesProp.arraySize > 1)
				{
						EditorGUILayout.LabelField("AND", GUILayout.Width(30));
						DrawRuleRow(rulesProp.GetArrayElementAtIndex(1), keys);
						if (GUILayout.Button("Remove AND Rule"))
						{
								rulesProp.arraySize = 1;
								ss.ApplyModifiedProperties();
								EditorUtility.SetDirty(saveSettings);
						}
				}
				else if (GUILayout.Button("Add AND Rule"))
				{
						rulesProp.arraySize = 2;
						ss.ApplyModifiedProperties();
						EditorUtility.SetDirty(saveSettings);
				}
		}

		private void DrawRuleRow(SerializedProperty ruleProp, string[] keys)
		{
			SerializedProperty typeProp = ruleProp.FindPropertyRelative("type");
			SerializedProperty keyProp = ruleProp.FindPropertyRelative("key");
			SerializedProperty opProp = ruleProp.FindPropertyRelative("op");
			SerializedProperty valueProp = ruleProp.FindPropertyRelative("value");

			string[] builtIns = { "Latest", "Oldest", "Local Wins", "Cloud Wins" };
			string[] options = keys.Concat(builtIns).ToArray();

			EditorGUILayout.BeginHorizontal();

			int selected;
			var ruleType = (MetadataRuleType)typeProp.enumValueIndex;
			if (ruleType == MetadataRuleType.Metadata)
			{
					int keyIndex = Array.IndexOf(keys, keyProp.stringValue);
					selected = keyIndex >= 0 ? keyIndex : 0;
			}
			else
			{
					selected = keys.Length + ((int)ruleType - 1);
			}

			int newSelected = EditorGUILayout.Popup(selected, options);

			if (newSelected < keys.Length)
			{
					typeProp.enumValueIndex = (int)MetadataRuleType.Metadata;
					keyProp.stringValue = keys[newSelected];
			}
			else
			{
					typeProp.enumValueIndex = newSelected - keys.Length + 1;
					keyProp.stringValue = string.Empty;
					opProp.enumValueIndex = (int)ComparisonOp.Equals;
					valueProp.stringValue = string.Empty;
			}

			if ((MetadataRuleType)typeProp.enumValueIndex == MetadataRuleType.Metadata)
			{
					opProp.enumValueIndex = (int)(ComparisonOp)EditorGUILayout.EnumPopup((ComparisonOp)opProp.enumValueIndex, GUILayout.Width(80));
					valueProp.stringValue = EditorGUILayout.TextField(valueProp.stringValue);
			}

			EditorGUILayout.EndHorizontal();
		}

		private List<string> GetScenePathsForSceneObjectScan()
		{
			var scenesToProcess = new List<string>();
			var seenPaths = new HashSet<string>(StringComparer.Ordinal);

			void AddScenePath(string scenePath)
			{
				if (string.IsNullOrWhiteSpace(scenePath))
					return;
				if (seenPaths.Add(scenePath))
					scenesToProcess.Add(scenePath);
			}

			switch (saveSettings.sceneObjectScanMode)
			{
				case SceneObjectScanMode.CurrentOpenedScene:
				{
					AddScenePath(SceneManager.GetActiveScene().path);
					Logger.Log("Scene scan mode: Current Opened Scene.", LogLevel.Info);
					break;
				}

				case SceneObjectScanMode.ScenesInBuildList:
				{
					foreach (var buildScene in EditorBuildSettings.scenes)
					{
						if (buildScene != null && buildScene.enabled)
							AddScenePath(buildScene.path);
					}
					Logger.Log($"Scene scan mode: Scenes in Build List ({scenesToProcess.Count} scenes).", LogLevel.Info);
					break;
				}

				case SceneObjectScanMode.AllScenesInProject:
				{
					string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
					foreach (string sceneGuid in sceneGuids)
					{
						AddScenePath(AssetDatabase.GUIDToAssetPath(sceneGuid));
					}
					Logger.Log($"Scene scan mode: All Scenes in Project ({scenesToProcess.Count} scenes).", LogLevel.Info);
					break;
				}

				case SceneObjectScanMode.Custom:
				{
					if (saveSettings.scenesToScan != null)
					{
						foreach (string customScenePath in saveSettings.scenesToScan)
						{
							AddScenePath(customScenePath);
						}
					}

					if (scenesToProcess.Count == 0)
					{
						AddScenePath(SceneManager.GetActiveScene().path);
						Logger.Log("Scene scan mode: Custom with empty list. Defaulting to Current Opened Scene.", LogLevel.Info);
					}
					else
					{
						Logger.Log($"Scene scan mode: Custom ({scenesToProcess.Count} scenes).", LogLevel.Info);
					}
					break;
				}
			}

			return scenesToProcess;
		}

		private IEnumerator AutoPopulateSceneObjectsCoroutine(bool isManual)
		{
			Logger.Log("AutoPopulateSceneObjectsCoroutine started.", LogLevel.Info);

			try
			{
				if (isManual)
				{
					EditorUtility.DisplayDialog("Scene Object Registry", "Starting scene object population...", "OK");
				}

				List<string> scenesToProcess = GetScenePathsForSceneObjectScan();
				if (scenesToProcess.Count == 0)
				{
					const string noScenesMessage =
						"No valid scenes were found for the selected Scene Scan Mode.\n" +
						"If the current scene is unsaved, save it first and try again.";
					Logger.Log(noScenesMessage, LogLevel.Warning);
					if (isManual)
					{
						EditorUtility.DisplayDialog("Scene Object Registry", noScenesMessage, "OK");
					}
					yield break;
				}

				string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
				var prefabByUniqueId = new Dictionary<string, GameObject>(StringComparer.Ordinal);
				var duplicatePrefabUniqueIds = new HashSet<string>(StringComparer.Ordinal);
				foreach (string guid in prefabGuids)
				{
					string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
					GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
					if (prefab == null)
						continue;

					UniqueID prefabUid = prefab.GetComponent<UniqueID>();
					if (prefabUid == null || string.IsNullOrEmpty(prefabUid.ID))
						continue;

					if (prefabByUniqueId.TryGetValue(prefabUid.ID, out GameObject existingPrefab))
					{
						if (!duplicatePrefabUniqueIds.Contains(prefabUid.ID))
						{
							Logger.Log(
								$"Duplicate prefab UniqueID '{prefabUid.ID}' found on '{existingPrefab.name}' and '{prefab.name}'. Scene objects using this ID will be skipped.",
								LogLevel.Warning);
						}
						duplicatePrefabUniqueIds.Add(prefabUid.ID);
						continue;
					}

					prefabByUniqueId[prefabUid.ID] = prefab;
				}

				List<SceneObjectRegistry.SceneObjectEntry> newEntries = new List<SceneObjectRegistry.SceneObjectEntry>();
				int processedCount = 0;
				int skippedCount = 0;

				for (int i = 0; i < scenesToProcess.Count; i++)
				{
					string scenePath = scenesToProcess[i];
					if (!File.Exists(scenePath))
					{
						Logger.Log($"Scene path '{scenePath}' does not exist. Skipping.", LogLevel.Warning);
						skippedCount++;
						continue;
					}

					EditorUtility.DisplayProgressBar(
						"Scanning Scenes",
						$"Processing scene: {Path.GetFileNameWithoutExtension(scenePath)}",
						(float)i / scenesToProcess.Count);

					var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
					yield return null;

#pragma warning disable CS0618 // Suppress FindObjectsByType deprecation warning for cross-version compatibility
					var sceneUniqueIds = FindObjectsByType<UniqueID>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#pragma warning restore CS0618
					foreach (var uid in sceneUniqueIds)
					{
						if (uid == null || string.IsNullOrEmpty(uid.ID))
						{
							Logger.Log($"Skipping invalid UniqueID in scene '{scene.name}'.", LogLevel.Warning);
							skippedCount++;
							continue;
						}

						if (duplicatePrefabUniqueIds.Contains(uid.ID))
						{
							Logger.Log(
								$"Skipping scene object '{uid.gameObject.name}' in scene '{scene.name}' because prefab UniqueID '{uid.ID}' is duplicated across prefab assets.",
								LogLevel.Warning);
							skippedCount++;
							continue;
						}

						if (prefabByUniqueId.TryGetValue(uid.ID, out GameObject prefab))
						{
							newEntries.Add(new SceneObjectRegistry.SceneObjectEntry
							{
								UniqueID = uid.ID,
								PrefabAsset = prefab
							});
							processedCount++;
							Logger.Log(
								$"Matched scene object '{uid.gameObject.name}' with UniqueID '{uid.ID}' to prefab '{prefab.name}' by UniqueID.",
								LogLevel.Info);
						}
						else
						{
							Logger.Log(
								$"No prefab with matching UniqueID '{uid.ID}' found for scene object '{uid.gameObject.name}' in scene '{scene.name}'. Skipped.",
								LogLevel.Info);
							skippedCount++;
						}
					}

					EditorUtility.DisplayProgressBar(
						"Scanning Scenes",
						$"Completed scene: {Path.GetFileNameWithoutExtension(scenePath)}",
						(float)(i + 1) / scenesToProcess.Count);
					yield return null;
				}

				bool entriesChanged = false;
				if (sceneObjectRegistry.Entries.Count != newEntries.Count)
				{
					entriesChanged = true;
				}
				else
				{
					for (int i = 0; i < newEntries.Count; i++)
					{
						if (sceneObjectRegistry.Entries[i].UniqueID != newEntries[i].UniqueID ||
							sceneObjectRegistry.Entries[i].PrefabAsset != newEntries[i].PrefabAsset)
						{
							entriesChanged = true;
							break;
						}
					}
				}

				if (!entriesChanged)
				{
					if (isManual)
					{
						EditorUtility.DisplayDialog("Scene Object Registry", "No changes detected in scene objects. Auto-population skipped.", "OK");
					}
					Logger.Log("No changes detected in scene objects. Auto-population skipped.", LogLevel.Info);
					yield break;
				}

				sceneObjectRegistry.Entries = newEntries;
				EditorUtility.SetDirty(sceneObjectRegistry);
				AssetDatabase.SaveAssets();

				Logger.Log($"Successfully populated {processedCount} scene objects. Skipped {skippedCount}.", LogLevel.Info);
				if (isManual)
				{
					EditorUtility.DisplayDialog("Scene Object Registry", $"Successfully populated {processedCount} scene objects.\nSkipped {skippedCount}.", "OK");
				}
			}
			finally
			{
				EditorUtility.ClearProgressBar();
				sceneObjectRegistrationCoroutine = null;
			}
		}

		private void ValidateUniqueIDs()
		{
			if (prefabRegistry == null)
			{
				EditorUtility.DisplayDialog("Validate UniqueIDs", "PrefabRegistry is not loaded.", "OK");
				return;
			}

			// Check for duplicate or missing UniqueIDs
			List<PrefabRegistry.PrefabEntry> problematicEntries = UniqueIDValidator.FindDuplicateOrMissingUniqueIDs<PrefabRegistry.PrefabEntry>(
				prefabRegistry.prefabEntries,
				(entry) => entry.uniqueID
			);

			// Check for mismatched IDs (registry ID doesn't match prefab's PrefabAssetID)
			List<PrefabRegistry.PrefabEntry> mismatchedEntries = new List<PrefabRegistry.PrefabEntry>();
			foreach (var entry in prefabRegistry.prefabEntries)
			{
				if (entry.prefab == null) continue;
				if (problematicEntries.Contains(entry)) continue; // Already flagged

				var saveable = entry.prefab.GetComponent<SaveablePrefab>();
				if (saveable == null) continue;

				string prefabAssetID = saveable.PrefabAssetID;
				if (!string.IsNullOrEmpty(prefabAssetID) && prefabAssetID != entry.uniqueID)
				{
					mismatchedEntries.Add(entry);
					Logger.Log($"ID Mismatch: Registry has '{entry.uniqueID}' but prefab '{entry.prefab.name}' has PrefabAssetID '{prefabAssetID}'.", LogLevel.Warning);
				}
			}

			if (problematicEntries.Count == 0 && mismatchedEntries.Count == 0)
			{
				EditorUtility.DisplayDialog("Validate UniqueIDs", "No duplicate, missing, or mismatched UniqueIDs found in PrefabRegistry.", "OK");
				return;
			}

			int duplicateCount = 0;
			int missingCount = 0;
			int mismatchCount = mismatchedEntries.Count;

			foreach (var entry in problematicEntries)
			{
				if (string.IsNullOrEmpty(entry.uniqueID))
					missingCount++;
				else
					duplicateCount++;
			}

			string message = "";
			if (duplicateCount > 0)
				message += $"Found {duplicateCount} duplicate UniqueIDs.\n";
			if (missingCount > 0)
				message += $"Found {missingCount} missing UniqueIDs.\n";
			if (mismatchCount > 0)
				message += $"Found {mismatchCount} mismatched UniqueIDs (registry ID differs from prefab's PrefabAssetID).\n";
			message += "Would you like to fix them now?";

			bool fixNow = EditorUtility.DisplayDialog(
				"UniqueID Issues Detected",
				message,
				"Yes",
				"No"
			);

			if (!fixNow)
				return;

			// Fix mismatched entries first (sync registry to match prefab's PrefabAssetID)
			int mismatchFixedCount = 0;
			foreach (var entry in mismatchedEntries)
			{
				var saveable = entry.prefab.GetComponent<SaveablePrefab>();
				if (saveable == null) continue;

				string correctID = saveable.PrefabAssetID;
				if (string.IsNullOrEmpty(correctID)) continue;

				// Update the registry entry to match the prefab's PrefabAssetID
				Logger.Log($"Fixing mismatch for '{entry.prefab.name}': Registry '{entry.uniqueID}' → Prefab's PrefabAssetID '{correctID}'.", LogLevel.Info);
				entry.uniqueID = correctID;
				mismatchFixedCount++;
			}

			Action<PrefabRegistry.PrefabEntry, string> setID = (entry, newID) =>
			{
				entry.uniqueID = newID;

				string prefabPath = AssetDatabase.GetAssetPath(entry.prefab);
				GameObject prefabAsset = PrefabUtility.LoadPrefabContents(prefabPath);
				if (prefabAsset != null)
				{
					SaveablePrefab saveable = prefabAsset.GetComponent<SaveablePrefab>();
					if (saveable != null)
					{
							SerializedObject serializedSaveable = new SerializedObject(saveable);
							SerializedProperty prefabAssetIDProp = serializedSaveable.FindProperty("prefabAssetID");
							SerializedProperty uniqueIDProp = serializedSaveable.FindProperty("uniqueID");
							if (prefabAssetIDProp != null)
							{
									prefabAssetIDProp.stringValue = newID;
							}
							else
							{
									Logger.Log($"SaveablePrefab: '{entry.prefab.name}' does not have a 'prefabAssetID' field.", LogLevel.Warning);
							}

							if (uniqueIDProp != null && !string.IsNullOrEmpty(uniqueIDProp.stringValue))
							{
									uniqueIDProp.stringValue = string.Empty;
									Logger.Log($"SaveablePrefab: Cleared instance uniqueID on '{entry.prefab.name}'.", LogLevel.Info);
							}

							serializedSaveable.ApplyModifiedProperties();

							PrefabUtility.SaveAsPrefabAsset(prefabAsset, prefabPath);
					}
					else
					{
						Logger.Log($"Prefab '{entry.prefab.name}' does not have a SaveablePrefab component.", LogLevel.Warning);
					}

					PrefabUtility.UnloadPrefabContents(prefabAsset);
				}
				else
				{
					Logger.Log($"Failed to load prefab asset for '{entry.prefab.name}'.", LogLevel.Warning);
				}
			};

			int fixedCount = UniqueIDValidator.FixDuplicateOrMissingUniqueIDs<PrefabRegistry.PrefabEntry>(
				problematicEntries,
				setID
			);

			EditorUtility.SetDirty(prefabRegistry);
			AssetDatabase.SaveAssets();

			// Build result message
			string resultMessage = "";
			if (fixedCount > 0)
				resultMessage += $"Fixed {fixedCount} duplicate or missing UniqueIDs.\n";
			if (mismatchFixedCount > 0)
				resultMessage += $"Fixed {mismatchFixedCount} mismatched UniqueIDs (synced registry to prefab's PrefabAssetID).";
			if (string.IsNullOrEmpty(resultMessage))
				resultMessage = "No fixes were applied.";

			EditorUtility.DisplayDialog("UniqueID Fix", resultMessage.Trim(), "OK");
		}

		private void DeregisterAllPrefabs()
		{
			if (prefabRegistry == null)
			{
				EditorUtility.DisplayDialog("Prefab Registry", "Prefab Registry asset is not loaded.", "OK");
				return;
			}

			int entryCount = prefabRegistry.prefabEntries.Count;
			if (entryCount == 0)
			{
				EditorUtility.DisplayDialog("Prefab Registry", "There are no prefabs listed to deregister.", "OK");
				return;
			}

			int totalComponentsRemoved = 0;

			try
			{
				for (int i = 0; i < entryCount; i++)
				{
					var entry = prefabRegistry.prefabEntries[i];
					if (entry == null || entry.prefab == null)
						continue;

					string assetPath = AssetDatabase.GetAssetPath(entry.prefab);
					if (string.IsNullOrEmpty(assetPath))
					{
						Logger.Log($"Deregister Prefabs: Unable to resolve asset path for '{entry.prefab.name}'.", LogLevel.Warning);
						continue;
					}

					float progress = (float)i / entryCount;
					EditorUtility.DisplayProgressBar(
						"Deregister Prefabs",
						$"Cleaning {entry.prefab.name} ({i + 1}/{entryCount})",
						progress);

					GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
					if (prefabRoot == null)
					{
						Logger.Log($"Deregister Prefabs: Failed to load prefab at '{assetPath}'.", LogLevel.Warning);
						continue;
					}

					int removed = RemoveCrystalSaveComponentsRecursive(prefabRoot.transform);
					totalComponentsRemoved += removed;

					PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
					PrefabUtility.UnloadPrefabContents(prefabRoot);
				}
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}

			prefabRegistry.prefabEntries.Clear();
			EditorUtility.SetDirty(prefabRegistry);
			AssetDatabase.SaveAssets();

			Logger.Log($"Deregister Prefabs: Removed {totalComponentsRemoved} Crystal Save components and cleared the registry.", LogLevel.Info);
			EditorUtility.DisplayDialog(
				"Prefab Registry",
				$"Removed Crystal Save components from listed prefabs. Components removed: {totalComponentsRemoved}.",
				"OK");
		}

		private int RemoveCrystalSaveComponentsRecursive(Transform root)
		{
			int removed = 0;

			var components = root.GetComponents<Component>();
			foreach (var component in components)
			{
				if (component == null) continue;

				var type = component.GetType();
				if (type.Namespace != null && type.Namespace.StartsWith("Arawn.CrystalSave.Runtime"))
				{
					DestroyImmediate(component, true);
					removed++;
				}
			}

			foreach (Transform child in root)
			{
				removed += RemoveCrystalSaveComponentsRecursive(child);
			}

			return removed;
		}

		private void AutoRegisterTags(bool isManual = false)
		{
			if (tagRegistry == null)
			{
				if (isManual)
				{
					EditorUtility.DisplayDialog("Tag Registry", "TagRegistry asset not found.", "OK");
				}
				return;
			}

			string[] allTags = InternalEditorUtility.tags;
			bool tagsChanged = false;

			if (tagRegistry.Tags.Count != allTags.Length)
			{
				tagRegistry.Tags.Clear();
				tagRegistry.Tags.AddRange(allTags);
				tagsChanged = true;
			}
			else
			{
				for (int i = 0; i < allTags.Length; i++)
				{
					if (tagRegistry.Tags[i] != allTags[i])
					{
						tagRegistry.Tags.Clear();
						tagRegistry.Tags.AddRange(allTags);
						tagsChanged = true;
						break;
					}
				}
			}

			if (!tagsChanged)
			{
				if (isManual)
				{
					EditorUtility.DisplayDialog("Tag Registry", "Project tags are already up to date.", "OK");
				}
				return;
			}

			EditorUtility.SetDirty(tagRegistry);
			AssetDatabase.SaveAssets();

			if (isManual)
			{
				EditorUtility.DisplayDialog("Tag Registry", "Tag Registry updated from Project Settings.", "OK");
			}
		}

		private void OnAfterAssemblyReload()
		{
			double currentTime = EditorApplication.timeSinceStartup;
			if (currentTime - lastTagRegistrationTime < registrationCooldown)
				return;

			lastTagRegistrationTime = currentTime;

			if (saveSettings.autoRegisterTags)
			{
				AutoRegisterTags();
			}
		}

		private void OnProjectChanged()
		{
			double currentTime = EditorApplication.timeSinceStartup;
			if (currentTime - lastTagRegistrationTime < registrationCooldown)
				return;

			lastTagRegistrationTime = currentTime;

			if (saveSettings.autoRegisterTags)
			{
				AutoRegisterTags();
			}
		}
	}
}
#endif
#endif
