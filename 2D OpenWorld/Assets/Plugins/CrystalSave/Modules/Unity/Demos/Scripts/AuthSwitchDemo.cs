using UnityEngine;
#if REMEMBERME_CORESERVICES_PRESENT && REMEMBERME_AUTHENTICATION_PRESENT
using UnityEngine.UI;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;
using System.Linq;
using System.Threading.Tasks;
#endif
#if REMEMBERME_GOOGLEPLAY_PRESENT
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif
#if REMEMBERME_STEAMWORKS_PRESENT
using Steamworks;
#endif

namespace Arawn.CrystalSave.Runtime.Demo
{
    /// <summary>
    /// Runtime-built micro UI that lets you switch authentication providers.
    /// Attach it to any empty GameObject in a scene.
    /// </summary>
    public sealed class AuthSwitchDemo : MonoBehaviour 
    {
#if REMEMBERME_CORESERVICES_PRESENT && REMEMBERME_AUTHENTICATION_PRESENT
        Canvas _canvas;
        Text _status;

        /* ───────────────────────────── Initialise ───────────────────────────── */
        async void Start()
        {
            await UnityServices.InitializeAsync();
            BuildUI();
            RefreshStatus();
        }

        /* ───────────────────────────── UI helpers ───────────────────────────── */
        void BuildUI()
        {
            _canvas = new GameObject(nameof(AuthSwitchDemo)).AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.gameObject.AddComponent<CanvasScaler>();
            _canvas.gameObject.AddComponent<GraphicRaycaster>();

            var panel = New("Panel", _canvas.transform, typeof(Image))
                        .GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
            panel.sizeDelta = new Vector2(340, 360);

            _status = New("Status", panel, typeof(Text)).GetComponent<Text>();
            _status.rectTransform.sizeDelta = new Vector2(310, 70);
            _status.rectTransform.anchoredPosition = new Vector2(0, 130);
            _status.font = RuntimeFont();
            _status.fontSize = 14;
            _status.alignment = TextAnchor.MiddleCenter;

            float y = 50;
            ButtonRow("Anonymous", y, SignInAnonymous);
            ButtonRow("Unity", y - 40, SignInUnity);
#if REMEMBERME_GOOGLEPLAY_PRESENT
            ButtonRow("Google Play", y - 80, SignInGooglePlay);
#endif
#if REMEMBERME_STEAMWORKS_PRESENT
            ButtonRow("Steam", y - 120, SignInSteam);
#endif
#if REMEMBERME_FACEBOOK_SDK_PRESENT
            ButtonRow("Facebook", y - 160, SignInFacebook);
#endif
#if REMEMBERME_APPLE_SIGNIN_PRESENT
            ButtonRow("Apple", y - 200, SignInApple);
#endif
        }

        GameObject New(string n, Transform p, params System.Type[] comps)
        {
            var go = new GameObject(n, comps);
            go.transform.SetParent(p, false);
            return go;
        }

        void ButtonRow(string label, float yPos, UnityEngine.Events.UnityAction onClick)
        {
            var btn = New(label + "Btn", _status.transform.parent, typeof(Image), typeof(Button));
            var rt  = btn.GetComponent<RectTransform>();
            rt.sizeDelta        = new Vector2(280, 34);
            rt.anchoredPosition = new Vector2(0, yPos);

            var txt = New("Txt", btn.transform, typeof(Text)).GetComponent<Text>();
            txt.text            = label;
            txt.alignment       = TextAnchor.MiddleCenter;
            txt.font            = RuntimeFont();
            txt.color           = Color.black;
            txt.rectTransform.sizeDelta = rt.sizeDelta;

            btn.GetComponent<Button>().onClick.AddListener(onClick);
        }

        static Font RuntimeFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f) return f;
            return Font.CreateDynamicFontFromOSFont(
                Font.GetOSInstalledFontNames().First(), 14);
        }

        /* ─────────────────────── Provider flows (each async) ────────────────── */
        async void SignInAnonymous()
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            RefreshStatus();
        }

        async void SignInUnity()
        {
            if (PlayerAccountService.Instance.IsSignedIn)
            {
                await SignInWithUnityAuth();
                RefreshStatus();
                return;
            }

            PlayerAccountService.Instance.SignedIn -= OnPlayerAccountSignedIn;
            PlayerAccountService.Instance.SignedIn += OnPlayerAccountSignedIn;
            await PlayerAccountService.Instance.StartSignInAsync();
        }

        async void OnPlayerAccountSignedIn()
        {
            PlayerAccountService.Instance.SignedIn -= OnPlayerAccountSignedIn;
            await SignInWithUnityAuth();
            RefreshStatus();
        }

        Task SignInWithUnityAuth() =>
            AuthenticationService.Instance.SignInWithUnityAsync(
                PlayerAccountService.Instance.AccessToken,
                new SignInOptions { CreateAccount = true });

#if REMEMBERME_GOOGLEPLAY_PRESENT
        async void SignInGooglePlay()
        {
            string code = await GooglePlayHelper.GetServerAuthCodeAsync();
            await AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(
                code, new SignInOptions { CreateAccount = true });
            RefreshStatus();
        }
#endif

#if REMEMBERME_STEAMWORKS_PRESENT
        async void SignInSteam()
        {
            if (!SteamManager.Initialized)
            {
                Debug.LogWarning("Steam not running.");
                return;
            }

            byte[] buf = new byte[1024];
            uint   len;

            // Create and pass identity to match the 4-arg signature
            SteamNetworkingIdentity identity = new SteamNetworkingIdentity();
            HAuthTicket ticket = SteamUser.GetAuthSessionTicket(
                buf,
                buf.Length,
                out len,
                ref identity
            );

            if (ticket == HAuthTicket.Invalid || len == 0)
                return;

            string b64 = System.Convert.ToBase64String(buf, 0, (int)len);

            await AuthenticationService.Instance.SignInWithSteamAsync(
                b64,
                "steam",
                new SignInOptions { CreateAccount = true }
            );

            // Always cancel when done
            SteamUser.CancelAuthTicket(ticket);
            RefreshStatus();
        }
#endif

#if REMEMBERME_FACEBOOK_SDK_PRESENT
        async void SignInFacebook()
        {
            string token = await FacebookSDK.GetAccessTokenAsync();
            await AuthenticationService.Instance.SignInWithFacebookAsync(
                token, new SignInOptions { CreateAccount = true });
            RefreshStatus();
        }
#endif

#if REMEMBERME_APPLE_SIGNIN_PRESENT
        async void SignInApple()
        {
            string jwt = await AppleSignInHelper.GetJwtAsync();
            await AuthenticationService.Instance.SignInWithAppleAsync(
                jwt, new SignInOptions { CreateAccount = true });
            RefreshStatus();
        }
#endif

        /* ────────────────────────── status text helper ─────────────────────── */
        void RefreshStatus()
        {
            string provider = "None";
            if (AuthenticationService.Instance.IsSignedIn)
            {
                var ids = AuthenticationService.Instance.PlayerInfo?.Identities;
                if (ids != null && ids.Count > 0)
                    provider = string.Join(",", ids.Select(i => i.TypeId));
            }

            string pid = AuthenticationService.Instance.IsSignedIn
                ? AuthenticationService.Instance.PlayerId
                : "-";

            _status.text =
                $"Signed In: <b>{AuthenticationService.Instance.IsSignedIn}</b>\n" +
                $"Provider:  <b>{provider}</b>\n" +
                $"PlayerId:  <b>{pid}</b>";
        }
#endif
    }
}
