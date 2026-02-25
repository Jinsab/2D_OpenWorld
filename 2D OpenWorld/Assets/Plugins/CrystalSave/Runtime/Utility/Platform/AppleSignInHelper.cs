#if REMEMBERME_APPLE_SIGNIN_PRESENT
using System.Threading.Tasks;
using UnityEngine;
using AppleAuth;
using AppleAuth.Enums;
using AppleAuth.Interfaces;
using AppleAuth.Native;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>Utility for obtaining an Apple identity-token (JWT).</summary>
    internal static class AppleSignInHelper
    {
        /// <summary>Returns the JWT used by <c>LinkWithAppleAsync</c>.</summary>
        public static Task<string> GetJwtAsync()
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();

            if (!AppleAuthManager.IsCurrentPlatformSupported)
            {
                Debug.LogWarning("Apple Sign-In is not supported on this platform.");
                tcs.SetResult(null);
                return tcs.Task;
            }

            var payload = new AppleAuthLoginArgs(LoginOptions.IncludeEmail);
            IAppleAuthManager mgr = new AppleAuthManager(new PayloadDeserializer());

            mgr.LoginWithAppleId(payload,
                credential =>
                {
                    if (credential is IAppleIDCredential appleCred)
                        tcs.SetResult(System.Text.Encoding.UTF8.GetString(
                                        appleCred.IdentityToken));
                    else
                        tcs.SetResult(null);
                },
                error =>
                {
                    Debug.LogError($"Apple login failed: {error.LocalizedDescription}");
                    tcs.SetResult(null);
                });

            return tcs.Task;
        }
    }
}
#endif
