using System;
using System.Collections.Generic;
using System.Drawing;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.Helpers
{
    public enum ActivityDisplayContext
    {
        Student,
        Teacher
    }

    public static class ActivityDisplayHelper
    {
        public static string TranslateActivity(
            RecentUserActivityModel activity,
            ActivityDisplayContext context = ActivityDisplayContext.Student)
        {
            string action = (activity.Action ?? string.Empty).ToUpperInvariant();
            string title = context == ActivityDisplayContext.Teacher
                ? TranslateTeacherActivity(action)
                : TranslateStudentActivity(action);

            string details = CleanDetails(activity.Details);
            return string.IsNullOrWhiteSpace(details) ? title : $"{title} - {details}";
        }

        private static string TranslateStudentActivity(string action)
        {
            return action switch
            {
                "LOGIN" => "Đăng nhập hệ thống",
                "LOGOUT" => "Đăng xuất hệ thống",
                "COURSE_ENROLL_REQUEST" => "Gửi yêu cầu tham gia khóa học",
                "COURSE_ENROLL" => "Được duyệt vào khóa học",
                "ONLINE_SESSION_JOIN" => "Tham gia lớp học trực tuyến",
                "ONLINE_SESSION_EXIT" => "Rời lớp học trực tuyến",
                "EXAM_JOIN" => "Bắt đầu làm bài kiểm tra",
                "EXAM_SUBMIT" => "Nộp bài kiểm tra",
                "EXAM_EXIT" => "Thoát màn hình làm bài kiểm tra",
                "CHANGE_PASSWORD" => "Đổi mật khẩu",
                "CHAT_USE" => "Trao đổi trong lớp học",
                _ => "Cập nhật hoạt động"
            };
        }

        private static string TranslateTeacherActivity(string action)
        {
            return action switch
            {
                "LOGIN" => "Đã đăng nhập vào hệ thống",
                "LOGOUT" => "Đã đăng xuất khỏi hệ thống",
                "CHANGE_PASSWORD" => "Đã đổi mật khẩu",
                "CHAT_USE" => "Đã trao đổi với học viên",
                "COURSE_ENROLL_REQUEST" => "Gửi yêu cầu tham gia khóa học",
                "COURSE_ENROLL" => "Được duyệt vào khóa học",
                "ONLINE_SESSION_JOIN" => "Tham gia lớp học trực tuyến",
                "ONLINE_SESSION_EXIT" => "Rời lớp học trực tuyến",
                "EXAM_JOIN" => "Bắt đầu làm bài kiểm tra",
                "EXAM_SUBMIT" => "Nộp bài kiểm tra",
                "EXAM_EXIT" => "Thoát màn hình làm bài kiểm tra",
                "MATERIAL_UPLOAD" => "Đã tải lên tài liệu mới",
                "ASSIGNMENT_CREATE" => "Đã tạo bài tập mới",
                "LESSON_CREATE" => "Đã tạo bài học/nội dung mới",
                _ => "Đã cập nhật hoạt động giảng dạy"
            };
        }

        public static string CleanDetails(string? details)
        {
            if (string.IsNullOrWhiteSpace(details))
                return string.Empty;

            string value = details.Trim();
            return value.Length > 72 ? value[..72] + "..." : value;
        }

        public static int SafeMetricCount(Func<int> getter)
        {
            try
            {
                return Math.Max(0, getter());
            }
            catch
            {
                return 0;
            }
        }

        public static double? SafeAverageScore(Func<double?> getter)
        {
            try
            {
                return getter();
            }
            catch
            {
                return null;
            }
        }

        public static List<T> SafeList<T>(Func<List<T>> getter)
        {
            try
            {
                return getter() ?? new List<T>();
            }
            catch
            {
                return new List<T>();
            }
        }

        public static Color GetActivityAccent(string? action)
        {
            return (action ?? string.Empty).ToUpperInvariant() switch
            {
                "LOGIN" or "COURSE_ENROLL" or "EXAM_SUBMIT" or "CHANGE_PASSWORD" or "MATERIAL_UPLOAD" or "ASSIGNMENT_CREATE" or "LESSON_CREATE" => AppColors.Success,
                "COURSE_ENROLL_REQUEST" or "EXAM_JOIN" or "ONLINE_SESSION_JOIN" or "CHAT_USE" => AppColors.Warning,
                "LOGOUT" or "EXAM_EXIT" or "ONLINE_SESSION_EXIT" => AppColors.TextMuted,
                _ => AppColors.AccentBlue
            };
        }
    }
}
