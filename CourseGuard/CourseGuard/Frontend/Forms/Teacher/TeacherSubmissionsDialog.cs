using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls.Teacher;

namespace CourseGuard.Frontend.Forms.Teacher
{
    public class TeacherSubmissionsDialog : ThemedDialogBase
    {
        private readonly int _teacherId;
        private readonly TeacherController _controller;
        private readonly List<TeacherCourseModel> _courses;
        private List<StudentSubmissionModel> _allSubmissions = new();

        private ComboBox _cboCourse = null!;
        private ComboBox _cboAssignment = null!;
        private ComboBox _cboStatus = null!;
        private DataGridView _grid = null!;
        private BindingSource _bindingSource = new();

        // Grading panel
        private Panel _gradingPanel = null!;
        private Label _lblStudentName = null!;
        private Button _btnDownload = null!;
        private NumericUpDown _numScore = null!;
        private TextBox _txtFeedback = null!;
        private Button _btnSaveGrade = null!;
        private Label _saveStatus = null!;
        private StudentSubmissionModel? _selectedSubmission;

        private sealed class FilterItem
        {
            public int Id { get; }
            public string Name { get; }

            public FilterItem(int id, string name)
            {
                Id = id;
                Name = name;
            }

            public override string ToString() => Name;
        }

        public TeacherSubmissionsDialog(int teacherId, TeacherController controller, List<TeacherCourseModel> courses)
        {
            _teacherId = teacherId;
            _controller = controller;
            _courses = courses;
            
            this.Text = "Danh sách sinh viên nộp bài";
            this.Width = 1000;
            this.Height = 650;

            InitializeComponent();
            ApplyTheme();
        }

        private void InitializeComponent()
        {
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(0)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 180)); // Grading panel
            ContentPanel.Controls.Add(mainLayout);

            // Filter
            var filterPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
            filterPanel.Controls.Add(new Label { Text = "Khóa học:", AutoSize = true, Font = MetaTheme.Fonts.BodyMdBold(), Margin = new Padding(0, 8, 8, 0) });
            _cboCourse = new ComboBox { Width = 240, DropDownStyle = ComboBoxStyle.DropDownList, Font = MetaTheme.Fonts.BodyMd(), Margin = new Padding(0, 4, 14, 0) };
            _cboCourse.Items.Add(new FilterItem(0, "Tất cả khóa học"));
            foreach (var c in _courses) _cboCourse.Items.Add(new FilterItem(c.Id, c.Name));
            _cboCourse.DisplayMember = "Name";
            _cboCourse.ValueMember = "Id";
            _cboCourse.SelectedIndex = 0;
            _cboCourse.SelectedIndexChanged += async (s, e) => await LoadDataAsync();
            filterPanel.Controls.Add(_cboCourse);

            filterPanel.Controls.Add(new Label { Text = "Bài tập:", AutoSize = true, Font = MetaTheme.Fonts.BodyMdBold(), Margin = new Padding(0, 8, 8, 0) });
            _cboAssignment = new ComboBox { Width = 240, DropDownStyle = ComboBoxStyle.DropDownList, Font = MetaTheme.Fonts.BodyMd(), Margin = new Padding(0, 4, 14, 0) };
            _cboAssignment.DisplayMember = "Name";
            _cboAssignment.ValueMember = "Id";
            _cboAssignment.SelectedIndexChanged += FilterChanged;
            filterPanel.Controls.Add(_cboAssignment);

            filterPanel.Controls.Add(new Label { Text = "Trạng thái:", AutoSize = true, Font = MetaTheme.Fonts.BodyMdBold(), Margin = new Padding(0, 8, 8, 0) });
            _cboStatus = new ComboBox { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList, Font = MetaTheme.Fonts.BodyMd(), Margin = new Padding(0, 4, 0, 0) };
            _cboStatus.Items.AddRange(new object[] { "Tất cả", "Chưa chấm", "Đã chấm" });
            _cboStatus.SelectedIndex = 0;
            _cboStatus.SelectedIndexChanged += FilterChanged;
            filterPanel.Controls.Add(_cboStatus);
            mainLayout.Controls.Add(filterPanel, 0, 0);

            // Grid
            _grid = new DataGridView {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = AppColors.BgBase
            };
            TeacherTabChrome.StyleGrid(_grid);
            _grid.SelectionChanged += Grid_SelectionChanged;
            mainLayout.Controls.Add(_grid, 0, 1);

            // Grading Panel
            _gradingPanel = new Panel { Dock = DockStyle.Fill, BackColor = AppColors.BgCard, Padding = new Padding(10) };
            
            _lblStudentName = new Label { Location = new Point(10, 10), AutoSize = true, Font = MetaTheme.Fonts.SubtitleLg() };
            _gradingPanel.Controls.Add(_lblStudentName);

            _btnDownload = TeacherTabChrome.SecondaryButton("Tải bài làm xuống");
            _btnDownload.Location = new Point(10, 45);
            _btnDownload.Click += BtnDownload_Click;
            _gradingPanel.Controls.Add(_btnDownload);

