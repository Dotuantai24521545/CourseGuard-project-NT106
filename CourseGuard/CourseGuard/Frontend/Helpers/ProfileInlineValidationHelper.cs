using System;
using System.Globalization;
using System.Linq;
using System.Net.Mail;

namespace CourseGuard.Frontend.Helpers
{
    public static class ProfileInlineValidationHelper
    {
        public const string FullNameRequiredMessage = "Họ và tên không được để trống.";
        public const string InvalidEmailMessage = "Email không hợp lệ.";
        public const string InvalidPhoneMessage = "Số điện thoại không hợp lệ.";
        public const string InvalidBirthDateMessage = "Ngày sinh cần có định dạng dd/MM/yyyy.";

        public static string ValidateFullName(string? fullName)
        {
            return string.IsNullOrWhiteSpace(fullName) ? FullNameRequiredMessage : string.Empty;
        }

        public static string ValidateEmail(string? email, bool required)
        {
            string value = email?.Trim() ?? string.Empty;
            if (value.Length == 0)
                return required ? InvalidEmailMessage : string.Empty;

            try
            {
                MailAddress address = new(value);
                return string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : InvalidEmailMessage;
            }
            catch (FormatException)
            {
                return InvalidEmailMessage;
            }
        }

        public static string ValidatePhone(string? phone)
        {
            string value = phone?.Trim() ?? string.Empty;
            if (value.Length == 0)
                return string.Empty;

            string normalized = value
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(".", string.Empty, StringComparison.Ordinal)
                .Replace("(", string.Empty, StringComparison.Ordinal)
                .Replace(")", string.Empty, StringComparison.Ordinal);

            if (normalized.StartsWith("+", StringComparison.Ordinal))
                normalized = normalized[1..];

            return normalized.Length is >= 9 and <= 15 && normalized.All(IsAsciiDigit)
                ? string.Empty
                : InvalidPhoneMessage;
        }

        public static string ValidateBirthDate(string? birthDate)
        {
            string value = birthDate?.Trim() ?? string.Empty;
            if (value.Length == 0)
                return string.Empty;

            return DateTime.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                ? string.Empty
                : InvalidBirthDateMessage;
        }

        private static bool IsAsciiDigit(char value)
        {
            return value is >= '0' and <= '9';
        }
    }
}
