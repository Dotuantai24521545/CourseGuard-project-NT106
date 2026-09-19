using CourseGuard.Backend.Models;
using CourseGuard.Backend.Services;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls.Teacher;

namespace CourseGuard.Frontend.Forms.Teacher
{
    public class TeacherMaterialUploadDialog : ThemedDialogBase
    {
        private readonly ComboBox _course = new();
        private readonly TextBox _file = new();
        private readonly Button _browse = TeacherTabChrome.SecondaryButton("Chọn file");
        private readonly Button _save = TeacherTabChrome.PrimaryButton("Lưu");
        private readonly Button _cancel = TeacherTabChrome.SecondaryButton("Hủy");
        private readonly List<TeacherCourseModel> _courses;
        private readonly bool _lockCourseSelection;
        private readonly Label _courseScopeLabel = new();

        public TeacherMaterialUploadDialog(IEnumerable<TeacherCourseModel> courses, int selectedCourseId = 0, bool lockCourseSelection = false)
        {
            _courses = courses.ToList();
            _lockCourseSelection = lockCourseSelection && selectedCourseId > 0;
            Text = "Tải tài liệu";
            Width = 640;
            Height = 270;
            BuildLayout(selectedCourseId);
        }

        public int CourseId => _course.SelectedItem is TeacherCourseModel course ? course.Id : 0;
        public string SelectedFilePath => _file.Text.Trim();

        private void BuildLayout(int selectedCourseId)
        {
            var root = TeacherCourseDialog.CreateGrid();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(18);
            root.RowCount = 2;
            root.RowStyles.Clear();
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, TeacherCourseDialog.DefaultInputRowHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, TeacherCourseDialog.DefaultInputRowHeight));

            _course.DropDownStyle = ComboBoxStyle.DropDownList;
            _course.DisplayMember = nameof(TeacherCourseModel.Name);
            foreach (TeacherCourseModel course in _courses)
                _course.Items.Add(course);
            int selectedIndex = _courses.FindIndex(c => c.Id == selectedCourseId);
            _course.SelectedIndex = selectedIndex >= 0 ? selectedIndex : (_course.Items.Count > 0 ? 0 : -1);
            RefreshCourseScopeLabel();

            _file.ReadOnly = true;
            var filePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            filePanel.Controls.Add(_file, 0, 0);
            filePanel.Controls.Add(_browse, 1, 0);

            _browse.Click += (_, _) => BrowseFile();
            _save.Click += (_, _) => Save();
            _cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

            AddRow(root, 0, "Khóa học", CreateCourseScopeDisplay());
            AddRow(root, 1, "File", filePanel);

            ContentPanel.Controls.Add(root);
            AddFooterButtons(_cancel, _save);
            AcceptButton = _save;
            CancelButton = _cancel;
        }

        private static void AddRow(TableLayoutPanel grid, int row, string label, Control control)
        {
            grid.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            control.Dock = DockStyle.Fill;
            grid.Controls.Add(control, 1, row);
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
            _courseScopeLabel.TextAlign = ContentAlignment.MiddleLeft;
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
                Filter = "Tài liệu (*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.txt;*.zip;*.rar)|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.txt;*.zip;*.rar|Tất cả file (*.*)|*.*"
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                _file.Text = dialog.FileName;
        }

        private void Save()
        {
            if (CourseId <= 0)
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn khóa học.", "Thiếu thông tin");
                return;
            }

            if (!File.Exists(SelectedFilePath))
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn file cần tải lên.", "Thiếu thông tin");
                return;
            }

            var info = new FileInfo(SelectedFilePath);
            MaterialFileValidation validation = MaterialFilePolicy.Validate(info.Name, info.Length);
            if (!validation.IsValid)
            {
                MetaTheme.ShowModernDialog(validation.ErrorMessage, "File không hợp lệ");
                return;
            }

            DialogResult = DialogResult.OK;
        }
    }
}
