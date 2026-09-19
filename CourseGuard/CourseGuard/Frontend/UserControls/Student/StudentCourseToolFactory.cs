using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.UserControls.Shared.CourseWorkspace;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CourseGuard.Frontend.UserControls.Student;

public static class StudentCourseToolFactory
{
    public static IReadOnlyList<CourseToolDefinition> Create(Action<string, CourseWorkspaceContext> openDetail)
    {
        return Create(openDetail, 0, null!, null!, null);
    }

    public static IReadOnlyList<CourseToolDefinition> Create(
        Action<string, CourseWorkspaceContext> openDetail,
        int studentId,
        CourseController courseController,
        CourseGuardDbContext dbContext,
        NotificationRepository? notificationRepository = null)
    {
        ArgumentNullException.ThrowIfNull(openDetail);

        return new[]
        {
            CreateTool("materials", "Tài liệu", "materials", "Xem tài liệu của khóa học", "Vào trang Tài liệu", openDetail, studentId, courseController, dbContext, notificationRepository),
            CreateTool("schedule", "Lịch học", "schedule", "Xem lịch học và buổi học sắp tới", "Vào trang Lịch học", openDetail, studentId, courseController, dbContext, notificationRepository),
            CreateTool("lessons", "Bài học", "lessons", "Theo dõi bài học trong khóa", "Vào trang Bài học", openDetail, studentId, courseController, dbContext, notificationRepository),
            CreateTool("assignments", "Bài tập", "assignments", "Xem bài tập và hạn nộp", "Vào trang Bài tập", openDetail, studentId, courseController, dbContext, notificationRepository),
            CreateTool("exams", "Bài kiểm tra", "exams", "Xem bài kiểm tra trong khóa", "Vào trang Bài kiểm tra", openDetail, studentId, courseController, dbContext, notificationRepository),
            CreateTool("results", "Kết quả", "results", "Xem điểm và kết quả học tập", "Vào trang Kết quả", openDetail, studentId, courseController, dbContext, notificationRepository),
            CreateTool("notifications", "Thông báo", "notifications", "Xem thông báo liên quan khóa học", "Vào trang Thông báo", openDetail, studentId, courseController, dbContext, notificationRepository)
        };
    }

    private static CourseToolDefinition CreateTool(
        string id,
        string label,
        string iconKey,
        string description,
        string primaryActionLabel,
        Action<string, CourseWorkspaceContext> openDetail,
        int studentId,
        CourseController courseController,
        CourseGuardDbContext dbContext,
        NotificationRepository? notificationRepository)
    {
        return new CourseToolDefinition(
            id,
            label,
            iconKey,
            description,
            context => CreatePreview(
                context,
                label,
                description,
                primaryActionLabel,
                BuildStudentPreviewRows(id, context, studentId, courseController, dbContext, notificationRepository)),
            context => openDetail(id, context));
    }

