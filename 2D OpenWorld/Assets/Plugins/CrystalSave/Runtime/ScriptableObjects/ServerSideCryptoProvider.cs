#if ARAWN_REMEMBERME && MEMORYPACK
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
using Unity.Services.Authentication;
#endif

namespace Arawn.CrystalSave.Runtime
{
    [HelpURL("https://arawn-software-publishing.gitbook.io/arawn/basics/encryption")]
    [CreateAssetMenu(
        fileName = "ServerSideCryptoProvider",
        menuName = "Crystal Save/Settings/Security/Server-Side Crypto Provider",
        order = 803)]
    public sealed class ServerSideCryptoProvider : ScriptableObject, ICloudCryptoProvider
    {
        public enum ResponseFormat { Base64Text, JsonWithBase64Field }

        [Tooltip("HTTPS endpoint that accepts plaintext base64 and returns encrypted base64. This is an API endpoint (do not put the key in the URL).")]
        public string encryptUrl = "https://your-server.example.com/crystalsave/encrypt";

        [Tooltip("HTTPS endpoint that accepts encrypted base64 and returns plaintext base64. This is an API endpoint (do not put the key in the URL).")]
        public string decryptUrl = "https://your-server.example.com/crystalsave/decrypt";

        [Tooltip("Include Unity Authentication access token in the Authorization header (Bearer). Requires Unity Authentication and a signed-in player.")]
        public bool includeUnityAuthToken = true;

        [Tooltip("HTTP header name used for the auth token.")]
        public string authHeaderName = "Authorization";

        [Tooltip("Prefix for the auth header value (e.g., 'Bearer ').")]
        public string authHeaderPrefix = "Bearer ";

        [Tooltip("Include the resolved user id as a header (PlayerId if signed in, else per-install GUID).")]
        public bool includeUserIdHeader = true;

        public string userIdHeaderName = "X-CrystalSave-UserId";

        [Tooltip("Send the user id in the JSON body.")]
        public bool includeUserIdInBody = true;

        public ResponseFormat responseFormat = ResponseFormat.Base64Text;

        [Tooltip("JSON field name that contains the base64 payload when using JSON response format.")]
        public string jsonBase64Field = "base64";

        [Tooltip("Allow plain HTTP endpoints (not recommended).")]
        public bool allowInsecureHttp = false;

        [Serializable]
        private class CryptoPayload
        {
            public string base64;
            public string userId;
        }

        [Serializable]
        private class Base64Response
        {
            public string base64;
        }

        public async ValueTask<byte[]> EncryptForCloudAsync(byte[] plain)
        {
            if (plain == null || plain.Length == 0)
                return null;

            string userId = (includeUserIdHeader || includeUserIdInBody)
                ? SaveManager.ResolveUserIdentifier()
                : null;

            byte[] result = await SendCryptoRequestAsync(encryptUrl, plain, userId).ConfigureAwait(false);
            return result;
        }

        public async ValueTask<byte[]> DecryptFromCloudAsync(byte[] blob)
        {
            if (blob == null || blob.Length == 0)
                return null;

            string userId = (includeUserIdHeader || includeUserIdInBody)
                ? SaveManager.ResolveUserIdentifier()
                : null;

            byte[] result = await SendCryptoRequestAsync(decryptUrl, blob, userId).ConfigureAwait(false);
            return result;
        }

        private async Task<byte[]> SendCryptoRequestAsync(string url, byte[] payload, string userId)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                Logger.Log("ServerSideCryptoProvider: endpoint URL is empty.", LogCategory.Cryptography, LogLevel.Warning);
                return null;
            }

            if (!allowInsecureHttp && url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Log("ServerSideCryptoProvider: HTTP is disabled. Use HTTPS or enable Allow Insecure Http.", LogCategory.Cryptography, LogLevel.Warning);
                return null;
            }

            string base64 = Convert.ToBase64String(payload);
            var body = new CryptoPayload { base64 = base64, userId = includeUserIdInBody ? userId : null };
            string json = JsonUtility.ToJson(body);
            byte[] bodyBytes = Encoding.UTF8.GetBytes(json);

            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            if (includeUserIdHeader && !string.IsNullOrEmpty(userId))
                request.SetRequestHeader(userIdHeaderName, userId);

