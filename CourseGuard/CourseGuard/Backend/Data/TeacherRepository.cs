using System;
using System.Collections.Generic;
using System.Linq;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Services;
using Npgsql;

namespace CourseGuard.Backend.Data
{
    public class TeacherRepository
    {
        private readonly CourseGuardDbContext _dbContext;
        private readonly NotificationRepository _notifications = new();

        public TeacherRepository(CourseGuardDbContext dbContext)
        {
            _dbContext = dbContext;
            EnsureTeacherSchema();
        }

        public TeacherDashboardSummaryModel GetDashboardSummary(int teacherId)
        {
            using var connection = _dbContext.CreateConnection();
            connection.Open();

            using var activeExamsCmd = new NpgsqlCommand(@"
                SELECT COUNT(*)
                FROM exams ex
                JOIN courses c ON c.id = ex.course_id
                WHERE c.teacher_id = @teacher_id
                  AND UPPER(COALESCE(ex.status, '')) = 'ACTIVE'
                  AND (ex.open_time IS NULL OR ex.open_time <= @now)
                  AND (ex.close_time IS NULL OR ex.close_time >= @now)", connection);
            activeExamsCmd.Parameters.AddWithValue("@teacher_id", teacherId);
            activeExamsCmd.Parameters.AddWithValue("@now", DateTime.Now);
            int activeExamsCount = Convert.ToInt32(activeExamsCmd.ExecuteScalar());

            var summary = new TeacherDashboardSummaryModel
            {
                TotalCourses = Count(connection, "SELECT COUNT(*) FROM courses WHERE teacher_id = @teacher_id", teacherId),
                PendingEnrollments = Count(connection, @"
                    SELECT COUNT(*)
                    FROM enrollments e
                    JOIN courses c ON c.id = e.course_id
                    WHERE c.teacher_id = @teacher_id AND UPPER(e.status) = 'PENDING'", teacherId),
                TotalStudents = Count(connection, @"
                    SELECT COUNT(DISTINCT e.student_id)
                    FROM enrollments e
                    JOIN courses c ON c.id = e.course_id
                    WHERE c.teacher_id = @teacher_id AND UPPER(e.status) IN ('ACTIVE', 'APPROVED')", teacherId),
                ActiveExams = activeExamsCount
            };

            using var activityCommand = new NpgsqlCommand(@"
                SELECT message
                FROM (
                    SELECT 'Khóa học: ' || COALESCE(name, '') AS message, created_at AS event_time
                    FROM courses
                    WHERE teacher_id = @teacher_id
                    UNION ALL
                    SELECT 'Ghi danh: ' || COALESCE(u.full_name, u.username, '') || ' - ' || COALESCE(c.name, ''), e.joined_at
                    FROM enrollments e
                    JOIN courses c ON c.id = e.course_id
                    JOIN users u ON u.id = e.student_id
                    WHERE c.teacher_id = @teacher_id
                ) recent
                ORDER BY event_time DESC NULLS LAST
                LIMIT 6", connection);
            activityCommand.Parameters.AddWithValue("@teacher_id", teacherId);
            using var reader = activityCommand.ExecuteReader();
            while (reader.Read())
                summary.RecentActivities.Add(reader.GetString(0));

            return summary;
        }

        public TeacherProfileModel? GetTeacherProfile(int teacherId)
        {
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(@"
                SELECT u.id,
                       COALESCE(u.full_name, ''),
                       COALESCE(u.email, ''),
                       COALESCE(tp.phone, ''),
                       COALESCE(tp.gender, ''),
                       tp.birth_date,
                       COALESCE(tp.address, ''),
                       COALESCE(tp.major, ''),
                       COALESCE(tp.degrees, ''),
                       COALESCE(tp.bio, ''),
                       COALESCE(tp.avatar_path, '')
                FROM users u
                LEFT JOIN teacher_profiles tp ON tp.user_id = u.id
                WHERE u.id = @teacher_id", connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
                return null;

            return new TeacherProfileModel
            {
                UserId = reader.GetInt32(0),
                FullName = reader.GetString(1),
                Email = reader.GetString(2),
                Phone = reader.GetString(3),
                Gender = reader.GetString(4),
                BirthDate = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                Address = reader.GetString(6),
                Major = reader.GetString(7),
                Degrees = reader.GetString(8),
                Bio = reader.GetString(9),
                AvatarPath = reader.GetString(10)
            };
        }

        public bool UpsertTeacherProfile(int teacherId, TeacherProfileModel input)
        {
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            try
            {
                using (var updateUser = new NpgsqlCommand(@"
                    UPDATE users
                    SET full_name = @full_name,
                        email = @email
                    WHERE id = @teacher_id", connection, transaction))
                {
                    updateUser.Parameters.AddWithValue("@teacher_id", teacherId);
                    updateUser.Parameters.AddWithValue("@full_name", string.IsNullOrWhiteSpace(input.FullName) ? DBNull.Value : input.FullName);
                    updateUser.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(input.Email) ? DBNull.Value : input.Email);
                    updateUser.ExecuteNonQuery();
                }

                using (var upsertProfile = new NpgsqlCommand(@"
                    INSERT INTO teacher_profiles (user_id, teacher_code, phone, gender, birth_date, address, major, degrees, bio, avatar_path, updated_at)
                    VALUES (@teacher_id, @teacher_code, @phone, @gender, @birth_date, @address, @major, @degrees, @bio, @avatar_path, CURRENT_TIMESTAMP)
                    ON CONFLICT (user_id)
                    DO UPDATE SET phone = EXCLUDED.phone,
                                  gender = EXCLUDED.gender,
                                  birth_date = EXCLUDED.birth_date,
                                  address = EXCLUDED.address,
                                  major = EXCLUDED.major,
                                  degrees = EXCLUDED.degrees,
                                  bio = EXCLUDED.bio,
                                  avatar_path = EXCLUDED.avatar_path,
                                  updated_at = CURRENT_TIMESTAMP", connection, transaction))
                {
                    upsertProfile.Parameters.AddWithValue("@teacher_id", teacherId);
                    upsertProfile.Parameters.AddWithValue("@teacher_code", $"GV{teacherId:00000}");
                    upsertProfile.Parameters.AddWithValue("@phone", string.IsNullOrWhiteSpace(input.Phone) ? DBNull.Value : input.Phone);
                    upsertProfile.Parameters.AddWithValue("@gender", string.IsNullOrWhiteSpace(input.Gender) ? DBNull.Value : input.Gender);
                    upsertProfile.Parameters.AddWithValue("@birth_date", input.BirthDate.HasValue ? input.BirthDate.Value.Date : DBNull.Value);
                    upsertProfile.Parameters.AddWithValue("@address", string.IsNullOrWhiteSpace(input.Address) ? DBNull.Value : input.Address);
                    upsertProfile.Parameters.AddWithValue("@major", string.IsNullOrWhiteSpace(input.Major) ? DBNull.Value : input.Major);
                    upsertProfile.Parameters.AddWithValue("@degrees", string.IsNullOrWhiteSpace(input.Degrees) ? DBNull.Value : input.Degrees);
                    upsertProfile.Parameters.AddWithValue("@bio", string.IsNullOrWhiteSpace(input.Bio) ? DBNull.Value : input.Bio);
                    upsertProfile.Parameters.AddWithValue("@avatar_path", string.IsNullOrWhiteSpace(input.AvatarPath) ? DBNull.Value : input.AvatarPath);
                    upsertProfile.ExecuteNonQuery();
                }

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public List<TeacherCourseModel> GetCourses(int teacherId)
        {
            var rows = new List<TeacherCourseModel>();
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(@"
                SELECT c.id, COALESCE(c.name, ''), COALESCE(c.description, ''), COALESCE(c.status, 'DRAFT'),
                       c.start_date, c.end_date, COALESCE(c.rejection_reason, ''),
                       COUNT(e.id) FILTER (WHERE UPPER(COALESCE(e.status, '')) IN ('ACTIVE', 'APPROVED'))::int
                FROM courses c
                LEFT JOIN enrollments e ON e.course_id = c.id
                WHERE c.teacher_id = @teacher_id
                GROUP BY c.id
                ORDER BY c.created_at DESC, c.id DESC", connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new TeacherCourseModel
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    Status = reader.GetString(3),
                    StartDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                    EndDate = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    RejectionReason = reader.GetString(6),
                    StudentCount = reader.GetInt32(7)
                });
            }
            return rows;
        }

        public int CreateCourse(int teacherId, TeacherCourseModel input)
        {
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = new NpgsqlCommand(@"
                INSERT INTO courses (name, description, teacher_id, status, rejection_reason, start_date, end_date, teaching_days, session_start_time, session_end_time, frequency, meeting_link)
                VALUES (@name, @description, @teacher_id, 'DRAFT', NULL, @start_date, @end_date, @teaching_days, @session_start_time, @session_end_time, @frequency, @meeting_link)
                RETURNING id", connection, transaction);
            AddCourseParameters(command, teacherId, input, includeId: false);
            int courseId = Convert.ToInt32(command.ExecuteScalar());

            if (input.GenerateScheduleOnCreate)
                InsertGeneratedSessions(connection, transaction, teacherId, courseId, input);

            transaction.Commit();
            return courseId;
        }

        public bool UpdateCourse(int teacherId, TeacherCourseModel input)
        {
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(@"
                UPDATE courses
                SET name = @name,
                    description = @description,
                    status = CASE
                        WHEN UPPER(COALESCE(status, '')) = 'ACTIVE' THEN status
                        WHEN UPPER(COALESCE(status, '')) = 'PENDING' THEN status
                        ELSE @status
                    END,
                    start_date = @start_date,
                    end_date = @end_date,
                    teaching_days = @teaching_days,
                    session_start_time = @session_start_time,
                    session_end_time = @session_end_time,
                    frequency = @frequency,
                    meeting_link = @meeting_link
                WHERE id = @id AND teacher_id = @teacher_id", connection);
            AddCourseParameters(command, teacherId, input, includeId: true);
            return command.ExecuteNonQuery() > 0;
        }

        public bool SubmitCourseForApproval(int teacherId, int courseId)
        {
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(@"
                UPDATE courses
                SET status = 'PENDING', rejection_reason = NULL
                WHERE id = @id
                  AND teacher_id = @teacher_id
                  AND UPPER(COALESCE(status, '')) IN ('DRAFT', 'REJECTED')", connection);
            command.Parameters.AddWithValue("@id", courseId);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            return command.ExecuteNonQuery() > 0;
        }

        public bool DeleteCourse(int teacherId, int courseId)
        {
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand("DELETE FROM courses WHERE id = @id AND teacher_id = @teacher_id", connection);
            command.Parameters.AddWithValue("@id", courseId);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            return command.ExecuteNonQuery() > 0;
        }

        public List<TeacherLessonModel> GetLessons(int teacherId) => QueryLessons(teacherId, null);
        public List<TeacherAssignmentModel> GetAssignments(int teacherId) => QueryAssignments(teacherId, null);
        public List<TeacherExamModel> GetExams(int teacherId) => QueryExams(teacherId, null);
        public List<TeacherMaterialModel> GetMaterials(int teacherId, int? courseId = null) => QueryMaterials(teacherId, courseId);
        public List<TeacherScheduleItemModel> GetSchedule(int teacherId) => QuerySchedule(teacherId);

        public List<AttendanceLogModel> GetAttendanceSummary(int teacherId, int sessionId)
        {
            var rows = new List<AttendanceLogModel>();
            if (teacherId <= 0 || sessionId <= 0)
                return rows;

            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(@"
                SELECT al.id,
                       al.student_id,
                       al.session_id,
                       COALESCE(u.full_name, u.username, ''),
                       COALESCE(c.name, ''),
                       COALESCE(os.title, ''),
                       al.joined_at,
                       al.left_at,
                       COALESCE(al.duration_minutes, 0),
                       COALESCE(al.is_valid, FALSE),
                       COALESCE(al.created_at, al.joined_at)
                FROM attendance_logs al
                JOIN users u ON u.id = al.student_id
                JOIN online_sessions os ON os.id = al.session_id
                JOIN courses c ON c.id = os.course_id
                WHERE al.session_id = @session_id
                  AND c.teacher_id = @teacher_id
                ORDER BY al.joined_at DESC, al.id DESC", connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@session_id", sessionId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new AttendanceLogModel
                {
                    Id = reader.GetInt32(0),
                    StudentId = reader.GetInt32(1),
                    SessionId = reader.GetInt32(2),
                    StudentName = reader.GetString(3),
                    CourseName = reader.GetString(4),
                    SessionTitle = reader.GetString(5),
                    JoinedAt = reader.GetDateTime(6),
                    LeftAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                    DurationMinutes = reader.GetInt32(8),
                    IsValid = reader.GetBoolean(9),
                    CreatedAt = reader.GetDateTime(10)
                });
            }

            return rows;
        }

        public List<TeacherTeachingTaskModel> GetTeachingTasks(int teacherId)
        {
            var tasks = new List<TeacherTeachingTaskModel>();
            tasks.AddRange(QueryUpcomingTeachingSessions(teacherId));
            tasks.AddRange(QueryAssignmentTasks(teacherId));
            tasks.AddRange(QueryExamTasks(teacherId));
            tasks.AddRange(QueryMonitoringTasks(teacherId));

            return tasks
                .OrderBy(t => t.DueAt ?? DateTime.MaxValue)
                .ThenByDescending(t => t.RequiresAction)
                .Take(10)
                .ToList();
        }

        public int CreateLesson(int teacherId, TeacherLessonModel input) => InsertCourseChild(
            teacherId,
            input.CourseId,
            @"INSERT INTO teacher_lessons (course_id, title, content, publish_at, status, file_name, file_path, content_type, file_size, file_content)
              SELECT @course_id, @title, @content, @publish_at, @status, @file_name, @file_path, @content_type, @file_size, @file_content
              WHERE EXISTS (SELECT 1 FROM courses WHERE id = @course_id AND teacher_id = @teacher_id)
              RETURNING id",
            command =>
            {
                command.Parameters.AddWithValue("@title", input.Title);
                command.Parameters.AddWithValue("@content", input.Content);
                command.Parameters.AddWithValue("@publish_at", input.PublishAt.HasValue ? input.PublishAt.Value : DBNull.Value);
                command.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(input.Status) ? "DRAFT" : input.Status);
                command.Parameters.AddWithValue("@file_name", string.IsNullOrWhiteSpace(input.FileName) ? DBNull.Value : input.FileName);
                command.Parameters.AddWithValue("@file_path", string.IsNullOrWhiteSpace(input.FilePath) ? DBNull.Value : input.FilePath);
                command.Parameters.AddWithValue("@content_type", string.IsNullOrWhiteSpace(input.ContentType) ? DBNull.Value : input.ContentType);
                command.Parameters.AddWithValue("@file_size", input.FileSize.HasValue ? input.FileSize.Value : DBNull.Value);
                command.Parameters.AddWithValue("@file_content", input.FileContent == null || input.FileContent.Length == 0 ? DBNull.Value : input.FileContent);
            });

        public bool UpdateLesson(int teacherId, TeacherLessonModel input) => ExecuteCourseChild(
            teacherId,
            input.CourseId,
            @"UPDATE teacher_lessons l
              SET title = @title, content = @content, publish_at = @publish_at, status = @status,
                  file_name = @file_name, file_path = @file_path, content_type = @content_type, file_size = @file_size, file_content = CASE WHEN @update_file = 1 THEN @file_content ELSE l.file_content END
              FROM courses c
              WHERE l.course_id = c.id AND c.teacher_id = @teacher_id AND l.id = @id AND l.course_id = @course_id",
            command =>
            {
                command.Parameters.AddWithValue("@id", input.Id);
                command.Parameters.AddWithValue("@title", input.Title);
                command.Parameters.AddWithValue("@content", input.Content);
                command.Parameters.AddWithValue("@publish_at", input.PublishAt.HasValue ? input.PublishAt.Value : DBNull.Value);
                command.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(input.Status) ? "DRAFT" : input.Status);
                command.Parameters.AddWithValue("@file_name", string.IsNullOrWhiteSpace(input.FileName) ? DBNull.Value : input.FileName);
                command.Parameters.AddWithValue("@file_path", string.IsNullOrWhiteSpace(input.FilePath) ? DBNull.Value : input.FilePath);
                command.Parameters.AddWithValue("@content_type", string.IsNullOrWhiteSpace(input.ContentType) ? DBNull.Value : input.ContentType);
                command.Parameters.AddWithValue("@file_size", input.FileSize.HasValue ? input.FileSize.Value : DBNull.Value);
                
                if (input.FileContent != null && input.FileContent.Length > 0)
                {
                    command.Parameters.AddWithValue("@update_file", 1);
                    command.Parameters.AddWithValue("@file_content", input.FileContent);
                }
                else if (input.HasStoredContent && input.FileContent == null) 
                {
                    command.Parameters.AddWithValue("@update_file", 0);
                    command.Parameters.AddWithValue("@file_content", DBNull.Value); 
                }
                else 
                {
                    command.Parameters.AddWithValue("@update_file", 1);
                    command.Parameters.AddWithValue("@file_content", DBNull.Value);
                }
            });

        public bool DeleteLesson(int teacherId, int lessonId) => ExecuteOwnedDelete(
            teacherId,
            @"DELETE FROM teacher_lessons l USING courses c
              WHERE l.course_id = c.id AND c.teacher_id = @teacher_id AND l.id = @id",
            lessonId);

        public async Task<byte[]?> GetLessonFileContentAsync(int lessonId, System.Threading.CancellationToken cancellationToken = default)
        {
            await using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            string query = "SELECT file_content FROM teacher_lessons WHERE id = @id LIMIT 1";
            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@id", lessonId);

            object? result = await command.ExecuteScalarAsync(cancellationToken);
            return result != DBNull.Value && result is byte[] bytes ? bytes : null;
        }

        public int CreateAssignment(int teacherId, TeacherAssignmentModel input) => InsertCourseChild(
            teacherId,
            input.CourseId,
            @"INSERT INTO teacher_assignments (course_id, title, description, due_at, status, file_name, file_path, content_type, file_size, file_content)
              SELECT @course_id, @title, @description, @due_at, @status, @file_name, @file_path, @content_type, @file_size, @file_content
              WHERE EXISTS (SELECT 1 FROM courses WHERE id = @course_id AND teacher_id = @teacher_id)
              RETURNING id",
            command =>
            {
                command.Parameters.AddWithValue("@title", input.Title);
                command.Parameters.AddWithValue("@description", input.Description);
                command.Parameters.AddWithValue("@due_at", input.DueAt.HasValue ? input.DueAt.Value : DBNull.Value);
                command.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(input.Status) ? "OPEN" : input.Status);
                command.Parameters.AddWithValue("@file_name", string.IsNullOrWhiteSpace(input.FileName) ? DBNull.Value : input.FileName);
                command.Parameters.AddWithValue("@file_path", string.IsNullOrWhiteSpace(input.FilePath) ? DBNull.Value : input.FilePath);
                command.Parameters.AddWithValue("@content_type", string.IsNullOrWhiteSpace(input.ContentType) ? DBNull.Value : input.ContentType);
                command.Parameters.AddWithValue("@file_size", input.FileSize.HasValue ? input.FileSize.Value : DBNull.Value);
                command.Parameters.AddWithValue("@file_content", input.FileContent == null || input.FileContent.Length == 0 ? DBNull.Value : input.FileContent);
            });

        public bool UpdateAssignment(int teacherId, TeacherAssignmentModel input) => ExecuteCourseChild(
            teacherId,
            input.CourseId,
            @"UPDATE teacher_assignments a
              SET title = @title, description = @description, due_at = @due_at, status = @status,
                  file_name = @file_name, file_path = @file_path, content_type = @content_type, file_size = @file_size, file_content = CASE WHEN @update_file = 1 THEN @file_content ELSE a.file_content END
              FROM courses c
              WHERE a.course_id = c.id AND c.teacher_id = @teacher_id AND a.id = @id AND a.course_id = @course_id",
            command =>
            {
                command.Parameters.AddWithValue("@id", input.Id);
                command.Parameters.AddWithValue("@title", input.Title);
                command.Parameters.AddWithValue("@description", input.Description);
                command.Parameters.AddWithValue("@due_at", input.DueAt.HasValue ? input.DueAt.Value : DBNull.Value);
                command.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(input.Status) ? "OPEN" : input.Status);
                command.Parameters.AddWithValue("@file_name", string.IsNullOrWhiteSpace(input.FileName) ? DBNull.Value : input.FileName);
                command.Parameters.AddWithValue("@file_path", string.IsNullOrWhiteSpace(input.FilePath) ? DBNull.Value : input.FilePath);
                command.Parameters.AddWithValue("@content_type", string.IsNullOrWhiteSpace(input.ContentType) ? DBNull.Value : input.ContentType);
                command.Parameters.AddWithValue("@file_size", input.FileSize.HasValue ? input.FileSize.Value : DBNull.Value);
                if (input.FileContent != null && input.FileContent.Length > 0)
                {
                    command.Parameters.AddWithValue("@update_file", 1);
                    command.Parameters.AddWithValue("@file_content", input.FileContent);
                }
                else if (input.HasStoredContent && input.FileContent == null)
                {
                    command.Parameters.AddWithValue("@update_file", 0);
                    command.Parameters.AddWithValue("@file_content", DBNull.Value);
                }
                else
                {
                    command.Parameters.AddWithValue("@update_file", 1);
                    command.Parameters.AddWithValue("@file_content", DBNull.Value);
                }
            });

        public bool DeleteAssignment(int teacherId, int assignmentId) => ExecuteOwnedDelete(
            teacherId,
            @"DELETE FROM teacher_assignments a USING courses c
              WHERE a.course_id = c.id AND c.teacher_id = @teacher_id AND a.id = @id",
            assignmentId);

        public int CreateExam(int teacherId, TeacherExamModel input) => InsertCourseChild(
            teacherId,
            input.CourseId,
            @"INSERT INTO exams (course_id, title, open_time, close_time, duration_minutes, max_attempts, max_violations, created_by, status)
              SELECT @course_id, @title, @open_time, @close_time, @duration_minutes, @max_attempts, @max_violations, @teacher_id, @status
              WHERE EXISTS (SELECT 1 FROM courses WHERE id = @course_id AND teacher_id = @teacher_id)
              RETURNING id",
            command => AddExamParameters(command, input, includeId: false));

        public bool UpdateExam(int teacherId, TeacherExamModel input) => ExecuteCourseChild(
            teacherId,
            input.CourseId,
            @"UPDATE exams ex
              SET title = @title, open_time = @open_time, close_time = @close_time,
                  duration_minutes = @duration_minutes, max_attempts = @max_attempts,
                  max_violations = @max_violations,
                  status = CASE
                      WHEN @status = @closed_status THEN @closed_status
                      WHEN UPPER(COALESCE(ex.status, '')) = @active_status THEN @active_status
                      WHEN UPPER(COALESCE(ex.status, '')) = @closed_status THEN @closed_status
                      WHEN UPPER(COALESCE(ex.status, '')) = @draft_status THEN @draft_status
                      ELSE COALESCE(ex.status, '')
                  END
              FROM courses c
              WHERE ex.course_id = c.id AND c.teacher_id = @teacher_id AND ex.id = @id AND ex.course_id = @course_id",
            command =>
            {
                AddExamParameters(command, input, includeId: true);
                AddExamWorkflowStatusParameters(command);
            });

        public bool ActivateExam(int teacherId, int examId)
        {
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(@"
                UPDATE exams ex
                SET status = @active_status
                FROM courses c
                WHERE ex.course_id = c.id
                  AND c.teacher_id = @teacher_id
                  AND ex.id = @exam_id
                  AND UPPER(COALESCE(ex.status, '')) = @draft_status
                  AND EXISTS (
                      SELECT 1
                      FROM exam_questions eq
                      WHERE eq.exam_id = ex.id
                  )", connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@exam_id", examId);
            command.Parameters.AddWithValue("@active_status", WorkflowConstants.ExamStatus.Active);
            command.Parameters.AddWithValue("@draft_status", WorkflowConstants.ExamStatus.Draft);
            return command.ExecuteNonQuery() > 0;
        }

        public bool DeleteExam(int teacherId, int examId)
        {
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            try
            {
                using (var cmd = new NpgsqlCommand(@"
                    DELETE FROM violations v
                    USING exam_attempts a, exams ex, courses c
                    WHERE v.exam_attempt_id = a.id
                      AND a.exam_id = ex.id
                      AND ex.course_id = c.id
                      AND c.teacher_id = @teacher_id
                      AND ex.id = @id", connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@teacher_id", teacherId);
                    cmd.Parameters.AddWithValue("@id", examId);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new NpgsqlCommand(@"
                    DELETE FROM exams ex USING courses c
                    WHERE ex.course_id = c.id AND c.teacher_id = @teacher_id AND ex.id = @id", connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@teacher_id", teacherId);
                    cmd.Parameters.AddWithValue("@id", examId);
                    bool deleted = cmd.ExecuteNonQuery() > 0;
                    transaction.Commit();
                    return deleted;
                }
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public bool CanActivateExam(int teacherId, int examId)
        {
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(@"
                SELECT EXISTS (
                    SELECT 1
                    FROM exams ex
                    JOIN courses c ON c.id = ex.course_id
                    WHERE c.teacher_id = @teacher_id
                      AND ex.id = @exam_id
                      AND UPPER(COALESCE(ex.status, '')) = @draft_status
                      AND EXISTS (
                          SELECT 1
                          FROM exam_questions eq
                          WHERE eq.exam_id = ex.id
                      )
                )", connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@exam_id", examId);
            command.Parameters.AddWithValue("@draft_status", WorkflowConstants.ExamStatus.Draft);
            return command.ExecuteScalar() is bool canActivate && canActivate;
        }

        public List<TeacherExamQuestionModel> GetExamQuestions(int teacherId, int examId)
        {
            var rows = new List<TeacherExamQuestionModel>();
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(@"
                SELECT eq.id, eq.exam_id, COALESCE(eq.question_text, ''),
                       COALESCE(eq.option_a, ''), COALESCE(eq.option_b, ''),
                       COALESCE(eq.option_c, ''), COALESCE(eq.option_d, ''),
                       COALESCE(eq.correct_option, 'A'), COALESCE(eq.points, 0),
                       COALESCE(eq.display_order, 1),
                       COALESCE(eq.difficulty, 'MEDIUM'), eq.chapter,
                       COALESCE(eq.question_type, 'MULTIPLE_CHOICE')
                FROM exam_questions eq
                JOIN exams ex ON ex.id = eq.exam_id
                JOIN courses c ON c.id = ex.course_id
                WHERE c.teacher_id = @teacher_id
                  AND eq.exam_id = @exam_id
                ORDER BY eq.display_order, eq.id", connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@exam_id", examId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new TeacherExamQuestionModel
                {
                    Id = reader.GetInt32(0),
                    ExamId = reader.GetInt32(1),
                    QuestionText = reader.GetString(2),
                    OptionA = reader.GetString(3),
                    OptionB = reader.GetString(4),
                    OptionC = reader.GetString(5),
                    OptionD = reader.GetString(6),
                    CorrectOption = reader.GetString(7),
                    Points = reader.GetDecimal(8),
                    DisplayOrder = reader.GetInt32(9),
                    Difficulty = reader.GetString(10),
                    Chapter = reader.IsDBNull(11) ? null : reader.GetString(11),
                    QuestionType = reader.GetString(12)
                });
            }
            return rows;
        }

        public string GetExamStatus(int teacherId, int examId)
        {
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(@"
                SELECT ex.status
                FROM exams ex
                JOIN courses c ON c.id = ex.course_id
                WHERE c.teacher_id = @teacher_id
                  AND ex.id = @exam_id", connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@exam_id", examId);
            object? result = command.ExecuteScalar();
            return result == null || result == DBNull.Value ? string.Empty : Convert.ToString(result) ?? string.Empty;
        }

        public int CreateExamQuestion(int teacherId, TeacherExamQuestionModel input)
        {
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            int nextOrder = GetNextQuestionOrder(connection, transaction, teacherId, input.ExamId);
            using var command = new NpgsqlCommand(@"
                INSERT INTO exam_questions
                    (exam_id, question_text, option_a, option_b, option_c, option_d, correct_option, points, display_order, difficulty, chapter, question_type)
                SELECT @exam_id, @question_text, @option_a, @option_b, @option_c, @option_d, @correct_option, 0, @display_order,
                       @difficulty, @chapter, @question_type
                WHERE EXISTS (
                    SELECT 1 FROM exams ex
                    JOIN courses c ON c.id = ex.course_id
                    WHERE ex.id = @exam_id
                      AND c.teacher_id = @teacher_id
                      AND UPPER(COALESCE(ex.status, '')) = @draft_status
                )
                RETURNING id", connection, transaction);
            AddQuestionParameters(command, teacherId, input, nextOrder);
            command.Parameters.AddWithValue("@draft_status", WorkflowConstants.ExamStatus.Draft);
            object? result = command.ExecuteScalar();
            int id = result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            if (id > 0)
                RecalculateQuestionPoints(connection, transaction, input.ExamId);
            transaction.Commit();
            return id;
        }

        public bool UpdateExamQuestion(int teacherId, TeacherExamQuestionModel input)
        {
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(@"
                UPDATE exam_questions eq
                SET question_text = @question_text,
                    option_a = @option_a,
                    option_b = @option_b,
                    option_c = @option_c,
                    option_d = @option_d,
                    correct_option = @correct_option,
                    difficulty = @difficulty,
                    chapter = @chapter,
                    question_type = @question_type
                FROM exams ex
                JOIN courses c ON c.id = ex.course_id
                WHERE eq.exam_id = ex.id
                  AND c.teacher_id = @teacher_id
                  AND eq.id = @id
                  AND eq.exam_id = @exam_id
                  AND UPPER(COALESCE(ex.status, '')) = @draft_status", connection);
            command.Parameters.AddWithValue("@id", input.Id);
            AddQuestionParameters(command, teacherId, input, input.DisplayOrder <= 0 ? 1 : input.DisplayOrder);
            command.Parameters.AddWithValue("@draft_status", WorkflowConstants.ExamStatus.Draft);
            return command.ExecuteNonQuery() > 0;
        }

        public bool DeleteExamQuestion(int teacherId, int examId, int questionId)
        {
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = new NpgsqlCommand(@"
                DELETE FROM exam_questions eq
                USING exams ex, courses c
                WHERE eq.exam_id = ex.id
                  AND c.id = ex.course_id
                  AND c.teacher_id = @teacher_id
                  AND eq.exam_id = @exam_id
                  AND eq.id = @id
                  AND UPPER(COALESCE(ex.status, '')) = @draft_status", connection, transaction);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@exam_id", examId);
            command.Parameters.AddWithValue("@id", questionId);
            command.Parameters.AddWithValue("@draft_status", WorkflowConstants.ExamStatus.Draft);
            bool deleted = command.ExecuteNonQuery() > 0;
            if (deleted)
                RecalculateQuestionPoints(connection, transaction, examId);
            transaction.Commit();
            return deleted;
        }

        public List<TeacherStudentModel> GetPendingEnrollments(int teacherId) => QueryStudents(teacherId, "PENDING", null);
        public List<TeacherStudentModel> GetEnrolledStudents(int teacherId, int? courseId) => QueryStudents(teacherId, "ACTIVE", courseId);

        public bool ApproveEnrollment(int teacherId, int courseId, int studentId) => UpdateEnrollmentStatus(teacherId, courseId, studentId, approve: true);
        public bool RejectEnrollment(int teacherId, int courseId, int studentId) => UpdateEnrollmentStatus(teacherId, courseId, studentId, approve: false);

        public List<TeacherScoreModel> GetResults(int teacherId, int? courseId, int? examId)
        {
            var rows = new List<TeacherScoreModel>();
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            string query = @"
                SELECT a.id, ex.id, c.id, s.id,
                       COALESCE(c.name, ''), COALESCE(ex.title, ''), COALESCE(s.full_name, s.username, ''),
                       a.score, COALESCE(a.status, ''), a.submit_time
                FROM exam_attempts a
                JOIN exams ex ON ex.id = a.exam_id
                JOIN courses c ON c.id = ex.course_id
                JOIN users s ON s.id = a.student_id
                WHERE c.teacher_id = @teacher_id";
            if (courseId.HasValue)
                query += " AND c.id = @course_id";
            if (examId.HasValue)
                query += " AND ex.id = @exam_id";
            query += " ORDER BY a.start_time DESC, a.id DESC";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            if (courseId.HasValue)
                command.Parameters.AddWithValue("@course_id", courseId.Value);
            if (examId.HasValue)
                command.Parameters.AddWithValue("@exam_id", examId.Value);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new TeacherScoreModel
                {
                    AttemptId = reader.GetInt32(0),
                    ExamId = reader.GetInt32(1),
                    CourseId = reader.GetInt32(2),
                    StudentId = reader.GetInt32(3),
                    CourseName = reader.GetString(4),
                    ExamTitle = reader.GetString(5),
                    StudentName = reader.GetString(6),
                    Score = reader.IsDBNull(7) ? null : Convert.ToDouble(reader.GetValue(7)),
                    Status = reader.GetString(8),
                    SubmitTime = reader.IsDBNull(9) ? null : reader.GetDateTime(9)
                });
            }
            return rows;
        }

        public List<TeacherQuickSearchResultModel> SearchResultQuickAccess(int teacherId, string keyword)
        {
            var results = new List<TeacherQuickSearchResultModel>();
            using var connection = _dbContext.CreateConnection();
            connection.Open();

            string pattern = $"%{keyword.Trim()}%";
            using var courseCommand = new NpgsqlCommand(@"
                SELECT c.id, COALESCE(c.name, ''), COUNT(DISTINCT a.id) AS attempt_count
                FROM courses c
                JOIN exams ex ON ex.course_id = c.id
                JOIN exam_attempts a ON a.exam_id = ex.id
                WHERE c.teacher_id = @teacher_id
                  AND COALESCE(c.name, '') ILIKE @pattern
                GROUP BY c.id, c.name
                ORDER BY c.name
                LIMIT 5", connection);
            courseCommand.Parameters.AddWithValue("@teacher_id", teacherId);
            courseCommand.Parameters.AddWithValue("@pattern", pattern);
            using (var reader = courseCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    string courseName = reader.GetString(1);
                    int attemptCount = Convert.ToInt32(reader.GetValue(2));
                    results.Add(new TeacherQuickSearchResultModel
                    {
                        Kind = TeacherQuickSearchKinds.ResultCourse,
                        Id = reader.GetInt32(0),
                        Group = "Kết quả",
                        Title = $"[Kết quả] {courseName}",
                        Description = $"Xem {attemptCount} bài thi trong khóa học {courseName}",
                        PageName = "Kết quả",
                        Keyword = keyword
                    });
                }
            }

            using var studentCommand = new NpgsqlCommand(@"
                SELECT c.id, s.id, COALESCE(s.full_name, s.username, ''), COALESCE(c.name, ''), COUNT(DISTINCT a.id) AS attempt_count
                FROM exam_attempts a
                JOIN exams ex ON ex.id = a.exam_id
                JOIN courses c ON c.id = ex.course_id
                JOIN users s ON s.id = a.student_id
                WHERE c.teacher_id = @teacher_id
                  AND COALESCE(s.full_name, s.username, '') ILIKE @pattern
                GROUP BY c.id, c.name, s.id, s.full_name, s.username
                ORDER BY COALESCE(s.full_name, s.username, ''), COALESCE(c.name, '')
                LIMIT 5", connection);
            studentCommand.Parameters.AddWithValue("@teacher_id", teacherId);
            studentCommand.Parameters.AddWithValue("@pattern", pattern);
            using (var reader = studentCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    int courseId = reader.GetInt32(0);
                    string studentName = reader.GetString(2);
                    string courseName = reader.GetString(3);
                    int attemptCount = Convert.ToInt32(reader.GetValue(4));
                    if (!string.IsNullOrWhiteSpace(courseName))
                        studentName = $"{studentName} - {courseName}";
                    results.Add(new TeacherQuickSearchResultModel
                    {
                        Kind = TeacherQuickSearchKinds.ResultStudent,
                        Id = reader.GetInt32(1),
                        ParentId = courseId,
                        Group = "Kết quả",
                        Title = $"[Kết quả] {studentName}",
                        Description = $"Xem {attemptCount} bài thi của sinh viên {studentName}",
                        PageName = "Kết quả",
                        Keyword = keyword
                    });
                }
            }

            return results;
        }

        public bool UpdateScore(int teacherId, TeacherScoreModel input)
        {
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(@"
                UPDATE exam_attempts a
                SET score = @score
                FROM exams ex
                JOIN courses c ON c.id = ex.course_id
                WHERE a.exam_id = ex.id
                  AND c.teacher_id = @teacher_id
                  AND a.id = @attempt_id
                  AND UPPER(COALESCE(a.status, '')) IN ('SUBMITTED', 'COMPLETED', 'GRADED')", connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@attempt_id", input.AttemptId);
            command.Parameters.AddWithValue("@score", input.Score.HasValue ? input.Score.Value : DBNull.Value);
            return command.ExecuteNonQuery() > 0;
        }

        public int CreateMaterial(int teacherId, TeacherMaterialModel input) => InsertCourseChild(
            teacherId,
            input.CourseId,
            @"INSERT INTO materials (course_id, file_name, file_path, content_type, file_size, file_content, uploaded_by, uploaded_at)
              SELECT @course_id, @file_name, @file_path, @content_type, @file_size, @file_content, @teacher_id, CURRENT_TIMESTAMP
              WHERE EXISTS (SELECT 1 FROM courses WHERE id = @course_id AND teacher_id = @teacher_id)
              RETURNING id",
            command =>
            {
                command.Parameters.AddWithValue("@file_name", input.FileName);
                command.Parameters.AddWithValue("@file_path", input.FilePath);
                command.Parameters.AddWithValue("@content_type", string.IsNullOrWhiteSpace(input.ContentType) ? DBNull.Value : input.ContentType);
                command.Parameters.AddWithValue("@file_size", input.FileSize);
                command.Parameters.AddWithValue("@file_content", input.FileContent == null || input.FileContent.Length == 0 ? DBNull.Value : input.FileContent);
            });

        public bool UpdateMaterial(int teacherId, TeacherMaterialModel input) => ExecuteCourseChild(
            teacherId,
            input.CourseId,
            @"UPDATE materials m
              SET file_name = @file_name, file_path = @file_path,
                  content_type = @content_type, file_size = @file_size, file_content = @file_content
              FROM courses c
              WHERE m.course_id = c.id AND c.teacher_id = @teacher_id AND m.id = @id AND m.course_id = @course_id",
            command =>
            {
                command.Parameters.AddWithValue("@id", input.Id);
                command.Parameters.AddWithValue("@file_name", input.FileName);
                command.Parameters.AddWithValue("@file_path", input.FilePath);
                command.Parameters.AddWithValue("@content_type", string.IsNullOrWhiteSpace(input.ContentType) ? DBNull.Value : input.ContentType);
                command.Parameters.AddWithValue("@file_size", input.FileSize);
                command.Parameters.AddWithValue("@file_content", input.FileContent == null || input.FileContent.Length == 0 ? DBNull.Value : input.FileContent);
            });

        public bool DeleteMaterial(int teacherId, int materialId) => ExecuteOwnedDelete(
            teacherId,
            @"DELETE FROM materials m USING courses c
              WHERE m.course_id = c.id AND c.teacher_id = @teacher_id AND m.id = @id",
            materialId);

        public int CreateScheduleItem(int teacherId, TeacherScheduleItemModel input) => InsertCourseChild(
            teacherId,
            input.CourseId,
            @"INSERT INTO online_sessions (course_id, title, start_time, end_time, meeting_link, created_by)
              SELECT @course_id, @title, @start_time, @end_time, @meeting_link, @teacher_id
              WHERE EXISTS (SELECT 1 FROM courses WHERE id = @course_id AND teacher_id = @teacher_id)
              RETURNING id",
            command => AddScheduleParameters(command, input, includeId: false));

        public bool UpdateScheduleItem(int teacherId, TeacherScheduleItemModel input) => ExecuteCourseChild(
            teacherId,
            input.CourseId,
            @"UPDATE online_sessions os
              SET title = @title, start_time = @start_time, end_time = @end_time, meeting_link = @meeting_link
              FROM courses c
              WHERE os.course_id = c.id AND c.teacher_id = @teacher_id AND os.id = @id AND os.course_id = @course_id",
            command => AddScheduleParameters(command, input, includeId: true));

        public bool DeleteScheduleItem(int teacherId, int scheduleId) => ExecuteOwnedDelete(
            teacherId,
            @"DELETE FROM online_sessions os USING courses c
              WHERE os.course_id = c.id AND c.teacher_id = @teacher_id AND os.id = @id",
            scheduleId);

        public async Task<bool> UpdateSessionStatusAsync(int teacherId, int sessionId, bool isOpened, string? meetingLink = null)
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync();

            const string query = @"
                UPDATE online_sessions os
                SET is_opened = @is_opened,
                    meeting_link = COALESCE(@meeting_link, os.meeting_link)
                FROM courses c
                WHERE os.course_id = c.id
                  AND c.teacher_id = @teacher_id
                  AND os.id = @session_id";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@session_id", sessionId);
            command.Parameters.AddWithValue("@is_opened", isOpened);
            command.Parameters.AddWithValue("@meeting_link", string.IsNullOrWhiteSpace(meetingLink) ? DBNull.Value : meetingLink);

            return await command.ExecuteNonQueryAsync() > 0;
        }

        public List<TeacherActiveExamSessionModel> GetActiveExamSessions(int teacherId)
        {
            var rows = new List<TeacherActiveExamSessionModel>();
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(@"
                SELECT a.id, ex.id, c.id, s.id,
                       COALESCE(c.name, ''), COALESCE(ex.title, ''), COALESCE(s.full_name, s.username, ''),
                       a.start_time, COALESCE(a.status, '')
                FROM exam_attempts a
                JOIN exams ex ON ex.id = a.exam_id
                JOIN courses c ON c.id = ex.course_id
                JOIN users s ON s.id = a.student_id
                WHERE c.teacher_id = @teacher_id
                  AND UPPER(COALESCE(a.status, '')) = 'IN_PROGRESS'
                  AND (ex.open_time IS NULL OR ex.open_time <= @now)
                  AND (ex.close_time IS NULL OR ex.close_time >= @now)
                ORDER BY a.start_time DESC", connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@now", DateTime.Now);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new TeacherActiveExamSessionModel
                {
                    AttemptId = reader.GetInt32(0),
                    ExamId = reader.GetInt32(1),
                    CourseId = reader.GetInt32(2),
                    StudentId = reader.GetInt32(3),
                    CourseName = reader.GetString(4),
                    ExamTitle = reader.GetString(5),
                    StudentName = reader.GetString(6),
                    StartTime = reader.IsDBNull(7) ? DateTime.MinValue : reader.GetDateTime(7),
                    Status = reader.GetString(8)
                });
            }
            return rows;
        }

        public int GetOpenExamsCount(int teacherId)
        {
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(@"
                SELECT COUNT(ex.id)
                FROM exams ex
                JOIN courses c ON c.id = ex.course_id
                WHERE c.teacher_id = @teacher_id
                  AND UPPER(COALESCE(ex.status, '')) = 'ACTIVE'
                  AND (ex.open_time IS NULL OR ex.open_time <= @now)
                  AND (ex.close_time IS NULL OR ex.close_time >= @now)", connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@now", DateTime.Now);
            object? result = command.ExecuteScalar();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        private List<TeacherLessonModel> QueryLessons(int teacherId, int? courseId)
        {
            var rows = new List<TeacherLessonModel>();
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            string query = @"
                SELECT l.id, l.course_id, COALESCE(c.name, ''), COALESCE(l.title, ''),
                       COALESCE(l.content, ''), l.publish_at, COALESCE(l.status, 'DRAFT'),
                       COALESCE(l.file_name, ''), COALESCE(l.file_path, ''),
                       COALESCE(l.content_type, ''), COALESCE(l.file_size, 0),
                       l.file_content IS NOT NULL
                FROM teacher_lessons l
                JOIN courses c ON c.id = l.course_id
                WHERE c.teacher_id = @teacher_id";
            if (courseId.HasValue)
                query += " AND c.id = @course_id";
            query += " ORDER BY l.publish_at DESC NULLS LAST, l.id DESC";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            if (courseId.HasValue)
                command.Parameters.AddWithValue("@course_id", courseId.Value);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new TeacherLessonModel
                {
                    Id = reader.GetInt32(0),
                    CourseId = reader.GetInt32(1),
                    CourseName = reader.GetString(2),
                    Title = reader.GetString(3),
                    Content = reader.GetString(4),
                    PublishAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    Status = reader.GetString(6),
                    FileName = reader.GetString(7),
                    FilePath = reader.GetString(8),
                    ContentType = reader.GetString(9),
                    FileSize = reader.GetInt64(10),
                    HasStoredContent = reader.GetBoolean(11)
                });
            }
            return rows;
        }

        private List<TeacherAssignmentModel> QueryAssignments(int teacherId, int? courseId)
        {
            var rows = new List<TeacherAssignmentModel>();
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            string query = @"
                SELECT a.id, a.course_id, COALESCE(c.name, ''), COALESCE(a.title, ''),
                       COALESCE(a.description, ''), a.due_at, COALESCE(a.status, 'OPEN'),
                       COALESCE(a.file_name, ''), COALESCE(a.file_path, ''),
                       COALESCE(a.content_type, ''), COALESCE(a.file_size, 0),
                       a.file_content IS NOT NULL
                FROM teacher_assignments a
                JOIN courses c ON c.id = a.course_id
                WHERE c.teacher_id = @teacher_id";
            if (courseId.HasValue)
                query += " AND c.id = @course_id";
            query += " ORDER BY a.due_at ASC NULLS LAST, a.id DESC";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            if (courseId.HasValue)
                command.Parameters.AddWithValue("@course_id", courseId.Value);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new TeacherAssignmentModel
                {
                    Id = reader.GetInt32(0),
                    CourseId = reader.GetInt32(1),
                    CourseName = reader.GetString(2),
                    Title = reader.GetString(3),
                    Description = reader.GetString(4),
                    DueAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    Status = reader.GetString(6),
                    FileName = reader.GetString(7),
                    FilePath = reader.GetString(8),
                    ContentType = reader.GetString(9),
                    FileSize = reader.GetInt64(10),
                    HasStoredContent = reader.GetBoolean(11)
                });
            }
            return rows;
        }

