using System.Collections.Generic;
using System.Linq;
using CourseGuard.Backend.Models;

namespace CourseGuard.Frontend.Helpers
{
    public static class OverviewActionBuilder
    {
        public static OverviewActionItem[] BuildStudentActions(
            bool hasOpenExam,
            bool hasDeadlineSoon,
            bool hasClassToday,
            bool hasUnreadChat,
            bool hasUnreadNotification)
        {
            var actions = new List<OverviewActionItem>();

            if (hasOpenExam)
                actions.Add(new OverviewActionItem { Priority = 10, Title = "Làm bài thi đang mở", ActionText = "Làm bài", PageName = "Bài kiểm tra", Tone = "Warning" });
            if (hasDeadlineSoon)
                actions.Add(new OverviewActionItem { Priority = 20, Title = "Nộp bài tập sắp hết hạn", ActionText = "Nộp bài", PageName = "Bài tập", Tone = "Warning" });
            if (hasClassToday)
                actions.Add(new OverviewActionItem { Priority = 30, Title = "Vào lớp học hôm nay", ActionText = "Xem lịch", PageName = "Lịch học", Tone = "Neutral" });
            if (hasUnreadChat)
                actions.Add(new OverviewActionItem { Priority = 40, Title = "Đọc tin nhắn mới", ActionText = "Mở tin nhắn", PageName = "Tin nhắn", Tone = "Neutral" });
            if (hasUnreadNotification)
                actions.Add(new OverviewActionItem { Priority = 41, Title = "Xem thông báo mới", ActionText = "Mở thông báo", PageName = "Thông báo", Tone = "Neutral" });

            return actions.OrderBy(a => a.Priority).ToArray();
        }

        public static OverviewActionItem[] BuildTeacherActions(
            bool hasActiveExam,
            bool hasRequiredTask,
            bool hasUpcomingClass,
            bool hasUnreadChat,
            bool hasUnreadNotification)
        {
            var actions = new List<OverviewActionItem>();

            if (hasActiveExam)
                actions.Add(new OverviewActionItem { Priority = 10, Title = "Giám sát kỳ thi đang diễn ra", ActionText = "Giám sát", PageName = "Giám sát thi", Tone = "Warning" });
            if (hasRequiredTask)
                actions.Add(new OverviewActionItem { Priority = 20, Title = "Xử lý việc cần chú ý", ActionText = "Xử lý", PageName = "Bài tập", Tone = "Warning" });
            if (hasUpcomingClass)
                actions.Add(new OverviewActionItem { Priority = 30, Title = "Mở buổi dạy sắp tới", ActionText = "Xem lịch", PageName = "Lịch dạy", Tone = "Neutral" });
            if (hasUnreadChat)
                actions.Add(new OverviewActionItem { Priority = 40, Title = "Trả lời tin nhắn mới", ActionText = "Mở tin nhắn", PageName = "Tin nhắn", Tone = "Neutral" });
            if (hasUnreadNotification)
                actions.Add(new OverviewActionItem { Priority = 41, Title = "Xem thông báo mới", ActionText = "Mở thông báo", PageName = "Thông báo", Tone = "Neutral" });

            return actions.OrderBy(a => a.Priority).ToArray();
        }

