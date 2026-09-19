using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Forms.Teacher;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;
using ClosedXML.Excel;

namespace CourseGuard.Frontend.UserControls.Teacher
{
    public partial class UC_TeacherResults : TeacherGridPageBase, ITeacherQuickSearchTarget
    {
        private static readonly HashSet<string> EditableStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "SUBMITTED",
            "COMPLETED",
            "GRADED"
        };

        private readonly Button _exportButton = new() { Text = "Xuất bảng điểm" };
        private readonly Button _viewViolationsButton = new() { Text = "Xem vi phạm" };
        private readonly int? _initialCourseId;
        private TeacherQuickSearchRequest? _quickSearchRequest;

        public UC_TeacherResults(int teacherId, TeacherController controller)
            : this(teacherId, controller, null)
        {
        }

        public UC_TeacherResults(int teacherId, TeacherController controller, int? initialCourseId) : base(teacherId, controller, "Kết quả", "Xem và cập nhật điểm cho bài thi thuộc khóa học của mình.", "Bảng điểm")
        {
            _initialCourseId = initialCourseId;
            AddButton.Visible = false;
            EditButton.Text = "Cập nhật điểm";
            TeacherTabChrome.StylePrimaryButton(EditButton);
            TeacherTabChrome.FitButtonToText(EditButton);
            DeleteButton.Visible = false;

            TeacherTabChrome.FitButtonToText(_exportButton);
            StyleSuccessButton(_exportButton);
            _exportButton.Click += async (_, _) => await ExportExcelAsync();

            TeacherTabChrome.FitButtonToText(_viewViolationsButton);
            TeacherTabChrome.StyleSecondaryButton(_viewViolationsButton);
            _viewViolationsButton.Click += async (_, _) => await ViewViolationsAsync();

            AddHeaderActionBefore(_viewViolationsButton, RefreshButton);
            AddHeaderActionBefore(_exportButton, _viewViolationsButton);

            Grid.CellDoubleClick += async (_, e) =>
            {
                if (e.RowIndex < 0)
                    return;

                Grid.CurrentCell = Grid.Rows[e.RowIndex].Cells[Math.Max(0, e.ColumnIndex)];
                await EditAsync();
            };
            ReloadForInitialCourse();
        }

        protected override string EditSelectionColumnName => "AttemptId";

        protected override Task<DataTable> CreateTableAsync() => Task.Run(() => BuildResultsTable(ApplyQuickSearchFilter(ApplyCourseFilter(Controller.GetResults(TeacherId)))));

        public async Task ApplyQuickSearchAsync(TeacherQuickSearchRequest request)
        {
            if (!IsResultQuickSearch(request.Kind))
                return;

            _quickSearchRequest = request;
            await LoadDataAsync();
            SelectResultRow(request);
        }

        private IEnumerable<TeacherScoreModel> ApplyQuickSearchFilter(IEnumerable<TeacherScoreModel> rows)
        {
            if (_quickSearchRequest == null)
                return rows;

            string keyword = (_quickSearchRequest.Keyword ?? string.Empty).Trim();
            if (string.Equals(_quickSearchRequest.Kind, TeacherQuickSearchKinds.ResultCourse, StringComparison.OrdinalIgnoreCase))
            {
                return rows.Where(row => row.CourseId == _quickSearchRequest.Id
                    || ContainsKeyword(row.CourseName, keyword));
            }

            if (string.Equals(_quickSearchRequest.Kind, TeacherQuickSearchKinds.ResultStudent, StringComparison.OrdinalIgnoreCase))
            {
                return rows.Where(row => row.StudentId == _quickSearchRequest.Id
                    || ContainsKeyword(row.StudentName, keyword));
            }

            return rows;
        }

        private IEnumerable<TeacherScoreModel> ApplyCourseFilter(IEnumerable<TeacherScoreModel> rows)
        {
            return _initialCourseId.HasValue
                ? rows.Where(row => row.CourseId == _initialCourseId.Value)
                : rows;
        }

        private void ReloadForInitialCourse()
        {
            if (_initialCourseId.HasValue)
                LoadDataAsync().FireAndForgetSafe(this);
        }

