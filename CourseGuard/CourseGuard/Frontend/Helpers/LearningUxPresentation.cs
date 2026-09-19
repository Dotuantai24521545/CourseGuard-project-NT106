using System;
using System.Collections.Generic;
using System.Globalization;
using CourseGuard.Backend.Models;

namespace CourseGuard.Frontend.Helpers
{
    public sealed class LearningUxPresentation
    {
        public string StatusText { get; init; } = string.Empty;
        public string DetailText { get; init; } = string.Empty;
        public string PrimaryActionText { get; init; } = string.Empty;
        public string SecondaryActionText { get; init; } = string.Empty;
        public string Tone { get; init; } = "Neutral";
        public bool CanUsePrimaryAction { get; init; }
        public bool CanDownload { get; init; }
        public string NextActionKind { get; init; } = NextActionKinds.None;
        public string TargetTab { get; init; } = NavigationTargets.None;
        public int? TargetCourseId { get; init; }
        public int? TargetItemId { get; init; }
        public int? TargetSessionId { get; init; }
    }

    public static class StudentCourseUxPresenter
    {
        public static LearningUxPresentation PresentAvailableCourse(CourseModel course, int enrolledCount, DateTime now)
        {
            return new LearningUxPresentation
            {
                StatusText = LearningUxFormatting.RenderCourseStatus(course.Status),
                DetailText = $"{LearningUxFormatting.FormatDateRange(course.StartDate, course.EndDate)} - {enrolledCount} học viên đã đăng ký.",
                PrimaryActionText = "Gửi yêu cầu",
                SecondaryActionText = "Xem chi tiết",
                Tone = "Info",
                CanUsePrimaryAction = true,
                NextActionKind = NextActionKinds.RequestCourseEnrollment,
                TargetTab = NavigationTargets.StudentCourseList,
                TargetCourseId = course.Id
            };
        }

        public static LearningUxPresentation PresentEnrollment(EnrollmentModel enrollment, DateTime now)
        {
            string status = Normalize(enrollment.Status);
            return status switch
            {
                WorkflowConstants.EnrollmentStatus.Active or WorkflowConstants.EnrollmentStatus.Approved => new LearningUxPresentation
                {
                    StatusText = "Đang học",
                    DetailText = $"{enrollment.TeacherName} - {LearningUxFormatting.FormatDateRange(enrollment.CourseStartDate, enrollment.CourseEndDate)}.",
                    PrimaryActionText = "Vào trang Bài học",
                    SecondaryActionText = "Xem chi tiết",
                    Tone = "Success",
                    CanUsePrimaryAction = false,
                    NextActionKind = NextActionKinds.OpenStudentLessonsTab,
                    TargetTab = NavigationTargets.StudentLessons,
                    TargetCourseId = enrollment.CourseId
                },
                WorkflowConstants.EnrollmentStatus.Pending => new LearningUxPresentation
                {
                    StatusText = "Chờ duyệt",
                    DetailText = "Yêu cầu tham gia đã được gửi. Bạn có thể theo dõi trạng thái tại đây.",
                    PrimaryActionText = "Chờ giảng viên/Admin duyệt",
                    SecondaryActionText = "Xem chi tiết",
                    Tone = "Warning",
                    TargetCourseId = enrollment.CourseId
                },
                WorkflowConstants.EnrollmentStatus.Dropped => new LearningUxPresentation
                {
                    StatusText = "Đã rút",
                    DetailText = "Bạn đã rút khỏi khóa học này.",
                    PrimaryActionText = "Đã rút",
                    SecondaryActionText = "Xem chi tiết",
                    Tone = "Muted",
                    TargetCourseId = enrollment.CourseId
                },
                "COMPLETED" => new LearningUxPresentation
                {
                    StatusText = "Hoàn thành",
                    DetailText = $"{enrollment.TeacherName} - {LearningUxFormatting.FormatDateRange(enrollment.CourseStartDate, enrollment.CourseEndDate)}.",
                    PrimaryActionText = "Xem lại",
                    SecondaryActionText = "Xem chi tiết",
                    Tone = "Success",
                    CanUsePrimaryAction = false,
                    NextActionKind = NextActionKinds.OpenStudentLessonsTab,
                    TargetTab = NavigationTargets.StudentLessons,
                    TargetCourseId = enrollment.CourseId
                },
                _ => new LearningUxPresentation
                {
                    StatusText = string.IsNullOrWhiteSpace(enrollment.Status) ? "Không rõ" : enrollment.Status,
                    DetailText = $"{enrollment.TeacherName} - {LearningUxFormatting.FormatDateRange(enrollment.CourseStartDate, enrollment.CourseEndDate)}.",
                    PrimaryActionText = "Xem chi tiết",
                    SecondaryActionText = "Xem chi tiết",
                    Tone = "Neutral",
                    TargetCourseId = enrollment.CourseId
                }
            };
        }