        public static OverviewActionItem[] BuildStudentCourseActions(
            IEnumerable<StudentExamListItemModel> openExams,
            IEnumerable<DeadlineReminderItem> urgentDeadlines,
            IEnumerable<StudentScheduleItemModel> todayClasses,
            IEnumerable<ChatCourseModel> unreadChatCourses,
            IEnumerable<NotificationModel> notifications)
        {
            var actions = new List<OverviewActionItem>();

            actions.AddRange((openExams ?? Enumerable.Empty<StudentExamListItemModel>())
                .Where(exam => exam.CourseId > 0)
                .GroupBy(exam => exam.CourseId)
                .Select(group =>
                {
                    StudentExamListItemModel latest = group
                        .OrderBy(exam => exam.CloseTime ?? exam.OpenTime ?? System.DateTime.MaxValue)
                        .First();
                    return CreateCourseAction(
                        priority: 10,
                        title: group.Count() == 1 ? "Làm bài thi đang mở" : $"Làm {group.Count()} bài thi đang mở",
                        subtitle: BuildCourseSubtitle(latest.CourseName, latest.Title),
                        pageName: "Bài kiểm tra",
                        actionText: "Làm bài",
                        toolId: "exams",
                        courseId: group.Key,
                        courseName: latest.CourseName,
                        tone: "Warning");
                }));

            actions.AddRange((urgentDeadlines ?? Enumerable.Empty<DeadlineReminderItem>())
                .Where(deadline => deadline.CourseId > 0)
                .Select(deadline => new
                {
                    Deadline = deadline,
                    IsExam = string.Equals(deadline.SourceType, DeadlineReminderItem.SourceTypeExam, System.StringComparison.OrdinalIgnoreCase)
                })
                .GroupBy(item => new
                {
                    item.Deadline.CourseId,
                    ToolId = item.IsExam ? "exams" : "assignments",
                    PageName = item.IsExam ? "Bài kiểm tra" : "Bài tập",
                    ActionText = item.IsExam ? "Làm bài" : "Nộp bài"
                })
                .Select(group =>
                {
                    DeadlineReminderItem nearest = group.Select(item => item.Deadline).OrderBy(deadline => deadline.DueAt).First();
                    return CreateCourseAction(
                        priority: 20,
                        title: group.Count() == 1 ? "Việc sắp hết hạn" : $"{group.Count()} việc sắp hết hạn",
                        subtitle: BuildCourseSubtitle(nearest.CourseName, nearest.Title),
                        pageName: group.Key.PageName,
                        actionText: group.Key.ActionText,
                        toolId: group.Key.ToolId,
                        courseId: group.Key.CourseId,
                        courseName: nearest.CourseName,
                        tone: "Warning");
                }));

            actions.AddRange((todayClasses ?? Enumerable.Empty<StudentScheduleItemModel>())
                .Where(session => session.CourseId > 0)
                .GroupBy(session => session.CourseId)
                .Select(group =>
                {
                    StudentScheduleItemModel next = group.OrderBy(session => session.StartTime ?? System.DateTime.MaxValue).First();
                    return CreateCourseAction(
                        priority: 30,
                        title: "Vào lớp học hôm nay",
                        subtitle: BuildCourseSubtitle(next.CourseName, next.Title),
                        pageName: "Lịch học",
                        actionText: "Xem lịch",
                        toolId: "schedule",
                        courseId: group.Key,
                        courseName: next.CourseName,
                        tone: "Neutral");
                }));

            actions.AddRange(BuildCourseChatActions(unreadChatCourses, teacher: false));
            actions.AddRange(BuildCourseNotificationActions(notifications));

            return actions
                .OrderBy(action => action.Priority)
                .ToArray();
        }

        public static OverviewActionItem[] BuildTeacherCourseActions(
            IEnumerable<TeacherActiveExamSessionModel> activeSessions,
            IEnumerable<TeacherTeachingTaskModel> requiredTasks,
            IEnumerable<TeacherTeachingTaskModel> upcomingClasses,
            IEnumerable<ChatCourseModel> unreadChatCourses,
            IEnumerable<NotificationModel> notifications)
        {
            var actions = new List<OverviewActionItem>();

            actions.AddRange((activeSessions ?? Enumerable.Empty<TeacherActiveExamSessionModel>())
                .Where(session => session.CourseId > 0)
                .GroupBy(session => session.CourseId)
                .Select(group =>
                {
                    TeacherActiveExamSessionModel latest = group.OrderByDescending(session => session.StartTime).First();
                    return CreateCourseAction(
                        priority: 10,
                        title: group.Count() == 1 ? "Giám sát kỳ thi đang diễn ra" : $"Giám sát {group.Count()} phiên thi",
                        subtitle: BuildCourseSubtitle(latest.CourseName, latest.ExamTitle),
                        pageName: "Bài kiểm tra",
                        actionText: "Giám sát",
                        toolId: "exams",
                        courseId: group.Key,
                        courseName: latest.CourseName,
                        tone: "Warning");
                }));

            actions.AddRange((requiredTasks ?? Enumerable.Empty<TeacherTeachingTaskModel>())
                .Where(task => task.CourseId > 0 && !string.IsNullOrWhiteSpace(task.ToolId))
                .GroupBy(task => new { task.CourseId, task.ToolId, task.Category })
                .Select(group =>
                {
                    TeacherTeachingTaskModel next = group.OrderBy(task => task.DueAt ?? System.DateTime.MaxValue).First();
                    return CreateCourseAction(
                        priority: 20,
                        title: group.Count() == 1 ? "Xử lý việc cần chú ý" : $"{group.Count()} việc cần chú ý",
                        subtitle: BuildCourseSubtitle(next.Subtitle, next.Title),
                        pageName: string.IsNullOrWhiteSpace(next.Category) ? "Bài tập" : next.Category,
                        actionText: "Xử lý",
                        toolId: group.Key.ToolId,
                        courseId: group.Key.CourseId,
                        courseName: next.Subtitle,
                        tone: "Warning");
                }));

            actions.AddRange((upcomingClasses ?? Enumerable.Empty<TeacherTeachingTaskModel>())
                .Where(task => task.CourseId > 0)
                .GroupBy(task => task.CourseId)
                .Select(group =>
                {
                    TeacherTeachingTaskModel next = group.OrderBy(task => task.DueAt ?? System.DateTime.MaxValue).First();
                    return CreateCourseAction(
                        priority: 30,
                        title: "Mở buổi dạy sắp tới",
                        subtitle: BuildCourseSubtitle(next.Subtitle, next.Title),
                        pageName: "Lịch dạy",
                        actionText: "Xem lịch",
                        toolId: "schedule",
                        courseId: group.Key,
                        courseName: next.Subtitle,
                        tone: "Neutral");
                }));

            actions.AddRange(BuildCourseChatActions(unreadChatCourses, teacher: true));
            actions.AddRange(BuildCourseNotificationActions(notifications));

            return actions
                .OrderBy(action => action.Priority)
                .ToArray();
        }

