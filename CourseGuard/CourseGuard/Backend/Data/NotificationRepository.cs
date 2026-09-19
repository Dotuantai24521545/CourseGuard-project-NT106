using System;
using System.Collections.Generic;
using System.Data;
using CourseGuard.Backend.Models;

namespace CourseGuard.Backend.Data
{
    public class NotificationRepository : INotificationWriter
    {
        public NotificationRepository()
        {
            EnsureNotificationSchema();
        }

        public List<NotificationModel> LoadAll()
        {
            const string sql = @"
                SELECT id, user_id, title, content, is_read, created_at,
                       COALESCE(category, 'SystemAdmin') AS category,
                       COALESCE(notification_type, 'Informational') AS notification_type,
                       COALESCE(source_type, '') AS source_type,
                       source_id
                FROM notifications
                ORDER BY created_at DESC;";

            return MapRows(DatabaseAction.ExecuteQuery(sql, null));
        }

        public List<NotificationModel> LoadByUserId(int userId, string? category = null, string? notificationType = null)
        {
            string sql = @"
                SELECT n.id, n.user_id, n.title, n.content, n.is_read, n.created_at,
                       COALESCE(n.category, 'SystemAdmin') AS category,
                       COALESCE(n.notification_type, 'Informational') AS notification_type,
                       COALESCE(n.source_type, '') AS source_type,
                       n.source_id,
                       resolved_course.id AS course_id,
                       COALESCE(resolved_course.name, '') AS course_name
                FROM notifications n
                LEFT JOIN courses source_course
                       ON COALESCE(n.source_type, '') = 'Course'
                      AND n.source_id = source_course.id
                LEFT JOIN teacher_assignments source_assignment
                       ON COALESCE(n.source_type, '') = 'Assignment'
                      AND n.source_id = source_assignment.id
                LEFT JOIN exams source_exam
                       ON COALESCE(n.source_type, '') = 'Exam'
                      AND n.source_id = source_exam.id
                LEFT JOIN materials source_material
                       ON COALESCE(n.source_type, '') = 'Material'
                      AND n.source_id = source_material.id
                LEFT JOIN courses resolved_course
                       ON resolved_course.id = COALESCE(
                           source_course.id,
                           source_assignment.course_id,
                           source_exam.course_id,
                           source_material.course_id)
                WHERE n.user_id = @userId";

            var parameters = new Dictionary<string, (SqlDbType, object)>
            {
                { "@userId", (SqlDbType.Int, userId) }
            };

            if (!string.IsNullOrWhiteSpace(category))
            {
                sql += " AND n.category = @category";
                parameters.Add("@category", (SqlDbType.VarChar, category));
            }

            if (!string.IsNullOrWhiteSpace(notificationType))
            {
                sql += " AND n.notification_type = @notification_type";
                parameters.Add("@notification_type", (SqlDbType.VarChar, notificationType));
            }

            sql += " ORDER BY n.created_at DESC;";
            return MapRows(DatabaseAction.ExecuteQuery(sql, parameters));
        }