        private static string Normalize(string? status)
        {
            return (status ?? string.Empty).Trim().ToUpperInvariant();
        }
    }

    public static class StudentLearningItemUxPresenter
    {
        public static LearningUxPresentation Present(TeacherLessonModel item)
        {
            bool isMaterial = item.Id < 0;
            bool canDownload = item.HasStoredContent || !string.IsNullOrWhiteSpace(item.FilePath);

            return new LearningUxPresentation
            {
                StatusText = isMaterial ? "Tài liệu" : "Bài học",
                DetailText = LearningUxFormatting.BuildContentDetail(item.CourseName, item.PublishAt, item.FileName, item.FileSize),
                PrimaryActionText = isMaterial ? "Tải tài liệu" : "Xem bài học",
                SecondaryActionText = "Xem chi tiết",
                Tone = isMaterial ? "Info" : "Success",
                CanUsePrimaryAction = isMaterial ? canDownload : true,
                CanDownload = canDownload,
                NextActionKind = isMaterial ? NextActionKinds.DownloadCurrentMaterial : NextActionKinds.OpenCurrentLesson,
                TargetTab = NavigationTargets.StudentLessons,
                TargetCourseId = item.CourseId,
                TargetItemId = Math.Abs(item.Id)
            };
        }

        public static LearningUxPresentation PresentMaterial(
            string courseName,
            string fileName,
            DateTime uploadedAt,
            long fileSize,
            bool hasStoredContent)
        {
            return new LearningUxPresentation
            {
                StatusText = "Tài liệu",
                DetailText = $"{courseName} - tải lên {LearningUxFormatting.FormatDateTimeOrPending(uploadedAt)} - {fileName} ({LearningUxFormatting.FormatSize(fileSize)}).",
                PrimaryActionText = hasStoredContent ? "Tải xuống" : "Mở đường dẫn",
                SecondaryActionText = "Xem chi tiết",
                Tone = "Info",
                CanUsePrimaryAction = true,
                CanDownload = hasStoredContent,
                NextActionKind = NextActionKinds.DownloadCurrentMaterial,
                TargetTab = NavigationTargets.StudentDocuments
            };
        }
    }

