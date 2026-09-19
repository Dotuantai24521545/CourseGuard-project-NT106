using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Services;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls.Teacher;

namespace CourseGuard.Frontend.Forms.Teacher
{
    public partial class TeacherAssignmentDialog : ThemedDialogBase
    {
        private const long MaxAttachmentBytes = 10 * 1024 * 1024;
        private const string DefaultValidationText = "Đặt hạn nộp rõ ràng; file đính kèm tối đa 10MB.";

        private readonly ComboBox _course = new();
        private readonly TextBox _title = new();
        private readonly TextBox _details = new();
        private readonly DateTimePicker _date = new();
        private readonly ComboBox _status = new();
        private readonly Label _validationSummary = new();
        private readonly bool _lockCourseSelection;
        private readonly Label _courseScopeLabel = new();
        
        private readonly TextBox _file = new();
        private readonly Label _fileSummary = new();
        private readonly Button _browse = TeacherTabChrome.SecondaryButton("Chọn file");
        private readonly Button _clearFile = TeacherTabChrome.SecondaryButton("Xóa file");
        private string _selectedFilePath = string.Empty;
        private string _existingFileName = string.Empty;
        private bool _clearExistingFile;

        private class StatusItem
        {
            public string Value { get; }
            public string Display { get; }
            public StatusItem(string value, string display) { Value = value; Display = display; }
            public override string ToString() => Display;
        }

        public int CourseId => _course.SelectedItem is TeacherCourseModel course ? course.Id : 0;
        public string ItemTitle => _title.Text.Trim();
        public string Details => _details.Text.Trim();
        public DateTime SelectedDate => _date.Value;
        public string Status => (_status.SelectedItem as StatusItem)?.Value ?? "OPEN";
        
        public string SelectedFilePath => _selectedFilePath;
        public bool ClearFileRequested => _clearExistingFile;

        public TeacherAssignmentDialog(
            IEnumerable<TeacherCourseModel> courses,
            TeacherAssignmentModel? existing = null,
            int selectedCourseId = 0,
            bool lockCourseSelection = false)
        {
            List<TeacherCourseModel> courseList = courses.ToList();
            _lockCourseSelection = lockCourseSelection && (selectedCourseId > 0 || existing?.CourseId > 0);
            Text = existing == null ? "Tạo Bài Tập" : "Sửa Bài Tập";
            Width = 800;
            Height = 600;

            _course.DropDownStyle = ComboBoxStyle.DropDownList;
            _course.DisplayMember = nameof(TeacherCourseModel.Name);
            foreach (var course in courseList)
                _course.Items.Add(course);
            
            SelectCourse(selectedCourseId);

            if (existing != null)
            {
                SelectCourse(existing.CourseId);
                _title.Text = existing.Title;
                _details.Text = existing.Description;
                if (existing.DueAt.HasValue) _date.Value = existing.DueAt.Value;
                _existingFileName = existing.FileName;
                _file.Text = _existingFileName;
            }

            _details.Multiline = true;
            _details.ScrollBars = ScrollBars.Vertical;
            _date.Format = DateTimePickerFormat.Custom;
            _date.CustomFormat = "dd/MM/yyyy HH:mm";
            
            _status.DropDownStyle = ComboBoxStyle.DropDownList;
            var statuses = new[]
            {
                new StatusItem("OPEN", "Đã mở"),
                new StatusItem("CLOSED", "Đã đóng")
            };
            _status.Items.AddRange(statuses);
            
            var existingStatus = existing?.Status ?? "OPEN";
            _status.SelectedItem = statuses.FirstOrDefault(s => s.Value == existingStatus) ?? statuses[0];

            _file.ReadOnly = true;
            _browse.Click += (_, _) => BrowseFile();
            _clearFile.Click += (_, _) =>
            {
                _selectedFilePath = string.Empty;
                _clearExistingFile = !string.IsNullOrWhiteSpace(_existingFileName);
                _file.Text = string.Empty;
                UpdateFileSummary();
            };

            ResetValidationSummary();
            _validationSummary.Dock = DockStyle.Fill;
            _validationSummary.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            _validationSummary.Font = MetaTheme.Fonts.BodyMd();
            _validationSummary.AutoEllipsis = true;

            _fileSummary.Dock = DockStyle.Fill;
            _fileSummary.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            _fileSummary.Font = MetaTheme.Fonts.BodySm();
            _fileSummary.ForeColor = AppColors.TextSecondary;

            var save = new Button { Text = "Lưu", DialogResult = DialogResult.OK, Width = 90 };
            var cancel = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel, Width = 90 };
            TeacherTabChrome.StylePrimaryButton(save);
            TeacherTabChrome.StyleSecondaryButton(cancel);

