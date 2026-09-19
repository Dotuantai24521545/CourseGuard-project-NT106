-- Migration: 20260601_1000_schedule_attendance_notes.sql
-- Description: Add recurring, is_opened, quick_notes, attendance_logs for Schedule feature.

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
    joined_at TIMESTAMP NOT NULL,
    left_at TIMESTAMP,
    duration_minutes INT DEFAULT 0,
    is_valid BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Thêm index để tối ưu truy vấn thời gian thực
CREATE INDEX IF NOT EXISTS idx_quick_notes_user_session ON quick_notes(user_id, session_id);
CREATE INDEX IF NOT EXISTS idx_attendance_logs_session_student ON attendance_logs(session_id, student_id);
CREATE INDEX IF NOT EXISTS idx_attendance_logs_open_session_student ON attendance_logs(session_id, student_id)
    WHERE left_at IS NULL;