    public static class TeacherCourseUxPresenter
    {
        public static LearningUxPresentation Present(TeacherCourseModel course)
        {
            string status = Normalize(course.Status);
            return status switch
            {
                WorkflowConstants.CourseStatus.Draft => new LearningUxPresentation
                {
                    StatusText = "Bản nháp",
                    DetailText = LearningUxFormatting.BuildTeacherCourseDetail(course),
                    PrimaryActionText = "Gửi duyệt",
                    SecondaryActionText = "Xem chi tiết",
                    Tone = "Info",
                    CanUsePrimaryAction = true,
                    NextActionKind = NextActionKinds.SubmitCourseApproval,
                    TargetTab = NavigationTargets.TeacherCourses,
                    TargetCourseId = course.Id
                },
                WorkflowConstants.CourseStatus.Active => new LearningUxPresentation
                {
                    StatusText = "Đang dạy",
                    DetailText = LearningUxFormatting.BuildTeacherCourseDetail(course),
                    PrimaryActionText = "Quản lý nội dung",
                    SecondaryActionText = "Xem chi tiết",
                    Tone = "Success",
                    CanUsePrimaryAction = false,
                    NextActionKind = NextActionKinds.OpenTeacherLessonsTab,
                    TargetTab = NavigationTargets.TeacherLessons,
                    TargetCourseId = course.Id
                },
                WorkflowConstants.CourseStatus.Rejected => new LearningUxPresentation
                {
                    StatusText = "Bị từ chối",
                    DetailText = string.IsNullOrWhiteSpace(course.RejectionReason)
                        ? "Khóa học cần được cập nhật trước khi gửi duyệt lại."
                        : course.RejectionReason,
                    PrimaryActionText = "Cập nhật và gửi lại",
                    SecondaryActionText = "Xem chi tiết",
                    Tone = "Danger",
                    CanUsePrimaryAction = true,
                    NextActionKind = NextActionKinds.SubmitCourseApproval,
                    TargetTab = NavigationTargets.TeacherCourses,
                    TargetCourseId = course.Id
                },
                WorkflowConstants.CourseStatus.Pending => new LearningUxPresentation
                {
                    StatusText = "Chờ duyệt",
                    DetailText = LearningUxFormatting.BuildTeacherCourseDetail(course),
                    PrimaryActionText = "Chờ duyệt",
                    SecondaryActionText = "Xem chi tiết",
                    Tone = "Warning",
                    TargetTab = NavigationTargets.TeacherCourses,
                    TargetCourseId = course.Id
                },
                _ => new LearningUxPresentation
                {
                    StatusText = string.IsNullOrWhiteSpace(course.Status) ? "Không rõ" : course.Status,
                    DetailText = LearningUxFormatting.BuildTeacherCourseDetail(course),
                    PrimaryActionText = "Xem chi tiết",
                    SecondaryActionText = "Xem chi tiết",
                    Tone = "Neutral",
                    TargetTab = NavigationTargets.TeacherCourses,
                    TargetCourseId = course.Id
                }
            };
        }

        private static string Normalize(string? status)
        {
            return (status ?? string.Empty).Trim().ToUpperInvariant();
        }
    }

    public static class TeacherContentUxPresenter
    {
        public static LearningUxPresentation PresentLesson(TeacherLessonModel lesson)
        {
            bool hasFile = lesson.HasStoredContent && !string.IsNullOrWhiteSpace(lesson.FileName);

            return new LearningUxPresentation
            {
                StatusText = string.IsNullOrWhiteSpace(lesson.Status) ? "Không rõ" : lesson.Status,
                DetailText = hasFile
                    ? LearningUxFormatting.BuildContentDetail(lesson.CourseName, lesson.PublishAt, lesson.FileName, lesson.FileSize)
                    : $"{lesson.CourseName} - {LearningUxFormatting.FormatDateTimeOrPending(lesson.PublishAt)} - không có giáo trình.",
                PrimaryActionText = hasFile ? "Tải giáo trình" : "Chưa có file",
                SecondaryActionText = "Xem chi tiết",
                Tone = hasFile ? "Info" : "Muted",
                CanUsePrimaryAction = hasFile,
                CanDownload = hasFile,
                NextActionKind = hasFile ? NextActionKinds.DownloadTeacherLessonFile : NextActionKinds.None,
                TargetTab = NavigationTargets.TeacherLessons,
                TargetCourseId = lesson.CourseId,
                TargetItemId = lesson.Id
            };
        }

