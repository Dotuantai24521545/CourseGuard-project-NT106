-- Adds poll bump support for chat poll messages.
-- Safe to run multiple times on PostgreSQL / Supabase.

ALTER TABLE MESSAGES
ADD COLUMN IF NOT EXISTS POLL_ID INT;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE LOWER(table_name) = 'messages'
          AND LOWER(constraint_name) = 'fk_messages_polls'
    ) THEN
        ALTER TABLE MESSAGES
        ADD CONSTRAINT FK_MESSAGES_POLLS
        FOREIGN KEY (POLL_ID) REFERENCES POLLS(ID) ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS IDX_MESSAGES_POLL_ID
ON MESSAGES(POLL_ID);

CREATE INDEX IF NOT EXISTS IDX_MESSAGES_POLL_BUMP_COOLDOWN
ON MESSAGES(POLL_ID, MESSAGE_TYPE, SENT_AT DESC)
WHERE MESSAGE_TYPE = 'POLL_BUMP';
