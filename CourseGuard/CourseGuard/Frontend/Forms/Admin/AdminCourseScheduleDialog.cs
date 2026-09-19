using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.Forms.Teacher;
using CourseGuard.Frontend.UserControls.Teacher;

namespace CourseGuard.Frontend.Forms.Admin
{
    public class AdminCourseScheduleDialog : ThemedDialogBase
    {
        private readonly CheckBox _generateSchedule = new();
        private readonly CheckedListBox _teachingDays = new();
        private readonly DateTimePicker _sessionStart = new();
        private readonly DateTimePicker _sessionEnd = new();
        private readonly TextBox _meetingLink = new();

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string TeachingDaysResult { get; private set; } = string.Empty;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public TimeSpan? SessionStartTimeResult { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public TimeSpan? SessionEndTimeResult { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string MeetingLinkResult { get; set; } = string.Empty;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool GenerateScheduleResult { get; set; }

        public AdminCourseScheduleDialog(
            string currentTeachingDays,
            TimeSpan? currentStartTime,
            TimeSpan? currentEndTime,
            string currentMeetingLink,
            bool currentGenerateSchedule)
        {
            Text = "Cấu hình lịch học";
            Width = 600;
            Height = 480;

            _generateSchedule.Text = "Tự động tạo lịch học (online sessions)";
            _generateSchedule.Checked = currentGenerateSchedule;

            _teachingDays.CheckOnClick = true;
            _teachingDays.Height = 120;

            // DayOfWeek enum starts with Sunday=0, Monday=1, etc.
            var daysList = DayItems().ToList();
            foreach (var day in daysList)
            {
                _teachingDays.Items.Add(day);
            }

            // Parse current teaching days and check corresponding list items
            if (!string.IsNullOrEmpty(currentTeachingDays))
            {
                var splitDays = currentTeachingDays.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                  .Select(d => d.Trim())
                                                  .ToList();
                for (int i = 0; i < _teachingDays.Items.Count; i++)
                {
                    if (_teachingDays.Items[i] is DayItem item && splitDays.Contains(item.Day.ToString()))
                    {
                        _teachingDays.SetItemChecked(i, true);
                    }
                }
            }

            _sessionStart.Format = DateTimePickerFormat.Custom;
            _sessionStart.CustomFormat = "HH:mm";
            _sessionStart.ShowUpDown = true;
            _sessionStart.Value = DateTime.Today.Add(currentStartTime ?? new TimeSpan(19, 0, 0));

            _sessionEnd.Format = DateTimePickerFormat.Custom;
            _sessionEnd.CustomFormat = "HH:mm";
            _sessionEnd.ShowUpDown = true;
            _sessionEnd.Value = DateTime.Today.Add(currentEndTime ?? new TimeSpan(21, 0, 0));

            _meetingLink.Text = currentMeetingLink;

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
            AddRow(grid, 0, "Tự động tạo lịch", _generateSchedule);
            AddRow(grid, 1, "Thứ trong tuần", _teachingDays);
            AddRow(grid, 2, "Giờ bắt đầu", _sessionStart);
            AddRow(grid, 3, "Giờ kết thúc", _sessionEnd);
            AddRow(grid, 4, "Link cuộc họp", _meetingLink);

            ContentPanel.Controls.Add(grid);
            AddFooterButtons(cancel, save);

            AcceptButton = save;
            CancelButton = cancel;
        }

        private TableLayoutPanel CreateGrid()
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(16),
                BackColor = AppColors.BgBase
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, TeacherCourseDialog.DefaultInputRowHeight)); // checkbox
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 130)); // teaching days
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, TeacherCourseDialog.DefaultInputRowHeight)); // start time
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, TeacherCourseDialog.DefaultInputRowHeight)); // end time
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // meeting link
            return grid;
        }

        private static void AddRow(TableLayoutPanel grid, int row, string label, Control editor)
        {
            grid.Controls.Add(new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Font = MetaTheme.Fonts.BodyMdBold()
            }, 0, row);
            
            editor.Dock = DockStyle.Fill;
            grid.Controls.Add(editor, 1, row);
        }

        private bool ValidateAndApply()
        {
            TimeSpan start = _sessionStart.Value.TimeOfDay;
            TimeSpan end = _sessionEnd.Value.TimeOfDay;

            if (_generateSchedule.Checked)
            {
                if (end <= start)
                {
                    MetaTheme.ShowModernDialog("Giờ kết thúc phải sau giờ bắt đầu.");
                    return false;
                }

                var checkedDays = _teachingDays.CheckedItems.Cast<DayItem>().Select(d => d.Day).ToList();
                if (checkedDays.Count == 0)
                {
                    MetaTheme.ShowModernDialog("Vui lòng chọn ít nhất một thứ trong tuần để tạo lịch dạy.");
                    return false;
                }

                TeachingDaysResult = string.Join(",", checkedDays);
            }
            else
            {
                var checkedDays = _teachingDays.CheckedItems.Cast<DayItem>().Select(d => d.Day).ToList();
                TeachingDaysResult = string.Join(",", checkedDays);
            }

            SessionStartTimeResult = start;
            SessionEndTimeResult = end;
            MeetingLinkResult = _meetingLink.Text.Trim();
            GenerateScheduleResult = _generateSchedule.Checked;

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
