using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace HiveAxyl.Sdk
{
    internal static class GoogleDesktopSignIn
    {
        private const string AuthUrl = "https://accounts.google.com/o/oauth2/v2/auth";
        private const string TokenUrl = "https://oauth2.googleapis.com/token";
        [Serializable]
        private sealed class TokenResponse
        {
            public string id_token;
        }

        public static async Task<string> SignInAsync(string clientId, string clientSecret, int port)
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            string resolvedClientId = clientId == null ? "" : clientId.Trim();
            if (resolvedClientId.Length == 0)
            {
                throw HiveAxylException.InvalidArgument("Google desktop client ID is required");
            }
            string resolvedClientSecret = clientSecret == null ? "" : clientSecret.Trim();

            using (DesktopOAuthLoopbackServer loopback = DesktopOAuthLoopbackServer.Start(port))
            {
                string redirectUri = loopback.RedirectUri;
                string state = DesktopOAuthLoopbackServer.RandomUrlToken(16);
                string verifier = DesktopOAuthLoopbackServer.RandomUrlToken(32);
                string challenge = Base64Url(Sha256(verifier));
                string nonce = DesktopOAuthLoopbackServer.RandomUrlToken(16);
                string url = AuthUrl + "?" + FormEncode(new Dictionary<string, string>
                {
                    { "response_type", "code" },
                    { "client_id", resolvedClientId },
                    { "redirect_uri", redirectUri },
                    { "scope", "openid email profile" },
                    { "code_challenge", challenge },
                    { "code_challenge_method", "S256" },
                    { "state", state },
                    { "nonce", nonce },
                    { "prompt", "select_account" }
                });

                Application.OpenURL(url);
                Dictionary<string, string> parameters = await loopback.WaitForCallbackAsync(
                    state,
                    "state",
                    "Google");
                string code;
                parameters.TryGetValue("code", out code);
                if (string.IsNullOrEmpty(code))
                {
                    throw HiveAxylException.InvalidArgument("Google authorization code is missing");
                }
                return await ExchangeCodeAsync(resolvedClientId, resolvedClientSecret, verifier, redirectUri, code);
            }
#else
            await Task.CompletedTask;
            throw HiveAxylException.InvalidArgument(
                "Google desktop sign-in is only available on desktop platforms");
#endif
        }

#if UNITY_STANDALONE || UNITY_EDITOR
        private static async Task<string> ExchangeCodeAsync(
            string clientId,
            string clientSecret,
            string verifier,
            string redirectUri,
            string code)
        {
            Dictionary<string, string> fields = new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "code", code },
                { "client_id", clientId },
                { "redirect_uri", redirectUri },
                { "code_verifier", verifier }
            };
            // Google 데스크톱 앱 클라이언트는 토큰 교환에 client_secret을 요구한다 (installed app에서는 기밀로 취급되지 않음).
            if (clientSecret.Length > 0)
            {
                fields["client_secret"] = clientSecret;
            }
            string payload = FormEncode(fields);
            byte[] body = Encoding.UTF8.GetBytes(payload);

            using (UnityWebRequest request = new UnityWebRequest(TokenUrl, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
                request.SetRequestHeader("Accept", "application/json");

                await SendAsync(request);

                string text = request.downloadHandler.text ?? "";
                if (request.responseCode != 200)
                {
                    throw HiveAxylException.Transport(
                        "Google token exchange failed: HTTP " + request.responseCode + " " + text);
                }
                TokenResponse parsed;
                try
                {
                    parsed = JsonUtility.FromJson<TokenResponse>(text);
                }
                catch
                {
                    throw HiveAxylException.Transport("Google token response is invalid");
                }
                if (parsed == null || string.IsNullOrEmpty(parsed.id_token))
                {
                    throw HiveAxylException.Transport("Google token response missing id_token");
                }
                return parsed.id_token;
            }
        }

        private static Task SendAsync(UnityWebRequest request)
        {
            TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            operation.completed += _ => completion.TrySetResult(true);
            if (operation.isDone)
            {
                completion.TrySetResult(true);
            }
            return completion.Task;
        }

        private static string FormEncode(Dictionary<string, string> values)
        {
            StringBuilder builder = new StringBuilder();
            foreach (KeyValuePair<string, string> entry in values)
            {
                if (builder.Length > 0)
                {
                    builder.Append('&');
                }
                builder.Append(Uri.EscapeDataString(entry.Key));
                builder.Append('=');
                builder.Append(Uri.EscapeDataString(entry.Value));
            }
            return builder.ToString();
        }

        private static byte[] Sha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            }
        }

        private static string Base64Url(byte[] bytes)
        {
            return DesktopOAuthLoopbackServer.Base64Url(bytes);
        }
#endif
    }
}
