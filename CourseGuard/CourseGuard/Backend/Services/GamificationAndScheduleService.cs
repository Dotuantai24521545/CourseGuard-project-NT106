using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using CourseGuard.Backend.Data;
using Npgsql;

namespace CourseGuard.Backend.Services
{
    public class LeaderboardItem
    {
        public int Rank { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public decimal AverageScore { get; set; }
        public int CompletedExams { get; set; }
        public string BadgeTitle { get; set; } = string.Empty;
    }

    public class StudentBadge
    {
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = "🏅";
        public string Description { get; set; } = string.Empty;
    }

    public class GamificationAndScheduleService
    {
        private readonly CourseGuardDbContext _dbContext = new("");

        // 🥇 4. Bảng Xếp Hạng & Hệ Thống Huy Hiệu (Leaderboards & Badges)
        public async Task<List<LeaderboardItem>> GetCourseLeaderboardAsync(int courseId)
        {
            var list = new List<LeaderboardItem>();
            try
            {
                using var connection = _dbContext.CreateConnection();
                await connection.OpenAsync();

                string sql = @"
                    SELECT u.id, COALESCE(u.full_name, u.username), COALESCE(AVG(es.score), 0) as avg_score, COUNT(DISTINCT es.exam_id) as total_exams
                    FROM users u
                    JOIN enrollments en ON en.student_id = u.id
                    LEFT JOIN exam_attempts es ON es.student_id = u.id
                        AND es.exam_id IN (SELECT id FROM exams WHERE course_id = @course_id)
                        AND UPPER(es.status) = 'SUBMITTED'
                    WHERE en.course_id = @course_id AND UPPER(u.status) = 'ACTIVE'
                        AND UPPER(en.status) IN ('ACTIVE', 'APPROVED')
                    GROUP BY u.id, u.full_name, u.username
                    ORDER BY avg_score DESC, total_exams DESC, u.id
                    LIMIT 10;";

                using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("@course_id", courseId);

                using var reader = await command.ExecuteReaderAsync();
                int rank = 1;
                while (await reader.ReadAsync())
                {
                    decimal avg = reader.GetDecimal(2);
                    string badge = rank == 1 ? "🥇 Thủ khoa" : (rank <= 3 ? "🥈 Ong thợ" : "🏅 Chăm chỉ");
                    list.Add(new LeaderboardItem
                    {
                        Rank = rank++,
                        StudentId = reader.GetInt32(0),
                        StudentName = reader.GetString(1),
                        AverageScore = Math.Round(avg, 2),
                        CompletedExams = Convert.ToInt32(reader.GetInt64(3)),
                        BadgeTitle = badge
                    });
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Không thể tải bảng xếp hạng từ database.", ex);
            }
            return list;
        }

        public List<StudentBadge> GetStudentBadges(int attendanceRate, decimal avgScore)
        {
            var badges = new List<StudentBadge>();
            if (attendanceRate >= 90)
            {
                badges.Add(new StudentBadge { Title = "Chăm chỉ", Icon = "⭐", Description = "Tham gia trên 90% buổi học online" });
            }
            if (avgScore >= 8.5m)
            {
                badges.Add(new StudentBadge { Title = "Thủ khoa", Icon = "👑", Description = "Đạt điểm trung bình môn từ 8.5 trở lên" });
            }
            badges.Add(new StudentBadge { Title = "Ong thợ", Icon = "🐝", Description = "Hoàn thành bài tập về nhà đúng deadline" });
            return badges;
        }

        // ⚠️ 6. Tự động Phát hiện Trùng Lịch Thi (Schedule Conflict Detector)
        public async Task<bool> HasExamScheduleConflictAsync(int studentId, DateTime startTime, DateTime endTime, int currentExamId)
        {
            if (endTime <= startTime)
                throw new ArgumentException("Thời gian kết thúc phải sau thời gian bắt đầu.", nameof(endTime));
            try
            {
                using var connection = _dbContext.CreateConnection();
                await connection.OpenAsync();

                string sql = @"
                    SELECT COUNT(*)
                    FROM exams ex
                    JOIN enrollments en ON en.course_id = ex.course_id
                    WHERE en.student_id = @student_id 
                      AND ex.id <> @current_exam_id
                      AND UPPER(en.status) IN ('ACTIVE', 'APPROVED')
                      AND (@start_time < ex.close_time AND @end_time > ex.open_time);";

                using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("@student_id", studentId);
                command.Parameters.AddWithValue("@current_exam_id", currentExamId);
                command.Parameters.AddWithValue("@start_time", startTime);
                command.Parameters.AddWithValue("@end_time", endTime);

                long count = Convert.ToInt64(await command.ExecuteScalarAsync() ?? 0);
                return count > 0;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Không thể kiểm tra trùng lịch thi từ database.", ex);
            }
        }
    }
}
