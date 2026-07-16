using System;
using System.Threading.Tasks;
using Hiveng.V1;

namespace HiveAxyl.Sdk
{
    public sealed class AuthApi
    {
        private readonly Session session;
        private readonly ClientPlatform platform;
        private readonly GuestInstallation guestInstallation;
        private readonly object gate = new object();
        private ConnectClient client;
        private Player player;

        public event Action<HiveAxylBannedException> Banned;

        internal AuthApi(
            Session session,
            ClientPlatform platform,
            GuestInstallation guestInstallation)
        {
            this.session = session;
            this.platform = platform;
            this.guestInstallation = guestInstallation;
        }

        internal void Bind(ConnectClient client)
        {
            lock (gate)
            {
                this.client = client;
            }
        }

        public async Task<LoginProviders> GetLoginProvidersAsync(string countryOverride = "")
        {
            ConnectClient activeClient = RequireClient();
            GetLoginProvidersRequest request = new GetLoginProvidersRequest
            {
                CountryOverride = countryOverride ?? "",
                Platform = platform
            };
            GetLoginProvidersResponse response =
                await activeClient.UnaryAsync(
                    "AuthService",
                    "GetLoginProviders",
                    request,
                    GetLoginProvidersResponse.Parser,
                    true);
            return LoginProviders.From(response);
        }

        public Task<Player> LoginWithGoogleAsync(string idToken)
        {
            if (string.IsNullOrEmpty(idToken))
            {
                throw HiveAxylException.InvalidArgument("idToken is required");
            }
            return LoginAsync(IdentityProvider.Google, idToken);
        }

        public Task<Player> LoginWithAppleAsync(string identityToken)
        {
            if (string.IsNullOrEmpty(identityToken))
            {
                throw HiveAxylException.InvalidArgument("identityToken is required");
            }
            return LoginAsync(IdentityProvider.Apple, identityToken);
        }

        public Task<Player> LoginAsGuestAsync()
        {
            string credential = guestInstallation.GetOrCreateCredential();
            return LoginAsync(IdentityProvider.Guest, credential);
        }

        public async Task<Player> LoginWithGoogleDesktopAsync(
            string clientId,
            string clientSecret = "",
            int port = 0)
        {
            string idToken = await GoogleDesktopSignIn.SignInAsync(clientId, clientSecret, port);
            return await LoginAsync(IdentityProvider.Google, idToken);
        }

        public async Task<Player> LoginWithFacebookDesktopAsync(int port = 0)
        {
            ConnectClient activeClient = RequireClient();
            CompleteFacebookDesktopLoginResponse response =
                await FacebookDesktopSignIn.SignInAsync(activeClient, platform, port);
            return SaveLogin(response.Player, response.TokenPair);
        }

        public async Task<Player> LoginWithAppleDesktopAsync(string clientId, int port = 0)
        {
            ConnectClient activeClient = RequireClient();
            TokenPair tokenPair = await AppleDesktopSignIn.SignInAsync(activeClient, clientId, port);
            session.Save(tokenPair);
            Player logged = await GetPlayerAsync();
            if (logged == null)
            {
                throw HiveAxylException.Transport("Apple login response missing player");
            }
            return logged;
        }

        public async Task<Player> GetPlayerAsync()
        {
            if (string.IsNullOrEmpty(session.AccessToken))
            {
                return null;
            }

            ConnectClient activeClient = RequireClient();
            GetPlayerResponse response =
                await activeClient.UnaryAsync(
                    "AuthService",
                    "GetPlayer",
                    new GetPlayerRequest(),
                    GetPlayerResponse.Parser,
                    true);
            if (response.Player == null)
            {
                return null;
            }

            Player restored = Player.From(response.Player);
            SetPlayer(restored);
            return restored;
        }

        public async Task<Player> RestoreSessionAsync()
        {
            if (string.IsNullOrEmpty(session.AccessToken))
            {
                return null;
            }

            ConnectClient activeClient;
            try
            {
                activeClient = RequireClient();
            }
            catch
            {
                return null;
            }

            try
            {
                GetPlayerResponse response =
                    await activeClient.UnaryAsync(
                        "AuthService",
                        "GetPlayer",
                        new GetPlayerRequest(),
                        GetPlayerResponse.Parser,
                        true);
                if (response.Player == null)
                {
                    return null;
                }

                Player restored = Player.From(response.Player);
                SetPlayer(restored);
                return restored;
            }
            catch
            {
                return null;
            }
        }

        public async Task LogoutAsync()
        {
            ConnectClient activeClient = RequireClient();
            if (!string.IsNullOrEmpty(session.AccessToken))
            {
                try
                {
                    await activeClient.UnaryAsync(
                        "AuthService",
                        "Logout",
                        new LogoutRequest(),
                        LogoutResponse.Parser,
                        true);
                }
                catch
                {
                }
            }
            session.Clear();
            ClearPlayer();
        }

        public Player CurrentPlayer()
        {
            lock (gate)
            {
                return player;
            }
        }

        public string PlayerValidationToken
        {
            get { return session.PlayerValidationToken; }
        }

        internal void EmitIfBanned(HiveAxylException error)
        {
            HiveAxylBannedException banned = error as HiveAxylBannedException;
            if (banned == null)
            {
                return;
            }

            Action<HiveAxylBannedException> handler = Banned;
            if (handler != null)
            {
                handler(banned);
            }
        }

        internal void ClearPlayer()
        {
            lock (gate)
            {
                player = null;
            }
        }

        private async Task<Player> LoginAsync(IdentityProvider provider, string providerToken)
        {
            ConnectClient activeClient = RequireClient();
            LoginWithProviderRequest request = new LoginWithProviderRequest
            {
                Provider = provider,
                ProviderToken = providerToken,
                Platform = platform
            };
            LoginWithProviderResponse response =
                await activeClient.UnaryAsync(
                    "AuthService",
                    "LoginWithProvider",
                    request,
                    LoginWithProviderResponse.Parser,
                    true);

            if (response.Player == null || response.TokenPair == null)
            {
                throw HiveAxylException.Transport("login response missing player or token pair");
            }

            return SaveLogin(response.Player, response.TokenPair);
        }

        private Player SaveLogin(Hiveng.V1.Player playerMessage, TokenPair tokenPair)
        {
            if (playerMessage == null || tokenPair == null)
            {
                throw HiveAxylException.Transport("login response missing player or token pair");
            }
            session.Save(tokenPair);
            Player logged = Player.From(playerMessage);
            SetPlayer(logged);
            return logged;
        }

        private void SetPlayer(Player value)
        {
            lock (gate)
            {
                player = value;
            }
        }

        private ConnectClient RequireClient()
        {
            lock (gate)
            {
                if (client == null)
                {
                    throw HiveAxylException.NotInitialized();
                }
                return client;
            }
        }
    }
}