        public static OverviewActionItem[] BuildCourseNotificationActions(IEnumerable<NotificationModel> notifications)
        {
            return (notifications ?? Enumerable.Empty<NotificationModel>())
                .Where(notification => !notification.IsRead && notification.CourseId.GetValueOrDefault() > 0)
                .GroupBy(notification => notification.CourseId!.Value)
                .Select(group =>
                {
                    NotificationModel latest = group.OrderByDescending(notification => notification.CreatedAt).First();
                    string courseName = FirstNonEmpty(latest.CourseName, group.Select(notification => notification.CourseName));
                    return new
                    {
                        LatestAt = latest.CreatedAt,
                        Action = CreateCourseAction(
                            priority: 41,
                            title: group.Count() == 1 ? "Xem thông báo mới" : $"Xem {group.Count()} thông báo mới",
                            subtitle: BuildCourseSubtitle(courseName, latest.Title),
                            pageName: "Thông báo",
                            actionText: "Mở thông báo",
                            toolId: "notifications",
                            courseId: group.Key,
                            courseName: courseName,
                            tone: "Neutral")
                    };
                })
                .OrderByDescending(item => item.LatestAt)
                .Select(item => item.Action)
                .ToArray();
        }

        public static OverviewActionItem[] BuildCourseChatActions(IEnumerable<ChatCourseModel> courses, bool teacher)
        {
            return (courses ?? Enumerable.Empty<ChatCourseModel>())
                .Where(course => course.CourseId > 0 && course.UnreadCount > 0)
                .Select(course => CreateCourseAction(
                    priority: 40,
                    title: teacher ? "Trả lời tin nhắn mới" : "Đọc tin nhắn mới",
                    subtitle: $"{course.UnreadCount} tin chưa đọc - {course.CourseName}",
                    pageName: "Tin nhắn",
                    actionText: "Mở tin nhắn",
                    toolId: string.Empty,
                    courseId: course.CourseId,
                    courseName: course.CourseName,
                    tone: "Neutral"))
                .OrderByDescending(action => ExtractLeadingCount(action.Subtitle))
                .ThenBy(action => action.CourseName)
                .ToArray();
        }

        private static OverviewActionItem CreateCourseAction(
            int priority,
            string title,
            string subtitle,
            string pageName,
            string actionText,
            string toolId,
            int courseId,
            string courseName,
            string tone)
        {
            return new OverviewActionItem
            {
                Priority = priority,
                Title = title,
                Subtitle = subtitle,
                PageName = pageName,
                ActionText = actionText,
                ToolId = toolId,
                CourseId = courseId,
                CourseName = courseName ?? string.Empty,
                Tone = tone
            };
        }

        private static string BuildCourseSubtitle(string courseName, string detail)
        {
            if (string.IsNullOrWhiteSpace(courseName))
                return detail ?? string.Empty;
            if (string.IsNullOrWhiteSpace(detail))
                return courseName.Trim();
            return $"{courseName.Trim()} - {detail.Trim()}";
        }

        private static string FirstNonEmpty(string preferred, IEnumerable<string> fallback)
        {
            if (!string.IsNullOrWhiteSpace(preferred))
                return preferred.Trim();

            return fallback.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
        }

        private static int ExtractLeadingCount(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            string firstToken = text.Trim().Split(' ').FirstOrDefault() ?? string.Empty;
            return int.TryParse(firstToken, out int count) ? count : 0;
        }
    }
}
