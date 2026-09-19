using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CourseGuard.Backend.Models;

namespace CourseGuard.Backend.Services
{
    public sealed class TeacherStudentExcelExportService
    {
        private static readonly string[] Headers =
        {
            "STT",
            "Họ tên sinh viên",
            "Email",
            "Tên khóa học",
            "Ngày tham gia khóa học",
            "Trạng thái học viên"
        };

        public Task ExportStudentsAsync(
            IEnumerable<TeacherStudentModel> students,
            string filePath,
            CancellationToken cancellationToken = default)
        {
            if (students == null)
                throw new ArgumentNullException(nameof(students));

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Đường dẫn lưu file Excel không hợp lệ.", nameof(filePath));

            return Task.Run(() => ExportStudents(students.ToList(), filePath, cancellationToken), cancellationToken);
        }

        private static void ExportStudents(
            IReadOnlyList<TeacherStudentModel> students,
            string filePath,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Danh sách sinh viên");

            for (int column = 0; column < Headers.Length; column++)
            {
                worksheet.Cell(1, column + 1).Value = Headers[column];
            }

            var headerRange = worksheet.Range(1, 1, 1, Headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            for (int index = 0; index < students.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                TeacherStudentModel student = students[index];
                int row = index + 2;

                worksheet.Cell(row, 1).Value = index + 1;
                worksheet.Cell(row, 2).Value = student.StudentName;
                worksheet.Cell(row, 3).Value = student.Email;
                worksheet.Cell(row, 4).Value = student.CourseName;
                worksheet.Cell(row, 5).Value = student.JoinedAt;
                worksheet.Cell(row, 5).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
                worksheet.Cell(row, 6).Value = student.Status;
            }

            worksheet.SheetView.FreezeRows(1);
            worksheet.Row(1).Height = 22;
            worksheet.Columns().AdjustToContents();
            worksheet.RangeUsed()?.SetAutoFilter();

            workbook.SaveAs(filePath);
        }
    }
}
