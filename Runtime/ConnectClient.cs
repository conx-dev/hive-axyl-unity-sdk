using System;
using System.Threading.Tasks;
using Google.Protobuf;
using UnityEngine;
using UnityEngine.Networking;

namespace HiveAxyl.Sdk
{
    internal sealed class ConnectClient
    {
        private readonly string baseUrl;
        private readonly string apiKey;
        private readonly string language;
        private readonly Session session;
        private readonly bool debug;

        public Action<HiveAxylException> OnBannedError { get; set; }

        public ConnectClient(string baseUrl, string apiKey, string language, Session session, bool debug)
        {
            this.baseUrl = TrimTrailingSlash(baseUrl);
            this.apiKey = apiKey;
            this.language = language ?? "";
            this.session = session;
            this.debug = debug;
        }

        public async Task<TResponse> UnaryAsync<TRequest, TResponse>(
            string service,
            string method,
            TRequest request,
            MessageParser<TResponse> parser,
            bool allowsSessionRefresh)
            where TRequest : IMessage
            where TResponse : IMessage<TResponse>
        {
            try
            {
                return await SendOnceAsync(service, method, request, parser);
            }
            catch (HiveAxylException error)
            {
                if (!allowsSessionRefresh || error.ErrorCode != Hiveng.V1.ErrorCode.SessionExpired)
                {
                    throw;
                }

                bool refreshed = await session.TryRefreshAsync();
                if (!refreshed)
                {
                    Exception refreshError = session.ConsumeRefreshError();
                    if (refreshError != null)
                    {
                        throw refreshError;
                    }
                    throw;
                }

                return await SendOnceAsync(service, method, request, parser);
            }
        }

        private async Task<TResponse> SendOnceAsync<TRequest, TResponse>(
            string service,
            string method,
            TRequest message,
            MessageParser<TResponse> parser)
            where TRequest : IMessage
            where TResponse : IMessage<TResponse>
        {
            string url = baseUrl + "/hiveng.v1." + service + "/" + method;
            byte[] body = message.ToByteArray();
            using (UnityWebRequest webRequest = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(body);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/proto");
                webRequest.SetRequestHeader("Accept", "application/proto");
                ApplyAuthHeaders(webRequest, method);

                await SendAsync(webRequest);

                byte[] data = webRequest.downloadHandler.data ?? new byte[0];
                if (webRequest.responseCode != 200)
                {
                    HiveAxylException error = ConnectErrorParser.Parse(webRequest.responseCode, data);
                    Log(service + "/" + method + " failed: " + error.Code + " " + error.Message);
                    if (error is HiveAxylBannedException && OnBannedError != null)
                    {
                        OnBannedError(error);
                    }
                    throw error;
                }

                if (webRequest.result == UnityWebRequest.Result.ConnectionError)
                {
                    throw HiveAxylException.Transport(webRequest.error);
                }

                try
                {
                    return parser.ParseFrom(data);
                }
                catch
                {
                    throw HiveAxylException.Transport("invalid response body for " + service + "/" + method);
                }
            }
        }

        private void ApplyAuthHeaders(UnityWebRequest request, string method)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                return;
            }

            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            if (!string.IsNullOrEmpty(language))
            {
                request.SetRequestHeader("X-Hive-Ng-Language", language);
            }
            string accessToken = session.AccessToken;
            if (!string.IsNullOrEmpty(accessToken) && method != "RefreshToken")
            {
                request.SetRequestHeader("X-Player-Token", accessToken);
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

        private static string TrimTrailingSlash(string value)
        {
            if (value == null)
            {
                return "";
            }
            return value.TrimEnd('/');
        }

        private void Log(string message)
        {
            if (!debug)
            {
                return;
            }
            Debug.Log("[hive-axyl] " + message);
        }
    }
}
