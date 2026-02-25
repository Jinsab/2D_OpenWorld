#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// One-way callback: when the child is destroyed, tell the parent SaveablePrefab.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    internal sealed class DestroyObserver : MonoBehaviour
    {
        internal SaveablePrefab Owner;
        internal string         LocalPath;      // e.g. "Arm/Hand/Finger"

        void OnDestroy()            // called just before the child disappears
        {
            if (Owner) Owner.NotifyChildDestroyed(LocalPath);
        }
    }
}
#endif