using System.Collections.Generic;
using UnityEngine;

namespace HiveAxyl.Sdk
{
    public interface ITokenStorage
    {
        string Get(string key);
        void Set(string key, string value);
        void Remove(string key);
    }

    public sealed class InMemoryTokenStorage : ITokenStorage
    {
        private readonly Dictionary<string, string> values = new Dictionary<string, string>();

        public string Get(string key)
        {
            string value;
            if (values.TryGetValue(key, out value))
            {
                return value;
            }
            return null;
        }

        public void Set(string key, string value)
        {
            values[key] = value ?? "";
        }

        public void Remove(string key)
        {
            values.Remove(key);
        }
    }

    public sealed class PlayerPrefsTokenStorage : ITokenStorage
    {
        public string Get(string key)
        {
            if (!PlayerPrefs.HasKey(key))
            {
                return null;
            }
            return PlayerPrefs.GetString(key, "");
        }

        public void Set(string key, string value)
        {
            PlayerPrefs.SetString(key, value ?? "");
            PlayerPrefs.Save();
        }

        public void Remove(string key)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }

    // 리브랜드(Hive Axyl) 전 키를 유지 — 변경 시 기존 설치 기기의 세션이 유실된다.
    internal static class TokenStorageKeys
    {
        public const string AccessToken = "hiveng.accessToken";
        public const string RefreshToken = "hiveng.refreshToken";
        public const string PlayerValidationToken = "hiveng.playerValidationToken";
        public const string PlayerValidationTokenExpiresAt = "hiveng.playerValidationTokenExpiresAt";
    }
}
