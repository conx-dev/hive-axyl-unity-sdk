using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Hiveng.V1;
using UnityEngine;

namespace HiveAxyl.Sdk
{
    internal static class ConnectErrorParser
    {
        [Serializable]
        private sealed class Envelope
        {
            public string code = "";
            public string message = "";
            public Detail[] details = new Detail[0];
        }

        [Serializable]
        private sealed class Detail
        {
            public string type = "";
            public string value = "";
        }

        public static HiveAxylException Parse(long statusCode, byte[] body)
        {
            string json = Encoding.UTF8.GetString(body ?? new byte[0]);
            Envelope envelope;
            try
            {
                envelope = JsonUtility.FromJson<Envelope>(json);
            }
            catch
            {
                return HiveAxylException.Transport("HTTP " + statusCode);
            }
            if (envelope == null)
            {
                return HiveAxylException.Transport("HTTP " + statusCode);
            }

            string message = envelope.message ?? "";
            ErrorDetail detail = ErrorDetailOf(envelope);
            if (detail == null)
            {
                if (IsRetryableConnectCode(envelope.code))
                {
                    return HiveAxylException.Transport(message.Length == 0 ? "HTTP " + statusCode : message);
                }
                ErrorCode errorCode = RestErrorCode(envelope.code);
                if (errorCode != ErrorCode.Unspecified)
                {
                    return new HiveAxylException(errorCode, message);
                }
                return new HiveAxylException(ErrorCode.Unspecified, message);
            }
            return MapDetail(detail, message);
        }

        private static bool IsRetryableConnectCode(string code)
        {
            return code == "unavailable" || code == "deadline_exceeded";
        }

        private static ErrorCode RestErrorCode(string code)
        {
            if (string.IsNullOrEmpty(code) || !code.StartsWith("ERROR_CODE_", StringComparison.Ordinal))
            {
                return ErrorCode.Unspecified;
            }
            Google.Protobuf.Reflection.EnumDescriptor descriptor =
                CommonReflection.Descriptor.EnumTypes[0];
            Google.Protobuf.Reflection.EnumValueDescriptor value = descriptor.FindValueByName(code);
            if (value == null)
            {
                return ErrorCode.Unspecified;
            }
            return (ErrorCode)value.Number;
        }

        private static ErrorDetail ErrorDetailOf(Envelope envelope)
        {
            if (envelope.details == null)
            {
                return null;
            }

            for (int i = 0; i < envelope.details.Length; i++)
            {
                Detail detail = envelope.details[i];
                if (detail == null || string.IsNullOrEmpty(detail.value))
                {
                    continue;
                }

                string type = NormalizeType(detail.type);
                if (type != "hiveng.v1.ErrorDetail")
                {
                    continue;
                }

                byte[] bytes = DecodeBase64(detail.value);
                if (bytes == null)
                {
                    continue;
                }

                try
                {
                    return ErrorDetail.Parser.ParseFrom(bytes);
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        private static string NormalizeType(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            int index = value.LastIndexOf('/');
            if (index < 0 || index == value.Length - 1)
            {
                return value;
            }
            return value.Substring(index + 1);
        }

        private static byte[] DecodeBase64(string value)
        {
            string normalized = value.Replace('-', '+').Replace('_', '/');
            int remainder = normalized.Length % 4;
            if (remainder > 0)
            {
                normalized = normalized.PadRight(normalized.Length + 4 - remainder, '=');
            }

            try
            {
                return Convert.FromBase64String(normalized);
            }
            catch
            {
                return null;
            }
        }

        private static HiveAxylException MapDetail(ErrorDetail detail, string message)
        {
            Dictionary<string, string> metadata = new Dictionary<string, string>(detail.Metadata);
            if (detail.Code == ErrorCode.PlayerBanned)
            {
                string reason = ValueOrDefault(metadata, "reason", message);
                string untilRaw = ValueOrDefault(metadata, "until", ValueOrDefault(metadata, "banned_until", ""));
                bool permanent = ValueOrDefault(metadata, "permanent", "") == "true";
                DateTime? until = ParseDateTime(untilRaw);
                return new HiveAxylBannedException(reason, until, permanent, message, metadata);
            }
            if (detail.Code == ErrorCode.MaintenanceInProgress)
            {
                string maintenanceMessage = ValueOrDefault(metadata, "message", message);
                DateTime? startsAt = ParseDateTime(ValueOrDefault(metadata, "starts_at", ""));
                DateTime? endsAt = ParseDateTime(ValueOrDefault(metadata, "ends_at", ""));
                return new HiveAxylMaintenanceException(
                    maintenanceMessage,
                    startsAt,
                    endsAt,
                    maintenanceMessage,
                    metadata);
            }
            return new HiveAxylException(detail.Code, message, metadata, false);
        }

        private static string ValueOrDefault(Dictionary<string, string> values, string key, string fallback)
        {
            string value;
            if (values.TryGetValue(key, out value))
            {
                return value;
            }
            return fallback;
        }

        private static DateTime? ParseDateTime(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            DateTime parsed;
            bool ok = DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out parsed);
            if (!ok)
            {
                return null;
            }
            return parsed;
        }
    }
}
