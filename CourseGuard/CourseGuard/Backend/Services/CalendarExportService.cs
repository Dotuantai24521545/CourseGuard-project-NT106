using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CourseGuard.Backend.Services
{
    public class CalendarEventItem
    {
        public string Summary { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    public static class CalendarExportService
    {
        public static string ExportToIcsString(IEnumerable<CalendarEventItem> events)
        {
            var sb = new StringBuilder();
            sb.AppendLine("BEGIN:VCALENDAR");
            sb.AppendLine("VERSION:2.0");
            sb.AppendLine("PRODID:-//CourseGuard//CourseGuard Exam & Class Schedule//VN");
            sb.AppendLine("CALSCALE:GREGORIAN");
            sb.AppendLine("METHOD:PUBLISH");

            foreach (var ev in events)
            {
                sb.AppendLine("BEGIN:VEVENT");
                sb.AppendLine($"UID:{Guid.NewGuid()}@courseguard.edu.vn");
                sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
                sb.AppendLine($"DTSTART:{ev.StartTime.ToUniversalTime():yyyyMMddTHHmmssZ}");
                sb.AppendLine($"DTEND:{ev.EndTime.ToUniversalTime():yyyyMMddTHHmmssZ}");
                sb.AppendLine($"SUMMARY:{EscapeIcsText(ev.Summary)}");
                sb.AppendLine($"DESCRIPTION:{EscapeIcsText(ev.Description)}");
                if (!string.IsNullOrWhiteSpace(ev.Location))
                {
                    sb.AppendLine($"LOCATION:{EscapeIcsText(ev.Location)}");
                }
                sb.AppendLine("STATUS:CONFIRMED");
                sb.AppendLine("BEGIN:VALARM");
                sb.AppendLine("TRIGGER:-PT30M");
                sb.AppendLine("ACTION:DISPLAY");
                sb.AppendLine("DESCRIPTION:Nhắc nhở lịch học/thi CourseGuard");
                sb.AppendLine("END:VALARM");
                sb.AppendLine("END:VEVENT");
            }

            sb.AppendLine("END:VCALENDAR");
            return sb.ToString();
        }

        public static void SaveIcsFile(IEnumerable<CalendarEventItem> events, string filePath)
        {
            string csContent = ExportToIcsString(events);
            File.WriteAllText(filePath, csContent, Encoding.UTF8);
        }

        private static string EscapeIcsText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("\\", "\\\\")
                       .Replace(";", "\\;")
                       .Replace(",", "\\,")
                       .Replace("\n", "\\n");
        }
    }
}
