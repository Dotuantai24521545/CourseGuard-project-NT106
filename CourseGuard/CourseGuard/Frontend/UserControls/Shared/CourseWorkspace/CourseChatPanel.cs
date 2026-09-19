using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Services.Realtime;
using CourseGuard.Frontend.Forms.Teacher;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls.Shared.Chat;

namespace CourseGuard.Frontend.UserControls.Shared.CourseWorkspace
{
    public class CourseChatPanel : UserControl
    {
        private const int ComposerMinimumHeight = 60;
        private const int ComposerHostHeight = 96;

        private readonly int _userId;
        private readonly ChatController _chatController;
        private readonly bool _allowPollCreation;
        private readonly Label _title = new();
        private readonly ChatMessageListControl _messageList = new();
        private readonly RoundedPanel _messageListShell;
        private readonly TextBox _input = new();
        private readonly RoundedPanel _composerInputShell;
        private readonly ChatImagePreviewStripControl _imagePreviewStrip = new();
        private readonly Button _send = new() { Text = "Gửi" };
        private readonly Button _attachMenu = new() { Text = "+" };
        private readonly ContextMenuStrip _attachStrip = new();
        private TableLayoutPanel? _chatGrid;
        private readonly object _messageLoadSync = new();
        private CancellationTokenSource? _messageLoadCts;
        private int _selectedCourseId;
        private int _messageLoadGeneration;
        private int _activeMessageLoads;
        private bool _isLoadingOlderMessages;
        private bool _hasLoadedInitialMessages;
        private bool _isSending;
        private bool _uiAlive = true;
        private int _nextTemporaryMessageId = -1;
        private static Color CourseSurfaceColor => AppColors.IsDarkMode ? AppColors.BgCard : Color.White;

        internal static bool StartRealtimeForTesting = true;
        internal Func<int, int, int, CancellationToken, Task<List<ChatMessageModel>>>? MessageLoaderForTesting;

        public event EventHandler? MessagesMarkedRead;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowEmbeddedHeader
        {
            get => _title.Visible;
            set
            {
                _title.Visible = value;
                if (_chatGrid != null && _chatGrid.RowStyles.Count > 0)
                    _chatGrid.RowStyles[0].Height = value ? 36f : 0f;
            }
        }

        public CourseChatPanel(int userId, ChatController chatController, bool allowPollCreation)
        {
            _userId = userId;
            _chatController = chatController ?? throw new ArgumentNullException(nameof(chatController));
            _allowPollCreation = allowPollCreation;
            _messageListShell = CreateMessageListShell();
            _composerInputShell = CreateComposerInputShell();

            BuildLayout();
            ApplyStyles();
            WireEvents();

            SupabaseRealtimeChatService.Instance.OnChatChanged += OnChatRealtimeChanged;
            if (StartRealtimeForTesting)
            {
                SupabaseRealtimeChatService.Instance.Start();
            }

            Disposed += (_, _) =>
            {
                _uiAlive = false;
                CancelActiveMessageLoad();
                _attachStrip.Dispose();
                SupabaseRealtimeChatService.Instance.OnChatChanged -= OnChatRealtimeChanged;
            };
        }

        public async Task LoadCourseAsync(int courseId, string courseName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (loadGeneration, loadCts) = BeginCourseMessageLoad(cancellationToken);
            _selectedCourseId = courseId;
            _hasLoadedInitialMessages = false;
            _messageList.ClearMessages();
            _title.Text = string.IsNullOrWhiteSpace(courseName)
                ? "Nội dung trao đổi"
                : $"Nội dung trao đổi - {courseName}";

            if (courseId <= 0)
            {
                DisposeMessageLoadSource(loadCts);
                return;
            }

            await RefreshMessagesAsync(loadGeneration, loadCts.Token, loadCts);
        }

