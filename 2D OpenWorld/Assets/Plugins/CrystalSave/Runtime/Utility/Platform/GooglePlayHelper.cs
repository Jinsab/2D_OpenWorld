#if REMEMBERME_GOOGLEPLAY_PRESENT && UNITY_ANDROID
using System.Threading.Tasks;
using GooglePlayGames;
using GooglePlayGames.BasicApi;

namespace Arawn.CrystalSave.Runtime
{
	internal static class GooglePlayHelper
	{
		public static async Task<string> GetServerAuthCodeAsync()
		{
			// 1. Make sure the plugin is activated once on app start.
			PlayGamesPlatform.Activate();

			// 2. Sign-in (auto-silent + user prompt if needed).
			var signInTcs = new TaskCompletionSource<SignInStatus>();
			PlayGamesPlatform.Instance.Authenticate(status => signInTcs.SetResult(status));

			var result = await signInTcs.Task;
			if (result != SignInStatus.Success)
				throw new System.Exception($"GPG sign-in failed: {result}");

			// 3. Ask Google for a server-side auth-code.
			var codeTcs = new TaskCompletionSource<string>();
			PlayGamesPlatform.Instance.RequestServerSideAccess(false,
				code => codeTcs.SetResult(code));

			return await codeTcs.Task;   // may be empty if the console isn�t configured correctly
		}
	}
}
#endif