            save.Click += (s, e) =>
            {
                if (!ValidateBeforeSave())
                    DialogResult = DialogResult.None;
            };

            var grid = TeacherCourseDialog.CreateGrid();
            grid.RowCount = 6;
            grid.RowStyles.Clear();
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, TeacherCourseDialog.DefaultInputRowHeight));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, TeacherCourseDialog.DefaultInputRowHeight));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, TeacherCourseDialog.DefaultInputRowHeight));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, TeacherCourseDialog.DefaultInputRowHeight));

            grid.Controls.Add(new Label { Text = "Khóa học", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 0);
            grid.Controls.Add(CreateCourseScopeDisplay(), 1, 0);
            grid.Controls.Add(new Label { Text = "Tiêu đề", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 1);
            _title.Dock = DockStyle.Fill;
            grid.Controls.Add(_title, 1, 1);
            grid.Controls.Add(new Label { Text = "Mô tả", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 2);
            _details.Dock = DockStyle.Fill;
            grid.Controls.Add(_details, 1, 2);
            
            var filePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Margin = new Padding(0) };
            filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            filePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, TeacherCourseDialog.DefaultInputRowHeight));
            filePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _file.Dock = DockStyle.Fill;
            filePanel.Controls.Add(_file, 0, 0);
            filePanel.Controls.Add(_browse, 1, 0);
            filePanel.Controls.Add(_clearFile, 2, 0);
            filePanel.Controls.Add(_fileSummary, 0, 1);
            filePanel.SetColumnSpan(_fileSummary, 3);
            
            grid.Controls.Add(new Label { Text = "File đính kèm", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 3);
            grid.Controls.Add(filePanel, 1, 3);
            
            grid.Controls.Add(new Label { Text = "Hạn nộp", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 4);
            _date.Dock = DockStyle.Fill;
            grid.Controls.Add(_date, 1, 4);
            grid.Controls.Add(new Label { Text = "Trạng thái", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 5);
            _status.Dock = DockStyle.Fill;
            grid.Controls.Add(_status, 1, 5);

            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0) };
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.Controls.Add(_validationSummary, 0, 0);
            content.Controls.Add(grid, 0, 1);

            UpdateFileSummary();
            ContentPanel.Controls.Add(content);
            AddFooterButtons(cancel, save);
            AcceptButton = save;
            CancelButton = cancel;
        }
        
        public void SetSelectedCourse(int courseId)
        {
            SelectCourse(courseId);
        }
        
        public void SetExistingFile(string fileName)
        {
            _selectedFilePath = string.Empty;
            _existingFileName = fileName;
            _clearExistingFile = false;
            _file.Text = fileName;
            UpdateFileSummary();
        }

        private void SelectCourse(int courseId)
        {
            int idx = _course.Items.Cast<TeacherCourseModel>().ToList().FindIndex(c => c.Id == courseId);
            if (idx >= 0)
                _course.SelectedIndex = idx;
            else if (_course.Items.Count > 0 && _course.SelectedIndex < 0)
                _course.SelectedIndex = 0;

            RefreshCourseScopeLabel();
        }

        private Control CreateCourseScopeDisplay()
        {
            var host = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                FillColor = AppColors.BgInput,
                BorderColor = AppColors.BorderStrong,
                CornerRadius = 8,
                BackColor = AppColors.BgCard,
                Padding = new Padding(12, 0, 12, 0),
                Margin = new Padding(0, 4, 0, 4)
            };

            _course.Visible = !_lockCourseSelection;
            _course.Dock = DockStyle.Fill;

            _courseScopeLabel.Dock = DockStyle.Fill;
            _courseScopeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            _courseScopeLabel.Font = MetaTheme.Fonts.BodyMd();
            _courseScopeLabel.ForeColor = AppColors.TextPrimary;
            _courseScopeLabel.BackColor = AppColors.BgInput;
            _courseScopeLabel.Visible = _lockCourseSelection;
            RefreshCourseScopeLabel();

            if (_lockCourseSelection)
            {
                host.Controls.Add(_courseScopeLabel);
                return host;
            }

            return _course;
        }

        private void RefreshCourseScopeLabel()
        {
            _courseScopeLabel.Text = _course.SelectedItem?.ToString() ?? string.Empty;
        }

        private void BrowseFile()
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Files (*.pdf;*.doc;*.docx;*.txt)|*.pdf;*.doc;*.docx;*.txt|All files (*.*)|*.*"
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _selectedFilePath = dialog.FileName;
                _clearExistingFile = false;
                _file.Text = dialog.FileName;
                UpdateFileSummary();
            }
        }

        private bool ValidateBeforeSave()
        {
            ResetValidationSummary();

            if (CourseId <= 0 || string.IsNullOrWhiteSpace(ItemTitle))
                return ShowValidationError("Vui lòng chọn khóa học và nhập tiêu đề.");

            if (Status == "OPEN" && SelectedDate < DateTime.Now)
                return ShowValidationError("Bài tập đang mở phải có hạn nộp trong tương lai.");

            if (!string.IsNullOrWhiteSpace(SelectedFilePath) && !File.Exists(SelectedFilePath))
                return ShowValidationError("File đính kèm đã chọn không còn tồn tại. Vui lòng chọn lại file.");

            if (!string.IsNullOrWhiteSpace(SelectedFilePath))
            {
                var info = new FileInfo(SelectedFilePath);
                MaterialFileValidation validation = MaterialFilePolicy.Validate(info.Name, info.Length);
                if (!validation.IsValid)
                {
                    if (info.Length > MaxAttachmentBytes)
                        return ShowValidationError("File đính kèm không được vượt quá 10MB.");

                    return ShowValidationError(validation.ErrorMessage);
                }

                if (info.Length > MaxAttachmentBytes)
                    return ShowValidationError("File đính kèm không được vượt quá 10MB.");
            }

            return true;
        }

        private bool ShowValidationError(string message)
        {
            _validationSummary.Text = message;
            _validationSummary.ForeColor = AppColors.Danger;
            return false;
        }

        private void ResetValidationSummary()
        {
            _validationSummary.Text = DefaultValidationText;
            _validationSummary.ForeColor = AppColors.TextSecondary;
        }

        private void UpdateFileSummary()
        {
            if (_clearExistingFile)
            {
                _fileSummary.Text = "Chưa chọn file đính kèm.";
                _fileSummary.ForeColor = AppColors.TextSecondary;
                return;
            }

            if (!string.IsNullOrWhiteSpace(_selectedFilePath) && File.Exists(_selectedFilePath))
            {
                var info = new FileInfo(_selectedFilePath);
                double sizeMb = info.Length / 1024d / 1024d;
                _fileSummary.Text = $"Đã chọn {info.Name} ({sizeMb:0.##} MB).";
                _fileSummary.ForeColor = AppColors.TextSecondary;
                return;
            }

            if (!string.IsNullOrWhiteSpace(_existingFileName))
            {
                _fileSummary.Text = $"File hiện tại: {Path.GetFileName(_existingFileName)}";
                _fileSummary.ForeColor = AppColors.TextSecondary;
                return;
            }

            _fileSummary.Text = "Chưa chọn file đính kèm.";
            _fileSummary.ForeColor = AppColors.TextSecondary;
        }
    }
}
