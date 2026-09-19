ALTER TABLE notifications
    ADD COLUMN IF NOT EXISTS category VARCHAR(32) NOT NULL DEFAULT 'SystemAdmin',
    ADD COLUMN IF NOT EXISTS notification_type VARCHAR(32) NOT NULL DEFAULT 'Informational',
    ADD COLUMN IF NOT EXISTS source_type VARCHAR(64),
    ADD COLUMN IF NOT EXISTS source_id INT;

ALTER TABLE courses
    ADD COLUMN IF NOT EXISTS rejection_reason TEXT;

UPDATE courses
SET status = 'ACTIVE'
WHERE status IS NULL OR TRIM(status) = '';

UPDATE enrollments
SET status = 'PENDING'
WHERE status IS NULL OR TRIM(status) = '';

CREATE INDEX IF NOT EXISTS idx_notifications_user_read_created
    ON notifications(user_id, is_read, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_notifications_category
    ON notifications(category);

CREATE INDEX IF NOT EXISTS idx_courses_status
    ON courses(status);

CREATE INDEX IF NOT EXISTS idx_online_sessions_course_start
    ON online_sessions(course_id, start_time);
