using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls.Teacher;

namespace CourseGuard.Frontend.Forms.Teacher
{
    public partial class TeacherCourseDialog : ThemedDialogBase
    {
        internal const int DefaultInputRowHeight = 52;
        internal const int MultilineInputRowHeight = 96;

        private readonly TextBox _name = new();
        private readonly TextBox _description = new();
        private readonly Label _status = new();
        private readonly DateTimePicker _startDate = new();
        private readonly DateTimePicker _endDate = new();
        private readonly CheckBox _generateSchedule = new();
        private readonly CheckedListBox _teachingDays = new();
        private readonly DateTimePicker _sessionStart = new();
        private readonly DateTimePicker _sessionEnd = new();
        private readonly TextBox _meetingLink = new();

        public TeacherCourseModel Course { get; private set; }

        public TeacherCourseDialog(TeacherCourseModel? course = null)
        {
            Course = course ?? new TeacherCourseModel();
            Text = course == null ? "Tạo khóa học" : "Sửa khóa học";
            Width = 620;
            Height = 780;

            _name.Text = Course.Name;
            _description.Text = Course.Description;
            _description.Multiline = true;
            _status.Text = string.IsNullOrWhiteSpace(Course.Status) ? WorkflowConstants.CourseStatus.Draft : Course.Status;
            _status.Dock = DockStyle.Fill;
            _status.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            _startDate.Value = Course.StartDate ?? DateTime.Today;
            _endDate.Value = Course.EndDate ?? DateTime.Today.AddMonths(1);
            _startDate.Format = DateTimePickerFormat.Short;
            _endDate.Format = DateTimePickerFormat.Short;

            _generateSchedule.Text = "Tạo lịch dạy lặp cho khóa học";
            _generateSchedule.Checked = Course.GenerateScheduleOnCreate;
            _teachingDays.CheckOnClick = true;
            _teachingDays.Height = 72;
            foreach (var day in DayItems())
                _teachingDays.Items.Add(day);
            _sessionStart.Format = DateTimePickerFormat.Custom;
            _sessionStart.CustomFormat = "HH:mm";
            _sessionStart.ShowUpDown = true;
            _sessionStart.Value = DateTime.Today.Add(Course.SessionStartTime ?? new TimeSpan(8, 0, 0));
            _sessionEnd.Format = DateTimePickerFormat.Custom;
            _sessionEnd.CustomFormat = "HH:mm";
            _sessionEnd.ShowUpDown = true;
            _sessionEnd.Value = DateTime.Today.Add(Course.SessionEndTime ?? new TimeSpan(10, 0, 0));
            _meetingLink.Text = Course.MeetingLink;

            var save = new Button { Text = "Lưu", DialogResult = DialogResult.OK, Width = 96 };
            var cancel = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel, Width = 96 };
            TeacherTabChrome.StylePrimaryButton(save);
            TeacherTabChrome.StyleSecondaryButton(cancel);
            save.Click += (_, _) =>
            {
                if (!ValidateAndApply())
                    DialogResult = DialogResult.None;
            };

            var grid = CreateGrid();
            AddRow(grid, 0, "Tên khóa học", _name);
            AddRow(grid, 1, "Mô tả", _description);
            AddRow(grid, 2, "Trạng thái", _status);
            AddRow(grid, 3, "Ngày bắt đầu", _startDate);
            AddRow(grid, 4, "Ngày kết thúc", _endDate);
            AddRow(grid, 5, string.Empty, _generateSchedule);
            AddRow(grid, 6, "Thứ trong tuần", _teachingDays);
            AddRow(grid, 7, "Giờ bắt đầu", _sessionStart);
            AddRow(grid, 8, "Giờ kết thúc", _sessionEnd);
            AddRow(grid, 9, "Link học", _meetingLink);

            ContentPanel.Controls.Add(grid);
            AddFooterButtons(cancel, save);
            
            AcceptButton = save;
            CancelButton = cancel;
        }

        internal static TableLayoutPanel CreateGrid()
        {
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 10, Padding = new Padding(16), BackColor = AppColors.BgBase, AutoScroll = true };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, DefaultInputRowHeight));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, MultilineInputRowHeight));
            for (int i = 2; i < 10; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, i == 6 ? MultilineInputRowHeight : DefaultInputRowHeight));
            return grid;
        }

        private static void AddRow(TableLayoutPanel grid, int row, string label, Control editor)
        {
            grid.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, row);
            editor.Dock = DockStyle.Fill;
            grid.Controls.Add(editor, 1, row);
        }

        private bool ValidateAndApply()
        {
            if (string.IsNullOrWhiteSpace(_name.Text))
            {
                CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Tên khóa học không được để trống.");
                return false;
            }

            if (_startDate.Value.Date > _endDate.Value.Date)
            {
                CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
                return false;
            }

            TimeSpan start = _sessionStart.Value.TimeOfDay;
            TimeSpan end = _sessionEnd.Value.TimeOfDay;
            if (_generateSchedule.Checked && end <= start)
            {
                CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Giờ kết thúc phải sau giờ bắt đầu.");
                return false;
            }

            var days = _teachingDays.CheckedItems.Cast<DayItem>().Select(d => d.Day).ToList();
            if (_generateSchedule.Checked && days.Count == 0)
            {
                CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Vui lòng chọn ít nhất một thứ trong tuần để tạo lịch dạy.");
                return false;
            }

            Course.Name = _name.Text.Trim();
            Course.Description = _description.Text.Trim();
            Course.Status = string.IsNullOrWhiteSpace(Course.Status) ? WorkflowConstants.CourseStatus.Draft : Course.Status;
            Course.StartDate = _startDate.Value.Date;
            Course.EndDate = _endDate.Value.Date;
            Course.GenerateScheduleOnCreate = _generateSchedule.Checked;
            Course.TeachingDays = days;
            Course.SessionStartTime = start;
            Course.SessionEndTime = end;
            Course.MeetingLink = _meetingLink.Text.Trim();
            return true;
        }

        private static IEnumerable<DayItem> DayItems()
        {
            yield return new DayItem(DayOfWeek.Monday, "Thứ 2");
            yield return new DayItem(DayOfWeek.Tuesday, "Thứ 3");
            yield return new DayItem(DayOfWeek.Wednesday, "Thứ 4");
            yield return new DayItem(DayOfWeek.Thursday, "Thứ 5");
            yield return new DayItem(DayOfWeek.Friday, "Thứ 6");
            yield return new DayItem(DayOfWeek.Saturday, "Thứ 7");
            yield return new DayItem(DayOfWeek.Sunday, "Chủ nhật");
        }

        private sealed class DayItem
        {
            public DayItem(DayOfWeek day, string label)
            {
                Day = day;
                Label = label;
            }

            public DayOfWeek Day { get; }
            public string Label { get; }
            public override string ToString() => Label;
        }
    }
}
