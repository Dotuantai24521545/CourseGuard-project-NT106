using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls.Shared.CourseWorkspace;

namespace CourseGuard.Frontend.UserControls.Student
{
    public partial class UC_Chat : UserControl
    {
        private readonly ChatController _chatController;
        private readonly List<ChatCourseModel> _courses = new();
        private readonly int _userId;
        private readonly CourseChatPanel _chatPanel;
        private bool _uiAlive = true;

        public UC_Chat()
            : this(UserSessionContext.CurrentUserId ?? 0, new ChatController(new CourseGuardDbContext("")))
        {
        }

        public UC_Chat(int userId, ChatController chatController)
        {
            _userId = userId;
            _chatController = chatController ?? throw new ArgumentNullException(nameof(chatController));
            _chatPanel = new CourseChatPanel(userId, _chatController, allowPollCreation: false);

            InitializeComponent();
            BuildCardLayout();
            ApplyCardStyle();

            lstContacts.SelectedIndexChanged += async (_, _) => await OnCourseChangedAsync();
            lstContacts.DrawItem += DrawCourseItem;
            Load += async (_, _) => await LoadCoursesAsync();
            Disposed += (_, _) => _uiAlive = false;
        }

        private void BuildCardLayout()
        {
            var root = StudentTabChrome.CreateRoot(this);
            root.Controls.Add(StudentTabChrome.CreateHeader(
                "Tin nhắn",
                "Trao đổi với lớp học và giáo viên trong các khóa đã tham gia."), 0, 0);

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

            var contactsCard = StudentTabChrome.CreateDataCard("Khóa học", lstContacts);
            contactsCard.Margin = new Padding(0, 0, 12, 0);

            var messageShell = StudentTabChrome.CreateCard();
            messageShell.Padding = new Padding(18);
            messageShell.Margin = new Padding(12, 0, 0, 0);
            messageShell.Controls.Add(_chatPanel);

            content.Controls.Add(contactsCard, 0, 0);
            content.Controls.Add(messageShell, 1, 0);
            root.Controls.Add(content, 0, 1);
            StudentTabChrome.EnableNaturalFocusClear(this);
        }

        private void ApplyCardStyle()
        {
            BackColor = AppColors.BgBase;
            lstContacts.BorderStyle = BorderStyle.None;
            lstContacts.BackColor = AppColors.BgCard;
            lstContacts.ForeColor = AppColors.TextPrimary;
            lstContacts.Font = AppFonts.Semibold(11f);
            lstContacts.DrawMode = DrawMode.OwnerDrawFixed;
            lstContacts.ItemHeight = 42;
            lstContacts.IntegralHeight = false;
            lstContacts.HorizontalScrollbar = false;
            lstContacts.Margin = new Padding(0);
            txtMessages.Visible = false;
            txtInput.Visible = false;
            btnSend.Visible = false;
        }

        private void DrawCourseItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= lstContacts.Items.Count)
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
                lstContacts.Items[e.Index]?.ToString() ?? string.Empty,
                lstContacts.Font,
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
            this.ShowSkeleton(SkeletonType.ChatLayout);
            try
            {
                lstContacts.Items.Clear();
                await _chatPanel.LoadCourseAsync(0, string.Empty);

                if (_userId <= 0)
                {
                    return;
                }

                List<ChatCourseModel> courses = await _chatController.GetMyCoursesAsync(_userId);
                if (!_uiAlive || IsDisposed)
                {
                    return;
                }

                _courses.Clear();
                _courses.AddRange(courses);

                lstContacts.Items.Clear();
                foreach (ChatCourseModel course in _courses)
                {
                    string roleTag = course.IsTeacherCourse ? "GV" : "HV";
                    string display = $"[{roleTag}] {course.CourseName}";
                    if (!string.IsNullOrWhiteSpace(course.ClassCode))
                    {
                        display += $" - {course.ClassCode}";
                    }

                    lstContacts.Items.Add(display);
                }

                if (lstContacts.Items.Count > 0)
                {
                    lstContacts.SelectedIndex = 0;
                }
                else
                {
                    await _chatPanel.LoadCourseAsync(0, string.Empty);
                }
            }
            finally
            {
                this.HideSkeleton();
            }
        }

        private async Task OnCourseChangedAsync()
        {
            if (!_uiAlive || IsDisposed)
            {
                return;
            }

            int idx = lstContacts.SelectedIndex;
            if (idx < 0 || idx >= _courses.Count)
            {
                await _chatPanel.LoadCourseAsync(0, string.Empty);
                return;
            }

            ChatCourseModel course = _courses[idx];
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
