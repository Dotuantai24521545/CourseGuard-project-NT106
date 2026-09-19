using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Extensions;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Shared.Chat
{
    public class PollBubbleControl : UserControl
    {
        private const int AvatarSize = 36;
        private const int MaxBubbleWidth = 560;
        private const int MinBubbleWidth = 320;
        private const int HeaderHeight = 20;
        private const int QuestionLineHeight = 20;
        private const int OptionHeight = 40;
        private const int OptionGap = 8;
        private const int FooterHeight = 30;
        private const int BumpCaptionHeight = 18;

        private readonly ChatMessageModel _message;
        private readonly PollModel _poll;
        private readonly bool _isMine;
        private readonly bool _canClosePoll;
        private readonly ToolTip _toolTip = new();
        private readonly FlowLayoutPanel _stack = new();

        public event EventHandler<PollVoteEventArgs>? VoteRequested;
        public event EventHandler<PollCloseEventArgs>? CloseRequested;

        public int MessageId => _message.Id;
        public int PollId => _poll.Id;
        public DateTime SentAt => _message.SentAt;
        public bool IsActionPending { get; private set; }

        public PollBubbleControl(ChatMessageModel message, int currentUserId, bool canClosePoll = false)
        {
            _message = message ?? throw new ArgumentNullException(nameof(message));
            _poll = message.Poll ?? throw new ArgumentException("Poll message must include Poll data.", nameof(message));
            _isMine = message.SenderId == currentUserId;
            _canClosePoll = canClosePoll && !_poll.IsClosed && _poll.CreatedBy == currentUserId;

            DoubleBuffered = true;
            AutoSize = false;
            BackColor = AppColors.BgCard;
            Margin = new Padding(0, 5, 0, 5);
            MinimumSize = new Size(260, 120);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            BuildLayout();
        }

        public void UpdateContainerWidth(int containerWidth)
        {
            int safeWidth = Math.Max(280, containerWidth - 28);
            Width = safeWidth;
            Height = CalculatePreferredHeight(safeWidth);
            Invalidate(true);
        }

        public void SetActionPending(bool pending)
        {
            IsActionPending = pending;
            SetEnabledRecursive(_stack, !pending);
            Cursor = pending ? Cursors.WaitCursor : Cursors.Default;
            Invalidate(true);
        }

        public void RefreshPoll(ChatMessageModel message)
        {
            if (message?.Poll == null || (message.Id != MessageId && message.Poll.Id != PollId))
            {
                return;
            }

            _poll.TotalVotes = message.Poll.TotalVotes;
            _poll.MySelectedOptionId = message.Poll.MySelectedOptionId;
            _poll.IsClosed = message.Poll.IsClosed;
            _poll.ClosedAt = message.Poll.ClosedAt;
            _poll.Options.Clear();
            _poll.Options.AddRange(message.Poll.Options);
            BuildLayout();
            Invalidate(true);
        }

        private void BuildLayout()
        {
            SuspendLayout();
            Controls.Clear();

            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.BgCard,
                ColumnCount = 3,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, AvatarSize + 8));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, AvatarSize + 8));

            Control content = CreateContentStack();
            if (_isMine)
            {
                row.Controls.Add(CreateSpacer(), 0, 0);
                row.Controls.Add(CreateRightHost(content), 1, 0);
                row.Controls.Add(CreateSpacer(), 2, 0);
            }
            else
            {
                row.Controls.Add(CreateAvatar(), 0, 0);
                row.Controls.Add(CreateLeftHost(content), 1, 0);
                row.Controls.Add(CreateSpacer(), 2, 0);
            }

            Controls.Add(row);
            ResumeLayout(true);
        }

        private Control CreateContentStack()
        {
            int bubbleWidth = CalculateBubbleWidth(Width <= 0 ? 420 : Width);
            string senderName = _isMine ? "Bạn" : _message.SenderName.GetShortName();

            _stack.Controls.Clear();
            _stack.AutoSize = true;
            _stack.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _stack.FlowDirection = FlowDirection.TopDown;
            _stack.WrapContents = false;
            _stack.BackColor = AppColors.BgCard;
            _stack.Margin = Padding.Empty;
            _stack.Padding = Padding.Empty;

            var nameLabel = new Label
            {
                AutoSize = false,
                Width = bubbleWidth,
                Height = HeaderHeight,
                Text = senderName,
                TextAlign = _isMine ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft,
                Font = AppFonts.Caption,
                ForeColor = AppColors.TextSecondary,
                BackColor = AppColors.BgCard,
                Margin = new Padding(0, 0, 0, 2)
            };

            var card = new PollCardPanel(_poll, _isMine)
            {
                Width = bubbleWidth,
                Height = CalculateCardHeight(bubbleWidth),
                Margin = Padding.Empty,
                Padding = new Padding(16, 14, 16, 12)
            };

            foreach (PollOptionModel option in _poll.Options.OrderBy(option => option.SortOrder).ThenBy(option => option.Id))
            {
                var optionRow = new PollOptionButton(_poll, option)
                {
                    Width = bubbleWidth - card.Padding.Horizontal,
                    Height = OptionHeight,
                    Margin = new Padding(0, 0, 0, OptionGap),
                    Enabled = !_poll.IsClosed && !IsActionPending,
                    Cursor = _poll.IsClosed ? Cursors.Default : Cursors.Hand
                };
                optionRow.Click += (_, _) => OnOptionClicked(option.Id);
                card.OptionHost.Controls.Add(optionRow);
            }

            if (_canClosePoll)
            {
                var closeButton = new ClosePollButton
                {
                    Width = 96,
                    Height = 26,
                    Margin = new Padding(0, 2, 0, 0),
                    Enabled = !IsActionPending,
                    Cursor = Cursors.Hand
                };
                closeButton.Click += (_, _) => OnCloseClicked();
                card.FooterHost.Controls.Add(closeButton);
            }

            string tooltip = $"{_message.SentAt:HH:mm dd/MM/yyyy}";
            _toolTip.SetToolTip(this, tooltip);
            _toolTip.SetToolTip(nameLabel, tooltip);
            _toolTip.SetToolTip(card, tooltip);

            _stack.Controls.Add(nameLabel);

            if (IsBumpMessage())
            {
                var bumpCaption = CreateBumpCaptionLabel(bubbleWidth);
                _toolTip.SetToolTip(bumpCaption, tooltip);
                _stack.Controls.Add(bumpCaption);
            }

            _stack.Controls.Add(card);
            SetEnabledRecursive(_stack, !IsActionPending);
            return _stack;
        }

        private Label CreateBumpCaptionLabel(int bubbleWidth)
        {
            return new Label
            {
                AutoSize = false,
                Width = bubbleWidth,
                Height = BumpCaptionHeight,
                Text = string.IsNullOrWhiteSpace(_message.Content) ? "Bình chọn vừa được cập nhật" : _message.Content.Trim(),
                TextAlign = _isMine ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft,
                Font = new Font(AppFonts.Body.FontFamily, 9.5f, FontStyle.Italic),
                ForeColor = AppColors.TextSecondary,
                BackColor = AppColors.BgCard,
                Margin = new Padding(0, 0, 0, 4)
            };
        }

        private bool IsBumpMessage()
        {
            return string.Equals(_message.MessageType, "POLL_BUMP", StringComparison.OrdinalIgnoreCase);
        }

        private void OnOptionClicked(int optionId)
        {
            if (IsActionPending || _poll.IsClosed)
            {
                return;
            }

            SetActionPending(true);
            VoteRequested?.Invoke(this, new PollVoteEventArgs(MessageId, _poll.Id, optionId));
        }

        private void OnCloseClicked()
        {
            if (IsActionPending || _poll.IsClosed)
            {
                return;
            }

            SetActionPending(true);
            CloseRequested?.Invoke(this, new PollCloseEventArgs(MessageId, _poll.Id));
        }

        private Control CreateLeftHost(Control content)
        {
            return new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.BgCard,
                Padding = new Padding(0, 0, 58, 0),
                Controls = { content }
            };
        }

        private Control CreateRightHost(Control content)
        {
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.BgCard,
                Padding = new Padding(58, 0, 0, 0)
            };
            content.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            host.Controls.Add(content);
            host.Resize += (_, _) => content.Left = Math.Max(0, host.ClientSize.Width - content.Width);
            return host;
        }

        private static Control CreateSpacer()
        {
            return new Panel { Dock = DockStyle.Fill, BackColor = AppColors.BgCard };
        }

        private Control CreateAvatar()
        {
            return new InitialsAvatarControl(_message.SenderName)
            {
                Width = AvatarSize,
                Height = AvatarSize,
                Margin = new Padding(0, 20, 8, 0),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
        }

        private int CalculatePreferredHeight(int safeWidth)
        {
            int bubbleWidth = CalculateBubbleWidth(safeWidth);
            int captionHeight = IsBumpMessage() ? BumpCaptionHeight + 4 : 0;
            return HeaderHeight + captionHeight + CalculateCardHeight(bubbleWidth) + 12;
        }

        private int CalculateBubbleWidth(int availableWidth)
        {
            int reserved = _isMine ? 128 : 112;
            int width = Math.Max(MinBubbleWidth, availableWidth - reserved);
            return Math.Min(MaxBubbleWidth, width);
        }

        private int CalculateCardHeight(int bubbleWidth)
        {
            int questionWidth = Math.Max(180, bubbleWidth - 32);
            Size questionSize = TextRenderer.MeasureText(
                _poll.Question,
                AppFonts.Semibold(10f),
                new Size(questionWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            int questionHeight = Math.Max(QuestionLineHeight, questionSize.Height + 2);
            int optionCount = Math.Max(1, _poll.Options.Count);
            int optionHeight = optionCount * (OptionHeight + OptionGap);
            return 18 + questionHeight + 10 + optionHeight + FooterHeight + 8;
        }

        private static void SetEnabledRecursive(Control root, bool enabled)
        {
            foreach (Control child in root.Controls)
            {
                child.Enabled = enabled;
                SetEnabledRecursive(child, enabled);
            }
        }

        private sealed class PollCardPanel : Panel
        {
            private readonly PollModel _poll;
            private readonly bool _isMine;

            public FlowLayoutPanel OptionHost { get; } = new();
            public FlowLayoutPanel FooterHost { get; } = new();

            public PollCardPanel(PollModel poll, bool isMine)
            {
                _poll = poll;
                _isMine = isMine;
                DoubleBuffered = true;
                BackColor = AppColors.BgCard;
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

                OptionHost.FlowDirection = FlowDirection.TopDown;
                OptionHost.WrapContents = false;
                OptionHost.AutoScroll = false;
                OptionHost.BackColor = AppColors.BgCard;
                OptionHost.Margin = Padding.Empty;
                OptionHost.Padding = Padding.Empty;

                FooterHost.FlowDirection = FlowDirection.RightToLeft;
                FooterHost.WrapContents = false;
                FooterHost.BackColor = AppColors.BgCard;
                FooterHost.Margin = Padding.Empty;
                FooterHost.Padding = Padding.Empty;

                Controls.Add(OptionHost);
                Controls.Add(FooterHost);
            }

            protected override void OnLayout(LayoutEventArgs levent)
            {
                base.OnLayout(levent);
                int questionWidth = Width - Padding.Horizontal;
                Size questionSize = TextRenderer.MeasureText(
                    _poll.Question,
                    AppFonts.Semibold(10f),
                    new Size(questionWidth, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                int questionHeight = Math.Max(QuestionLineHeight, questionSize.Height + 2);
                int optionTop = Padding.Top + 18 + questionHeight + 10;
                OptionHost.SetBounds(Padding.Left, optionTop, questionWidth, Math.Max(OptionHeight, Height - optionTop - Padding.Bottom - FooterHeight));
                FooterHost.SetBounds(Padding.Left, Height - Padding.Bottom - FooterHeight + 4, questionWidth, FooterHeight);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new(0, 0, Width - 1, Height - 1);
                using GraphicsPath path = CreateRoundRect(rect, 20);
                using SolidBrush fill = new(_isMine ? AppColors.AccentPressed : AppColors.BgElevated);
                using Pen border = new(_isMine ? AppColors.AccentHover : AppColors.BorderStrong);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);

                Rectangle badgeRect = new(Padding.Left, Padding.Top, 86, 18);
                TextRenderer.DrawText(e.Graphics, "📊 Vote", AppFonts.Caption, badgeRect, _isMine ? Color.White : AppColors.AccentBlue, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                int questionTop = Padding.Top + 20;
                Rectangle questionRect = new(Padding.Left, questionTop, Width - Padding.Horizontal, OptionHost.Top - questionTop - 8);
                TextRenderer.DrawText(e.Graphics, _poll.Question, AppFonts.Semibold(10f), questionRect, _isMine ? Color.White : AppColors.TextPrimary, TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);

                string footer = _poll.IsClosed ? $"Đã đóng · {_poll.TotalVotes} phiếu" : $"Đang mở · {_poll.TotalVotes} phiếu";
                Rectangle footerRect = new(Padding.Left, Height - Padding.Bottom - 18, Width - Padding.Horizontal - 104, 18);
                TextRenderer.DrawText(e.Graphics, footer, AppFonts.Caption, footerRect, _isMine ? Color.WhiteSmoke : AppColors.TextSecondary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private sealed class PollOptionButton : Control
        {
            private readonly PollModel _poll;
            private readonly PollOptionModel _option;
            private bool _hover;

            public PollOptionButton(PollModel poll, PollOptionModel option)
            {
                _poll = poll;
                _option = option;
                DoubleBuffered = true;
                BackColor = AppColors.BgCard;
                Font = AppFonts.Body;
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                _hover = true;
                Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                _hover = false;
                Invalidate();
            }

            protected override void OnEnabledChanged(EventArgs e)
            {
                base.OnEnabledChanged(e);
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle rect = new(0, 0, Width - 1, Height - 1);
                bool selected = _poll.MySelectedOptionId == _option.Id;
                double ratio = _poll.TotalVotes <= 0 ? 0 : Math.Max(0, Math.Min(1, _option.VoteCount / (double)_poll.TotalVotes));

                using GraphicsPath path = CreateRoundRect(rect, 12);
                using SolidBrush bg = new(_hover && Enabled ? AppColors.BgCardHover : AppColors.BgCard);
                using Pen border = new(selected ? AppColors.AccentBlue : AppColors.BorderStrong);
                e.Graphics.FillPath(bg, path);

                if (_poll.TotalVotes > 0)
                {
                    Rectangle progressRect = new(0, 0, Math.Max(4, (int)(Width * ratio)), Height - 1);
                    using GraphicsPath progressPath = CreateRoundRect(progressRect, 12);
                    using SolidBrush progressBrush = new(selected ? AppColors.AccentSoft : AppColors.ChartFill);
                    e.Graphics.FillPath(progressBrush, progressPath);
                }

                e.Graphics.DrawPath(border, path);

                Rectangle radioOuter = new(12, 12, 16, 16);
                using Pen radioPen = new(selected ? AppColors.AccentBlue : AppColors.TextSecondary, 2f);
                e.Graphics.DrawEllipse(radioPen, radioOuter);
                if (selected)
                {
                    Rectangle radioInner = new(17, 17, 6, 6);
                    using SolidBrush selectedBrush = new(AppColors.AccentBlue);
                    e.Graphics.FillEllipse(selectedBrush, radioInner);
                }

                Color textColor = Enabled ? AppColors.TextPrimary : AppColors.TextSecondary;
                Rectangle textRect = new(38, 8, Width - 92, Height - 16);
                TextRenderer.DrawText(e.Graphics, _option.OptionText, AppFonts.Body, textRect, textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                string countText = _poll.TotalVotes <= 0 ? "0%" : $"{Math.Round(ratio * 100)}%";
                Rectangle percentRect = new(Width - 52, 8, 42, Height - 16);
                TextRenderer.DrawText(e.Graphics, countText, AppFonts.Caption, percentRect, AppColors.TextSecondary, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
        }

        private sealed class ClosePollButton : Control
        {
            private bool _hover;

            public ClosePollButton()
            {
                DoubleBuffered = true;
                BackColor = AppColors.BgCard;
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                _hover = true;
                Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                _hover = false;
                Invalidate();
            }

            protected override void OnEnabledChanged(EventArgs e)
            {
                base.OnEnabledChanged(e);
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new(0, 0, Width - 1, Height - 1);
                using GraphicsPath path = CreateRoundRect(rect, 13);
                using SolidBrush fill = new(_hover && Enabled ? AppColors.DangerSoft : AppColors.BgCardHover);
                using Pen border = new(AppColors.BorderStrong);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
                TextRenderer.DrawText(e.Graphics, "Đóng vote", AppFonts.Caption, rect, Enabled ? AppColors.Danger : AppColors.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private sealed class InitialsAvatarControl : Control
        {
            private readonly string _initials;

            public InitialsAvatarControl(string name)
            {
                _initials = name.GetInitials();
                DoubleBuffered = true;
                BackColor = AppColors.BgCard;
                Font = AppFonts.Semibold(9f);
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new(0, 0, Width - 1, Height - 1);
                using SolidBrush fill = new(AppColors.AccentSoft);
                using Pen border = new(AppColors.AccentBlue);
                e.Graphics.FillEllipse(fill, rect);
                e.Graphics.DrawEllipse(border, rect);
                TextRenderer.DrawText(e.Graphics, _initials, Font, rect, AppColors.AccentBlue, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private static GraphicsPath CreateRoundRect(Rectangle bounds, int radius)
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
    }
}
