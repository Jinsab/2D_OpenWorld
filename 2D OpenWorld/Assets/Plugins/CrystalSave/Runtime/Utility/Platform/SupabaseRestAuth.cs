#if ARAWN_REMEMBERME && MEMORYPACK
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Arawn.CrystalSave.Runtime
{
    public static class SupabaseRestAuth
    {
        const string JSON = "application/json";

        /* ───── DTOs ───── */
        [Serializable] struct UserJson   { public string id; }          // auth.uid()
        [Serializable] struct SessionJson
        {
            public string    access_token;
            public int       expires_in;
            public string    refresh_token;
            public string    token_type;
            public UserJson  user;          // default(id=null) if backend omits user object
        }
        
        /* ─────────────────────────────────────────────────────────────── */
        /*  Sign-up (create a new user)                                    */
        /*  - returns (jwt, userId, needsConfirmation)                     */
        /* ─────────────────────────────────────────────────────────────── */
        public static IEnumerator SignUpWithEmailPassword(
            string projectUrl, string anonKey,
            string email, string password,
            Action<string, string, bool> onSuccess,    // jwt, userId, needsConfirm
            Action<string>               onError)
        {
            string url  = $"{projectUrl}/auth/v1/signup";
            string body = $"{{\"email\":\"{email}\",\"password\":\"{password}\"}}";

            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            req.SetRequestHeader("apikey", anonKey);
            req.SetRequestHeader("Content-Type", JSON);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var session = JsonUtility.FromJson<SessionJson>(req.downloadHandler.text);

                string jwt  = session.access_token;          // may be empty
                string uid  = !string.IsNullOrEmpty(session.user.id)
                                ? session.user.id
                                : ExtractSubFromJwt(jwt);     // fallback
                bool needsConfirm = string.IsNullOrEmpty(jwt);

                onSuccess?.Invoke(jwt, uid, needsConfirm);
            }
            else
            {
                Debug.LogError(
                    $"Supabase Sign-up {req.responseCode}: {req.downloadHandler.text}");
                onError?.Invoke(req.error);
            }
        }


        /* ─────────────────────────────── sign-in with e-mail + password */
        public static IEnumerator SignInWithPassword(
            string projectUrl, string anonKey,
            string email, string password,
            Action<string, string> onSuccess,   // jwt, userId
            Action<string> onError)
        {
            string url = $"{projectUrl}/auth/v1/token?grant_type=password";
            string body = $"{{\"email\":\"{email}\",\"password\":\"{password}\"}}";

            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            req.SetRequestHeader("apikey", anonKey);
            req.SetRequestHeader("Content-Type", JSON);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var session = JsonUtility.FromJson<SessionJson>(req.downloadHandler.text);
                string jwt = session.access_token;
                string uid = !string.IsNullOrEmpty(session.user.id)
                                ? session.user.id
                                : ExtractSubFromJwt(jwt);   // fallback
                onSuccess?.Invoke(jwt, uid);
            }
            else
            {
                Debug.LogError(
                    $"Supabase Auth {req.responseCode}: {req.downloadHandler.text}");
                onError?.Invoke(req.error);
            }
        }


        /* legacy overload (JWT only) */
        public static IEnumerator SignInWithPassword(
            string projectUrl, string anonKey,
            string email, string password,
            Action<string> onSuccess, Action<string> onError)
        {
            return SignInWithPassword(
                projectUrl, anonKey, email, password,
                (jwt, _uid) => onSuccess?.Invoke(jwt),
                onError);
        }

        /* ─────────────────────────────── send magic-link (OTP) */
        public static IEnumerator SendMagicLink(
            string projectUrl, string anonKey,
            string email,
            Action onOk, Action<string> onError,
            string redirectTo = null)
        {
            string url = $"{projectUrl}/auth/v1/otp";
            var sb = new StringBuilder()
                .Append("{\"email\":\"").Append(email).Append("\",")
                .Append("\"create_user\":true,")
                .Append("\"type\":\"magiclink\"");
            if (!string.IsNullOrEmpty(redirectTo))
                sb.Append(",\"redirect_to\":\"").Append(redirectTo).Append('"');
            sb.Append('}');

            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(sb.ToString())),
                downloadHandler = new DownloadHandlerBuffer()
            };
            req.SetRequestHeader("apikey", anonKey);
            req.SetRequestHeader("Content-Type", JSON);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                onOk?.Invoke();
            else
                onError?.Invoke(req.error);
        }

        /* ─────────────────────────────── helper: extract "sub" from JWT */
        static string ExtractSubFromJwt(string jwt)
        {
            if (string.IsNullOrEmpty(jwt)) return null;
            var parts = jwt.Split('.');
            if (parts.Length < 2) return null;

            string payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "=";  break;
            }

            try
            {
                string json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                int idx = json.IndexOf("\"sub\":\"", StringComparison.Ordinal);
                if (idx < 0) return null;
                idx += 7;
                int end = json.IndexOf('"', idx);
                return end > idx ? json.Substring(idx, end - idx) : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
#endif
