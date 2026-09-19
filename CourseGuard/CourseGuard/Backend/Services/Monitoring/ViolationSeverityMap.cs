namespace CourseGuard.Backend.Services.Monitoring
{
    public static class ViolationSeverityMap
    {
        public static string Get(string? violationType)
        {
            string value = (violationType ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(value))
                return "MEDIUM";

            if (value.Contains("CONNECTION_LOST", System.StringComparison.OrdinalIgnoreCase) ||
                value is "MULTI_MONITOR" or "BLACKLISTED_APP" or "VM_DETECTION")
                return "HIGH";

            if (value is "KEY_PRESS" or "WINDOW_MINIMIZE")
                return "LOW";

            if (value is "ALT_TAB" or "SCREEN_SWITCH" || value.Contains("FOCUS") || value.Contains("CHUYEN TAB"))
                return "MEDIUM";

            return "MEDIUM";
        }
    }
}