        public int Create(
            int userId,
            string title,
            string content,
            string category,
            string notificationType,
            string? sourceType = null,
            int? sourceId = null)
        {
            const string sql = @"
                INSERT INTO notifications (user_id, title, content, is_read, created_at, category, notification_type, source_type, source_id)
                VALUES (@user_id, @title, @content, false, CURRENT_TIMESTAMP, @category, @notification_type, @source_type, @source_id)
                RETURNING id;";

            var parameters = new Dictionary<string, (SqlDbType, object)>
            {
                { "@user_id", (SqlDbType.Int, userId) },
                { "@title", (SqlDbType.VarChar, title) },
                { "@content", (SqlDbType.VarChar, content) },
                { "@category", (SqlDbType.VarChar, string.IsNullOrWhiteSpace(category) ? WorkflowConstants.NotificationCategory.SystemAdmin : category) },
                { "@notification_type", (SqlDbType.VarChar, string.IsNullOrWhiteSpace(notificationType) ? WorkflowConstants.NotificationType.Informational : notificationType) },
                { "@source_type", (SqlDbType.VarChar, string.IsNullOrWhiteSpace(sourceType) ? DBNull.Value : sourceType) },
                { "@source_id", (SqlDbType.Int, sourceId.HasValue ? sourceId.Value : DBNull.Value) }
            };

            object? result = DatabaseAction.ExecuteScalar(sql, parameters);
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        public bool MarkAsRead(int notificationId)
        {
            const string sql = @"
                UPDATE notifications
                SET is_read = true
                WHERE id = @id;";

            var parameters = new Dictionary<string, (SqlDbType, object)>
            {
                { "@id", (SqlDbType.Int, notificationId) }
            };

            return DatabaseAction.ExecuteNonQuery(sql, parameters) > 0;
        }

        public bool Delete(int notificationId)
        {
            const string sql = @"
                DELETE FROM notifications
                WHERE id = @id;";

            var parameters = new Dictionary<string, (SqlDbType, object)>
            {
                { "@id", (SqlDbType.Int, notificationId) }
            };

            return DatabaseAction.ExecuteNonQuery(sql, parameters) > 0;
        }

        private static List<NotificationModel> MapRows(DataTable dt)
        {
            var list = new List<NotificationModel>();
            foreach (DataRow row in dt.Rows)
                list.Add(MapRow(row));
            return list;
        }

        private static NotificationModel MapRow(DataRow row)
        {
            return new NotificationModel
            {
                Id = Convert.ToInt32(row["id"]),
                UserId = Convert.ToInt32(row["user_id"]),
                Title = row["title"] == DBNull.Value ? string.Empty : Convert.ToString(row["title"]) ?? string.Empty,
                Content = row["content"] == DBNull.Value ? string.Empty : Convert.ToString(row["content"]) ?? string.Empty,
                IsRead = Convert.ToBoolean(row["is_read"]),
                CreatedAt = Convert.ToDateTime(row["created_at"]),
                Category = row["category"] == DBNull.Value ? WorkflowConstants.NotificationCategory.SystemAdmin : Convert.ToString(row["category"]) ?? WorkflowConstants.NotificationCategory.SystemAdmin,
                NotificationType = row["notification_type"] == DBNull.Value ? WorkflowConstants.NotificationType.Informational : Convert.ToString(row["notification_type"]) ?? WorkflowConstants.NotificationType.Informational,
                SourceType = row["source_type"] == DBNull.Value ? string.Empty : Convert.ToString(row["source_type"]) ?? string.Empty,
                SourceId = row["source_id"] == DBNull.Value ? null : Convert.ToInt32(row["source_id"]),
                CourseId = row.Table.Columns.Contains("course_id") && row["course_id"] != DBNull.Value ? Convert.ToInt32(row["course_id"]) : null,
                CourseName = row.Table.Columns.Contains("course_name") && row["course_name"] != DBNull.Value ? Convert.ToString(row["course_name"]) ?? string.Empty : string.Empty
            };
        }

        private static void EnsureNotificationSchema()
        {
            const string sql = @"
                ALTER TABLE notifications
                    ADD COLUMN IF NOT EXISTS category VARCHAR(32) NOT NULL DEFAULT 'SystemAdmin',
                    ADD COLUMN IF NOT EXISTS notification_type VARCHAR(32) NOT NULL DEFAULT 'Informational',
                    ADD COLUMN IF NOT EXISTS source_type VARCHAR(64),
                    ADD COLUMN IF NOT EXISTS source_id INT;

                CREATE INDEX IF NOT EXISTS idx_notifications_user_read_created
                    ON notifications(user_id, is_read, created_at DESC);
                CREATE INDEX IF NOT EXISTS idx_notifications_category
                    ON notifications(category);";

            DatabaseAction.ExecuteNonQuery(sql, null);
        }
    }
}
