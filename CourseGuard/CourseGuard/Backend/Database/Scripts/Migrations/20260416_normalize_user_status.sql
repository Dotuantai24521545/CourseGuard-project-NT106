-- 2026-04-16: normalize status values for schema-compatible filtering
-- Run once after deploying latest backend changes.

BEGIN;

UPDATE USERS
SET STATUS = UPPER(COALESCE(STATUS, 'ACTIVE'))
WHERE STATUS IS NULL
   OR STATUS <> UPPER(STATUS);

COMMIT;
