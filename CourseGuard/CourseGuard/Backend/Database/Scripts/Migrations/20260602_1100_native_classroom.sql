-- Migration: 20260602_1100_native_classroom.sql
-- Description: Native online classroom schema for Teams-like TCP classroom control.

ALTER TABLE online_sessions
    ADD COLUMN IF NOT EXISTS is_opened BOOLEAN DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS meeting_link TEXT,
    ADD COLUMN IF NOT EXISTS room_code VARCHAR(100);

CREATE TABLE IF NOT EXISTS classroom_participants (
    id SERIAL PRIMARY KEY,
    session_id INT NOT NULL REFERENCES online_sessions(id) ON DELETE CASCADE,
    user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    display_name VARCHAR(150),
    role VARCHAR(20) NOT NULL,
    is_online BOOLEAN DEFAULT FALSE,
    is_hand_raised BOOLEAN DEFAULT FALSE,
    is_muted BOOLEAN DEFAULT FALSE,
    is_camera_on BOOLEAN DEFAULT FALSE,
    joined_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    left_at TIMESTAMP,
    last_seen_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS classroom_messages (
    id SERIAL PRIMARY KEY,
    session_id INT NOT NULL REFERENCES online_sessions(id) ON DELETE CASCADE,
    sender_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    sender_name VARCHAR(150),
    message TEXT NOT NULL,
    sent_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_classroom_participants_session
    ON classroom_participants(session_id);

CREATE INDEX IF NOT EXISTS idx_classroom_participants_session_user
    ON classroom_participants(session_id, user_id);

CREATE INDEX IF NOT EXISTS idx_classroom_participants_online
    ON classroom_participants(session_id, is_online);

CREATE INDEX IF NOT EXISTS idx_classroom_messages_session_sent
    ON classroom_messages(session_id, sent_at DESC);
