using System;
using System.Globalization;

namespace CourseGuard.Frontend.Theme
{
    internal static class SystemTimeFormatter
    {
        private const string DisplayFormat = "dd/MM/yyyy HH:mm";
        private static readonly TimeZoneInfo VietnamTimeZone = CreateVietnamTimeZone();

        public static string FormatVietnamTime(DateTime value)
        {
            if (value == DateTime.MinValue)
                return string.Empty;

            if (value.Kind == DateTimeKind.Unspecified)
                return value.ToString(DisplayFormat, CultureInfo.InvariantCulture);

            DateTime utc = value.Kind == DateTimeKind.Utc
                ? value
                : value.ToUniversalTime();

            DateTime vietnamTime = TimeZoneInfo.ConvertTimeFromUtc(utc, VietnamTimeZone);
            return vietnamTime.ToString(DisplayFormat, CultureInfo.InvariantCulture);
        }

        private static TimeZoneInfo CreateVietnamTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
            catch (TimeZoneNotFoundException)
            {
                return CreateUtcPlusSevenTimeZone();
            }
            catch (InvalidTimeZoneException)
            {
                return CreateUtcPlusSevenTimeZone();
            }
        }

        private static TimeZoneInfo CreateUtcPlusSevenTimeZone()
        {
            return TimeZoneInfo.CreateCustomTimeZone(
                "UTC+07",
                TimeSpan.FromHours(7),
                "UTC+07",
                "UTC+07");
        }
    }
}
