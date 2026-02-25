#if ARAWN_REMEMBERME && MEMORYPACK
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Custom folder + auth token resolver (runtime-persistent).
    /// </summary>
    [CreateAssetMenu(
        fileName = "CustomFolderAuthResolver",
        menuName = "Crystal Save/Settings/Custom Folder & Auth Resolver")]
    public class CustomFolderAuthResolver :
        ScriptableObject, IUserAuthorizationResolver
    {
        /* default placeholders shown only in Inspector */
        //[HideInInspector]
        [SerializeField] string userId    = "guest";
        //[HideInInspector]
        [SerializeField] string accessKey = "";

        /* PlayerPrefs keys (swap for secure store if needed) */
        const string PP_UID = "cs_uid";
        const string PP_JWT = "cs_jwt";

        /* ------------------------------------------------------------ */
        void OnEnable()
        {
            /* Load cached creds on every domain reload / game start.
             * In the Editor this runs for edit-time objects too, so we
             * guard with `Application.isPlaying` to avoid dirtying the
             * asset outside Play-Mode.                                */
            if (Application.isPlaying || !Application.isEditor)
            {
                userId    = PlayerPrefs.GetString(PP_UID, "guest");
                accessKey = PlayerPrefs.GetString(PP_JWT, "");

                // When credentials are already stored (e.g. returning
                // player), notify the SaveManager so cloud operations can
                // proceed without requiring an explicit login step.
                if (Application.isPlaying && !string.IsNullOrEmpty(accessKey))
                {
                    SupabaseAuthRelay.FireLoggedIn(userId);
                }
            }
        }

#if UNITY_EDITOR
        /* Keep the on-disk asset “clean” when you exit Play-Mode. */
        void OnValidate()
        {
            if (!Application.isPlaying)
            {
                userId    = "guest";
                accessKey = "";
            }
        }
#endif

        /* ------------------------------------------------------------ */
        public void SetUserId(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return;
            userId = uid;
            PlayerPrefs.SetString(PP_UID, uid);
            PlayerPrefs.Save();
        }

        public void SetToken(string jwt)
        {
            if (string.IsNullOrEmpty(jwt)) return;
            accessKey = jwt;
            PlayerPrefs.SetString(PP_JWT, jwt);
            PlayerPrefs.Save();
        }

        public void ClearCredentials()
        {
            // reset to guest/default
            userId    = "guest";
            accessKey = "";

            // remove from PlayerPrefs
            PlayerPrefs.DeleteKey("cs_uid");
            PlayerPrefs.DeleteKey("cs_jwt");
            PlayerPrefs.Save();
        }

        /* ---------- IUserAuthorizationResolver ---------- */
        public string ResolveUserFolder() => $"users/{userId}";
        public string ResolveAccessKey()  => accessKey;
    }
}
#endif
