#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    public static class CrystalSaveOverrides
    {
        static CrystalSaveAssetOverrides cached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            cached = null;
        }

        public static CrystalSaveAssetOverrides Current
        {
            get
            {
                if (cached != null) return cached;
                var found = Resources.FindObjectsOfTypeAll<CrystalSaveAssetOverrides>();
                if (found != null && found.Length > 0)
                {
                    cached = found[0];
                }
                return cached;
            }
        }

        public static T GetOverride<T>() where T : Object
        {
            var current = Current;
            if (current == null) return null;

            if (typeof(T) == typeof(SaveSettings)) return current.saveSettings as T;
            if (typeof(T) == typeof(PrefabRegistry)) return current.prefabRegistry as T;
            if (typeof(T) == typeof(TagRegistry)) return current.tagRegistry as T;
            if (typeof(T) == typeof(SceneObjectRegistry)) return current.sceneObjectRegistry as T;
            if (typeof(T) == typeof(MigrationManager)) return current.migrationManager as T;
            if (typeof(T) == typeof(LoggerConfig)) return current.loggerConfig as T;
            if (typeof(T) == typeof(SaveSlotMetadataSO)) return current.saveSlotMetadata as T;

            return null;
        }
    }
}
#endif
