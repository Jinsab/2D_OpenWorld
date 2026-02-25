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
    [Obsolete("Deprecated: use Cloud Crypto Mode = ServerSide instead.")]
    public sealed class ServerMasterSecretProvider : ScriptableObject, IMasterSecretProvider
    {
        public enum HttpMethod { Get, Post }
        public enum ResponseFormat { Base64Text, JsonWithBase64Field }

        [Tooltip("HTTPS endpoint that returns a base64-encoded 32-byte secret. For JSON format, return {\"base64\":\"...\"}.")]
        public string endpointUrl = "https://your-server.example.com/crystalsave/master-secret";

        public HttpMethod method = HttpMethod.Get;

        [Tooltip("Include Unity Authentication access token in the Authorization header (Bearer). Requires Unity Authentication and a signed-in player.")]
        public bool includeUnityAuthToken = true;

        [Tooltip("HTTP header name used for the auth token.")]
        public string authHeaderName = "Authorization";

        [Tooltip("Prefix for the auth header value (e.g., 'Bearer ').")]
        public string authHeaderPrefix = "Bearer ";

        [Tooltip("Include the resolved user id as a header (PlayerId if signed in, else per-install GUID).")]
        public bool includeUserIdHeader = true;

        public string userIdHeaderName = "X-CrystalSave-UserId";

        [Tooltip("Send the user id in the JSON body when using POST.")]
        public bool includeUserIdInBody = true;

        public ResponseFormat responseFormat = ResponseFormat.Base64Text;

        [Tooltip("JSON field name that contains the base64 secret when using JSON response format.")]
        public string jsonBase64Field = "base64";

        [Tooltip("Allow plain HTTP endpoints (not recommended).")]
        public bool allowInsecureHttp = false;

        [Serializable]
        private class Base64Response
        {
            public string base64;
        }

        [Serializable]
        private class UserIdPayload
        {
            public string userId;
        }

        public async ValueTask<byte[]> GetMasterSecretAsync()
        {
            if (string.IsNullOrWhiteSpace(endpointUrl))
            {
                Logger.Log("ServerMasterSecretProvider: endpointUrl is empty.", LogCategory.Cryptography, LogLevel.Warning);
                return null;
            }

            if (!allowInsecureHttp && endpointUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Log("ServerMasterSecretProvider: HTTP is disabled. Use HTTPS or enable Allow Insecure Http.", LogCategory.Cryptography, LogLevel.Warning);
                return null;
            }

            string userId = (includeUserIdHeader || includeUserIdInBody)
                ? SaveManager.ResolveUserIdentifier()
                : null;

            using var request = BuildRequest(userId);
            await SendRequestAsync(request);

#if UNITY_2020_2_OR_NEWER
            bool requestFailed = request.result != UnityWebRequest.Result.Success;
#else
            bool requestFailed = request.isNetworkError || request.isHttpError;
#endif
            if (requestFailed)
            {
                Logger.Log($"ServerMasterSecretProvider: request failed ({request.responseCode}) {request.error}", LogCategory.Cryptography, LogLevel.Warning);
                return null;
            }

            string payload = request.downloadHandler != null ? request.downloadHandler.text : null;
            if (string.IsNullOrWhiteSpace(payload))
            {
                Logger.Log("ServerMasterSecretProvider: empty response body.", LogCategory.Cryptography, LogLevel.Warning);
                return null;
            }

            string base64 = ExtractBase64(payload);
            if (string.IsNullOrWhiteSpace(base64))
            {
                Logger.Log("ServerMasterSecretProvider: response did not contain a base64 secret.", LogCategory.Cryptography, LogLevel.Warning);
                return null;
            }

            try
            {
                byte[] bytes = Convert.FromBase64String(base64.Trim());
                if (bytes.Length != 32)
                {
                    Logger.Log("ServerMasterSecretProvider: secret must be exactly 32 bytes.", LogCategory.Cryptography, LogLevel.Warning);
                    return null;
                }

                return bytes;
            }
            catch (Exception ex)
            {
                Logger.Log($"ServerMasterSecretProvider: invalid base64 secret ({ex.Message}).", LogCategory.Cryptography, LogLevel.Warning);
                return null;
            }
        }

        private UnityWebRequest BuildRequest(string userId)
        {
            UnityWebRequest request;
            if (method == HttpMethod.Post)
            {
                request = new UnityWebRequest(endpointUrl, UnityWebRequest.kHttpVerbPOST);
                string json = includeUserIdInBody && !string.IsNullOrEmpty(userId)
                    ? JsonUtility.ToJson(new UserIdPayload { userId = userId })
                    : "{}";
                byte[] body = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
            }
            else
            {
                request = UnityWebRequest.Get(endpointUrl);
            }

            if (request.downloadHandler == null)
                request.downloadHandler = new DownloadHandlerBuffer();

            if (includeUserIdHeader && !string.IsNullOrEmpty(userId))
                request.SetRequestHeader(userIdHeaderName, userId);

            if (includeUnityAuthToken)
            {
                string token = TryGetUnityAuthToken();
                if (!string.IsNullOrEmpty(token))
                    request.SetRequestHeader(authHeaderName, authHeaderPrefix + token);
            }

            return request;
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

            // Minimal JSON field extraction for custom field names
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

                Logger.Log("ServerMasterSecretProvider: Unity Authentication token API not found; request will be unauthenticated.", LogCategory.Cryptography, LogLevel.Info);
                return null;
            }

            Logger.Log("ServerMasterSecretProvider: Unity Authentication not signed in; request will be unauthenticated.", LogCategory.Cryptography, LogLevel.Info);
            return null;
#else
            Logger.Log("ServerMasterSecretProvider: Unity Authentication package not present; request will be unauthenticated.", LogCategory.Cryptography, LogLevel.Info);
            return null;
#endif
        }
    }
}
#endif
