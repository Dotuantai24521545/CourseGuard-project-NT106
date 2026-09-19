using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls.Shared.CourseWorkspace;

namespace CourseGuard.Frontend.UserControls.Teacher
{
    public partial class UC_TeacherMessages : UserControl
    {
        private readonly int _teacherId;
        private readonly ChatController _chat = new(new CourseGuardDbContext(""));
        private readonly List<ChatCourseModel> _courses = new();
        private readonly ListBox _courseList = new();
        private readonly CourseChatPanel _chatPanel;
        private bool _uiAlive = true;

        public UC_TeacherMessages(int teacherId)
        {
            _teacherId = teacherId;
            _chatPanel = new CourseChatPanel(teacherId, _chat, allowPollCreation: true);

            InitializeComponent();
            BuildLayout();
            ApplyStyles();

            _courseList.SelectedIndexChanged += async (_, _) => await OnCourseChangedAsync();
            _courseList.DrawItem += DrawCourseItem;
            Load += async (_, _) => await LoadCoursesAsync();
            Disposed += (_, _) => _uiAlive = false;
        }

        private void BuildLayout()
        {
            var root = TeacherTabChrome.CreateRoot(this);
            root.Controls.Add(TeacherTabChrome.CreateHeader(
                "Tin nhắn",
                "Trao đổi với học viên trong các phòng chat khóa học."), 0, 0);

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.BgBase,
                ColumnCount = 2,
                RowCount = 1
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24f));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 76f));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var courseCard = TeacherTabChrome.CreateDataCard("Khóa học", _courseList);
            courseCard.Margin = new Padding(0, 0, 8, 0);
            content.Controls.Add(courseCard, 0, 0);

            var chatCard = TeacherTabChrome.CreateCard();
            chatCard.Padding = new Padding(12);
            chatCard.Margin = new Padding(8, 0, 0, 0);
            chatCard.Controls.Add(_chatPanel);

            content.Controls.Add(chatCard, 1, 0);
            root.Controls.Add(content, 0, 1);
            TeacherTabChrome.EnableNaturalFocusClear(this);
        }

        private void ApplyStyles()
        {
            BackColor = AppColors.BgBase;
            _courseList.BorderStyle = BorderStyle.None;
            _courseList.BackColor = AppColors.BgCard;
            _courseList.ForeColor = AppColors.TextPrimary;
            _courseList.Font = AppFonts.Semibold(11f);
            _courseList.DrawMode = DrawMode.OwnerDrawFixed;
            _courseList.ItemHeight = 42;
            _courseList.IntegralHeight = false;
            _courseList.HorizontalScrollbar = false;
            _courseList.Margin = new Padding(0);
        }

        private void DrawCourseItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _courseList.Items.Count)
            {
                return;
            }

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Rectangle bounds = e.Bounds;
            Color backColor = selected ? MetaTheme.Colors.Primary : AppColors.BgCard;
            Color textColor = selected ? Color.White : AppColors.TextPrimary;

            using var canvas = new SolidBrush(AppColors.BgCard);
            e.Graphics.FillRectangle(canvas, bounds);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle itemBounds = new(bounds.Left + 8, bounds.Top + 3, bounds.Width - 16, bounds.Height - 6);
            using GraphicsPath itemPath = CreateRoundedRectanglePath(itemBounds, 10);
            using var background = new SolidBrush(backColor);
            e.Graphics.FillPath(background, itemPath);

            if (!selected)
            {
                using var border = new Pen(MetaTheme.Colors.BorderSoft);
                e.Graphics.DrawPath(border, itemPath);
            }

            Rectangle textBounds = new(itemBounds.Left + 12, itemBounds.Top + 1, itemBounds.Width - 24, itemBounds.Height - 2);
            TextRenderer.DrawText(
                e.Graphics,
                _courseList.Items[e.Index]?.ToString() ?? string.Empty,
                _courseList.Font,
                textBounds,
                textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            e.DrawFocusRectangle();
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private async Task LoadCoursesAsync()
        {
            _courseList.Items.Clear();
            await _chatPanel.LoadCourseAsync(0, string.Empty);

            if (_teacherId <= 0)
            {
                return;
            }

            List<ChatCourseModel> courses = await Task.Run(() => _chat.GetMyCourses(_teacherId));
            if (!_uiAlive || IsDisposed)
            {
                return;
            }

            _courses.Clear();
            _courses.AddRange(courses);
            _courseList.Items.Clear();
            foreach (ChatCourseModel course in _courses)
            {
                string display = course.CourseName;
                if (!string.IsNullOrWhiteSpace(course.ClassCode))
                {
                    display += $" - {course.ClassCode}";
                }

                _courseList.Items.Add(display);
            }

            if (_courseList.Items.Count == 0)
            {
                await _chatPanel.LoadCourseAsync(0, string.Empty);
                return;
            }

            _courseList.SelectedIndex = 0;
        }

        private async Task OnCourseChangedAsync()
        {
            int index = _courseList.SelectedIndex;
            if (index < 0 || index >= _courses.Count)
            {
                await _chatPanel.LoadCourseAsync(0, string.Empty);
                return;
            }

            ChatCourseModel course = _courses[index];
            await _chatPanel.LoadCourseAsync(course.CourseId, course.CourseName);
        }

        private void AppendLocalSendStatus(string messageType, string status, string preview, string? errorMessage = null)
        {
            if (!_uiAlive || IsDisposed || !string.Equals(status, "FAILED", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            MetaTheme.ShowModernDialog(errorMessage ?? "Gửi tin nhắn thất bại.", "Chat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
