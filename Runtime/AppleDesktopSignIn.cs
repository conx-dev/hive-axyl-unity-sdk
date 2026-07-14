using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Hiveng.V1;
using UnityEngine;

namespace HiveAxyl.Sdk
{
    internal static class AppleDesktopSignIn
    {
        [Serializable]
        private sealed class StartRequest
        {
            public string clientId;
            public string returnUrl;
            public string platform;
        }

        [Serializable]
        private sealed class StartResponse
        {
            public string authorizationUrl;
        }

        public static async Task<TokenPair> SignInAsync(
            ConnectClient client,
            string clientId,
            int port)
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            string resolvedClientId = clientId == null ? "" : clientId.Trim();
            if (resolvedClientId.Length == 0)
            {
                throw HiveAxylException.InvalidArgument("Apple Services ID is required");
            }

            using (DesktopOAuthLoopbackServer loopback = DesktopOAuthLoopbackServer.Start(port))
            {
                string callbackState = DesktopOAuthLoopbackServer.RandomUrlToken(16);
                string returnUrl = loopback.RedirectUri
                    + "?callback_state="
                    + Uri.EscapeDataString(callbackState);
                StartRequest startRequest = new StartRequest
                {
                    clientId = resolvedClientId,
                    returnUrl = returnUrl,
                    platform = "desktop"
                };
                string json = await client.PostJsonAsync(
                    "/oauth/apple/start",
                    JsonUtility.ToJson(startRequest));
                StartResponse startResponse = ParseStartResponse(json);

                Application.OpenURL(startResponse.authorizationUrl);
                Dictionary<string, string> parameters = await loopback.WaitForCallbackAsync(
                    callbackState,
                    "callback_state",
                    "Apple");
                return ParseTokenPair(parameters);
            }
#else
            await Task.CompletedTask;
            throw HiveAxylException.InvalidArgument(
                "Apple desktop sign-in is only available on desktop platforms");
#endif
        }

#if UNITY_STANDALONE || UNITY_EDITOR
        private static StartResponse ParseStartResponse(string json)
        {
            StartResponse response;
            try
            {
                response = JsonUtility.FromJson<StartResponse>(json);
            }
            catch
            {
                throw HiveAxylException.Transport("Apple login response is invalid");
            }
            if (response == null || string.IsNullOrEmpty(response.authorizationUrl))
            {
                throw HiveAxylException.Transport("Apple login response missing authorization URL");
            }
            return response;
        }

        private static TokenPair ParseTokenPair(Dictionary<string, string> parameters)
        {
            string status = Value(parameters, "status");
            if (status != "ok")
            {
                throw HiveAxylException.InvalidArgument("Apple login callback is invalid");
            }
            string accessToken = Value(parameters, "access_token");
            string refreshToken = Value(parameters, "refresh_token");
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
            {
                throw HiveAxylException.Transport("Apple login response missing token pair");
            }

            TokenPair pair = new TokenPair
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                PlayerValidationToken = Value(parameters, "player_validation_token")
            };
            pair.AccessTokenExpiresAt = ParseTimestamp(Value(parameters, "access_token_expires_at"));
            pair.PlayerValidationTokenExpiresAt = ParseTimestamp(
                Value(parameters, "player_validation_token_expires_at"));
            return pair;
        }

        private static string Value(Dictionary<string, string> parameters, string key)
        {
            string value;
            if (parameters.TryGetValue(key, out value))
            {
                return value ?? "";
            }
            return "";
        }

        private static Timestamp ParseTimestamp(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
            DateTime parsed;
            bool valid = DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out parsed);
            if (!valid)
            {
                return null;
            }
            return Timestamp.FromDateTime(parsed.ToUniversalTime());
        }
#endif
    }
}
