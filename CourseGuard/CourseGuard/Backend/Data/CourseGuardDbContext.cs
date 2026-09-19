using System.Data;
using System.Threading;
using System.Threading.Tasks;
using CourseGuard.Backend.Config;
using Npgsql;
using NpgsqlTypes;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Services;
using System.Collections.Generic;

namespace CourseGuard.Backend.Data
{
    /// <summary>
    /// Core Data Context for CourseGuard.
    /// Simplified access replacing Repositories.
    /// </summary>
    public class CourseGuardDbContext : IDeadlineReminderStore
    {
        private readonly string _connectionString;

        public CourseGuardDbContext(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _connectionString = AppEnvironment.GetRequired(
                    "COURSEGUARD_DB_CONNECTION",
                    "SUPABASE_DB_CONNECTION",
                    "CONNECTION_STRING");
            }
            else
            {
                _connectionString = connectionString;
            }
        }

        public NpgsqlConnection CreateConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }

        private static void EnsureUserSecuritySchema(NpgsqlConnection connection)
        {
            const string sql = @"
                ALTER TABLE users
                    ADD COLUMN IF NOT EXISTS temp_password_expires_at TIMESTAMP,
                    ADD COLUMN IF NOT EXISTS failed_attempts INT DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS lockout_until TIMESTAMP,
                    ADD COLUMN IF NOT EXISTS mfa_otp VARCHAR(10),
                    ADD COLUMN IF NOT EXISTS mfa_otp_expires_at TIMESTAMP;

                CREATE INDEX IF NOT EXISTS idx_users_temp_password_expires_at
                    ON users (temp_password_expires_at)
                    WHERE temp_password_expires_at IS NOT NULL;";

            using var command = new NpgsqlCommand(sql, connection);
            command.ExecuteNonQuery();

            EnsureStudentProfileSchema(connection);
            EnsureTeacherProfileSchema(connection);
        }

        private static void EnsureTeacherProfileSchema(NpgsqlConnection connection)
        {
            const string sql = @"
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
                );

                INSERT INTO teacher_profiles (user_id, teacher_code)
                SELECT u.id, 'GV' || LPAD(u.id::text, 5, '0')
                FROM users u
                JOIN roles r ON r.id = u.role_id
                WHERE UPPER(r.name) = 'TEACHER'
                ON CONFLICT (user_id) DO NOTHING;";

            using var command = new NpgsqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        private static UserModel ReadUser(NpgsqlDataReader reader, int tempPasswordExpiresAtIndex, int? avatarPathIndex = null)
        {
            var user = new UserModel
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                PasswordHash = reader.GetString(2),
                FullName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Email = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                Role = reader.GetString(5),
                Status = reader.GetString(6),
                TempPasswordExpiresAt = reader.IsDBNull(tempPasswordExpiresAtIndex) ? null : reader.GetDateTime(tempPasswordExpiresAtIndex),
                AvatarPath = avatarPathIndex.HasValue && !reader.IsDBNull(avatarPathIndex.Value)
                    ? reader.GetString(avatarPathIndex.Value)
                    : string.Empty
            };

            if (reader.FieldCount > 9)
            {
                user.FailedAttempts = reader.IsDBNull(9) ? 0 : reader.GetInt32(9);
                user.LockoutUntil = reader.IsDBNull(10) ? null : reader.GetDateTime(10);
                user.MfaOtp = reader.IsDBNull(11) ? null : reader.GetString(11);
                user.MfaOtpExpiresAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12);
            }

            return user;
        }

        public void EnsureCourseWorkflowSchema()
        {
            using var connection = CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(@"
                ALTER TABLE courses
                    ADD COLUMN IF NOT EXISTS rejection_reason TEXT,
                    ADD COLUMN IF NOT EXISTS teaching_days VARCHAR(255),
                    ADD COLUMN IF NOT EXISTS session_start_time TIME,
                    ADD COLUMN IF NOT EXISTS session_end_time TIME,
                    ADD COLUMN IF NOT EXISTS frequency VARCHAR(50) DEFAULT 'Weekly',
                    ADD COLUMN IF NOT EXISTS meeting_link VARCHAR(500);

                ALTER TABLE notifications
                    ADD COLUMN IF NOT EXISTS category VARCHAR(32) NOT NULL DEFAULT 'SystemAdmin',
                    ADD COLUMN IF NOT EXISTS notification_type VARCHAR(32) NOT NULL DEFAULT 'Informational',
                    ADD COLUMN IF NOT EXISTS source_type VARCHAR(64),
                    ADD COLUMN IF NOT EXISTS source_id INT;

                -- Drop and recreate constraints to support clean user deletion
                ALTER TABLE COURSES DROP CONSTRAINT IF EXISTS courses_teacher_id_fkey;
                ALTER TABLE COURSES ADD CONSTRAINT courses_teacher_id_fkey FOREIGN KEY (teacher_id) REFERENCES USERS(id) ON DELETE CASCADE;

                ALTER TABLE ENROLLMENTS DROP CONSTRAINT IF EXISTS enrollments_student_id_fkey;
                ALTER TABLE ENROLLMENTS ADD CONSTRAINT enrollments_student_id_fkey FOREIGN KEY (student_id) REFERENCES USERS(id) ON DELETE CASCADE;

                ALTER TABLE EXAMS DROP CONSTRAINT IF EXISTS exams_created_by_fkey;
                ALTER TABLE EXAMS ADD CONSTRAINT exams_created_by_fkey FOREIGN KEY (created_by) REFERENCES USERS(id) ON DELETE SET NULL;

                ALTER TABLE EXAM_ATTEMPTS DROP CONSTRAINT IF EXISTS exam_attempts_student_id_fkey;
                ALTER TABLE EXAM_ATTEMPTS ADD CONSTRAINT exam_attempts_student_id_fkey FOREIGN KEY (student_id) REFERENCES USERS(id) ON DELETE CASCADE;

                ALTER TABLE ONLINE_SESSIONS DROP CONSTRAINT IF EXISTS online_sessions_created_by_fkey;
                ALTER TABLE ONLINE_SESSIONS ADD CONSTRAINT online_sessions_created_by_fkey FOREIGN KEY (created_by) REFERENCES USERS(id) ON DELETE SET NULL;

                ALTER TABLE MATERIALS DROP CONSTRAINT IF EXISTS materials_uploaded_by_fkey;
                ALTER TABLE MATERIALS ADD CONSTRAINT materials_uploaded_by_fkey FOREIGN KEY (uploaded_by) REFERENCES USERS(id) ON DELETE CASCADE;

                ALTER TABLE MESSAGES ALTER COLUMN sender_id DROP NOT NULL;
                ALTER TABLE MESSAGES DROP CONSTRAINT IF EXISTS messages_sender_id_fkey;
                ALTER TABLE MESSAGES ADD CONSTRAINT messages_sender_id_fkey FOREIGN KEY (sender_id) REFERENCES USERS(id) ON DELETE SET NULL;

                ALTER TABLE AUDIT_LOGS DROP CONSTRAINT IF EXISTS audit_logs_user_id_fkey;
                ALTER TABLE AUDIT_LOGS ADD CONSTRAINT audit_logs_user_id_fkey FOREIGN KEY (user_id) REFERENCES USERS(id) ON DELETE SET NULL;

                ALTER TABLE VIOLATIONS DROP CONSTRAINT IF EXISTS violations_user_id_fkey;
                ALTER TABLE VIOLATIONS ADD CONSTRAINT violations_user_id_fkey FOREIGN KEY (user_id) REFERENCES USERS(id) ON DELETE CASCADE;", connection);
            command.ExecuteNonQuery();
        }

        // Updated direct query method to include all fields
        public UserModel? GetUserByUsername(string username)
        {
            using var connection = CreateConnection();
            connection.Open();
            EnsureUserSecuritySchema(connection);

            string query = @"SELECT u.id, u.username, u.password_hash, u.full_name, u.email, r.name as role, u.status,
                                    u.temp_password_expires_at,
                                    COALESCE(sp.avatar_path, tp.avatar_path, '') AS avatar_path,
                                    u.failed_attempts, u.lockout_until, u.mfa_otp, u.mfa_otp_expires_at
                             FROM USERS u
                             JOIN ROLES r ON u.role_id = r.id
                             LEFT JOIN student_profiles sp ON sp.user_id = u.id
                             LEFT JOIN teacher_profiles tp ON tp.user_id = u.id
                             WHERE LOWER(u.username) = LOWER(@username)";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@username", username);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return ReadUser(reader, tempPasswordExpiresAtIndex: 7, avatarPathIndex: 8);
            }

            return null;
        }

        public async Task<UserModel?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            EnsureUserSecuritySchema(connection);

            const string query = @"SELECT u.id, u.username, u.password_hash, u.full_name, u.email, r.name as role, u.status,
                                    u.temp_password_expires_at,
                                    COALESCE(sp.avatar_path, tp.avatar_path, '') AS avatar_path,
                                    u.failed_attempts, u.lockout_until, u.mfa_otp, u.mfa_otp_expires_at
                             FROM USERS u
                             JOIN ROLES r ON u.role_id = r.id
                             LEFT JOIN student_profiles sp ON sp.user_id = u.id
                             LEFT JOIN teacher_profiles tp ON tp.user_id = u.id
                             WHERE LOWER(u.username) = LOWER(@username)";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@username", username);

            using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return ReadUser(reader, tempPasswordExpiresAtIndex: 7, avatarPathIndex: 8);
            }

            return null;
        }

        public void InsertUser(UserModel user, string passwordHash)
        {
            using var connection = CreateConnection();
            connection.Open();

            // Resolve role_id first
            string getRoleIdQuery = "SELECT id FROM ROLES WHERE name = @roleName";
            int roleId = 3; // Default to STUDENT
            using (var roleCmd = new NpgsqlCommand(getRoleIdQuery, connection))
            {
                roleCmd.Parameters.AddWithValue("@roleName", (user.Role ?? "STUDENT").ToUpper());
                var result = roleCmd.ExecuteScalar();
                if (result != null) roleId = Convert.ToInt32(result);
            }

            string query = @"INSERT INTO USERS (username, password_hash, full_name, email, role_id, status) 
                            VALUES (@username, @password_hash, @full_name, @email, @role_id, @status)";
            
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@username", user.Username);
            command.Parameters.AddWithValue("@password_hash", passwordHash);
            command.Parameters.AddWithValue("@full_name", user.FullName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@email", user.Email ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@role_id", roleId);
            command.Parameters.AddWithValue("@status", user.Status ?? "PENDING");

            command.ExecuteNonQuery();
        }

        public void UpdateUserStatus(int userId, string status)
        {
            using var connection = CreateConnection();
            connection.Open();

            string query = "UPDATE Users SET status = @status WHERE id = @id";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@status", status);
            command.Parameters.AddWithValue("@id", userId);

            command.ExecuteNonQuery();
        }

        public void UpdateUserPassword(int userId, string passwordHash, DateTime? tempPasswordExpiresAt = null)
        {
            using var connection = CreateConnection();
            connection.Open();
            EnsureUserSecuritySchema(connection);

            const string query = @"
                UPDATE Users
                SET password_hash = @password_hash,
                    temp_password_expires_at = @temp_password_expires_at
                WHERE id = @id";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@password_hash", passwordHash);
            command.Parameters.AddWithValue("@temp_password_expires_at", (object?)tempPasswordExpiresAt ?? DBNull.Value);
            command.Parameters.AddWithValue("@id", userId);

            command.ExecuteNonQuery();
        }

        public void IncrementFailedAttempts(int userId)
        {
            using var connection = CreateConnection();
            connection.Open();
            EnsureUserSecuritySchema(connection);

            // Get current failed attempts
            int currentAttempts = 0;
            const string selectSql = "SELECT failed_attempts FROM Users WHERE id = @id";
            using (var selectCmd = new NpgsqlCommand(selectSql, connection))
            {
                selectCmd.Parameters.AddWithValue("@id", userId);
                var result = selectCmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    currentAttempts = Convert.ToInt32(result);
                }
            }

            currentAttempts++;
            
            int limit = 5;
            string? limitStr = Environment.GetEnvironmentVariable("BRUTE_FORCE_LIMIT");
            if (int.TryParse(limitStr, out int l)) limit = l;

            int lockoutMinutes = 15;
            string? lockoutStr = Environment.GetEnvironmentVariable("BRUTE_FORCE_LOCKOUT_MINUTES");
            if (int.TryParse(lockoutStr, out int lm)) lockoutMinutes = lm;

            DateTime? lockoutUntil = null;
            if (currentAttempts >= limit)
            {
                lockoutUntil = DateTime.Now.AddMinutes(lockoutMinutes);
            }

            const string updateSql = @"
                UPDATE Users
                SET failed_attempts = @failed_attempts,
                    lockout_until = @lockout_until
                WHERE id = @id";

            using var command = new NpgsqlCommand(updateSql, connection);
            command.Parameters.AddWithValue("@failed_attempts", currentAttempts);
            command.Parameters.AddWithValue("@lockout_until", (object?)lockoutUntil ?? DBNull.Value);
            command.Parameters.AddWithValue("@id", userId);
            command.ExecuteNonQuery();
        }

        public void ResetFailedAttempts(int userId)
        {
            using var connection = CreateConnection();
            connection.Open();
            EnsureUserSecuritySchema(connection);

            const string sql = @"
                UPDATE Users
                SET failed_attempts = 0,
                    lockout_until = NULL
                WHERE id = @id";

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", userId);
            command.ExecuteNonQuery();
        }

        public void SaveMfaOtp(int userId, string otp, DateTime expiresAt)
        {
            using var connection = CreateConnection();
            connection.Open();
            EnsureUserSecuritySchema(connection);

            const string sql = @"
                UPDATE Users
                SET mfa_otp = @mfa_otp,
                    mfa_otp_expires_at = @mfa_otp_expires_at
                WHERE id = @id";

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@mfa_otp", otp);
            command.Parameters.AddWithValue("@mfa_otp_expires_at", expiresAt);
            command.Parameters.AddWithValue("@id", userId);
            command.ExecuteNonQuery();
        }

        public bool VerifyMfaOtp(int userId, string otp)
        {
            if (string.IsNullOrWhiteSpace(otp)) return false;

            using var connection = CreateConnection();
            connection.Open();
            EnsureUserSecuritySchema(connection);

            const string sql = @"
                SELECT mfa_otp, mfa_otp_expires_at
                FROM Users
                WHERE id = @id";

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", userId);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                string? storedOtp = reader.IsDBNull(0) ? null : reader.GetString(0);
                DateTime? expiresAt = reader.IsDBNull(1) ? null : reader.GetDateTime(1);

                if (storedOtp != null && expiresAt.HasValue && 
                    string.Equals(storedOtp.Trim(), otp.Trim(), StringComparison.Ordinal) &&
                    expiresAt.Value > DateTime.Now)
                {
                    reader.Close();

                    // Clear OTP after successful verification to prevent reuse
                    const string clearSql = @"
                        UPDATE Users
                        SET mfa_otp = NULL,
                            mfa_otp_expires_at = NULL
                        WHERE id = @id";
                    using var clearCommand = new NpgsqlCommand(clearSql, connection);
                    clearCommand.Parameters.AddWithValue("@id", userId);
                    clearCommand.ExecuteNonQuery();

                    return true;
                }
            }

            return false;
        }

        private static void EnsureStudentProfileSchema(NpgsqlConnection connection)
        {
            const string sql = @"
                CREATE TABLE IF NOT EXISTS student_profiles (
                    user_id INTEGER PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
                    phone TEXT,
                    address TEXT,
                    major TEXT,
                    gender TEXT,
                    birth_date DATE,
                    bio TEXT,
                    avatar_path TEXT,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );";

            using var command = new NpgsqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        public StudentProfileModel? GetStudentProfile(int userId)
        {
            using var connection = CreateConnection();
            connection.Open();
            EnsureStudentProfileSchema(connection);

            const string query = @"
                SELECT u.id,
                       COALESCE(u.full_name, ''),
                       COALESCE(u.email, ''),
                       COALESCE(sp.phone, ''),
                       COALESCE(sp.address, ''),
                       COALESCE(sp.major, ''),
                       COALESCE(sp.gender, ''),
                       sp.birth_date,
                       COALESCE(sp.bio, ''),
                       COALESCE(sp.avatar_path, '')
                FROM users u
                LEFT JOIN student_profiles sp ON sp.user_id = u.id
                WHERE u.id = @user_id";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@user_id", userId);

            using var reader = command.ExecuteReader(CommandBehavior.SingleRow);
            if (!reader.Read())
                return null;

            return new StudentProfileModel
            {
                UserId = reader.GetInt32(0),
                FullName = reader.GetString(1),
                Email = reader.GetString(2),
                Phone = reader.GetString(3),
                Address = reader.GetString(4),
                Major = reader.GetString(5),
                Gender = reader.GetString(6),
                BirthDate = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                Bio = reader.GetString(8),
                AvatarPath = reader.GetString(9)
            };
        }

        public bool UpsertStudentProfile(StudentProfileModel profile)
        {
            using var connection = CreateConnection();
            connection.Open();
            EnsureStudentProfileSchema(connection);

            using var transaction = connection.BeginTransaction();
            try
            {
                const string updateUserSql = @"
                    UPDATE users
                    SET full_name = @full_name,
                        email = @email
                    WHERE id = @user_id";

                using (var updateUser = new NpgsqlCommand(updateUserSql, connection, transaction))
                {
                    updateUser.Parameters.AddWithValue("@user_id", profile.UserId);
                    updateUser.Parameters.AddWithValue("@full_name", string.IsNullOrWhiteSpace(profile.FullName) ? DBNull.Value : profile.FullName);
                    updateUser.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(profile.Email) ? DBNull.Value : profile.Email);
                    updateUser.ExecuteNonQuery();
                }

                const string upsertProfileSql = @"
                    INSERT INTO student_profiles (user_id, phone, address, major, gender, birth_date, bio, avatar_path, updated_at)
                    VALUES (@user_id, @phone, @address, @major, @gender, @birth_date, @bio, @avatar_path, CURRENT_TIMESTAMP)
                    ON CONFLICT (user_id)
                    DO UPDATE SET phone = EXCLUDED.phone,
                                  address = EXCLUDED.address,
                                  major = EXCLUDED.major,
                                  gender = EXCLUDED.gender,
                                  birth_date = EXCLUDED.birth_date,
                                  bio = EXCLUDED.bio,
                                  avatar_path = EXCLUDED.avatar_path,
                                  updated_at = CURRENT_TIMESTAMP";

                using (var upsertProfile = new NpgsqlCommand(upsertProfileSql, connection, transaction))
                {
                    upsertProfile.Parameters.AddWithValue("@user_id", profile.UserId);
                    upsertProfile.Parameters.AddWithValue("@phone", string.IsNullOrWhiteSpace(profile.Phone) ? DBNull.Value : profile.Phone);
                    upsertProfile.Parameters.AddWithValue("@address", string.IsNullOrWhiteSpace(profile.Address) ? DBNull.Value : profile.Address);
                    upsertProfile.Parameters.AddWithValue("@major", string.IsNullOrWhiteSpace(profile.Major) ? DBNull.Value : profile.Major);
                    upsertProfile.Parameters.AddWithValue("@gender", string.IsNullOrWhiteSpace(profile.Gender) ? DBNull.Value : profile.Gender);
                    upsertProfile.Parameters.AddWithValue("@birth_date", profile.BirthDate.HasValue ? profile.BirthDate.Value.Date : DBNull.Value);
                    upsertProfile.Parameters.AddWithValue("@bio", string.IsNullOrWhiteSpace(profile.Bio) ? DBNull.Value : profile.Bio);
                    upsertProfile.Parameters.AddWithValue("@avatar_path", string.IsNullOrWhiteSpace(profile.AvatarPath) ? DBNull.Value : profile.AvatarPath);
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

        private static string GetStudentCodeFromUserId(int userId)
        {
            return $"HS{userId:00000}";
        }

        private static void EnsureStudentScoreSchema(NpgsqlConnection connection)
        {
            const string createTable = @"
                CREATE TABLE IF NOT EXISTS public.student_scores (
                    id      BIGSERIAL PRIMARY KEY,
                    mssv    TEXT   NOT NULL UNIQUE,
                    ho_ten  TEXT   NOT NULL DEFAULT '',
                    lop     TEXT   NOT NULL DEFAULT '',
                    diem_gk FLOAT8 NOT NULL DEFAULT 0,
                    diem_ck FLOAT8 NOT NULL DEFAULT 0
                );";

            using var command = new NpgsqlCommand(createTable, connection);
            command.ExecuteNonQuery();
        }

        private static bool TableExists(NpgsqlConnection connection, string tableName)
        {
            const string query = @"
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND LOWER(table_name) = LOWER(@table_name)
                )";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@table_name", tableName);
            object? result = command.ExecuteScalar();
            return result is bool exists && exists;
        }

        private static void EnsureQuestionBankMetadataSchema(NpgsqlConnection connection)
        {
            if (TableExists(connection, "exams"))
            {
                using var examCommand = new NpgsqlCommand(@"
                    ALTER TABLE exams
                        ADD COLUMN IF NOT EXISTS max_violations INT NOT NULL DEFAULT 0;", connection);
                examCommand.ExecuteNonQuery();
            }

            if (TableExists(connection, "exam_questions"))
            {
                const string snapshotSql = @"
                    ALTER TABLE exam_questions
                        ADD COLUMN IF NOT EXISTS difficulty VARCHAR(10) NOT NULL DEFAULT 'MEDIUM',
                        ADD COLUMN IF NOT EXISTS chapter VARCHAR(100),
                        ADD COLUMN IF NOT EXISTS question_type VARCHAR(20) NOT NULL DEFAULT 'MULTIPLE_CHOICE';

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

                    CREATE INDEX IF NOT EXISTS idx_exam_questions_difficulty ON exam_questions(difficulty);
                    CREATE INDEX IF NOT EXISTS idx_exam_questions_chapter ON exam_questions(chapter) WHERE chapter IS NOT NULL;";

                using var snapshotCommand = new NpgsqlCommand(snapshotSql, connection);
                snapshotCommand.ExecuteNonQuery();
            }

            if (TableExists(connection, "questions"))
            {
                const string questionSql = @"
                    ALTER TABLE questions
                        ADD COLUMN IF NOT EXISTS difficulty VARCHAR(10) NOT NULL DEFAULT 'MEDIUM',
                        ADD COLUMN IF NOT EXISTS chapter VARCHAR(100),
                        ADD COLUMN IF NOT EXISTS question_type VARCHAR(20) NOT NULL DEFAULT 'MULTIPLE_CHOICE';

                    DO $$
                    BEGIN
                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_questions_difficulty') THEN
                            ALTER TABLE questions
                                ADD CONSTRAINT chk_questions_difficulty
                                CHECK (difficulty IN ('EASY', 'MEDIUM', 'HARD'));
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_questions_question_type') THEN
                            ALTER TABLE questions
                                ADD CONSTRAINT chk_questions_question_type
                                CHECK (question_type IN ('MULTIPLE_CHOICE', 'TRUE_FALSE', 'FILL_BLANK'));
                        END IF;
                    END $$;

                    CREATE INDEX IF NOT EXISTS idx_questions_course_difficulty ON questions(course_id, difficulty);
                    CREATE INDEX IF NOT EXISTS idx_questions_course_chapter ON questions(course_id, chapter) WHERE chapter IS NOT NULL;";

                using var questionCommand = new NpgsqlCommand(questionSql, connection);
                questionCommand.ExecuteNonQuery();
            }

            if (TableExists(connection, "violations"))
            {
                const string violationSql = @"
                    ALTER TABLE violations
                        ADD COLUMN IF NOT EXISTS severity VARCHAR(10) NOT NULL DEFAULT 'MEDIUM',
                        ADD COLUMN IF NOT EXISTS action_taken VARCHAR(50),
                        ADD COLUMN IF NOT EXISTS image_url TEXT;

                    DO $$
                    BEGIN
                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_violations_severity') THEN
                            ALTER TABLE violations
                                ADD CONSTRAINT chk_violations_severity
                                CHECK (severity IN ('LOW', 'MEDIUM', 'HIGH'));
                        END IF;
                    END $$;";

                using var violationCommand = new NpgsqlCommand(violationSql, connection);
                violationCommand.ExecuteNonQuery();
            }
        }

        public int CountActiveEnrollments(int studentId)
        {
            using var connection = CreateConnection();
            connection.Open();

            const string query = @"
                SELECT COUNT(*)
                FROM enrollments
                WHERE student_id = @student_id
                  AND UPPER(status) IN ('ACTIVE', 'APPROVED')";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@student_id", studentId);
            return Convert.ToInt32(command.ExecuteScalar());
        }

        public int CountUnreadNotifications(int userId)
        {
            using var connection = CreateConnection();
            connection.Open();

            const string query = @"
                SELECT COUNT(*)
                FROM notifications
                WHERE user_id = @user_id
                  AND is_read = false";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@user_id", userId);
            return Convert.ToInt32(command.ExecuteScalar());
        }

        public int CountStudentScoreRecords(int userId)
        {
            using var connection = CreateConnection();
            connection.Open();
            EnsureStudentScoreSchema(connection);

            const string query = @"
                SELECT COUNT(*)
                FROM public.student_scores
                WHERE mssv = @mssv";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@mssv", GetStudentCodeFromUserId(userId));
            return Convert.ToInt32(command.ExecuteScalar());
        }

        public double? GetStudentAverageScore(int userId)
        {
            using var connection = CreateConnection();
            connection.Open();
            EnsureStudentScoreSchema(connection);

            const string query = @"
                SELECT AVG((diem_gk * 0.3) + (diem_ck * 0.7))
                FROM public.student_scores
                WHERE mssv = @mssv";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@mssv", GetStudentCodeFromUserId(userId));
            object? result = command.ExecuteScalar();
            return result == null || result == DBNull.Value ? null : Convert.ToDouble(result);
        }

        public double? GetStudentExamAverageScore(int studentId)
        {
            using var connection = CreateConnection();
            connection.Open();

            if (TableExists(connection, "exam_attempts"))
            {
                const string attemptAverageQuery = @"
                    SELECT AVG(score)
                    FROM exam_attempts
                    WHERE student_id = @student_id
                      AND score IS NOT NULL";

                using var attemptCommand = new NpgsqlCommand(attemptAverageQuery, connection);
                attemptCommand.Parameters.AddWithValue("@student_id", studentId);
                object? attemptAverage = attemptCommand.ExecuteScalar();
                if (attemptAverage != null && attemptAverage != DBNull.Value)
                    return Convert.ToDouble(attemptAverage);
            }

            EnsureStudentScoreSchema(connection);
            const string scoreAverageQuery = @"
                SELECT AVG((diem_gk * 0.3) + (diem_ck * 0.7))
                FROM public.student_scores
                WHERE mssv = @mssv";

            using var scoreCommand = new NpgsqlCommand(scoreAverageQuery, connection);
            scoreCommand.Parameters.AddWithValue("@mssv", GetStudentCodeFromUserId(studentId));
            object? scoreAverage = scoreCommand.ExecuteScalar();
            return scoreAverage == null || scoreAverage == DBNull.Value ? null : Convert.ToDouble(scoreAverage);
        }

        public int CountOpenExamsForStudent(int studentId)
        {
            return CountStudentExamsByAvailability(studentId, openNowOnly: true);
        }

        public int CountAvailableExamsForStudent(int studentId)
        {
            return CountStudentExamsByAvailability(studentId, openNowOnly: false);
        }

        public int CountCompletedExamsForStudent(int studentId)
        {
            using var connection = CreateConnection();
            connection.Open();

            if (TableExists(connection, "exam_attempts"))
            {
                const string attemptQuery = @"
                    SELECT COUNT(*)
                    FROM exam_attempts
                    WHERE student_id = @student_id
                      AND score IS NOT NULL";

                using var attemptCommand = new NpgsqlCommand(attemptQuery, connection);
                attemptCommand.Parameters.AddWithValue("@student_id", studentId);
                int attemptCount = Convert.ToInt32(attemptCommand.ExecuteScalar());
                if (attemptCount > 0)
                    return attemptCount;
            }

            EnsureStudentScoreSchema(connection);
            const string scoreQuery = @"
                SELECT COUNT(*)
                FROM public.student_scores
                WHERE mssv = @mssv";

            using var scoreCommand = new NpgsqlCommand(scoreQuery, connection);
            scoreCommand.Parameters.AddWithValue("@mssv", GetStudentCodeFromUserId(studentId));
            return Convert.ToInt32(scoreCommand.ExecuteScalar());
        }

        public List<StudentExamListItemModel> GetAvailableExamsForStudent(int studentId, int limit = 100)
        {
            var result = new List<StudentExamListItemModel>();
            int safeLimit = limit <= 0 ? 100 : limit;

            using var connection = CreateConnection();
            connection.Open();

            if (!TableExists(connection, "exams") || !TableExists(connection, "enrollments"))
                return result;

            bool hasAttempts = TableExists(connection, "exam_attempts");
            bool hasQuestions = TableExists(connection, "exam_questions");

            string attemptsExpression = hasAttempts
                ? @"(
                    SELECT COUNT(*)
                    FROM exam_attempts ea
                    WHERE ea.exam_id = ex.id
                      AND ea.student_id = @student_id
                )"
                : "0";

            string inProgressAttemptsExpression = hasAttempts
                ? @"(
                    SELECT COUNT(*)
                    FROM exam_attempts ea
                    WHERE ea.exam_id = ex.id
                      AND ea.student_id = @student_id
                      AND UPPER(COALESCE(ea.status, '')) = 'IN_PROGRESS'
                )"
                : "0";

            string questionExpression = hasQuestions
                ? @"(
                    SELECT COUNT(*)
                    FROM exam_questions eq
                    WHERE eq.exam_id = ex.id
                )"
                : "0";

            string query = $@"
                SELECT ex.id,
                       ex.course_id,
                       COALESCE(ex.title, '') AS title,
                       COALESCE(c.name, '') AS course_name,
                       ex.open_time,
                       ex.close_time,
                       COALESCE(ex.duration_minutes, 0) AS duration_minutes,
                       COALESCE(ex.max_attempts, 1) AS max_attempts,
                       {attemptsExpression}::int AS attempt_count,
                       {inProgressAttemptsExpression}::int AS in_progress_attempt_count,
                       {questionExpression}::int AS question_count
                FROM exams ex
                JOIN enrollments en ON en.course_id = ex.course_id
                LEFT JOIN courses c ON c.id = ex.course_id
                WHERE en.student_id = @student_id
                  AND UPPER(COALESCE(en.status, 'ACTIVE')) IN ('ACTIVE', 'APPROVED')
                  AND UPPER(COALESCE(c.status, 'ACTIVE')) IN ('ACTIVE', 'APPROVED', 'OPEN')
                  AND UPPER(COALESCE(ex.status, 'DRAFT')) = 'ACTIVE'
                ORDER BY
                  CASE
                    WHEN (ex.open_time IS NULL OR ex.open_time <= @now)
                     AND (ex.close_time IS NULL OR ex.close_time >= @now) THEN 0
                    WHEN ex.open_time > @now THEN 1
                    ELSE 2
                  END,
                  ex.open_time NULLS FIRST,
                  ex.close_time NULLS LAST
                LIMIT @limit";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@student_id", studentId);
            command.Parameters.AddWithValue("@limit", safeLimit);
            command.Parameters.AddWithValue("@now", DateTime.Now);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new StudentExamListItemModel
                {
                    Id = reader.GetInt32(0),
                    CourseId = reader.GetInt32(1),
                    Title = reader.GetString(2),
                    CourseName = reader.GetString(3),
                    OpenTime = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                    CloseTime = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    DurationMinutes = reader.GetInt32(6),
                    MaxAttempts = reader.GetInt32(7),
                    AttemptCount = reader.GetInt32(8),
                    InProgressAttemptCount = reader.GetInt32(9),
                    QuestionCount = reader.GetInt32(10),
                    AttemptStorageAvailable = hasAttempts
                });
            }

            return result;
        }

        public StudentExamTakingModel? StartOrResumeStudentExam(int studentId, int examId)
        {
            using var connection = CreateConnection();
            connection.Open();
            EnsureStudentExamTakingSchema(connection);
            EnsureQuestionBankMetadataSchema(connection);
            if (!TableExists(connection, "exam_attempts") || !TableExists(connection, "exam_questions"))
                return null;

            using var transaction = connection.BeginTransaction();
            int attemptId = GetExistingInProgressAttempt(connection, transaction, studentId, examId);
            if (attemptId <= 0)
            {
                if (!CanStartStudentExam(connection, transaction, studentId, examId))
                    return null;

                using var insert = new NpgsqlCommand(@"
                    INSERT INTO exam_attempts (exam_id, student_id, start_time, status)
                    VALUES (@exam_id, @student_id, @now, 'IN_PROGRESS')
                    RETURNING id", connection, transaction);
                insert.Parameters.AddWithValue("@exam_id", examId);
                insert.Parameters.AddWithValue("@student_id", studentId);
                insert.Parameters.AddWithValue("@now", DateTime.Now);
                attemptId = Convert.ToInt32(insert.ExecuteScalar());
            }

            StudentExamTakingModel? session = LoadStudentExamSession(connection, transaction, studentId, attemptId);
            transaction.Commit();
            return session;
        }

        public bool SaveStudentExamAnswer(int studentId, int attemptId, int examQuestionId, string selectedOption)
        {
            string option = NormalizeOption(selectedOption);
            if (string.IsNullOrEmpty(option))
                return false;

            using var connection = CreateConnection();
            connection.Open();
            EnsureStudentExamTakingSchema(connection);

            using var command = new NpgsqlCommand(@"
                INSERT INTO exam_attempt_answers (attempt_id, exam_question_id, selected_option, answered_at)
                SELECT @attempt_id, @exam_question_id, @selected_option, @now
                WHERE EXISTS (
                    SELECT 1
                    FROM exam_attempts a
                    JOIN exam_questions eq ON eq.exam_id = a.exam_id
                    WHERE a.id = @attempt_id
                      AND a.student_id = @student_id
                      AND UPPER(COALESCE(a.status, '')) = 'IN_PROGRESS'
                      AND eq.id = @exam_question_id
                )
                ON CONFLICT (attempt_id, exam_question_id)
                DO UPDATE SET selected_option = EXCLUDED.selected_option, answered_at = @now", connection);
            command.Parameters.AddWithValue("@student_id", studentId);
            command.Parameters.AddWithValue("@attempt_id", attemptId);
            command.Parameters.AddWithValue("@exam_question_id", examQuestionId);
            command.Parameters.AddWithValue("@selected_option", option);
            command.Parameters.AddWithValue("@now", DateTime.Now);
            return command.ExecuteNonQuery() > 0;
        }

        public StudentExamSubmitResultModel SubmitStudentExamAttempt(int studentId, int attemptId)
        {
            using var connection = CreateConnection();
            connection.Open();
            EnsureStudentExamTakingSchema(connection);
            using var transaction = connection.BeginTransaction();

            if (!OwnsInProgressAttempt(connection, transaction, studentId, attemptId))
            {
                return new StudentExamSubmitResultModel
                {
                    Success = false,
                    Message = "Không tìm thấy lượt làm bài đang mở."
                };
            }

            var questions = new List<TeacherExamQuestionModel>();
            using (var questionCommand = new NpgsqlCommand(@"
                SELECT eq.id, COALESCE(eq.correct_option, 'A'), COALESCE(eq.points, 0)
                FROM exam_questions eq
                JOIN exam_attempts a ON a.exam_id = eq.exam_id
                WHERE a.id = @attempt_id
                ORDER BY COALESCE(eq.display_order, 1), eq.id", connection, transaction))
            {
                questionCommand.Parameters.AddWithValue("@attempt_id", attemptId);
                using var reader = questionCommand.ExecuteReader();
                while (reader.Read())
                {
                    questions.Add(new TeacherExamQuestionModel
                    {
                        Id = reader.GetInt32(0),
                        CorrectOption = reader.GetString(1),
                        Points = reader.GetDecimal(2)
                    });
                }
            }

            var selected = new Dictionary<int, string>();
            using (var answerCommand = new NpgsqlCommand(@"
                SELECT exam_question_id, COALESCE(selected_option, '')
                FROM exam_attempt_answers
                WHERE attempt_id = @attempt_id", connection, transaction))
            {
                answerCommand.Parameters.AddWithValue("@attempt_id", attemptId);
                using var reader = answerCommand.ExecuteReader();
                while (reader.Read())
                    selected[reader.GetInt32(0)] = reader.GetString(1);
            }

            decimal score = ExamScoringService.CalculateScore(questions, selected);

            using (var answerScoreCommand = new NpgsqlCommand(@"
                UPDATE exam_attempt_answers aa
                SET is_correct = UPPER(COALESCE(aa.selected_option, '')) = UPPER(COALESCE(eq.correct_option, '')),
                    score = CASE
                        WHEN UPPER(COALESCE(aa.selected_option, '')) = UPPER(COALESCE(eq.correct_option, '')) THEN COALESCE(eq.points, 0)
                        ELSE 0
                    END
                FROM exam_questions eq
                WHERE aa.exam_question_id = eq.id
                  AND aa.attempt_id = @attempt_id", connection, transaction))
            {
                answerScoreCommand.Parameters.AddWithValue("@attempt_id", attemptId);
                answerScoreCommand.ExecuteNonQuery();
            }

            using (var updateAttempt = new NpgsqlCommand(@"
                UPDATE exam_attempts
                SET score = @score, submit_time = @now, status = 'SUBMITTED'
                WHERE id = @attempt_id AND student_id = @student_id", connection, transaction))
            {
                updateAttempt.Parameters.AddWithValue("@attempt_id", attemptId);
                updateAttempt.Parameters.AddWithValue("@student_id", studentId);
                updateAttempt.Parameters.AddWithValue("@score", score);
                updateAttempt.Parameters.AddWithValue("@now", DateTime.Now);
                updateAttempt.ExecuteNonQuery();
            }

            transaction.Commit();
            return new StudentExamSubmitResultModel
            {
                Success = true,
                Score = score,
                Message = "Đã nộp bài."
            };
        }

        public List<StudentResultCourseFilterModel> GetActiveResultCourseFiltersForStudent(int studentId)
        {
            var result = new List<StudentResultCourseFilterModel>();
            using var connection = CreateConnection();
            connection.Open();
            if (!TableExists(connection, "courses") || !TableExists(connection, "enrollments"))
                return result;

            using var command = new NpgsqlCommand(@"
                SELECT DISTINCT c.id, COALESCE(c.name, '')
                FROM courses c
                JOIN enrollments e ON e.course_id = c.id
                WHERE e.student_id = @student_id
                  AND UPPER(COALESCE(e.status, '')) IN ('ACTIVE', 'APPROVED')
                ORDER BY COALESCE(c.name, '')", connection);
            command.Parameters.AddWithValue("@student_id", studentId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new StudentResultCourseFilterModel
                {
                    CourseId = reader.GetInt32(0),
                    CourseName = reader.GetString(1)
                });
            }
            return result;
        }

        public List<StudentResultListItemModel> GetStudentResultItems(int studentId, int? courseId = null, string? examTitleKeyword = null, int limit = 100)
        {
            var result = new List<StudentResultListItemModel>();
            int safeLimit = limit <= 0 ? 100 : limit;
            bool hasFilter = (courseId.HasValue && courseId.Value > 0) || !string.IsNullOrWhiteSpace(examTitleKeyword);

            using var connection = CreateConnection();
            connection.Open();

            if (TableExists(connection, "exam_attempts") && TableExists(connection, "exams") && TableExists(connection, "enrollments"))
            {
                bool hasQuestions = TableExists(connection, "exam_questions");
                bool hasHiddenResults = TableExists(connection, "student_hidden_results");
                bool hasAttemptAnswers = TableExists(connection, "exam_attempt_answers");
                string questionExpression = hasQuestions
                    ? @"(
                        SELECT COUNT(*)
                        FROM exam_questions eq
                        WHERE eq.exam_id = ex.id
                    )"
                    : "0";
                string correctExpression = hasAttemptAnswers
                    ? @"(
                        SELECT COUNT(*)
                        FROM exam_attempt_answers aa
                        WHERE aa.attempt_id = a.id
                          AND aa.is_correct = TRUE
                    )"
                    : "-1";
                string hiddenJoin = hasHiddenResults
                    ? "LEFT JOIN student_hidden_results shr ON shr.attempt_id = a.id AND shr.student_id = a.student_id"
                    : string.Empty;
                string hiddenFilter = hasHiddenResults
                    ? "AND shr.attempt_id IS NULL"
                    : string.Empty;

                string query = $@"
                    SELECT a.id AS attempt_id,
                           ex.id AS exam_id,
                           ex.course_id,
                           COALESCE(ex.title, '') AS exam_title,
                           COALESCE(c.name, '') AS course_name,
                           COALESCE(a.score, 0)::float8 AS score,
                           COALESCE(a.status, '') AS attempt_status,
                           {questionExpression}::int AS question_count,
                           {correctExpression}::int AS correct_count,
                           COALESCE(ex.status, '') AS exam_status
                    FROM exam_attempts a
                    JOIN exams ex ON ex.id = a.exam_id
                    JOIN enrollments en ON en.course_id = ex.course_id AND en.student_id = a.student_id
                    LEFT JOIN courses c ON c.id = ex.course_id
                    {hiddenJoin}
                    WHERE a.student_id = @student_id
                      AND a.score IS NOT NULL
                      AND UPPER(COALESCE(en.status, '')) IN ('ACTIVE', 'APPROVED')
                      {hiddenFilter}";
                if (courseId.HasValue && courseId.Value > 0)
                    query += " AND ex.course_id = @course_id";
                if (!string.IsNullOrWhiteSpace(examTitleKeyword))
                    query += " AND COALESCE(ex.title, '') ILIKE @keyword";
                query += " ORDER BY COALESCE(a.submit_time, a.start_time) DESC LIMIT @limit";

                using var command = new NpgsqlCommand(query, connection);
                command.Parameters.AddWithValue("@student_id", studentId);
                command.Parameters.AddWithValue("@limit", safeLimit);
                if (courseId.HasValue && courseId.Value > 0)
                    command.Parameters.AddWithValue("@course_id", courseId.Value);
                if (!string.IsNullOrWhiteSpace(examTitleKeyword))
                    command.Parameters.AddWithValue("@keyword", $"%{examTitleKeyword.Trim()}%");
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    double score = reader.GetDouble(5);
                    int questionCount = reader.GetInt32(7);
                    int correctCount = reader.GetInt32(8);
                    result.Add(new StudentResultListItemModel
                    {
                        AttemptId = reader.GetInt32(0),
                        ExamId = reader.GetInt32(1),
                        CourseId = reader.GetInt32(2),
                        ExamTitle = reader.GetString(3),
                        CourseName = reader.GetString(4),
                        CorrectAnswersText = questionCount > 0 && correctCount >= 0 ? $"{correctCount}/{questionCount}" : questionCount > 0 ? $"N/A/{questionCount}" : "N/A",
                        Score = score,
                        StatusText = BuildResultStatus(score, reader.GetString(6)),
                        ExamStatus = reader.GetString(9)
                    });
                }
            }

            if (result.Count > 0 || hasFilter)
                return result;

            EnsureStudentScoreSchema(connection);
            const string fallbackQuery = @"
                SELECT lop, diem_gk, diem_ck
                FROM public.student_scores
                WHERE mssv = @mssv
                LIMIT @limit";

            using var fallbackCommand = new NpgsqlCommand(fallbackQuery, connection);
            fallbackCommand.Parameters.AddWithValue("@mssv", GetStudentCodeFromUserId(studentId));
            fallbackCommand.Parameters.AddWithValue("@limit", safeLimit);
            using var fallbackReader = fallbackCommand.ExecuteReader();
            while (fallbackReader.Read())
            {
                double midterm = fallbackReader.GetDouble(1);
                double final = fallbackReader.GetDouble(2);
                double score = Math.Round((midterm * 0.3) + (final * 0.7), 1);
                result.Add(new StudentResultListItemModel
                {
                    ExamTitle = "Bảng điểm tổng hợp",
                    CourseName = fallbackReader.IsDBNull(0) ? string.Empty : fallbackReader.GetString(0),
                    CorrectAnswersText = "N/A",
                    Score = score,
                    StatusText = score < 5.0 ? "Không đạt" : "Đạt"
                });
            }

            return result;
        }

        public bool HideStudentResult(int studentId, int attemptId)
        {
            using var connection = CreateConnection();
            connection.Open();
            EnsureStudentHiddenResultsSchema(connection);
            if (!TableExists(connection, "student_hidden_results"))
                return false;

            using var command = new NpgsqlCommand(@"
                INSERT INTO student_hidden_results (student_id, attempt_id)
                SELECT @student_id, @attempt_id
                WHERE EXISTS (
                    SELECT 1 FROM exam_attempts
                    WHERE id = @attempt_id AND student_id = @student_id
                )
                ON CONFLICT (student_id, attempt_id) DO NOTHING", connection);
            command.Parameters.AddWithValue("@student_id", studentId);
            command.Parameters.AddWithValue("@attempt_id", attemptId);
            return command.ExecuteNonQuery() > 0;
        }

        private static void EnsureStudentHiddenResultsSchema(NpgsqlConnection connection)
        {
            if (!TableExists(connection, "users") || !TableExists(connection, "exam_attempts"))
                return;

            using var command = new NpgsqlCommand(@"
                CREATE TABLE IF NOT EXISTS student_hidden_results (
                    student_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                    attempt_id INT NOT NULL REFERENCES exam_attempts(id) ON DELETE CASCADE,
                    hidden_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (student_id, attempt_id)
                );

                CREATE INDEX IF NOT EXISTS idx_student_hidden_results_student ON student_hidden_results(student_id);", connection);
            command.ExecuteNonQuery();
        }

        public StudentExamReviewModel? GetStudentExamReview(int studentId, int attemptId)
        {
            using var connection = CreateConnection();
            connection.Open();
            if (!TableExists(connection, "exam_attempts") || !TableExists(connection, "exams"))
                return null;

            using var headerCommand = new NpgsqlCommand(@"
                SELECT a.id, ex.id, COALESCE(ex.title, ''), COALESCE(c.name, ''),
                       COALESCE(a.score, 0)::float8, COALESCE(a.status, ''),
                       COALESCE(ex.status, '')
                FROM exam_attempts a
                JOIN exams ex ON ex.id = a.exam_id
                LEFT JOIN courses c ON c.id = ex.course_id
                WHERE a.student_id = @student_id
                  AND a.id = @attempt_id
                  AND UPPER(COALESCE(ex.status, '')) = 'CLOSED'", connection);
            headerCommand.Parameters.AddWithValue("@student_id", studentId);
            headerCommand.Parameters.AddWithValue("@attempt_id", attemptId);
            using var reader = headerCommand.ExecuteReader();
            if (!reader.Read())
                return null;

            double score = reader.GetDouble(4);
            var review = new StudentExamReviewModel
            {
                AttemptId = reader.GetInt32(0),
                ExamId = reader.GetInt32(1),
                ExamTitle = reader.GetString(2),
                CourseName = reader.GetString(3),
                Score = score,
                StatusText = BuildResultStatus(score, reader.GetString(5))
            };
            reader.Close();

            if (!TableExists(connection, "exam_questions"))
                return review;

            bool hasAttemptAnswers = TableExists(connection, "exam_attempt_answers");
            string answerJoin = hasAttemptAnswers
                ? "LEFT JOIN exam_attempt_answers aa ON aa.exam_question_id = eq.id AND aa.attempt_id = @attempt_id"
                : string.Empty;
            string selectedExpression = hasAttemptAnswers ? "COALESCE(aa.selected_option, '')" : "''";

            using var questionCommand = new NpgsqlCommand($@"
                SELECT COALESCE(eq.display_order, 1), COALESCE(eq.question_text, ''),
                       COALESCE(eq.option_a, ''), COALESCE(eq.option_b, ''),
                       COALESCE(eq.option_c, ''), COALESCE(eq.option_d, ''),
                       COALESCE(eq.correct_option, ''), COALESCE(eq.points, 0),
                       {selectedExpression}
                FROM exam_questions eq
                {answerJoin}
                WHERE eq.exam_id = @exam_id
                ORDER BY eq.display_order, eq.id", connection);
            questionCommand.Parameters.AddWithValue("@exam_id", review.ExamId);
            if (hasAttemptAnswers)
                questionCommand.Parameters.AddWithValue("@attempt_id", attemptId);
            using var qReader = questionCommand.ExecuteReader();
            while (qReader.Read())
            {
                review.Questions.Add(new StudentExamReviewQuestionModel
                {
                    DisplayOrder = qReader.GetInt32(0),
                    QuestionText = qReader.GetString(1),
                    OptionA = qReader.GetString(2),
                    OptionB = qReader.GetString(3),
                    OptionC = qReader.GetString(4),
                    OptionD = qReader.GetString(5),
                    CorrectOption = qReader.GetString(6),
                    Points = qReader.GetDecimal(7),
                    SelectedOption = qReader.GetString(8)
                });
            }

            return review;
        }

        public List<StudentSearchResultModel> SearchStudentGlobal(int studentId, string keyword, int limitPerGroup = 5)
        {
            var results = new List<StudentSearchResultModel>();
            string term = (keyword ?? string.Empty).Trim();
            if (studentId <= 0 || string.IsNullOrWhiteSpace(term))
                return results;

            int safeLimit = Math.Clamp(limitPerGroup, 1, 20);
            string pattern = $"%{term}%";

            using var connection = CreateConnection();
            connection.Open();

            if (TableExists(connection, "courses") && TableExists(connection, "enrollments"))
            {
                const string courseQuery = @"
                    SELECT DISTINCT COALESCE(c.name, '') AS title,
                           COALESCE(u.full_name, u.username, '') AS teacher_name,
                           CASE
                             WHEN e.id IS NOT NULL THEN 'Khóa học của tôi'
                             ELSE 'Tìm khóa học'
                           END AS page_name
                    FROM courses c
                    LEFT JOIN users u ON u.id = c.teacher_id
                    LEFT JOIN enrollments e ON e.course_id = c.id AND e.student_id = @student_id
                    WHERE UPPER(COALESCE(c.status, '')) = 'ACTIVE'
                      AND (
                           e.id IS NULL
                           OR UPPER(COALESCE(e.status, '')) IN ('ACTIVE', 'APPROVED', 'PENDING')
                      )
                      AND (
                           COALESCE(c.name, '') ILIKE @pattern
                           OR COALESCE(c.description, '') ILIKE @pattern
                           OR COALESCE(u.full_name, u.username, '') ILIKE @pattern
                      )
                    ORDER BY title
                    LIMIT @limit";

                using var command = new NpgsqlCommand(courseQuery, connection);
                command.Parameters.AddWithValue("@student_id", studentId);
                command.Parameters.AddWithValue("@pattern", pattern);
                command.Parameters.AddWithValue("@limit", safeLimit);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    results.Add(new StudentSearchResultModel
                    {
                        Group = "Khóa học",
                        Title = reader.GetString(0),
                        Description = reader.GetString(1),
                        PageName = reader.GetString(2),
                        Keyword = term
                    });
                }
            }

            if (TableExists(connection, "exams") && TableExists(connection, "enrollments"))
            {
                bool hasAttempts = TableExists(connection, "exam_attempts");
                bool hasQuestions = TableExists(connection, "exam_questions");
                string attemptsExpression = hasAttempts
                    ? @"(
                        SELECT COUNT(*)
                        FROM exam_attempts ea
                        WHERE ea.exam_id = ex.id
                          AND ea.student_id = @student_id
                    )"
                    : "0";
                string questionExpression = hasQuestions
                    ? @"(
                        SELECT COUNT(*)
                        FROM exam_questions eq
                        WHERE eq.exam_id = ex.id
                    )"
                    : "0";

                string examQuery = $@"
                    SELECT COALESCE(ex.title, '') AS title,
                           COALESCE(c.name, '') AS course_name
                    FROM exams ex
                    JOIN enrollments en ON en.course_id = ex.course_id
                    LEFT JOIN courses c ON c.id = ex.course_id
                    WHERE en.student_id = @student_id
                      AND UPPER(COALESCE(en.status, 'ACTIVE')) IN ('ACTIVE', 'APPROVED')
                      AND UPPER(COALESCE(c.status, 'ACTIVE')) IN ('ACTIVE', 'APPROVED', 'OPEN')
                      AND UPPER(COALESCE(ex.status, 'DRAFT')) = 'ACTIVE'
                      AND (ex.open_time IS NULL OR ex.open_time <= @now)
                      AND (ex.close_time IS NULL OR ex.close_time >= @now)
                      AND {questionExpression} > 0
                      AND (COALESCE(ex.max_attempts, 1) <= 0 OR {attemptsExpression} < COALESCE(ex.max_attempts, 1))
                      AND (
                           COALESCE(ex.title, '') ILIKE @pattern
                           OR COALESCE(c.name, '') ILIKE @pattern
                      )
                    ORDER BY ex.open_time NULLS FIRST, ex.close_time NULLS LAST
                    LIMIT @limit";

                using var command = new NpgsqlCommand(examQuery, connection);
                command.Parameters.AddWithValue("@student_id", studentId);
                command.Parameters.AddWithValue("@pattern", pattern);
                command.Parameters.AddWithValue("@limit", safeLimit);
                command.Parameters.AddWithValue("@now", DateTime.Now);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    results.Add(new StudentSearchResultModel
                    {
                        Group = "Bài kiểm tra",
                        Title = reader.GetString(0),
                        Description = reader.GetString(1),
                        PageName = "Bài kiểm tra",
                        Keyword = term
                    });
                }
            }

            if (TableExists(connection, "materials") && TableExists(connection, "courses") && TableExists(connection, "enrollments"))
            {
                const string materialQuery = @"
                    SELECT COALESCE(m.file_name, '') AS title,
                           COALESCE(c.name, '') AS course_name
                    FROM materials m
                    JOIN courses c ON c.id = m.course_id
                    JOIN enrollments e ON e.course_id = c.id
                    WHERE e.student_id = @student_id
                      AND UPPER(COALESCE(e.status, '')) IN ('ACTIVE', 'APPROVED')
                      AND UPPER(COALESCE(c.status, 'ACTIVE')) IN ('ACTIVE', 'APPROVED', 'OPEN')
                      AND (
                           COALESCE(m.file_name, '') ILIKE @pattern
                           OR COALESCE(m.file_path, '') ILIKE @pattern
                           OR COALESCE(c.name, '') ILIKE @pattern
                      )
                    ORDER BY m.uploaded_at DESC, m.id DESC
                    LIMIT @limit";

                using var command = new NpgsqlCommand(materialQuery, connection);
                command.Parameters.AddWithValue("@student_id", studentId);
                command.Parameters.AddWithValue("@pattern", pattern);
                command.Parameters.AddWithValue("@limit", safeLimit);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    results.Add(new StudentSearchResultModel
                    {
                        Group = "Tài liệu",
                        Title = reader.GetString(0),
                        Description = reader.GetString(1),
                        PageName = "Tài liệu",
                        Keyword = term
                    });
                }
            }

            if (TableExists(connection, "notifications"))
            {
                const string notificationQuery = @"
                    SELECT COALESCE(title, '') AS title,
                           COALESCE(content, '') AS content
                    FROM notifications
                    WHERE user_id = @student_id
                      AND (
                           COALESCE(title, '') ILIKE @pattern
                           OR COALESCE(content, '') ILIKE @pattern
                      )
                    ORDER BY created_at DESC
                    LIMIT @limit";

                using var command = new NpgsqlCommand(notificationQuery, connection);
                command.Parameters.AddWithValue("@student_id", studentId);
                command.Parameters.AddWithValue("@pattern", pattern);
                command.Parameters.AddWithValue("@limit", safeLimit);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string content = reader.GetString(1);
                    results.Add(new StudentSearchResultModel
                    {
                        Group = "Thông báo",
                        Title = reader.GetString(0),
                        Description = content.Length > 64 ? content[..64] + "..." : content,
                        PageName = "Thông báo",
                        Keyword = term
                    });
                }
            }

            return results;
        }

        private int CountStudentExamsByAvailability(int studentId, bool openNowOnly)
        {
            using var connection = CreateConnection();
            connection.Open();

            if (!TableExists(connection, "exams") || !TableExists(connection, "enrollments"))
                return 0;

            bool hasAttempts = TableExists(connection, "exam_attempts");
            bool hasQuestions = TableExists(connection, "exam_questions");
            string attemptsExpression = hasAttempts
                ? @"(
                    SELECT COUNT(*)
                    FROM exam_attempts ea
                    WHERE ea.exam_id = ex.id
                      AND ea.student_id = @student_id
                )"
                : "0";
            string questionExpression = hasQuestions
                ? @"(
                    SELECT COUNT(*)
                    FROM exam_questions eq
                    WHERE eq.exam_id = ex.id
                )"
                : "0";

            string timeFilter = openNowOnly
                ? @"AND (ex.open_time IS NULL OR ex.open_time <= @now)
                   AND (ex.close_time IS NULL OR ex.close_time >= @now)"
                : @"AND (ex.open_time IS NULL OR ex.open_time <= @now)
                   AND (ex.close_time IS NULL OR ex.close_time >= @now)";

            string query = $@"
                SELECT COUNT(*)
                FROM exams ex
                JOIN enrollments en ON en.course_id = ex.course_id
                LEFT JOIN courses c ON c.id = ex.course_id
                WHERE en.student_id = @student_id
                  AND UPPER(COALESCE(en.status, 'ACTIVE')) IN ('ACTIVE', 'APPROVED')
                  AND UPPER(COALESCE(c.status, 'ACTIVE')) IN ('ACTIVE', 'APPROVED', 'OPEN')
                  AND UPPER(COALESCE(ex.status, 'DRAFT')) = 'ACTIVE'
                  {timeFilter}
                  AND {questionExpression} > 0
                  AND (COALESCE(ex.max_attempts, 1) <= 0 OR {attemptsExpression} < COALESCE(ex.max_attempts, 1))";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@student_id", studentId);
            command.Parameters.AddWithValue("@now", DateTime.Now);
            object? result = command.ExecuteScalar();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        private static void EnsureStudentExamTakingSchema(NpgsqlConnection connection)
        {
            if (!TableExists(connection, "exam_attempts") || !TableExists(connection, "exam_questions"))
                return;

            using var command = new NpgsqlCommand(@"
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
                CREATE INDEX IF NOT EXISTS idx_exam_attempt_answers_question ON exam_attempt_answers(exam_question_id);", connection);
            command.ExecuteNonQuery();
        }

        private static int GetExistingInProgressAttempt(NpgsqlConnection connection, NpgsqlTransaction transaction, int studentId, int examId)
        {
            using var command = new NpgsqlCommand(@"
                SELECT id
                FROM exam_attempts
                WHERE student_id = @student_id
                  AND exam_id = @exam_id
                  AND UPPER(COALESCE(status, '')) = 'IN_PROGRESS'
                ORDER BY start_time DESC, id DESC
                LIMIT 1", connection, transaction);
            command.Parameters.AddWithValue("@student_id", studentId);
            command.Parameters.AddWithValue("@exam_id", examId);
            object? result = command.ExecuteScalar();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        private static bool CanStartStudentExam(NpgsqlConnection connection, NpgsqlTransaction transaction, int studentId, int examId)
        {
            using var command = new NpgsqlCommand(@"
                SELECT ex.open_time,
                       ex.close_time,
                       COALESCE(ex.max_attempts, 1),
                       (SELECT COUNT(*) FROM exam_attempts a WHERE a.exam_id = ex.id AND a.student_id = @student_id)::int,
                       (SELECT COUNT(*) FROM exam_questions eq WHERE eq.exam_id = ex.id)::int
                FROM exams ex
                JOIN enrollments en ON en.course_id = ex.course_id
                LEFT JOIN courses c ON c.id = ex.course_id
                WHERE ex.id = @exam_id
                  AND en.student_id = @student_id
                  AND UPPER(COALESCE(en.status, '')) IN ('ACTIVE', 'APPROVED')
                  AND UPPER(COALESCE(c.status, 'ACTIVE')) IN ('ACTIVE', 'APPROVED', 'OPEN')
                  AND UPPER(COALESCE(ex.status, 'DRAFT')) = 'ACTIVE'", connection, transaction);
            command.Parameters.AddWithValue("@student_id", studentId);
            command.Parameters.AddWithValue("@exam_id", examId);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
                return false;

            DateTime? openTime = reader.IsDBNull(0) ? null : reader.GetDateTime(0);
            DateTime? closeTime = reader.IsDBNull(1) ? null : reader.GetDateTime(1);
            int maxAttempts = reader.GetInt32(2);
            int attemptCount = reader.GetInt32(3);
            int questionCount = reader.GetInt32(4);
            DateTime now = DateTime.Now;
            return questionCount > 0
                && (!openTime.HasValue || openTime.Value <= now)
                && (!closeTime.HasValue || closeTime.Value >= now)
                && (maxAttempts <= 0 || attemptCount < maxAttempts);
        }

        private static bool OwnsInProgressAttempt(NpgsqlConnection connection, NpgsqlTransaction transaction, int studentId, int attemptId)
        {
            using var command = new NpgsqlCommand(@"
                SELECT COUNT(*)::int
                FROM exam_attempts
                WHERE id = @attempt_id
                  AND student_id = @student_id
                  AND UPPER(COALESCE(status, '')) = 'IN_PROGRESS'", connection, transaction);
            command.Parameters.AddWithValue("@attempt_id", attemptId);
            command.Parameters.AddWithValue("@student_id", studentId);
            return Convert.ToInt32(command.ExecuteScalar() ?? 0) > 0;
        }

        private static StudentExamTakingModel? LoadStudentExamSession(NpgsqlConnection connection, NpgsqlTransaction transaction, int studentId, int attemptId)
        {
            using var headerCommand = new NpgsqlCommand(@"
                SELECT a.id, ex.id, COALESCE(ex.title, ''), COALESCE(c.name, ''),
                       COALESCE(ex.duration_minutes, 0), COALESCE(a.start_time, @now),
                       COALESCE(ex.max_violations, 0), COALESCE(ex.shuffle_questions, FALSE)
                FROM exam_attempts a
                JOIN exams ex ON ex.id = a.exam_id
                LEFT JOIN courses c ON c.id = ex.course_id
                WHERE a.id = @attempt_id
                  AND a.student_id = @student_id
                  AND UPPER(COALESCE(a.status, '')) = 'IN_PROGRESS'", connection, transaction);
            headerCommand.Parameters.AddWithValue("@attempt_id", attemptId);
            headerCommand.Parameters.AddWithValue("@student_id", studentId);
            headerCommand.Parameters.AddWithValue("@now", DateTime.Now);
            using var reader = headerCommand.ExecuteReader();
            if (!reader.Read())
                return null;

            var session = new StudentExamTakingModel
            {
                AttemptId = reader.GetInt32(0),
                ExamId = reader.GetInt32(1),
                ExamTitle = reader.GetString(2),
                CourseName = reader.GetString(3),
                DurationMinutes = reader.GetInt32(4),
                StartTime = reader.GetDateTime(5),
                MaxViolations = reader.GetInt32(6)
            };
            bool shuffleQuestions = reader.GetBoolean(7);
            reader.Close();

            using var questionCommand = new NpgsqlCommand(@"
                SELECT eq.id, COALESCE(eq.display_order, 1), COALESCE(eq.question_text, ''),
                       COALESCE(eq.option_a, ''), COALESCE(eq.option_b, ''),
                       COALESCE(eq.option_c, ''), COALESCE(eq.option_d, ''),
                       COALESCE(aa.selected_option, '')
                FROM exam_questions eq
                JOIN exam_attempts a ON a.exam_id = eq.exam_id
                LEFT JOIN exam_attempt_answers aa ON aa.attempt_id = a.id AND aa.exam_question_id = eq.id
                WHERE a.id = @attempt_id
                ORDER BY COALESCE(eq.display_order, 1), eq.id", connection, transaction);
            questionCommand.Parameters.AddWithValue("@attempt_id", attemptId);
            using var qReader = questionCommand.ExecuteReader();
            while (qReader.Read())
            {
                session.Questions.Add(new StudentExamTakingQuestionModel
                {
                    Id = qReader.GetInt32(0),
                    DisplayOrder = qReader.GetInt32(1),
                    QuestionText = qReader.GetString(2),
                    OptionA = qReader.GetString(3),
                    OptionB = qReader.GetString(4),
                    OptionC = qReader.GetString(5),
                    OptionD = qReader.GetString(6),
                    SelectedOption = qReader.GetString(7)
                });
            }

            if (shuffleQuestions && session.Questions.Count > 1)
            {
                var random = new Random(session.AttemptId); // Use attempt id as seed so it's consistent for the same attempt
                int n = session.Questions.Count;
                while (n > 1)
                {
                    n--;
                    int k = random.Next(n + 1);
                    var value = session.Questions[k];
                    session.Questions[k] = session.Questions[n];
                    session.Questions[n] = value;
                }
                
                // Re-assign display order after shuffling
                for (int i = 0; i < session.Questions.Count; i++)
                {
                    session.Questions[i].DisplayOrder = i + 1;
                }
            }

            return session;
        }

        public async Task<int> BulkInsertQuestionsAndMapToExamAsync(int teacherId, int examId, int courseId, List<TeacherExamQuestionModel> questions, CancellationToken cancellationToken = default)
        {
            if (questions == null || questions.Count == 0) return 0;

            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            EnsureQuestionBankMetadataSchema(connection);
            using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                await using (var guard = new NpgsqlCommand(@"
                    SELECT COUNT(*)::int
                    FROM courses c
                    JOIN exams ex ON ex.course_id = c.id
                    WHERE c.teacher_id = @teacher_id
                      AND ex.id = @exam_id
                      AND ex.course_id = @course_id
                      AND UPPER(COALESCE(ex.status, '')) = @draft_status", connection, transaction))
                {
                    guard.Parameters.AddWithValue("@teacher_id", teacherId);
                    guard.Parameters.AddWithValue("@exam_id", examId);
                    guard.Parameters.AddWithValue("@course_id", courseId);
                    guard.Parameters.AddWithValue("@draft_status", WorkflowConstants.ExamStatus.Draft);

                    int editable = Convert.ToInt32(await guard.ExecuteScalarAsync(cancellationToken) ?? 0);
                    if (editable <= 0)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return 0;
                    }
                }

                // We use a single string builder to insert all questions into the QUESTIONS table
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("INSERT INTO questions (course_id, content, type, correct_answer, score, difficulty, chapter, question_type) VALUES ");

                for (int i = 0; i < questions.Count; i++)
                {
                    sb.Append($"(@course_id, @q_content_{i}, 'SINGLE', @q_correct_{i}, @q_score_{i}, @q_difficulty_{i}, @q_chapter_{i}, @q_type_{i})");
                    if (i < questions.Count - 1) sb.AppendLine(",");
                }
                sb.AppendLine(" RETURNING id;");

                using var qCommand = new NpgsqlCommand(sb.ToString(), connection, transaction);
                qCommand.Parameters.AddWithValue("@course_id", courseId);

                for (int i = 0; i < questions.Count; i++)
                {
                    // Escape is not strictly needed for parameterized queries, but we are using parameters to be safe!
                    var q = questions[i];
                    string fullContent = $"{q.QuestionText}\nA. {q.OptionA}\nB. {q.OptionB}\nC. {q.OptionC}\nD. {q.OptionD}";
                    qCommand.Parameters.AddWithValue($"@q_content_{i}", fullContent);
                    qCommand.Parameters.AddWithValue($"@q_correct_{i}", q.CorrectOption ?? "A");
                    qCommand.Parameters.AddWithValue($"@q_score_{i}", q.Points);
                    qCommand.Parameters.AddWithValue($"@q_difficulty_{i}", QuestionMetadataNormalizer.NormalizeDifficulty(q.Difficulty));
                    qCommand.Parameters.AddWithValue($"@q_chapter_{i}", QuestionMetadataNormalizer.NormalizeChapter(q.Chapter) ?? (object)DBNull.Value);
                    qCommand.Parameters.AddWithValue($"@q_type_{i}", QuestionMetadataNormalizer.NormalizeQuestionType(q.QuestionType));
                }

                var insertedQuestionIds = new List<int>();
                using (var reader = await qCommand.ExecuteReaderAsync(cancellationToken))
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        insertedQuestionIds.Add(reader.GetInt32(0));
                    }
                }

                // Now insert into exam_questions
                var sbEq = new System.Text.StringBuilder();
                sbEq.AppendLine("INSERT INTO exam_questions (exam_id, question_id, question_text, option_a, option_b, option_c, option_d, correct_option, points, display_order, difficulty, chapter, question_type) VALUES ");
                for (int i = 0; i < insertedQuestionIds.Count; i++)
                {
                    sbEq.Append($"(@exam_id, @q_id_{i}, @text_{i}, @opt_a_{i}, @opt_b_{i}, @opt_c_{i}, @opt_d_{i}, @corr_{i}, @pts_{i}, @ord_{i}, @diff_{i}, @chapter_{i}, @type_{i})");
                    if (i < insertedQuestionIds.Count - 1) sbEq.AppendLine(",");
                }

                using var eqCommand = new NpgsqlCommand(sbEq.ToString(), connection, transaction);
                eqCommand.Parameters.AddWithValue("@exam_id", examId);

                for (int i = 0; i < insertedQuestionIds.Count; i++)
                {
                    var q = questions[i];
                    eqCommand.Parameters.AddWithValue($"@q_id_{i}", insertedQuestionIds[i]);
                    eqCommand.Parameters.AddWithValue($"@text_{i}", q.QuestionText ?? "");
                    eqCommand.Parameters.AddWithValue($"@opt_a_{i}", q.OptionA ?? "");
                    eqCommand.Parameters.AddWithValue($"@opt_b_{i}", q.OptionB ?? "");
                    eqCommand.Parameters.AddWithValue($"@opt_c_{i}", q.OptionC ?? "");
                    eqCommand.Parameters.AddWithValue($"@opt_d_{i}", q.OptionD ?? "");
                    eqCommand.Parameters.AddWithValue($"@corr_{i}", q.CorrectOption ?? "A");
                    eqCommand.Parameters.AddWithValue($"@pts_{i}", q.Points);
                    eqCommand.Parameters.AddWithValue($"@ord_{i}", i + 1);
                    eqCommand.Parameters.AddWithValue($"@diff_{i}", QuestionMetadataNormalizer.NormalizeDifficulty(q.Difficulty));
                    eqCommand.Parameters.AddWithValue($"@chapter_{i}", QuestionMetadataNormalizer.NormalizeChapter(q.Chapter) ?? (object)DBNull.Value);
                    eqCommand.Parameters.AddWithValue($"@type_{i}", QuestionMetadataNormalizer.NormalizeQuestionType(q.QuestionType));
                }

                await eqCommand.ExecuteNonQueryAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return insertedQuestionIds.Count;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<int> AddQuestionsFromBankAsync(int examId, int courseId, IReadOnlyList<int> questionIds, CancellationToken cancellationToken = default)
        {
            var distinctQuestionIds = questionIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<int>();
            if (examId <= 0 || courseId <= 0 || distinctQuestionIds.Count == 0) return 0;

            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            EnsureQuestionBankMetadataSchema(connection);
            using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                await using (var guard = new NpgsqlCommand(@"
                    SELECT COUNT(*)::int
                    FROM exams
                    WHERE id = @exam_id
                      AND course_id = @course_id
                      AND UPPER(COALESCE(status, '')) = 'DRAFT'", connection, transaction))
                {
                    guard.Parameters.AddWithValue("@exam_id", examId);
                    guard.Parameters.AddWithValue("@course_id", courseId);
                    int editable = Convert.ToInt32(await guard.ExecuteScalarAsync(cancellationToken) ?? 0);
                    if (editable <= 0)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return 0;
                    }
                }

                int insertedCount = await InsertQuestionBankSnapshotsAsync(connection, transaction, examId, courseId, distinctQuestionIds, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return insertedCount;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public List<TeacherExamQuestionModel> GetQuestionsByCourseId(int courseId)
        {
            var result = new List<TeacherExamQuestionModel>();
            using var connection = CreateConnection();
            connection.Open();
            EnsureQuestionBankMetadataSchema(connection);

            // Extract parts back from content string
            string query = @"SELECT id, content, correct_answer, score, COALESCE(difficulty, 'MEDIUM'), chapter, COALESCE(question_type, 'MULTIPLE_CHOICE') FROM questions WHERE course_id = @course_id ORDER BY id DESC";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@course_id", courseId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                string content = reader.GetString(1);
                string text = content;
                string a = "", b = "", c = "", d = "";

                // Very basic split to reverse the format stored in DB
                int idxA = content.IndexOf("\nA. ");
                int idxB = content.IndexOf("\nB. ");
                int idxC = content.IndexOf("\nC. ");
                int idxD = content.IndexOf("\nD. ");

                if (idxA > 0 && idxB > idxA && idxC > idxB && idxD > idxC)
                {
                    text = content.Substring(0, idxA).Trim();
                    a = content.Substring(idxA + 4, idxB - (idxA + 4)).Trim();
                    b = content.Substring(idxB + 4, idxC - (idxB + 4)).Trim();
                    c = content.Substring(idxC + 4, idxD - (idxC + 4)).Trim();
                    d = content.Substring(idxD + 4).Trim();
                }

                result.Add(new TeacherExamQuestionModel
                {
                    Id = reader.GetInt32(0),
                    QuestionText = text,
                    OptionA = a,
                    OptionB = b,
                    OptionC = c,
                    OptionD = d,
                    CorrectOption = reader.GetString(2),
                    Points = reader.GetDecimal(3),
                    Difficulty = reader.GetString(4),
                    Chapter = reader.IsDBNull(5) ? null : reader.GetString(5),
                    QuestionType = reader.GetString(6)
                });
            }

            return result;
        }

        public List<string> GetQuestionBankChapters(int courseId)
        {
            var chapters = new List<string>();
            using var connection = CreateConnection();
            connection.Open();
            EnsureQuestionBankMetadataSchema(connection);

            const string query = @"
                SELECT DISTINCT chapter
                FROM questions
                WHERE course_id = @course_id
                  AND chapter IS NOT NULL
                  AND TRIM(chapter) <> ''
                ORDER BY chapter";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@course_id", courseId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
                chapters.Add(reader.GetString(0));

            return chapters;
        }

        public List<TeacherExamQuestionModel> GetRandomQuestionsByCriteria(int courseId, IReadOnlyList<RandomQuestionCriteria> criteria)
        {
            var result = new List<TeacherExamQuestionModel>();
            if (criteria == null || criteria.Count == 0)
                return result;

            using var connection = CreateConnection();
            connection.Open();
            EnsureQuestionBankMetadataSchema(connection);

            foreach (RandomQuestionCriteria item in criteria.Where(c => c.Count > 0))
            {
                const string query = @"
                    SELECT id, content, correct_answer, score,
                           COALESCE(difficulty, 'MEDIUM'), chapter, COALESCE(question_type, 'MULTIPLE_CHOICE')
                    FROM questions WHERE course_id = @course_id
                      AND UPPER(COALESCE(difficulty, 'MEDIUM')) = @difficulty
                      AND (@chapter IS NULL OR chapter = @chapter)
                    ORDER BY RANDOM()
                    LIMIT @count";

                using var command = new NpgsqlCommand(query, connection);
                command.Parameters.AddWithValue("@course_id", courseId);
                command.Parameters.AddWithValue("@difficulty", QuestionMetadataNormalizer.NormalizeDifficulty(item.Difficulty));
                command.Parameters.AddWithValue("@chapter", QuestionMetadataNormalizer.NormalizeChapter(item.Chapter) ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@count", item.Count);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var parsed = ParseQuestionBankContent(reader.GetString(1));
                    result.Add(new TeacherExamQuestionModel
                    {
                        Id = reader.GetInt32(0),
                        QuestionText = parsed.Text,
                        OptionA = parsed.OptionA,
                        OptionB = parsed.OptionB,
                        OptionC = parsed.OptionC,
                        OptionD = parsed.OptionD,
                        CorrectOption = reader.GetString(2),
                        Points = reader.GetDecimal(3),
                        Difficulty = reader.GetString(4),
                        Chapter = reader.IsDBNull(5) ? null : reader.GetString(5),
                        QuestionType = reader.GetString(6)
                    });
                }
            }

            return result;
        }

        public async Task<int> AddRandomQuestionsToExamAsync(int examId, int courseId, IReadOnlyList<RandomQuestionCriteria> criteria, CancellationToken cancellationToken = default)
        {
            if (criteria == null || criteria.Count == 0)
                return 0;

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            EnsureQuestionBankMetadataSchema(connection);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                await using (var guard = new NpgsqlCommand(@"
                    SELECT COUNT(*)::int
                    FROM exams
                    WHERE id = @exam_id
                      AND course_id = @course_id
                      AND UPPER(COALESCE(status, '')) = 'DRAFT'", connection, transaction))
                {
                    guard.Parameters.AddWithValue("@exam_id", examId);
                    guard.Parameters.AddWithValue("@course_id", courseId);
                    int editable = Convert.ToInt32(await guard.ExecuteScalarAsync(cancellationToken) ?? 0);
                    if (editable <= 0)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return 0;
                    }
                }

                var selectedIds = new List<int>();
                foreach (RandomQuestionCriteria item in criteria.Where(c => c.Count > 0))
                {
                    await using var randomCommand = new NpgsqlCommand(@"
                        SELECT id
                        FROM questions
                        WHERE course_id = @course_id
                          AND UPPER(COALESCE(difficulty, 'MEDIUM')) = @difficulty
                          AND (@chapter IS NULL OR chapter = @chapter)
                        ORDER BY RANDOM()
                        LIMIT @count", connection, transaction);
                    randomCommand.Parameters.AddWithValue("@course_id", courseId);
                    randomCommand.Parameters.AddWithValue("@difficulty", QuestionMetadataNormalizer.NormalizeDifficulty(item.Difficulty));
                    randomCommand.Parameters.AddWithValue("@chapter", QuestionMetadataNormalizer.NormalizeChapter(item.Chapter) ?? (object)DBNull.Value);
                    randomCommand.Parameters.AddWithValue("@count", item.Count);

                    await using var reader = await randomCommand.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                        selectedIds.Add(reader.GetInt32(0));
                }

                var distinctSelectedIds = selectedIds
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();
                if (distinctSelectedIds.Count == 0)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return 0;
                }

                int insertedCount = await InsertQuestionBankSnapshotsAsync(connection, transaction, examId, courseId, distinctSelectedIds, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return insertedCount;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        private static async Task<int> InsertQuestionBankSnapshotsAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            int examId,
            int courseId,
            IReadOnlyList<int> questionIds,
            CancellationToken cancellationToken)
        {
            if (questionIds.Count == 0)
                return 0;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(@"WITH selected AS (
                                SELECT q.id, q.content, q.correct_answer, q.score,
                                       COALESCE(q.difficulty, 'MEDIUM') AS difficulty,
                                       q.chapter,
                                       COALESCE(q.question_type, 'MULTIPLE_CHOICE') AS question_type,
                                       row_number() over (order by q.id) AS rn
                                FROM questions q
                                WHERE q.course_id = @course_id
                                  AND q.id IN (");
            for (int i = 0; i < questionIds.Count; i++)
            {
                sb.Append($"@qid_{i}");
                if (i < questionIds.Count - 1)
                    sb.Append(", ");
            }

            sb.AppendLine(@")
                                  AND NOT EXISTS (
                                      SELECT 1
                                      FROM exam_questions existing
                                      WHERE existing.exam_id = @exam_id
                                        AND existing.question_id = q.id
                                  )
                            ),
                            order_base AS (
                                SELECT COALESCE(MAX(display_order), 0) AS start_order
                                FROM exam_questions
                                WHERE exam_id = @exam_id
                            )
                            INSERT INTO exam_questions (exam_id, question_id, question_text, option_a, option_b, option_c, option_d, correct_option, points, display_order, difficulty, chapter, question_type)
                            SELECT @exam_id, id, split_part(content, E'\nA.', 1),
                                   split_part(split_part(content, E'\nA. ', 2), E'\nB. ', 1),
                                   split_part(split_part(content, E'\nB. ', 2), E'\nC. ', 1),
                                   split_part(split_part(content, E'\nC. ', 2), E'\nD. ', 1),
                                   substring(content from E'\\nD. (.*)$'),
                                   correct_answer, score, order_base.start_order + rn,
                                   difficulty, chapter, question_type
                            FROM selected
                            CROSS JOIN order_base;");

            await using var command = new NpgsqlCommand(sb.ToString(), connection, transaction);
            command.Parameters.AddWithValue("@exam_id", examId);
            command.Parameters.AddWithValue("@course_id", courseId);
            for (int i = 0; i < questionIds.Count; i++)
                command.Parameters.AddWithValue($"@qid_{i}", questionIds[i]);

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static (string Text, string OptionA, string OptionB, string OptionC, string OptionD) ParseQuestionBankContent(string content)
        {
            string text = content;
            string a = string.Empty;
            string b = string.Empty;
            string c = string.Empty;
            string d = string.Empty;

            int idxA = content.IndexOf("\nA. ", StringComparison.Ordinal);
            int idxB = content.IndexOf("\nB. ", StringComparison.Ordinal);
            int idxC = content.IndexOf("\nC. ", StringComparison.Ordinal);
            int idxD = content.IndexOf("\nD. ", StringComparison.Ordinal);

            if (idxA > 0 && idxB > idxA && idxC > idxB && idxD > idxC)
            {
                text = content.Substring(0, idxA).Trim();
                a = content.Substring(idxA + 4, idxB - (idxA + 4)).Trim();
                b = content.Substring(idxB + 4, idxC - (idxB + 4)).Trim();
                c = content.Substring(idxC + 4, idxD - (idxC + 4)).Trim();
                d = content.Substring(idxD + 4).Trim();
            }

            return (text, a, b, c, d);
        }

        private static string NormalizeOption(string? value)
        {
            string option = (value ?? string.Empty).Trim().ToUpperInvariant();
            return option is "A" or "B" or "C" or "D" ? option : string.Empty;
        }

        private static string BuildResultStatus(double score, string rawStatus)
        {
            if (string.Equals(rawStatus, "SUBMITTED", StringComparison.OrdinalIgnoreCase))
            {
                return score < 5.0 ? "Không đạt" : "Đạt";
            }
            if (string.Equals(rawStatus, "IN_PROGRESS", StringComparison.OrdinalIgnoreCase))
            {
                return "Đang làm";
            }
            if (!string.IsNullOrWhiteSpace(rawStatus))
            {
                return rawStatus;
            }

            return score < 5.0 ? "Không đạt" : "Đạt";
        }

        public List<UserModel> GetUsersByStatus(params string[] statuses)
        {
            var users = new List<UserModel>();
            using var connection = CreateConnection();
            connection.Open();

            string statusParams = string.Join(",", statuses.Select((s, i) => $"@s{i}"));
            string query = $@"SELECT u.id, u.username, u.password_hash, u.full_name, u.email, r.name as role, u.status 
                             FROM USERS u 
                             JOIN ROLES r ON u.role_id = r.id 
                             WHERE UPPER(u.status) IN ({statusParams})";
            
            using var command = new NpgsqlCommand(query, connection);
            for (int i = 0; i < statuses.Length; i++)
            {
                command.Parameters.AddWithValue($"@s{i}", statuses[i].ToUpperInvariant());
            }

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                users.Add(new UserModel
                {
                    Id = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    PasswordHash = reader.GetString(2),
                    FullName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    Email = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Role = reader.GetString(5),
                    Status = reader.GetString(6)
                });
            }

            return users;
        }

        public UserModel? GetUserByUsernameAndEmail(string username, string email)
        {
            using var connection = CreateConnection();
            connection.Open();
            EnsureUserSecuritySchema(connection);

            string query = @"SELECT u.id, u.username, u.password_hash, u.full_name, u.email, r.name as role, u.status,
                                    u.temp_password_expires_at,
                                    '' AS avatar_path,
                                    u.failed_attempts, u.lockout_until, u.mfa_otp, u.mfa_otp_expires_at
                            FROM USERS u 
                            JOIN ROLES r ON u.role_id = r.id 
                            WHERE LOWER(u.username) = LOWER(@username) AND u.email = @email";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@username", username);
            command.Parameters.AddWithValue("@email", email);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return ReadUser(reader, tempPasswordExpiresAtIndex: 7, avatarPathIndex: 8);
            }

            return null;
        }

        public UserModel? GetUserById(int userId)
        {
            using var connection = CreateConnection();
            connection.Open();
            EnsureUserSecuritySchema(connection);

            const string query = @"SELECT u.id, u.username, u.password_hash, u.full_name, u.email, r.name as role, u.status,
                                   u.temp_password_expires_at,
                                   '' AS avatar_path,
                                   u.failed_attempts, u.lockout_until, u.mfa_otp, u.mfa_otp_expires_at
                                   FROM USERS u
                                   JOIN ROLES r ON u.role_id = r.id
                                   WHERE u.id = @id";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@id", userId);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return ReadUser(reader, tempPasswordExpiresAtIndex: 7, avatarPathIndex: 8);
            }

            return null;
        }

        public bool DeleteUser(int userId)
        {
            using var connection = CreateConnection();
            connection.Open();

            // Avoid deleting admin account to keep system operable.
            const string roleCheckQuery = @"
                SELECT COALESCE(r.NAME, '')
                FROM USERS u
                JOIN ROLES r ON u.ROLE_ID = r.ID
                WHERE u.ID = @id";
            using (var roleCmd = new NpgsqlCommand(roleCheckQuery, connection))
            {
                roleCmd.Parameters.AddWithValue("@id", userId);
                string roleName = (roleCmd.ExecuteScalar()?.ToString() ?? string.Empty).ToUpperInvariant();
                if (roleName == "ADMIN")
                {
                    return false;
                }
            }

            const string deleteQuery = "DELETE FROM USERS WHERE id = @id";
            using var command = new NpgsqlCommand(deleteQuery, connection);
            command.Parameters.AddWithValue("@id", userId);
            int affectedRows = command.ExecuteNonQuery();
            return affectedRows > 0;
        }

        public List<UserModel> GetUsersByRole(string role)
        {
            var users = new List<UserModel>();
            using var connection = CreateConnection();
            connection.Open();

            const string query = @"SELECT u.id, u.username, u.password_hash, u.full_name, u.email, r.name as role, u.status
                                   FROM USERS u
                                   JOIN ROLES r ON u.role_id = r.id
                                   WHERE UPPER(r.name) = UPPER(@role)
                                   ORDER BY u.id DESC";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@role", role);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                users.Add(new UserModel
                {
                    Id = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    PasswordHash = reader.GetString(2),
                    FullName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    Email = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Role = reader.GetString(5),
                    Status = reader.GetString(6)
                });
            }

            return users;
        }

        public List<CourseModel> GetAllCourses()
        {
            var courses = new List<CourseModel>();
            using var connection = CreateConnection();
            connection.Open();

            string query = @"SELECT c.id, c.name, c.description, c.teacher_id, u.full_name as teacher_name,
                                    c.status, c.start_date, c.end_date, COALESCE(c.rejection_reason, ''),
                                    COALESCE(c.teaching_days, ''), c.session_start_time, c.session_end_time,
                                    COALESCE(c.frequency, 'Weekly'), COALESCE(c.meeting_link, '')
                             FROM COURSES c 
                             JOIN USERS u ON c.teacher_id = u.id";
            
            using var command = new NpgsqlCommand(query, connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                courses.Add(new CourseModel
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    TeacherId = reader.GetInt32(3),
                    TeacherName = reader.IsDBNull(4) ? "Unknown" : reader.GetString(4),
                    Status = reader.GetString(5),
                    StartDate = reader.IsDBNull(6) ? DateTime.MinValue : reader.GetDateTime(6),
                    EndDate = reader.IsDBNull(7) ? DateTime.MinValue : reader.GetDateTime(7),
                    RejectionReason = reader.GetString(8),
                    TeachingDays = reader.GetString(9),
                    SessionStartTime = reader.IsDBNull(10) ? null : reader.GetTimeSpan(10),
                    SessionEndTime = reader.IsDBNull(11) ? null : reader.GetTimeSpan(11),
                    Frequency = reader.GetString(12),
                    MeetingLink = reader.GetString(13)
                });
            }

            return courses;
        }

        public CourseModel? GetCourseById(int courseId)
        {
            using var connection = CreateConnection();
            connection.Open();

            const string query = @"
                SELECT c.id, c.name, COALESCE(c.description, ''), c.teacher_id,
                       COALESCE(u.full_name, u.username) AS teacher_name,
                       COALESCE(c.status, 'ACTIVE'), c.start_date, c.end_date,
                       COALESCE(c.rejection_reason, ''),
                       COALESCE(c.teaching_days, ''), c.session_start_time, c.session_end_time,
                       COALESCE(c.frequency, 'Weekly'), COALESCE(c.meeting_link, '')
                FROM courses c
                JOIN users u ON u.id = c.teacher_id
                WHERE c.id = @id";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@id", courseId);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
                return null;

            return new CourseModel
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.GetString(2),
                TeacherId = reader.GetInt32(3),
                TeacherName = reader.GetString(4),
                Status = reader.GetString(5),
                StartDate = reader.IsDBNull(6) ? DateTime.MinValue : reader.GetDateTime(6),
                EndDate = reader.IsDBNull(7) ? DateTime.MinValue : reader.GetDateTime(7),
                RejectionReason = reader.GetString(8),
                TeachingDays = reader.GetString(9),
                SessionStartTime = reader.IsDBNull(10) ? null : reader.GetTimeSpan(10),
                SessionEndTime = reader.IsDBNull(11) ? null : reader.GetTimeSpan(11),
                Frequency = reader.GetString(12),
                MeetingLink = reader.GetString(13)
            };
        }

        public void InsertCourse(CourseModel course)
        {
            using var connection = CreateConnection();
            connection.Open();

            string query = @"INSERT INTO COURSES (name, description, teacher_id, status, rejection_reason, start_date, end_date, teaching_days, session_start_time, session_end_time, frequency, meeting_link) 
                            VALUES (@name, @description, @teacher_id, @status, @rejection_reason, @start_date, @end_date, @teaching_days, @session_start_time, @session_end_time, @frequency, @meeting_link) RETURNING id";
            
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@name", course.Name);
            command.Parameters.AddWithValue("@description", course.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@teacher_id", course.TeacherId);
            command.Parameters.AddWithValue("@status", course.Status ?? "ACTIVE");
            command.Parameters.AddWithValue("@rejection_reason", string.IsNullOrWhiteSpace(course.RejectionReason) ? (object)DBNull.Value : course.RejectionReason);
            command.Parameters.AddWithValue("@start_date", course.StartDate == DateTime.MinValue ? (object)DBNull.Value : course.StartDate);
            command.Parameters.AddWithValue("@end_date", course.EndDate == DateTime.MinValue ? (object)DBNull.Value : course.EndDate);
            command.Parameters.AddWithValue("@teaching_days", course.TeachingDays ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@session_start_time", course.SessionStartTime.HasValue ? course.SessionStartTime.Value : (object)DBNull.Value);
            command.Parameters.AddWithValue("@session_end_time", course.SessionEndTime.HasValue ? course.SessionEndTime.Value : (object)DBNull.Value);
            command.Parameters.AddWithValue("@frequency", course.Frequency ?? "Weekly");
            command.Parameters.AddWithValue("@meeting_link", course.MeetingLink ?? (object)DBNull.Value);

            int courseId = Convert.ToInt32(command.ExecuteScalar());
            course.Id = courseId;

            if (course.GenerateSchedule)
            {
                InsertGeneratedSessions(courseId, course.TeacherId, course.Name, course.StartDate, course.EndDate, course.TeachingDays, course.SessionStartTime, course.SessionEndTime, course.MeetingLink);
            }
        }

        public void UpdateCourse(CourseModel course)
        {
            using var connection = CreateConnection();
            connection.Open();

            string query = @"UPDATE COURSES SET name = @name, description = @description, teacher_id = @teacher_id, 
                             status = @status, rejection_reason = @rejection_reason, start_date = @start_date, end_date = @end_date,
                             teaching_days = @teaching_days, session_start_time = @session_start_time, session_end_time = @session_end_time,
                             frequency = @frequency, meeting_link = @meeting_link WHERE id = @id";
            
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@name", course.Name);
            command.Parameters.AddWithValue("@description", course.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@teacher_id", course.TeacherId);
            command.Parameters.AddWithValue("@status", course.Status);
            command.Parameters.AddWithValue("@rejection_reason", string.IsNullOrWhiteSpace(course.RejectionReason) ? (object)DBNull.Value : course.RejectionReason);
            command.Parameters.AddWithValue("@start_date", course.StartDate == DateTime.MinValue ? (object)DBNull.Value : course.StartDate);
            command.Parameters.AddWithValue("@end_date", course.EndDate == DateTime.MinValue ? (object)DBNull.Value : course.EndDate);
            command.Parameters.AddWithValue("@teaching_days", course.TeachingDays ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@session_start_time", course.SessionStartTime.HasValue ? course.SessionStartTime.Value : (object)DBNull.Value);
            command.Parameters.AddWithValue("@session_end_time", course.SessionEndTime.HasValue ? course.SessionEndTime.Value : (object)DBNull.Value);
            command.Parameters.AddWithValue("@frequency", course.Frequency ?? "Weekly");
            command.Parameters.AddWithValue("@meeting_link", course.MeetingLink ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@id", course.Id);

            command.ExecuteNonQuery();

            if (course.GenerateSchedule)
            {
                using var deleteCmd = new NpgsqlCommand("DELETE FROM online_sessions WHERE course_id = @course_id", connection);
                deleteCmd.Parameters.AddWithValue("@course_id", course.Id);
                deleteCmd.ExecuteNonQuery();

                InsertGeneratedSessions(course.Id, course.TeacherId, course.Name, course.StartDate, course.EndDate, course.TeachingDays, course.SessionStartTime, course.SessionEndTime, course.MeetingLink);
            }
        }

        public void DeleteCourse(int courseId)
        {
            using var connection = CreateConnection();
            connection.Open();

            string query = "DELETE FROM COURSES WHERE id = @id";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@id", courseId);

            command.ExecuteNonQuery();
        }

        public bool EnrollStudent(int courseId, int studentId)
        {
            using var connection = CreateConnection();
            connection.Open();

            const string query = @"
                WITH existing AS (
                    SELECT 1 FROM ENROLLMENTS WHERE course_id = @course_id AND student_id = @student_id
                )
                INSERT INTO ENROLLMENTS (course_id, student_id, status)
                SELECT @course_id, @student_id, 'ACTIVE'
                WHERE NOT EXISTS (SELECT 1 FROM existing)";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@course_id", courseId);
            command.Parameters.AddWithValue("@student_id", studentId);

            return command.ExecuteNonQuery() > 0;
        }

        public HashSet<int> GetStudentEnrolledCourseIds(int studentId)
        {
            var result = new HashSet<int>();
            using var connection = CreateConnection();
            connection.Open();

            const string query = @"
                SELECT COURSE_ID
                FROM ENROLLMENTS
                WHERE STUDENT_ID = @student_id
                  AND UPPER(COALESCE(STATUS, 'ACTIVE')) IN ('ACTIVE', 'APPROVED')";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@student_id", studentId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(reader.GetInt32(0));
            }

            return result;
        }

        // ════════════════════════════════════════════════════════════════
        //  STUDENT ENROLLMENT METHODS
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Lấy danh sách khóa học mà sinh viên đã ghi danh (bất kể trạng thái).
        /// </summary>
        public List<EnrollmentModel> GetEnrollmentsByStudent(int studentId)
        {
            var list = new List<EnrollmentModel>();
            using var connection = CreateConnection();
            connection.Open();

            const string query = @"
                SELECT e.id, e.course_id, e.student_id, e.status, e.joined_at,
                       c.name AS course_name, COALESCE(u.full_name, u.username) AS teacher_name,
                       c.status AS course_status, c.start_date, c.end_date, COALESCE(c.description, '') AS description
                FROM ENROLLMENTS e
                JOIN COURSES c ON e.course_id = c.id
                JOIN USERS u ON c.teacher_id = u.id
                WHERE e.student_id = @student_id
                ORDER BY e.joined_at DESC";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@student_id", studentId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new EnrollmentModel
                {
                    Id = reader.GetInt32(0),
                    CourseId = reader.GetInt32(1),
                    StudentId = reader.GetInt32(2),
                    Status = reader.GetString(3),
                    JoinedAt = reader.IsDBNull(4) ? DateTime.MinValue : reader.GetDateTime(4),
                    CourseName = reader.GetString(5),
                    TeacherName = reader.IsDBNull(6) ? "Unknown" : reader.GetString(6),
                    CourseStatus = reader.GetString(7),
                    CourseStartDate = reader.IsDBNull(8) ? DateTime.MinValue : reader.GetDateTime(8),
                    CourseEndDate = reader.IsDBNull(9) ? DateTime.MinValue : reader.GetDateTime(9),
                    CourseDescription = reader.GetString(10)
                });
            }

            return list;
        }

        public List<StudentScheduleItemModel> GetStudentOnlineSessions(int studentId)
        {
            var rows = new List<StudentScheduleItemModel>();
            using var connection = CreateConnection();
            connection.Open();

            const string query = @"
                SELECT os.id, os.course_id, COALESCE(c.name, ''),
                       COALESCE(u.full_name, u.username, ''),
                       COALESCE(os.title, ''), os.start_time, os.end_time,
                       COALESCE(os.meeting_link, ''), COALESCE(os.is_opened, FALSE)
                FROM online_sessions os
                JOIN courses c ON c.id = os.course_id
                JOIN enrollments e ON e.course_id = c.id
                JOIN users u ON u.id = c.teacher_id
                WHERE e.student_id = @student_id
                  AND UPPER(COALESCE(e.status, '')) IN ('ACTIVE', 'APPROVED')
                  AND UPPER(COALESCE(c.status, '')) IN ('ACTIVE', 'APPROVED', 'OPEN')
                ORDER BY os.start_time ASC NULLS LAST, os.id DESC";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@student_id", studentId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new StudentScheduleItemModel
                {
                    SessionId = reader.GetInt32(0),
                    CourseId = reader.GetInt32(1),
                    CourseName = reader.GetString(2),
                    TeacherName = reader.GetString(3),
                    Title = reader.GetString(4),
                    StartTime = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    EndTime = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                    MeetingLink = reader.GetString(7),
                    IsOpened = reader.GetBoolean(8)
                });
            }

            return rows;
        }

        /// <summary>
        /// Lấy trạng thái enrollment hiện tại của sinh viên trong một khóa học.
        /// Trả về null nếu chưa ghi danh.
        /// </summary>
        public string? GetEnrollmentStatus(int courseId, int studentId)
        {
            using var connection = CreateConnection();
            connection.Open();

            const string query = "SELECT status FROM ENROLLMENTS WHERE course_id = @course_id AND student_id = @student_id";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@course_id", courseId);
            command.Parameters.AddWithValue("@student_id", studentId);

            var result = command.ExecuteScalar();
            return result?.ToString();
        }

        /// <summary>
        /// Sinh viên tự ghi danh vào khóa học (trạng thái PENDING, chờ Admin/Teacher duyệt).
        /// Trả về true nếu thành công, false nếu đã tồn tại.
        /// </summary>
        public bool SelfEnroll(int courseId, int studentId)
        {
            using var connection = CreateConnection();
            connection.Open();

            const string query = @"
                UPDATE enrollments
                SET status = 'PENDING', joined_at = CURRENT_TIMESTAMP
                WHERE course_id = @course_id
                  AND student_id = @student_id
                  AND UPPER(COALESCE(status, '')) = 'REJECTED'
                  AND EXISTS (
                      SELECT 1 FROM courses
                      WHERE id = @course_id AND UPPER(COALESCE(status, '')) = 'ACTIVE'
                  );

                INSERT INTO ENROLLMENTS (course_id, student_id, status)
                SELECT @course_id, @student_id, 'PENDING'
                WHERE EXISTS (
                    SELECT 1 FROM courses
                    WHERE id = @course_id AND UPPER(COALESCE(status, '')) = 'ACTIVE'
                )
                  AND NOT EXISTS (
                    SELECT 1 FROM ENROLLMENTS
                    WHERE course_id = @course_id AND student_id = @student_id
                );

                SELECT COUNT(*)
                FROM enrollments
                WHERE course_id = @course_id
                  AND student_id = @student_id
                  AND UPPER(COALESCE(status, '')) = 'PENDING';";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@course_id", courseId);
            command.Parameters.AddWithValue("@student_id", studentId);

            return Convert.ToInt64(command.ExecuteScalar()) > 0;
        }

        /// <summary>
        /// Hủy / rút khỏi khóa học.
        /// Nếu trạng thái là PENDING → xóa hẳn record.
        /// Nếu trạng thái là ACTIVE → chuyển sang DROPPED.
        /// </summary>
        public bool DropEnrollment(int courseId, int studentId)
        {
            using var connection = CreateConnection();
            connection.Open();

            // Lấy trạng thái hiện tại
            const string statusQuery = "SELECT status FROM ENROLLMENTS WHERE course_id = @cid AND student_id = @sid";
            using var statusCmd = new NpgsqlCommand(statusQuery, connection);
            statusCmd.Parameters.AddWithValue("@cid", courseId);
            statusCmd.Parameters.AddWithValue("@sid", studentId);
            var currentStatus = statusCmd.ExecuteScalar()?.ToString();

            if (string.IsNullOrEmpty(currentStatus)) return false;

            if (currentStatus == "PENDING")
            {
                // Xóa hẳn nếu chưa được duyệt
                const string deleteQuery = "DELETE FROM ENROLLMENTS WHERE course_id = @cid AND student_id = @sid";
                using var deleteCmd = new NpgsqlCommand(deleteQuery, connection);
                deleteCmd.Parameters.AddWithValue("@cid", courseId);
                deleteCmd.Parameters.AddWithValue("@sid", studentId);
                return deleteCmd.ExecuteNonQuery() > 0;
            }
            else
            {
                // Chuyển sang DROPPED nếu đang ACTIVE
                const string updateQuery = "UPDATE ENROLLMENTS SET status = 'DROPPED' WHERE course_id = @cid AND student_id = @sid AND status = 'ACTIVE'";
                using var updateCmd = new NpgsqlCommand(updateQuery, connection);
                updateCmd.Parameters.AddWithValue("@cid", courseId);
                updateCmd.Parameters.AddWithValue("@sid", studentId);
                return updateCmd.ExecuteNonQuery() > 0;
            }
        }

        /// <summary>
        /// Lấy danh sách khóa học ACTIVE mà sinh viên CHƯA ghi danh (để hiển thị "Browse Courses").
        /// </summary>
        public List<CourseModel> GetAvailableCourses(int studentId)
        {
            var courses = new List<CourseModel>();
            using var connection = CreateConnection();
            connection.Open();

            const string query = @"
                SELECT c.id, c.name, COALESCE(c.description, '') AS description, c.teacher_id,
                       COALESCE(u.full_name, u.username) AS teacher_name,
                       c.status, c.start_date, c.end_date
                FROM COURSES c
                JOIN USERS u ON c.teacher_id = u.id
                WHERE UPPER(COALESCE(c.status, '')) = 'ACTIVE'
                  AND c.id NOT IN (
                      SELECT course_id FROM ENROLLMENTS
                      WHERE student_id = @student_id
                        AND UPPER(COALESCE(status, '')) IN ('PENDING', 'ACTIVE', 'APPROVED')
                  )
                ORDER BY c.created_at DESC";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@student_id", studentId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                courses.Add(new CourseModel
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    TeacherId = reader.GetInt32(3),
                    TeacherName = reader.IsDBNull(4) ? "Unknown" : reader.GetString(4),
                    Status = reader.GetString(5),
                    StartDate = reader.IsDBNull(6) ? DateTime.MinValue : reader.GetDateTime(6),
                    EndDate = reader.IsDBNull(7) ? DateTime.MinValue : reader.GetDateTime(7)
                });
            }

            return courses;
        }

        /// <summary>
        /// Đếm số sinh viên đang tham gia (ACTIVE hoặc PENDING) trong một khóa học.
        /// </summary>
        public int GetEnrolledCount(int courseId)
        {
            using var connection = CreateConnection();
            connection.Open();

            const string query = "SELECT COUNT(*) FROM ENROLLMENTS WHERE course_id = @course_id AND status IN ('ACTIVE', 'PENDING')";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@course_id", courseId);

            return Convert.ToInt32(command.ExecuteScalar());
        }


        // ════════════════════════════════════════════════════════════════
        //  ADMIN / TEACHER ENROLLMENT METHODS
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Lấy danh sách các sinh viên đang có trạng thái PENDING để duyệt.
        /// Có thể lọc theo khóa học (courseId) nếu muốn.
        /// </summary>
        public List<EnrollmentModel> GetPendingEnrollments(int? courseId = null)
        {
            var list = new List<EnrollmentModel>();
            using var connection = CreateConnection();
            connection.Open();

            string query = @"
                SELECT e.id, e.course_id, e.student_id, e.status, e.joined_at,
                       c.name AS course_name, COALESCE(u.full_name, u.username) AS teacher_name,
                       c.status AS course_status, c.start_date, c.end_date, COALESCE(c.description, '') AS description,
                       COALESCE(s.full_name, s.username) AS student_name
                FROM ENROLLMENTS e
                JOIN COURSES c ON e.course_id = c.id
                JOIN USERS u ON c.teacher_id = u.id
                JOIN USERS s ON e.student_id = s.id
                WHERE e.status = 'PENDING'";

            if (courseId.HasValue)
            {
                query += " AND e.course_id = @course_id";
            }
            query += " ORDER BY e.joined_at DESC";

            using var command = new NpgsqlCommand(query, connection);
            if (courseId.HasValue)
            {
                command.Parameters.AddWithValue("@course_id", courseId.Value);
            }

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new EnrollmentModel
                {
                    Id = reader.GetInt32(0),
                    CourseId = reader.GetInt32(1),
                    StudentId = reader.GetInt32(2),
                    Status = reader.GetString(3),
                    JoinedAt = reader.IsDBNull(4) ? DateTime.MinValue : reader.GetDateTime(4),
                    CourseName = reader.GetString(5),
                    TeacherName = reader.IsDBNull(6) ? "Unknown" : reader.GetString(6),
                    CourseStatus = reader.GetString(7),
                    CourseStartDate = reader.IsDBNull(8) ? DateTime.MinValue : reader.GetDateTime(8),
                    CourseEndDate = reader.IsDBNull(9) ? DateTime.MinValue : reader.GetDateTime(9),
                    CourseDescription = reader.GetString(10),
                    StudentName = reader.GetString(11)
                });
            }

            return list;
        }

        public List<EnrollmentModel> GetEnrollmentsByStatus(int courseId, string status)
        {
            var list = new List<EnrollmentModel>();
            using var connection = CreateConnection();
            connection.Open();

            const string query = @"
                SELECT e.id, e.course_id, e.student_id, e.status, e.joined_at,
                       c.name AS course_name, COALESCE(u.full_name, u.username) AS teacher_name,
                       c.status AS course_status, c.start_date, c.end_date, COALESCE(c.description, '') AS description,
                       COALESCE(s.full_name, s.username) AS student_name
                FROM ENROLLMENTS e
                JOIN COURSES c ON e.course_id = c.id
                JOIN USERS u ON c.teacher_id = u.id
                JOIN USERS s ON e.student_id = s.id
                WHERE e.course_id = @course_id AND UPPER(e.status) = UPPER(@status)
                ORDER BY e.joined_at DESC";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@course_id", courseId);
            command.Parameters.AddWithValue("@status", status);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new EnrollmentModel
                {
                    Id = reader.GetInt32(0),
                    CourseId = reader.GetInt32(1),
                    StudentId = reader.GetInt32(2),
                    Status = reader.GetString(3),
                    JoinedAt = reader.IsDBNull(4) ? DateTime.MinValue : reader.GetDateTime(4),
                    CourseName = reader.GetString(5),
                    TeacherName = reader.IsDBNull(6) ? "Unknown" : reader.GetString(6),
                    CourseStatus = reader.GetString(7),
                    CourseStartDate = reader.IsDBNull(8) ? DateTime.MinValue : reader.GetDateTime(8),
                    CourseEndDate = reader.IsDBNull(9) ? DateTime.MinValue : reader.GetDateTime(9),
                    CourseDescription = reader.GetString(10),
                    StudentName = reader.GetString(11)
                });
            }

            return list;
        }

        /// <summary>
        /// Duyệt yêu cầu đăng ký của sinh viên (PENDING -> ACTIVE)
        /// </summary>
        public bool ApproveEnrollment(int courseId, int studentId)
        {
            using var connection = CreateConnection();
            connection.Open();

            const string query = "UPDATE ENROLLMENTS SET status = 'ACTIVE' WHERE course_id = @cid AND student_id = @sid AND status = 'PENDING'";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@cid", courseId);
            command.Parameters.AddWithValue("@sid", studentId);

            return command.ExecuteNonQuery() > 0;
        }

        /// <summary>
        /// Từ chối/Xóa yêu cầu đăng ký của sinh viên (chuyển DROPPED hoặc xóa)
        /// Ở đây ta xóa luôn record nếu là PENDING để họ có thể đăng ký lại, hoặc chuyển sang DROPPED.
        /// </summary>
        public bool RejectEnrollment(int courseId, int studentId)
        {
            using var connection = CreateConnection();
            connection.Open();

            const string query = @"
                UPDATE enrollments
                SET status = 'REJECTED'
                WHERE course_id = @cid
                  AND student_id = @sid
                  AND UPPER(COALESCE(status, '')) = 'PENDING'";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@cid", courseId);
            command.Parameters.AddWithValue("@sid", studentId);

            return command.ExecuteNonQuery() > 0;
        }

        public bool UserExists(string username)
        {
            using var connection = CreateConnection();
            connection.Open();
            string query = "SELECT COUNT(*) FROM USERS WHERE LOWER(username) = LOWER(@username)";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@username", username);
            return Convert.ToInt64(command.ExecuteScalar()) > 0;
        }

        public bool EmailExists(string email)
        {
            using var connection = CreateConnection();
            connection.Open();
            const string query = "SELECT COUNT(*) FROM USERS WHERE LOWER(email) = LOWER(@email)";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@email", email);
            return Convert.ToInt64(command.ExecuteScalar()) > 0;
        }

        public List<(string Username, string Email)> GetAllUsernamesAndEmails()
        {
            var result = new List<(string Username, string Email)>();
            using var connection = CreateConnection();
            connection.Open();
            const string query = "SELECT COALESCE(username, ''), COALESCE(email, '') FROM USERS";
            using var command = new NpgsqlCommand(query, connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add((reader.GetString(0), reader.GetString(1)));
            }
            return result;
        }

        public void LogDeviceActivity(int userId, string deviceName, string ipAddress)
        {
            using var connection = CreateConnection();
            connection.Open();
            const string query = @"
                WITH updated AS (
                    UPDATE DEVICES
                    SET last_active = @now,
                        ip_address = @ip_address,
                        status = 'ACTIVE'
                    WHERE user_id = @user_id AND device_name = @device_name
                    RETURNING id
                )
                INSERT INTO DEVICES (user_id, device_name, ip_address, status, last_active)
                SELECT @user_id, @device_name, @ip_address, 'ACTIVE', @now
                WHERE NOT EXISTS (SELECT 1 FROM updated)";
            
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@user_id", userId);
            command.Parameters.AddWithValue("@device_name", deviceName ?? "Unknown Device");
            command.Parameters.AddWithValue("@ip_address", ipAddress ?? "Unknown IP");
            command.Parameters.AddWithValue("@now", DateTime.Now);

            command.ExecuteNonQuery();
        }

        private static void EnsureChatSchema(NpgsqlConnection connection)
        {
            const string sql = @"
                CREATE TABLE IF NOT EXISTS MESSAGES (
                    ID SERIAL PRIMARY KEY,
                    COURSE_ID INT NOT NULL,
                    SENDER_ID INT NOT NULL,
                    CONTENT TEXT,
                    SENT_AT TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (COURSE_ID) REFERENCES COURSES(ID) ON DELETE CASCADE,
                    FOREIGN KEY (SENDER_ID) REFERENCES USERS(ID)
                );
                ALTER TABLE MESSAGES ADD COLUMN IF NOT EXISTS MESSAGE_TYPE VARCHAR(20) NOT NULL DEFAULT 'TEXT';
                ALTER TABLE MESSAGES ADD COLUMN IF NOT EXISTS FILE_URL TEXT;
                ALTER TABLE MESSAGES ADD COLUMN IF NOT EXISTS FILE_NAME VARCHAR(255);
                ALTER TABLE MESSAGES ADD COLUMN IF NOT EXISTS FILE_SIZE BIGINT;
                ALTER TABLE MESSAGES ADD COLUMN IF NOT EXISTS MIME_TYPE VARCHAR(100);
                ALTER TABLE MESSAGES ADD COLUMN IF NOT EXISTS FILE_CONTENT BYTEA;
                ALTER TABLE MESSAGES ADD COLUMN IF NOT EXISTS REPLY_TO_ID INT NULL;
                ALTER TABLE MESSAGES ADD COLUMN IF NOT EXISTS IS_DELETED BOOLEAN NOT NULL DEFAULT FALSE;
                CREATE INDEX IF NOT EXISTS IDX_MESSAGES_COURSE_SENT_AT ON MESSAGES(COURSE_ID, SENT_AT DESC);
                CREATE INDEX IF NOT EXISTS IDX_MESSAGES_SENDER_ID ON MESSAGES(SENDER_ID);
                CREATE INDEX IF NOT EXISTS IDX_MESSAGES_COURSE_ID_ID ON MESSAGES(COURSE_ID, ID);
                CREATE INDEX IF NOT EXISTS IDX_MESSAGES_COURSE_SENDER_ID ON MESSAGES(COURSE_ID, SENDER_ID, ID) WHERE IS_DELETED = FALSE;
                CREATE TABLE IF NOT EXISTS CHAT_READS (
                    USER_ID INT NOT NULL,
                    COURSE_ID INT NOT NULL,
                    LAST_READ_MESSAGE_ID INT NULL,
                    UPDATED_AT TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (USER_ID, COURSE_ID),
                    FOREIGN KEY (USER_ID) REFERENCES USERS(ID) ON DELETE CASCADE,
                    FOREIGN KEY (COURSE_ID) REFERENCES COURSES(ID) ON DELETE CASCADE,
                    FOREIGN KEY (LAST_READ_MESSAGE_ID) REFERENCES MESSAGES(ID) ON DELETE SET NULL
                );";

            using var cmd = new NpgsqlCommand(sql, connection);
            cmd.ExecuteNonQuery();

            try
            {
                const string publicationSql = @"
                    DO $$
                    BEGIN
                        IF EXISTS (SELECT 1 FROM pg_publication WHERE pubname = 'supabase_realtime') THEN
                            IF NOT EXISTS (
                                SELECT 1 FROM pg_publication_tables 
                                WHERE pubname = 'supabase_realtime' AND tablename = 'messages'
                            ) THEN
                                ALTER PUBLICATION supabase_realtime ADD TABLE messages;
                            END IF;
                        END IF;
                    END $$;";
                using var pubCmd = new NpgsqlCommand(publicationSql, connection);
                pubCmd.ExecuteNonQuery();
            }
            catch
            {
                // Bỏ qua lỗi phân quyền trong môi trường DB bị hạn chế
            }
        }

        public List<ChatCourseModel> GetChatCoursesForUser(int userId)
        {
            var result = new List<ChatCourseModel>();
            using var connection = CreateConnection();
            connection.Open();
            EnsureChatSchema(connection);

            const string query = @"
                SELECT c.ID,
                       COALESCE(c.NAME, '') AS COURSE_NAME,
                       COALESCE(c.CLASS_CODE, '') AS CLASS_CODE,
                       (c.TEACHER_ID = @user_id) AS IS_TEACHER,
                       (
                           SELECT COUNT(*)::int
                           FROM MESSAGES m
                           LEFT JOIN CHAT_READS cr ON cr.USER_ID = @user_id AND cr.COURSE_ID = m.COURSE_ID
                           WHERE m.COURSE_ID = c.ID
                             AND COALESCE(m.IS_DELETED, FALSE) = FALSE
                             AND m.SENDER_ID <> @user_id
                             AND (cr.LAST_READ_MESSAGE_ID IS NULL OR m.ID > cr.LAST_READ_MESSAGE_ID)
                       ) AS UNREAD_COUNT
                FROM COURSES c
                LEFT JOIN ENROLLMENTS e ON e.COURSE_ID = c.ID AND e.STUDENT_ID = @user_id
                WHERE c.TEACHER_ID = @user_id
                   OR (e.STUDENT_ID IS NOT NULL AND UPPER(COALESCE(e.STATUS, 'ACTIVE')) IN ('ACTIVE', 'APPROVED'))
                ORDER BY c.ID DESC";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@user_id", userId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new ChatCourseModel
                {
                    CourseId = reader.GetInt32(0),
                    CourseName = reader.GetString(1),
                    ClassCode = reader.GetString(2),
                    IsTeacherCourse = reader.GetBoolean(3),
                    UnreadCount = reader.GetInt32(4)
                });
            }

            return result;
        }

        public async Task<List<ChatCourseModel>> GetChatCoursesForUserAsync(int userId, CancellationToken cancellationToken = default)
        {
            var result = new List<ChatCourseModel>();
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            EnsureChatSchema(connection);

            const string query = @"
                SELECT c.ID,
                       COALESCE(c.NAME, '') AS COURSE_NAME,
                       COALESCE(c.CLASS_CODE, '') AS CLASS_CODE,
                       (c.TEACHER_ID = @user_id) AS IS_TEACHER,
                       (
                           SELECT COUNT(*)::int
                           FROM MESSAGES m
                           LEFT JOIN CHAT_READS cr ON cr.USER_ID = @user_id AND cr.COURSE_ID = m.COURSE_ID
                           WHERE m.COURSE_ID = c.ID
                             AND COALESCE(m.IS_DELETED, FALSE) = FALSE
                             AND m.SENDER_ID <> @user_id
                             AND (cr.LAST_READ_MESSAGE_ID IS NULL OR m.ID > cr.LAST_READ_MESSAGE_ID)
                       ) AS UNREAD_COUNT
                FROM COURSES c
                LEFT JOIN ENROLLMENTS e ON e.COURSE_ID = c.ID AND e.STUDENT_ID = @user_id
                WHERE c.TEACHER_ID = @user_id
                   OR (e.STUDENT_ID IS NOT NULL AND UPPER(COALESCE(e.STATUS, 'ACTIVE')) IN ('ACTIVE', 'APPROVED'))
                ORDER BY c.ID DESC";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@user_id", userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new ChatCourseModel
                {
                    CourseId = reader.GetInt32(0),
                    CourseName = reader.GetString(1),
                    ClassCode = reader.GetString(2),
                    IsTeacherCourse = reader.GetBoolean(3),
                    UnreadCount = reader.GetInt32(4)
                });
            }

            return result;
        }

        public int GetUnreadChatCount(int userId)
        {
            if (userId <= 0)
            {
                return 0;
            }

            using var connection = CreateConnection();
            connection.Open();
            EnsureChatSchema(connection);

            const string query = @"
                SELECT COUNT(*)
                FROM MESSAGES m
                JOIN COURSES c ON c.ID = m.COURSE_ID
                LEFT JOIN CHAT_READS cr ON cr.USER_ID = @user_id AND cr.COURSE_ID = m.COURSE_ID
                WHERE COALESCE(m.IS_DELETED, FALSE) = FALSE
                  AND m.SENDER_ID <> @user_id
                  AND (
                      c.TEACHER_ID = @user_id
                      OR EXISTS (
                          SELECT 1
                          FROM ENROLLMENTS e
                          WHERE e.COURSE_ID = c.ID
                            AND e.STUDENT_ID = @user_id
                            AND UPPER(COALESCE(e.STATUS, 'ACTIVE')) IN ('ACTIVE', 'APPROVED')
                      )
                  )
                  AND (cr.LAST_READ_MESSAGE_ID IS NULL OR m.ID > cr.LAST_READ_MESSAGE_ID)";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@user_id", userId);
            object? result = command.ExecuteScalar();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        public void MarkAllChatRead(int userId)
        {
            if (userId <= 0)
            {
                return;
            }

            using var connection = CreateConnection();
            connection.Open();
            EnsureChatSchema(connection);

            const string query = @"
                INSERT INTO CHAT_READS (USER_ID, COURSE_ID, LAST_READ_MESSAGE_ID, UPDATED_AT)
                SELECT @user_id,
                       c.ID,
                       MAX(m.ID) AS LAST_READ_MESSAGE_ID,
                       CURRENT_TIMESTAMP
                FROM COURSES c
                JOIN MESSAGES m ON m.COURSE_ID = c.ID
                WHERE COALESCE(m.IS_DELETED, FALSE) = FALSE
                  AND (
                      c.TEACHER_ID = @user_id
                      OR EXISTS (
                          SELECT 1
                          FROM ENROLLMENTS e
                          WHERE e.COURSE_ID = c.ID
                            AND e.STUDENT_ID = @user_id
                            AND UPPER(COALESCE(e.STATUS, 'ACTIVE')) IN ('ACTIVE', 'APPROVED')
                      )
                  )
                GROUP BY c.ID
                ON CONFLICT (USER_ID, COURSE_ID) DO UPDATE
                SET LAST_READ_MESSAGE_ID = EXCLUDED.LAST_READ_MESSAGE_ID,
                    UPDATED_AT = CURRENT_TIMESTAMP
                WHERE CHAT_READS.LAST_READ_MESSAGE_ID IS NULL
                   OR EXCLUDED.LAST_READ_MESSAGE_ID > CHAT_READS.LAST_READ_MESSAGE_ID";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@user_id", userId);
            command.ExecuteNonQuery();
        }

        public void MarkCourseChatRead(int userId, int courseId)
        {
            if (userId <= 0 || courseId <= 0)
            {
                return;
            }

            using var connection = CreateConnection();
            connection.Open();
            EnsureChatSchema(connection);

            const string query = @"
                INSERT INTO CHAT_READS (USER_ID, COURSE_ID, LAST_READ_MESSAGE_ID, UPDATED_AT)
                SELECT @user_id,
                       @course_id,
                       MAX(m.ID) AS LAST_READ_MESSAGE_ID,
                       CURRENT_TIMESTAMP
                FROM MESSAGES m
                JOIN COURSES c ON c.ID = m.COURSE_ID
                WHERE m.COURSE_ID = @course_id
                  AND COALESCE(m.IS_DELETED, FALSE) = FALSE
                  AND (
                      c.TEACHER_ID = @user_id
                      OR EXISTS (
                          SELECT 1
                          FROM ENROLLMENTS e
                          WHERE e.COURSE_ID = c.ID
                            AND e.STUDENT_ID = @user_id
                            AND UPPER(COALESCE(e.STATUS, 'ACTIVE')) IN ('ACTIVE', 'APPROVED')
                      )
                  )
                GROUP BY c.ID
                HAVING MAX(m.ID) IS NOT NULL
                ON CONFLICT (USER_ID, COURSE_ID) DO UPDATE
                SET LAST_READ_MESSAGE_ID = EXCLUDED.LAST_READ_MESSAGE_ID,
                    UPDATED_AT = CURRENT_TIMESTAMP
                WHERE CHAT_READS.LAST_READ_MESSAGE_ID IS NULL
                   OR EXCLUDED.LAST_READ_MESSAGE_ID > CHAT_READS.LAST_READ_MESSAGE_ID";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@user_id", userId);
            command.Parameters.AddWithValue("@course_id", courseId);
            command.ExecuteNonQuery();
        }

        public bool CanAccessCourseChat(int userId, int courseId)
        {
            using var connection = CreateConnection();
            connection.Open();
            EnsureChatSchema(connection);

            const string query = @"
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM COURSES c
                    LEFT JOIN ENROLLMENTS e ON e.COURSE_ID = c.ID AND e.STUDENT_ID = @user_id
                    WHERE c.ID = @course_id
                      AND (
                          c.TEACHER_ID = @user_id
                          OR (e.STUDENT_ID IS NOT NULL AND UPPER(COALESCE(e.STATUS, 'ACTIVE')) IN ('ACTIVE', 'APPROVED'))
                      )
                ) THEN 1 ELSE 0 END";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@user_id", userId);
            command.Parameters.AddWithValue("@course_id", courseId);
            return Convert.ToInt32(command.ExecuteScalar()) == 1;
        }

        public int SendChatMessage(int courseId, int senderId, string content)
        {
            using var connection = CreateConnection();
            connection.Open();
            EnsureChatSchema(connection);

            const string query = @"
                INSERT INTO MESSAGES (COURSE_ID, SENDER_ID, CONTENT, MESSAGE_TYPE, SENT_AT)
                VALUES (@course_id, @sender_id, @content, 'TEXT', CURRENT_TIMESTAMP)
                RETURNING ID";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@course_id", courseId);
            command.Parameters.AddWithValue("@sender_id", senderId);
            command.Parameters.AddWithValue("@content", content);
            object? result = command.ExecuteScalar();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        public int SendChatFileMessage(int courseId, int senderId, string content, string fileUrl, string fileName, long fileSize, string mimeType, byte[]? fileContent = null)
        {
            using var connection = CreateConnection();
            connection.Open();
            EnsureChatSchema(connection);

            const string query = @"
                INSERT INTO MESSAGES (COURSE_ID, SENDER_ID, CONTENT, MESSAGE_TYPE, FILE_URL, FILE_NAME, FILE_SIZE, MIME_TYPE, FILE_CONTENT, SENT_AT)
                VALUES (@course_id, @sender_id, @content, 'FILE', @file_url, @file_name, @file_size, @mime_type, @file_content, CURRENT_TIMESTAMP)
                RETURNING ID";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@course_id", courseId);
            command.Parameters.AddWithValue("@sender_id", senderId);
            command.Parameters.AddWithValue("@content", content ?? string.Empty);
            command.Parameters.AddWithValue("@file_url", fileUrl ?? string.Empty);
            command.Parameters.AddWithValue("@file_name", fileName ?? string.Empty);
            command.Parameters.AddWithValue("@file_size", fileSize);
            command.Parameters.AddWithValue("@mime_type", mimeType ?? string.Empty);
            command.Parameters.Add("@file_content", NpgsqlDbType.Bytea).Value = (object?)fileContent ?? DBNull.Value;
            object? result = command.ExecuteScalar();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        public async Task<ChatMessageModel?> SendChatMessageAsync(int courseId, int senderId, string content, CancellationToken cancellationToken = default)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            EnsureChatSchema(connection);

            const string query = @"
                WITH inserted AS (
                    INSERT INTO MESSAGES (COURSE_ID, SENDER_ID, CONTENT, MESSAGE_TYPE, SENT_AT)
                    VALUES (@course_id, @sender_id, @content, 'TEXT', CURRENT_TIMESTAMP)
                    RETURNING ID, COURSE_ID, SENDER_ID, CONTENT, MESSAGE_TYPE, FILE_URL, FILE_NAME, FILE_SIZE, MIME_TYPE, POLL_ID, SENT_AT
                )
                SELECT i.ID,
                       i.COURSE_ID,
                       i.SENDER_ID,
                       COALESCE(u.FULL_NAME, u.USERNAME, 'Unknown') AS SENDER_NAME,
                       COALESCE(r.NAME, '') AS SENDER_ROLE,
                       COALESCE(i.CONTENT, '') AS CONTENT,
                       COALESCE(i.MESSAGE_TYPE, 'TEXT') AS MESSAGE_TYPE,
                       COALESCE(i.FILE_URL, '') AS FILE_URL,
                       COALESCE(i.FILE_NAME, '') AS FILE_NAME,
                       COALESCE(i.FILE_SIZE, 0) AS FILE_SIZE,
                       COALESCE(i.MIME_TYPE, '') AS MIME_TYPE,
                       i.POLL_ID,
                       COALESCE(tp.avatar_path, sp.avatar_path, '') AS SENDER_AVATAR,
                       i.SENT_AT
                FROM inserted i
                JOIN USERS u ON u.ID = i.SENDER_ID
                LEFT JOIN ROLES r ON r.ID = u.ROLE_ID
                LEFT JOIN student_profiles sp ON sp.user_id = u.ID
                LEFT JOIN teacher_profiles tp ON tp.user_id = u.ID";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@course_id", courseId);
            command.Parameters.AddWithValue("@sender_id", senderId);
            command.Parameters.AddWithValue("@content", content);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? ReadChatMessage(reader) : null;
        }

        public async Task<ChatMessageModel?> SendChatFileMessageAsync(
            int courseId,
            int senderId,
            string content,
            string fileUrl,
            string fileName,
            long fileSize,
            string mimeType,
            CancellationToken cancellationToken = default)
        {
            return await SendChatFileMessageAsync(
                courseId,
                senderId,
                content,
                fileUrl,
                fileName,
                fileSize,
                mimeType,
                "FILE",
                null,
                cancellationToken);
        }

        public async Task<ChatMessageModel?> SendChatFileMessageAsync(
            int courseId,
            int senderId,
            string content,
            string fileUrl,
            string fileName,
            long fileSize,
            string mimeType,
            byte[]? fileContent,
            CancellationToken cancellationToken = default)
        {
            return await SendChatFileMessageAsync(
                courseId,
                senderId,
                content,
                fileUrl,
                fileName,
                fileSize,
                mimeType,
                "FILE",
                fileContent,
                cancellationToken);
        }

        public async Task<ChatMessageModel?> SendChatImageGroupMessageAsync(
            int courseId,
            int senderId,
            string content,
            string attachmentJson,
            string fileName,
            long totalFileSize,
            string mimeType,
            CancellationToken cancellationToken = default)
        {
            return await SendChatFileMessageAsync(
                courseId,
                senderId,
                content,
                attachmentJson,
                fileName,
                totalFileSize,
                mimeType,
                ChatMessageModel.ImageGroupMessageType,
                null,
                cancellationToken);
        }

        private async Task<ChatMessageModel?> SendChatFileMessageAsync(
            int courseId,
            int senderId,
            string content,
            string fileUrl,
            string fileName,
            long fileSize,
            string mimeType,
            string messageType,
            byte[]? fileContent,
            CancellationToken cancellationToken)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            EnsureChatSchema(connection);

            const string query = @"
                WITH inserted AS (
                    INSERT INTO MESSAGES (COURSE_ID, SENDER_ID, CONTENT, MESSAGE_TYPE, FILE_URL, FILE_NAME, FILE_SIZE, MIME_TYPE, FILE_CONTENT, SENT_AT)
                    VALUES (@course_id, @sender_id, @content, @message_type, @file_url, @file_name, @file_size, @mime_type, @file_content, CURRENT_TIMESTAMP)
                    RETURNING ID, COURSE_ID, SENDER_ID, CONTENT, MESSAGE_TYPE, FILE_URL, FILE_NAME, FILE_SIZE, MIME_TYPE, POLL_ID, SENT_AT
                )
                SELECT i.ID,
                       i.COURSE_ID,
                       i.SENDER_ID,
                       COALESCE(u.FULL_NAME, u.USERNAME, 'Unknown') AS SENDER_NAME,
                       COALESCE(r.NAME, '') AS SENDER_ROLE,
                       COALESCE(i.CONTENT, '') AS CONTENT,
                       COALESCE(i.MESSAGE_TYPE, 'TEXT') AS MESSAGE_TYPE,
                       COALESCE(i.FILE_URL, '') AS FILE_URL,
                       COALESCE(i.FILE_NAME, '') AS FILE_NAME,
                       COALESCE(i.FILE_SIZE, 0) AS FILE_SIZE,
                       COALESCE(i.MIME_TYPE, '') AS MIME_TYPE,
                       i.POLL_ID,
                       COALESCE(tp.avatar_path, sp.avatar_path, '') AS SENDER_AVATAR,
                       i.SENT_AT
                FROM inserted i
                JOIN USERS u ON u.ID = i.SENDER_ID
                LEFT JOIN ROLES r ON r.ID = u.ROLE_ID
                LEFT JOIN student_profiles sp ON sp.user_id = u.ID
                LEFT JOIN teacher_profiles tp ON tp.user_id = u.ID";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@course_id", courseId);
            command.Parameters.AddWithValue("@sender_id", senderId);
            command.Parameters.AddWithValue("@content", content ?? string.Empty);
            command.Parameters.AddWithValue("@message_type", string.IsNullOrWhiteSpace(messageType) ? "FILE" : messageType);
            command.Parameters.AddWithValue("@file_url", fileUrl ?? string.Empty);
            command.Parameters.AddWithValue("@file_name", fileName ?? string.Empty);
            command.Parameters.AddWithValue("@file_size", fileSize);
            command.Parameters.AddWithValue("@mime_type", mimeType ?? string.Empty);
            command.Parameters.Add("@file_content", NpgsqlDbType.Bytea).Value = (object?)fileContent ?? DBNull.Value;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? ReadChatMessage(reader) : null;
        }

        public async Task<byte[]?> GetChatFileContentAsync(int courseId, int messageId, CancellationToken cancellationToken = default)
        {
            if (courseId <= 0 || messageId <= 0)
            {
                return null;
            }

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            EnsureChatSchema(connection);

            const string query = @"
                SELECT FILE_CONTENT
                FROM MESSAGES
                WHERE ID = @message_id
                  AND COURSE_ID = @course_id
                  AND UPPER(COALESCE(MESSAGE_TYPE, '')) = 'FILE'
                  AND COALESCE(IS_DELETED, FALSE) = FALSE";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@message_id", messageId);
            command.Parameters.AddWithValue("@course_id", courseId);
            object? result = await command.ExecuteScalarAsync(cancellationToken);
            return result == null || result == DBNull.Value ? null : (byte[])result;
        }

        public List<ChatMessageModel> GetChatMessages(int courseId, int limit = 100)
        {
            var result = new List<ChatMessageModel>();
            using var connection = CreateConnection();
            connection.Open();
            EnsureChatSchema(connection);
            EnsurePollSchema(connection);

            const string query = @"
                SELECT m.ID,
                       m.COURSE_ID,
                       m.SENDER_ID,
                       COALESCE(u.FULL_NAME, u.USERNAME, 'Unknown') AS SENDER_NAME,
                       COALESCE(r.NAME, '') AS SENDER_ROLE,
                       COALESCE(m.CONTENT, '') AS CONTENT,
                       COALESCE(m.MESSAGE_TYPE, 'TEXT') AS MESSAGE_TYPE,
                       COALESCE(m.FILE_URL, '') AS FILE_URL,
                       COALESCE(m.FILE_NAME, '') AS FILE_NAME,
                       COALESCE(m.FILE_SIZE, 0) AS FILE_SIZE,
                       COALESCE(m.MIME_TYPE, '') AS MIME_TYPE,
                       m.POLL_ID,
                       COALESCE(tp.avatar_path, sp.avatar_path, '') AS SENDER_AVATAR,
                       m.SENT_AT,
                       m.FILE_CONTENT
                FROM MESSAGES m
                JOIN USERS u ON u.ID = m.SENDER_ID
                LEFT JOIN ROLES r ON r.ID = u.ROLE_ID
                LEFT JOIN student_profiles sp ON sp.user_id = u.ID
                LEFT JOIN teacher_profiles tp ON tp.user_id = u.ID
                WHERE m.COURSE_ID = @course_id
                  AND COALESCE(m.IS_DELETED, FALSE) = FALSE
                ORDER BY m.SENT_AT ASC
                LIMIT @limit";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@course_id", courseId);
            command.Parameters.AddWithValue("@limit", limit <= 0 ? 100 : limit);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(ReadChatMessage(reader));
            }

            AttachPollData(connection, result, currentUserId: 0);
            return result;
        }

        public async Task<List<ChatMessageModel>> GetChatMessagesAsync(int courseId, int limit = 100, CancellationToken cancellationToken = default)
        {
            var result = new List<ChatMessageModel>();
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            EnsureChatSchema(connection);
            EnsurePollSchema(connection);

            const string query = @"
                SELECT m.ID,
                       m.COURSE_ID,
                       m.SENDER_ID,
                       COALESCE(u.FULL_NAME, u.USERNAME, 'Unknown') AS SENDER_NAME,
                       COALESCE(r.NAME, '') AS SENDER_ROLE,
                       COALESCE(m.CONTENT, '') AS CONTENT,
                       COALESCE(m.MESSAGE_TYPE, 'TEXT') AS MESSAGE_TYPE,
                       COALESCE(m.FILE_URL, '') AS FILE_URL,
                       COALESCE(m.FILE_NAME, '') AS FILE_NAME,
                       COALESCE(m.FILE_SIZE, 0) AS FILE_SIZE,
                       COALESCE(m.MIME_TYPE, '') AS MIME_TYPE,
                       m.POLL_ID,
                       COALESCE(tp.avatar_path, sp.avatar_path, '') AS SENDER_AVATAR,
                       m.SENT_AT,
                       m.FILE_CONTENT
                FROM MESSAGES m
                JOIN USERS u ON u.ID = m.SENDER_ID
                LEFT JOIN ROLES r ON r.ID = u.ROLE_ID
                LEFT JOIN student_profiles sp ON sp.user_id = u.ID
                LEFT JOIN teacher_profiles tp ON tp.user_id = u.ID
                WHERE m.COURSE_ID = @course_id
                  AND COALESCE(m.IS_DELETED, FALSE) = FALSE
                ORDER BY m.SENT_AT ASC
                LIMIT @limit";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@course_id", courseId);
            command.Parameters.AddWithValue("@limit", limit <= 0 ? 100 : limit);
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    result.Add(ReadChatMessage(reader));
                }
            }

            await AttachPollDataAsync(connection, result, currentUserId: 0, cancellationToken);
            return result;
        }

        public async Task<List<ChatMessageModel>> GetChatMessagesForUserAsync(int userId, int courseId, int limit = 100, CancellationToken cancellationToken = default)
        {
            var messages = await GetChatMessagesAsync(courseId, limit, cancellationToken);
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            EnsurePollSchema(connection);
            await AttachPollDataAsync(connection, messages, userId, cancellationToken);
            return messages;
        }

        public async Task<List<ChatMessageModel>> GetChatMessagesBeforeAsync(int courseId, int beforeMessageId, int limit = 20, CancellationToken cancellationToken = default)
        {
            var result = new List<ChatMessageModel>();
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            EnsureChatSchema(connection);
            EnsurePollSchema(connection);

            const string query = @"
                SELECT * FROM (
                    SELECT m.ID,
                           m.COURSE_ID,
                           m.SENDER_ID,
                           COALESCE(u.FULL_NAME, u.USERNAME, 'Unknown') AS SENDER_NAME,
                           COALESCE(r.NAME, '') AS SENDER_ROLE,
                           COALESCE(m.CONTENT, '') AS CONTENT,
                           COALESCE(m.MESSAGE_TYPE, 'TEXT') AS MESSAGE_TYPE,
                           COALESCE(m.FILE_URL, '') AS FILE_URL,
                           COALESCE(m.FILE_NAME, '') AS FILE_NAME,
                           COALESCE(m.FILE_SIZE, 0) AS FILE_SIZE,
                           COALESCE(m.MIME_TYPE, '') AS MIME_TYPE,
                           m.POLL_ID,
                           COALESCE(tp.avatar_path, sp.avatar_path, '') AS SENDER_AVATAR,
                           m.SENT_AT,
                           m.FILE_CONTENT
                    FROM MESSAGES m
                    JOIN USERS u ON u.ID = m.SENDER_ID
                    LEFT JOIN ROLES r ON r.ID = u.ROLE_ID
                    LEFT JOIN student_profiles sp ON sp.user_id = u.ID
                    LEFT JOIN teacher_profiles tp ON tp.user_id = u.ID
                    WHERE m.COURSE_ID = @course_id
                      AND m.ID < @before_message_id
                      AND COALESCE(m.IS_DELETED, FALSE) = FALSE
                    ORDER BY m.ID DESC
                    LIMIT @limit
                ) older_messages
                ORDER BY ID ASC";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@course_id", courseId);
            command.Parameters.AddWithValue("@before_message_id", beforeMessageId);
            command.Parameters.AddWithValue("@limit", limit <= 0 ? 20 : limit);
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    result.Add(ReadChatMessage(reader));
                }
            }

            await AttachPollDataAsync(connection, result, currentUserId: 0, cancellationToken);
            return result;
        }

        public async Task<List<ChatMessageModel>> GetChatMessagesBeforeForUserAsync(int userId, int courseId, int beforeMessageId, int limit = 20, CancellationToken cancellationToken = default)
        {
            var messages = await GetChatMessagesBeforeAsync(courseId, beforeMessageId, limit, cancellationToken);
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            EnsurePollSchema(connection);
            await AttachPollDataAsync(connection, messages, userId, cancellationToken);
            return messages;
        }

        public async Task<List<ChatMessageModel>> GetChatMessagesAfterAsync(int courseId, int afterMessageId, int limit = 50, CancellationToken cancellationToken = default)
        {
            var result = new List<ChatMessageModel>();
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            EnsureChatSchema(connection);
            EnsurePollSchema(connection);

            const string query = @"
                SELECT m.ID,
                       m.COURSE_ID,
                       m.SENDER_ID,
                       COALESCE(u.FULL_NAME, u.USERNAME, 'Unknown') AS SENDER_NAME,
                       COALESCE(r.NAME, '') AS SENDER_ROLE,
                       COALESCE(m.CONTENT, '') AS CONTENT,
                       COALESCE(m.MESSAGE_TYPE, 'TEXT') AS MESSAGE_TYPE,
                       COALESCE(m.FILE_URL, '') AS FILE_URL,
                       COALESCE(m.FILE_NAME, '') AS FILE_NAME,
                       COALESCE(m.FILE_SIZE, 0) AS FILE_SIZE,
                       COALESCE(m.MIME_TYPE, '') AS MIME_TYPE,
                       m.POLL_ID,
                       COALESCE(tp.avatar_path, sp.avatar_path, '') AS SENDER_AVATAR,
                       m.SENT_AT,
                       m.FILE_CONTENT
                FROM MESSAGES m
                JOIN USERS u ON u.ID = m.SENDER_ID
                LEFT JOIN ROLES r ON r.ID = u.ROLE_ID
                LEFT JOIN student_profiles sp ON sp.user_id = u.ID
                LEFT JOIN teacher_profiles tp ON tp.user_id = u.ID
                WHERE m.COURSE_ID = @course_id
                  AND m.ID > @after_message_id
                  AND COALESCE(m.IS_DELETED, FALSE) = FALSE
                ORDER BY m.ID ASC
                LIMIT @limit";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@course_id", courseId);
            command.Parameters.AddWithValue("@after_message_id", afterMessageId);
            command.Parameters.AddWithValue("@limit", limit <= 0 ? 50 : limit);
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    result.Add(ReadChatMessage(reader));
                }
            }

            await AttachPollDataAsync(connection, result, currentUserId: 0, cancellationToken);
            return result;
        }

        public async Task<List<ChatMessageModel>> GetChatMessagesAfterForUserAsync(int userId, int courseId, int afterMessageId, int limit = 50, CancellationToken cancellationToken = default)
        {
            var messages = await GetChatMessagesAfterAsync(courseId, afterMessageId, limit, cancellationToken);
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            EnsurePollSchema(connection);
            await AttachPollDataAsync(connection, messages, userId, cancellationToken);
            return messages;
        }

        private static ChatMessageModel ReadChatMessage(NpgsqlDataReader reader)
        {
            var message = new ChatMessageModel
            {
                Id = reader.GetInt32(0),
                CourseId = reader.GetInt32(1),
                SenderId = reader.GetInt32(2),
                SenderName = reader.GetString(3),
                SenderRole = reader.GetString(4),
                Content = reader.GetString(5),
                MessageType = reader.GetString(6),
                FileUrl = reader.GetString(7),
                FileName = reader.GetString(8),
                FileSize = reader.GetInt64(9),
                MimeType = reader.GetString(10),
                PollId = reader.IsDBNull(11) ? null : reader.GetInt32(11),
                SenderAvatar = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                SentAt = reader.GetDateTime(13)
            };

            byte[]? storedFileContent = reader.FieldCount > 14 && !reader.IsDBNull(14)
                ? (byte[])reader.GetValue(14)
                : null;
            HydrateStoredChatImage(message, storedFileContent);

            if (CourseGuard.Frontend.Helpers.ChatImageHelper.TryParseImageAttachments(message.FileUrl, out var attachments))
            {
                message.ImageAttachments = attachments;
            }

            return message;
        }

        private static void HydrateStoredChatImage(ChatMessageModel message, byte[]? content)
        {
            if (message == null || content == null || content.Length == 0)
                return;

            if (CourseGuard.Frontend.Helpers.ChatImageHelper.IsImageGroupMessage(message.MessageType, message.MimeType, message.FileUrl))
                return;

            if (!CourseGuard.Frontend.Helpers.ChatImageHelper.IsImageMessage(message.MessageType, message.MimeType, message.FileName, message.FileUrl))
                return;

            string resolvedPath = CourseGuard.Frontend.Helpers.ChatImageHelper.ResolveLocalFilePath(message.FileUrl);
            if (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath))
            {
                message.FileUrl = resolvedPath;
                return;
            }

            try
            {
                string cachePath = BuildChatImageCachePath(message);
                if (!File.Exists(cachePath) || new FileInfo(cachePath).Length != content.Length)
                {
                    File.WriteAllBytes(cachePath, content);
                }

                message.FileUrl = cachePath;
            }
            catch
            {
                // Keep the original path so existing download/error behavior remains unchanged.
            }
        }

        private static string BuildChatImageCachePath(ChatMessageModel message)
        {
            string cacheDirectory = Path.Combine(Path.GetTempPath(), "CourseGuard", "chat-image-cache", $"course-{message.CourseId}");
            Directory.CreateDirectory(cacheDirectory);

            string baseName = Path.GetFileNameWithoutExtension(message.FileName);
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = $"chat-image-{message.Id}";

            baseName = SanitizeFileName(baseName);
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = $"chat-image-{message.Id}";

            return Path.Combine(cacheDirectory, $"{message.Id}_{baseName}{ResolveChatImageExtension(message)}");
        }

        private static string ResolveChatImageExtension(ChatMessageModel message)
        {
            if (string.Equals(message.MimeType, "image/png", StringComparison.OrdinalIgnoreCase))
                return ".png";

            if (string.Equals(message.MimeType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(message.MimeType, "image/jpg", StringComparison.OrdinalIgnoreCase))
                return ".jpg";

            string extension = Path.GetExtension(message.FileName);
            if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
                return ".png";

            return ".jpg";
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalidChar, '_');
            }

            return value.Trim();
        }

        public async Task<int> CreatePollMessageAsync(int teacherId, int courseId, string question, IReadOnlyList<string> options, CancellationToken cancellationToken = default)
        {
            if (teacherId <= 0 || courseId <= 0 || string.IsNullOrWhiteSpace(question) || options == null)
            {
                return 0;
            }

            var cleanedOptions = options
                .Where(option => !string.IsNullOrWhiteSpace(option))
                .Select(option => option.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToList();

            if (cleanedOptions.Count < 2 || !CanCreateCoursePoll(teacherId, courseId))
            {
                return 0;
            }

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            EnsureChatSchema(connection);
            EnsurePollSchema(connection);

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                const string insertMessageSql = @"
                    INSERT INTO MESSAGES (COURSE_ID, SENDER_ID, CONTENT, MESSAGE_TYPE, SENT_AT)
                    VALUES (@course_id, @sender_id, @content, 'POLL', CURRENT_TIMESTAMP)
                    RETURNING ID";

                int messageId;
                await using (var messageCommand = new NpgsqlCommand(insertMessageSql, connection, transaction))
                {
                    messageCommand.Parameters.AddWithValue("@course_id", courseId);
                    messageCommand.Parameters.AddWithValue("@sender_id", teacherId);
                    messageCommand.Parameters.AddWithValue("@content", question.Trim());
                    object? messageResult = await messageCommand.ExecuteScalarAsync(cancellationToken);
                    messageId = messageResult == null || messageResult == DBNull.Value ? 0 : Convert.ToInt32(messageResult);
                }

                if (messageId <= 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return 0;
                }

                const string insertPollSql = @"
                    INSERT INTO POLLS (COURSE_ID, MESSAGE_ID, CREATED_BY, QUESTION, IS_CLOSED, CREATED_AT)
                    VALUES (@course_id, @message_id, @created_by, @question, FALSE, CURRENT_TIMESTAMP)
                    RETURNING ID";

                int pollId;
                await using (var pollCommand = new NpgsqlCommand(insertPollSql, connection, transaction))
                {
                    pollCommand.Parameters.AddWithValue("@course_id", courseId);
                    pollCommand.Parameters.AddWithValue("@message_id", messageId);
                    pollCommand.Parameters.AddWithValue("@created_by", teacherId);
                    pollCommand.Parameters.AddWithValue("@question", question.Trim());
                    object? pollResult = await pollCommand.ExecuteScalarAsync(cancellationToken);
                    pollId = pollResult == null || pollResult == DBNull.Value ? 0 : Convert.ToInt32(pollResult);
                }

                if (pollId <= 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return 0;
                }

                const string insertOptionSql = @"
                    INSERT INTO POLL_OPTIONS (POLL_ID, OPTION_TEXT, SORT_ORDER, CREATED_AT)
                    VALUES (@poll_id, @option_text, @sort_order, CURRENT_TIMESTAMP)";

                for (int i = 0; i < cleanedOptions.Count; i++)
                {
                    await using var optionCommand = new NpgsqlCommand(insertOptionSql, connection, transaction);
                    optionCommand.Parameters.AddWithValue("@poll_id", pollId);
                    optionCommand.Parameters.AddWithValue("@option_text", cleanedOptions[i]);
                    optionCommand.Parameters.AddWithValue("@sort_order", i);
                    await optionCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                return messageId;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public bool CanCreateCoursePoll(int teacherId, int courseId)
        {
            using var connection = CreateConnection();
            connection.Open();

            const string query = @"
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM COURSES c
                    JOIN USERS u ON u.ID = @teacher_id
                    LEFT JOIN ROLES r ON r.ID = u.ROLE_ID
                    WHERE c.ID = @course_id
                      AND c.TEACHER_ID = @teacher_id
                      AND UPPER(COALESCE(r.NAME, '')) = 'TEACHER'
                      AND UPPER(COALESCE(c.STATUS, 'ACTIVE')) = 'ACTIVE'
                ) THEN 1 ELSE 0 END";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@course_id", courseId);
            return Convert.ToInt32(command.ExecuteScalar()) == 1;
        }

        public async Task<bool> VotePollAsync(int userId, int pollId, int optionId, CancellationToken cancellationToken = default)
        {
            if (userId <= 0 || pollId <= 0 || optionId <= 0)
            {
                return false;
            }

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            EnsurePollSchema(connection);

            const string query = @"
                WITH poll_context AS (
                    SELECT p.ID AS POLL_ID, p.COURSE_ID
                    FROM POLLS p
                    WHERE p.ID = @poll_id
                      AND COALESCE(p.IS_CLOSED, FALSE) = FALSE
                ), authorized_user AS (
                    SELECT pc.POLL_ID
                    FROM poll_context pc
                    JOIN COURSES c ON c.ID = pc.COURSE_ID
                    LEFT JOIN ENROLLMENTS e ON e.COURSE_ID = pc.COURSE_ID AND e.STUDENT_ID = @user_id
                    WHERE c.TEACHER_ID = @user_id
                       OR (e.STUDENT_ID IS NOT NULL AND UPPER(COALESCE(e.STATUS, 'ACTIVE')) IN ('ACTIVE', 'APPROVED'))
                ), valid_option AS (
                    SELECT po.ID AS OPTION_ID, po.POLL_ID
                    FROM POLL_OPTIONS po
                    JOIN authorized_user au ON au.POLL_ID = po.POLL_ID
                    WHERE po.ID = @option_id
                )
                INSERT INTO POLL_VOTES (POLL_ID, USER_ID, OPTION_ID, VOTED_AT)
                SELECT POLL_ID, @user_id, OPTION_ID, CURRENT_TIMESTAMP
                FROM valid_option
                ON CONFLICT (POLL_ID, USER_ID) DO UPDATE
                SET OPTION_ID = EXCLUDED.OPTION_ID,
                    VOTED_AT = CURRENT_TIMESTAMP";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@user_id", userId);
            command.Parameters.AddWithValue("@poll_id", pollId);
            command.Parameters.AddWithValue("@option_id", optionId);
            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        public async Task<bool> ClosePollAsync(int teacherId, int pollId, CancellationToken cancellationToken = default)
        {
            if (teacherId <= 0 || pollId <= 0)
            {
                return false;
            }

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            EnsurePollSchema(connection);

            const string query = @"
                UPDATE POLLS p
                SET IS_CLOSED = TRUE,
                    CLOSED_AT = COALESCE(CLOSED_AT, CURRENT_TIMESTAMP)
                FROM COURSES c
                JOIN USERS u ON u.ID = @teacher_id
                LEFT JOIN ROLES r ON r.ID = u.ROLE_ID
                WHERE p.ID = @poll_id
                  AND c.ID = p.COURSE_ID
                  AND c.TEACHER_ID = @teacher_id
                  AND UPPER(COALESCE(r.NAME, '')) = 'TEACHER'
                  AND COALESCE(p.IS_CLOSED, FALSE) = FALSE";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@poll_id", pollId);
            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        public async Task<int> TryCreatePollBumpMessageAsync(int actorUserId, int pollId, string caption, CancellationToken cancellationToken = default)
        {
            if (actorUserId <= 0 || pollId <= 0 || string.IsNullOrWhiteSpace(caption))
            {
                return 0;
            }

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            EnsureChatSchema(connection);
            EnsurePollSchema(connection);

            const string query = @"
                WITH poll_context AS (
                    SELECT p.ID AS POLL_ID, p.COURSE_ID
                    FROM POLLS p
                    JOIN COURSES c ON c.ID = p.COURSE_ID
                    LEFT JOIN ENROLLMENTS e ON e.COURSE_ID = p.COURSE_ID AND e.STUDENT_ID = @actor_user_id
                    WHERE p.ID = @poll_id
                      AND (
                          c.TEACHER_ID = @actor_user_id
                          OR (e.STUDENT_ID IS NOT NULL AND UPPER(COALESCE(e.STATUS, 'ACTIVE')) IN ('ACTIVE', 'APPROVED'))
                      )
                ), cooldown AS (
                    SELECT CASE WHEN EXISTS (
                        SELECT 1
                        FROM MESSAGES m
                        WHERE m.POLL_ID = @poll_id
                          AND m.MESSAGE_TYPE = 'POLL_BUMP'
                          AND COALESCE(m.IS_DELETED, FALSE) = FALSE
                          AND m.SENT_AT >= CURRENT_TIMESTAMP - INTERVAL '30 seconds'
                    ) THEN 1 ELSE 0 END AS IS_COOLDOWN_ACTIVE
                )
                INSERT INTO MESSAGES (COURSE_ID, SENDER_ID, CONTENT, MESSAGE_TYPE, POLL_ID, SENT_AT)
                SELECT pc.COURSE_ID,
                       @actor_user_id,
                       @caption,
                       'POLL_BUMP',
                       pc.POLL_ID,
                       CURRENT_TIMESTAMP
                FROM poll_context pc
                CROSS JOIN cooldown cd
                WHERE cd.IS_COOLDOWN_ACTIVE = 0
                RETURNING ID";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@actor_user_id", actorUserId);
            command.Parameters.AddWithValue("@poll_id", pollId);
            command.Parameters.AddWithValue("@caption", caption.Trim());
            object? result = await command.ExecuteScalarAsync(cancellationToken);
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        public async Task<string> GetUserDisplayNameAsync(int userId, CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
            {
                return string.Empty;
            }

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            const string query = @"
                SELECT COALESCE(NULLIF(TRIM(FULL_NAME), ''), NULLIF(TRIM(USERNAME), ''), 'Người dùng')
                FROM USERS
                WHERE ID = @user_id";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@user_id", userId);
            object? result = await command.ExecuteScalarAsync(cancellationToken);
            return result == null || result == DBNull.Value ? string.Empty : Convert.ToString(result) ?? string.Empty;
        }

        private static void AttachPollData(NpgsqlConnection connection, List<ChatMessageModel> messages, int currentUserId)
        {
            var pollMessageIds = messages
                .Where(message => string.Equals(message.MessageType, "POLL", StringComparison.OrdinalIgnoreCase))
                .Select(message => message.Id)
                .Distinct()
                .ToArray();

            var bumpPollIds = messages
                .Where(message => string.Equals(message.MessageType, "POLL_BUMP", StringComparison.OrdinalIgnoreCase) && message.PollId.HasValue)
                .Select(message => message.PollId!.Value)
                .Distinct()
                .ToArray();

            if (pollMessageIds.Length == 0 && bumpPollIds.Length == 0)
            {
                return;
            }

            const string pollQuery = @"
                SELECT p.ID, p.COURSE_ID, p.MESSAGE_ID, p.CREATED_BY, p.QUESTION,
                       COALESCE(p.IS_CLOSED, FALSE) AS IS_CLOSED,
                       p.CREATED_AT, p.CLOSED_AT,
                       COALESCE(total_votes.TOTAL, 0) AS TOTAL_VOTES,
                       my_vote.OPTION_ID AS MY_OPTION_ID
                FROM POLLS p
                LEFT JOIN (
                    SELECT POLL_ID, COUNT(*) AS TOTAL
                    FROM POLL_VOTES
                    GROUP BY POLL_ID
                ) total_votes ON total_votes.POLL_ID = p.ID
                LEFT JOIN POLL_VOTES my_vote ON my_vote.POLL_ID = p.ID AND my_vote.USER_ID = @current_user_id
                WHERE p.MESSAGE_ID = ANY(@message_ids)
                   OR p.ID = ANY(@poll_ids)";

            var pollsByMessageId = new Dictionary<int, PollModel>();
            var pollsById = new Dictionary<int, PollModel>();
            using (var command = new NpgsqlCommand(pollQuery, connection))
            {
                command.Parameters.AddWithValue("@message_ids", pollMessageIds);
                command.Parameters.AddWithValue("@poll_ids", bumpPollIds);
                command.Parameters.AddWithValue("@current_user_id", currentUserId);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var poll = new PollModel
                    {
                        Id = reader.GetInt32(0),
                        CourseId = reader.GetInt32(1),
                        MessageId = reader.GetInt32(2),
                        CreatedBy = reader.GetInt32(3),
                        Question = reader.GetString(4),
                        IsClosed = reader.GetBoolean(5),
                        CreatedAt = reader.GetDateTime(6),
                        ClosedAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                        TotalVotes = reader.GetInt32(8),
                        MySelectedOptionId = reader.IsDBNull(9) ? null : reader.GetInt32(9)
                    };
                    pollsByMessageId[poll.MessageId] = poll;
                    pollsById[poll.Id] = poll;
                }
            }

            AttachPollOptions(connection, pollsById.Values.ToList());
            foreach (var message in messages)
            {
                if (string.Equals(message.MessageType, "POLL", StringComparison.OrdinalIgnoreCase)
                    && pollsByMessageId.TryGetValue(message.Id, out PollModel? pollByMessage))
                {
                    message.PollId = pollByMessage.Id;
                    message.Poll = pollByMessage;
                    continue;
                }

                if (string.Equals(message.MessageType, "POLL_BUMP", StringComparison.OrdinalIgnoreCase)
                    && message.PollId.HasValue
                    && pollsById.TryGetValue(message.PollId.Value, out PollModel? pollById))
                {
                    message.Poll = pollById;
                }
            }
        }

        private static async Task AttachPollDataAsync(NpgsqlConnection connection, List<ChatMessageModel> messages, int currentUserId, CancellationToken cancellationToken)
        {
            var pollMessageIds = messages
                .Where(message => string.Equals(message.MessageType, "POLL", StringComparison.OrdinalIgnoreCase))
                .Select(message => message.Id)
                .Distinct()
                .ToArray();

            var bumpPollIds = messages
                .Where(message => string.Equals(message.MessageType, "POLL_BUMP", StringComparison.OrdinalIgnoreCase) && message.PollId.HasValue)
                .Select(message => message.PollId!.Value)
                .Distinct()
                .ToArray();

            if (pollMessageIds.Length == 0 && bumpPollIds.Length == 0)
            {
                return;
            }

            const string pollQuery = @"
                SELECT p.ID, p.COURSE_ID, p.MESSAGE_ID, p.CREATED_BY, p.QUESTION,
                       COALESCE(p.IS_CLOSED, FALSE) AS IS_CLOSED,
                       p.CREATED_AT, p.CLOSED_AT,
                       COALESCE(total_votes.TOTAL, 0) AS TOTAL_VOTES,
                       my_vote.OPTION_ID AS MY_OPTION_ID
                FROM POLLS p
                LEFT JOIN (
                    SELECT POLL_ID, COUNT(*) AS TOTAL
                    FROM POLL_VOTES
                    GROUP BY POLL_ID
                ) total_votes ON total_votes.POLL_ID = p.ID
                LEFT JOIN POLL_VOTES my_vote ON my_vote.POLL_ID = p.ID AND my_vote.USER_ID = @current_user_id
                WHERE p.MESSAGE_ID = ANY(@message_ids)
                   OR p.ID = ANY(@poll_ids)";

            var pollsByMessageId = new Dictionary<int, PollModel>();
            var pollsById = new Dictionary<int, PollModel>();
            await using (var command = new NpgsqlCommand(pollQuery, connection))
            {
                command.Parameters.AddWithValue("@message_ids", pollMessageIds);
                command.Parameters.AddWithValue("@poll_ids", bumpPollIds);
                command.Parameters.AddWithValue("@current_user_id", currentUserId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var poll = new PollModel
                    {
                        Id = reader.GetInt32(0),
                        CourseId = reader.GetInt32(1),
                        MessageId = reader.GetInt32(2),
                        CreatedBy = reader.GetInt32(3),
                        Question = reader.GetString(4),
                        IsClosed = reader.GetBoolean(5),
                        CreatedAt = reader.GetDateTime(6),
                        ClosedAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                        TotalVotes = reader.GetInt32(8),
                        MySelectedOptionId = reader.IsDBNull(9) ? null : reader.GetInt32(9)
                    };
                    pollsByMessageId[poll.MessageId] = poll;
                    pollsById[poll.Id] = poll;
                }
            }

            await AttachPollOptionsAsync(connection, pollsById.Values.ToList(), cancellationToken);
            foreach (var message in messages)
            {
                if (string.Equals(message.MessageType, "POLL", StringComparison.OrdinalIgnoreCase)
                    && pollsByMessageId.TryGetValue(message.Id, out PollModel? pollByMessage))
                {
                    message.PollId = pollByMessage.Id;
                    message.Poll = pollByMessage;
                    continue;
                }

                if (string.Equals(message.MessageType, "POLL_BUMP", StringComparison.OrdinalIgnoreCase)
                    && message.PollId.HasValue
                    && pollsById.TryGetValue(message.PollId.Value, out PollModel? pollById))
                {
                    message.Poll = pollById;
                }
            }
        }

        private static void AttachPollOptions(NpgsqlConnection connection, List<PollModel> polls)
        {
            if (polls.Count == 0)
            {
                return;
            }

            int[] pollIds = polls.Select(poll => poll.Id).ToArray();
            var pollsById = polls.ToDictionary(poll => poll.Id);
            const string optionQuery = @"
                SELECT po.ID, po.POLL_ID, po.OPTION_TEXT, po.SORT_ORDER,
                       COALESCE(vote_counts.VOTE_COUNT, 0) AS VOTE_COUNT
                FROM POLL_OPTIONS po
                LEFT JOIN (
                    SELECT OPTION_ID, COUNT(*) AS VOTE_COUNT
                    FROM POLL_VOTES
                    GROUP BY OPTION_ID
                ) vote_counts ON vote_counts.OPTION_ID = po.ID
                WHERE po.POLL_ID = ANY(@poll_ids)
                ORDER BY po.POLL_ID, po.SORT_ORDER, po.ID";

            using var command = new NpgsqlCommand(optionQuery, connection);
            command.Parameters.AddWithValue("@poll_ids", pollIds);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                int pollId = reader.GetInt32(1);
                if (!pollsById.TryGetValue(pollId, out PollModel? poll))
                {
                    continue;
                }

                poll.Options.Add(new PollOptionModel
                {
                    Id = reader.GetInt32(0),
                    PollId = pollId,
                    OptionText = reader.GetString(2),
                    SortOrder = reader.GetInt32(3),
                    VoteCount = reader.GetInt32(4)
                });
            }
        }

        private static async Task AttachPollOptionsAsync(NpgsqlConnection connection, List<PollModel> polls, CancellationToken cancellationToken)
        {
            if (polls.Count == 0)
            {
                return;
            }

            int[] pollIds = polls.Select(poll => poll.Id).ToArray();
            var pollsById = polls.ToDictionary(poll => poll.Id);
            const string optionQuery = @"
                SELECT po.ID, po.POLL_ID, po.OPTION_TEXT, po.SORT_ORDER,
                       COALESCE(vote_counts.VOTE_COUNT, 0) AS VOTE_COUNT
                FROM POLL_OPTIONS po
                LEFT JOIN (
                    SELECT OPTION_ID, COUNT(*) AS VOTE_COUNT
                    FROM POLL_VOTES
                    GROUP BY OPTION_ID
                ) vote_counts ON vote_counts.OPTION_ID = po.ID
                WHERE po.POLL_ID = ANY(@poll_ids)
                ORDER BY po.POLL_ID, po.SORT_ORDER, po.ID";

            await using var command = new NpgsqlCommand(optionQuery, connection);
            command.Parameters.AddWithValue("@poll_ids", pollIds);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                int pollId = reader.GetInt32(1);
                if (!pollsById.TryGetValue(pollId, out PollModel? poll))
                {
                    continue;
                }

                poll.Options.Add(new PollOptionModel
                {
                    Id = reader.GetInt32(0),
                    PollId = pollId,
                    OptionText = reader.GetString(2),
                    SortOrder = reader.GetInt32(3),
                    VoteCount = reader.GetInt32(4)
                });
            }
        }

        private static void EnsurePollSchema(NpgsqlConnection connection)
        {
            const string sql = @"
                CREATE TABLE IF NOT EXISTS POLLS (
                    ID SERIAL PRIMARY KEY,
                    COURSE_ID INT NOT NULL REFERENCES COURSES(ID) ON DELETE CASCADE,
                    MESSAGE_ID INT NOT NULL UNIQUE REFERENCES MESSAGES(ID) ON DELETE CASCADE,
                    CREATED_BY INT NOT NULL REFERENCES USERS(ID) ON DELETE CASCADE,
                    QUESTION TEXT NOT NULL,
                    IS_CLOSED BOOLEAN NOT NULL DEFAULT FALSE,
                    CREATED_AT TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    CLOSED_AT TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS POLL_OPTIONS (
                    ID SERIAL PRIMARY KEY,
                    POLL_ID INT NOT NULL REFERENCES POLLS(ID) ON DELETE CASCADE,
                    OPTION_TEXT TEXT NOT NULL,
                    SORT_ORDER INT NOT NULL DEFAULT 0,
                    CREATED_AT TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS POLL_VOTES (
                    POLL_ID INT NOT NULL REFERENCES POLLS(ID) ON DELETE CASCADE,
                    USER_ID INT NOT NULL REFERENCES USERS(ID) ON DELETE CASCADE,
                    OPTION_ID INT NOT NULL REFERENCES POLL_OPTIONS(ID) ON DELETE CASCADE,
                    VOTED_AT TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (POLL_ID, USER_ID)
                );

                ALTER TABLE MESSAGES ADD COLUMN IF NOT EXISTS POLL_ID INT;

                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.table_constraints
                        WHERE LOWER(table_name) = 'messages'
                          AND LOWER(constraint_name) = 'fk_messages_polls'
                    ) THEN
                        ALTER TABLE MESSAGES
                        ADD CONSTRAINT FK_MESSAGES_POLLS
                        FOREIGN KEY (POLL_ID) REFERENCES POLLS(ID) ON DELETE SET NULL;
                    END IF;
                END $$;

                CREATE INDEX IF NOT EXISTS IDX_POLLS_COURSE_ID ON POLLS(COURSE_ID);
                CREATE INDEX IF NOT EXISTS IDX_POLLS_MESSAGE_ID ON POLLS(MESSAGE_ID);
                CREATE INDEX IF NOT EXISTS IDX_POLL_OPTIONS_POLL_ID ON POLL_OPTIONS(POLL_ID);
                CREATE INDEX IF NOT EXISTS IDX_POLL_OPTIONS_SORT_ORDER ON POLL_OPTIONS(POLL_ID, SORT_ORDER, ID);
                CREATE INDEX IF NOT EXISTS IDX_POLL_VOTES_POLL_ID ON POLL_VOTES(POLL_ID);
                CREATE INDEX IF NOT EXISTS IDX_POLL_VOTES_OPTION_ID ON POLL_VOTES(OPTION_ID);
                CREATE INDEX IF NOT EXISTS IDX_MESSAGES_POLL_ID ON MESSAGES(POLL_ID);
                CREATE INDEX IF NOT EXISTS IDX_MESSAGES_POLL_BUMP_COOLDOWN
                    ON MESSAGES(POLL_ID, MESSAGE_TYPE, SENT_AT DESC)
                    WHERE MESSAGE_TYPE = 'POLL_BUMP';";

            using var command = new NpgsqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        private static void EnsureAuditLogSchema(NpgsqlConnection connection)
        {
            const string sql = @"
                CREATE TABLE IF NOT EXISTS AUDIT_LOGS (
                    ID SERIAL PRIMARY KEY,
                    USER_ID INT NULL,
                    ACTION VARCHAR(200),
                    TARGET_TABLE VARCHAR(100),
                    TARGET_ID INT NULL,
                    CREATED_AT TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );
                ALTER TABLE AUDIT_LOGS ADD COLUMN IF NOT EXISTS DETAILS TEXT;
                ALTER TABLE AUDIT_LOGS ADD COLUMN IF NOT EXISTS IP_ADDRESS VARCHAR(50);";

            using var cmd = new NpgsqlCommand(sql, connection);
            cmd.ExecuteNonQuery();
        }

        private static void EnsureLoginDailyStatsSchema(NpgsqlConnection connection)
        {
            const string sql = @"
                CREATE TABLE IF NOT EXISTS LOGIN_DAILY_STATS (
                    STAT_DATE DATE PRIMARY KEY,
                    LOGIN_COUNT INT NOT NULL DEFAULT 0,
                    UPDATED_AT TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
                CREATE INDEX IF NOT EXISTS IDX_LOGIN_DAILY_STATS_DATE
                    ON LOGIN_DAILY_STATS (STAT_DATE DESC);";

            using var cmd = new NpgsqlCommand(sql, connection);
            cmd.ExecuteNonQuery();
        }

        private static void UpsertTodayLoginStats(NpgsqlConnection connection)
        {
            const string sql = @"
                INSERT INTO LOGIN_DAILY_STATS (STAT_DATE, LOGIN_COUNT, UPDATED_AT)
                VALUES (CURRENT_DATE, 1, CURRENT_TIMESTAMP)
                ON CONFLICT (STAT_DATE)
                DO UPDATE SET LOGIN_COUNT = LOGIN_DAILY_STATS.LOGIN_COUNT + 1,
                              UPDATED_AT = CURRENT_TIMESTAMP";

            using var cmd = new NpgsqlCommand(sql, connection);
            cmd.ExecuteNonQuery();
        }

        public void LogUserActivity(int? userId, string action, string details, string ipAddress)
        {
            using var connection = CreateConnection();
            connection.Open();
            EnsureAuditLogSchema(connection);
            EnsureLoginDailyStatsSchema(connection);

            const string query = @"
                INSERT INTO AUDIT_LOGS (USER_ID, ACTION, TARGET_TABLE, TARGET_ID, DETAILS, IP_ADDRESS, CREATED_AT)
                VALUES (@user_id, @action, 'USERS', @target_id, @details, @ip_address, @now)";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@user_id", userId.HasValue ? (object)userId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@action", action ?? "UNKNOWN");
            command.Parameters.AddWithValue("@target_id", userId.HasValue ? (object)userId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@details", details ?? string.Empty);
            command.Parameters.AddWithValue("@ip_address", ipAddress ?? string.Empty);
            command.Parameters.AddWithValue("@now", DateTime.Now);
            command.ExecuteNonQuery();

            if (string.Equals(action, "LOGIN", StringComparison.OrdinalIgnoreCase))
            {
                UpsertTodayLoginStats(connection);
            }
        }

        public async Task LogUserActivityAsync(int? userId, string action, string details, string ipAddress, CancellationToken cancellationToken = default)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            EnsureAuditLogSchema(connection);
            EnsureLoginDailyStatsSchema(connection);

            const string query = @"
                INSERT INTO AUDIT_LOGS (USER_ID, ACTION, TARGET_TABLE, TARGET_ID, DETAILS, IP_ADDRESS, CREATED_AT)
                VALUES (@user_id, @action, 'USERS', @target_id, @details, @ip_address, @now)";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@user_id", userId.HasValue ? (object)userId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@action", action ?? "UNKNOWN");
            command.Parameters.AddWithValue("@target_id", userId.HasValue ? (object)userId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@details", details ?? string.Empty);
            command.Parameters.AddWithValue("@ip_address", ipAddress ?? string.Empty);
            command.Parameters.AddWithValue("@now", DateTime.Now);
            await command.ExecuteNonQueryAsync(cancellationToken);

            if (string.Equals(action, "LOGIN", StringComparison.OrdinalIgnoreCase))
            {
                UpsertTodayLoginStats(connection);
            }
        }

        public AdminDashboardMetricsModel GetAdminDashboardMetrics()
        {
            using var connection = CreateConnection();
            connection.Open();
            EnsureAuditLogSchema(connection);

            int ScalarInt(string sql)
            {
                using var cmd = new NpgsqlCommand(sql, connection);
                object? value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
            }

            return new AdminDashboardMetricsModel
            {
                TotalUsers = ScalarInt("SELECT COUNT(*) FROM USERS"),
                ActiveCourses = ScalarInt("SELECT COUNT(*) FROM COURSES WHERE UPPER(COALESCE(STATUS, '')) = 'ACTIVE'"),
                PendingRequests = ScalarInt("SELECT COUNT(*) FROM USERS WHERE UPPER(COALESCE(STATUS, '')) = 'PENDING'"),
                TodayLogins = ScalarInt("SELECT COUNT(*) FROM AUDIT_LOGS WHERE ACTION = 'LOGIN' AND CREATED_AT >= CURRENT_DATE AND CREATED_AT < CURRENT_DATE + INTERVAL '1 day'")
            };
        }

        public List<RecentUserActivityModel> GetRecentUserActivities(int limit = 20)
        {
            var result = new List<RecentUserActivityModel>();
            using var connection = CreateConnection();
            connection.Open();
            EnsureAuditLogSchema(connection);

            const string query = @"
                SELECT a.CREATED_AT,
                       COALESCE(u.USERNAME, 'SYSTEM') AS USERNAME,
                       COALESCE(a.ACTION, '') AS ACTION,
                       COALESCE(a.IP_ADDRESS, '') AS IP_ADDRESS,
                       COALESCE(a.DETAILS, '') AS DETAILS
                FROM AUDIT_LOGS a
                LEFT JOIN USERS u ON u.ID = a.USER_ID
                WHERE a.ACTION IN (
                    'LOGIN', 'LOGOUT', 'SIGNUP', 'FORGOT_PASSWORD', 'CHANGE_PASSWORD',
                    'COURSE_ENROLL_REQUEST', 'COURSE_ENROLL', 'ONLINE_SESSION_JOIN', 'ONLINE_SESSION_EXIT',
                    'EXAM_JOIN', 'EXAM_SUBMIT', 'EXAM_EXIT',
                    'ADMIN_ADD_USER', 'ADMIN_DELETE_USER', 'ADMIN_APPROVE_REGISTRATION',
                    'ADMIN_REJECT_USER_REQUEST', 'ADMIN_RESET_PASSWORD', 'FORGOT_PASSWORD_APPROVED'
                )
                ORDER BY a.CREATED_AT DESC
                LIMIT @limit";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@limit", limit);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new RecentUserActivityModel
                {
                    CreatedAt = reader.GetDateTime(0),
                    Username = reader.GetString(1),
                    Action = reader.GetString(2),
                    IpAddress = reader.GetString(3),
                    Details = reader.GetString(4)
                });
            }

            return result;
        }

        public List<RecentUserActivityModel> GetRecentUserActivitiesByUser(int userId, int limit = 10)
        {
            var result = new List<RecentUserActivityModel>();
            int safeLimit = limit <= 0 ? 10 : limit;

            using var connection = CreateConnection();
            connection.Open();
            EnsureAuditLogSchema(connection);

            const string query = @"
                SELECT a.CREATED_AT,
                       COALESCE(u.USERNAME, 'SYSTEM') AS USERNAME,
                       COALESCE(a.ACTION, '') AS ACTION,
                       COALESCE(a.IP_ADDRESS, '') AS IP_ADDRESS,
                       COALESCE(a.DETAILS, '') AS DETAILS
                FROM AUDIT_LOGS a
                LEFT JOIN USERS u ON u.ID = a.USER_ID
                WHERE a.USER_ID = @user_id
                  AND a.ACTION IN (
                    'LOGIN', 'LOGOUT', 'SIGNUP', 'FORGOT_PASSWORD', 'CHANGE_PASSWORD',
                    'COURSE_ENROLL_REQUEST', 'COURSE_ENROLL', 'ONLINE_SESSION_JOIN', 'ONLINE_SESSION_EXIT',
                    'EXAM_JOIN', 'EXAM_SUBMIT', 'EXAM_EXIT',
                    'CHAT_USE'
                  )
                ORDER BY a.CREATED_AT DESC
                LIMIT @limit";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@user_id", userId);
            command.Parameters.AddWithValue("@limit", safeLimit);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new RecentUserActivityModel
                {
                    CreatedAt = reader.GetDateTime(0),
                    Username = reader.GetString(1),
                    Action = reader.GetString(2),
                    IpAddress = reader.GetString(3),
                    Details = reader.GetString(4)
                });
            }

            return result;
        }

        public List<LoginFrequencyModel> GetLoginFrequencyStats(int days = 7)
        {
            var result = new List<LoginFrequencyModel>();
            int safeDays = days <= 0 ? 7 : days;

            using var connection = CreateConnection();
            connection.Open();
            EnsureLoginDailyStatsSchema(connection);

            const string query = @"
                SELECT STAT_DATE, LOGIN_COUNT
                FROM LOGIN_DAILY_STATS
                WHERE STAT_DATE >= CURRENT_DATE - (@days - 1)
                ORDER BY STAT_DATE ASC";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@days", safeDays);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new LoginFrequencyModel
                {
                    LoginDate = reader.GetDateTime(0),
                    LoginCount = reader.GetInt32(1)
                });
            }

            return result;
        }

        public async Task<List<LoginFrequencyModel>> GetLoginFrequencyStatsAsync(int days = 7, CancellationToken cancellationToken = default)
        {
            var result = new List<LoginFrequencyModel>();
            int safeDays = days <= 0 ? 7 : days;

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            EnsureLoginDailyStatsSchema(connection);

            const string query = @"
                SELECT STAT_DATE, LOGIN_COUNT
                FROM LOGIN_DAILY_STATS
                WHERE STAT_DATE >= CURRENT_DATE - (@days - 1)
                ORDER BY STAT_DATE ASC";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@days", safeDays);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new LoginFrequencyModel
                {
                    LoginDate = reader.GetDateTime(0),
                    LoginCount = reader.GetInt32(1)
                });
            }

            return result;
        }

        public List<CourseListItemModel> GetCourseListItems(int limit = 100)
        {
            var result = new List<CourseListItemModel>();
            int safeLimit = limit <= 0 ? 100 : limit;

            using var connection = CreateConnection();
            connection.Open();

            const string query = @"
                SELECT c.ID,
                       COALESCE(c.NAME, '') AS COURSE_NAME,
                       COALESCE(u.FULL_NAME, u.USERNAME, 'Unknown') AS TEACHER_NAME,
                       COALESCE(c.STATUS, 'UNKNOWN') AS COURSE_STATUS,
                       COUNT(e.ID)::int AS ENROLLMENT_COUNT
                FROM COURSES c
                LEFT JOIN USERS u ON u.ID = c.TEACHER_ID
                LEFT JOIN ENROLLMENTS e ON e.COURSE_ID = c.ID
                GROUP BY c.ID, c.NAME, u.FULL_NAME, u.USERNAME, c.STATUS
                ORDER BY c.ID DESC
                LIMIT @limit";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@limit", safeLimit);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new CourseListItemModel
                {
                    CourseId = reader.GetInt32(0),
                    CourseName = reader.GetString(1),
                    TeacherName = reader.GetString(2),
                    Status = reader.GetString(3),
                    EnrollmentCount = reader.GetInt32(4)
                });
            }

            return result;
        }

        public async Task<List<CourseListItemModel>> GetCourseListItemsAsync(int limit = 100, CancellationToken cancellationToken = default)
        {
            var result = new List<CourseListItemModel>();
            int safeLimit = limit <= 0 ? 100 : limit;

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            const string query = @"
                SELECT c.ID,
                       COALESCE(c.NAME, '') AS COURSE_NAME,
                       COALESCE(u.FULL_NAME, u.USERNAME, 'Unknown') AS TEACHER_NAME,
                       COALESCE(c.STATUS, 'UNKNOWN') AS COURSE_STATUS,
                       COUNT(e.ID)::int AS ENROLLMENT_COUNT
                FROM COURSES c
                LEFT JOIN USERS u ON u.ID = c.TEACHER_ID
                LEFT JOIN ENROLLMENTS e ON e.COURSE_ID = c.ID
                GROUP BY c.ID, c.NAME, u.FULL_NAME, u.USERNAME, c.STATUS
                ORDER BY c.ID DESC
                LIMIT @limit";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@limit", safeLimit);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new CourseListItemModel
                {
                    CourseId = reader.GetInt32(0),
                    CourseName = reader.GetString(1),
                    TeacherName = reader.GetString(2),
                    Status = reader.GetString(3),
                    EnrollmentCount = reader.GetInt32(4)
                });
            }

            return result;
        }

        public AccountSummaryModel GetAccountSummary()
        {
            using var connection = CreateConnection();
            connection.Open();

            const string query = @"
                SELECT
                    COUNT(*)::int AS TOTAL_ACCOUNTS,
                    COUNT(*) FILTER (WHERE UPPER(COALESCE(u.STATUS, '')) = 'ACTIVE')::int AS ACTIVE_ACCOUNTS,
                    COUNT(*) FILTER (WHERE UPPER(COALESCE(u.STATUS, '')) = 'PENDING')::int AS PENDING_ACCOUNTS,
                    COUNT(*) FILTER (WHERE UPPER(COALESCE(r.NAME, '')) = 'TEACHER')::int AS TEACHER_ACCOUNTS,
                    COUNT(*) FILTER (WHERE UPPER(COALESCE(r.NAME, '')) = 'STUDENT')::int AS STUDENT_ACCOUNTS
                FROM USERS u
                LEFT JOIN ROLES r ON r.ID = u.ROLE_ID";

            using var command = new NpgsqlCommand(query, connection);
            using var reader = command.ExecuteReader(CommandBehavior.SingleRow);
            if (reader.Read())
            {
                return new AccountSummaryModel
                {
                    TotalAccounts = reader.GetInt32(0),
                    ActiveAccounts = reader.GetInt32(1),
                    PendingAccounts = reader.GetInt32(2),
                    TeacherAccounts = reader.GetInt32(3),
                    StudentAccounts = reader.GetInt32(4)
                };
            }

            return new AccountSummaryModel();
        }

        public async Task<AccountSummaryModel> GetAccountSummaryAsync(CancellationToken cancellationToken = default)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            const string query = @"
                SELECT
                    COUNT(*)::int AS TOTAL_ACCOUNTS,
                    COUNT(*) FILTER (WHERE UPPER(COALESCE(u.STATUS, '')) = 'ACTIVE')::int AS ACTIVE_ACCOUNTS,
                    COUNT(*) FILTER (WHERE UPPER(COALESCE(u.STATUS, '')) = 'PENDING')::int AS PENDING_ACCOUNTS,
                    COUNT(*) FILTER (WHERE UPPER(COALESCE(r.NAME, '')) = 'TEACHER')::int AS TEACHER_ACCOUNTS,
                    COUNT(*) FILTER (WHERE UPPER(COALESCE(r.NAME, '')) = 'STUDENT')::int AS STUDENT_ACCOUNTS
                FROM USERS u
                LEFT JOIN ROLES r ON r.ID = u.ROLE_ID";

            await using var command = new NpgsqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new AccountSummaryModel
                {
                    TotalAccounts = reader.GetInt32(0),
                    ActiveAccounts = reader.GetInt32(1),
                    PendingAccounts = reader.GetInt32(2),
                    TeacherAccounts = reader.GetInt32(3),
                    StudentAccounts = reader.GetInt32(4)
                };
            }

            return new AccountSummaryModel();
        }

        public async Task LogDeviceActivityAsync(int userId, string deviceName, string ipAddress, CancellationToken cancellationToken = default)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            const string query = @"
                WITH updated AS (
                    UPDATE DEVICES
                    SET last_active = @now,
                        ip_address = @ip_address,
                        status = 'ACTIVE'
                    WHERE user_id = @user_id AND device_name = @device_name
                    RETURNING id
                )
                INSERT INTO DEVICES (user_id, device_name, ip_address, status, last_active)
                SELECT @user_id, @device_name, @ip_address, 'ACTIVE', @now
                WHERE NOT EXISTS (SELECT 1 FROM updated)";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@user_id", userId);
            command.Parameters.AddWithValue("@device_name", deviceName ?? "Unknown Device");
            command.Parameters.AddWithValue("@ip_address", ipAddress ?? "Unknown IP");
            command.Parameters.AddWithValue("@now", DateTime.Now);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public List<UserModel> SearchUsers(string status, string role)
        {
            var users = new List<UserModel>();
            using var connection = CreateConnection();
            connection.Open();

            string query = @"SELECT u.id, u.username, u.password_hash, u.full_name, u.email, r.name as role, u.status 
                             FROM USERS u 
                             JOIN ROLES r ON u.role_id = r.id 
                             WHERE 1=1";
            
            if (status != "ALL") query += " AND UPPER(u.status) = UPPER(@status)";
            if (role != "ALL") query += " AND UPPER(r.name) = UPPER(@role)";

            using var command = new NpgsqlCommand(query, connection);
            if (status != "ALL") command.Parameters.AddWithValue("@status", status);
            if (role != "ALL") command.Parameters.AddWithValue("@role", role);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                users.Add(new UserModel
                {
                    Id = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    PasswordHash = reader.GetString(2),
                    FullName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    Email = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Role = reader.GetString(5),
                    Status = reader.GetString(6)
                });
            }

            return users;
        }

        public async Task<List<TeacherLessonModel>> GetStudentLessonsAsync(int studentId, CancellationToken cancellationToken = default)
        {
            var result = new List<TeacherLessonModel>();
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            const string query = @"
                SELECT l.id,
                       c.id AS course_id,
                       c.name AS course_name,
                       l.title,
                       COALESCE(l.content, '') AS content,
                       l.publish_at,
                       l.status,
                       COALESCE(l.file_name, '') AS file_name,
                       COALESCE(l.file_size, 0) AS file_size,
                       (l.file_content IS NOT NULL) AS has_stored_content
                FROM teacher_lessons l
                JOIN courses c ON c.id = l.course_id
                JOIN enrollments e ON e.course_id = c.id
                WHERE e.student_id = @student_id
                  AND UPPER(COALESCE(e.status, '')) IN ('ACTIVE', 'APPROVED')
                  AND UPPER(COALESCE(c.status, 'ACTIVE')) IN ('ACTIVE', 'APPROVED', 'OPEN')
                  AND UPPER(COALESCE(l.status, '')) = 'PUBLISHED'

                UNION ALL

                SELECT -m.id AS id,
                       c.id AS course_id,
                       c.name AS course_name,
                       COALESCE(m.file_name, 'Tài liệu') AS title,
                       '' AS content,
                       m.uploaded_at AS publish_at,
                       'PUBLISHED' AS status,
                       COALESCE(m.file_name, '') AS file_name,
                       COALESCE(m.file_size, 0) AS file_size,
                       (m.file_content IS NOT NULL) AS has_stored_content
                FROM materials m
                JOIN courses c ON c.id = m.course_id
                JOIN enrollments e ON e.course_id = c.id
                WHERE e.student_id = @student_id
                  AND UPPER(COALESCE(e.status, '')) IN ('ACTIVE', 'APPROVED')
                  AND UPPER(COALESCE(c.status, 'ACTIVE')) IN ('ACTIVE', 'APPROVED', 'OPEN')

                ORDER BY publish_at DESC, id DESC";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@student_id", studentId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new TeacherLessonModel
                {
                    Id = reader.GetInt32(0),
                    CourseId = reader.GetInt32(1),
                    CourseName = reader.GetString(2),
                    Title = reader.GetString(3),
                    Content = reader.GetString(4),
                    PublishAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    Status = reader.GetString(6),
                    FileName = reader.GetString(7),
                    FileSize = reader.GetInt64(8),
                    HasStoredContent = reader.GetBoolean(9)
                });
            }

            return result;
        }

        public async Task<byte[]?> GetStudentLessonFileContentAsync(int lessonId, int studentId, CancellationToken cancellationToken = default)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            bool isMaterialRow = lessonId < 0;
            int sourceId = isMaterialRow ? Math.Abs(lessonId) : lessonId;
            string query = isMaterialRow
                ? @"
                SELECT m.file_content
                FROM materials m
                JOIN courses c ON c.id = m.course_id
                JOIN enrollments e ON e.course_id = c.id
                WHERE m.id = @source_id
                  AND e.student_id = @student_id
                  AND UPPER(COALESCE(e.status, '')) IN ('ACTIVE', 'APPROVED')
                  AND UPPER(COALESCE(c.status, 'ACTIVE')) IN ('ACTIVE', 'APPROVED', 'OPEN')"
                : @"
                SELECT l.file_content
                FROM teacher_lessons l
                JOIN courses c ON c.id = l.course_id
                JOIN enrollments e ON e.course_id = c.id
                WHERE l.id = @source_id
                  AND e.student_id = @student_id
                  AND UPPER(COALESCE(e.status, '')) IN ('ACTIVE', 'APPROVED')
                  AND UPPER(COALESCE(c.status, 'ACTIVE')) IN ('ACTIVE', 'APPROVED', 'OPEN')
                  AND UPPER(COALESCE(l.status, '')) = 'PUBLISHED'";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@source_id", sourceId);
            command.Parameters.AddWithValue("@student_id", studentId);
            
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result == null || result == DBNull.Value ? null : (byte[])result;
        }

        public void EnsureDeadlineReminderSchema()
        {
            using var connection = CreateConnection();
            connection.Open();
            EnsureDeadlineReminderSchema(connection);
        }

        private static void EnsureDeadlineReminderSchema(NpgsqlConnection connection)
        {
            const string sql = @"
                CREATE TABLE IF NOT EXISTS deadline_reminders_sent (
                    user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                    source_type VARCHAR(32) NOT NULL,
                    source_id INT NOT NULL,
                    remind_type VARCHAR(10) NOT NULL,
                    sent_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (user_id, source_type, source_id, remind_type)
                );

                CREATE INDEX IF NOT EXISTS idx_deadline_reminders_sent_at
                    ON deadline_reminders_sent(sent_at);

                ALTER TABLE notifications
                    ADD COLUMN IF NOT EXISTS category VARCHAR(32) NOT NULL DEFAULT 'SystemAdmin',
                    ADD COLUMN IF NOT EXISTS notification_type VARCHAR(32) NOT NULL DEFAULT 'Informational',
                    ADD COLUMN IF NOT EXISTS source_type VARCHAR(64),
                    ADD COLUMN IF NOT EXISTS source_id INT;

                DELETE FROM deadline_reminders_sent
                WHERE sent_at < CURRENT_TIMESTAMP - INTERVAL '7 days';";

            using var command = new NpgsqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        public int CreateDeadlineReminderNotification(
            int userId,
            DeadlineReminderItem item,
            string remindType,
            string title,
            string content)
        {
            using var connection = CreateConnection();
            connection.Open();
            EnsureDeadlineReminderSchema(connection);

            using var transaction = connection.BeginTransaction();

            const string sql = @"
                WITH claimed AS (
                    INSERT INTO deadline_reminders_sent (user_id, source_type, source_id, remind_type, sent_at)
                    VALUES (@user_id, @source_type, @source_id, @remind_type, CURRENT_TIMESTAMP)
                    ON CONFLICT (user_id, source_type, source_id, remind_type) DO NOTHING
                    RETURNING 1
                ),
                created AS (
                    INSERT INTO notifications (
                        user_id,
                        title,
                        content,
                        is_read,
                        created_at,
                        category,
                        notification_type,
                        source_type,
                        source_id)
                    SELECT
                        @user_id,
                        @title,
                        @content,
                        false,
                        CURRENT_TIMESTAMP,
                        @category,
                        @notification_type,
                        @source_type,
                        @source_id
                    FROM claimed
                    RETURNING id
                )
                SELECT id FROM created;";

            try
            {
                using var command = new NpgsqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@user_id", userId);
                command.Parameters.AddWithValue("@source_type", item.SourceType);
                command.Parameters.AddWithValue("@source_id", item.SourceId);
                command.Parameters.AddWithValue("@remind_type", remindType);
                command.Parameters.AddWithValue("@title", title);
                command.Parameters.AddWithValue("@content", content);
                command.Parameters.AddWithValue("@category", item.NotificationCategory);
                command.Parameters.AddWithValue("@notification_type", WorkflowConstants.NotificationType.ActionRequired);

                object? result = command.ExecuteScalar();
                transaction.Commit();
                return result == null || result == DBNull.Value ? 0 : System.Convert.ToInt32(result);
            }
            catch
            {
                try
                {
                    transaction.Rollback();
                }
                catch
                {
                }

                throw;
            }
        }

        public List<DeadlineReminderItem> GetUpcomingDeadlines(int userId, DateTime from, DateTime to)
        {
            var result = new List<DeadlineReminderItem>();
            using var connection = CreateConnection();
            connection.Open();
            EnsureDeadlineReminderSchema(connection);

            if (!TableExists(connection, "courses") || !TableExists(connection, "enrollments"))
                return result;

            var queryParts = new List<string>();

            if (TableExists(connection, "exams"))
            {
                bool hasAttempts = TableExists(connection, "exam_attempts");
                bool hasQuestions = TableExists(connection, "exam_questions");
                string attemptsExpression = hasAttempts
                    ? @"(
                        SELECT COUNT(*)
                        FROM exam_attempts ea
                        WHERE ea.exam_id = ex.id
                          AND ea.student_id = @student_id
                      )"
                    : "0";
                string questionExpression = hasQuestions
                    ? @"(
                        SELECT COUNT(*)
                        FROM exam_questions eq
                        WHERE eq.exam_id = ex.id
                      )"
                    : "0";
                string inProgressAttemptsExpression = hasAttempts
                    ? @"(
                        SELECT COUNT(*)
                        FROM exam_attempts ea
                        WHERE ea.exam_id = ex.id
                          AND ea.student_id = @student_id
                          AND UPPER(COALESCE(ea.status, '')) = 'IN_PROGRESS'
                      )"
                    : "0";

                queryParts.Add($@"
                    SELECT '{DeadlineReminderItem.SourceTypeExam}' AS source_type,
                           ex.id AS source_id,
                           c.id AS course_id,
                           COALESCE(ex.title, '') AS title,
                           COALESCE(c.name, '') AS course_name,
                           ex.close_time AS due_at
                    FROM exams ex
                    JOIN courses c ON c.id = ex.course_id
                    JOIN enrollments e ON e.course_id = c.id AND e.student_id = @student_id
                    WHERE ex.close_time IS NOT NULL
                      AND ex.close_time BETWEEN @from AND @to
                      AND UPPER(COALESCE(e.status, '')) IN ('ACTIVE', 'APPROVED')
                      AND UPPER(COALESCE(c.status, 'ACTIVE')) IN ('ACTIVE', 'APPROVED', 'OPEN')
                      AND UPPER(COALESCE(ex.status, 'DRAFT')) = 'ACTIVE'
                      AND ({questionExpression}) > 0
                      AND (
                          ({inProgressAttemptsExpression}) > 0
                          OR COALESCE(ex.max_attempts, 1) <= 0
                          OR {attemptsExpression} < COALESCE(ex.max_attempts, 1)
                      )");
            }

            if (TableExists(connection, "teacher_assignments"))
            {
                bool hasSubmissions = TableExists(connection, "assignment_submissions");
                string submissionJoin = hasSubmissions
                    ? "LEFT JOIN assignment_submissions s ON s.assignment_id = a.id AND s.student_id = e.student_id"
                    : string.Empty;
                string unsubmittedFilter = hasSubmissions ? "AND s.id IS NULL" : string.Empty;

                queryParts.Add($@"
                    SELECT '{DeadlineReminderItem.SourceTypeAssignment}' AS source_type,
                           a.id AS source_id,
                           c.id AS course_id,
                           COALESCE(a.title, '') AS title,
                           COALESCE(c.name, '') AS course_name,
                           a.due_at AS due_at
                    FROM teacher_assignments a
                    JOIN courses c ON c.id = a.course_id
                    JOIN enrollments e ON e.course_id = c.id AND e.student_id = @student_id
                    {submissionJoin}
                    WHERE a.due_at IS NOT NULL
                      AND a.due_at BETWEEN @from AND @to
                      AND UPPER(COALESCE(e.status, '')) IN ('ACTIVE', 'APPROVED')
                      AND UPPER(COALESCE(c.status, '')) = 'ACTIVE'
                      AND UPPER(COALESCE(a.status, 'OPEN')) = 'OPEN'
                      {unsubmittedFilter}");
            }

            if (queryParts.Count == 0)
                return result;

            string query = string.Join(Environment.NewLine + "UNION ALL" + Environment.NewLine, queryParts)
                + Environment.NewLine
                + "ORDER BY due_at ASC, source_id ASC;";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@student_id", userId);
            command.Parameters.AddWithValue("@from", from);
            command.Parameters.AddWithValue("@to", to);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new DeadlineReminderItem
                {
                    SourceType = reader.GetString(0),
                    SourceId = reader.GetInt32(1),
                    CourseId = reader.GetInt32(2),
                    Title = reader.GetString(3),
                    CourseName = reader.GetString(4),
                    DueAt = reader.GetDateTime(5)
                });
            }

            return result;
        }

        public async Task<List<StudentAssignmentRow>> GetStudentAssignmentsAsync(int studentId, CancellationToken cancellationToken = default)
        {
            var result = new List<StudentAssignmentRow>();
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            const string query = @"
                SELECT a.id AS assignment_id,
                       c.id AS course_id,
                       c.name AS course_name,
                       a.title,
                       COALESCE(a.description, '') AS description,
                       a.due_at AS due_date,
                       a.status,
                       COALESCE(a.file_name, '') AS teacher_file_name,
                       COALESCE(a.file_size, 0) AS teacher_file_size,
                       (a.file_content IS NOT NULL) AS has_teacher_file,
                       s.id AS submission_id,
                       COALESCE(s.file_name, '') AS student_file_name,
                       s.submitted_at,
                       s.score,
                       COALESCE(s.feedback, '') AS feedback
                FROM teacher_assignments a
                JOIN courses c ON c.id = a.course_id
                JOIN enrollments e ON e.course_id = c.id
                LEFT JOIN assignment_submissions s ON s.assignment_id = a.id AND s.student_id = e.student_id
                WHERE e.student_id = @student_id
                  AND UPPER(COALESCE(e.status, '')) IN ('ACTIVE', 'APPROVED')
                  AND UPPER(COALESCE(c.status, '')) = 'ACTIVE'
                ORDER BY a.due_at DESC, a.id DESC";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@student_id", studentId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new StudentAssignmentRow
                {
                    AssignmentId = reader.GetInt32(0),
                    CourseId = reader.GetInt32(1),
                    CourseName = reader.GetString(2),
                    Title = reader.GetString(3),
                    Description = reader.GetString(4),
                    DueDate = reader.GetDateTime(5),
                    Status = reader.GetString(6),
                    TeacherFileName = reader.GetString(7),
                    TeacherFileSize = reader.GetInt64(8),
                    HasTeacherFile = reader.GetBoolean(9),
                    SubmissionId = reader.IsDBNull(10) ? null : reader.GetInt32(10),
                    StudentFileName = reader.GetString(11),
                    SubmittedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
                    Score = reader.IsDBNull(13) ? null : reader.GetDecimal(13),
                    Feedback = reader.GetString(14)
                });
            }

            return result;
        }

        public async Task<byte[]?> GetAssignmentContentAsync(int assignmentId, int studentId, CancellationToken cancellationToken = default)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            
            const string query = @"
                SELECT a.file_content
                FROM teacher_assignments a
                JOIN courses c ON c.id = a.course_id
                JOIN enrollments e ON e.course_id = c.id
                WHERE a.id = @assignment_id
                  AND e.student_id = @student_id
                  AND UPPER(COALESCE(e.status, '')) IN ('ACTIVE', 'APPROVED')
                  AND UPPER(COALESCE(c.status, '')) = 'ACTIVE'";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@assignment_id", assignmentId);
            command.Parameters.AddWithValue("@student_id", studentId);
            
            object? value = await command.ExecuteScalarAsync(cancellationToken);
            return value == null || value == DBNull.Value ? null : (byte[])value;
        }

        public async Task<bool> SubmitAssignmentAsync(AssignmentSubmissionModel submission, CancellationToken cancellationToken = default)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            const string query = @"
                WITH eligible_assignment AS (
                    SELECT a.id
                    FROM (SELECT 1) guard
                    JOIN teacher_assignments a ON a.id = @assignment_id
                    JOIN courses c ON c.id = a.course_id
                    JOIN enrollments e ON e.course_id = c.id
                    WHERE e.student_id = @student_id
                      AND UPPER(COALESCE(e.status, '')) IN ('ACTIVE', 'APPROVED')
                      AND UPPER(COALESCE(c.status, '')) = 'ACTIVE'
                      AND UPPER(COALESCE(a.status, '')) = 'OPEN'
                      AND (a.due_at IS NULL OR a.due_at >= CURRENT_TIMESTAMP)
                    LIMIT 1
                ),
                updated AS (
                    UPDATE assignment_submissions s
                    SET file_name = @file_name,
                        content_type = @content_type,
                        file_size = @file_size,
                        file_content = @file_content,
                        submitted_at = @submitted_at
                    FROM eligible_assignment a
                    WHERE s.assignment_id = a.id
                      AND s.student_id = @student_id
                      AND s.score IS NULL
                      AND UPPER(COALESCE(s.status, '')) <> 'GRADED'
                    RETURNING s.id
                ),
                inserted AS (
                    INSERT INTO assignment_submissions (assignment_id, student_id, file_name, content_type, file_size, file_content, submitted_at)
                    SELECT @assignment_id, @student_id, @file_name, @content_type, @file_size, @file_content, @submitted_at
                    FROM eligible_assignment a
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM assignment_submissions s
                        WHERE s.assignment_id = a.id
                          AND s.student_id = @student_id
                    )
                    RETURNING id
                )
                SELECT (SELECT COUNT(*) FROM updated) + (SELECT COUNT(*) FROM inserted)";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@assignment_id", submission.AssignmentId);
            command.Parameters.AddWithValue("@student_id", submission.StudentId);
            command.Parameters.AddWithValue("@file_name", submission.FileName ?? string.Empty);
            command.Parameters.AddWithValue("@content_type", submission.ContentType ?? string.Empty);
            command.Parameters.AddWithValue("@file_size", submission.FileSize);
            command.Parameters.AddWithValue("@file_content", submission.FileContent ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@submitted_at", DateTime.Now);

            object? result = await command.ExecuteScalarAsync(cancellationToken);
            int rows = result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            return rows > 0;
        }

        /// <summary>
        /// Ensures default seed accounts exist in the database.
        /// Safe to call multiple times (uses ON CONFLICT DO NOTHING).
        /// </summary>
        public void EnsureSeedAccounts()
        {
            try
            {
                using var connection = CreateConnection();
                connection.Open();

                // Ensure "student" account exists (password: admin123, role: STUDENT)
                const string seedQuery = @"
                    INSERT INTO USERS (username, password_hash, full_name, email, role_id, status)
                    SELECT @username, @password_hash, @full_name, @email, 
                           (SELECT id FROM ROLES WHERE UPPER(name) = 'STUDENT'), 'ACTIVE'
                    WHERE NOT EXISTS (SELECT 1 FROM USERS WHERE LOWER(username) = LOWER(@username))";

                using var command = new NpgsqlCommand(seedQuery, connection);
                command.Parameters.AddWithValue("@username", "student");
                command.Parameters.AddWithValue("@password_hash", "240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9");
                command.Parameters.AddWithValue("@full_name", "Student Test");
                command.Parameters.AddWithValue("@email", "student@courseguard.local");
                command.ExecuteNonQuery();
            }
            catch
            {
                // Silently ignore seed errors to avoid blocking app startup
            }
        }

        // --- Bổ sung hạ tầng DB Async cho Lịch học, Ghi chú & Điểm danh ---

        public async Task<QuickNoteModel?> GetQuickNoteAsync(int userId, int sessionId, CancellationToken cancellationToken = default)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            const string query = @"
                SELECT id, user_id, session_id, content, created_at, updated_at
                FROM quick_notes
                WHERE user_id = @user_id AND session_id = @session_id";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@user_id", userId);
            command.Parameters.AddWithValue("@session_id", sessionId);

            using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new QuickNoteModel
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    SessionId = reader.GetInt32(2),
                    Content = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    CreatedAt = reader.GetDateTime(4),
                    UpdatedAt = reader.GetDateTime(5)
                };
            }
            return null;
        }

        public async Task<bool> SaveQuickNoteAsync(QuickNoteModel note, CancellationToken cancellationToken = default)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            const string checkQuery = "SELECT id FROM quick_notes WHERE user_id = @user_id AND session_id = @session_id";
            using var checkCommand = new NpgsqlCommand(checkQuery, connection);
            checkCommand.Parameters.AddWithValue("@user_id", note.UserId);
            checkCommand.Parameters.AddWithValue("@session_id", note.SessionId);
            
            var existingId = await checkCommand.ExecuteScalarAsync(cancellationToken);
            
            if (existingId != null)
            {
                const string updateQuery = @"
                    UPDATE quick_notes 
                    SET content = @content, updated_at = CURRENT_TIMESTAMP
                    WHERE id = @id";
                using var updateCommand = new NpgsqlCommand(updateQuery, connection);
                updateCommand.Parameters.AddWithValue("@id", Convert.ToInt32(existingId));
                updateCommand.Parameters.AddWithValue("@content", string.IsNullOrWhiteSpace(note.Content) ? DBNull.Value : note.Content);
                return await updateCommand.ExecuteNonQueryAsync(cancellationToken) > 0;
            }
            else
            {
                const string insertQuery = @"
                    INSERT INTO quick_notes (user_id, session_id, content, created_at, updated_at)
                    VALUES (@user_id, @session_id, @content, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)";
                using var insertCommand = new NpgsqlCommand(insertQuery, connection);
                insertCommand.Parameters.AddWithValue("@user_id", note.UserId);
                insertCommand.Parameters.AddWithValue("@session_id", note.SessionId);
                insertCommand.Parameters.AddWithValue("@content", string.IsNullOrWhiteSpace(note.Content) ? DBNull.Value : note.Content);
                return await insertCommand.ExecuteNonQueryAsync(cancellationToken) > 0;
            }
        }

        private async Task EnsureScheduleAttendanceSchemaAsync(NpgsqlConnection connection, CancellationToken cancellationToken = default)
        {
            const string query = @"
                ALTER TABLE online_sessions
                    ADD COLUMN IF NOT EXISTS recurring_rule TEXT,
                    ADD COLUMN IF NOT EXISTS is_opened BOOLEAN DEFAULT FALSE,
                    ADD COLUMN IF NOT EXISTS meeting_link TEXT;

                CREATE TABLE IF NOT EXISTS quick_notes (
                    id SERIAL PRIMARY KEY,
                    user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                    session_id INT NOT NULL REFERENCES online_sessions(id) ON DELETE CASCADE,
                    content TEXT,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );

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

                CREATE INDEX IF NOT EXISTS idx_quick_notes_user_session
                    ON quick_notes(user_id, session_id);
                CREATE INDEX IF NOT EXISTS idx_attendance_logs_session_student
                    ON attendance_logs(session_id, student_id);
                CREATE INDEX IF NOT EXISTS idx_attendance_logs_open_session_student
                    ON attendance_logs(session_id, student_id)
                    WHERE left_at IS NULL;
                CREATE INDEX IF NOT EXISTS idx_online_sessions_opened
                    ON online_sessions(is_opened);";

            using var command = new NpgsqlCommand(query, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<int> LogAttendanceInAsync(int studentId, int sessionId, CancellationToken cancellationToken = default)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await EnsureScheduleAttendanceSchemaAsync(connection, cancellationToken);

            const string query = @"
                WITH authorized_session AS (
                    SELECT os.id
                    FROM online_sessions os
                    JOIN courses c ON c.id = os.course_id
                    JOIN enrollments e ON e.course_id = c.id
                    WHERE os.id = @session_id
                      AND e.student_id = @student_id
                      AND UPPER(COALESCE(e.status, '')) IN ('ACTIVE', 'APPROVED')
                      AND UPPER(COALESCE(c.status, '')) = 'ACTIVE'
                      AND COALESCE(os.is_opened, FALSE) = TRUE
                ), open_log AS (
                    SELECT al.id
                    FROM attendance_logs al
                    JOIN authorized_session aus ON aus.id = al.session_id
                    WHERE al.student_id = @student_id
                      AND al.left_at IS NULL
                    ORDER BY al.joined_at DESC
                    LIMIT 1
                ), inserted AS (
                    INSERT INTO attendance_logs (student_id, session_id, joined_at)
                    SELECT @student_id, @session_id, CURRENT_TIMESTAMP
                    WHERE EXISTS (SELECT 1 FROM authorized_session)
                      AND NOT EXISTS (SELECT 1 FROM open_log)
                    RETURNING id
                )
                SELECT id FROM inserted
                UNION ALL
                SELECT id FROM open_log
                LIMIT 1";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@student_id", studentId);
            command.Parameters.AddWithValue("@session_id", sessionId);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result != null ? Convert.ToInt32(result) : 0;
        }

        public async Task<bool> LogAttendanceOutAsync(int logId, CancellationToken cancellationToken = default)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await EnsureScheduleAttendanceSchemaAsync(connection, cancellationToken);

            const string query = @"
                UPDATE attendance_logs
                SET left_at = CURRENT_TIMESTAMP,
                    duration_minutes = GREATEST(CAST(EXTRACT(EPOCH FROM (CURRENT_TIMESTAMP - joined_at)) / 60 AS INTEGER), 0),
                    is_valid = (EXTRACT(EPOCH FROM (CURRENT_TIMESTAMP - joined_at)) / 60) >= 30
                WHERE id = @log_id AND left_at IS NULL";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@log_id", logId);

            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        public async Task<bool> UpdateSessionStatusAsync(int sessionId, bool isOpened, string? meetingLink = null, CancellationToken cancellationToken = default)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await EnsureScheduleAttendanceSchemaAsync(connection, cancellationToken);

            const string query = @"
                UPDATE online_sessions
                SET is_opened = @is_opened,
                    meeting_link = COALESCE(@meeting_link, meeting_link)
                WHERE id = @session_id";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@session_id", sessionId);
            command.Parameters.AddWithValue("@is_opened", isOpened);
            command.Parameters.AddWithValue("@meeting_link", string.IsNullOrWhiteSpace(meetingLink) ? (object)DBNull.Value : meetingLink);

            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        public List<Models.DeviceModel> GetActiveDevices()
        {
            var list = new List<Models.DeviceModel>();
            using var connection = CreateConnection();
            connection.Open();
            const string query = @"
                SELECT d.id, d.user_id, u.username, u.full_name, d.device_name, d.ip_address, d.status, d.last_active
                FROM DEVICES d
                JOIN USERS u ON d.user_id = u.id
                ORDER BY d.last_active DESC";
            using var command = new NpgsqlCommand(query, connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Models.DeviceModel
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Username = reader.GetString(2),
                    UserFullName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    DeviceName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    IpAddress = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    Status = reader.IsDBNull(6) ? "ACTIVE" : reader.GetString(6),
                    LastActive = reader.GetDateTime(7)
                });
            }
            return list;
        }

        public bool UpdateDeviceStatus(int deviceId, string status)
        {
            using var connection = CreateConnection();
            connection.Open();
            const string query = "UPDATE DEVICES SET status = @status WHERE id = @id";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@status", status);
            command.Parameters.AddWithValue("@id", deviceId);
            return command.ExecuteNonQuery() > 0;
        }

        public bool DeleteDevice(int deviceId)
        {
            using var connection = CreateConnection();
            connection.Open();
            const string query = "DELETE FROM DEVICES WHERE id = @id";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@id", deviceId);
            return command.ExecuteNonQuery() > 0;
        }

        public bool IsDeviceBlocked(int userId, string deviceName)
        {
            using var connection = CreateConnection();
            connection.Open();
            const string query = "SELECT 1 FROM DEVICES WHERE user_id = @user_id AND device_name = @device_name AND UPPER(status) = 'BLOCKED'";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@user_id", userId);
            command.Parameters.AddWithValue("@device_name", deviceName);
            var result = command.ExecuteScalar();
            return result != null;
        }

        public async Task<bool> IsDeviceBlockedAsync(int userId, string deviceName, CancellationToken cancellationToken = default)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            const string query = "SELECT 1 FROM DEVICES WHERE user_id = @user_id AND device_name = @device_name AND UPPER(status) = 'BLOCKED'";
            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@user_id", userId);
            command.Parameters.AddWithValue("@device_name", deviceName);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result != null;
        }

        public void InsertGeneratedSessions(int courseId, int teacherId, string name, DateTime? startDate, DateTime? endDate, string? teachingDaysStr, TimeSpan? sessionStartTime, TimeSpan? sessionEndTime, string? meetingLink)
        {
            if (!startDate.HasValue || !endDate.HasValue) return;
            if (!sessionStartTime.HasValue || !sessionEndTime.HasValue) return;
            if (string.IsNullOrEmpty(teachingDaysStr)) return;

            var days = teachingDaysStr.Split(',', System.StringSplitOptions.RemoveEmptyEntries)
                                      .Select(d => System.Enum.Parse<System.DayOfWeek>(d.Trim()))
                                      .ToList();
            if (days.Count == 0) return;

            System.DateTime startD = startDate.Value.Date;
            System.DateTime endD = endDate.Value.Date;
            if (endD < startD || sessionEndTime.Value <= sessionStartTime.Value) return;

            using var connection = CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                for (System.DateTime day = startD; day <= endD; day = day.AddDays(1))
                {
                    if (!days.Contains(day.DayOfWeek)) continue;

                    System.DateTime sessionStart = day.Add(sessionStartTime.Value);
                    System.DateTime sessionEnd = day.Add(sessionEndTime.Value);

                    using var cmd = new NpgsqlCommand(@"
                        INSERT INTO online_sessions (course_id, title, start_time, end_time, meeting_link, created_by)
                        VALUES (@course_id, @title, @start_time, @end_time, @meeting_link, @teacher_id)", connection, transaction);
                    cmd.Parameters.AddWithValue("@course_id", courseId);
                    cmd.Parameters.AddWithValue("@title", string.IsNullOrWhiteSpace(name) ? "Buổi học" : name);
                    cmd.Parameters.AddWithValue("@start_time", sessionStart);
                    cmd.Parameters.AddWithValue("@end_time", sessionEnd);
                    cmd.Parameters.AddWithValue("@meeting_link", string.IsNullOrWhiteSpace(meetingLink) ? System.DBNull.Value : meetingLink);
                    cmd.Parameters.AddWithValue("@teacher_id", teacherId);
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public bool HasTeacherConflict(int teacherId, DateTime startDate, DateTime endDate, string teachingDaysStr, TimeSpan startTime, TimeSpan endTime, int? excludeCourseId = null)
        {
            if (string.IsNullOrEmpty(teachingDaysStr)) return false;

            var days = teachingDaysStr.Split(',', System.StringSplitOptions.RemoveEmptyEntries)
                                      .Select(d => System.Enum.Parse<System.DayOfWeek>(d.Trim()))
                                      .ToList();

            var proposedSessions = new System.Collections.Generic.List<(System.DateTime Start, System.DateTime End)>();
            for (System.DateTime day = startDate.Date; day <= endDate.Date; day = day.AddDays(1))
            {
                if (!days.Contains(day.DayOfWeek)) continue;
                proposedSessions.Add((day.Add(startTime), day.Add(endTime)));
            }

            if (proposedSessions.Count == 0) return false;

            using var connection = CreateConnection();
            connection.Open();

            const string query = @"
                SELECT os.start_time, os.end_time
                FROM online_sessions os
                JOIN courses c ON c.id = os.course_id
                WHERE c.teacher_id = @teacher_id
                  AND (@exclude_course_id IS NULL OR c.id <> @exclude_course_id)
                  AND UPPER(COALESCE(c.status, '')) IN ('ACTIVE', 'APPROVED')";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@teacher_id", teacherId);
            command.Parameters.AddWithValue("@exclude_course_id", excludeCourseId.HasValue ? excludeCourseId.Value : (object)System.DBNull.Value);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                System.DateTime existStart = reader.GetDateTime(0);
                System.DateTime existEnd = reader.GetDateTime(1);

                foreach (var prop in proposedSessions)
                {
                    if (prop.Start < existEnd && prop.End > existStart)
                    {
                        return true; // Conflict!
                    }
                }
            }

            return false;
        }

        public bool HasStudentConflict(int studentId, int courseId)
        {
            using var connection = CreateConnection();
            connection.Open();

            var targetSessions = new System.Collections.Generic.List<(System.DateTime Start, System.DateTime End)>();
            const string targetQuery = "SELECT start_time, end_time FROM online_sessions WHERE course_id = @course_id";
            using (var cmd = new NpgsqlCommand(targetQuery, connection))
            {
                cmd.Parameters.AddWithValue("@course_id", courseId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                        targetSessions.Add((reader.GetDateTime(0), reader.GetDateTime(1)));
                }
            }

            if (targetSessions.Count == 0) return false;

            const string existQuery = @"
                SELECT os.start_time, os.end_time
                FROM online_sessions os
                JOIN courses c ON c.id = os.course_id
                JOIN enrollments e ON e.course_id = c.id
                WHERE e.student_id = @student_id
                  AND e.course_id <> @course_id
                  AND UPPER(COALESCE(e.status, '')) IN ('ACTIVE', 'APPROVED')
                  AND UPPER(COALESCE(c.status, '')) IN ('ACTIVE', 'APPROVED', 'OPEN')";

            using (var cmd = new NpgsqlCommand(existQuery, connection))
            {
                cmd.Parameters.AddWithValue("@student_id", studentId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                    {
                        System.DateTime existStart = reader.GetDateTime(0);
                        System.DateTime existEnd = reader.GetDateTime(1);

                        foreach (var target in targetSessions)
                        {
                            if (target.Start < existEnd && target.End > existStart)
                            {
                                return true; // Conflict!
                            }
                        }
                    }
                }
            }

            return false;
        }

        public System.Collections.Generic.List<int> GetCourseStudents(int courseId)
        {
            var list = new System.Collections.Generic.List<int>();
            using var connection = CreateConnection();
            connection.Open();
            const string query = "SELECT student_id FROM enrollments WHERE course_id = @course_id AND UPPER(COALESCE(status, '')) IN ('ACTIVE', 'APPROVED')";
            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@course_id", courseId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(reader.GetInt32(0));
            }
            return list;
        }
    }
}
