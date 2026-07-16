using System;
using System.Collections.Generic;
using Hiveng.V1;

namespace HiveAxyl.Sdk
{
    public class HiveAxylException : Exception
    {
        public ErrorCode ErrorCode { get; private set; }
        public string Code { get; private set; }
        public IReadOnlyDictionary<string, string> Metadata { get; private set; }
        public bool IsTransport { get; private set; }

        public HiveAxylException(ErrorCode errorCode, string message)
            : this(errorCode, message, new Dictionary<string, string>(), false)
        {
        }

        public HiveAxylException(
            ErrorCode errorCode,
            string message,
            IReadOnlyDictionary<string, string> metadata,
            bool isTransport)
            : base(message)
        {
            ErrorCode = errorCode;
            Code = ErrorCodeName.Of(errorCode);
            Metadata = metadata ?? new Dictionary<string, string>();
            IsTransport = isTransport;
        }

        internal static HiveAxylException InvalidArgument(string message)
        {
            return new HiveAxylException(ErrorCode.InvalidArgument, message);
        }

        internal static HiveAxylException NotInitialized()
        {
            return new HiveAxylException(
                ErrorCode.Unspecified,
                "HiveAxyl not initialized - call InitializeAsync() first");
        }

        internal static HiveAxylException Transport(string message)
        {
            return new HiveAxylException(
                ErrorCode.Unspecified,
                message,
                new Dictionary<string, string>(),
                true);
        }
    }

    public sealed class HiveAxylBannedException : HiveAxylException
    {
        public string Reason { get; private set; }
        public DateTime? Until { get; private set; }
        public bool Permanent { get; private set; }

        public HiveAxylBannedException(
            string reason,
            DateTime? until,
            bool permanent,
            string message,
            IReadOnlyDictionary<string, string> metadata)
            : base(ErrorCode.PlayerBanned, message, metadata, false)
        {
            Reason = reason;
            Until = until;
            Permanent = permanent;
        }
    }

    public sealed class HiveAxylMaintenanceException : HiveAxylException
    {
        public string MaintenanceMessage { get; private set; }
        public DateTime? StartsAt { get; private set; }
        public DateTime? EndsAt { get; private set; }

        public HiveAxylMaintenanceException(
            string maintenanceMessage,
            DateTime? startsAt,
            DateTime? endsAt,
            string message,
            IReadOnlyDictionary<string, string> metadata)
            : base(ErrorCode.MaintenanceInProgress, message, metadata, false)
        {
            MaintenanceMessage = maintenanceMessage;
            StartsAt = startsAt;
            EndsAt = endsAt;
        }
    }

    internal static class ErrorCodeName
    {
        public static string Of(ErrorCode code)
        {
            switch (code)
            {
                case ErrorCode.Internal:
                    return "INTERNAL";
                case ErrorCode.InvalidArgument:
                    return "INVALID_ARGUMENT";
                case ErrorCode.NotFound:
                    return "NOT_FOUND";
                case ErrorCode.AlreadyExists:
                    return "ALREADY_EXISTS";
                case ErrorCode.PermissionDenied:
                    return "PERMISSION_DENIED";
                case ErrorCode.Unauthenticated:
                    return "UNAUTHENTICATED";
                case ErrorCode.RateLimited:
                    return "RATE_LIMITED";
                case ErrorCode.MaintenanceInProgress:
                    return "MAINTENANCE_IN_PROGRESS";
                case ErrorCode.GeoBlocked:
                    return "GEO_BLOCKED";
                case ErrorCode.ClientVersionUnsupported:
                    return "CLIENT_VERSION_UNSUPPORTED";
                case ErrorCode.PlayerBanned:
                    return "PLAYER_BANNED";
                case ErrorCode.InvalidProviderToken:
                    return "INVALID_PROVIDER_TOKEN";
                case ErrorCode.ProviderNotEnabled:
                    return "PROVIDER_NOT_ENABLED";
                case ErrorCode.CredentialNotConfigured:
                    return "CREDENTIAL_NOT_CONFIGURED";
                case ErrorCode.SessionExpired:
                    return "SESSION_EXPIRED";
                case ErrorCode.PlayerNotFound:
                    return "PLAYER_NOT_FOUND";
                case ErrorCode.DuplicateReceipt:
                    return "DUPLICATE_RECEIPT";
                case ErrorCode.ReceiptVerificationFailed:
                    return "RECEIPT_VERIFICATION_FAILED";
                case ErrorCode.MarketNotSupported:
                    return "MARKET_NOT_SUPPORTED";
                case ErrorCode.ApiKeyInvalid:
                    return "API_KEY_INVALID";
                case ErrorCode.ApiKeyRevoked:
                    return "API_KEY_REVOKED";
                case ErrorCode.ServerKeyInvalid:
                    return "SERVER_KEY_INVALID";
                case ErrorCode.ServerKeyRevoked:
                    return "SERVER_KEY_REVOKED";
                case ErrorCode.AdminEmailExists:
                    return "ADMIN_EMAIL_EXISTS";
                case ErrorCode.AdminInvalidCredentials:
                    return "ADMIN_INVALID_CREDENTIALS";
                case ErrorCode.PackageNameExists:
                    return "PACKAGE_NAME_EXISTS";
                default:
                    return "UNSPECIFIED";
            }
        }
    }
}
