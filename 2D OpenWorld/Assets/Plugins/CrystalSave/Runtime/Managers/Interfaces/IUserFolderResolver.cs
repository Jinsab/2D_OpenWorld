#if MEMORYPACK && ARAWN_REMEMBERME
namespace Arawn.CrystalSave.Runtime
{
    /// Implement this once in your game and point SaveSettings to it.
    /// Used by SupabaseSaveSystem, FirebaseSaveSystem, and future cloud backends.
    public interface IUserFolderResolver
    {
        /// Return the **user-relative** folder, e.g. "users/abc123".
        /// Do NOT add a trailing slash; the cloud save system will handle it.
        string ResolveUserFolder();
    }
}
#endif