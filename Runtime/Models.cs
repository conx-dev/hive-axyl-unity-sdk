using System;
using System.Collections.Generic;
using Hiveng.V1;

namespace HiveAxyl.Sdk
{
    public sealed class Player
    {
        public string PlayerId { get; private set; }
        public string ProjectId { get; private set; }
        public string Country { get; private set; }
        public string Email { get; private set; }
        public string Nickname { get; private set; }
        public string LastLoginPlatform { get; private set; }
        public IReadOnlyList<string> Providers { get; private set; }
        public DateTime? CreatedAt { get; private set; }
        public DateTime? LastLoginAt { get; private set; }

        private Player()
        {
        }

        internal static Player From(Hiveng.V1.Player message)
        {
            List<string> providers = new List<string>();
            for (int i = 0; i < message.Providers.Count; i++)
            {
                providers.Add(ProviderName(message.Providers[i]));
            }

            return new Player
            {
                PlayerId = message.PlayerId,
                ProjectId = message.ProjectId,
                Country = message.Country,
                Email = message.Email,
                Nickname = message.Nickname,
                LastLoginPlatform = PlatformName(message.LastLoginPlatform),
                Providers = providers,
                CreatedAt = ToDateTime(message.CreatedAt),
                LastLoginAt = ToDateTime(message.LastLoginAt)
            };
        }

        private static DateTime? ToDateTime(Google.Protobuf.WellKnownTypes.Timestamp timestamp)
        {
            if (timestamp == null)
            {
                return null;
            }
            return timestamp.ToDateTime();
        }

        private static string ProviderName(IdentityProvider provider)
        {
            switch (provider)
            {
                case IdentityProvider.Kakao:
                    return "kakao";
                case IdentityProvider.Naver:
                    return "naver";
                case IdentityProvider.Google:
                    return "google";
                case IdentityProvider.Facebook:
                    return "facebook";
                case IdentityProvider.Apple:
                    return "apple";
                case IdentityProvider.Line:
                    return "line";
                case IdentityProvider.Truecaller:
                    return "truecaller";
                case IdentityProvider.PhoneOtp:
                    return "phone_otp";
                case IdentityProvider.Guest:
                    return "guest";
                default:
                    return "unspecified";
            }
        }

        private static string PlatformName(ClientPlatform platform)
        {
            switch (platform)
            {
                case ClientPlatform.Web:
                    return "web";
                case ClientPlatform.Android:
                    return "android";
                case ClientPlatform.Ios:
                    return "ios";
                case ClientPlatform.Desktop:
                    return "desktop";
                default:
                    return "unspecified";
            }
        }
    }

    public sealed class LoginProviders
    {
        public IReadOnlyList<string> Providers { get; private set; }
        public string Country { get; private set; }

        private LoginProviders()
        {
        }

        internal static LoginProviders From(GetLoginProvidersResponse message)
        {
            List<string> providers = new List<string>();
            for (int i = 0; i < message.Providers.Count; i++)
            {
                providers.Add(ProviderName(message.Providers[i]));
            }

            return new LoginProviders
            {
                Providers = providers,
                Country = message.Country
            };
        }

        private static string ProviderName(IdentityProvider provider)
        {
            switch (provider)
            {
                case IdentityProvider.Kakao:
                    return "kakao";
                case IdentityProvider.Naver:
                    return "naver";
                case IdentityProvider.Google:
                    return "google";
                case IdentityProvider.Facebook:
                    return "facebook";
                case IdentityProvider.Apple:
                    return "apple";
                case IdentityProvider.Line:
                    return "line";
                case IdentityProvider.Truecaller:
                    return "truecaller";
                case IdentityProvider.PhoneOtp:
                    return "phone_otp";
                case IdentityProvider.Guest:
                    return "guest";
                default:
                    return "unspecified";
            }
        }
    }

    public sealed class Notice
    {
        public string Id { get; private set; }
        public string ProjectId { get; private set; }
        public string Title { get; private set; }
        public string Body { get; private set; }
        public DateTime? StartsAt { get; private set; }
        public DateTime? EndsAt { get; private set; }
        public long ViewCount { get; private set; }

        private Notice()
        {
        }

        internal static Notice From(Hiveng.V1.Notice message, string language)
        {
            return new Notice
            {
                Id = message.Id,
                ProjectId = message.ProjectId,
                Title = ResolveLocalized(message.Title, language),
                Body = ResolveLocalized(message.Body, language),
                StartsAt = ToDateTime(message.StartsAt),
                EndsAt = ToDateTime(message.EndsAt),
                ViewCount = message.ViewCount
            };
        }

        private static DateTime? ToDateTime(Google.Protobuf.WellKnownTypes.Timestamp timestamp)
        {
            if (timestamp == null)
            {
                return null;
            }
            return timestamp.ToDateTime();
        }

        private static string ResolveLocalized(IDictionary<string, string> values, string language)
        {
            string normalized = language == null ? "" : language.Trim();
            if (normalized.Length > 0)
            {
                string exact;
                if (values.TryGetValue(normalized, out exact))
                {
                    return exact;
                }
                int dash = normalized.IndexOf('-');
                if (dash > 0)
                {
                    string baseMatch;
                    if (values.TryGetValue(normalized.Substring(0, dash), out baseMatch))
                    {
                        return baseMatch;
                    }
                }
            }
            string english;
            if (values.TryGetValue("en", out english))
            {
                return english;
            }
            string korean;
            if (values.TryGetValue("ko", out korean))
            {
                return korean;
            }
            foreach (KeyValuePair<string, string> entry in values)
            {
                return entry.Value;
            }
            return "";
        }
    }

