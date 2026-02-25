#if REMEMBERME_FACEBOOK_SDK_PRESENT
using System.Threading.Tasks;
using Facebook.Unity;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>Utility that ensures FB SDK is initialised and logged-in.</summary>
    internal static class FacebookSDK
    {
        public static async Task<string> GetAccessTokenAsync()
        {
            if (!FB.IsInitialized)
            {
                var tcsInit = new System.Threading.Tasks.TaskCompletionSource<bool>();
                FB.Init(() => tcsInit.SetResult(true));
                await tcsInit.Task;
            }

            if (!FB.IsLoggedIn)
            {
                var tcsLogin = new System.Threading.Tasks.TaskCompletionSource<bool>();
                FB.LogInWithReadPermissions(new[] { "public_profile", "email" },
                    res => tcsLogin.SetResult(res != null && !res.Cancelled));
                await tcsLogin.Task;
            }

            return FB.IsLoggedIn
                ? AccessToken.CurrentAccessToken.TokenString
                : null;
        }
    }
}
#endif
