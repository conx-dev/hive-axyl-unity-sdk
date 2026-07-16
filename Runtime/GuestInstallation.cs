using System;
using System.Security.Cryptography;
using Hiveng.V1;
using UnityEngine;

namespace HiveAxyl.Sdk
{
    internal interface IGuestInstallationStorage
    {
        string Get();
        bool Set(string value);
    }

    internal sealed class PlayerPrefsGuestInstallationStorage : IGuestInstallationStorage
    {
        private const string Key = "hive-ng.device.id";

        public string Get()
        {
            if (!PlayerPrefs.HasKey(Key))
            {
                return null;
            }
            return PlayerPrefs.GetString(Key, "");
        }

        public bool Set(string value)
        {
            PlayerPrefs.SetString(Key, value);
            PlayerPrefs.Save();
            return PlayerPrefs.GetString(Key, "") == value;
        }
    }

    internal sealed class GuestInstallation
    {
        private const string Prefix = "g1_";
        private const int RandomBytes = 32;
        private readonly IGuestInstallationStorage storage;
        private readonly object gate = new object();

        public GuestInstallation(IGuestInstallationStorage storage)
        {
            this.storage = storage;
        }

        public string GetOrCreateCredential()
        {
            lock (gate)
            {
                try
                {
                    string existing = storage.Get();
                    if (IsCredential(existing))
                    {
                        return existing;
                    }
                    byte[] random = new byte[RandomBytes];
                    using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
                    {
                        generator.GetBytes(random);
                    }
                    string credential = Prefix + EncodeBase64Url(random);
                    if (!storage.Set(credential) || storage.Get() != credential)
                    {
                        throw Unavailable();
                    }
                    return credential;
                }
                catch (HiveAxylException)
                {
                    throw;
                }
                catch (Exception)
                {
                    throw Unavailable();
                }
            }
        }

        private static bool IsCredential(string value)
        {
            if (string.IsNullOrEmpty(value)
                || value.Length != 46
                || !value.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return false;
            }
            string encoded = value.Substring(Prefix.Length);
            try
            {
                byte[] decoded = Convert.FromBase64String(ToStandardBase64(encoded));
                return decoded.Length == RandomBytes && EncodeBase64Url(decoded) == encoded;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static string EncodeBase64Url(byte[] value)
        {
            return Convert.ToBase64String(value)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string ToStandardBase64(string value)
        {
            return value.Replace('-', '+').Replace('_', '/') + "=";
        }

        private static HiveAxylException Unavailable()
        {
            return new HiveAxylException(
                ErrorCode.Internal,
                "Guest login requires persistent app storage and secure randomness");
        }
    }
}
