using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.UserControls.Shared.CourseWorkspace;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CourseGuard.Frontend.UserControls.Teacher;

public static class TeacherCourseToolFactory
{
    public static IReadOnlyList<CourseToolDefinition> Create(Action<string, CourseWorkspaceContext> openDetail)
    {
        return Create(openDetail, 0, null!);
    }

    public static IReadOnlyList<CourseToolDefinition> Create(
        Action<string, CourseWorkspaceContext> openDetail,
        int teacherId,
        TeacherController teacherController)
    {
        ArgumentNullException.ThrowIfNull(openDetail);

        return new[]
        {
            CreateTool("lessons", "Bài học", "lessons", "Quản lý bài học của khóa dạy", "Vào trang Bài học", openDetail, teacherId, teacherController),
            CreateTool("materials", "Tài liệu", "materials", "Quản lý tài liệu của khóa dạy", "Vào trang Tài liệu", openDetail, teacherId, teacherController),
            CreateTool("assignments", "Bài tập", "assignments", "Quản lý bài tập và hạn nộp", "Vào trang Bài tập", openDetail, teacherId, teacherController),
            CreateTool("exams", "Bài kiểm tra", "exams", "Quản lý bài kiểm tra trong khóa", "Vào trang Bài kiểm tra", openDetail, teacherId, teacherController),
            CreateTool("results", "Kết quả", "results", "Xem điểm và kết quả học tập", "Vào trang Kết quả", openDetail, teacherId, teacherController),
            CreateTool("students", "Sinh viên", "students", "Theo dõi sinh viên trong khóa", "Vào trang Sinh viên", openDetail, teacherId, teacherController),
            CreateTool("schedule", "Lịch dạy", "schedule", "Xem lịch dạy và buổi học sắp tới", "Vào trang Lịch dạy", openDetail, teacherId, teacherController),
            CreateTool("notifications", "Thông báo", "notifications", "Xem thông báo liên quan khóa dạy", "Vào trang Thông báo", openDetail, teacherId, teacherController)
        };
    }

    private static CourseToolDefinition CreateTool(
        string id,
        string label,
        string iconKey,
        string description,
        string primaryActionLabel,
        Action<string, CourseWorkspaceContext> openDetail,
        int teacherId,
        TeacherController teacherController)
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
                BuildTeacherPreviewRows(id, context, teacherId, teacherController)),
            context => openDetail(id, context));
    }

    private static IReadOnlyList<string> BuildTeacherPreviewRows(
        string toolId,
        CourseWorkspaceContext context,
        int teacherId,
        TeacherController teacherController)
    {
        List<string> rows = BuildBaseRows(context);
        if (teacherId <= 0 || teacherController == null)
            return rows;

        try
        {
            switch (toolId)
            {
                case "lessons":
                {
                    List<TeacherLessonModel> lessons = teacherController.GetLessons(teacherId)
                        .Where(row => row.CourseId == context.CourseId)
                        .ToList();
                    rows.Add($"Bài học: {lessons.Count}");
                    AddLatest(rows, "Mới nhất", lessons.OrderByDescending(row => row.PublishAt ?? DateTime.MinValue).FirstOrDefault()?.Title);
                    break;
                }
                case "materials":
                {
                    List<TeacherMaterialModel> materials = teacherController.GetMaterials(teacherId, context.CourseId);
                    rows.Add($"Tài liệu: {materials.Count}");
                    AddLatest(rows, "Mới nhất", materials.OrderByDescending(row => row.UploadedAt).FirstOrDefault()?.FileName);
                    break;
                }
                case "assignments":
                {
                    List<TeacherAssignmentModel> assignments = teacherController.GetAssignments(teacherId)
                        .Where(row => row.CourseId == context.CourseId)
                        .ToList();
                    rows.Add($"Bài tập: {assignments.Count}");
                    rows.Add($"Đang mở: {assignments.Count(row => string.Equals(row.Status, "OPEN", StringComparison.OrdinalIgnoreCase))}");
                    AddLatest(rows, "Hạn gần nhất", assignments.OrderBy(row => row.DueAt ?? DateTime.MaxValue).FirstOrDefault()?.Title);
                    break;
                }
                case "exams":
                {
                    List<TeacherExamModel> exams = teacherController.GetExams(teacherId)
                        .Where(row => row.CourseId == context.CourseId)
                        .ToList();
                    rows.Add($"Bài kiểm tra: {exams.Count}");
                    rows.Add($"Đã kích hoạt: {exams.Count(row => string.Equals(row.Status, WorkflowConstants.ExamStatus.Active, StringComparison.OrdinalIgnoreCase))}");
                    AddLatest(rows, "Gần nhất", exams.OrderBy(row => row.OpenTime ?? DateTime.MaxValue).FirstOrDefault()?.Title);
                    break;
                }
                case "results":
                {
                    List<TeacherScoreModel> results = teacherController.GetResults(teacherId, context.CourseId);
                    rows.Add($"Bài nộp có điểm: {results.Count}");
                    TeacherScoreModel? latest = results.OrderByDescending(row => row.SubmitTime ?? DateTime.MinValue).FirstOrDefault();
                    if (latest != null)
                        rows.Add($"Gần nhất: {latest.StudentName} - {latest.ExamTitle}");
                    break;
                }
                case "students":
                {
                    List<TeacherStudentModel> students = teacherController.GetEnrolledStudents(teacherId, context.CourseId);
                    rows.Add($"Sinh viên đang học: {students.Count}");
                    AddLatest(rows, "Mới nhất", students.OrderByDescending(row => row.JoinedAt).FirstOrDefault()?.StudentName);
                    break;
                }
                case "schedule":
                {
                    List<TeacherScheduleItemModel> sessions = teacherController.GetSchedule(teacherId)
                        .Where(row => row.CourseId == context.CourseId)
                        .ToList();
                    rows.Add($"Buổi dạy: {sessions.Count}");
                    TeacherScheduleItemModel? next = sessions
                        .Where(row => !row.StartTime.HasValue || row.StartTime.Value >= DateTime.Now)
                        .OrderBy(row => row.StartTime ?? DateTime.MaxValue)
                        .FirstOrDefault();
                    if (next != null)
                        rows.Add($"Buổi tới: {next.Title} - {FormatDate(next.StartTime)}");
                    break;
                }
                case "notifications":
                    rows.Add("Thông báo: mở danh sách thông báo của khóa dạy");
                    break;
            }
        }
        catch
        {
            rows.Add("Chưa tải được dữ liệu xem trước.");
        }

        return rows;
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
            $"Khóa dạy: {context.CourseName}",
            $"Mã/Lớp: {context.SecondaryText}",
            $"Trạng thái: {context.Status}"
        };
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