        public static LearningUxPresentation PresentMaterial(TeacherMaterialModel material)
        {
            bool hasStoredContent = material.HasStoredContent;

            return new LearningUxPresentation
            {
                StatusText = hasStoredContent ? "Lưu trong hệ thống" : "Đường dẫn",
                DetailText = $"{material.CourseName} - {material.FileName} ({LearningUxFormatting.FormatSize(material.FileSize)}).",
                PrimaryActionText = hasStoredContent ? "Tải xuống" : "Mở đường dẫn",
                SecondaryActionText = "Xem chi tiết",
                Tone = hasStoredContent ? "Info" : "Neutral",
                CanUsePrimaryAction = hasStoredContent || !string.IsNullOrWhiteSpace(material.FilePath),
                CanDownload = hasStoredContent,
                NextActionKind = hasStoredContent ? NextActionKinds.DownloadTeacherMaterial : NextActionKinds.None,
                TargetTab = NavigationTargets.TeacherMaterials,
                TargetCourseId = material.CourseId,
                TargetItemId = material.Id
            };
        }
    }

    internal static class LearningUxFormatting
    {
        public static string RenderCourseStatus(string? status)
        {
            return Normalize(status) switch
            {
                WorkflowConstants.CourseStatus.Draft => "Bản nháp",
                WorkflowConstants.CourseStatus.Pending => "Chờ duyệt",
                WorkflowConstants.CourseStatus.Active => "Đang mở",
                WorkflowConstants.CourseStatus.Rejected => "Bị từ chối",
                WorkflowConstants.CourseStatus.Closed => "Đã đóng",
                _ => string.IsNullOrWhiteSpace(status) ? "Không rõ" : status!
            };
        }

        public static string FormatDateRange(DateTime start, DateTime end)
        {
            return FormatDateRange((DateTime?)start, end);
        }

        public static string FormatDateRange(DateTime? start, DateTime? end)
        {
            bool missingStart = IsMissingDate(start);
            bool missingEnd = IsMissingDate(end);

            if (missingStart && missingEnd)
                return "chưa có ngày bắt đầu - chưa có ngày kết thúc";

            if (missingStart)
                return $"chưa có ngày bắt đầu - đến {FormatDate(end!.Value)}";

            if (missingEnd)
                return $"Từ {FormatDate(start!.Value)} - chưa có ngày kết thúc";

            return $"{FormatDate(start!.Value)} - {FormatDate(end!.Value)}";
        }

        public static string FormatDateTime(DateTime value)
        {
            return value.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
        }

        public static string FormatDateTimeOrPending(DateTime? value)
        {
            return IsMissingDate(value) ? "chưa rõ" : FormatDateTime(value!.Value);
        }

        public static string FormatSize(long bytes)
        {
            if (bytes <= 0)
                return "không rõ dung lượng";

            if (bytes < 1024)
                return $"{bytes} B";

            double kilobytes = bytes / 1024d;
            if (kilobytes < 1024)
                return $"{FormatNumber(kilobytes)} KB";

            double megabytes = kilobytes / 1024d;
            return $"{FormatNumber(megabytes)} MB";
        }

        public static string BuildContentDetail(string courseName, DateTime? publishAt, string? fileName, long? fileSize)
        {
            List<string> parts = new();
            if (!string.IsNullOrWhiteSpace(courseName))
                parts.Add(courseName);

            parts.Add(FormatDateTimeOrPending(publishAt));

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                string fileText = fileName;
                if (fileSize.HasValue)
                    fileText += $" ({FormatSize(fileSize.Value)})";
                parts.Add(fileText);
            }

            return string.Join(" - ", parts) + ".";
        }

        public static string BuildTeacherCourseDetail(TeacherCourseModel course)
        {
            return $"{FormatDateRange(course.StartDate, course.EndDate)} - {course.StudentCount} học viên.";
        }

        private static string FormatDate(DateTime value)
        {
            return value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        private static bool IsMissingDate(DateTime? value)
        {
            return !value.HasValue || value.Value == DateTime.MinValue;
        }

        private static string FormatNumber(double value)
        {
            return value % 1 == 0
                ? value.ToString("0", CultureInfo.InvariantCulture)
                : value.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private static string Normalize(string? status)
        {
            return (status ?? string.Empty).Trim().ToUpperInvariant();
        }
    }
}