        private void BuildLayout()
        {
            Dock = DockStyle.Fill;
            Tag = "custom";
            BackColor = CourseSurfaceColor;
            Padding = new Padding(0, 0, 0, 8);
            AllowDrop = true;
            _messageList.AllowDrop = true;
            BuildAttachmentMenu();

            var chatGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = CourseSurfaceColor,
                ColumnCount = 1,
                RowCount = 4
            };
            _chatGrid = chatGrid;
            chatGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
            chatGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            chatGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 0f));
            chatGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 112f));

            _title.Dock = DockStyle.Fill;
            _title.AutoSize = false;
            _title.BackColor = CourseSurfaceColor;
            _title.Font = AppFonts.Semibold(12f);
            _title.ForeColor = AppColors.TextPrimary;
            _title.Text = "Nội dung trao đổi";
            _title.TextAlign = ContentAlignment.MiddleLeft;
            chatGrid.Controls.Add(_title, 0, 0);

            _messageListShell.Dock = DockStyle.Fill;
            _messageListShell.Margin = new Padding(0);
            chatGrid.Controls.Add(_messageListShell, 0, 1);

            var composerHost = new Panel
            {
                BackColor = CourseSurfaceColor,
                Dock = DockStyle.Bottom,
                Height = ComposerHostHeight,
                Margin = Padding.Empty,
                MinimumSize = new Size(0, ComposerMinimumHeight),
                Padding = new Padding(0, 0, 0, 8),
                Tag = "custom"
            };

            var composer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = CourseSurfaceColor,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0, 12, 0, 10)
            };
            composer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            composer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            composer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74f));
            composer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96f));

            _input.PlaceholderText = "Nhập tin nhắn...";
            _composerInputShell.Dock = DockStyle.Fill;
            _composerInputShell.Margin = new Padding(0, 8, 10, 10);
            _attachMenu.Dock = DockStyle.Fill;
            _attachMenu.Margin = new Padding(0, 8, 10, 10);
            _send.Dock = DockStyle.Fill;
            _send.Margin = new Padding(0, 8, 0, 10);

            composer.Controls.Add(_composerInputShell, 0, 0);
            composer.Controls.Add(_attachMenu, 1, 0);
            composer.Controls.Add(_send, 2, 0);

            _imagePreviewStrip.Margin = new Padding(0, 8, 0, 0);
            chatGrid.Controls.Add(_imagePreviewStrip, 0, 2);
            composerHost.Controls.Add(composer);
            chatGrid.Controls.Add(composerHost, 0, 3);
            Controls.Add(chatGrid);
            UpdateImagePreviewVisibility(focusInput: false);
        }

        private void BuildAttachmentMenu()
        {
            _attachStrip.Items.Clear();

            var imageItem = new ToolStripMenuItem("Gửi ảnh");
            imageItem.Click += (_, _) => PickAndSendImageAsync().FireAndForgetSafe(this);

            var fileItem = new ToolStripMenuItem("Gửi tài liệu");
            fileItem.Click += (_, _) => PickAndSendFileAsync().FireAndForgetSafe(this);

            var separator = new ToolStripSeparator { Visible = _allowPollCreation };

            var pollItem = new ToolStripMenuItem("Tạo vote") { Visible = _allowPollCreation };
            pollItem.Click += (_, _) => CreatePollAsync().FireAndForgetSafe(this);

            _attachStrip.Items.Add(imageItem);
            _attachStrip.Items.Add(fileItem);
            _attachStrip.Items.Add(separator);
            _attachStrip.Items.Add(pollItem);
            StudentDropdownStyler.Apply(_attachStrip, 190);
        }

        private RoundedPanel CreateMessageListShell()
        {
            var shell = new RoundedPanel
            {
                BackColor = CourseSurfaceColor,
                BorderColor = AppColors.Border,
                CornerRadius = 12,
                FillColor = CourseSurfaceColor,
                Padding = new Padding(10)
            };

            _messageList.Dock = DockStyle.Fill;
            _messageList.Margin = new Padding(2);
            shell.Controls.Add(_messageList);
            return shell;
        }

        private RoundedPanel CreateComposerInputShell()
        {
            _input.PlaceholderText = "Nhập tin nhắn...";
            _input.BorderStyle = BorderStyle.None;
            _input.Font = AppFonts.Body;
            _input.Margin = Padding.Empty;
            _input.Multiline = true;
            _input.WordWrap = true;
            _input.AcceptsReturn = true;
            _input.ScrollBars = ScrollBars.None;

            var shell = new RoundedPanel
            {
                BackColor = Color.Transparent,
                BorderColor = AppColors.Border,
                CornerRadius = 12,
                Cursor = Cursors.IBeam,
                FillColor = AppColors.BgInput,
                Padding = new Padding(12, 8, 12, 8)
            };

            shell.Controls.Add(_input);
            shell.MouseDown += (_, _) => FocusInputIfAvailable();
            shell.Resize += (_, _) => LayoutComposerInput(shell);
            _input.GotFocus += (_, _) => UpdateComposerInputChrome(shell, focused: true);
            _input.LostFocus += (_, _) => UpdateComposerInputChrome(shell, focused: false);
            LayoutComposerInput(shell);
            return shell;
        }

        private void ApplyStyles()
        {
            _messageListShell.BackColor = CourseSurfaceColor;
            _messageListShell.FillColor = CourseSurfaceColor;
            _messageListShell.BorderColor = AppColors.Border;
            _messageList.BackColor = CourseSurfaceColor;
            _composerInputShell.FillColor = AppColors.BgInput;
            _composerInputShell.BorderColor = AppColors.Border;
            _composerInputShell.BackColor = Color.Transparent;
            _input.BackColor = _composerInputShell.FillColor;
            _input.ForeColor = AppColors.TextPrimary;
            _input.Font = AppFonts.Body;
            MetaTheme.StylePrimaryButton(_send);
            MetaTheme.StyleSecondaryButton(_attachMenu);
            RoundedButtonHelper.Apply(12, _send, _attachMenu);
            StudentDropdownStyler.Apply(_attachStrip, 190);
        }

        private void LayoutComposerInput(RoundedPanel shell)
        {
            if (_input.IsDisposed)
                return;

            int inputTop = shell.Padding.Top;
            int inputHeight = Math.Max(24, shell.Height - shell.Padding.Vertical);
            _input.SetBounds(shell.Padding.Left, inputTop, Math.Max(40, shell.Width - shell.Padding.Horizontal), inputHeight);
        }

        private void UpdateComposerInputChrome(RoundedPanel shell, bool focused)
        {
            shell.BorderColor = focused ? MetaTheme.Colors.BorderFocus : AppColors.Border;
            shell.Invalidate();
        }

        private void WireEvents()
        {
            DragEnter += OnChatDragEnter;
            DragDrop += async (_, e) => await OnChatDragDropAsync(e);
            _messageList.DragEnter += OnChatDragEnter;
            _messageList.DragDrop += async (_, e) => await OnChatDragDropAsync(e);
            _messageList.TopReached += async (_, _) => await LoadOlderMessagesAsync();
            _messageList.PollVoteRequested += async (_, args) => await VotePollAsync(args);
            _messageList.PollCloseRequested += async (_, args) => await ClosePollAsync(args);
            _messageList.FileDownloadRequested += async (_, args) => await DownloadChatFileAsync(args);
            _send.Click += async (_, _) => await SendComposerAsync();
            _attachMenu.Click += (_, _) =>
            {
                int menuHeight = Math.Max(_attachStrip.Height, _attachStrip.GetPreferredSize(Size.Empty).Height);
                _attachStrip.Show(_attachMenu, new Point(0, -menuHeight));
            };
            _input.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    if (e.Shift)
                    {
                        return;
                    }

                    e.SuppressKeyPress = true;
                    SendComposerAsync().FireAndForgetSafe(this);
                }
                else if (e.Control && e.KeyCode == Keys.O)
                {
                    e.SuppressKeyPress = true;
                    PickAndSendImageAsync().FireAndForgetSafe(this);
                }
            };
            _imagePreviewStrip.ImagesChanged += (_, _) => UpdateImagePreviewVisibility();
            _imagePreviewStrip.RequestInputFocus += (_, _) => FocusInputIfAvailable();
        }

        private void OnChatRealtimeChanged(object? sender, ChatChangedEventArgs e)
        {
            if (e.CourseId != _selectedCourseId)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => OnChatRealtimeChanged(sender, e)));
                }
                catch
                {
                }

                return;
            }

            if (!_uiAlive || IsDisposed)
            {
                return;
            }

            RefreshMessagesAsync().FireAndForgetSafe(this);
        }

        private void UpdateImagePreviewVisibility(bool focusInput = true)
        {
            if (!_uiAlive || IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => UpdateImagePreviewVisibility(focusInput)));
                return;
            }

            if (_chatGrid == null || _chatGrid.IsDisposed)
            {
                return;
            }

            bool hasImages = _imagePreviewStrip.HasImages;
            _chatGrid.SuspendLayout();
            try
            {
                _imagePreviewStrip.Visible = hasImages;
                _chatGrid.RowStyles[2].Height = hasImages ? 104f : 0f;
            }
            finally
            {
                _chatGrid.ResumeLayout(true);
            }

            if (focusInput)
            {
                FocusInputIfAvailable();
            }
        }

        private Task RefreshMessagesAsync(CancellationToken cancellationToken = default)
        {
            int loadGeneration = Volatile.Read(ref _messageLoadGeneration);
            return RefreshMessagesAsync(loadGeneration, cancellationToken, loadCts: null, skipIfLoading: true);
        }

        private async Task RefreshMessagesAsync(
            int loadGeneration,
            CancellationToken cancellationToken,
            CancellationTokenSource? loadCts = null,
            bool skipIfLoading = false)
        {
            bool countedActiveLoad = false;
            try
            {
                if (!_uiAlive || IsDisposed || _selectedCourseId <= 0 || _userId <= 0)
                {
                    return;
                }

                if (skipIfLoading && Volatile.Read(ref _activeMessageLoads) > 0)
                {
                    return;
                }

                Interlocked.Increment(ref _activeMessageLoads);
                countedActiveLoad = true;
                cancellationToken.ThrowIfCancellationRequested();

                int userId = _userId;
                int courseId = _selectedCourseId;
                List<ChatMessageModel> messages;
                int newestMessageId = _messageList.NewestMessageId ?? 0;
                if (!_hasLoadedInitialMessages || newestMessageId <= 0)
                {
                    messages = await LoadInitialMessagesAsync(userId, courseId, 20, cancellationToken);
                }
                else
                {
                    messages = await _chatController.GetMessagesAfterAsync(userId, courseId, newestMessageId, 50, cancellationToken);
                }

                if (!_uiAlive
                    || IsDisposed
                    || _messageList.IsDisposed
                    || _selectedCourseId != courseId
                    || _messageLoadGeneration != loadGeneration)
                {
                    return;
                }

                if (!_hasLoadedInitialMessages)
                {
                    _messageList.SetMessages(messages, userId);
                    _hasLoadedInitialMessages = true;
                }
                else
                {
                    _messageList.AppendMessages(messages, userId);
                }

                if (messages.Count == 0)
                {
                    return;
                }

                MarkDisplayedMessagesReadAsync(courseId).FireAndForgetSafe(this);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            finally
            {
                if (countedActiveLoad)
                {
                    Interlocked.Decrement(ref _activeMessageLoads);
                }

                if (loadCts != null)
                {
                    DisposeMessageLoadSource(loadCts);
                }
            }
        }

        private Task<List<ChatMessageModel>> LoadInitialMessagesAsync(int userId, int courseId, int limit, CancellationToken cancellationToken)
        {
            var messageLoader = MessageLoaderForTesting;
            return messageLoader == null
                ? _chatController.GetMessagesAsync(userId, courseId, limit, cancellationToken)
                : messageLoader(userId, courseId, limit, cancellationToken);
        }

        private (int LoadGeneration, CancellationTokenSource LoadCts) BeginCourseMessageLoad(CancellationToken cancellationToken)
        {
            var loadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationTokenSource? previousLoadCts;
            int loadGeneration;
            lock (_messageLoadSync)
            {
                previousLoadCts = _messageLoadCts;
                _messageLoadCts = loadCts;
                loadGeneration = ++_messageLoadGeneration;
            }

            try
            {
                previousLoadCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            return (loadGeneration, loadCts);
        }

        private void CancelActiveMessageLoad()
        {
            CancellationTokenSource? loadCts;
            lock (_messageLoadSync)
            {
                _messageLoadGeneration++;
                loadCts = _messageLoadCts;
                _messageLoadCts = null;
            }

            if (loadCts == null)
            {
                return;
            }

            try
            {
                loadCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                loadCts.Dispose();
            }
        }

        private void DisposeMessageLoadSource(CancellationTokenSource loadCts)
        {
            lock (_messageLoadSync)
            {
                if (ReferenceEquals(_messageLoadCts, loadCts))
                {
                    _messageLoadCts = null;
                }
            }

            loadCts.Dispose();
        }

        private async Task LoadOlderMessagesAsync()
        {
            if (!_uiAlive || IsDisposed || _isLoadingOlderMessages || _selectedCourseId <= 0 || _userId <= 0)
            {
                return;
            }

            int? oldestMessageId = _messageList.OldestMessageId;
            if (!oldestMessageId.HasValue || oldestMessageId.Value <= 0)
            {
                return;
            }

            int userId = _userId;
            int courseId = _selectedCourseId;
            try
            {
                _isLoadingOlderMessages = true;
                List<ChatMessageModel> olderMessages = await _chatController.GetMessagesBeforeAsync(userId, courseId, oldestMessageId.Value, 20);
                if (!_uiAlive || IsDisposed || _messageList.IsDisposed || _selectedCourseId != courseId)
                {
                    return;
                }

                _messageList.PrependOlderMessages(olderMessages, userId);
            }
            finally
            {
                _isLoadingOlderMessages = false;
            }
        }

        private async Task MarkDisplayedMessagesReadAsync(int courseId)
        {
            int userId = _userId;
            if (userId <= 0 || courseId <= 0)
            {
                return;
            }

            try
            {
                await Task.Run(() => _chatController.MarkCourseRead(userId, courseId));
                if (_uiAlive && !IsDisposed && _selectedCourseId == courseId)
                {
                    MessagesMarkedRead?.Invoke(this, EventArgs.Empty);
                }
            }
            catch
            {
            }
        }

        private async Task SendTextAsync()
        {
            string content = _input.Text.Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            if (_userId <= 0 || _selectedCourseId <= 0)
            {
                MetaTheme.ShowModernDialog("Không tìm thấy phòng chat hợp lệ.", "Chat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int userId = _userId;
            int courseId = _selectedCourseId;
            int temporaryMessageId = _nextTemporaryMessageId--;
            var temporaryMessage = new ChatMessageModel
            {
                Id = temporaryMessageId,
                CourseId = courseId,
                SenderId = userId,
                SenderName = "Bạn",
                SenderRole = _allowPollCreation ? "Teacher" : "Student",
                Content = content,
                MessageType = "TEXT",
                SentAt = DateTime.Now,
                DeliveryStatus = "PENDING"
            };

            _input.Clear();
            _messageList.AppendTemporaryMessage(temporaryMessage, userId);

            try
            {
                ChatMessageModel? sentMessage = await _chatController.SendMessageAsync(userId, courseId, content);
                if (!_uiAlive || IsDisposed || _messageList.IsDisposed || _selectedCourseId != courseId)
                {
                    return;
                }

                if (sentMessage == null)
                {
                    _messageList.MarkMessageFailed(temporaryMessageId, _chatController.LastErrorMessage);
                    return;
                }

                sentMessage.DeliveryStatus = "SENT";
                _messageList.ReplaceMessage(temporaryMessageId, sentMessage, userId);
            }
            catch (Exception ex)
            {
                if (_uiAlive && !IsDisposed && !_messageList.IsDisposed)
                {
                    _messageList.MarkMessageFailed(temporaryMessageId, ex.Message);
                }
            }
        }

        private void OnChatDragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
                e.Effect = files.Length > 0 && ChatImageHelper.IsSupportedImage(files[0]) ? DragDropEffects.Copy : DragDropEffects.None;
                return;
            }

            e.Effect = DragDropEffects.None;
        }

        private Task OnChatDragDropAsync(DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
                if (files.Length > 0)
                {
                    AddImagesToPreview(files);
                }
            }

            return Task.CompletedTask;
        }

        private void AddImagesToPreview(IEnumerable<string> files)
        {
            try
            {
                _imagePreviewStrip.AddImages(files.Where(ChatImageHelper.IsSupportedImage));
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog(ex.Message, "Ảnh không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private Task PickAndSendImageAsync()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Chọn ảnh để gửi",
                Filter = "Ảnh JPG/PNG|*.jpg;*.jpeg;*.png",
                Multiselect = true
            };

            if (dialog.ShowDialog(FindForm()) == DialogResult.OK && dialog.FileNames.Length > 0)
            {
                AddImagesToPreview(dialog.FileNames);
            }

            return Task.CompletedTask;
        }

        private async Task PickAndSendFileAsync()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Chọn tài liệu để gửi",
                Filter = "Tài liệu|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.txt;*.zip;*.rar",
                Multiselect = false
            };

            if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
            {
                await SendFileAsync(dialog.FileName);
            }
        }

        private async Task SendFileAsync(string sourceFilePath)
        {
            if (_isSending)
            {
                return;
            }

            if (_userId <= 0 || _selectedCourseId <= 0)
            {
                MetaTheme.ShowModernDialog("Không tìm thấy phòng chat hợp lệ.", "Chat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                MetaTheme.ShowModernDialog("Không tìm thấy file để gửi.", "Gửi tài liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            FileInfo fileInfo = new(sourceFilePath);
            string caption = string.IsNullOrWhiteSpace(_input.Text) ? string.Empty : _input.Text.Trim();
            int userId = _userId;
            int courseId = _selectedCourseId;
            int temporaryMessageId = _nextTemporaryMessageId--;
            var temporaryMessage = new ChatMessageModel
            {
                Id = temporaryMessageId,
                CourseId = courseId,
                SenderId = userId,
                SenderName = "Bạn",
                SenderRole = _allowPollCreation ? "Teacher" : "Student",
                Content = caption,
                MessageType = "FILE",
                FileUrl = sourceFilePath,
                FileName = fileInfo.Name,
                FileSize = fileInfo.Length,
                MimeType = ResolveComposerFileMimeType(fileInfo.Extension),
                SentAt = DateTime.Now,
                DeliveryStatus = "PENDING"
            };

            _input.Clear();
            _messageList.AppendTemporaryMessage(temporaryMessage, userId);
            SetComposerSending(true, "Đang gửi...");
            try
            {
                ChatMessageModel? sentMessage = await _chatController.SendFileMessageModelAsync(userId, courseId, sourceFilePath, caption);
                if (!_uiAlive || IsDisposed || _messageList.IsDisposed || _selectedCourseId != courseId)
                {
                    return;
                }

                if (sentMessage == null)
                {
                    _messageList.MarkMessageFailed(temporaryMessageId, _chatController.LastErrorMessage);
                    return;
                }

                sentMessage.DeliveryStatus = "SENT";
                _messageList.ReplaceMessage(temporaryMessageId, sentMessage, userId);
            }
            catch (Exception ex)
            {
                if (_uiAlive && !IsDisposed && !_messageList.IsDisposed)
                {
                    _messageList.MarkMessageFailed(temporaryMessageId, ex.Message);
                }
            }
            finally
            {
                SetComposerSending(false);
                FocusInputIfAvailable();
            }
        }

        private async Task DownloadChatFileAsync(ChatFileDownloadEventArgs args)
        {
            ChatMessageModel message = args.Message;
            if (_userId <= 0 || _selectedCourseId <= 0 || message.Id <= 0)
            {
                MetaTheme.ShowModernDialog("Không tìm thấy tài liệu chat hợp lệ.", "Tải tài liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string fileName = string.IsNullOrWhiteSpace(message.FileName)
                ? "TaiLieuChat"
                : Path.GetFileName(message.FileName);

            try
            {
                byte[]? content = await _chatController.GetFileMessageContentAsync(_userId, _selectedCourseId, message.Id);
                if ((content == null || content.Length == 0) && !string.IsNullOrWhiteSpace(message.FileUrl))
                {
                    string localPath = ChatImageHelper.ResolveLocalFilePath(message.FileUrl);
                    if (File.Exists(localPath))
                    {
                        content = await File.ReadAllBytesAsync(localPath);
                    }
                }

                if (content == null || content.Length == 0)
                {
                    string error = string.IsNullOrWhiteSpace(_chatController.LastErrorMessage)
                        ? "Không tìm thấy nội dung tài liệu để tải xuống."
                        : _chatController.LastErrorMessage;
                    MetaTheme.ShowModernDialog(error, "Tải tài liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using var dialog = new SaveFileDialog
                {
                    Title = "Lưu tài liệu chat",
                    FileName = fileName,
                    Filter = "Tất cả tệp (*.*)|*.*",
                    OverwritePrompt = true
                };

                if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }

                await File.WriteAllBytesAsync(dialog.FileName, content);
                MetaTheme.ShowModernDialog("Tải tài liệu thành công.", "Tải tài liệu", MessageBoxButtons.OK, MessageBoxIcon.Information);

                try
                {
                    Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
                }
                catch
                {
                    // Opening after download is a convenience only; the saved file is still valid.
                }
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog($"Không thể tải tài liệu lúc này.\n{ex.Message}", "Tải tài liệu", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task SendComposerAsync()
        {
            if (_imagePreviewStrip.SelectedImages.Count > 0)
            {
                await SendImageGroupAsync(_imagePreviewStrip.SelectedImages);
            }
            else
            {
                await SendTextAsync();
            }
        }

        private static string ResolveComposerFileMimeType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".txt" => "text/plain",
                ".zip" => "application/zip",
                ".rar" => "application/vnd.rar",
                _ => "application/octet-stream"
            };
        }

        private async Task SendImageGroupAsync(IReadOnlyList<string> imagePaths)
        {
            if (_isSending)
            {
                return;
            }

            if (_userId <= 0 || _selectedCourseId <= 0)
            {
                MetaTheme.ShowModernDialog("Không tìm thấy phòng chat hợp lệ.", "Chat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<string> normalized;
            try
            {
                normalized = ChatImageHelper.NormalizeAndValidateImagePaths(imagePaths);
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog(ex.Message, "Ảnh không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string caption = string.IsNullOrWhiteSpace(_input.Text) ? string.Empty : _input.Text.Trim();
            _input.Clear();
            _imagePreviewStrip.ClearImages();

            SetComposerSending(true, "Đang gửi...");
            try
            {
                for (int i = 0; i < normalized.Count; i++)
                {
                    string path = normalized[i];
                    string currentCaption = (i == 0) ? caption : string.Empty;

                    // Compress the single image
                    using var compressed = await ChatImageHelper.CompressImageAsync(path);
                    
                    int temporaryMessageId = _nextTemporaryMessageId--;
                    var tempMsg = new ChatMessageModel
                    {
                        Id = temporaryMessageId,
                        CourseId = _selectedCourseId,
                        SenderId = _userId,
                        SenderName = "Bạn",
                        SenderRole = _allowPollCreation ? "Teacher" : "Student",
                        Content = currentCaption,
                        MessageType = "FILE",
                        FileUrl = path,
                        FileName = Path.GetFileName(path),
                        FileSize = new FileInfo(path).Length,
                        MimeType = ChatImageHelper.GetMimeType(path),
                        SentAt = DateTime.Now,
                        DeliveryStatus = "PENDING"
                    };

                    _messageList.AppendTemporaryMessage(tempMsg, _userId);

                    ChatMessageModel? sentMessage = await _chatController.SendImageMessageAsync(
                        _userId,
                        _selectedCourseId,
                        compressed.FilePath,
                        compressed.OriginalFileName,
                        currentCaption);

                    if (sentMessage == null)
                    {
                        _messageList.MarkMessageFailed(temporaryMessageId, _chatController.LastErrorMessage);
                    }
                    else
                    {
                        sentMessage.DeliveryStatus = "SENT";
                        _messageList.ReplaceMessage(temporaryMessageId, sentMessage, _userId);
                    }
                }
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog($"Không thể gửi ảnh lúc này.\n{ex.Message}", "Lỗi gửi ảnh", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetComposerSending(false);
                FocusInputIfAvailable();
            }
        }

        private async Task CreatePollAsync()
        {
            if (!_allowPollCreation || _isSending)
            {
                return;
            }

            if (_userId <= 0 || _selectedCourseId <= 0)
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn phòng chat hợp lệ trước khi tạo vote.", "Vote", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dialog = new CreatePollDialog();
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
            {
                return;
            }

            int userId = _userId;
            int courseId = _selectedCourseId;
            SetComposerSending(true);
            try
            {
                int messageId = await _chatController.CreatePollAsync(userId, courseId, dialog.PollQuestion, dialog.PollOptions);
                if (!_uiAlive || IsDisposed || _messageList.IsDisposed || _selectedCourseId != courseId)
                {
                    return;
                }

                if (messageId <= 0)
                {
                    MetaTheme.ShowModernDialog(_chatController.LastErrorMessage, "Tạo vote thất bại", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                await RefreshMessagesAsync();
            }
            catch (Exception ex)
            {
                if (_uiAlive && !IsDisposed)
                {
                    MetaTheme.ShowModernDialog($"Không thể tạo vote lúc này.\n{ex.Message}", "Lỗi mạng", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                SetComposerSending(false);
            }
        }

        private async Task VotePollAsync(PollVoteEventArgs args)
        {
            if (_userId <= 0 || _selectedCourseId <= 0)
            {
                _messageList.SetPollActionPending(args.MessageId, false);
                MetaTheme.ShowModernDialog("Không tìm thấy phòng chat hợp lệ.", "Vote", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int userId = _userId;
            int courseId = _selectedCourseId;
            try
            {
                bool success = await _chatController.VotePollAsync(userId, args.PollId, args.OptionId);
                if (!_uiAlive || IsDisposed || _messageList.IsDisposed || _selectedCourseId != courseId)
                {
                    return;
                }

                if (!success)
                {
                    MetaTheme.ShowModernDialog(_chatController.LastErrorMessage, "Vote thất bại", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                await RefreshSinglePollBubbleAsync(args.MessageId, userId, courseId);
            }
            catch (Exception ex)
            {
                if (_uiAlive && !IsDisposed)
                {
                    MetaTheme.ShowModernDialog($"Không thể gửi vote lúc này.\n{ex.Message}", "Lỗi mạng", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                if (_uiAlive && !IsDisposed && !_messageList.IsDisposed)
                {
                    _messageList.SetPollActionPending(args.MessageId, false);
                }
            }
        }

        private async Task ClosePollAsync(PollCloseEventArgs args)
        {
            if (!_allowPollCreation)
            {
                _messageList.SetPollActionPending(args.MessageId, false);
                MetaTheme.ShowModernDialog("Chỉ giảng viên mới được đóng vote.", "Vote", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_userId <= 0 || _selectedCourseId <= 0)
            {
                _messageList.SetPollActionPending(args.MessageId, false);
                MetaTheme.ShowModernDialog("Vui lòng chọn phòng chat hợp lệ.", "Vote", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult confirm = MetaTheme.ShowModernDialog(
                "Bạn chắc chắn muốn đóng vote này? Sau khi đóng, mọi người sẽ không thể vote thêm.",
                "Đóng vote",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
            {
                _messageList.SetPollActionPending(args.MessageId, false);
                return;
            }

            int userId = _userId;
            int courseId = _selectedCourseId;
            try
            {
                bool success = await _chatController.ClosePollAsync(userId, args.PollId);
                if (!_uiAlive || IsDisposed || _messageList.IsDisposed || _selectedCourseId != courseId)
                {
                    return;
                }

                if (!success)
                {
                    MetaTheme.ShowModernDialog(_chatController.LastErrorMessage, "Đóng vote thất bại", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                await RefreshSinglePollBubbleAsync(args.MessageId, userId, courseId);
            }
            catch (Exception ex)
            {
                if (_uiAlive && !IsDisposed)
                {
                    MetaTheme.ShowModernDialog($"Không thể đóng vote lúc này.\n{ex.Message}", "Lỗi mạng", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                if (_uiAlive && !IsDisposed && !_messageList.IsDisposed)
                {
                    _messageList.SetPollActionPending(args.MessageId, false);
                }
            }
        }

        private async Task RefreshSinglePollBubbleAsync(int messageId, int userId, int courseId)
        {
            List<ChatMessageModel> latestMessages = await _chatController.GetMessagesAsync(userId, courseId, 50);
            if (!_uiAlive || IsDisposed || _messageList.IsDisposed || _selectedCourseId != courseId)
            {
                return;
            }

            ChatMessageModel? updatedMessage = latestMessages.FirstOrDefault(message => message.Id == messageId);
            if (updatedMessage?.Poll != null)
            {
                _messageList.RefreshPollBubble(updatedMessage);
            }
        }

        private void SetComposerSending(bool sending, string? statusText = null)
        {
            _isSending = sending;
            if (!_uiAlive || IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => SetComposerSending(sending, statusText)));
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                }

                return;
            }

            if (!_uiAlive || IsDisposed)
            {
                return;
            }

            if (!_send.IsDisposed)
            {
                _send.Enabled = !sending;
            }

            if (!_attachMenu.IsDisposed)
            {
                _attachMenu.Enabled = !sending;
                _attachMenu.Text = sending ? (statusText ?? "...") : "+";
            }

            if (sending && !_attachStrip.IsDisposed)
            {
                _attachStrip.Close();
            }

            if (!_input.IsDisposed)
            {
                _input.Enabled = !sending;
            }
        }

        private void FocusInputIfAvailable()
        {
            if (!_uiAlive || IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(FocusInputIfAvailable));
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                }

                return;
            }

            if (_uiAlive && !IsDisposed && !_input.IsDisposed && _input.CanFocus)
            {
                _input.Focus();
            }
        }
    }
}
