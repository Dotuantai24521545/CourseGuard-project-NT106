using System;
using System.Collections.Generic;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;

namespace CourseGuard.Backend.Controllers
{
    public class CourseController
    {
        private readonly CourseGuardDbContext _dbContext;
        private readonly NotificationRepository _notifications = new();

        public CourseController(CourseGuardDbContext dbContext)
        {
            _dbContext = dbContext;
            _dbContext.EnsureCourseWorkflowSchema();
        }

        // ════════════════════════════════════════════════════════════════
        //  ADMIN-ONLY METHODS (giữ nguyên)
        // ════════════════════════════════════════════════════════════════

        public List<CourseModel> GetAllCourses()
        {
            return _dbContext.GetAllCourses();
        }

        public string AddCourse(CourseModel course)
        {
            if (!UserSessionContext.IsAdmin())
            {
                return "Forbidden";
            }

            if (course.GenerateSchedule && !string.IsNullOrEmpty(course.TeachingDays) && course.SessionStartTime.HasValue && course.SessionEndTime.HasValue)
            {
                if (_dbContext.HasTeacherConflict(course.TeacherId, course.StartDate, course.EndDate, course.TeachingDays, course.SessionStartTime.Value, course.SessionEndTime.Value))
                {
                    return "Giáo viên đã có lịch dạy vào thời gian này.";
                }
            }

            try
            {
                _dbContext.InsertCourse(course);
                return "Success";
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }

        public bool UpdateCourse(CourseModel course)
        {
            if (!UserSessionContext.IsAdmin())
            {
                return false;
            }

            if (course.GenerateSchedule && !string.IsNullOrEmpty(course.TeachingDays) && course.SessionStartTime.HasValue && course.SessionEndTime.HasValue)
            {
                if (_dbContext.HasTeacherConflict(course.TeacherId, course.StartDate, course.EndDate, course.TeachingDays, course.SessionStartTime.Value, course.SessionEndTime.Value, course.Id))
                {
                    throw new InvalidOperationException("Giáo viên đã có lịch dạy vào thời gian này.");
                }
            }

            try
            {
                _dbContext.UpdateCourse(course);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool DeleteCourse(int courseId)
        {
            if (!UserSessionContext.IsAdmin())
            {
                return false;
            }

            try
            {
                _dbContext.DeleteCourse(courseId);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool ApproveCourse(int courseId)
        {
            if (!UserSessionContext.IsAdmin())
                return false;

            CourseModel? course = _dbContext.GetCourseById(courseId);
            if (course == null || !string.Equals(course.Status, WorkflowConstants.CourseStatus.Pending, StringComparison.OrdinalIgnoreCase))
                return false;

            course.Status = WorkflowConstants.CourseStatus.Active;
            course.RejectionReason = string.Empty;
            try
            {
                _dbContext.UpdateCourse(course);
                _notifications.Create(
                    course.TeacherId,
                    "Khóa học đã được duyệt",
                    $"Khóa học \"{course.Name}\" đã được Admin duyệt và sinh viên có thể tìm thấy.",
                    WorkflowConstants.NotificationCategory.SystemAdmin,
                    WorkflowConstants.NotificationType.Informational,
                    "Course",
                    course.Id);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool RejectCourse(int courseId, string reason)
        {
            if (!UserSessionContext.IsAdmin())
                return false;

            CourseModel? course = _dbContext.GetCourseById(courseId);
            if (course == null || !string.Equals(course.Status, WorkflowConstants.CourseStatus.Pending, StringComparison.OrdinalIgnoreCase))
                return false;

            course.Status = WorkflowConstants.CourseStatus.Rejected;
            course.RejectionReason = reason?.Trim() ?? string.Empty;
            try
            {
                _dbContext.UpdateCourse(course);
                string detail = string.IsNullOrWhiteSpace(course.RejectionReason)
                    ? $"Khóa học \"{course.Name}\" đã bị Admin từ chối."
                    : $"Khóa học \"{course.Name}\" đã bị Admin từ chối. Lý do: {course.RejectionReason}";
                _notifications.Create(
                    course.TeacherId,
                    "Khóa học bị từ chối",
                    detail,
                    WorkflowConstants.NotificationCategory.SystemAdmin,
                    WorkflowConstants.NotificationType.Informational,
                    "Course",
                    course.Id);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool EnrollStudent(int courseId, int studentId)
        {
            if (!UserSessionContext.IsAdmin())
            {
                return false;
            }

            if (_dbContext.HasStudentConflict(studentId, courseId))
            {
                throw new InvalidOperationException("Học sinh đã có lịch học trùng với khóa học này.");
            }

            try
            {
                bool enrolled = _dbContext.EnrollStudent(courseId, studentId);
                if (enrolled)
                {
                    _dbContext.LogUserActivity(
                        UserSessionContext.CurrentUserId,
                        "COURSE_ENROLL",
                        $"Admin ghi danh học viên ID={studentId} vào khóa học ID={courseId}",
                        string.Empty);
                }
                return enrolled;
            }
            catch
            {
                return false;
            }
        }

        public HashSet<int> GetStudentEnrolledCourseIds(int studentId)
        {
            if (studentId <= 0)
            {
                return new HashSet<int>();
            }

            return _dbContext.GetStudentEnrolledCourseIds(studentId);
        }

        public bool StudentJoinCourse(int courseId, int studentId)
        {
            if (studentId <= 0)
            {
                return false;
            }

            if (UserSessionContext.CurrentUserId != studentId)
            {
                return false;
            }

            if (_dbContext.HasStudentConflict(studentId, courseId))
            {
                throw new InvalidOperationException("Học sinh đã có lịch học trùng với khóa học này.");
            }

            try
            {
                bool enrolled = _dbContext.EnrollStudent(courseId, studentId);
                if (enrolled)
                {
                    _dbContext.LogUserActivity(
                        studentId,
                        "COURSE_ENROLL_REQUEST",
                        $"Học viên ID={studentId} tham gia khóa học ID={courseId}",
                        string.Empty);
                }

                return enrolled;
            }
            catch
            {
                return false;
            }
        }

        public List<EnrollmentModel> GetPendingEnrollments(int? courseId = null)
        {
            // Allow both Admin and Teacher to view pending enrollments
            if (UserSessionContext.CurrentRole != "ADMIN" && UserSessionContext.CurrentRole != "TEACHER") 
                return new List<EnrollmentModel>();

            try { return _dbContext.GetPendingEnrollments(courseId); }
            catch { return new List<EnrollmentModel>(); }
        }

        public List<EnrollmentModel> GetEnrollmentsByStatus(int courseId, string status)
        {
            if (UserSessionContext.CurrentRole != "ADMIN" && UserSessionContext.CurrentRole != "TEACHER") 
                return new List<EnrollmentModel>();

            try { return _dbContext.GetEnrollmentsByStatus(courseId, status); }
            catch { return new List<EnrollmentModel>(); }
        }

        public bool ApproveEnrollment(int courseId, int studentId)
        {
            if (UserSessionContext.CurrentRole != "ADMIN") 
                return false;

            if (_dbContext.HasStudentConflict(studentId, courseId))
            {
                throw new InvalidOperationException("Học sinh đã có lịch học trùng với khóa học này.");
            }

            try { return _dbContext.ApproveEnrollment(courseId, studentId); }
            catch { return false; }
        }

        public bool RejectEnrollment(int courseId, int studentId)
        {
            if (UserSessionContext.CurrentRole != "ADMIN") 
                return false;

            try { return _dbContext.RejectEnrollment(courseId, studentId); }
            catch { return false; }
        }

        // ════════════════════════════════════════════════════════════════
        //  STUDENT METHODS (Giai đoạn Đăng ký & Quản lý môn học)
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Lấy danh sách khóa học ACTIVE mà sinh viên chưa đăng ký
        /// (để hiển thị trên trang "Duyệt khóa học").
        /// </summary>
        public List<CourseModel> GetAvailableCourses(int studentId)
        {
            try
            {
                return _dbContext.GetAvailableCourses(studentId);
            }
            catch
            {
                return new List<CourseModel>();
            }
        }

        /// <summary>
        /// Lấy danh sách enrollment của một sinh viên
        /// (hiển thị trên trang "Khóa học của tôi").
        /// </summary>
        public string? GetEnrollmentStatus(int courseId, int studentId)
        {
            try { return _dbContext.GetEnrollmentStatus(courseId, studentId); }
            catch { return null; }
        }

        public List<EnrollmentModel> GetMyEnrollments(int studentId)
        {
            try
            {
                return _dbContext.GetEnrollmentsByStudent(studentId);
            }
            catch
            {
                return new List<EnrollmentModel>();
            }
        }

        /// <summary>
        /// Sinh viên tự ghi danh vào khóa học (trạng thái PENDING).
        /// </summary>
        public string RequestEnrollment(int courseId, int studentId)
        {
            try
            {
                CourseModel? course = _dbContext.GetCourseById(courseId);
                if (course == null || !string.Equals(course.Status, WorkflowConstants.CourseStatus.Active, StringComparison.OrdinalIgnoreCase))
                    return "Khóa học này chưa được mở ghi danh.";

                if (_dbContext.HasStudentConflict(studentId, courseId))
                {
                    return "Học sinh đã có lịch học trùng với khóa học này.";
                }

                // Kiểm tra trạng thái hiện tại
                var existing = _dbContext.GetEnrollmentStatus(courseId, studentId);
                if (existing != null)
                {
                    return existing switch
                    {
                        "PENDING" => "Bạn đã gửi yêu cầu tham gia khóa học này. Vui lòng chờ duyệt.",
                        "ACTIVE" => "Bạn đã tham gia khóa học này rồi.",
                        "APPROVED" => "Bạn đã tham gia khóa học này rồi.",
                        "DROPPED" => "Bạn đã rút khỏi khóa học này. Liên hệ Admin để đăng ký lại.",
                        "REJECTED" => TryReRequestRejectedEnrollment(courseId, studentId, course),
                        _ => $"Trạng thái hiện tại: {existing}"
                    };
                }

                bool success = _dbContext.SelfEnroll(courseId, studentId);
                if (success)
                    NotifyTeacherEnrollmentRequest(course, studentId);
                return success
                    ? "Đã gửi yêu cầu tham gia. Vui lòng chờ Admin/Giảng viên duyệt."
                    : "Không thể gửi yêu cầu. Vui lòng thử lại.";
            }
            catch (Exception ex)
            {
                return "Lỗi: " + ex.Message;
            }
        }

        /// <summary>
        /// Sinh viên hủy đăng ký / rút khỏi khóa học.
        /// </summary>
        public string DropCourse(int courseId, int studentId)
        {
            try
            {
                var existing = _dbContext.GetEnrollmentStatus(courseId, studentId);
                if (existing == null)
                {
                    return "Bạn chưa đăng ký khóa học này.";
                }

                if (existing == "DROPPED")
                {
                    return "Bạn đã rút khỏi khóa học này trước đó.";
                }

                bool success = _dbContext.DropEnrollment(courseId, studentId);
                if (success)
                {
                    return existing == "PENDING"
                        ? "Đã hủy yêu cầu tham gia thành công."
                        : "Đã rút khỏi khóa học thành công.";
                }

                return "Không thể thực hiện. Vui lòng thử lại.";
            }
            catch (Exception ex)
            {
                return "Lỗi: " + ex.Message;
            }
        }

        /// <summary>
        /// Lấy số sinh viên đang ghi danh trong khóa học.
        /// </summary>
        public int GetEnrolledCount(int courseId)
        {
            try
            {
                return _dbContext.GetEnrolledCount(courseId);
            }
            catch
            {
                return 0;
            }
        }

        public List<StudentScheduleItemModel> GetStudentOnlineSessions(int studentId)
        {
            try
            {
                return _dbContext.GetStudentOnlineSessions(studentId);
            }
            catch
            {
                return new List<StudentScheduleItemModel>();
            }
        }

        private string TryReRequestRejectedEnrollment(int courseId, int studentId, CourseModel course)
        {
            bool success = _dbContext.SelfEnroll(courseId, studentId);
            if (success)
            {
                NotifyTeacherEnrollmentRequest(course, studentId);
                return "Đã gửi lại yêu cầu tham gia. Vui lòng chờ Giảng viên duyệt.";
            }

            return "Không thể gửi lại yêu cầu. Vui lòng thử lại.";
        }

        private void NotifyTeacherEnrollmentRequest(CourseModel course, int studentId)
        {
            try
            {
                _notifications.Create(
                    course.TeacherId,
                    "Yêu cầu ghi danh mới",
                    $"Sinh viên ID={studentId} đã gửi yêu cầu tham gia khóa học \"{course.Name}\".",
                    WorkflowConstants.NotificationCategory.Enrollment,
                    WorkflowConstants.NotificationType.ActionRequired,
                    "Course",
                    course.Id);
            }
            catch
            {
                // Enrollment remains valid even if notification creation fails.
            }
        }
    }
}
