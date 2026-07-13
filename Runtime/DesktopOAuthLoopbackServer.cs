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
        private const int RequestReadFrames = 80;

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
                    Dictionary<string, string> parameters = ParseCallbackQuery(requestText);
                    WriteCallbackResponse(client, parameters.ContainsKey("error"));
                    string error;
                    if (parameters.TryGetValue("error", out error))
                    {
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
            for (int index = 0; index < RequestReadFrames; index++)
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
            string callbackPath = question >= 0 ? path.Substring(0, question) : path;
            if (callbackPath != "/callback" || question < 0)
            {
                return parameters;
            }

            string query = path.Substring(question + 1);
            string[] pairs = query.Split('&');
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
                parameters[key] = value;
            }
            return parameters;
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
