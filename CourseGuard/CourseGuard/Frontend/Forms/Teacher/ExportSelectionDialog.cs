using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls.Teacher;

namespace CourseGuard.Frontend.Forms.Teacher
{
    public enum StudentExportScope
    {
        All,
        Course
    }

    public sealed class ExportSelectionDialog : ThemedDialogBase
    {
        private readonly IReadOnlyList<ExportCourseOption> _courses;
        private readonly RadioButton _allStudentsRadio = new();
        private readonly RadioButton _courseRadio = new();
        private readonly ComboBox _courseCombo = new();

        public ExportSelectionDialog(IEnumerable<ExportCourseOption> courses)
        {
            _courses = (courses ?? throw new ArgumentNullException(nameof(courses)))
                .OrderBy(course => course.CourseName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            Text = "Tùy chọn xuất Excel";
            Width = 560;
            Height = 340;
            MinimumSize = new Size(520, 320);

            InitializeComponent();
            UpdateCourseSelectionState();
        }

        public StudentExportScope SelectedScope => _courseRadio.Checked
            ? StudentExportScope.Course
            : StudentExportScope.All;

        public int? SelectedCourseId => SelectedScope == StudentExportScope.Course
            && _courseCombo.SelectedItem is ExportCourseOption option
                ? option.CourseId
                : null;

        public string SelectedCourseName => SelectedScope == StudentExportScope.Course
            && _courseCombo.SelectedItem is ExportCourseOption option
                ? option.CourseName
                : string.Empty;

        private void InitializeComponent()
        {
            ContentPanel.Padding = new Padding(24, 14, 24, 12);

            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.BgCard,
                ColumnCount = 1,
                RowCount = 4,
                Padding = Padding.Empty
            };
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 88f));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            ContentPanel.Controls.Add(body);

            body.Controls.Add(new Label
            {
                AutoSize = true,
                BackColor = Color.Transparent,
                ForeColor = AppColors.TextSecondary,
                Font = AppFonts.Body,
                Margin = new Padding(0, 0, 0, 14),
                Text = "Chọn phạm vi dữ liệu cần xuất. Dữ liệu xuất tất cả sẽ được sắp xếp theo khóa học rồi đến tên sinh viên.",
                MaximumSize = new Size(490, 0),
                UseCompatibleTextRendering = true
            }, 0, 0);

            ConfigureRadio(_allStudentsRadio, "Xuất tất cả sinh viên");
            _allStudentsRadio.Checked = true;
            _allStudentsRadio.CheckedChanged += (_, _) =>
            {
                if (_allStudentsRadio.Checked)
                    _courseRadio.Checked = false;

                UpdateCourseSelectionState();
            };
            body.Controls.Add(_allStudentsRadio, 0, 1);

            var coursePanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            coursePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
            coursePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, TeacherCourseDialog.DefaultInputRowHeight));
            body.Controls.Add(coursePanel, 0, 2);

            ConfigureRadio(_courseRadio, "Xuất theo khóa học");
            _courseRadio.CheckedChanged += (_, _) =>
            {
                if (_courseRadio.Checked)
                    _allStudentsRadio.Checked = false;

                UpdateCourseSelectionState();
            };
            coursePanel.Controls.Add(_courseRadio, 0, 0);

            _courseCombo.Dock = DockStyle.Top;
            _courseCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _courseCombo.Font = AppFonts.Body;
            _courseCombo.DisplayMember = nameof(ExportCourseOption.CourseName);
            _courseCombo.ValueMember = nameof(ExportCourseOption.CourseId);
            _courseCombo.DataSource = _courses.ToList();
            _courseCombo.Margin = new Padding(28, 0, 0, 0);
            StudentDropdownStyler.StyleComboBox(_courseCombo, null, true);
            coursePanel.Controls.Add(_courseCombo, 0, 1);

            var cancelButton = TeacherTabChrome.SecondaryButton("Hủy");
            cancelButton.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            var confirmButton = TeacherTabChrome.PrimaryButton("Xác nhận");
            confirmButton.Click += (_, _) => ConfirmSelection();

            AcceptButton = confirmButton;
            CancelButton = cancelButton;
            AddFooterButtons(cancelButton, confirmButton);
        }

        private static void ConfigureRadio(RadioButton radio, string text)
        {
            radio.AutoSize = true;
            radio.BackColor = Color.Transparent;
            radio.Cursor = Cursors.Hand;
            radio.FlatStyle = FlatStyle.Standard;
            radio.Font = AppFonts.Semibold(10.5f);
            radio.ForeColor = AppColors.TextPrimary;
            radio.Margin = new Padding(0, 7, 0, 0);
            radio.Text = text;
            radio.UseVisualStyleBackColor = false;
        }

        private void UpdateCourseSelectionState()
        {
            bool selectingCourse = _courseRadio.Checked;
            bool hasCourses = _courses.Count > 0;

            _courseRadio.Enabled = hasCourses;
            _courseCombo.Enabled = selectingCourse && hasCourses;

            if (selectingCourse && hasCourses && _courseCombo.SelectedIndex < 0)
                _courseCombo.SelectedIndex = 0;
        }

        private void ConfirmSelection()
        {
            if (_courseRadio.Checked && _courseCombo.SelectedItem is not ExportCourseOption)
            {
                MetaTheme.ShowModernDialog(
                    "Vui lòng chọn một khóa học để xuất danh sách sinh viên.",
                    "Thiếu khóa học",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }

    public sealed class ExportCourseOption
    {
        public ExportCourseOption(int courseId, string courseName)
        {
            CourseId = courseId;
            CourseName = string.IsNullOrWhiteSpace(courseName) ? "Không rõ khóa học" : courseName.Trim();
        }

        public int CourseId { get; }
        public string CourseName { get; }

        public override string ToString() => CourseName;
    }
}