    private static IReadOnlyList<string> BuildStudentPreviewRows(
        string toolId,
        CourseWorkspaceContext context,
        int studentId,
        CourseController courseController,
        CourseGuardDbContext dbContext,
        NotificationRepository? notificationRepository)
    {
        List<string> rows = BuildBaseRows(context);
        if (studentId <= 0 || dbContext == null)
            return rows;

        try
        {
            switch (toolId)
            {
                case "lessons":
                {
                    List<TeacherLessonModel> lessons = RunBackground(() => dbContext.GetStudentLessonsAsync(studentId))
                        .Where(row => row.CourseId == context.CourseId && row.Id > 0)
                        .ToList();
                    rows.Add($"Bài học đã đăng: {lessons.Count}");
                    AddLatest(rows, "Mới nhất", lessons.OrderByDescending(row => row.PublishAt ?? DateTime.MinValue).FirstOrDefault()?.Title);
                    break;
                }
                case "materials":
                {
                    List<TeacherLessonModel> materials = RunBackground(() => dbContext.GetStudentLessonsAsync(studentId))
                        .Where(row => row.CourseId == context.CourseId && row.Id < 0)
                        .ToList();
                    rows.Add($"Tài liệu: {materials.Count}");
                    AddLatest(rows, "Mới nhất", materials.OrderByDescending(row => row.PublishAt ?? DateTime.MinValue).FirstOrDefault()?.Title);
                    break;
                }
                case "assignments":
                {
                    List<StudentAssignmentRow> assignments = RunBackground(() => dbContext.GetStudentAssignmentsAsync(studentId))
                        .Where(row => row.CourseId == context.CourseId)
                        .ToList();
                    int pending = assignments.Count(row => !row.IsSubmitted);
                    rows.Add($"Bài tập: {assignments.Count}");
                    rows.Add($"Chưa nộp: {pending}");
                    StudentAssignmentRow? next = assignments
                        .Where(row => !row.IsSubmitted)
                        .OrderBy(row => row.DueDate)
                        .FirstOrDefault();
                    if (next != null)
                        rows.Add($"Hạn gần nhất: {next.Title} - {FormatDate(next.DueDate)}");
                    break;
                }
                case "exams":
                {
                    List<StudentExamListItemModel> exams = dbContext.GetAvailableExamsForStudent(studentId)
                        .Where(row => row.CourseId == context.CourseId)
                        .ToList();
                    rows.Add($"Bài kiểm tra khả dụng: {exams.Count}");
                    AddLatest(rows, "Sắp tới", exams.OrderBy(row => row.OpenTime ?? DateTime.MaxValue).FirstOrDefault()?.Title);
                    break;
                }
                case "results":
                {
                    List<StudentResultListItemModel> results = dbContext.GetStudentResultItems(studentId, context.CourseId, null, 20);
                    rows.Add($"Kết quả đã có: {results.Count}");
                    StudentResultListItemModel? latest = results.FirstOrDefault();
                    if (latest != null)
                        rows.Add($"Gần nhất: {latest.ExamTitle} - {latest.Score:0.#}");
                    break;
                }
                case "schedule":
                {
                    List<StudentScheduleItemModel> sessions = (courseController == null
                            ? new List<StudentScheduleItemModel>()
                            : courseController.GetStudentOnlineSessions(studentId))
                        .Where(row => row.CourseId == context.CourseId)
                        .ToList();
                    rows.Add($"Buổi học: {sessions.Count}");
                    StudentScheduleItemModel? next = sessions
                        .Where(row => !row.StartTime.HasValue || row.StartTime.Value >= DateTime.Now)
                        .OrderBy(row => row.StartTime ?? DateTime.MaxValue)
                        .FirstOrDefault();
                    if (next != null)
                        rows.Add($"Buổi tới: {next.Title} - {FormatDate(next.StartTime)}");
                    break;
                }
                case "notifications":
                {
                    notificationRepository ??= new NotificationRepository();
                    List<NotificationModel> notifications = notificationRepository.LoadByUserId(studentId)
                        .Where(row => IsCourseNotification(row, context.CourseId))
                        .ToList();
                    rows.Add($"Thông báo liên quan: {notifications.Count}");
                    rows.Add($"Chưa đọc: {notifications.Count(row => !row.IsRead)}");
                    AddLatest(rows, "Mới nhất", notifications.FirstOrDefault()?.Title);
                    break;
                }
            }
        }
        catch
        {
            rows.Add("Chưa tải được dữ liệu xem trước.");
        }

        return rows;
    }

    private static T RunBackground<T>(Func<Task<T>> work)
    {
        return Task.Run(work).GetAwaiter().GetResult();
    }

    private static Control CreatePreview(
        CourseWorkspaceContext context,
        string label,
        string description,
        string primaryActionLabel,
        IReadOnlyList<string> rows)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            FlowDirection = FlowDirection.TopDown,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Tag = new CourseToolPreview(label, description, rows, primaryActionLabel),
            WrapContents = false
        };

        return panel;
    }

    private static List<string> BuildBaseRows(CourseWorkspaceContext context)
    {
        return new List<string>
        {
            $"Khóa học: {context.CourseName}",
            $"Mã/Lớp: {context.SecondaryText}",
            $"Trạng thái: {context.Status}"
        };
    }

    private static bool IsCourseNotification(NotificationModel notification, int courseId)
    {
        return notification.CourseId == courseId
            || (notification.SourceId == courseId
                && string.Equals(notification.SourceType, "Course", StringComparison.OrdinalIgnoreCase));
    }

    private static void AddLatest(List<string> rows, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            rows.Add($"{label}: {value.Trim()}");
    }

    private static string FormatDate(DateTime? value)
    {
        return value.HasValue ? value.Value.ToString("dd/MM/yyyy HH:mm") : "Chưa có lịch";
    }
}
