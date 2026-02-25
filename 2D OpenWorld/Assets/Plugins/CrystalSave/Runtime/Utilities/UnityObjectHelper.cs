#if ARAWN_REMEMBERME && MEMORYPACK
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Helper class for Unity Object operations that provides compatibility across Unity versions.
    /// Handles deprecated API changes like GetInstanceID() -> GetEntityId() in Unity 6+.
    /// </summary>
    public static class UnityObjectHelper
    {
        /// <summary>
        /// Gets a unique identifier for a Unity Object.
        /// Uses GetEntityId() for Unity 6+ and GetInstanceID() for older versions.
        /// </summary>
        /// <param name="obj">The Unity Object to get the ID from.</param>
        /// <returns>A unique integer identifier for the object.</returns>
        public static int GetUniqueId(Object obj)
        {
            if (obj == null)
                return 0;

#if UNITY_6000_3_OR_NEWER
            // In Unity 6.3+, use GetEntityId().GetHashCode() for stable integer IDs
            return obj.GetEntityId().GetHashCode();
#else
            return obj.GetInstanceID();
#endif
        }

        /// <summary>
        /// Gets a unique identifier for a Unity Object with null safety.
        /// Returns 0 if the object is null or destroyed.
        /// </summary>
        /// <param name="obj">The Unity Object to get the ID from.</param>
        /// <returns>A unique integer identifier for the object, or 0 if null.</returns>
        public static int GetUniqueIdSafe(Object obj)
        {
            if (obj == null || !obj)
                return 0;

            return GetUniqueId(obj);
        }
    }
}
#endif