        private List<TeacherExamModel> QueryExams(int teacherId, int? courseId)
        {
            var rows = new List<TeacherExamModel>();
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            string query = @"
                SELECT ex.id, ex.course_id, COALESCE(c.name, ''), COALESCE(ex.title, ''),
                       ex.open_time, ex.close_time, COALESCE(ex.duration_minutes, 0),
                       COALESCE(ex.max_attempts, 1), COALESCE(ex.max_violations, 0), COUNT(eq.id)::int,
                       COALESCE(ex.status, '')
                FROM exams ex
                JOIN courses c ON c.id = ex.course_id
                LEFT JOIN exam_questions eq ON eq.exam_id = ex.id
                WHERE c.teacher_id = @teacher_id";
            if (courseId.HasValue)
                query += " AND c.id = @course_id";
            query += " GROUP BY ex.id, c.name ORDER BY ex.open_time DESC NULLS LAST, ex.id DESC";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            if (courseId.HasValue)
                command.Parameters.AddWithValue("@course_id", courseId.Value);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                DateTime? open = reader.IsDBNull(4) ? null : reader.GetDateTime(4);
                DateTime? close = reader.IsDBNull(5) ? null : reader.GetDateTime(5);
                rows.Add(new TeacherExamModel
                {
                    Id = reader.GetInt32(0),
                    CourseId = reader.GetInt32(1),
                    CourseName = reader.GetString(2),
                    Title = reader.GetString(3),
                    OpenTime = open,
                    CloseTime = close,
                    DurationMinutes = reader.GetInt32(6),
                    MaxAttempts = reader.GetInt32(7),
                    MaxViolations = reader.GetInt32(8),
                    QuestionCount = reader.GetInt32(9),
                    Status = reader.GetString(10),
                    StatusText = reader.GetString(10)
                });
            }
            return rows;
        }

