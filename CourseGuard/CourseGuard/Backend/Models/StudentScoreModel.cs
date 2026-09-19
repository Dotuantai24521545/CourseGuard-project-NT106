/*
 * StudentScoreModel.cs
 *
 * Layer: Core / Models
 * Vai trò: Định nghĩa đối tượng Điểm Sinh Viên, ánh xạ tới View "vw_student_scores" trên Supabase (PostgreSQL).
 * Sử dụng:
 *   - Đọc/ghi dữ liệu từ DB thông qua NpgsqlDataReader.
 *   - Import/Export CSV thông qua CsvHelper (dùng attribute [Name(...)]).
 *   - Hiển thị trong DataGridView của UC_ScoreManagement.
 *
 * Lưu ý:
 *   - TongKet và TrangThai là thuộc tính tính toán (computed), KHÔNG ghi vào DB, KHÔNG đọc từ CSV.
 */
using CsvHelper.Configuration.Attributes; // Để dùng [Name] và [Ignore] của CSV
namespace CourseGuard.Backend.Models
{
    /// <summary>
    /// Mô hình dữ liệu điểm sinh viên, tương ứng với View hoặc Table "vw_student_scores" trên Supabase.
    /// </summary>
   
    public class StudentScoreModel
    {

        // --- Thuộc tính ánh xạ với cột CSDL và CSV ---

        /// <summary>Khóa chính (ID hàng trong DB). Không cần thiết trong CSV.</summary>
        [Ignore] // Bỏ qua khi đọc/ghi CSV
        public long Id { get; set; }

        /// <summary>Mã số sinh viên (uniquely identifies a student). Đây là conflict key cho UPSERT.</summary>
        [Name("MSSV")]
        public string Mssv { get; set; } = string.Empty;

        /// <summary>Họ và tên sinh viên.</summary>
        [Name("Ho Ten")]
        public string HoTen { get; set; } = string.Empty;

        /// <summary>Lớp/Khóa học của sinh viên.</summary>
        [Name("Lop")]
        public string Lop { get; set; } = string.Empty;

        /// <summary>Điểm giữa kỳ (hệ số 30%).</summary>
        [Name("Diem GK")]
        public double DiemGK { get; set; }

        /// <summary>Điểm cuối kỳ (hệ số 70%).</summary>
        [Name("Diem CK")]
        public double DiemCK { get; set; }

        // --- Thuộc tính tính toán (Computed, get-only) ---

        /// <summary>
        /// Tổng kết = (DiemGK * 0.3) + (DiemCK * 0.7), làm tròn 1 chữ số thập phân.
        /// Thuộc tính này KHÔNG được lưu vào DB và KHÔNG được đọc từ CSV.
        /// </summary>
        [Ignore] // Bỏ qua khi đọc/ghi CSV
        public double TongKet => Math.Round((DiemGK * 0.3) + (DiemCK * 0.7), 1);

        /// <summary>
        /// Trạng thái học tập dựa trên TongKet.
        /// Thuộc tính này KHÔNG được lưu vào DB và KHÔNG được đọc từ CSV.
        /// </summary>
        [Ignore] // Bỏ qua khi đọc/ghi CSV
        public string TrangThai => TongKet < 5.0 ? "Không đạt" : "Đạt";
    }
}
