using System.Globalization;
using Hiveng.V1;
using UnityEngine;

namespace HiveAxyl.Sdk
{
    public enum HiveAxylClientPlatform
    {
        Auto,
        Web,
        Android,
        Ios,
        Desktop
    }

    public sealed class HiveAxylConfig
    {
        public const string DefaultGatewayUrl = "https://gw-test-gcl.c2xstation.net:8081";

        public string GatewayUrl { get; set; }
        public string ProjectId { get; set; }
        public string ApiKey { get; set; }
        public string ClientVersion { get; set; }
        public string Language { get; set; }
        public bool Debug { get; set; }
        public bool PersistSession { get; set; } = true;
        public HiveAxylClientPlatform Platform { get; set; }
        public ITokenStorage TokenStorage { get; set; }

        public HiveAxylConfig()
        {
            GatewayUrl = "";
            ProjectId = "";
            ApiKey = "";
            ClientVersion = "";
            Language = "";
            Platform = HiveAxylClientPlatform.Auto;
        }
    }

    internal sealed class ResolvedConfig
    {
        public string GatewayUrl { get; private set; }
        public string ProjectId { get; private set; }
        public string ApiKey { get; private set; }
        public string ClientVersion { get; private set; }
        public string Language { get; private set; }
        public bool Debug { get; private set; }
        public ClientPlatform Platform { get; private set; }

        private ResolvedConfig()
        {
        }

        public static ResolvedConfig From(HiveAxylConfig config)
        {
            if (config == null)
            {
                throw HiveAxylException.InvalidArgument("config is required");
            }

            string gatewayUrl = TrimTrailingSlash(config.GatewayUrl);
            string projectId = Trim(config.ProjectId);
            if (gatewayUrl.Length == 0)
            {
                gatewayUrl = HiveAxylConfig.DefaultGatewayUrl;
            }
            if (projectId.Length == 0)
            {
                throw HiveAxylException.InvalidArgument("projectId is required");
            }
            if (string.IsNullOrEmpty(config.ApiKey))
            {
                throw HiveAxylException.InvalidArgument("apiKey is required");
            }

            return new ResolvedConfig
            {
                GatewayUrl = gatewayUrl,
                ProjectId = projectId,
                ApiKey = config.ApiKey,
                ClientVersion = ResolveClientVersion(config.ClientVersion),
                Language = ResolveLanguage(config.Language),
                Debug = config.Debug,
                Platform = UnityClientPlatform.ToProto(config.Platform)
            };
        }

        private static string Trim(string value)
        {
            if (value == null)
            {
                return "";
            }
            return value.Trim();
        }

        private static string TrimTrailingSlash(string value)
        {
            return Trim(value).TrimEnd('/');
        }

        private static string ResolveClientVersion(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
            return Application.version ?? "";
        }

        private static string ResolveLanguage(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
            return CultureInfo.CurrentUICulture.Name;
        }
    }
}
