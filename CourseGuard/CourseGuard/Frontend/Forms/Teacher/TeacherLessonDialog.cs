using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls.Teacher;

namespace CourseGuard.Frontend.Forms.Teacher
{
    public partial class TeacherLessonDialog : ThemedDialogBase
    {
        private readonly ComboBox _course = new();
        private readonly TextBox _title = new();
        private readonly TextBox _content = new();
        private readonly DateTimePicker _publishAt = new();
        private readonly ComboBox _status = new();
        private readonly bool _lockCourseSelection;
        private readonly Label _courseScopeLabel = new();
        
        private readonly TextBox _file = new();
        private readonly Button _browse = TeacherTabChrome.SecondaryButton("Chọn file");
        private readonly Button _clearFile = TeacherTabChrome.SecondaryButton("Xóa file");
        private readonly Button _save;
        private readonly Button _cancel;

        private class StatusItem
        {
            public string Value { get; }
            public string Display { get; }
            public StatusItem(string value, string display) { Value = value; Display = display; }
            public override string ToString() => Display;
        }

        public int CourseId => _course.SelectedItem is TeacherCourseModel course ? course.Id : 0;
        public string ItemTitle => _title.Text.Trim();
        public string ItemContent => _content.Text.Trim();
        public DateTime? PublishAt => _publishAt.Checked ? _publishAt.Value : null;
        public string Status => (_status.SelectedItem as StatusItem)?.Value ?? "DRAFT";
        
        public string SelectedFilePath => _file.Text.Trim();
        
        public string? ContentType { get; private set; }
        public long? FileSize { get; private set; }
        public byte[]? FileContent { get; private set; }
        public string? OriginalFileName { get; private set; }

        public TeacherLessonDialog(
            IEnumerable<TeacherCourseModel> courses,
            TeacherLessonModel? existing = null,
            int selectedCourseId = 0,
            bool lockCourseSelection = false)
        {
            List<TeacherCourseModel> courseList = courses.ToList();
            _lockCourseSelection = lockCourseSelection && (selectedCourseId > 0 || existing?.CourseId > 0);
            Text = existing == null ? "Thêm Bài học" : "Sửa Bài học";
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
                _content.Text = existing.Content;
                if (existing.PublishAt.HasValue) 
                {
                    _publishAt.Checked = true;
                    _publishAt.Value = existing.PublishAt.Value;
                }
                else
                {
                    _publishAt.Checked = false;
                }
                _file.Text = existing.FileName;
                OriginalFileName = existing.FileName;
            }

            _content.Multiline = true;
            _content.ScrollBars = ScrollBars.Vertical;
            _publishAt.Format = DateTimePickerFormat.Custom;
            _publishAt.CustomFormat = "dd/MM/yyyy HH:mm";
            _publishAt.ShowCheckBox = true;
            if (existing == null) _publishAt.Checked = false;
            
            _status.DropDownStyle = ComboBoxStyle.DropDownList;
            var statuses = new[]
            {
                new StatusItem("DRAFT", "Bản nháp"),
                new StatusItem("PUBLISHED", "Đã xuất bản")
            };
            _status.Items.AddRange(statuses);
            
            var existingStatus = existing?.Status ?? "DRAFT";
            _status.SelectedItem = statuses.FirstOrDefault(s => s.Value == existingStatus) ?? statuses[0];

            _file.ReadOnly = true;
            _browse.Click += (_, _) => BrowseFile();
            _clearFile.Click += (_, _) => _file.Text = string.Empty;

            _save = new Button { Text = "Lưu", Width = 90 };
            _cancel = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel, Width = 90 };
            TeacherTabChrome.StylePrimaryButton(_save);
            TeacherTabChrome.StyleSecondaryButton(_cancel);

            _save.Click += async (s, e) => await OnSaveAsync();