        private List<TeacherStudentModel> QueryStudents(int teacherId, string status, int? courseId)
        {
            var rows = new List<TeacherStudentModel>();
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            string query = @"
                SELECT e.id, e.course_id, e.student_id, COALESCE(c.name, ''),
                       COALESCE(u.full_name, u.username, ''), COALESCE(u.email, ''),
                       COALESCE(e.status, ''), e.joined_at
                FROM enrollments e
                JOIN courses c ON c.id = e.course_id
                JOIN users u ON u.id = e.student_id
                WHERE c.teacher_id = @teacher_id
                  AND UPPER(COALESCE(e.status, '')) = UPPER(@status)";
            if (courseId.HasValue)
                query += " AND c.id = @course_id";
            query += " ORDER BY e.joined_at DESC";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@status", status);
            if (courseId.HasValue)
                command.Parameters.AddWithValue("@course_id", courseId.Value);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new TeacherStudentModel
                {
                    EnrollmentId = reader.GetInt32(0),
                    CourseId = reader.GetInt32(1),
                    StudentId = reader.GetInt32(2),
                    CourseName = reader.GetString(3),
                    StudentName = reader.GetString(4),
                    Email = reader.GetString(5),
                    Status = reader.GetString(6),
                    JoinedAt = reader.IsDBNull(7) ? DateTime.MinValue : reader.GetDateTime(7)
                });
            }
            return rows;
        }

        private List<TeacherMaterialModel> QueryMaterials(int teacherId, int? courseId)
        {
            var rows = new List<TeacherMaterialModel>();
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            string sql = @"
                SELECT m.id, m.course_id, COALESCE(c.name, ''), COALESCE(m.file_name, ''),
                       COALESCE(m.file_path, ''), m.uploaded_at,
                       COALESCE(m.content_type, ''), COALESCE(m.file_size, 0),
                       m.file_content IS NOT NULL
                FROM materials m
                JOIN courses c ON c.id = m.course_id
                WHERE c.teacher_id = @teacher_id";
            if (courseId.HasValue && courseId.Value > 0)
                sql += " AND c.id = @course_id";
            sql += " ORDER BY m.uploaded_at DESC NULLS LAST, m.id DESC";
            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            if (courseId.HasValue && courseId.Value > 0)
                command.Parameters.AddWithValue("@course_id", courseId.Value);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new TeacherMaterialModel
                {
                    Id = reader.GetInt32(0),
                    CourseId = reader.GetInt32(1),
                    CourseName = reader.GetString(2),
                    FileName = reader.GetString(3),
                    FilePath = reader.GetString(4),
                    UploadedAt = reader.IsDBNull(5) ? DateTime.MinValue : reader.GetDateTime(5),
                    ContentType = reader.GetString(6),
                    FileSize = reader.GetInt64(7),
                    HasStoredContent = reader.GetBoolean(8)
                });
            }
            return rows;
        }

        private List<TeacherTeachingTaskModel> QueryUpcomingTeachingSessions(int teacherId)
        {
            var rows = new List<TeacherTeachingTaskModel>();
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(@"
                SELECT c.id, COALESCE(os.title, c.name, ''), COALESCE(c.name, ''), os.start_time
                FROM online_sessions os
                JOIN courses c ON c.id = os.course_id
                WHERE c.teacher_id = @teacher_id
                  AND os.start_time >= CURRENT_TIMESTAMP
                  AND os.start_time < CURRENT_TIMESTAMP + INTERVAL '7 days'
                ORDER BY os.start_time ASC
                LIMIT 5", connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                DateTime? start = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
                rows.Add(new TeacherTeachingTaskModel
                {
                    CourseId = reader.GetInt32(0),
                    ToolId = "schedule",
                    Category = "Lịch dạy",
                    Title = reader.GetString(1),
                    Subtitle = reader.GetString(2),
                    DueAt = start,
                    StatusText = start.HasValue && start.Value.Date == DateTime.Today ? "Hôm nay" : "Sắp tới",
                    RequiresAction = false
                });
            }
            return rows;
        }

        private List<TeacherTeachingTaskModel> QueryAssignmentTasks(int teacherId)
        {
            var rows = new List<TeacherTeachingTaskModel>();
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(@"
                SELECT c.id, COALESCE(a.title, ''), COALESCE(c.name, ''), a.due_at, COALESCE(a.status, '')
                FROM teacher_assignments a
                JOIN courses c ON c.id = a.course_id
                WHERE c.teacher_id = @teacher_id
                  AND UPPER(COALESCE(a.status, '')) IN ('OPEN', 'ACTIVE', 'PENDING')
                  AND (a.due_at IS NULL OR a.due_at <= CURRENT_TIMESTAMP + INTERVAL '7 days')
                ORDER BY a.due_at ASC NULLS LAST
                LIMIT 5", connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                DateTime? due = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
                rows.Add(new TeacherTeachingTaskModel
                {
                    CourseId = reader.GetInt32(0),
                    ToolId = "assignments",
                    Category = "Bài tập",
                    Title = reader.GetString(1),
                    Subtitle = reader.GetString(2),
                    DueAt = due,
                    StatusText = due.HasValue && due.Value < DateTime.Now ? "Quá hạn/cần xem" : "Sắp hết hạn",
                    RequiresAction = true
                });
            }
            return rows;
        }

        private List<TeacherTeachingTaskModel> QueryExamTasks(int teacherId)
        {
            var rows = new List<TeacherTeachingTaskModel>();
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(@"
                SELECT c.id, COALESCE(ex.title, ''), COALESCE(c.name, ''), ex.open_time, ex.close_time
                FROM exams ex
                JOIN courses c ON c.id = ex.course_id
                WHERE c.teacher_id = @teacher_id
                  AND (ex.close_time IS NULL OR ex.close_time >= CURRENT_TIMESTAMP)
                  AND (ex.open_time IS NULL OR ex.open_time <= CURRENT_TIMESTAMP + INTERVAL '7 days')
                ORDER BY ex.open_time ASC NULLS LAST
                LIMIT 5", connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                DateTime? open = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
                DateTime? close = reader.IsDBNull(4) ? null : reader.GetDateTime(4);
                bool openNow = (!open.HasValue || open.Value <= DateTime.Now) && (!close.HasValue || close.Value >= DateTime.Now);
                rows.Add(new TeacherTeachingTaskModel
                {
                    CourseId = reader.GetInt32(0),
                    ToolId = "exams",
                    Category = "Bài kiểm tra",
                    Title = reader.GetString(1),
                    Subtitle = reader.GetString(2),
                    DueAt = open,
                    StatusText = openNow ? "Đang mở" : "Sắp mở",
                    RequiresAction = openNow
                });
            }
            return rows;
        }

        private List<TeacherTeachingTaskModel> QueryMonitoringTasks(int teacherId)
        {
            var rows = new List<TeacherTeachingTaskModel>();
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(@"
                SELECT c.id, COALESCE(v.type, 'Cảnh báo thi'), COALESCE(c.name, ''), v.created_at
                FROM violations v
                JOIN exam_attempts a ON a.id = v.exam_attempt_id
                JOIN exams ex ON ex.id = a.exam_id
                JOIN courses c ON c.id = ex.course_id
                WHERE c.teacher_id = @teacher_id
                  AND v.created_at >= CURRENT_TIMESTAMP - INTERVAL '7 days'
                ORDER BY v.created_at DESC
                LIMIT 5", connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                DateTime? created = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
                rows.Add(new TeacherTeachingTaskModel
                {
                    CourseId = reader.GetInt32(0),
                    ToolId = "exams",
                    Category = "Giám sát thi",
                    Title = reader.GetString(1),
                    Subtitle = reader.GetString(2),
                    DueAt = created,
                    StatusText = "Cần xem lại",
                    RequiresAction = true
                });
            }
            return rows;
        }

        private List<TeacherScheduleItemModel> QuerySchedule(int teacherId)
        {
            var rows = new List<TeacherScheduleItemModel>();
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(@"
                SELECT os.id, os.course_id, COALESCE(c.name, ''), COALESCE(os.title, ''),
                       os.start_time, os.end_time, COALESCE(os.meeting_link, ''),
                       COALESCE(os.is_opened, FALSE)
                FROM online_sessions os
                JOIN courses c ON c.id = os.course_id
                WHERE c.teacher_id = @teacher_id
                ORDER BY os.start_time ASC NULLS LAST, os.id DESC", connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new TeacherScheduleItemModel
                {
                    Id = reader.GetInt32(0),
                    CourseId = reader.GetInt32(1),
                    CourseName = reader.GetString(2),
                    Title = reader.GetString(3),
                    StartTime = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                    EndTime = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    MeetingLink = reader.GetString(6),
                    IsOpened = reader.GetBoolean(7)
                });
            }
            return rows;
        }

        private void EnsureTeacherSchema()
        {
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using (var command = new NpgsqlCommand(@"
                ALTER TABLE courses
                    ADD COLUMN IF NOT EXISTS rejection_reason TEXT;

                ALTER TABLE notifications
                    ADD COLUMN IF NOT EXISTS category VARCHAR(32) NOT NULL DEFAULT 'SystemAdmin',
                    ADD COLUMN IF NOT EXISTS notification_type VARCHAR(32) NOT NULL DEFAULT 'Informational',
                    ADD COLUMN IF NOT EXISTS source_type VARCHAR(64),
                    ADD COLUMN IF NOT EXISTS source_id INT;", connection))
                command.ExecuteNonQuery();

            using (var command = new NpgsqlCommand(@"
                ALTER TABLE online_sessions
                    ADD COLUMN IF NOT EXISTS recurring_rule TEXT,
                    ADD COLUMN IF NOT EXISTS is_opened BOOLEAN DEFAULT FALSE,
                    ADD COLUMN IF NOT EXISTS meeting_link TEXT;

                CREATE INDEX IF NOT EXISTS idx_online_sessions_course_start
                    ON online_sessions(course_id, start_time);
                CREATE INDEX IF NOT EXISTS idx_online_sessions_opened
                    ON online_sessions(is_opened);", connection))
                command.ExecuteNonQuery();

            using (var command = new NpgsqlCommand(@"
                CREATE TABLE IF NOT EXISTS attendance_logs (
                    id SERIAL PRIMARY KEY,
                    student_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                    session_id INT NOT NULL REFERENCES online_sessions(id) ON DELETE CASCADE,
                    joined_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    left_at TIMESTAMP,
                    duration_minutes INT DEFAULT 0,
                    is_valid BOOLEAN DEFAULT FALSE,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );

                CREATE INDEX IF NOT EXISTS idx_attendance_logs_session_student
                    ON attendance_logs(session_id, student_id);
                CREATE INDEX IF NOT EXISTS idx_attendance_logs_open_session_student
                    ON attendance_logs(session_id, student_id)
                    WHERE left_at IS NULL;", connection))
                command.ExecuteNonQuery();

            using (var command = new NpgsqlCommand(@"
                CREATE TABLE IF NOT EXISTS teacher_profiles (
                    user_id INTEGER PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
                    teacher_code VARCHAR(32) UNIQUE NOT NULL,
                    phone TEXT,
                    gender TEXT,
                    birth_date DATE,
                    address TEXT,
                    major TEXT,
                    degrees TEXT,
                    bio TEXT,
                    avatar_path TEXT,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );", connection))
                command.ExecuteNonQuery();
            using (var command = new NpgsqlCommand("ALTER TABLE teacher_profiles ADD COLUMN IF NOT EXISTS avatar_path TEXT;", connection))
                command.ExecuteNonQuery();
            using (var command = new NpgsqlCommand(@"
                INSERT INTO teacher_profiles (user_id, teacher_code)
                SELECT u.id, 'GV' || LPAD(u.id::text, 5, '0')
                FROM users u
                JOIN roles r ON r.id = u.role_id
                WHERE UPPER(r.name) = 'TEACHER'
                ON CONFLICT (user_id) DO NOTHING;", connection))
                command.ExecuteNonQuery();
            using (var command = new NpgsqlCommand(@"
                CREATE TABLE IF NOT EXISTS teacher_lessons (
                    id SERIAL PRIMARY KEY,
                    course_id INT NOT NULL REFERENCES courses(id) ON DELETE CASCADE,
                    title VARCHAR(200) NOT NULL,
                    content TEXT,
                    publish_at TIMESTAMP,
                    status VARCHAR(20) DEFAULT 'DRAFT',
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );", connection))
                command.ExecuteNonQuery();
            using (var command = new NpgsqlCommand(@"
                CREATE TABLE IF NOT EXISTS teacher_assignments (
                    id SERIAL PRIMARY KEY,
                    course_id INT NOT NULL REFERENCES courses(id) ON DELETE CASCADE,
                    title VARCHAR(200) NOT NULL,
                    description TEXT,
                    due_at TIMESTAMP,
                    status VARCHAR(20) DEFAULT 'OPEN',
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    file_name VARCHAR(255),
                    file_path VARCHAR(500),
                    content_type VARCHAR(100),
                    file_size BIGINT,
                    file_content BYTEA
                );", connection))
                command.ExecuteNonQuery();
            using (var command = new NpgsqlCommand(@"
                ALTER TABLE teacher_assignments 
                ADD COLUMN IF NOT EXISTS file_name VARCHAR(255),
                ADD COLUMN IF NOT EXISTS file_path VARCHAR(500),
                ADD COLUMN IF NOT EXISTS content_type VARCHAR(100),
                ADD COLUMN IF NOT EXISTS file_size BIGINT,
                ADD COLUMN IF NOT EXISTS file_content BYTEA;", connection))
                command.ExecuteNonQuery();
            using (var command = new NpgsqlCommand(@"
                CREATE TABLE IF NOT EXISTS assignment_submissions (
                    id SERIAL PRIMARY KEY,
                    assignment_id INT NOT NULL REFERENCES teacher_assignments(id) ON DELETE CASCADE,
                    student_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                    file_name VARCHAR(255) NOT NULL,
                    file_size BIGINT NOT NULL,
                    file_content BYTEA NOT NULL,
                    content_type VARCHAR(100),
                    submitted_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    score DECIMAL(5,2),
                    feedback TEXT,
                    status VARCHAR(20) DEFAULT 'SUBMITTED'
                );
                CREATE INDEX IF NOT EXISTS idx_assignment_submissions_assignment ON assignment_submissions(assignment_id);
                CREATE INDEX IF NOT EXISTS idx_assignment_submissions_student ON assignment_submissions(student_id);", connection))
                command.ExecuteNonQuery();
            using (var command = new NpgsqlCommand("CREATE INDEX IF NOT EXISTS idx_teacher_lessons_course ON teacher_lessons(course_id);", connection))
                command.ExecuteNonQuery();
            using (var command = new NpgsqlCommand("CREATE INDEX IF NOT EXISTS idx_teacher_assignments_course ON teacher_assignments(course_id);", connection))
                command.ExecuteNonQuery();
            using (var command = new NpgsqlCommand(@"
                ALTER TABLE exams
                    ADD COLUMN IF NOT EXISTS status VARCHAR(20) NOT NULL DEFAULT 'DRAFT';

                ALTER TABLE exams
                    ADD COLUMN IF NOT EXISTS max_violations INT NOT NULL DEFAULT 0;

                ALTER TABLE exams
                    ALTER COLUMN status SET DEFAULT 'DRAFT';

                UPDATE exams
                SET status = CASE
                    WHEN close_time IS NOT NULL AND close_time < CURRENT_TIMESTAMP THEN 'CLOSED'
                    ELSE 'DRAFT'
                END
                WHERE status IS NULL OR TRIM(status) = '';

                CREATE TABLE IF NOT EXISTS exam_questions (
                    id SERIAL PRIMARY KEY,
                    exam_id INT NOT NULL REFERENCES exams(id) ON DELETE CASCADE,
                    question_text TEXT NOT NULL,
                    option_a TEXT NOT NULL,
                    option_b TEXT NOT NULL,
                    option_c TEXT NOT NULL,
                    option_d TEXT NOT NULL,
                    correct_option CHAR(1) NOT NULL DEFAULT 'A',
                    points NUMERIC(6,2) NOT NULL DEFAULT 0,
                    display_order INT NOT NULL DEFAULT 1,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );

                DO $$
                DECLARE
                    existing_primary_key TEXT;
                BEGIN
                    ALTER TABLE exam_questions
                        ADD COLUMN IF NOT EXISTS id INTEGER;

                    CREATE SEQUENCE IF NOT EXISTS exam_questions_id_seq;

                    UPDATE exam_questions
                    SET id = nextval('exam_questions_id_seq')
                    WHERE id IS NULL;

                    PERFORM setval(
                        'exam_questions_id_seq',
                        COALESCE((SELECT MAX(id) FROM exam_questions), 1),
                        (SELECT COALESCE(MAX(id), 0) > 0 FROM exam_questions)
                    );

                    ALTER TABLE exam_questions
                        ALTER COLUMN id SET DEFAULT nextval('exam_questions_id_seq');

                    SELECT c.conname
                    INTO existing_primary_key
                    FROM pg_constraint c
                    WHERE c.conrelid = 'exam_questions'::regclass
                      AND c.contype = 'p'
                      AND NOT (
                          array_length(c.conkey, 1) = 1
                          AND c.conkey[1] = (
                              SELECT a.attnum
                              FROM pg_attribute a
                              WHERE a.attrelid = 'exam_questions'::regclass
                                AND a.attname = 'id'
                                AND NOT a.attisdropped
                          )
                      )
                    LIMIT 1;

                    IF existing_primary_key IS NOT NULL THEN
                        EXECUTE format('ALTER TABLE exam_questions DROP CONSTRAINT %I', existing_primary_key);
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = current_schema()
                          AND table_name = 'exam_questions'
                          AND column_name = 'question_id'
                    ) THEN
                        ALTER TABLE exam_questions
                            ALTER COLUMN question_id DROP NOT NULL;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM pg_index i
                        WHERE i.indrelid = 'exam_questions'::regclass
                          AND i.indisprimary
                    ) THEN
                        ALTER TABLE exam_questions
                            ADD CONSTRAINT exam_questions_pkey PRIMARY KEY (id);
                    END IF;
                END $$;

                ALTER TABLE exam_questions
                    ADD COLUMN IF NOT EXISTS question_text TEXT,
                    ADD COLUMN IF NOT EXISTS option_a TEXT,
                    ADD COLUMN IF NOT EXISTS option_b TEXT,
                    ADD COLUMN IF NOT EXISTS option_c TEXT,
                    ADD COLUMN IF NOT EXISTS option_d TEXT,
                    ADD COLUMN IF NOT EXISTS correct_option CHAR(1) DEFAULT 'A',
                    ADD COLUMN IF NOT EXISTS points NUMERIC(6,2) DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS display_order INT DEFAULT 1,
                    ADD COLUMN IF NOT EXISTS difficulty VARCHAR(10) NOT NULL DEFAULT 'MEDIUM',
                    ADD COLUMN IF NOT EXISTS chapter VARCHAR(100),
                    ADD COLUMN IF NOT EXISTS question_type VARCHAR(20) NOT NULL DEFAULT 'MULTIPLE_CHOICE';

                CREATE INDEX IF NOT EXISTS idx_exams_status ON exams(status);
                CREATE INDEX IF NOT EXISTS idx_exam_questions_exam ON exam_questions(exam_id);
                CREATE INDEX IF NOT EXISTS idx_exam_questions_difficulty ON exam_questions(difficulty);
                CREATE INDEX IF NOT EXISTS idx_exam_questions_chapter ON exam_questions(chapter) WHERE chapter IS NOT NULL;

                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_exam_questions_difficulty') THEN
                        ALTER TABLE exam_questions
                            ADD CONSTRAINT chk_exam_questions_difficulty
                            CHECK (difficulty IN ('EASY', 'MEDIUM', 'HARD'));
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_exam_questions_question_type') THEN
                        ALTER TABLE exam_questions
                            ADD CONSTRAINT chk_exam_questions_question_type
                            CHECK (question_type IN ('MULTIPLE_CHOICE', 'TRUE_FALSE', 'FILL_BLANK'));
                    END IF;
                END $$;

                DO $$
                BEGIN
                    IF to_regclass('public.violations') IS NOT NULL THEN
                        ALTER TABLE violations
                            ADD COLUMN IF NOT EXISTS severity VARCHAR(10) NOT NULL DEFAULT 'MEDIUM',
                            ADD COLUMN IF NOT EXISTS action_taken VARCHAR(50),
                            ADD COLUMN IF NOT EXISTS image_url TEXT;

                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_violations_severity') THEN
                            ALTER TABLE violations
                                ADD CONSTRAINT chk_violations_severity
                                CHECK (severity IN ('LOW', 'MEDIUM', 'HIGH'));
                        END IF;
                    END IF;
                END $$;

                CREATE TABLE IF NOT EXISTS exam_attempt_answers (
                    attempt_id INT NOT NULL REFERENCES exam_attempts(id) ON DELETE CASCADE,
                    exam_question_id INT NOT NULL REFERENCES exam_questions(id) ON DELETE CASCADE,
                    selected_option CHAR(1) NOT NULL,
                    is_correct BOOLEAN,
                    score NUMERIC(6,2) NOT NULL DEFAULT 0,
                    answered_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (attempt_id, exam_question_id)
                );

                CREATE INDEX IF NOT EXISTS idx_exam_attempt_answers_attempt ON exam_attempt_answers(attempt_id);
                CREATE INDEX IF NOT EXISTS idx_exam_attempt_answers_question ON exam_attempt_answers(exam_question_id);

                ALTER TABLE materials
                    ADD COLUMN IF NOT EXISTS content_type VARCHAR(120),
                    ADD COLUMN IF NOT EXISTS file_size BIGINT NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS file_content BYTEA;

                ALTER TABLE teacher_lessons
                    ADD COLUMN IF NOT EXISTS file_name VARCHAR(255),
                    ADD COLUMN IF NOT EXISTS file_path VARCHAR(500),
                    ADD COLUMN IF NOT EXISTS content_type VARCHAR(120),
                    ADD COLUMN IF NOT EXISTS file_size BIGINT,
                    ADD COLUMN IF NOT EXISTS file_content BYTEA;

                CREATE TABLE IF NOT EXISTS student_hidden_results (
                    student_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                    attempt_id INT NOT NULL REFERENCES exam_attempts(id) ON DELETE CASCADE,
                    hidden_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (student_id, attempt_id)
                );

                CREATE INDEX IF NOT EXISTS idx_student_hidden_results_student ON student_hidden_results(student_id);", connection))
                command.ExecuteNonQuery();
        }

        private static int Count(NpgsqlConnection connection, string sql, int teacherId)
        {
            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            return Convert.ToInt32(command.ExecuteScalar());
        }

        private static void InsertGeneratedSessions(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            int teacherId,
            int courseId,
            TeacherCourseModel input)
        {
            if (!input.StartDate.HasValue || !input.EndDate.HasValue)
                return;
            if (!input.SessionStartTime.HasValue || !input.SessionEndTime.HasValue)
                return;
            if (input.TeachingDays.Count == 0)
                return;

            DateTime startDate = input.StartDate.Value.Date;
            DateTime endDate = input.EndDate.Value.Date;
            if (endDate < startDate || input.SessionEndTime.Value <= input.SessionStartTime.Value)
                return;

            for (DateTime day = startDate; day <= endDate; day = day.AddDays(1))
            {
                if (!input.TeachingDays.Contains(day.DayOfWeek))
                    continue;

                DateTime start = day.Add(input.SessionStartTime.Value);
                DateTime end = day.Add(input.SessionEndTime.Value);
                using var sessionCommand = new NpgsqlCommand(@"
                    INSERT INTO online_sessions (course_id, title, start_time, end_time, meeting_link, created_by)
                    VALUES (@course_id, @title, @start_time, @end_time, @meeting_link, @teacher_id)", connection, transaction);
                sessionCommand.Parameters.AddWithValue("@course_id", courseId);
                sessionCommand.Parameters.AddWithValue("@title", string.IsNullOrWhiteSpace(input.Name) ? "Buổi học" : input.Name);
                sessionCommand.Parameters.AddWithValue("@start_time", start);
                sessionCommand.Parameters.AddWithValue("@end_time", end);
                sessionCommand.Parameters.AddWithValue("@meeting_link", string.IsNullOrWhiteSpace(input.MeetingLink) ? DBNull.Value : input.MeetingLink);
                sessionCommand.Parameters.AddWithValue("@teacher_id", teacherId);
                sessionCommand.ExecuteNonQuery();
            }
        }

        private static void AddCourseParameters(NpgsqlCommand command, int teacherId, TeacherCourseModel input, bool includeId)
        {
            if (includeId)
                command.Parameters.AddWithValue("@id", input.Id);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@name", input.Name);
            command.Parameters.AddWithValue("@description", input.Description);
            string status = NormalizeTeacherEditableCourseStatus(input.Status);
            command.Parameters.AddWithValue("@status", status);
            command.Parameters.AddWithValue("@start_date", input.StartDate.HasValue ? input.StartDate.Value : DBNull.Value);
            command.Parameters.AddWithValue("@end_date", input.EndDate.HasValue ? input.EndDate.Value : DBNull.Value);
            command.Parameters.AddWithValue("@teaching_days", input.TeachingDays != null && input.TeachingDays.Count > 0 ? string.Join(",", input.TeachingDays) : DBNull.Value);
            command.Parameters.AddWithValue("@session_start_time", input.SessionStartTime.HasValue ? input.SessionStartTime.Value : DBNull.Value);
            command.Parameters.AddWithValue("@session_end_time", input.SessionEndTime.HasValue ? input.SessionEndTime.Value : DBNull.Value);
            command.Parameters.AddWithValue("@frequency", "Weekly");
            command.Parameters.AddWithValue("@meeting_link", input.MeetingLink ?? string.Empty);
        }

        private static string NormalizeTeacherEditableCourseStatus(string? status)
        {
            string value = (status ?? string.Empty).Trim().ToUpperInvariant();
            return value switch
            {
                WorkflowConstants.CourseStatus.Pending => WorkflowConstants.CourseStatus.Pending,
                WorkflowConstants.CourseStatus.Closed => WorkflowConstants.CourseStatus.Closed,
                _ => WorkflowConstants.CourseStatus.Draft
            };
        }

        private static void AddExamParameters(NpgsqlCommand command, TeacherExamModel input, bool includeId)
        {
            if (includeId)
                command.Parameters.AddWithValue("@id", input.Id);
            command.Parameters.AddWithValue("@title", input.Title);
            command.Parameters.AddWithValue("@open_time", input.OpenTime.HasValue ? input.OpenTime.Value : DBNull.Value);
            command.Parameters.AddWithValue("@close_time", input.CloseTime.HasValue ? input.CloseTime.Value : DBNull.Value);
            command.Parameters.AddWithValue("@duration_minutes", input.DurationMinutes);
            command.Parameters.AddWithValue("@max_attempts", input.MaxAttempts <= 0 ? 1 : input.MaxAttempts);
            command.Parameters.AddWithValue("@max_violations", Math.Max(0, input.MaxViolations));
            command.Parameters.AddWithValue("@status", includeId ? NormalizeExamStatus(input.Status) : WorkflowConstants.ExamStatus.Draft);
        }

        private static void AddExamWorkflowStatusParameters(NpgsqlCommand command)
        {
            command.Parameters.AddWithValue("@active_status", WorkflowConstants.ExamStatus.Active);
            command.Parameters.AddWithValue("@closed_status", WorkflowConstants.ExamStatus.Closed);
            command.Parameters.AddWithValue("@draft_status", WorkflowConstants.ExamStatus.Draft);
        }

        private static string NormalizeExamStatus(string? status)
        {
            string value = (status ?? string.Empty).Trim().ToUpperInvariant();
            return value switch
            {
                WorkflowConstants.ExamStatus.Active => WorkflowConstants.ExamStatus.Active,
                WorkflowConstants.ExamStatus.Closed => WorkflowConstants.ExamStatus.Closed,
                _ => WorkflowConstants.ExamStatus.Draft
            };
        }

        private static void AddQuestionParameters(NpgsqlCommand command, int teacherId, TeacherExamQuestionModel input, int displayOrder)
        {
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@exam_id", input.ExamId);
            command.Parameters.AddWithValue("@question_text", input.QuestionText.Trim());
            command.Parameters.AddWithValue("@option_a", input.OptionA.Trim());
            command.Parameters.AddWithValue("@option_b", input.OptionB.Trim());
            command.Parameters.AddWithValue("@option_c", input.OptionC.Trim());
            command.Parameters.AddWithValue("@option_d", input.OptionD.Trim());
            command.Parameters.AddWithValue("@correct_option", NormalizeCorrectOption(input.CorrectOption));
            command.Parameters.AddWithValue("@display_order", displayOrder);
            command.Parameters.AddWithValue("@difficulty", QuestionMetadataNormalizer.NormalizeDifficulty(input.Difficulty));
            command.Parameters.AddWithValue("@chapter", QuestionMetadataNormalizer.NormalizeChapter(input.Chapter) ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@question_type", QuestionMetadataNormalizer.NormalizeQuestionType(input.QuestionType));
        }

        private static string NormalizeCorrectOption(string? value)
        {
            string option = (value ?? "A").Trim().ToUpperInvariant();
            return option is "A" or "B" or "C" or "D" ? option : "A";
        }

        private static int GetNextQuestionOrder(NpgsqlConnection connection, NpgsqlTransaction transaction, int teacherId, int examId)
        {
            using var command = new NpgsqlCommand(@"
                SELECT COALESCE(MAX(eq.display_order), 0) + 1
                FROM exams ex
                JOIN courses c ON c.id = ex.course_id
                LEFT JOIN exam_questions eq ON eq.exam_id = ex.id
                WHERE c.teacher_id = @teacher_id
                  AND ex.id = @exam_id", connection, transaction);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@exam_id", examId);
            return Convert.ToInt32(command.ExecuteScalar());
        }

        private static void RecalculateQuestionPoints(NpgsqlConnection connection, NpgsqlTransaction transaction, int examId)
        {
            using var countCommand = new NpgsqlCommand("SELECT COUNT(*) FROM exam_questions WHERE exam_id = @exam_id", connection, transaction);
            countCommand.Parameters.AddWithValue("@exam_id", examId);
            int count = Convert.ToInt32(countCommand.ExecuteScalar());
            if (count <= 0)
                return;

            decimal points = Math.Round(10m / count, 2, MidpointRounding.AwayFromZero);
            using var updateCommand = new NpgsqlCommand("UPDATE exam_questions SET points = @points WHERE exam_id = @exam_id", connection, transaction);
            updateCommand.Parameters.AddWithValue("@points", points);
            updateCommand.Parameters.AddWithValue("@exam_id", examId);
            updateCommand.ExecuteNonQuery();
        }

        private static void AddScheduleParameters(NpgsqlCommand command, TeacherScheduleItemModel input, bool includeId)
        {
            if (includeId)
                command.Parameters.AddWithValue("@id", input.Id);
            command.Parameters.AddWithValue("@title", input.Title);
            command.Parameters.AddWithValue("@start_time", input.StartTime.HasValue ? input.StartTime.Value : DBNull.Value);
            command.Parameters.AddWithValue("@end_time", input.EndTime.HasValue ? input.EndTime.Value : DBNull.Value);
            command.Parameters.AddWithValue("@meeting_link", input.MeetingLink);
        }

        private int InsertCourseChild(int teacherId, int courseId, string sql, Action<NpgsqlCommand> addParameters)
        {
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@course_id", courseId);
            addParameters(command);
            object? result = command.ExecuteScalar();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        private bool ExecuteCourseChild(int teacherId, int courseId, string sql, Action<NpgsqlCommand> addParameters)
        {
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@course_id", courseId);
            addParameters(command);
            return command.ExecuteNonQuery() > 0;
        }

        private bool ExecuteOwnedDelete(int teacherId, string sql, int id)
        {
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@id", id);
            return command.ExecuteNonQuery() > 0;
        }

        private bool UpdateEnrollmentStatus(int teacherId, int courseId, int studentId, bool approve)
        {
            using var connection = _dbContext.CreateConnection();
            connection.Open();
            var info = GetEnrollmentNotificationInfo(connection, teacherId, courseId, studentId);
            string sql = approve
                ? @"UPDATE enrollments e
                    SET status = 'ACTIVE'
                    FROM courses c
                    WHERE e.course_id = c.id AND c.teacher_id = @teacher_id
                      AND e.course_id = @course_id AND e.student_id = @student_id
                      AND UPPER(e.status) = 'PENDING'"
                : @"UPDATE enrollments e
                    SET status = 'REJECTED'
                    FROM courses c
                    WHERE e.course_id = c.id AND c.teacher_id = @teacher_id
                      AND e.course_id = @course_id AND e.student_id = @student_id
                      AND UPPER(e.status) = 'PENDING'";
            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@course_id", courseId);
            command.Parameters.AddWithValue("@student_id", studentId);
            bool updated = command.ExecuteNonQuery() > 0;
            if (updated && info.StudentId > 0)
                NotifyStudentEnrollmentDecision(info, approve);
            return updated;
        }

        private static (int StudentId, string CourseName) GetEnrollmentNotificationInfo(NpgsqlConnection connection, int teacherId, int courseId, int studentId)
        {
            using var command = new NpgsqlCommand(@"
                SELECT e.student_id, COALESCE(c.name, '')
                FROM enrollments e
                JOIN courses c ON c.id = e.course_id
                WHERE c.teacher_id = @teacher_id
                  AND e.course_id = @course_id
                  AND e.student_id = @student_id", connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@course_id", courseId);
            command.Parameters.AddWithValue("@student_id", studentId);
            using var reader = command.ExecuteReader();
            return reader.Read()
                ? (reader.GetInt32(0), reader.GetString(1))
                : (0, string.Empty);
        }

        private void NotifyStudentEnrollmentDecision((int StudentId, string CourseName) info, bool approved)
        {
            try
            {
                string title = approved ? "Yêu cầu ghi danh đã được duyệt" : "Yêu cầu ghi danh bị từ chối";
                string content = approved
                    ? $"Bạn đã được duyệt tham gia khóa học \"{info.CourseName}\"."
                    : $"Yêu cầu tham gia khóa học \"{info.CourseName}\" đã bị từ chối.";
                _notifications.Create(
                    info.StudentId,
                    title,
                    content,
                    WorkflowConstants.NotificationCategory.Enrollment,
                    WorkflowConstants.NotificationType.Informational,
                    "Course",
                    null);
            }
            catch
            {
            }
        }

        public async Task<List<StudentSubmissionModel>> GetStudentSubmissionsAsync(int teacherId, int? courseId = null, System.Threading.CancellationToken cancellationToken = default)
        {
            var results = new List<StudentSubmissionModel>();
            await using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            string query = @"
                SELECT s.id, s.assignment_id, a.course_id, c.name as course_name, 
                       a.title as assignment_title, s.student_id, u.full_name as student_name, 
                       s.file_name, s.submitted_at, s.status, s.score, s.feedback
                FROM assignment_submissions s
                JOIN teacher_assignments a ON s.assignment_id = a.id
                JOIN courses c ON a.course_id = c.id
                JOIN users u ON s.student_id = u.id
                WHERE c.teacher_id = @teacherId ";

            if (courseId.HasValue && courseId.Value > 0)
                query += " AND c.id = @courseId ";

            query += " ORDER BY c.name ASC, s.submitted_at ASC";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@teacherId", teacherId);
            if (courseId.HasValue && courseId.Value > 0)
                command.Parameters.AddWithValue("@courseId", courseId.Value);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new StudentSubmissionModel
                {
                    SubmissionId = reader.GetInt32(0),
                    AssignmentId = reader.GetInt32(1),
                    CourseId = reader.GetInt32(2),
                    CourseName = reader.GetString(3),
                    AssignmentTitle = reader.GetString(4),
                    StudentId = reader.GetInt32(5),
                    StudentName = reader.GetString(6),
                    FileName = reader.GetString(7),
                    SubmittedAt = reader.GetDateTime(8),
                    Status = reader.IsDBNull(9) ? "SUBMITTED" : reader.GetString(9),
                    Score = reader.IsDBNull(10) ? null : reader.GetDecimal(10),
                    Feedback = reader.IsDBNull(11) ? null : reader.GetString(11)
                });
            }
            return results;
        }

        public async Task<byte[]?> GetSubmissionContentAsync(int teacherId, int submissionId, System.Threading.CancellationToken cancellationToken = default)
        {
            await using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            string query = @"
                SELECT s.file_content
                FROM assignment_submissions s
                JOIN teacher_assignments a ON s.assignment_id = a.id
                JOIN courses c ON a.course_id = c.id
                WHERE s.id = @id
                  AND c.teacher_id = @teacher_id
                LIMIT 1";
            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@id", submissionId);
            command.Parameters.AddWithValue("@teacher_id", teacherId);

            object? result = await command.ExecuteScalarAsync(cancellationToken);
            return result != DBNull.Value && result is byte[] bytes ? bytes : null;
        }

        public async Task<bool> UpdateGradeAsync(int teacherId, int submissionId, decimal score, string feedback, System.Threading.CancellationToken cancellationToken = default)
        {
            await using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            string query = @"
                WITH owned_submission AS (
                    SELECT s.id
                    FROM assignment_submissions s
                    JOIN teacher_assignments a ON s.assignment_id = a.id
                    JOIN courses c ON a.course_id = c.id
                    WHERE s.id = @id
                      AND c.teacher_id = @teacher_id
                ),
                updated AS (
                    UPDATE assignment_submissions s
                    SET score = @score, feedback = @feedback, status = 'GRADED'
                    FROM owned_submission os
                    WHERE s.id = os.id
                    RETURNING s.student_id, s.assignment_id
                )
                SELECT u.student_id, u.assignment_id, a.title, c.name
                FROM updated u
                JOIN teacher_assignments a ON u.assignment_id = a.id
                JOIN courses c ON a.course_id = c.id";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@id", submissionId);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@score", score);
            command.Parameters.AddWithValue("@feedback", string.IsNullOrWhiteSpace(feedback) ? DBNull.Value : feedback);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                int studentId = reader.GetInt32(0);
                int assignmentId = reader.GetInt32(1);
                string title = reader.GetString(2);
                string courseName = reader.GetString(3);

                try
                {
                    _notifications.Create(
                        studentId,
                        $"Đã có điểm bài tập: {title}",
                        $"Bài tập \"{title}\" thuộc khóa học \"{courseName}\" đã được chấm. Điểm của bạn: {score.ToString("0.##")}",
                        WorkflowConstants.NotificationCategory.Assignment,
                        WorkflowConstants.NotificationType.Informational,
                        "Assignment",
                        assignmentId);
                }
                catch
                {
                    // Ignore notification errors to not block grading process
                }
                return true;
            }
            return false;
        }

        private static string BuildExamStatus(DateTime? open, DateTime? close)
        {
            DateTime now = DateTime.Now;
            if (open.HasValue && open.Value > now)
                return "Sắp mở";
            if (close.HasValue && close.Value < now)
                return "Đã đóng";
            return "Đang mở";
        }
    }
}
