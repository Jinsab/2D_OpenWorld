#if MEMORYPACK && ARAWN_REMEMBERME
using System;

namespace Arawn.CrystalSave.Runtime
{
    public interface IUserAuthorizationResolver : IUserFolderResolver
    {
        string ResolveAccessKey();        // Inject a custom token
    }

    [Obsolete("Use IUserAuthorizationResolver instead.")]
    public interface ISupabaseAuthorizationResolver : IUserAuthorizationResolver { }
}

#endif
