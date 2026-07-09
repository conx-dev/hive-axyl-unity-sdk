using System;
using System.Threading.Tasks;
using Hiveng.V1;

namespace HiveAxyl.Sdk
{
    internal sealed class Session
    {
        private readonly ITokenStorage storage;
        private readonly object gate = new object();
        private Task<bool> refreshing;
        private Exception refreshError;

        public Func<string, Task<TokenPair>> RefreshFunc { get; set; }
        public Action OnCleared { get; set; }

        public Session(ITokenStorage storage)
        {
            this.storage = storage;
        }

        public string AccessToken
        {
            get { return storage.Get(TokenStorageKeys.AccessToken); }
        }

        public string RefreshToken
        {
            get { return storage.Get(TokenStorageKeys.RefreshToken); }
        }

        public string PlayerValidationToken
        {
            get
            {
                string token = storage.Get(TokenStorageKeys.PlayerValidationToken);
                string rawExpiresAt = storage.Get(TokenStorageKeys.PlayerValidationTokenExpiresAt);
                long expiresAt;
                if (string.IsNullOrEmpty(token) || !long.TryParse(rawExpiresAt, out expiresAt))
                {
                    return null;
                }
                if (expiresAt <= DateTime.UtcNow.Ticks)
                {
                    ClearPlayerValidationToken();
                    return null;
                }
                return token;
            }
        }

        public void Save(TokenPair pair)
        {
            storage.Set(TokenStorageKeys.AccessToken, pair.AccessToken);
            storage.Set(TokenStorageKeys.RefreshToken, pair.RefreshToken);
            if (!string.IsNullOrEmpty(pair.PlayerValidationToken) &&
                pair.PlayerValidationTokenExpiresAt != null)
            {
                storage.Set(TokenStorageKeys.PlayerValidationToken, pair.PlayerValidationToken);
                storage.Set(
                    TokenStorageKeys.PlayerValidationTokenExpiresAt,
                    pair.PlayerValidationTokenExpiresAt.ToDateTime().Ticks.ToString());
            }
            else
            {
                ClearPlayerValidationToken();
            }
        }

        public void Clear()
        {
            storage.Remove(TokenStorageKeys.AccessToken);
            storage.Remove(TokenStorageKeys.RefreshToken);
            ClearPlayerValidationToken();
            Action onCleared = OnCleared;
            if (onCleared != null)
            {
                onCleared();
            }
        }

        private void ClearPlayerValidationToken()
        {
            storage.Remove(TokenStorageKeys.PlayerValidationToken);
            storage.Remove(TokenStorageKeys.PlayerValidationTokenExpiresAt);
        }

        public Task<bool> TryRefreshAsync()
        {
            Task<bool> current;
            lock (gate)
            {
                if (refreshing == null)
                {
                    refreshError = null;
                    refreshing = DoRefreshAsync();
                }
                current = refreshing;
            }
            return AwaitRefreshAsync(current);
        }

        public Exception ConsumeRefreshError()
        {
            lock (gate)
            {
                Exception error = refreshError;
                refreshError = null;
                return error;
            }
        }

        private async Task<bool> AwaitRefreshAsync(Task<bool> current)
        {
            try
            {
                return await current;
            }
            finally
            {
                lock (gate)
                {
                    if (ReferenceEquals(refreshing, current))
                    {
                        refreshing = null;
                    }
                }
            }
        }

        private async Task<bool> DoRefreshAsync()
        {
            string refreshToken = RefreshToken;
            if (string.IsNullOrEmpty(refreshToken) || RefreshFunc == null)
            {
                return false;
            }

            try
            {
                TokenPair pair = await RefreshFunc(refreshToken);
                Save(pair);
                return true;
            }
            catch (Exception error)
            {
                lock (gate)
                {
                    refreshError = error;
                }
                Clear();
                return false;
            }
        }
    }
}
