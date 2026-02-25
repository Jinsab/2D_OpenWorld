#if ARAWN_REMEMBERME && MEMORYPACK
namespace Arawn.CrystalSave.Runtime
{
    public static class SupabaseAuthRelay
    {
        public delegate void LoginEvent(string userId);
        public delegate void LoginErrorEvent(string error);

        public static event LoginEvent LoggedIn;      // jwt + uid obtained
        public static event System.Action LoggedOut;     // you cleared creds
        public static event LoginErrorEvent LoginFailed;   // wrong pwd, etc.

        /* internal helpers ------------------------------------------------ */
        internal static void FireLoggedIn(string uid) => LoggedIn?.Invoke(uid);
        internal static void FireLoggedOut() => LoggedOut?.Invoke();
        internal static void FireLoginFailed(string error) => LoginFailed?.Invoke(error);
    }
}
#endif