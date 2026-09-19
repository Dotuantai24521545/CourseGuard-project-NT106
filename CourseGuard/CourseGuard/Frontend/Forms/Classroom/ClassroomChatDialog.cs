using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls.Shared.CourseWorkspace;
using CourseGuard.Frontend.Forms.Teacher;
using System.Runtime.InteropServices;

namespace CourseGuard.Frontend.Forms.Classroom
{
    public sealed class ClassroomChatDialog : ThemedDialogBase
    {
        private ListBox? _chatList;
        private TextBox? _chatInput;
        private Button? _btnSend;
        private readonly CourseChatPanel? _courseChatPanel;
        private readonly CancellationTokenSource _courseChatLoadCts = new();
        private readonly int _courseId;
        private readonly string _courseName;
        private bool _courseChatLoaded;

        public event Func<string, Task>? SendRequested;

        public ClassroomChatDialog(string title)
            : this(title, 0, 0, string.Empty, allowPollCreation: false)
        {
        }

        public ClassroomChatDialog(
            string title,
            int userId,
            int courseId,
            string courseName,
            bool allowPollCreation)
        {
            _courseId = courseId;
            _courseName = courseName ?? string.Empty;

            Text = string.IsNullOrWhiteSpace(title) ? "Chat lớp học" : title;
            StartPosition = FormStartPosition.Manual;
            MinimumSize = new Size(420, 520);
            Size = new Size(520, 660);
            ShowInTaskbar = false;

            if (userId > 0 && courseId > 0)
            {
                _courseChatPanel = new CourseChatPanel(
                    userId,
                    new ChatController(new CourseGuardDbContext("")),
                    allowPollCreation);
                _courseChatPanel.Dock = DockStyle.Fill;
                _courseChatPanel.ShowEmbeddedHeader = false;
                ContentPanel.Controls.Add(_courseChatPanel);
                Shown += async (_, _) => await LoadCourseChatAsync();
            }
            else
            {
                var shell = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    RowCount = 2,
                    ColumnCount = 1,
                    BackColor = Color.Transparent,
                    Padding = Padding.Empty,
                    Margin = Padding.Empty
                };
                shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));

                _chatList = CreateFallbackMessageList();
                shell.Controls.Add(_chatList, 0, 0);

                var composer = CreateFallbackComposer();
                shell.Controls.Add(composer, 0, 1);
                
                ContentPanel.Controls.Add(shell);
            }

            FormClosing += (_, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    Hide();
                }
            };

            Disposed += (_, _) =>
            {
                _courseChatLoadCts.Cancel();
                _courseChatLoadCts.Dispose();
            };
        }

        public bool IsOpen => Visible && !IsDisposed;

        public void AppendMessage(string senderName, string message, bool isTeacher)
        {
            if (IsDisposed || _chatList == null)
                return;

            string label = isTeacher ? "GV" : "HS";
            _chatList.Items.Add($"[{DateTime.Now:HH:mm}] {label} {senderName}: {message}");
            _chatList.TopIndex = Math.Max(0, _chatList.Items.Count - 1);
        }

        public void FocusComposer()
        {
            if (IsDisposed)
                return;

            if (_chatInput != null)
            {
                _chatInput.Focus();
                return;
            }

            if (_courseChatPanel != null)
                _courseChatPanel.Focus();
        }

        private static ListBox CreateFallbackMessageList()
        {
            return new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ForeColor = AppColors.TextPrimary,
                BorderStyle = BorderStyle.None,
                Font = MetaTheme.Fonts.BodyMdBold(),
                IntegralHeight = false,
                HorizontalScrollbar = true,
                Margin = Padding.Empty
            };
        }

        private Control CreateFallbackComposer()
        {
            var composer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 2,
                BackColor = Color.Transparent,
                Padding = new Padding(14, 10, 14, 10),
                Margin = Padding.Empty
            };
            composer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            composer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

            _chatInput = new TextBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                BackColor = AppColors.BgInput,
                ForeColor = AppColors.TextPrimary,
                BorderStyle = BorderStyle.None,
                Font = MetaTheme.Fonts.BodyMdBold(),
                PlaceholderText = "Nhập tin nhắn...",
                Margin = new Padding(0, 7, 10, 0)
            };
            DialogInputStyler.StyleExistingControl(_chatInput);
            
            _chatInput.KeyDown += async (_, e) =>
            {
                if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    e.SuppressKeyPress = true;
                    await SendCurrentMessageAsync();
                }
            };
            composer.Controls.Add(_chatInput, 0, 0);

            _btnSend = new Button
            {
                Dock = DockStyle.Fill,
                Text = "Gửi",
                BackColor = AppColors.AccentBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = MetaTheme.Fonts.BodySmBold(),
                Cursor = Cursors.Hand,
                Margin = Padding.Empty
            };
            _btnSend.FlatAppearance.BorderSize = 0;
            RoundedButtonHelper.Apply(_btnSend, 10);
            _btnSend.Click += async (_, _) => await SendCurrentMessageAsync();
            composer.Controls.Add(_btnSend, 1, 0);

            return composer;
        }

        private async Task LoadCourseChatAsync()
        {
            if (_courseChatPanel == null || _courseChatLoaded)
                return;

            _courseChatLoaded = true;
            try
            {
                await _courseChatPanel.LoadCourseAsync(_courseId, _courseName, _courseChatLoadCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog("Không thể tải chat khóa học: " + ex.Message, "Chat lớp học");
            }
        }

        private async Task SendCurrentMessageAsync()
        {
            if (_chatInput == null)
                return;

            string message = _chatInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(message)) return;

            _chatInput.Clear();
            if (SendRequested != null)
            {
                await SendRequested.Invoke(message);
            }
        }
    }
}
