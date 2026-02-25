#if ARAWN_REMEMBERME && MEMORYPACK
using UnityEngine;
using UnityEngine.UI;
using Arawn.CrystalSave.Runtime;
using System.Threading.Tasks;

namespace Arawn.CrystalSave.Demo
{
    /// <summary>
    /// UI panel for Supabase e-mail auth:
    /// • Register  → OnRegister()
    /// • Sign-in   → OnSubmit() (with password)
    /// • Magic-link→ OnSubmit() (no password)
    ///
    /// Emits SupabaseAuthRelay:
    ///   LoggedIn(uid)      – after JWT + uid obtained
    ///   LoginFailed(err)   – wrong credentials / network error
    /// </summary>
    public class EmailAuthPanel : MonoBehaviour
    {
        /* ── cached settings ───────────────────────── */
        SaveSettings settings;

        /* ── UI refs ───────────────────────────────── */
        [Header("UI")]
        [SerializeField] InputField email;
        [SerializeField] InputField password;      // leave empty ⇒ magic-link
        [SerializeField] Text infoLabel;

        /* ── overrides (optional) ─────────────────── */
        [Header("Override (leave empty to read SaveSettings)")]
        [SerializeField] string supabaseUrl;
        [SerializeField] string anonKey;
        [SerializeField] CustomFolderAuthResolver resolver;

        /* ---------------------------------------------------------------- */
        void Awake()
        {
            settings = AssetProvider.Load<SaveSettings>("SaveSettings");

            if (string.IsNullOrWhiteSpace(supabaseUrl) && settings != null)
                supabaseUrl = settings.supabaseUrl;
            if (string.IsNullOrWhiteSpace(anonKey) && settings != null)
                anonKey = settings.supabaseAnonKey;

            if (resolver == null && settings != null &&
                settings.customUserFolderResolver is CustomFolderAuthResolver r)
                resolver = r;

            if (resolver == null)
                Debug.LogWarning("EmailAuthPanel: no resolver assigned and none found in SaveSettings.");

            if (password != null)
                password.onValueChanged.AddListener(_ => ShowHint());

            ShowHint();
        }

        /* ---------------------------------------------------------------- */
        void ShowHint()
        {
            if (!infoLabel) return;

            bool hasPwd = !string.IsNullOrWhiteSpace(password.text);
            infoLabel.text = hasPwd
                ? "Enter e-mail & password: press Send to sign in, or Register to create a new account."
                : "Leave password empty: press Send to receive a one-time magic-link.";
        }

        void ShowStatus(string msg) => infoLabel.text = msg;

        /* =================================================================
         *  REGISTER BUTTON
         * ===============================================================*/
        public void OnRegister()
        {
            if (string.IsNullOrWhiteSpace(email.text) ||
                string.IsNullOrWhiteSpace(password.text))
            {
                ShowStatus("Enter e-mail and a password first.");
                return;
            }

            ShowStatus("Creating account…");

            StartCoroutine(SupabaseRestAuth.SignUpWithEmailPassword(
                supabaseUrl, anonKey,
                email.text, password.text,
                (jwt, uid, needsConfirm) =>
                {
                    resolver?.SetUserId(uid);   // folder known immediately

                    if (needsConfirm)
                    {
                        ShowStatus("Verify your e-mail, then press Send to sign in.");
                    }
                    else
                    {
                        resolver?.SetToken(jwt);
                        ShowStatus("Account created & signed in!");
                        SupabaseAuthRelay.FireLoggedIn(uid);
                    }
                },
                err =>
                {
                    ShowStatus($"Sign-up error: {err}");
                    Debug.LogError(err);
                    SupabaseAuthRelay.FireLoginFailed(err);
                    ShowHint();
                }));
        }

        /* =================================================================
         *  SINGLE SEND BUTTON
         * ===============================================================*/
        public void OnSubmit()
        {
           bool hasPwd = !string.IsNullOrWhiteSpace(password.text);
            if (!hasPwd)
            {
                ShowStatus("Sending magic-link…");
                StartCoroutine(SupabaseRestAuth.SendMagicLink(
                    supabaseUrl, anonKey, email.text,
                    ()   => ShowStatus("Magic-link sent – check your inbox."),
                    err => { /* … */ }));
            }
            else
            {
                ShowStatus("Signing in…");
                StartCoroutine(SupabaseRestAuth.SignInWithPassword(
                    supabaseUrl, anonKey,
                    email.text, password.text,
                    (jwt, uid) =>
                    {
                        resolver?.SetToken(jwt);
                        resolver?.SetUserId(uid);
                        ShowStatus("Signed in!");
                        SupabaseAuthRelay.FireLoggedIn(uid);
                    },
                    err => { /* … */ }));
            }
        }
        
        public void OnLogout()
        {
            ShowStatus("Logging out…");
            SaveManager.Instance.LogoutFromSupabase();
            ShowStatus("Logged out.");
        }
    }
}
#endif
