using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using Npgsql;
internal sealed class MaterialStore(CourseGuardDbContext _dbContext)
{
        public List<StudentDocumentRow> QueryDocuments(int studentId)
        {
            var result = new List<StudentDocumentRow>();

            using var connection = _dbContext.CreateConnection();
            connection.Open();
            EnsureMaterialContentSchema(connection);

            const string query = @"
                SELECT m.ID,
                       c.NAME AS COURSE_NAME,
                       COALESCE(m.FILE_NAME, '') AS FILE_NAME,
                       COALESCE(m.FILE_PATH, '') AS FILE_PATH,
                       COALESCE(u.FULL_NAME, u.USERNAME, 'Không rõ') AS UPLOADED_BY,
                       m.UPLOADED_AT,
                       COALESCE(m.CONTENT_TYPE, '') AS CONTENT_TYPE,
                       COALESCE(m.FILE_SIZE, 0) AS FILE_SIZE,
                       m.FILE_CONTENT IS NOT NULL AS HAS_CONTENT
                FROM MATERIALS m
                JOIN COURSES c ON c.ID = m.COURSE_ID
                JOIN ENROLLMENTS e ON e.COURSE_ID = c.ID
                LEFT JOIN USERS u ON u.ID = m.UPLOADED_BY
                WHERE e.STUDENT_ID = @student_id
                  AND UPPER(COALESCE(e.STATUS, '')) IN ('ACTIVE', 'APPROVED')
                  AND UPPER(COALESCE(c.STATUS, 'ACTIVE')) IN ('ACTIVE', 'APPROVED', 'OPEN')
                ORDER BY m.UPLOADED_AT DESC, m.ID DESC";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@student_id", studentId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                string path = reader.GetString(3);
                result.Add(new StudentDocumentRow
                {
                    Id = reader.GetInt32(0),
                    CourseName = reader.GetString(1),
                    FileName = reader.GetString(2),
                    FilePath = path,
                    FileType = GetFileType(path, reader.GetString(2)),
                    UploadedBy = reader.GetString(4),
                    UploadedAt = reader.IsDBNull(5) ? DateTime.MinValue : reader.GetDateTime(5),
                    ContentType = reader.GetString(6),
                    FileSize = reader.GetInt64(7),
                    HasStoredContent = reader.GetBoolean(8)
                });
            }

            return result;
        }
        public byte[]? LoadDocumentContent(int materialId, int studentId)
        {

            using var connection = _dbContext.CreateConnection();
            connection.Open();
            EnsureMaterialContentSchema(connection);
            using var command = new NpgsqlCommand(@"
                SELECT m.file_content
                FROM materials m
                JOIN courses c ON c.id = m.course_id
                JOIN enrollments e ON e.course_id = c.id
                WHERE m.id = @material_id
                  AND e.student_id = @student_id
                  AND UPPER(COALESCE(e.status, '')) IN ('ACTIVE', 'APPROVED')
                  AND UPPER(COALESCE(c.status, 'ACTIVE')) IN ('ACTIVE', 'APPROVED', 'OPEN')", connection);
            command.Parameters.AddWithValue("@material_id", materialId);
            command.Parameters.AddWithValue("@student_id", studentId);
            object? value = command.ExecuteScalar();
            return value == null || value == DBNull.Value ? null : (byte[])value;
        }
        private static void EnsureMaterialContentSchema(NpgsqlConnection connection)
        {
            using var command = new NpgsqlCommand(@"
                ALTER TABLE materials
                    ADD COLUMN IF NOT EXISTS content_type VARCHAR(120),
                    ADD COLUMN IF NOT EXISTS file_size BIGINT NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS file_content BYTEA;", connection);
            command.ExecuteNonQuery();
        }
        private static string GetFileType(string path, string fileName)
        {
            string extension = Path.GetExtension(path);
            if (string.IsNullOrWhiteSpace(extension))
                extension = Path.GetExtension(fileName);

            return string.IsNullOrWhiteSpace(extension)
                ? "Không rõ"
                : extension.TrimStart('.').ToUpperInvariant();
        }
}
