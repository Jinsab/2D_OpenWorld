#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    [CreateAssetMenu(
        fileName = "CrystalSaveAssetOverrides",
        menuName = "Crystal Save/Settings/Crystal Save Asset Overrides",
        order = 804)]
    public class CrystalSaveAssetOverrides : ScriptableObject
    {
        [Tooltip("Optional direct reference to SaveSettings. If assigned, Crystal Save uses this instead of Resources/Addressables lookup.")]
        public SaveSettings saveSettings;

        [Tooltip("Optional direct reference to PrefabRegistry. If assigned, Crystal Save uses this instead of Resources/Addressables lookup.")]
        public PrefabRegistry prefabRegistry;

        [Tooltip("Optional direct reference to TagRegistry. If assigned, Crystal Save uses this instead of Resources/Addressables lookup.")]
        public TagRegistry tagRegistry;

        [Tooltip("Optional direct reference to SceneObjectRegistry. If assigned, Crystal Save uses this instead of Resources/Addressables lookup.")]
        public SceneObjectRegistry sceneObjectRegistry;

        [Tooltip("Optional direct reference to MigrationManager. If assigned, Crystal Save uses this instead of Resources/Addressables lookup.")]
        public MigrationManager migrationManager;

        [Tooltip("Optional direct reference to LoggerConfig. If assigned, Crystal Save uses this instead of Resources lookup.")]
        public LoggerConfig loggerConfig;

        [Tooltip("Optional direct reference to SaveSlotMetadata. If assigned, Crystal Save uses this instead of Resources/Addressables lookup.")]
        public SaveSlotMetadataSO saveSlotMetadata;
    }
}
#endif