    public sealed class Mail
    {
        public string Id { get; private set; }
        public string MailId { get; private set; }
        public string ProjectId { get; private set; }
        public MailType Type { get; private set; }
        public string Title { get; private set; }
        public string Body { get; private set; }
        public string Sender { get; private set; }
        public IReadOnlyDictionary<string, string> RewardPreview { get; private set; }
        public bool Claimed { get; private set; }
        public DateTime? ClaimableFrom { get; private set; }
        public DateTime? ExpiresAt { get; private set; }
        public DateTime? ClaimedAt { get; private set; }
        public DateTime? CreatedAt { get; private set; }

        private Mail()
        {
        }

        internal static Mail From(Hiveng.V1.Mail message, string language)
        {
            return new Mail
            {
                Id = message.Id,
                MailId = message.MailId,
                ProjectId = message.ProjectId,
                Type = message.Type,
                Title = ResolveLocalized(message.Title, language),
                Body = ResolveLocalized(message.Body, language),
                Sender = message.Sender,
                RewardPreview = message.RewardPreview,
                Claimed = message.Claimed,
                ClaimableFrom = ToDateTime(message.ClaimableFrom),
                ExpiresAt = ToDateTime(message.ExpiresAt),
                ClaimedAt = ToDateTime(message.ClaimedAt),
                CreatedAt = ToDateTime(message.CreatedAt)
            };
        }

        private static DateTime? ToDateTime(Google.Protobuf.WellKnownTypes.Timestamp timestamp)
        {
            if (timestamp == null)
            {
                return null;
            }
            return timestamp.ToDateTime();
        }

        private static string ResolveLocalized(IDictionary<string, string> values, string language)
        {
            string normalized = language == null ? "" : language.Trim();
            if (normalized.Length > 0)
            {
                string exact;
                if (values.TryGetValue(normalized, out exact))
                {
                    return exact;
                }
                int dash = normalized.IndexOf('-');
                if (dash > 0)
                {
                    string baseMatch;
                    if (values.TryGetValue(normalized.Substring(0, dash), out baseMatch))
                    {
                        return baseMatch;
                    }
                }
            }
            string english;
            if (values.TryGetValue("en", out english))
            {
                return english;
            }
            string korean;
            if (values.TryGetValue("ko", out korean))
            {
                return korean;
            }
            foreach (KeyValuePair<string, string> entry in values)
            {
                return entry.Value;
            }
            return "";
        }
    }

    public sealed class PaymentPurchase
    {
        public string Id { get; private set; }
        public string ProjectId { get; private set; }
        public string PlayerId { get; private set; }
        public string Market { get; private set; }
        public string ProductType { get; private set; }
        public string ProductId { get; private set; }
        public string PackageName { get; private set; }
        public string PurchaseIntentId { get; private set; }
        public long AmountMinor { get; private set; }
        public string Currency { get; private set; }
        public string Status { get; private set; }
        public string GrantStatus { get; private set; }
        public string ConsumeStatus { get; private set; }
        public string MarketOrderId { get; private set; }
        public DateTime? PurchasedAt { get; private set; }
        public DateTime? VerifiedAt { get; private set; }

        private PaymentPurchase()
        {
        }

        internal static PaymentPurchase From(Hiveng.V1.Purchase message)
        {
            return new PaymentPurchase
            {
                Id = message.Id,
                ProjectId = message.ProjectId,
                PlayerId = message.PlayerId,
                Market = MarketName(message.Market),
                ProductType = ProductTypeName(message.ProductType),
                ProductId = message.ProductId,
                PackageName = message.PackageName,
                PurchaseIntentId = message.PurchaseIntentId,
                AmountMinor = message.AmountMinor,
                Currency = message.Currency,
                Status = PurchaseStatusName(message.Status),
                GrantStatus = message.GrantStatus,
                ConsumeStatus = message.ConsumeStatus,
                MarketOrderId = message.MarketOrderId,
                PurchasedAt = ToDateTime(message.PurchasedAt),
                VerifiedAt = ToDateTime(message.VerifiedAt)
            };
        }

        private static DateTime? ToDateTime(Google.Protobuf.WellKnownTypes.Timestamp timestamp)
        {
            if (timestamp == null)
            {
                return null;
            }
            return timestamp.ToDateTime();
        }

        private static string MarketName(Hiveng.V1.Market market)
        {
            switch (market)
            {
                case Hiveng.V1.Market.GooglePlay:
                    return "google_play";
                case Hiveng.V1.Market.AppStore:
                    return "app_store";
                case Hiveng.V1.Market.Steam:
                    return "steam";
                case Hiveng.V1.Market.Web:
                    return "web";
                default:
                    return "unspecified";
            }
        }

        private static string ProductTypeName(Hiveng.V1.ProductType productType)
        {
            switch (productType)
            {
                case Hiveng.V1.ProductType.OneTime:
                    return "one_time";
                case Hiveng.V1.ProductType.Subscription:
                    return "subscription";
                default:
                    return "unspecified";
            }
        }

        private static string PurchaseStatusName(PurchaseStatus status)
        {
            switch (status)
            {
                case PurchaseStatus.Pending:
                    return "pending";
                case PurchaseStatus.Verified:
                    return "verified";
                case PurchaseStatus.Failed:
                    return "failed";
                case PurchaseStatus.Refunded:
                    return "refunded";
                case PurchaseStatus.Canceled:
                    return "canceled";
                case PurchaseStatus.Expired:
                    return "expired";
                default:
                    return "unspecified";
            }
        }
    }
}
