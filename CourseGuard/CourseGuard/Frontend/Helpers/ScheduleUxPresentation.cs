using System;
using System.Globalization;
using CourseGuard.Backend.Models;

namespace CourseGuard.Frontend.Helpers
{
    public sealed class ScheduleUxPresentation
    {
        public string StatusText { get; init; } = string.Empty;
        public string DetailText { get; init; } = string.Empty;
        public string PrimaryActionText { get; init; } = string.Empty;
        public string Tone { get; init; } = "Neutral";
        public bool CanJoin { get; init; }
        public bool CanOpenClass { get; init; }
        public bool IsLive { get; init; }
        public bool IsPast { get; init; }
        public string NextActionKind { get; init; } = NextActionKinds.None;
        public string TargetTab { get; init; } = NavigationTargets.None;
        public int? TargetCourseId { get; init; }
        public int? TargetItemId { get; init; }
        public int? TargetSessionId { get; init; }
    }

    public static class ScheduleUxPresenter
    {
        public static ScheduleUxPresentation PresentStudent(StudentScheduleItemModel session, DateTime now)
        {
            if (!session.StartTime.HasValue)
            {
                return new ScheduleUxPresentation
                {
                    StatusText = "Chưa xếp giờ học",
                    DetailText = "Buổi học chưa có thời gian bắt đầu.",
                    PrimaryActionText = "Chưa thể vào",
                    Tone = "Muted",
                    TargetCourseId = session.CourseId,
                    TargetSessionId = session.SessionId
                };
            }

            DateTime start = session.StartTime.Value;
            DateTime? end = session.EndTime;
            if (end.HasValue && now > end.Value)
                return PresentPast(session.CourseId, session.SessionId, NavigationTargets.StudentSchedule, end.Value);

            if (now < start)
            {
                return new ScheduleUxPresentation
                {
                    StatusText = "Chưa đến giờ",
                    DetailText = $"Bắt đầu lúc {FormatDateTime(start)}.",
                    PrimaryActionText = "Chưa đến giờ",
                    Tone = "Info",
                    TargetTab = NavigationTargets.StudentSchedule,
                    TargetCourseId = session.CourseId,
                    TargetSessionId = session.SessionId
                };
            }

            if (!session.IsOpened)
            {
                return new ScheduleUxPresentation
                {
                    StatusText = "Chờ giáo viên mở lớp",
                    DetailText = $"Buổi học đang trong khung giờ {FormatEndWindow(end)}, chờ giáo viên mở lớp.",
                    PrimaryActionText = "Chờ mở lớp",
                    Tone = "Warning",
                    IsLive = true,
                    TargetTab = NavigationTargets.StudentSchedule,
                    TargetCourseId = session.CourseId,
                    TargetSessionId = session.SessionId
                };
            }

            return new ScheduleUxPresentation
            {
                StatusText = "Đang mở",
                DetailText = $"Lớp đang mở {FormatEndWindow(end)}.",
                PrimaryActionText = "Vào lớp",
                Tone = "Success",
                CanJoin = true,
                IsLive = true,
                NextActionKind = NextActionKinds.JoinStudentClassroom,
                TargetTab = NavigationTargets.StudentSchedule,
                TargetCourseId = session.CourseId,
                TargetSessionId = session.SessionId
            };
        }

        public static ScheduleUxPresentation PresentTeacher(TeacherScheduleItemModel session, DateTime now)
        {
            if (!session.StartTime.HasValue)
            {
                return new ScheduleUxPresentation
                {
                    StatusText = "Chưa xếp giờ dạy",
                    DetailText = "Buổi dạy chưa có thời gian bắt đầu.",
                    PrimaryActionText = "Chưa thể mở",
                    Tone = "Muted",
                    TargetCourseId = session.CourseId,
                    TargetSessionId = session.Id
                };
            }

            DateTime start = session.StartTime.Value;
            DateTime? end = session.EndTime;
            if (end.HasValue && now > end.Value)
                return PresentPast(session.CourseId, session.Id, NavigationTargets.TeacherSchedule, end.Value);

            if (now < start)
            {
                return new ScheduleUxPresentation
                {
                    StatusText = "Chưa đến giờ",
                    DetailText = $"Bắt đầu lúc {FormatDateTime(start)}.",
                    PrimaryActionText = "Chưa mở lớp",
                    Tone = "Info",
                    TargetTab = NavigationTargets.TeacherSchedule,
                    TargetCourseId = session.CourseId,
                    TargetSessionId = session.Id
                };
            }

            bool opened = session.IsOpened;
            return new ScheduleUxPresentation
            {
                StatusText = opened ? "Đang mở" : "Đến giờ dạy",
                DetailText = opened
                    ? $"Lớp đang mở {FormatEndWindow(end)}."
                    : $"Đã đến giờ dạy. Có thể mở lớp {FormatEndWindow(end)}.",
                PrimaryActionText = opened ? "Vào lớp đang mở" : "Mở lớp",
                Tone = opened ? "Success" : "Warning",
                CanOpenClass = true,
                IsLive = true,
                NextActionKind = NextActionKinds.OpenTeacherClassroom,
                TargetTab = NavigationTargets.TeacherSchedule,
                TargetCourseId = session.CourseId,
                TargetSessionId = session.Id
            };
        }

        private static ScheduleUxPresentation PresentPast(int courseId, int sessionId, string targetTab, DateTime end)
        {
            return new ScheduleUxPresentation
            {
                StatusText = "Đã kết thúc",
                DetailText = $"Lớp đã kết thúc lúc {FormatDateTime(end)}.",
                PrimaryActionText = "Đã kết thúc",
                Tone = "Muted",
                IsPast = true,
                TargetTab = targetTab,
                TargetCourseId = courseId,
                TargetSessionId = sessionId
            };
        }

        private static string FormatDateTime(DateTime value)
        {
            return value.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
        }

        private static string FormatEndWindow(DateTime? value)
        {
            return value.HasValue
                ? $"đến {FormatDateTime(value.Value)}"
                : "với thời gian kết thúc chưa rõ";
        }
    }
}
