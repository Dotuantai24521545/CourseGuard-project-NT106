/*
 * ScoreRepository.cs
 *
 * Layer: Infrastructure / Data
 * Vai trò: Xử lý tất cả thao tác CSDL liên quan đến điểm sinh viên (vw_student_scores / student_scores).
 *   - LoadAll: Lấy toàn bộ danh sách điểm.
 *   - UpsertMany: Thêm mới hoặc cập nhật nhiều bản ghi (dựa trên conflict key 'mssv').
 * Sử dụng: Được gọi bởi UC_ScoreManagement.
 *
 * Lưu ý quan trọng về schema:
 *   - "vw_student_scores" là VIEW -> chỉ đọc được (SELECT).
 *   - UPSERT phải thực hiện trên TABLE gốc (ví dụ: "student_scores").
 *   - Script SQL tạo table/view đính kèm dưới đây (phần comment).
 */
using System;
using System.Collections.Generic;
using CourseGuard.Backend.Config;
using Npgsql;
using CourseGuard.Backend.Models;

namespace CourseGuard.Backend.Data
{
    /// <summary>
    /// Lớp Repository quản lý thao tác CSDL cho bảng/view điểm sinh viên.
    /// </summary>
    public class ScoreRepository
    {
        // --- Chuỗi kết nối Supabase ---
        // Sử dụng cùng một chuỗi kết nối với phần còn lại của dự án.
        // Port 6543 là Transaction Pooler của Supabase (phù hợp cho ứng dụng desktop).
        private readonly string _connectionString = AppEnvironment.GetRequired(
            "COURSEGUARD_DB_CONNECTION",
            "SUPABASE_DB_CONNECTION",
            "CONNECTION_STRING");

        // =====================================================================
        // SCRIPT SQL GỢI Ý (Chạy trên Supabase SQL Editor nếu chưa có):
        // ---------------------------------------------------------------------
        // -- Table lưu điểm gốc
        // CREATE TABLE IF NOT EXISTS public.student_scores (
        //     id       BIGSERIAL PRIMARY KEY,
        //     mssv     TEXT      NOT NULL UNIQUE,
        //     ho_ten   TEXT      NOT NULL,
        //     lop      TEXT      NOT NULL DEFAULT '',
        //     diem_gk  FLOAT8    NOT NULL DEFAULT 0,
        //     diem_ck  FLOAT8    NOT NULL DEFAULT 0
        // );
        //
        // -- View để query (tên khớp với yêu cầu đề bài)
        // CREATE OR REPLACE VIEW public.vw_student_scores AS
        //     SELECT id, mssv, ho_ten, lop, diem_gk, diem_ck
        //     FROM public.student_scores
        //     ORDER BY mssv;
        // =====================================================================

        /// <summary>
        /// Lấy toàn bộ danh sách điểm từ view "vw_student_scores".
        /// Tự động tạo table và view nếu chưa tồn tại trên Supabase (self-healing).
        /// </summary>
        /// <returns>Danh sách <see cref="StudentScoreModel"/>. Trả về rỗng nếu chưa có dữ liệu.</returns>
        public List<StudentScoreModel> LoadAll()
        {
            var list = new List<StudentScoreModel>();

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            // --- Bước 1: Tự động tạo table + view nếu chưa tồn tại ---
            // Dùng IF NOT EXISTS nên hoàn toàn an toàn khi chạy nhiều lần.
            EnsureSchemaExists(conn);

            // --- Bước 2: Query dữ liệu từ view ---
            const string sql = @"
                SELECT id, mssv, ho_ten, lop, diem_gk, diem_ck
                FROM public.vw_student_scores
                ORDER BY mssv;";

            using var cmd    = new NpgsqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(MapRow(reader));
            }

            return list;
        }