            _gradingPanel.Controls.Add(new Label { Text = "Điểm số (0-10):", Location = new Point(200, 45), AutoSize = true, Font = MetaTheme.Fonts.BodyMd() });
            _numScore = new NumericUpDown { Location = new Point(200, 70), Width = 100, Font = MetaTheme.Fonts.BodyMd(), Minimum = 0, Maximum = 10, DecimalPlaces = 1, Increment = 0.5m };
            _gradingPanel.Controls.Add(_numScore);

            _gradingPanel.Controls.Add(new Label { Text = "Nhận xét:", Location = new Point(340, 45), AutoSize = true, Font = MetaTheme.Fonts.BodyMd() });
            _txtFeedback = new TextBox { Location = new Point(340, 70), Width = 500, Height = 60, Multiline = true, Font = MetaTheme.Fonts.BodyMd() };
            _gradingPanel.Controls.Add(_txtFeedback);

            _btnSaveGrade = TeacherTabChrome.PrimaryButton("Cập nhật điểm");
            _btnSaveGrade.Location = new Point(340, 140);
            _btnSaveGrade.Width = 150;
            _btnSaveGrade.Click += BtnSaveGrade_Click;
            _gradingPanel.Controls.Add(_btnSaveGrade);

            _saveStatus = new Label { Location = new Point(510, 148), AutoSize = true, Font = MetaTheme.Fonts.BodyMd(), ForeColor = AppColors.TextSecondary };
            _gradingPanel.Controls.Add(_saveStatus);

            SetGradingInputsEnabled(false);
            mainLayout.Controls.Add(_gradingPanel, 0, 2);
            
