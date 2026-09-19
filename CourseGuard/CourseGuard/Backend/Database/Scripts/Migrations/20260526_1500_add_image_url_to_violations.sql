-- Migration: Thêm cột IMAGE_URL vào bảng VIOLATIONS
-- Ngày: 2026-05-26
-- Mục đích: Lưu trữ ảnh chụp màn hình vi phạm (upload lên Supabase Storage bucket 'exam-violations')

BEGIN;

ALTER TABLE violations ADD COLUMN IF NOT EXISTS image_url VARCHAR(500) DEFAULT NULL;

COMMENT ON COLUMN violations.image_url IS 'URL ảnh chụp màn hình vi phạm lấy từ Supabase Storage';

COMMIT;