            if (includeUnityAuthToken)
            {
                string token = TryGetUnityAuthToken();
                if (!string.IsNullOrEmpty(token))
                    request.SetRequestHeader(authHeaderName, authHeaderPrefix + token);
            }

            await SendRequestAsync(request).ConfigureAwait(false);

#if UNITY_2020_2_OR_NEWER
            bool requestFailed = request.result != UnityWebRequest.Result.Success;
#else
            bool requestFailed = request.isNetworkError || request.isHttpError;
#endif
            if (requestFailed)
            {
                Logger.Log($"ServerSideCryptoProvider: request failed ({request.responseCode}) {request.error}", LogCategory.Cryptography, LogLevel.Warning);
                return null;
            }

            string response = request.downloadHandler != null ? request.downloadHandler.text : null;
            if (string.IsNullOrWhiteSpace(response))
            {
                Logger.Log("ServerSideCryptoProvider: empty response body.", LogCategory.Cryptography, LogLevel.Warning);
                return null;
            }

            string responseBase64 = ExtractBase64(response);
            if (string.IsNullOrWhiteSpace(responseBase64))
            {
                Logger.Log("ServerSideCryptoProvider: response did not contain a base64 payload.", LogCategory.Cryptography, LogLevel.Warning);
                return null;
            }

            try
            {
                return Convert.FromBase64String(responseBase64.Trim());
            }
            catch (Exception ex)
            {
                Logger.Log($"ServerSideCryptoProvider: invalid base64 response ({ex.Message}).", LogCategory.Cryptography, LogLevel.Warning);
                return null;
            }
        }

        private static Task SendRequestAsync(UnityWebRequest request)
        {
            var tcs = new TaskCompletionSource<bool>();
            var op = request.SendWebRequest();
            op.completed += _ => tcs.TrySetResult(true);
            return tcs.Task;
        }

        private string ExtractBase64(string payload)
        {
            if (responseFormat == ResponseFormat.Base64Text)
                return payload.Trim();

            string trimmed = payload.Trim();
            if (string.Equals(jsonBase64Field, "base64", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var resp = JsonUtility.FromJson<Base64Response>(trimmed);
                    return resp != null ? resp.base64 : null;
                }
                catch
                {
                    return null;
                }
            }

            string token = "\"" + jsonBase64Field + "\"";
            int keyIndex = trimmed.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (keyIndex < 0) return null;
            int colonIndex = trimmed.IndexOf(':', keyIndex + token.Length);
            if (colonIndex < 0) return null;
            int valueStart = colonIndex + 1;
            while (valueStart < trimmed.Length && char.IsWhiteSpace(trimmed[valueStart])) valueStart++;
            if (valueStart >= trimmed.Length || trimmed[valueStart] != '"') return null;
            int valueEnd = trimmed.IndexOf('"', valueStart + 1);
            if (valueEnd < 0) return null;
            return trimmed.Substring(valueStart + 1, valueEnd - valueStart - 1);
        }

        private string TryGetUnityAuthToken()
        {
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
            var auth = AuthenticationService.Instance;
            if (auth != null && auth.IsSignedIn)
            {
                var type = auth.GetType();
                var prop = type.GetProperty("AccessToken");
                if (prop != null)
                    return prop.GetValue(auth) as string;

                var method = type.GetMethod("GetAccessToken", Type.EmptyTypes);
                if (method != null)
                    return method.Invoke(auth, null) as string;

                Logger.Log("ServerSideCryptoProvider: Unity Authentication token API not found; request will be unauthenticated.", LogCategory.Cryptography, LogLevel.Info);
                return null;
            }

            Logger.Log("ServerSideCryptoProvider: Unity Authentication not signed in; request will be unauthenticated.", LogCategory.Cryptography, LogLevel.Info);
            return null;
#else
            Logger.Log("ServerSideCryptoProvider: Unity Authentication package not present; request will be unauthenticated.", LogCategory.Cryptography, LogLevel.Info);
            return null;
#endif
        }
    }
}
#endif