            var grid = TeacherCourseDialog.CreateGrid();
            grid.RowCount = 6;
            grid.RowStyles.Clear();
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, TeacherCourseDialog.DefaultInputRowHeight));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, TeacherCourseDialog.DefaultInputRowHeight));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, TeacherCourseDialog.DefaultInputRowHeight));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, TeacherCourseDialog.DefaultInputRowHeight));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, TeacherCourseDialog.DefaultInputRowHeight));

            grid.Controls.Add(new Label { Text = "Khóa học", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 0);
            grid.Controls.Add(CreateCourseScopeDisplay(), 1, 0);
            
            grid.Controls.Add(new Label { Text = "Tiêu đề", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 1);
            _title.Dock = DockStyle.Fill;
            grid.Controls.Add(_title, 1, 1);
            
            grid.Controls.Add(new Label { Text = "Nội dung", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 2);
            _content.Dock = DockStyle.Fill;
            grid.Controls.Add(_content, 1, 2);
            
            var filePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = new Padding(0) };
            filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _file.Dock = DockStyle.Fill;
            filePanel.Controls.Add(_file, 0, 0);
            filePanel.Controls.Add(_browse, 1, 0);
            filePanel.Controls.Add(_clearFile, 2, 0);
            
            grid.Controls.Add(new Label { Text = "Giáo trình (Word/PDF)", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 3);
            grid.Controls.Add(filePanel, 1, 3);
            
            grid.Controls.Add(new Label { Text = "Ngày đăng", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 4);
            _publishAt.Dock = DockStyle.Fill;
            grid.Controls.Add(_publishAt, 1, 4);
            
            grid.Controls.Add(new Label { Text = "Trạng thái", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 5);
            _status.Dock = DockStyle.Fill;
            grid.Controls.Add(_status, 1, 5);

            ContentPanel.Controls.Add(grid);
            AddFooterButtons(_cancel, _save);
            CancelButton = _cancel;
        }
        
        public void SetSelectedCourse(int courseId)
        {
            SelectCourse(courseId);
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
            _courseScopeLabel.Text = (_course.SelectedItem as TeacherCourseModel)?.Name ?? _course.SelectedItem?.ToString() ?? string.Empty;
        }

        private void BrowseFile()
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Word & PDF (*.pdf;*.doc;*.docx)|*.pdf;*.doc;*.docx|All files (*.*)|*.*"
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                _file.Text = dialog.FileName;
        }
        
        private async Task OnSaveAsync()
        {
            if (CourseId <= 0 || string.IsNullOrWhiteSpace(ItemTitle))
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn khóa học và nhập tiêu đề.", "Thiếu thông tin");
                return;
            }
            
            string path = SelectedFilePath;
            
            if (!string.IsNullOrEmpty(path) && path != OriginalFileName)
            {
                if (!File.Exists(path))
                {
                    MetaTheme.ShowModernDialog("File đính kèm không tồn tại.", "Lỗi file");
                    return;
                }
                
                var info = new FileInfo(path);
                if (info.Length > 30 * 1024 * 1024)
                {
                    MetaTheme.ShowModernDialog("File đính kèm không được vượt quá 30MB.", "Lỗi dung lượng");
                    return;
                }
                
                _save.Enabled = false;
                _save.Text = "Đang tải...";
                _cancel.Enabled = false;
                _browse.Enabled = false;
                _clearFile.Enabled = false;

                try
                {
                    FileContent = await Task.Run(() => File.ReadAllBytes(path));
                    FileSize = info.Length;
                    ContentType = GetContentType(path);
                }
                catch (Exception ex)
                {
                    MetaTheme.ShowModernDialog($"Không thể đọc file: {ex.Message}", "Lỗi file");
                    _save.Enabled = true;
                    _save.Text = "Lưu";
                    _cancel.Enabled = true;
                    _browse.Enabled = true;
                    _clearFile.Enabled = true;
                    return;
                }
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private static string GetContentType(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => "application/octet-stream"
            };
        }
    }
}
