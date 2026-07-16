using System.Collections.Generic;
using System.Threading.Tasks;
using Hiveng.V1;

namespace HiveAxyl.Sdk
{
    public sealed class HiveAxyl
    {
        private readonly ResolvedConfig config;
        private readonly Session session;
        private readonly object gate = new object();
        private bool ready;

        public AuthApi Auth { get; private set; }
        public NoticeApi Notice { get; private set; }
        public MailboxApi Mailbox { get; private set; }
        public PaymentApi Payment { get; private set; }

        public HiveAxyl(HiveAxylConfig config)
        {
            this.config = ResolvedConfig.From(config);
            session = new Session(ResolveStorage(config));
            GuestInstallation guestInstallation = new GuestInstallation(
                new PlayerPrefsGuestInstallationStorage());
            Auth = new AuthApi(session, this.config.Platform, guestInstallation);
            Notice = new NoticeApi();
            Mailbox = new MailboxApi();
            Payment = new PaymentApi();
            session.OnCleared = Auth.ClearPlayer;
        }

        public async Task InitializeAsync()
        {
            ConnectClient gateway = new ConnectClient(
                config.GatewayUrl,
                config.ApiKey,
                config.Language,
                session,
                config.Debug);
            ResolveEndpointsRequest request = new ResolveEndpointsRequest
            {
                ClientVersion = config.ClientVersion,
                ProjectId = config.ProjectId
            };
            ResolveEndpointsResponse response =
                await gateway.UnaryAsync(
                    "DiscoveryService",
                    "ResolveEndpoints",
                    request,
                    ResolveEndpointsResponse.Parser,
                    false);

            Dictionary<string, string> resolved = new Dictionary<string, string>();
            for (int i = 0; i < response.Endpoints.Count; i++)
            {
                EndpointEntry entry = response.Endpoints[i];
                resolved[entry.Domain] = TrimTrailingSlash(entry.BaseUrl);
            }

            string authBaseUrl;
            if (!resolved.TryGetValue("auth", out authBaseUrl) || string.IsNullOrEmpty(authBaseUrl))
            {
                throw HiveAxylException.Transport("discovery returned no endpoint for domain: auth");
            }

            ConnectClient authClient = new ConnectClient(
                authBaseUrl,
                config.ApiKey,
                config.Language,
                session,
                config.Debug);
            session.RefreshFunc = async refreshToken =>
            {
                RefreshTokenRequest refreshRequest = new RefreshTokenRequest
                {
                    RefreshToken = refreshToken
                };
                RefreshTokenResponse refreshResponse =
                    await authClient.UnaryAsync(
                        "AuthService",
                        "RefreshToken",
                        refreshRequest,
                        RefreshTokenResponse.Parser,
                        false);
                if (refreshResponse.TokenPair == null)
                {
                    throw HiveAxylException.Transport("refresh response missing token pair");
                }
                return refreshResponse.TokenPair;
            };
            authClient.OnBannedError = Auth.EmitIfBanned;
            Auth.Bind(authClient);
            string noticeBaseUrl;
            if (resolved.TryGetValue("notice", out noticeBaseUrl) && !string.IsNullOrEmpty(noticeBaseUrl))
            {
                ConnectClient noticeClient = new ConnectClient(
                    noticeBaseUrl,
                    config.ApiKey,
                    config.Language,
                    session,
                    config.Debug);
                Notice.Bind(noticeClient, config.Language);
            }
            else
            {
                Notice.Unbind();
            }

            string mailboxBaseUrl;
            if (resolved.TryGetValue("mailbox", out mailboxBaseUrl) && !string.IsNullOrEmpty(mailboxBaseUrl))
            {
                ConnectClient mailboxClient = new ConnectClient(
                    mailboxBaseUrl,
                    config.ApiKey,
                    config.Language,
                    session,
                    config.Debug);
                Mailbox.Bind(mailboxClient, config.Language);
            }
            else
            {
                Mailbox.Unbind();
            }
            string paymentBaseUrl;
            if (resolved.TryGetValue("payment", out paymentBaseUrl) && !string.IsNullOrEmpty(paymentBaseUrl))
            {
                ConnectClient paymentClient = new ConnectClient(
                    paymentBaseUrl,
                    config.ApiKey,
                    config.Language,
                    session,
                    config.Debug);
                Payment.Bind(paymentClient);
            }
            else
            {
                Payment.Unbind();
            }

            lock (gate)
            {
                ready = true;
            }
        }

        public bool IsReady
        {
            get
            {
                lock (gate)
                {
                    return ready;
                }
            }
        }

        private static ITokenStorage ResolveStorage(HiveAxylConfig config)
        {
            if (config.TokenStorage != null)
            {
                return config.TokenStorage;
            }
            if (config.PersistSession)
            {
                return new PlayerPrefsTokenStorage();
            }
            return new InMemoryTokenStorage();
        }

        private static string TrimTrailingSlash(string value)
        {
            if (value == null)
            {
                return "";
            }
            return value.TrimEnd('/');
        }
    }

    public static class HiveAxylSdk
    {
        public static HiveAxyl CreateHiveAxyl(HiveAxylConfig config)
        {
            return new HiveAxyl(config);
        }
    }
}
