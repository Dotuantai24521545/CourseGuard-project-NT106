using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Shared.Chat
{
    public class ChatMessageListControl : UserControl
    {
        private const int MessageListLeftInset = 10;
        private const int MessageListRightGutter = 34;

        private readonly DoubleBufferedFlowLayoutPanel _panel = new();
        private readonly Label _emptyStateLabel = new();
        private readonly AvatarImageLoader _avatarImageLoader = new();
        private readonly ChatImageLoader _chatImageLoader = new();
        private bool _suppressTopReached;
        private static Color MessageListSurfaceColor => AppColors.IsDarkMode ? AppColors.BgCard : Color.White;

        public event EventHandler? TopReached;
        public event EventHandler<PollVoteEventArgs>? PollVoteRequested;
        public event EventHandler<PollCloseEventArgs>? PollCloseRequested;
        public event EventHandler<ChatFileDownloadEventArgs>? FileDownloadRequested;

        public IReadOnlyCollection<int> LoadedMessageIds => _panel.Controls
            .Cast<Control>()
            .Select(GetMessageId)
            .Where(messageId => messageId > 0)
            .ToArray();

        public int? OldestMessageId => _panel.Controls
            .Cast<Control>()
            .Select(control => new { MessageId = GetMessageId(control), SentAt = GetSentAt(control) })
            .Where(item => item.MessageId > 0)
            .OrderBy(item => item.SentAt)
            .ThenBy(item => item.MessageId)
            .Select(item => (int?)item.MessageId)
            .FirstOrDefault();

        public int? NewestMessageId => _panel.Controls
            .Cast<Control>()
            .Select(control => new { MessageId = GetMessageId(control), SentAt = GetSentAt(control) })
            .Where(item => item.MessageId > 0)
            .OrderByDescending(item => item.SentAt)
            .ThenByDescending(item => item.MessageId)
            .Select(item => (int?)item.MessageId)
            .FirstOrDefault();

        public ChatMessageListControl()
        {
            DoubleBuffered = true;
            Tag = "custom";
            BackColor = MessageListSurfaceColor;
            Dock = DockStyle.Fill;
            Padding = new Padding(0);

            _panel.Dock = DockStyle.Fill;
            _panel.BackColor = MessageListSurfaceColor;
            _panel.Tag = "custom";
            _panel.Padding = new Padding(MessageListLeftInset, 8, MessageListRightGutter, 8);
            _panel.Scroll += OnPanelScroll;
            _panel.Resize += (_, _) => ResizeBubbles();
            Controls.Add(_panel);

            _emptyStateLabel.AutoEllipsis = true;
            _emptyStateLabel.BackColor = MessageListSurfaceColor;
            _emptyStateLabel.Dock = DockStyle.Fill;
            _emptyStateLabel.Font = AppFonts.Body;
            _emptyStateLabel.ForeColor = AppColors.TextMuted;
            _emptyStateLabel.Text = "Chưa có tin nhắn nào. Hãy bắt đầu cuộc trò chuyện!";
            _emptyStateLabel.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(_emptyStateLabel);
            _emptyStateLabel.BringToFront();
            UpdateEmptyState();
        }

        public void ClearMessages()
        {
            _panel.SuspendLayout();
            try
            {
                DisposePanelChildren();
                UpdateEmptyState();
            }
            finally
            {
                _panel.ResumeLayout(true);
            }
        }

        public void SetMessages(IEnumerable<ChatMessageModel> messages, int currentUserId)
        {
            if (messages == null)
            {
                ClearMessages();
                return;
            }

            var ordered = messages
                .OrderBy(message => message.SentAt)
                .ThenBy(message => message.Id)
                .ToList();

            _suppressTopReached = true;
            _panel.SuspendLayout();
            try
            {
                DisposePanelChildren();
                AddBubbleRange(ordered, currentUserId, insertAtTop: false);
                UpdateEmptyState();
            }
            finally
            {
                _panel.ResumeLayout(true);
                ResizeBubbles();
                ScrollToBottom();
                _suppressTopReached = false;
            }
        }

        public void AppendMessages(IEnumerable<ChatMessageModel> messages, int currentUserId, bool scrollIfNearBottom = true)
        {
            if (messages == null)
            {
                return;
            }

            bool shouldScroll = !scrollIfNearBottom || IsNearBottom();
            var existing = LoadedMessageIds.ToHashSet();
            var ordered = messages
                .Where(message => !existing.Contains(message.Id))
                .OrderBy(message => message.SentAt)
                .ThenBy(message => message.Id)
                .ToList();

            if (ordered.Count == 0)
            {
                return;
            }

            _panel.SuspendLayout();
            try
            {
                AddBubbleRange(ordered, currentUserId, insertAtTop: false);
                UpdateEmptyState();
            }
            finally
            {
                _panel.ResumeLayout(true);
                ResizeBubbles();
                if (shouldScroll)
                {
                    ScrollToBottom();
                }
            }
        }

        public void AppendTemporaryMessage(ChatMessageModel message, int currentUserId)
        {
            if (message == null)
            {
                return;
            }

            _panel.SuspendLayout();
            try
            {
                _panel.Controls.Add(CreateBubble(message, currentUserId));
                UpdateEmptyState();
            }
            finally
            {
                _panel.ResumeLayout(true);
                ResizeBubbles();
                ScrollToBottom();
            }
        }

        public void ReplaceMessage(int temporaryMessageId, ChatMessageModel replacement, int currentUserId)
        {
            if (replacement == null)
            {
                return;
            }

            for (int i = 0; i < _panel.Controls.Count; i++)
            {
                if (GetMessageId(_panel.Controls[i]) != temporaryMessageId)
                {
                    continue;
                }

                Control oldBubble = _panel.Controls[i];
                Control newBubble = CreateBubble(replacement, currentUserId);
                _panel.SuspendLayout();
                try
                {
                    _panel.Controls.RemoveAt(i);
                    oldBubble.Dispose();
                    _panel.Controls.Add(newBubble);
                    _panel.Controls.SetChildIndex(newBubble, i);
                    UpdateEmptyState();
                }
                finally
                {
                    _panel.ResumeLayout(true);
                    ResizeBubbles();
                    ScrollToBottom();
                }

                return;
            }

            AppendTemporaryMessage(replacement, currentUserId);
        }

        public void MarkMessageFailed(int temporaryMessageId, string errorMessage)
        {
            foreach (ChatBubbleControl bubble in _panel.Controls.OfType<ChatBubbleControl>())
            {
                if (bubble.MessageId == temporaryMessageId)
                {
                    bubble.MarkFailed(errorMessage);
                    return;
                }
            }
        }

        public void PrependOlderMessages(IEnumerable<ChatMessageModel> messages, int currentUserId)
        {
            if (messages == null)
            {
                return;
            }

            var existing = LoadedMessageIds.ToHashSet();
            var ordered = messages
                .Where(message => message.Id <= 0 || !existing.Contains(message.Id))
                .OrderBy(message => message.SentAt)
                .ThenBy(message => message.Id)
                .ToList();

            if (ordered.Count == 0)
            {
                return;
            }

            int oldScrollValue = Math.Max(0, _panel.VerticalScroll.Value);
            int oldExtent = GetScrollExtent();

            _suppressTopReached = true;
            _panel.SuspendLayout();
            try
            {
                AddBubbleRange(ordered, currentUserId, insertAtTop: true);
                UpdateEmptyState();
            }
            finally
            {
                _panel.ResumeLayout(true);
                ResizeBubbles();
                RestoreScrollAfterPrepend(oldScrollValue, oldExtent);
                _suppressTopReached = false;
            }
        }

        public void ScrollToBottom()
        {
            if (_panel.Controls.Count == 0)
            {
                return;
            }

            _panel.ScrollControlIntoView(_panel.Controls[_panel.Controls.Count - 1]);
        }

        protected override void OnBackColorChanged(EventArgs e)
        {
            base.OnBackColorChanged(e);
            _panel.BackColor = BackColor;
            _emptyStateLabel.BackColor = BackColor;
        }

        private void AddBubbleRange(IReadOnlyList<ChatMessageModel> messages, int currentUserId, bool insertAtTop)
        {
            for (int i = 0; i < messages.Count; i++)
            {
                Control bubble = CreateBubble(messages[i], currentUserId);
                if (insertAtTop)
                {
                    _panel.Controls.Add(bubble);
                    _panel.Controls.SetChildIndex(bubble, i);
                }
                else
                {
                    _panel.Controls.Add(bubble);
                }
            }
        }

        private Control CreateBubble(ChatMessageModel message, int currentUserId)
        {
            if (IsPollRenderable(message))
            {
                var pollBubble = new PollBubbleControl(message, currentUserId, canClosePoll: true);
                pollBubble.VoteRequested += (_, args) => PollVoteRequested?.Invoke(this, args);
                pollBubble.CloseRequested += (_, args) => PollCloseRequested?.Invoke(this, args);
                pollBubble.UpdateContainerWidth(AvailableBubbleWidth());
                return pollBubble;
            }

            var bubble = new ChatBubbleControl(message, currentUserId, _avatarImageLoader, _chatImageLoader);
            bubble.FileDownloadRequested += (_, args) => FileDownloadRequested?.Invoke(this, args);
            bubble.UpdateContainerWidth(AvailableBubbleWidth());
            return bubble;
        }

        private void ResizeBubbles()
        {
            int width = AvailableBubbleWidth();
            foreach (Control bubble in _panel.Controls)
            {
                UpdateBubbleWidth(bubble, width);
            }
        }

        private int AvailableBubbleWidth()
        {
            return Math.Max(260, _panel.ClientSize.Width - _panel.Padding.Horizontal);
        }

        private void UpdateEmptyState()
        {
            _emptyStateLabel.Visible = _panel.Controls.Count == 0;
            if (_emptyStateLabel.Visible)
                _emptyStateLabel.BringToFront();
        }

        public void SetPollActionPending(int messageId, bool pending)
        {
            foreach (PollBubbleControl bubble in _panel.Controls.OfType<PollBubbleControl>())
            {
                if (bubble.MessageId == messageId || bubble.PollId == messageId)
                {
                    bubble.SetActionPending(pending);
                    return;
                }
            }
        }

        public void RefreshPollBubble(ChatMessageModel message)
        {
            if (message == null)
            {
                return;
            }

            foreach (PollBubbleControl bubble in _panel.Controls.OfType<PollBubbleControl>())
            {
                if (bubble.MessageId == message.Id || (message.PollId.HasValue && bubble.PollId == message.PollId.Value))
                {
                    bubble.RefreshPoll(message);
                    bubble.UpdateContainerWidth(AvailableBubbleWidth());
                    return;
                }
            }
        }

        private static bool IsPollRenderable(ChatMessageModel message)
        {
            return message.Poll != null
                && (string.Equals(message.MessageType, "POLL", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(message.MessageType, "POLL_BUMP", StringComparison.OrdinalIgnoreCase));
        }

        private static void UpdateBubbleWidth(Control bubble, int width)
        {
            if (bubble is PollBubbleControl pollBubble)
            {
                pollBubble.UpdateContainerWidth(width);
                return;
            }

            if (bubble is ChatBubbleControl chatBubble)
            {
                chatBubble.UpdateContainerWidth(width);
            }
        }

        private static int GetMessageId(Control control)
        {
            return control switch
            {
                PollBubbleControl pollBubble => pollBubble.MessageId,
                ChatBubbleControl chatBubble => chatBubble.MessageId,
                _ => 0
            };
        }

        private static DateTime GetSentAt(Control control)
        {
            return control switch
            {
                PollBubbleControl pollBubble => pollBubble.SentAt,
                ChatBubbleControl chatBubble => chatBubble.SentAt,
                _ => DateTime.MinValue
            };
        }

        private bool IsNearBottom()
        {
            int value = _panel.VerticalScroll.Value;
            int visible = _panel.ClientSize.Height;
            int max = _panel.VerticalScroll.Maximum;
            return max - (value + visible) <= 80;
        }

        private int GetScrollExtent()
        {
            if (_panel.Controls.Count == 0)
            {
                return 0;
            }

            return _panel.Controls.Cast<Control>().Max(control => control.Bottom);
        }

        private void RestoreScrollAfterPrepend(int oldScrollValue, int oldExtent)
        {
            int newExtent = GetScrollExtent();
            int delta = Math.Max(0, newExtent - oldExtent);
            int target = oldScrollValue + delta;
            int max = Math.Max(0, _panel.VerticalScroll.Maximum - _panel.ClientSize.Height + 1);
            target = Math.Max(0, Math.Min(target, max));
            _panel.VerticalScroll.Value = target;
            _panel.PerformLayout();
        }

        private void DisposePanelChildren()
        {
            var children = _panel.Controls.Cast<Control>().ToArray();
            _panel.Controls.Clear();
            foreach (Control child in children)
            {
                child.Dispose();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposePanelChildren();
                _avatarImageLoader.Dispose();
                _chatImageLoader.Dispose();
            }

            base.Dispose(disposing);
        }

        private void OnPanelScroll(object? sender, ScrollEventArgs e)
        {
            if (_suppressTopReached || e.ScrollOrientation != ScrollOrientation.VerticalScroll)
            {
                return;
            }

            if (_panel.VerticalScroll.Value <= 10)
            {
                TopReached?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public sealed class ChatFileDownloadEventArgs : EventArgs
    {
        public ChatFileDownloadEventArgs(ChatMessageModel message)
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public ChatMessageModel Message { get; }
    }
}
