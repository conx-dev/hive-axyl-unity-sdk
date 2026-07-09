using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
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
        private const int CallbackTimeoutMs = 120000;
        private const int RequestReadFrames = 80;

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

            TcpListener listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            try
            {
                int localPort = ((IPEndPoint)listener.LocalEndpoint).Port;
                string redirectUri = "http://127.0.0.1:" + localPort + "/callback";
                string state = RandomUrlToken(16);
                string verifier = RandomUrlToken(32);
                string challenge = Base64Url(Sha256(verifier));
                string nonce = RandomUrlToken(16);
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
                string code = await WaitForCallbackAsync(listener, state);
                return await ExchangeCodeAsync(resolvedClientId, resolvedClientSecret, verifier, redirectUri, code);
            }
            finally
            {
                listener.Stop();
            }
#else
            await Task.CompletedTask;
            throw HiveAxylException.InvalidArgument(
                "Google desktop sign-in is only available on desktop platforms");
#endif
        }

#if UNITY_STANDALONE || UNITY_EDITOR
        private static async Task<string> WaitForCallbackAsync(TcpListener listener, string state)
        {
            int timeoutAt = Environment.TickCount + CallbackTimeoutMs;
            while (Environment.TickCount < timeoutAt)
            {
                if (!listener.Pending())
                {
                    await Task.Yield();
                    continue;
                }

                using (TcpClient client = listener.AcceptTcpClient())
                {
                    string requestText = await ReadRequestAsync(client);
                    Dictionary<string, string> parameters = ParseCallbackQuery(requestText);
                    WriteCallbackResponse(client, parameters.ContainsKey("error"));
                    if (parameters.ContainsKey("error"))
                    {
                        throw HiveAxylException.Transport("Google sign-in failed: " + parameters["error"]);
                    }
                    string returnedState;
                    parameters.TryGetValue("state", out returnedState);
                    if (returnedState != state)
                    {
                        throw HiveAxylException.InvalidArgument("Google sign-in state mismatch");
                    }
                    string code;
                    parameters.TryGetValue("code", out code);
                    if (string.IsNullOrEmpty(code))
                    {
                        throw HiveAxylException.InvalidArgument("Google authorization code is missing");
                    }
                    return code;
                }
            }
            throw HiveAxylException.Transport("Google sign-in timed out");
        }

        private static async Task<string> ReadRequestAsync(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            StringBuilder builder = new StringBuilder();
            byte[] buffer = new byte[4096];
            for (int i = 0; i < RequestReadFrames; i++)
            {
                while (stream.DataAvailable)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                    {
                        break;
                    }
                    builder.Append(Encoding.UTF8.GetString(buffer, 0, read));
                }
                if (builder.ToString().Contains("\r\n\r\n"))
                {
                    return builder.ToString();
                }
                await Task.Yield();
            }
            return builder.ToString();
        }

        private static Dictionary<string, string> ParseCallbackQuery(string requestText)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            int lineEnd = requestText.IndexOf("\r\n", StringComparison.Ordinal);
            string requestLine = lineEnd >= 0 ? requestText.Substring(0, lineEnd) : requestText;
            string[] parts = requestLine.Split(' ');
            if (parts.Length < 2)
            {
                return parameters;
            }

            string path = parts[1];
            int question = path.IndexOf('?');
            if (question < 0)
            {
                return parameters;
            }

            string query = path.Substring(question + 1);
            string[] pairs = query.Split('&');
            for (int i = 0; i < pairs.Length; i++)
            {
                string pair = pairs[i];
                int equals = pair.IndexOf('=');
                if (equals < 0)
                {
                    continue;
                }
                string key = Uri.UnescapeDataString(pair.Substring(0, equals));
                string value = Uri.UnescapeDataString(pair.Substring(equals + 1).Replace("+", " "));
                parameters[key] = value;
            }
            return parameters;
        }

        private static void WriteCallbackResponse(TcpClient client, bool failed)
        {
            string body = failed
                ? "<!doctype html><html><body><h1>Hive Axyl sign-in failed</h1><p>You can close this window.</p></body></html>"
                : "<!doctype html><html><body><h1>Hive Axyl sign-in complete</h1><p>You can return to Unity.</p></body></html>";
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            string header = "HTTP/1.1 200 OK\r\n"
                + "Content-Type: text/html; charset=utf-8\r\n"
                + "Content-Length: " + bodyBytes.Length + "\r\n"
                + "Connection: close\r\n\r\n";
            byte[] headerBytes = Encoding.UTF8.GetBytes(header);
            NetworkStream stream = client.GetStream();
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(bodyBytes, 0, bodyBytes.Length);
            stream.Flush();
        }

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

        private static string RandomUrlToken(int byteLength)
        {
            byte[] bytes = new byte[byteLength];
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(bytes);
            }
            return Base64Url(bytes);
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
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }
#endif
    }
}
