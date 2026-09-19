-- 2026-06-15: add temporary password expiry support.
-- Run once after deploying the matching application build.

BEGIN;

ALTER TABLE users
    ADD COLUMN IF NOT EXISTS temp_password_expires_at TIMESTAMP;

CREATE INDEX IF NOT EXISTS idx_users_temp_password_expires_at
    ON users (temp_password_expires_at)
    WHERE temp_password_expires_at IS NOT NULL;

COMMIT;
