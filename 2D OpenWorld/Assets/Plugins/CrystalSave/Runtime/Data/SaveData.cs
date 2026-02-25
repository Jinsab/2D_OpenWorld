#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using System.Collections.Generic;

namespace Arawn.CrystalSave.Runtime
{
	[MemoryPackable]
	[Serializable]
	public partial class SaveData
	{
		public VersionData Version { get; set; }
		public DateTime LastSaved { get; set; }
		public string ScreenshotFileName { get; set; }
		public string LastActiveScene { get; set; }
                public Dictionary<string, byte[]> ComponentsData { get; set; } = new Dictionary<string, byte[]>();
                public Dictionary<string, ComponentDataMetadata> ComponentMetadata { get; set; } = new Dictionary<string, ComponentDataMetadata>();
		public List<SaveablePrefabData> Prefabs { get; set; }
		public List<SaveDataEntry> SaveableComponents { get; set; }
		public List<GameObjectState> GameObjectStates { get; set; }
		public List<string> DestroyedGameObjects { get; set; }
		public List<string> TrackedUniqueIDs { get; set; } = new List<string>();
                public Dictionary<string, Dictionary<string, byte[]>> DestroyedObjectData
                { get; set; } = new Dictionary<string, Dictionary<string, byte[]>>();

		// Persisted cache of per-scene snapshots for scene-bound SaveableComponents that
		// Remember Home Scene. Shape:
		//   SceneName -> GameObjectUniqueID -> ComponentUniqueIdentifier -> bytes
                public Dictionary<string, Dictionary<string, Dictionary<string, byte[]>>> HomeSceneComponentSnapshots
                { get; set; } = new Dictionary<string, Dictionary<string, Dictionary<string, byte[]>>>();

                // Prefab references for Last Snapshot Scene restoration
                public Dictionary<string, Dictionary<string, string>> HomeScenePrefabAssetIDs
                { get; set; } = new Dictionary<string, Dictionary<string, string>>();

		// Parameterless constructor for deserialization.
		public SaveData()
			: this(
			  SaveManager.Instance?.SaveSettings?.version.Clone() as VersionData
			  ?? new VersionData(1, 0, 0))
		{
			LastSaved = DateTime.UtcNow;
			ScreenshotFileName = string.Empty;
			LastActiveScene = string.Empty;
                        Prefabs = new List<SaveablePrefabData>();
                        SaveableComponents = new List<SaveDataEntry>();
                        DestroyedGameObjects = new List<string>();
                        ComponentsData = new Dictionary<string, byte[]>();
                        ComponentMetadata = new Dictionary<string, ComponentDataMetadata>();
                        GameObjectStates = new List<GameObjectState>();
                        HomeSceneComponentSnapshots = new Dictionary<string, Dictionary<string, Dictionary<string, byte[]>>>();
                        HomeScenePrefabAssetIDs = new Dictionary<string, Dictionary<string, string>>();
                }

		// Constructor with version initialization
		public SaveData(VersionData version)
			: this(
			  version,
			  DateTime.UtcNow,
			  string.Empty,
			  string.Empty,
			  new List<SaveablePrefabData>(),
                          new List<SaveDataEntry>(),
                          new List<GameObjectState>(),
                          new List<string>(),
                          null,
                          null,
                          null
                        )
                {
                        // No additional logic here.
                }

		// MemoryPack constructor for deserialization
		[MemoryPackConstructor]
		public SaveData(
			VersionData version,
			DateTime lastSaved,
			string screenshotFileName,
			string lastActiveScene,
                        List<SaveablePrefabData> prefabs,
                        List<SaveDataEntry> saveableComponents,
                        List<GameObjectState> gameObjectStates,
                        List<string> destroyedGameObjects,
                        Dictionary<string, Dictionary<string, Dictionary<string, byte[]>>> homeSceneComponentSnapshots = null,
                        Dictionary<string, Dictionary<string, string>> homeScenePrefabAssetIDs = null,
                        Dictionary<string, ComponentDataMetadata> componentMetadata = null)
                {
                        Version = version;
                        LastSaved = lastSaved;
                        ScreenshotFileName = screenshotFileName;
                        LastActiveScene = lastActiveScene;
                        Prefabs = prefabs;
                        SaveableComponents = saveableComponents;
                        GameObjectStates = gameObjectStates;
                        DestroyedGameObjects = destroyedGameObjects;
                        ComponentsData = new Dictionary<string, byte[]>();
                        ComponentMetadata = componentMetadata ?? new Dictionary<string, ComponentDataMetadata>();
                        DestroyedObjectData = new Dictionary<string, Dictionary<string, byte[]>>();
                        HomeSceneComponentSnapshots = homeSceneComponentSnapshots ?? new Dictionary<string, Dictionary<string, Dictionary<string, byte[]>>>();
                        HomeScenePrefabAssetIDs = homeScenePrefabAssetIDs ?? new Dictionary<string, Dictionary<string, string>>();
                }
        }
}
#endif