        /// <summary>
        /// Đảm bảo table "student_scores" và view "vw_student_scores" đã tồn tại trên CSDL.
        /// Nếu chưa → tạo mới. Nếu rồi → không làm gì (IF NOT EXISTS).
        /// </summary>
        private static void EnsureSchemaExists(NpgsqlConnection conn)
        {
            // Tạo table gốc lưu điểm (UNIQUE trên mssv để UPSERT hoạt động đúng)
            const string createTable = @"
                CREATE TABLE IF NOT EXISTS public.student_scores (
                    id      BIGSERIAL PRIMARY KEY,
                    mssv    TEXT   NOT NULL UNIQUE,
                    ho_ten  TEXT   NOT NULL DEFAULT '',
                    lop     TEXT   NOT NULL DEFAULT '',
                    diem_gk FLOAT8 NOT NULL DEFAULT 0,
                    diem_ck FLOAT8 NOT NULL DEFAULT 0
                );";

            // Tạo view để đọc dữ liệu (DROP + CREATE để luôn up-to-date)
            const string createView = @"
                CREATE OR REPLACE VIEW public.vw_student_scores AS
                    SELECT id, mssv, ho_ten, lop, diem_gk, diem_ck
                    FROM public.student_scores
                    ORDER BY mssv;";

            using (var cmd = new NpgsqlCommand(createTable, conn))
                cmd.ExecuteNonQuery();

            using (var cmd = new NpgsqlCommand(createView, conn))
                cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Upsert (INSERT or UPDATE) nhiều bản ghi vào table "student_scores".
        /// Nếu MSSV đã tồn tại -> cập nhật. Nếu chưa -> thêm mới.
        /// </summary>
        /// <param name="scores">Danh sách điểm cần ghi.</param>
        /// <returns>Tổng số hàng bị ảnh hưởng.</returns>
        /// <exception cref="Exception">Ném exception nếu kết nối hoặc câu lệnh SQL lỗi.</exception>
        public int UpsertMany(List<StudentScoreModel> scores)
        {
            if (scores == null || scores.Count == 0) return 0;

            int totalAffected = 0;

            // Câu lệnh UPSERT sử dụng ON CONFLICT để cập nhật nếu MSSV đã tồn tại.
            const string sql = @"
                INSERT INTO public.student_scores (mssv, ho_ten, lop, diem_gk, diem_ck)
                VALUES (@mssv, @ho_ten, @lop, @diem_gk, @diem_ck)
                ON CONFLICT (mssv)
                DO UPDATE SET
                    ho_ten  = EXCLUDED.ho_ten,
                    lop     = EXCLUDED.lop,
                    diem_gk = EXCLUDED.diem_gk,
                    diem_ck = EXCLUDED.diem_ck;";

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            // Dùng transaction để đảm bảo tính toàn vẹn dữ liệu
            using var transaction = conn.BeginTransaction();
            try
            {
                foreach (var score in scores)
                {
                    using var cmd = new NpgsqlCommand(sql, conn, transaction);
                    cmd.Parameters.AddWithValue("@mssv",    score.Mssv);
                    cmd.Parameters.AddWithValue("@ho_ten",  score.HoTen);
                    cmd.Parameters.AddWithValue("@lop",     score.Lop);
                    cmd.Parameters.AddWithValue("@diem_gk", score.DiemGK);
                    cmd.Parameters.AddWithValue("@diem_ck", score.DiemCK);

                    totalAffected += cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                // Rollback toàn bộ nếu có lỗi giữa chừng
                transaction.Rollback();
                throw; // Ném lại để tầng UI bắt và hiển thị thông báo
            }

            return totalAffected;
        }

        // --- Helper: Đọc một hàng từ NpgsqlDataReader và trả về StudentScoreModel ---
        private static StudentScoreModel MapRow(NpgsqlDataReader reader)
        {
            return new StudentScoreModel
            {
                Id      = reader.IsDBNull(reader.GetOrdinal("id"))      ? 0      : reader.GetInt64(reader.GetOrdinal("id")),
                Mssv    = reader.IsDBNull(reader.GetOrdinal("mssv"))    ? ""     : reader.GetString(reader.GetOrdinal("mssv")),
                HoTen   = reader.IsDBNull(reader.GetOrdinal("ho_ten"))  ? ""     : reader.GetString(reader.GetOrdinal("ho_ten")),
                Lop     = reader.IsDBNull(reader.GetOrdinal("lop"))     ? ""     : reader.GetString(reader.GetOrdinal("lop")),
                DiemGK  = reader.IsDBNull(reader.GetOrdinal("diem_gk")) ? 0.0   : reader.GetDouble(reader.GetOrdinal("diem_gk")),
                DiemCK  = reader.IsDBNull(reader.GetOrdinal("diem_ck")) ? 0.0   : reader.GetDouble(reader.GetOrdinal("diem_ck")),
            };
        }
    }
}
