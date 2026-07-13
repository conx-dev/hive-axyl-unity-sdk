using System.Collections.Generic;
using System.Threading.Tasks;
using Hiveng.V1;
using UnityEngine;

namespace HiveAxyl.Sdk
{
    internal static class FacebookDesktopSignIn
    {
        public static async Task<CompleteFacebookDesktopLoginResponse> SignInAsync(
            ConnectClient client,
            ClientPlatform platform,
            int port)
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            using (DesktopOAuthLoopbackServer loopback = DesktopOAuthLoopbackServer.Start(port))
            {
                string callbackState = DesktopOAuthLoopbackServer.RandomUrlToken(16);
                StartFacebookDesktopLoginRequest startRequest = new StartFacebookDesktopLoginRequest
                {
                    ReturnUrl = loopback.RedirectUri,
                    CallbackState = callbackState,
                    Platform = platform
                };
                StartFacebookDesktopLoginResponse startResponse = await client.UnaryAsync(
                    "AuthService",
                    "StartFacebookDesktopLogin",
                    startRequest,
                    StartFacebookDesktopLoginResponse.Parser,
                    false);
                if (string.IsNullOrEmpty(startResponse.AuthorizationUrl))
                {
                    throw HiveAxylException.Transport("Facebook login response missing authorization URL");
                }

                Application.OpenURL(startResponse.AuthorizationUrl);
                Dictionary<string, string> parameters = await loopback.WaitForCallbackAsync(
                    callbackState,
                    "callback_state",
                    "Facebook");
                string completionCode;
                parameters.TryGetValue("completion_code", out completionCode);
                if (string.IsNullOrEmpty(completionCode))
                {
                    throw HiveAxylException.InvalidArgument("Facebook completion code is missing");
                }

                CompleteFacebookDesktopLoginRequest completeRequest =
                    new CompleteFacebookDesktopLoginRequest
                    {
                        CompletionCode = completionCode
                    };
                return await client.UnaryAsync(
                    "AuthService",
                    "CompleteFacebookDesktopLogin",
                    completeRequest,
                    CompleteFacebookDesktopLoginResponse.Parser,
                    true);
            }
#else
            await Task.CompletedTask;
            throw HiveAxylException.InvalidArgument(
                "Facebook desktop sign-in is only available on desktop platforms");
#endif
        }
    }
}