            var closeBtn = TeacherTabChrome.SecondaryButton("Đóng");
            closeBtn.Click += (s, e) => Close();
            AddFooterButtons(closeBtn);
            CancelButton = closeBtn;
        }

        private void ApplyTheme()
        {
            _lblStudentName.ForeColor = AppColors.TextPrimary;
            _gradingPanel.BackColor = AppColors.BgCard;
            foreach (Control c in _gradingPanel.Controls)
            {
                if (c is Label l) l.ForeColor = AppColors.TextPrimary;
            }
            _saveStatus.ForeColor = AppColors.TextSecondary;
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                _allSubmissions = await _controller.GetStudentSubmissionsAsync(_teacherId, SelectedCourseId());
                RefreshAssignmentFilter();
                ApplyFilters();
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog("Không thể tải dữ liệu: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void FilterChanged(object? sender, EventArgs e)
        {
            ApplyFilters();
        }

        private int? SelectedCourseId()
        {
            return _cboCourse.SelectedItem is FilterItem item && item.Id > 0
                ? item.Id
                : null;
        }

        private int SelectedAssignmentId()
        {
            return _cboAssignment.SelectedItem is FilterItem item
                ? item.Id
                : 0;
        }

        private void RefreshAssignmentFilter()
        {
            _cboAssignment.SelectedIndexChanged -= FilterChanged;
            _cboAssignment.Items.Clear();
            _cboAssignment.Items.Add(new FilterItem(0, "Tất cả bài tập"));

            foreach (var assignment in _allSubmissions
                .GroupBy(s => new { s.AssignmentId, s.AssignmentTitle })
                .OrderBy(g => g.Key.AssignmentTitle))
            {
                _cboAssignment.Items.Add(new FilterItem(assignment.Key.AssignmentId, assignment.Key.AssignmentTitle));
            }

            _cboAssignment.SelectedIndex = 0;
            _cboAssignment.SelectedIndexChanged += FilterChanged;
        }

        private void ApplyFilters()
        {
            IEnumerable<StudentSubmissionModel> filtered = _allSubmissions;

            int assignmentId = SelectedAssignmentId();
            if (assignmentId > 0)
                filtered = filtered.Where(s => s.AssignmentId == assignmentId);

            string status = _cboStatus.SelectedItem?.ToString() ?? "Tất cả";
            if (status == "Chưa chấm")
                filtered = filtered.Where(s => !IsGraded(s));
            else if (status == "Đã chấm")
                filtered = filtered.Where(IsGraded);

            BindSubmissions(filtered);
        }

        private static bool IsGraded(StudentSubmissionModel submission)
        {
            return submission.Score.HasValue
                || string.Equals(submission.Status, "GRADED", StringComparison.OrdinalIgnoreCase);
        }

        private void BindSubmissions(IEnumerable<StudentSubmissionModel> submissions)
        {
            var table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Khóa học", typeof(string));
            table.Columns.Add("Bài tập", typeof(string));
            table.Columns.Add("Sinh viên", typeof(string));
            table.Columns.Add("Thời gian nộp", typeof(string));
            table.Columns.Add("File", typeof(string));
            table.Columns.Add("Trạng thái", typeof(string));
            table.Columns.Add("Điểm", typeof(string));
            table.Columns.Add("Hành động", typeof(string));

            foreach (var s in submissions)
            {
                AssignmentUxPresentation presentation = TeacherAssignmentUxPresenter.PresentSubmission(s);
                table.Rows.Add(
                    s.SubmissionId,
                    s.CourseName,
                    s.AssignmentTitle,
                    s.StudentName,
                    s.SubmittedAt.ToString("dd/MM/yyyy HH:mm"),
                    s.FileName,
                    presentation.StatusText,
                    s.Score?.ToString("0.0") ?? "",
                    presentation.ActionText);
            }

            _bindingSource.DataSource = table;
            _grid.DataSource = _bindingSource;
            if (_grid.Columns.Contains("Id") && _grid.Columns["Id"] != null)
                _grid.Columns["Id"]!.Visible = false;

            _grid.ClearSelection();
            ResetGradingPanel();
        }

        private void ResetGradingPanel()
        {
            _selectedSubmission = null;
            SetGradingInputsEnabled(false);
            _lblStudentName.Text = "Chưa chọn bài nộp";
            _numScore.Value = 0;
            _txtFeedback.Text = string.Empty;
            _saveStatus.Text = string.Empty;
        }

        private void Grid_SelectionChanged(object? sender, EventArgs e)
        {
            if (_grid.CurrentRow == null || _grid.CurrentRow.IsNewRow || _grid.CurrentRow.Cells["Id"].Value == null)
            {
                ResetGradingPanel();
                return;
            }

            int id = Convert.ToInt32(_grid.CurrentRow.Cells["Id"].Value);
            _selectedSubmission = _allSubmissions.FirstOrDefault(s => s.SubmissionId == id);

            if (_selectedSubmission != null)
            {
                SetGradingInputsEnabled(true);
                _lblStudentName.Text = $"Đang chọn: {_selectedSubmission.StudentName} - {_selectedSubmission.AssignmentTitle}";
                _numScore.Value = _selectedSubmission.Score ?? 0;
                _txtFeedback.Text = _selectedSubmission.Feedback ?? "";
                _saveStatus.Text = string.Empty;
            }
        }

        private void SetGradingInputsEnabled(bool enabled)
        {
            _btnDownload.Enabled = enabled;
            _numScore.Enabled = enabled;
            _txtFeedback.Enabled = enabled;
            _btnSaveGrade.Enabled = enabled;
        }

        private async void BtnDownload_Click(object? sender, EventArgs e)
        {
            if (_selectedSubmission == null) return;

            using var saveDialog = new SaveFileDialog
            {
                FileName = _selectedSubmission.FileName,
                Filter = "All files (*.*)|*.*",
                Title = "Lưu bài làm của sinh viên"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    _btnDownload.Enabled = false;
                    _btnDownload.Text = "Đang tải...";
                    
                    byte[]? content = await _controller.GetSubmissionContentAsync(_teacherId, _selectedSubmission.SubmissionId);
                    if (content == null || content.Length == 0)
                    {
                        MetaTheme.ShowModernDialog("Không tìm thấy file trên máy chủ.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    await File.WriteAllBytesAsync(saveDialog.FileName, content);
                    MetaTheme.ShowModernDialog("Tải file thành công!", "Thành công", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = saveDialog.FileName,
                            UseShellExecute = true
                        });
                    }
                    catch { /* Ignore if no default app */ }
                }
                catch (Exception ex)
                {
                    MetaTheme.ShowModernDialog("Lỗi khi tải file: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    _btnDownload.Enabled = true;
                    _btnDownload.Text = "Tải bài làm xuống";
                }
            }
        }

        private async void BtnSaveGrade_Click(object? sender, EventArgs e)
        {
            if (_selectedSubmission == null) return;

            try
            {
                SetGradingInputsEnabled(false);
                _btnSaveGrade.Text = "Đang lưu...";
                SetSaveStatus("Đang lưu điểm...", AppColors.TextSecondary);

                bool success = await _controller.UpdateGradeAsync(_teacherId, _selectedSubmission.SubmissionId, _numScore.Value, _txtFeedback.Text);
                
                if (success)
                {
                    MetaTheme.ShowModernDialog("Cập nhật điểm thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await LoadDataAsync();
                    SetSaveStatus("Đã lưu điểm.", AppColors.Success);
                }
                else
                {
                    SetSaveStatus("Chưa lưu được điểm.", AppColors.Danger);
                    MetaTheme.ShowModernDialog("Cập nhật thất bại.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                SetSaveStatus("Chưa lưu được điểm.", AppColors.Danger);
                MetaTheme.ShowModernDialog("Lỗi khi cập nhật điểm: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetGradingInputsEnabled(_selectedSubmission != null);
                _btnSaveGrade.Text = "Cập nhật điểm";
            }
        }

        private void SetSaveStatus(string message, Color color)
        {
            _saveStatus.Text = message;
            _saveStatus.ForeColor = color;
        }
    }
}
