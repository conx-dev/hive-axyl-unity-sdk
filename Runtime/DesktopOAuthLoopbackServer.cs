using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

#if UNITY_STANDALONE || UNITY_EDITOR
namespace HiveAxyl.Sdk
{
    internal sealed class DesktopOAuthLoopbackServer : IDisposable
    {
        private const int CallbackTimeoutMs = 120000;
        private const int RequestReadTimeoutMs = 5000;

        private readonly TcpListener listener;

        private DesktopOAuthLoopbackServer(TcpListener listener)
        {
            this.listener = listener;
            int localPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            RedirectUri = "http://127.0.0.1:" + localPort + "/callback";
        }

        public string RedirectUri { get; private set; }

        public static DesktopOAuthLoopbackServer Start(int port)
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return new DesktopOAuthLoopbackServer(listener);
        }

        public async Task<Dictionary<string, string>> WaitForCallbackAsync(
            string expectedState,
            string stateParameter,
            string providerName)
        {
            Stopwatch timeout = Stopwatch.StartNew();
            while (timeout.ElapsedMilliseconds < CallbackTimeoutMs)
            {
                if (!listener.Pending())
                {
                    await Task.Yield();
                    continue;
                }

                using (TcpClient client = listener.AcceptTcpClient())
                {
                    string requestText = await ReadRequestAsync(client);
                    Dictionary<string, string> parameters = ParseCallback(requestText);
                    bool failed = HasCallbackError(parameters);
                    WriteCallbackResponse(client, failed);
                    string error;
                    if (failed)
                    {
                        if (!parameters.TryGetValue("error", out error))
                        {
                            parameters.TryGetValue("error_code", out error);
                        }
                        if (string.IsNullOrEmpty(error))
                        {
                            error = "sign-in failed";
                        }
                        string detail;
                        if (parameters.TryGetValue("error_message", out detail) && !string.IsNullOrEmpty(detail))
                        {
                            error += ": " + detail;
                        }
                        throw HiveAxylException.Transport(providerName + " sign-in failed: " + error);
                    }
                    string returnedState;
                    parameters.TryGetValue(stateParameter, out returnedState);
                    if (returnedState != expectedState)
                    {
                        throw HiveAxylException.InvalidArgument(providerName + " sign-in state mismatch");
                    }
                    return parameters;
                }
            }
            throw HiveAxylException.Transport(providerName + " sign-in timed out");
        }

        public void Dispose()
        {
            listener.Stop();
        }

        public static string RandomUrlToken(int byteLength)
        {
            byte[] bytes = new byte[byteLength];
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(bytes);
            }
            return Base64Url(bytes);
        }

        public static string Base64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }

        private static async Task<string> ReadRequestAsync(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            StringBuilder builder = new StringBuilder();
            byte[] buffer = new byte[4096];
            Stopwatch timeout = Stopwatch.StartNew();
            while (timeout.ElapsedMilliseconds < RequestReadTimeoutMs)
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
                string requestText = builder.ToString();
                if (IsRequestComplete(requestText))
                {
                    return requestText;
                }
                await Task.Delay(10);
            }
            return builder.ToString();
        }

        private static bool IsRequestComplete(string requestText)
        {
            int headerEnd = requestText.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd < 0)
            {
                return false;
            }
            int contentLength = ParseContentLength(requestText.Substring(0, headerEnd));
            string body = requestText.Substring(headerEnd + 4);
            return Encoding.UTF8.GetByteCount(body) >= contentLength;
        }

        private static int ParseContentLength(string headers)
        {
            string[] lines = headers.Split(new[] { "\r\n" }, StringSplitOptions.None);
            for (int index = 1; index < lines.Length; index++)
            {
                string line = lines[index];
                int colon = line.IndexOf(':');
                if (colon < 0)
                {
                    continue;
                }
                string name = line.Substring(0, colon).Trim();
                if (!name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                int value;
                if (int.TryParse(line.Substring(colon + 1).Trim(), out value) && value > 0)
                {
                    return value;
                }
                return 0;
            }
            return 0;
        }

        internal static Dictionary<string, string> ParseCallback(string requestText)
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
            string callbackPath = question >= 0 ? path.Substring(0, question) : path;
            if (callbackPath != "/callback")
            {
                return parameters;
            }

            if (question >= 0)
            {
                ParseFormEncoded(path.Substring(question + 1), parameters, true);
            }
            int headerEnd = requestText.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd >= 0 && headerEnd + 4 < requestText.Length)
            {
                ParseFormEncoded(requestText.Substring(headerEnd + 4), parameters, false);
            }
            return parameters;
        }

        private static void ParseFormEncoded(
            string encoded,
            Dictionary<string, string> parameters,
            bool overwrite)
        {
            string[] pairs = encoded.Split('&');
            for (int index = 0; index < pairs.Length; index++)
            {
                string pair = pairs[index];
                int equals = pair.IndexOf('=');
                if (equals < 0)
                {
                    continue;
                }
                string key = Uri.UnescapeDataString(pair.Substring(0, equals));
                string value = Uri.UnescapeDataString(pair.Substring(equals + 1).Replace("+", " "));
                if (!overwrite && parameters.ContainsKey(key))
                {
                    continue;
                }
                parameters[key] = value;
            }
        }

        private static bool HasCallbackError(Dictionary<string, string> parameters)
        {
            if (parameters.ContainsKey("error"))
            {
                return true;
            }
            string status;
            return parameters.TryGetValue("status", out status) && status == "error";
        }

        private static void WriteCallbackResponse(TcpClient client, bool failed)
        {
            string body;
            if (failed)
            {
                body = "<!doctype html><html><body><h1>Hive Axyl sign-in failed</h1><p>You can close this window.</p></body></html>";
            }
            else
            {
                body = "<!doctype html><html><body><h1>Hive Axyl sign-in complete</h1><p>You can return to the game.</p></body></html>";
            }
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            string header = "HTTP/1.1 200 OK\r\n"
                + "Content-Type: text/html; charset=utf-8\r\n"
                + "Cache-Control: no-store\r\n"
                + "Content-Length: " + bodyBytes.Length + "\r\n"
                + "Connection: close\r\n\r\n";
            byte[] headerBytes = Encoding.UTF8.GetBytes(header);
            NetworkStream stream = client.GetStream();
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(bodyBytes, 0, bodyBytes.Length);
            stream.Flush();
        }
    }
}
#endif
