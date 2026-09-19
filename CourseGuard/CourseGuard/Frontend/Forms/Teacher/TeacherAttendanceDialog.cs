using System;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls.Teacher;

namespace CourseGuard.Frontend.Forms.Teacher
{
    public sealed class TeacherAttendanceDialog : ThemedDialogBase
    {
        private readonly int _teacherId;
        private readonly TeacherController _controller;
        private readonly BindingSource _bindingSource = new();
        private bool _isBindingSessions;

        private ComboBox _sessionCombo = null!;
        private DataGridView _grid = null!;
        private Label _emptyLabel = null!;

        public TeacherAttendanceDialog(int teacherId, TeacherController controller)
        {
            _teacherId = teacherId;
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));

            Text = "Điểm danh buổi học";
            Width = 920;
            Height = 600;

            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            LoadSessions();
        }

        private void InitializeComponent()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = AppColors.BgCard
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            ContentPanel.Controls.Add(root);

            var filter = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = AppColors.BgCard,
                Padding = new Padding(0, 6, 0, 6)
            };
            root.Controls.Add(filter, 0, 0);

            filter.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "Buổi học:",
                ForeColor = AppColors.TextPrimary,
                Font = MetaTheme.Fonts.BodyMdBold(),
                Margin = new Padding(0, 8, 10, 0)
            });

            _sessionCombo = new ComboBox
            {
                Width = 460,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = MetaTheme.Fonts.BodyMd(),
                DisplayMember = nameof(SessionOption.DisplayText),
                ValueMember = nameof(SessionOption.Id)
            };
            _sessionCombo.SelectedIndexChanged += (_, _) =>
            {
                if (_isBindingSessions)
                    return;

                LoadAttendance();
            };
            filter.Controls.Add(_sessionCombo);

            var body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.BgCard
            };
            root.Controls.Add(body, 0, 1);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                DataSource = _bindingSource
            };
            TeacherTabChrome.StyleGrid(_grid);
            body.Controls.Add(_grid);

            _emptyLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = AppColors.TextSecondary,
                Font = MetaTheme.Fonts.BodyMdBold(),
                Visible = false
            };
            body.Controls.Add(_emptyLabel);
            _emptyLabel.BringToFront();

            var closeButton = TeacherTabChrome.SecondaryButton("Đóng");
            closeButton.Click += (_, _) => Close();
            AddFooterButtons(closeButton);
            CancelButton = closeButton;
        }

        private void LoadSessions()
        {
            var sessions = _controller.GetSchedule(_teacherId)
                .OrderByDescending(s => s.StartTime ?? DateTime.MinValue)
                .ThenByDescending(s => s.Id)
                .Select(s => new SessionOption(s))
                .ToList();

            _isBindingSessions = true;
            try
            {
                _sessionCombo.DataSource = sessions;
            }
            finally
            {
                _isBindingSessions = false;
            }

            bool hasSessions = sessions.Count > 0;
            _sessionCombo.Enabled = hasSessions;
            _grid.Visible = hasSessions;
            _emptyLabel.Visible = !hasSessions;
            _emptyLabel.Text = "Chưa có buổi học để xem điểm danh.";

            if (hasSessions)
                LoadAttendance();
        }

        private void LoadAttendance()
        {
            if (_sessionCombo.SelectedItem is not SessionOption selected || selected.Id <= 0)
                return;

            var logs = _controller.GetAttendanceSummary(_teacherId, selected.Id);
            var table = new DataTable();
            table.Columns.Add("Họ tên", typeof(string));
            table.Columns.Add("Vào lúc", typeof(string));
            table.Columns.Add("Ra lúc", typeof(string));
            table.Columns.Add("Thời lượng", typeof(string));
            table.Columns.Add("Hợp lệ", typeof(string));

            foreach (AttendanceLogModel log in logs)
            {
                table.Rows.Add(
                    log.StudentName,
                    log.JoinedAt.ToString("dd/MM/yyyy HH:mm"),
                    log.LeftAt?.ToString("dd/MM/yyyy HH:mm") ?? "",
                    $"{log.DurationMinutes} phút",
                    log.IsValid ? "Có" : "Không");
            }

            _bindingSource.DataSource = table;
            bool hasLogs = logs.Count > 0;
            _grid.Visible = hasLogs;
            _emptyLabel.Visible = !hasLogs;
            _emptyLabel.Text = "Chưa có lần điểm danh nào cho buổi học này.";
        }

        private sealed class SessionOption
        {
            public SessionOption(TeacherScheduleItemModel session)
            {
                Id = session.Id;
                string date = session.StartTime?.ToString("dd/MM/yyyy HH:mm") ?? "Chưa có thời gian";
                DisplayText = $"{date} - {session.CourseName} - {session.Title}";
            }

            public int Id { get; }
            public string DisplayText { get; }
        }
    }
}