        private static DataTable BuildResultsTable(IEnumerable<TeacherScoreModel> rows) => TeacherTabChrome.ToTable(
            new[] { "AttemptId", "ExamId", "CourseId", "StudentId", "Khóa học", "Kỳ thi", "Sinh viên", "Điểm", "Trạng thái", "Nộp lúc" },
            rows,
            r => new object?[] { r.AttemptId, r.ExamId, r.CourseId, r.StudentId, r.CourseName, r.ExamTitle, r.StudentName, r.Score, r.Status, r.SubmitTime?.ToString("dd/MM/yyyy HH:mm") ?? "" });

        protected override async Task EditAsync()
        {
            int attemptId = CurrentInt("AttemptId");
            if (attemptId <= 0)
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn một sinh viên/bài làm để cập nhật điểm.", "Chưa chọn dữ liệu");
                return;
            }

            string status = CurrentString("Trạng thái").Trim();
            if (!EditableStatuses.Contains(status))
            {
                MetaTheme.ShowModernDialog(
                    "Chỉ có thể cập nhật điểm cho bài đã nộp hoặc đã chấm. Không thể sửa điểm khi trạng thái là IN_PROGRESS hoặc PENDING.",
                    "Không thể cập nhật điểm",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            double? currentScore = TryParseScore(CurrentString("Điểm"));
            using var dialog = new TeacherScoreEditDialog(
                CurrentString("Sinh viên"),
                CurrentString("Khóa học"),
                CurrentString("Kỳ thi"),
                status,
                currentScore);

            if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                return;

            bool updated = await Task.Run(() => Controller.UpdateScore(TeacherId, new TeacherScoreModel
            {
                AttemptId = attemptId,
                Score = dialog.Score
            }));

            if (!updated)
            {
                MetaTheme.ShowModernDialog(
                    "Không thể cập nhật điểm. Vui lòng kiểm tra trạng thái bài làm hoặc thử tải lại dữ liệu.",
                    "Cập nhật thất bại",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            await LoadDataAsync();
            MetaTheme.ShowModernDialog("Đã cập nhật điểm thành công.", "Thành công");
        }

        private async Task ViewViolationsAsync()
        {
            int attemptId = CurrentInt("AttemptId");
            if (attemptId <= 0)
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn một bài làm để xem vi phạm.", "Thông báo");
                return;
            }

            string studentName = CurrentString("Sinh viên");
            using var dialog = new TeacherViolationsHistoryDialog(attemptId, studentName);
            dialog.ShowDialog(FindForm());
        }

        private static bool IsResultQuickSearch(string kind)
        {
            return string.Equals(kind, TeacherQuickSearchKinds.ResultCourse, StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, TeacherQuickSearchKinds.ResultStudent, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsKeyword(string? value, string keyword)
        {
            return !string.IsNullOrWhiteSpace(keyword)
                && !string.IsNullOrWhiteSpace(value)
                && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }

        private void SelectResultRow(TeacherQuickSearchRequest request)
        {
            string columnName = string.Equals(request.Kind, TeacherQuickSearchKinds.ResultCourse, StringComparison.OrdinalIgnoreCase)
                ? "CourseId"
                : "StudentId";

            if (request.Id <= 0 || !Grid.Columns.Contains(columnName))
                return;

            Grid.ClearSelection();
            foreach (DataGridViewRow row in Grid.Rows)
            {
                if (row.IsNewRow || row.Cells[columnName].Value == null)
                    continue;

                if (Convert.ToInt32(row.Cells[columnName].Value) != request.Id)
                    continue;

                row.Selected = true;
                Grid.CurrentCell = GetFirstVisibleCell(row);
                Grid.FirstDisplayedScrollingRowIndex = Math.Max(0, row.Index);
                break;
            }
        }

        private DataGridViewCell? GetFirstVisibleCell(DataGridViewRow row)
        {
            foreach (DataGridViewCell cell in row.Cells)
            {
                if (cell.Visible)
                    return cell;
            }

            return null;
        }

        private async Task ExportExcelAsync()
        {
            if (Grid.Rows.Count == 0 || BindingSource.DataSource is not DataTable table || table.Rows.Count == 0)
            {
                MetaTheme.ShowModernDialog("Không có dữ liệu bảng điểm để xuất Excel.", "Thông báo");
                return;
            }

            string courseName = ResolveExportCourseName(table);
            string defaultFileName = $"BangDiem_{SanitizeFileName(courseName)}_{DateTime.Now:yyyyMMdd}.xlsx";
            using var saveDialog = new SaveFileDialog
            {
                Title = "Xuất bảng điểm",
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
                var rows = BuildVisibleExportRows();
                if (rows.Count == 0)
                {
                    MetaTheme.ShowModernDialog("Không có dòng dữ liệu đang hiển thị để xuất Excel.", "Thông báo");
                    return;
                }

                await Task.Run(() => SaveScoreWorkbook(saveDialog.FileName, rows));
                MetaTheme.ShowModernDialog($"Đã xuất bảng điểm thành công:\n{saveDialog.FileName}", "Xuất bảng điểm thành công");
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog("Không thể xuất bảng điểm: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private List<ScoreExportRow> BuildVisibleExportRows()
        {
            var rows = new List<ScoreExportRow>();
            int index = 1;
            foreach (DataGridViewRow gridRow in Grid.Rows)
            {
                if (gridRow.IsNewRow || !gridRow.Visible)
                    continue;

                rows.Add(new ScoreExportRow(
                    index++,
                    CellText(gridRow, "Sinh viên"),
                    CellText(gridRow, "Khóa học"),
                    CellText(gridRow, "Kỳ thi"),
                    CellValue(gridRow, "Điểm"),
                    CellText(gridRow, "Trạng thái"),
                    ParseSubmitTime(CellText(gridRow, "Nộp lúc"))));
            }

            return rows;
        }

        private void SaveScoreWorkbook(string filePath, IReadOnlyList<ScoreExportRow> rows)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Bảng điểm");

            string[] headers = { "STT", "Sinh viên", "Khóa học", "Kỳ thi", "Điểm", "Trạng thái", "Nộp lúc" };
            for (int col = 0; col < headers.Length; col++)
                worksheet.Cell(1, col + 1).Value = headers[col];

            for (int i = 0; i < rows.Count; i++)
            {
                ScoreExportRow row = rows[i];
                int excelRow = i + 2;
                worksheet.Cell(excelRow, 1).Value = row.Index;
                worksheet.Cell(excelRow, 2).Value = row.StudentName;
                worksheet.Cell(excelRow, 3).Value = row.CourseName;
                worksheet.Cell(excelRow, 4).Value = row.ExamTitle;
                SetScoreCell(worksheet.Cell(excelRow, 5), row.Score);
                worksheet.Cell(excelRow, 6).Value = row.Status;

                if (row.SubmitTime.HasValue)
                {
                    worksheet.Cell(excelRow, 7).Value = row.SubmitTime.Value;
                    worksheet.Cell(excelRow, 7).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
                }
                else
                {
                    worksheet.Cell(excelRow, 7).Value = string.Empty;
                }
            }

            var usedRange = worksheet.Range(1, 1, rows.Count + 1, headers.Length);
            usedRange.Style.Font.FontName = "Segoe UI";
            usedRange.Style.Font.FontSize = 11;
            usedRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            var headerRange = worksheet.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E5E7EB");
            headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.BottomBorderColor = XLColor.FromHtml("#CBD5E1");
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Range(2, 1, rows.Count + 1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Range(2, 5, rows.Count + 1, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Range(2, 6, rows.Count + 1, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Range(2, 7, rows.Count + 1, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Range(2, 2, rows.Count + 1, 4).Style.Alignment.WrapText = false;

            usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            usedRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#E5E7EB");
            usedRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#E5E7EB");

            worksheet.SheetView.FreezeRows(1);
            worksheet.Columns().AdjustToContents();
            worksheet.Column(1).Width = Math.Max(worksheet.Column(1).Width, 8);
            worksheet.Column(2).Width = Math.Max(worksheet.Column(2).Width, 22);
            worksheet.Column(3).Width = Math.Max(worksheet.Column(3).Width, 26);
            worksheet.Column(4).Width = Math.Max(worksheet.Column(4).Width, 24);
            worksheet.Column(5).Width = Math.Max(worksheet.Column(5).Width, 10);
            worksheet.Column(6).Width = Math.Max(worksheet.Column(6).Width, 14);
            worksheet.Column(7).Width = Math.Max(worksheet.Column(7).Width, 18);

            workbook.SaveAs(filePath);
        }

        private object? CellValue(DataGridViewRow row, string columnName)
        {
            if (!Grid.Columns.Contains(columnName))
                return null;

            object? value = row.Cells[columnName].Value;
            return value == DBNull.Value ? null : value;
        }

        private string CellText(DataGridViewRow row, string columnName)
        {
            return CellValue(row, columnName)?.ToString() ?? string.Empty;
        }

        private static DateTime? ParseSubmitTime(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            return DateTime.TryParseExact(raw.Trim(), "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime submitTime)
                ? submitTime
                : null;
        }

        private static void SetScoreCell(IXLCell cell, object? rawScore)
        {
            if (rawScore == null)
            {
                cell.Value = string.Empty;
                return;
            }

            string value = rawScore.ToString() ?? string.Empty;
            if (double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double score))
            {
                cell.Value = score;
                cell.Style.NumberFormat.Format = "0.##";
            }
            else
            {
                cell.Value = value;
            }
        }

        private static double? TryParseScore(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            string normalized = raw.Trim().Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double score)
                ? score
                : null;
        }

        private static string ResolveExportCourseName(DataTable table)
        {
            if (!table.Columns.Contains("Khóa học") || table.Rows.Count == 0)
                return "Tat_Ca";

            var names = table.AsEnumerable()
                .Select(row => row["Khóa học"]?.ToString())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return names.Count == 1 ? names[0]! : "Tat_Ca";
        }

        private static string SanitizeFileName(string value)
        {
            string safe = string.IsNullOrWhiteSpace(value) ? "Tat_Ca" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                safe = safe.Replace(invalid, '_');

            return safe.Replace(' ', '_');
        }

        private static void StyleSuccessButton(Button button)
        {
            button.Tag = "success";
            button.AutoSize = false;
            button.Height = 34;
            button.MinimumSize = new Size(84, 34);
            button.Padding = new Padding(14, 0, 14, 1);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Font = AppFonts.Button;
            button.Cursor = Cursors.Hand;
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.BackColor = ColorTranslator.FromHtml("#16A34A");
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#15803D");
            button.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#15803D");
            button.FlatAppearance.MouseDownBackColor = ColorTranslator.FromHtml("#166534");
            RoundedButtonHelper.Apply(button, 10);
        }

        private sealed record ScoreExportRow(
            int Index,
            string StudentName,
            string CourseName,
            string ExamTitle,
            object? Score,
            string Status,
            DateTime? SubmitTime);

        private sealed class TeacherScoreEditDialog : ThemedDialogBase
        {
            private readonly NumericUpDown _scoreInput;

            public double Score => decimal.ToDouble(_scoreInput.Value);

            public TeacherScoreEditDialog(string studentName, string courseName, string examTitle, string status, double? currentScore)
            {
                Text = "Cập nhật điểm";
                Width = 520;
                Height = 360;

                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 6,
                    BackColor = AppColors.BgCard,
                    Padding = new Padding(0, 4, 0, 0)
                };
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 16f));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                layout.Controls.Add(CreateInfoLabel("Sinh viên", studentName), 0, 0);
                layout.Controls.Add(CreateInfoLabel("Khóa học", courseName), 0, 1);
                layout.Controls.Add(CreateInfoLabel("Kỳ thi", examTitle), 0, 2);
                layout.Controls.Add(CreateInfoLabel("Trạng thái", status), 0, 3);

                var scorePanel = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    ColumnCount = 1,
                    BackColor = AppColors.BgCard,
                    Margin = new Padding(0, 8, 0, 0)
                };
                scorePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                scorePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, TeacherCourseDialog.DefaultInputRowHeight));
                scorePanel.Controls.Add(new Label
                {
                    Text = "Điểm mới (0 - 10)",
                    AutoSize = true,
                    Font = AppFonts.Semibold(10.5f),
                    ForeColor = AppColors.TextPrimary,
                    BackColor = AppColors.BgCard,
                    Margin = new Padding(0, 0, 0, 6)
                }, 0, 0);

                _scoreInput = new NumericUpDown
                {
                    Dock = DockStyle.Top,
                    Minimum = 0,
                    Maximum = 10,
                    DecimalPlaces = 2,
                    Increment = 0.25m,
                    Value = Convert.ToDecimal(Math.Clamp(currentScore ?? 0, 0, 10)),
                    Font = AppFonts.Body,
                    BackColor = AppColors.BgElevated,
                    ForeColor = AppColors.TextPrimary,
                    Height = 36
                };
                scorePanel.Controls.Add(_scoreInput, 0, 1);
                layout.Controls.Add(scorePanel, 0, 5);
                ContentPanel.Controls.Add(layout);

                var cancelButton = TeacherTabChrome.SecondaryButton("Hủy");
                var saveButton = TeacherTabChrome.PrimaryButton("Lưu điểm");
                TeacherTabChrome.FitButtonToText(cancelButton);
                TeacherTabChrome.FitButtonToText(saveButton);
                cancelButton.Click += (_, _) =>
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                };
                saveButton.Click += (_, _) =>
                {
                    DialogResult = DialogResult.OK;
                    Close();
                };
                AddFooterButtons(cancelButton, saveButton);
            }

            private static Control CreateInfoLabel(string label, string value)
            {
                return new Label
                {
                    Text = $"{label}: {value}",
                    AutoSize = true,
                    Font = AppFonts.Body,
                    ForeColor = AppColors.TextSecondary,
                    BackColor = AppColors.BgCard,
                    Margin = new Padding(0, 0, 0, 8)
                };
            }
        }

        private sealed class TeacherViolationsHistoryDialog : ThemedDialogBase
        {
            private readonly int _attemptId;
            private readonly DataGridView _grid;
            private readonly CourseGuard.Backend.Data.ViolationRepository _repo = new();

            public TeacherViolationsHistoryDialog(int attemptId, string studentName)
            {
                _attemptId = attemptId;
                Text = $"Lịch sử vi phạm - {studentName}";
                Width = 800;
                Height = 500;

                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 2,
                    BackColor = AppColors.BgCard,
                    Padding = new Padding(10)
                };
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

                var title = new Label
                {
                    Text = $"Danh sách vi phạm của: {studentName}",
                    AutoSize = true,
                    Font = AppFonts.Title,
                    ForeColor = AppColors.TextPrimary,
                    Margin = new Padding(0, 0, 0, 10)
                };
                layout.Controls.Add(title, 0, 0);

                _grid = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    ReadOnly = true,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect
                };
                TeacherTabChrome.StyleGrid(_grid);
                
                _grid.Columns.Add("CreatedAt", "Thời gian");
                _grid.Columns.Add("Type", "Loại vi phạm");
                _grid.Columns.Add("Severity", "Mức độ");
                
                var btnCol = new DataGridViewButtonColumn
                {
                    Name = "ViewImage",
                    HeaderText = "Ảnh chụp",
                    Text = "Xem",
                    UseColumnTextForButtonValue = true
                };
                _grid.Columns.Add(btnCol);
                
                _grid.CellClick += Grid_CellClick;

                layout.Controls.Add(_grid, 0, 1);
                ContentPanel.Controls.Add(layout);

                var closeButton = TeacherTabChrome.SecondaryButton("Đóng");
                TeacherTabChrome.FitButtonToText(closeButton);
                closeButton.Click += (_, _) => Close();
                AddFooterButtons(closeButton);

                LoadDataAsync().FireAndForgetSafe(this);
            }

            private async Task LoadDataAsync()
            {
                try
                {
                    var violations = await _repo.GetViolationsByAttemptIdAsync(_attemptId);
                    if (InvokeRequired)
                    {
                        BeginInvoke(new MethodInvoker(() => BindData(violations)));
                    }
                    else
                    {
                        BindData(violations);
                    }
                }
                catch (Exception ex)
                {
                    MetaTheme.ShowModernDialog("Không thể tải danh sách vi phạm: " + ex.Message, "Lỗi");
                }
            }

            private void BindData(IEnumerable<CourseGuard.Backend.Models.ViolationModel> violations)
            {
                _grid.Rows.Clear();
                foreach (var v in violations)
                {
                    int r = _grid.Rows.Add(v.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"), v.Type, v.Severity);
                    _grid.Rows[r].Tag = v.ImageUrl;
                }
            }

            private void Grid_CellClick(object? sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0 && e.RowIndex < _grid.Rows.Count
                    && _grid.Columns["ViewImage"] is DataGridViewColumn imageColumn
                    && e.ColumnIndex == imageColumn.Index)
                {
                    string? url = _grid.Rows[e.RowIndex].Tag as string;
                    if (!string.IsNullOrEmpty(url))
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = url,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            MetaTheme.ShowModernDialog($"Không thể mở ảnh: {ex.Message}", "Lỗi");
                        }
                    }
                    else
                    {
                        MetaTheme.ShowModernDialog("Vi phạm này không có ảnh đính kèm.", "Thông báo");
                    }
                }
            }
        }
    }
}
