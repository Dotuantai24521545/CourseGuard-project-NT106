-- 2026-04-15: data consistency constraints for enrollment/device flows
-- Run once in production with privileged DB account.

BEGIN;

-- Remove duplicate enrollment rows before adding unique constraint.
WITH ranked_enrollments AS (
    SELECT id,
           ROW_NUMBER() OVER (PARTITION BY course_id, student_id ORDER BY id) AS rn
    FROM enrollments
)
DELETE FROM enrollments
WHERE id IN (SELECT id FROM ranked_enrollments WHERE rn > 1);

CREATE UNIQUE INDEX IF NOT EXISTS uq_enrollments_course_student
ON enrollments (course_id, student_id);

CREATE INDEX IF NOT EXISTS idx_devices_user_device
ON devices (user_id, device_name);

COMMIT;
