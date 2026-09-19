using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CourseGuard.Backend.Config;
using CourseGuard.Backend.Models;
using Npgsql;

namespace CourseGuard.Backend.Data
{
    public class ViolationRepository
    {
        private readonly string _connectionString = AppEnvironment.GetRequired(
            "COURSEGUARD_DB_CONNECTION",
            "SUPABASE_DB_CONNECTION",
            "CONNECTION_STRING");

        public async Task<int> InsertViolationAsync(ViolationModel violation)
        {
            const string sql = @"
                INSERT INTO violations (user_id, exam_attempt_id, type, severity, action_taken, image_url)
                VALUES (@userId, @attemptId, @type, @severity, @actionTaken, @imageUrl)
                RETURNING id;";

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            await EnsureViolationSchemaAsync(conn);

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@userId", violation.UserId);
            cmd.Parameters.AddWithValue("@attemptId", violation.ExamAttemptId.HasValue ? violation.ExamAttemptId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@type", violation.Type);
            cmd.Parameters.AddWithValue("@severity", string.IsNullOrWhiteSpace(violation.Severity) ? "MEDIUM" : violation.Severity);
            cmd.Parameters.AddWithValue("@actionTaken", string.IsNullOrWhiteSpace(violation.ActionTaken) ? DBNull.Value : violation.ActionTaken);
            cmd.Parameters.AddWithValue("@imageUrl", string.IsNullOrEmpty(violation.ImageUrl) ? DBNull.Value : violation.ImageUrl);

            var id = await cmd.ExecuteScalarAsync();
            return id != null ? Convert.ToInt32(id) : 0;
        }

        public async Task<List<ViolationModel>> GetViolationsByAttemptIdAsync(int attemptId)
        {
            var list = new List<ViolationModel>();
            const string sql = @"
                SELECT id, user_id, exam_attempt_id, type, severity, action_taken, image_url, created_at
                FROM violations
                WHERE exam_attempt_id = @attemptId
                ORDER BY created_at DESC;";

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            await EnsureViolationSchemaAsync(conn);

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@attemptId", attemptId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new ViolationModel
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    ExamAttemptId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                    Type = reader.GetString(3),
                    Severity = reader.GetString(4),
                    ActionTaken = reader.IsDBNull(5) ? null : reader.GetString(5),
                    ImageUrl = reader.IsDBNull(6) ? null : reader.GetString(6),
                    CreatedAt = reader.GetDateTime(7)
                });
            }

            return list;
        }

        public async Task<int> CountViolationsByAttemptIdAsync(int attemptId)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            await EnsureViolationSchemaAsync(conn);

            const string sql = "SELECT COUNT(*)::int FROM violations WHERE exam_attempt_id = @attemptId;";
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@attemptId", attemptId);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
        }

        private static async Task EnsureViolationSchemaAsync(NpgsqlConnection conn)
        {
            const string sql = @"
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
                END $$;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();

            try
            {
                const string publicationSql = @"
                    DO $$
                    BEGIN
                        IF EXISTS (SELECT 1 FROM pg_publication WHERE pubname = 'supabase_realtime') THEN
                            IF NOT EXISTS (
                                SELECT 1 FROM pg_publication_tables 
                                WHERE pubname = 'supabase_realtime' AND tablename = 'violations'
                            ) THEN
                                ALTER PUBLICATION supabase_realtime ADD TABLE violations;
                            END IF;
                        END IF;
                    END $$;";
                await using var pubCmd = new NpgsqlCommand(publicationSql, conn);
                await pubCmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Bỏ qua lỗi phân quyền trong môi trường DB bị hạn chế
            }
        }
    }
}
