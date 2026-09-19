using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Forms.Teacher;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Teacher
{
    public partial class UC_TeacherStudents : TeacherGridPageBase, ITeacherQuickSearchTarget
    {
        private readonly Button _exportExcelButton = TeacherTabChrome.SuccessButton("Xuất Excel");
        private readonly int? _initialCourseId;
        private List<TeacherStudentModel> _allStudents = new();
        private string _quickSearchKeyword = string.Empty;
        private bool _isExporting;

        public UC_TeacherStudents(int teacherId, TeacherController controller)
            : this(teacherId, controller, null)
        {
        }

        public UC_TeacherStudents(int teacherId, TeacherController controller, int? initialCourseId) : base(teacherId, controller, "Sinh viên", "Duyệt ghi danh và xem danh sách học viên thuộc khóa học của mình.", "Yêu cầu và học viên")
        {
            _initialCourseId = initialCourseId;
            AddButton.Text = "Duyệt";
            EditButton.Text = "Từ chối";
            DeleteButton.Visible = false;
            TeacherTabChrome.FitButtonToText(AddButton);
            TeacherTabChrome.FitButtonToText(EditButton);

            TeacherTabChrome.FitButtonToText(_exportExcelButton);
            _exportExcelButton.Click += async (_, _) => await ExportVisibleStudentsAsync();
            AddHeaderActionBefore(_exportExcelButton, RefreshButton);

            var attendanceButton = TeacherTabChrome.SecondaryButton("Điểm danh");
            attendanceButton.Click += (_, _) =>
            {
                using var dialog = new TeacherAttendanceDialog(TeacherId, Controller);
                dialog.ShowDialog(FindForm());
            };
            AddHeaderAction(attendanceButton);
            ReloadForInitialCourse();
        }

        protected override string EditSelectionColumnName => "EnrollmentId";

        protected override Task<DataTable> CreateTableAsync() => Task.Run(() =>
        {
            _allStudents = Controller.GetPendingEnrollments(TeacherId)
                .Concat(Controller.GetEnrolledStudents(TeacherId))
                .ToList();

            return BuildStudentTable(ApplyLocalFilter(_allStudents));
        });

        protected override async Task AddAsync()
        {
            if (CurrentString("Trạng thái").ToUpperInvariant() == "PENDING")
            {
                await Task.Run(() => Controller.ApproveEnrollment(TeacherId, CurrentInt("CourseId"), CurrentInt("StudentId")));
                await LoadDataAsync();
            }
        }

        protected override async Task EditAsync()
        {
            if (CurrentString("Trạng thái").ToUpperInvariant() == "PENDING"
                && CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Từ chối yêu cầu ghi danh?", "Xác nhận", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                await Task.Run(() => Controller.RejectEnrollment(TeacherId, CurrentInt("CourseId"), CurrentInt("StudentId")));
                await LoadDataAsync();
            }
        }

        public async Task ApplyQuickSearchAsync(TeacherQuickSearchRequest request)
        {
            if (!string.Equals(request.Kind, TeacherQuickSearchKinds.Student, StringComparison.OrdinalIgnoreCase))
                return;

            _quickSearchKeyword = request.Keyword ?? string.Empty;
            await LoadDataAsync();
            SelectStudentRow(request.Id, request.ParentId);
        }

        private IEnumerable<TeacherStudentModel> ApplyLocalFilter(IEnumerable<TeacherStudentModel> students)
        {
            string keyword = _quickSearchKeyword.Trim();
            if (string.IsNullOrWhiteSpace(keyword))
                return ApplyCourseFilter(students);

            return ApplyCourseFilter(students).Where(s => ContainsKeyword(s.StudentName, keyword)
                || ContainsKeyword(s.Email, keyword)
                || ContainsKeyword(s.CourseName, keyword)
                || ContainsKeyword(s.Status, keyword));
        }

        private IEnumerable<TeacherStudentModel> ApplyCourseFilter(IEnumerable<TeacherStudentModel> students)
        {
            return _initialCourseId.HasValue
                ? students.Where(student => student.CourseId == _initialCourseId.Value)
                : students;
        }

        private void ReloadForInitialCourse()
        {
            if (_initialCourseId.HasValue)
                LoadDataAsync().FireAndForgetSafe(this);
        }

        private static DataTable BuildStudentTable(IEnumerable<TeacherStudentModel> rows) =>
            TeacherTabChrome.ToTable(new[] { "EnrollmentId", "CourseId", "StudentId", "Khóa học", "Sinh viên", "Email", "Trạng thái", "Ngày tham gia" },
                rows,
                s => new object?[] { s.EnrollmentId, s.CourseId, s.StudentId, s.CourseName, s.StudentName, s.Email, s.Status, s.JoinedAt.ToString("dd/MM/yyyy HH:mm") });

        private void SelectStudentRow(int studentId, int? courseId)
        {
            if (studentId <= 0 || !Grid.Columns.Contains("StudentId"))
                return;

            Grid.ClearSelection();
            foreach (DataGridViewRow row in Grid.Rows)
            {
                if (row.IsNewRow || row.Cells["StudentId"].Value == null)
                    continue;

                int currentStudentId = Convert.ToInt32(row.Cells["StudentId"].Value);
                int currentCourseId = Grid.Columns.Contains("CourseId") && row.Cells["CourseId"].Value != null
                    ? Convert.ToInt32(row.Cells["CourseId"].Value)
                    : 0;

                if (currentStudentId != studentId || (courseId.HasValue && courseId.Value > 0 && currentCourseId != courseId.Value))
                    continue;

                row.Selected = true;
                Grid.CurrentCell = GetFirstVisibleCell(row);
                Grid.FirstDisplayedScrollingRowIndex = Math.Max(0, row.Index);
                break;
            }
        }

        private DataGridViewCell? GetFirstVisibleCell(DataGridViewRow row)
        {
            return row.Cells.Cast<DataGridViewCell>().FirstOrDefault(cell => cell.Visible);
        }

        private async Task ExportVisibleStudentsAsync()
        {
            if (_isExporting)
                return;

            List<TeacherStudentModel> visibleStudents = ApplyLocalFilter(_allStudents).ToList();
            if (visibleStudents.Count == 0)
            {
                MetaTheme.ShowModernDialog("Không có sinh viên để xuất Excel.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<ExportCourseOption> courseOptions = BuildExportCourseOptions(visibleStudents);
            using var selectionDialog = new ExportSelectionDialog(courseOptions);
            if (selectionDialog.ShowDialog(FindForm()) != DialogResult.OK)
                return;

            List<TeacherStudentModel> studentsToExport = BuildExportRows(visibleStudents, selectionDialog);
            if (studentsToExport.Count == 0)
            {
                MetaTheme.ShowModernDialog("Không có sinh viên phù hợp với lựa chọn xuất Excel.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string scopeName = selectionDialog.SelectedScope == StudentExportScope.Course
                ? SanitizeFileName(selectionDialog.SelectedCourseName)
                : "TatCa";
            string defaultFileName = $"DanhSachSinhVien_{scopeName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            using var saveDialog = new SaveFileDialog
            {
                Title = "Xuất danh sách sinh viên",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = defaultFileName,
                AddExtension = true,
                DefaultExt = "xlsx",
                OverwritePrompt = true
            };

            if (saveDialog.ShowDialog(FindForm()) != DialogResult.OK)
                return;

            try
            {
                _isExporting = true;
                _exportExcelButton.Enabled = false;
                UseWaitCursor = true;
                Cursor.Current = Cursors.WaitCursor;

                await Controller.ExportStudentsToExcelAsync(studentsToExport, saveDialog.FileName);

                DialogResult openFile = MetaTheme.ShowModernDialog(
                    $"Đã xuất danh sách sinh viên thành công:\n{saveDialog.FileName}\n\nBạn có muốn mở file ngay không?",
                    "Xuất Excel thành công",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (openFile == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(saveDialog.FileName)
                    {
                        UseShellExecute = true
                    });
                }
            }
            catch (IOException)
            {
                MetaTheme.ShowModernDialog(
                    "File Excel đang được mở bởi một chương trình khác. Vui lòng đóng file và thử lại.",
                    "Không thể ghi file Excel",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog(
                    "Không thể xuất danh sách sinh viên: " + ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _isExporting = false;
                _exportExcelButton.Enabled = true;
                UseWaitCursor = false;
                Cursor.Current = Cursors.Default;
            }
        }

        private static List<ExportCourseOption> BuildExportCourseOptions(IEnumerable<TeacherStudentModel> students)
        {
            return students
                .Where(student => student.CourseId > 0 && !string.IsNullOrWhiteSpace(student.CourseName))
                .GroupBy(student => student.CourseId)
                .Select(group => new ExportCourseOption(
                    group.Key,
                    group.Select(student => student.CourseName)
                        .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? string.Empty))
                .OrderBy(option => option.CourseName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static List<TeacherStudentModel> BuildExportRows(
            IEnumerable<TeacherStudentModel> visibleStudents,
            ExportSelectionDialog selectionDialog)
        {
            IEnumerable<TeacherStudentModel> query = visibleStudents;

            if (selectionDialog.SelectedScope == StudentExportScope.Course && selectionDialog.SelectedCourseId.HasValue)
            {
                int selectedCourseId = selectionDialog.SelectedCourseId.Value;
                query = query.Where(student => student.CourseId == selectedCourseId)
                    .OrderBy(student => student.StudentName, StringComparer.CurrentCultureIgnoreCase);
            }
            else
            {
                query = query
                    .OrderBy(student => student.CourseName, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(student => student.StudentName, StringComparer.CurrentCultureIgnoreCase);
            }

            return query.ToList();
        }

        private static string SanitizeFileName(string value)
        {
            string safe = string.IsNullOrWhiteSpace(value) ? "KhoaHoc" : value.Trim();
            safe = Regex.Replace(safe, "[\\\\/:*?\"<>|]", "_");
            safe = Regex.Replace(safe, @"\s+", "_");
            safe = Regex.Replace(safe, "_+", "_").Trim('_', '.', ' ');
            return string.IsNullOrWhiteSpace(safe) ? "KhoaHoc" : safe;
        }

        private static bool ContainsKeyword(string? value, string keyword)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }
    }
}
