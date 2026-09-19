using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Extensions;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Shared.Chat
{
    public class ChatBubbleControl : UserControl
    {
        private const int AvatarSize = 36;
        private const int AvatarColumnWidth = AvatarSize + 8;
        private const int MessageRowChromeWidth = AvatarColumnWidth * 2;
        private const int MaxBubbleWidth = 520;
        private const int MinBubbleWidth = 96;

        private readonly ChatMessageModel _message;
        private readonly bool _isMine;
        private readonly AvatarImageLoader _avatarImageLoader;
        private readonly ChatImageLoader _chatImageLoader;
        private readonly CancellationTokenSource _avatarLoadCts = new();
        private readonly CancellationTokenSource _imageLoadCts = new();
        private readonly ToolTip _timeToolTip = new();
        private InitialsAvatarControl? _avatarControl;
        private Image? _thumbnailImage;
        private readonly List<Image> _attachmentThumbnails = new();
        private int _contentWidthLimit = MaxBubbleWidth;

        public int MessageId => _message.Id;
        public DateTime SentAt => _message.SentAt;

        public event EventHandler<ChatFileDownloadEventArgs>? FileDownloadRequested;

        public void MarkFailed(string errorMessage)
        {
            _message.DeliveryStatus = "FAILED";
            _message.DeliveryError = errorMessage ?? string.Empty;
            BuildLayout();
            Height = CalculatePreferredHeight(Width);
            Invalidate(true);
        }

        public ChatBubbleControl(ChatMessageModel message, int currentUserId, AvatarImageLoader avatarImageLoader, ChatImageLoader chatImageLoader)
        {
            _message = message ?? throw new ArgumentNullException(nameof(message));
            _avatarImageLoader = avatarImageLoader ?? throw new ArgumentNullException(nameof(avatarImageLoader));
            _chatImageLoader = chatImageLoader ?? throw new ArgumentNullException(nameof(chatImageLoader));
            _isMine = message.SenderId == currentUserId;

            DoubleBuffered = true;
            AutoSize = false;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = AppColors.BgCard;
            Margin = new Padding(0, 4, 0, 4);
            MinimumSize = new Size(220, 52);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

            BuildLayout();
        }

        public void UpdateContainerWidth(int containerWidth)
        {
            int safeWidth = Math.Max(260, containerWidth - 28);
            int contentWidthLimit = CalculateContentWidthLimit(safeWidth);
            bool shouldRebuild = contentWidthLimit != _contentWidthLimit;
            _contentWidthLimit = contentWidthLimit;
            Width = safeWidth;
            if (shouldRebuild)
            {
                BuildLayout();
            }

            Height = CalculatePreferredHeight(safeWidth);
            Invalidate(true);
        }

        private void BuildLayout()
        {
            DisposeImageResources();
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
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, AvatarColumnWidth));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, AvatarColumnWidth));

            Control content = CreateContentStack();
            if (_isMine)
            {
                row.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = AppColors.BgCard }, 0, 0);
                row.Controls.Add(CreateRightHost(content), 1, 0);
                row.Controls.Add(CreateAvatar(), 2, 0);
            }
            else
            {
                row.Controls.Add(CreateAvatar(), 0, 0);
                row.Controls.Add(CreateLeftHost(content), 1, 0);
                row.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = AppColors.BgCard }, 2, 0);
            }

            Controls.Add(row);
        }

        private Control CreateContentStack()
        {
            string senderName = _isMine ? "Bạn" : _message.SenderName.GetShortName();
            bool isFile = IsFileMessage(_message);
            bool isImage = ChatImageHelper.IsImageMessage(_message.MessageType, _message.MimeType, _message.FileName, _message.FileUrl);
            string body = isImage ? BuildImageCaption(_message) : BuildBodyText(_message);
            int bubbleWidth = CalculateContentWidth(isImage, body);

            var stack = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = AppColors.BgCard,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            var nameLabel = new Label
            {
                AutoSize = false,
                Width = bubbleWidth,
                Height = 18,
                Text = senderName,
                TextAlign = _isMine ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft,
                Font = AppFonts.Caption,
                ForeColor = AppColors.TextSecondary,
                BackColor = AppColors.BgCard,
                Margin = new Padding(0, 0, 0, 2)
            };

            string tooltip = _message.SentAt.ToString("HH:mm dd/MM/yyyy");
            _timeToolTip.SetToolTip(this, tooltip);
            _timeToolTip.SetToolTip(stack, tooltip);
            _timeToolTip.SetToolTip(nameLabel, tooltip);

            stack.Controls.Add(nameLabel);

            if (isImage)
            {
                Control imageSurface = CreateImageSurface(bubbleWidth);
                _timeToolTip.SetToolTip(imageSurface, tooltip);
                stack.Controls.Add(imageSurface);

                if (!string.IsNullOrWhiteSpace(body))
                {
                    int captionWidth = Math.Min(bubbleWidth, CalculateContentWidth(isImage: false, body));
                    Control textBubble = CreateTextBubble(body, captionWidth, CalculateTextBubbleHeight(body, captionWidth));
                    textBubble.Margin = _isMine
                        ? new Padding(Math.Max(0, bubbleWidth - captionWidth), 6, 0, 0)
                        : new Padding(0, 6, 0, 0);
                    _timeToolTip.SetToolTip(textBubble, tooltip);
                    stack.Controls.Add(textBubble);
                }
            }
            else
            {
                Control bubble = CreateTextBubble(body, bubbleWidth, CalculateTextBubbleHeight(body, bubbleWidth));
                _timeToolTip.SetToolTip(bubble, tooltip);
                if (isFile)
                {
                    bubble.Cursor = Cursors.Hand;
                    bubble.Click += (_, _) => RequestFileDownload();
                    _timeToolTip.SetToolTip(bubble, $"{tooltip}\nBấm để tải tài liệu");
                }

                stack.Controls.Add(bubble);
            }

            Label timeLabel = CreateTimestampLabel(bubbleWidth);
            _timeToolTip.SetToolTip(timeLabel, tooltip);
            stack.Controls.Add(timeLabel);

            Label? statusLabel = CreateStatusLabel(bubbleWidth);
            if (statusLabel != null)
            {
                stack.Controls.Add(statusLabel);
            }

            return stack;
        }

        private int CalculateContentWidth(bool isImage, string body)
        {
            int widthLimit = Math.Max(MinBubbleWidth, _contentWidthLimit);
            if (isImage)
            {
                int attachmentCount = Math.Max(1, GetImageAttachments().Count);
                int preferredImageWidth = attachmentCount <= 1 ? 260 : 336;
                return Math.Min(widthLimit, preferredImageWidth);
            }

            Size textSize = MeasureWrappedText(body, AppFonts.Body, Math.Max(80, widthLimit - 28));
            return Math.Min(widthLimit, Math.Max(MinBubbleWidth, textSize.Width + 30));
        }

        private static int CalculateContentWidthLimit(int safeWidth)
        {
            int rowContentWidth = Math.Max(MinBubbleWidth, safeWidth - MessageRowChromeWidth);
            return Math.Min(MaxBubbleWidth, rowContentWidth);
        }

        private int CalculateTextBubbleHeight(string body, int bubbleWidth)
        {
            Size textSize = MeasureWrappedText(body, AppFonts.Body, Math.Max(80, bubbleWidth - 28));
            return Math.Max(38, textSize.Height + 22);
        }

        private static Size MeasureWrappedText(string body, Font font, int maxTextWidth)
        {
            using var bitmap = new Bitmap(1, 1);
            using Graphics graphics = Graphics.FromImage(bitmap);
            using StringFormat format = CreateBubbleTextFormat();
            SizeF measured = graphics.MeasureString(body, font, Math.Max(1, maxTextWidth), format);
            return new Size((int)Math.Ceiling(measured.Width), (int)Math.Ceiling(measured.Height));
        }

        private static StringFormat CreateBubbleTextFormat()
        {
            return new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Near,
                Trimming = StringTrimming.None
            };
        }

        private Control CreateTextBubble(string body, int width, int height)
        {
            return new RoundedBubblePanel(_isMine, body)
            {
                Width = width,
                Height = height,
                Margin = Padding.Empty,
                Padding = new Padding(14, 10, 14, 10),
                Font = AppFonts.Body
            };
        }

        private void RequestFileDownload()
        {
            if (IsFileMessage(_message))
            {
                FileDownloadRequested?.Invoke(this, new ChatFileDownloadEventArgs(_message));
            }
        }

        private Label CreateTimestampLabel(int bubbleWidth)
        {
            return new Label
            {
                AutoSize = false,
                Width = bubbleWidth,
                Height = 16,
                Text = _message.SentAt.ToString("HH:mm"),
                TextAlign = _isMine ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft,
                Font = AppFonts.Caption,
                ForeColor = AppColors.TextMuted,
                BackColor = AppColors.BgCard,
                Margin = new Padding(0, 3, 0, 0)
            };
        }

        private Label? CreateStatusLabel(int bubbleWidth)
        {
            if (string.Equals(_message.DeliveryStatus, "SENT", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            bool failed = string.Equals(_message.DeliveryStatus, "FAILED", StringComparison.OrdinalIgnoreCase);
            string text = failed
                ? $"⚠ Lỗi gửi{(string.IsNullOrWhiteSpace(_message.DeliveryError) ? string.Empty : $": {_message.DeliveryError}")}"
                : "⏳ Đang gửi...";

            return new Label
            {
                AutoSize = false,
                Width = bubbleWidth,
                Height = 18,
                Text = text,
                TextAlign = _isMine ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft,
                Font = AppFonts.Caption,
                ForeColor = failed ? AppColors.Danger : AppColors.TextSecondary,
                BackColor = AppColors.BgCard,
                Margin = new Padding(0, 3, 0, 0)
            };
        }

        private static string BuildBodyText(ChatMessageModel message)
        {
            if (IsFileMessage(message))
            {
                string sizeText = message.FileSize <= 0 ? "-" : $"{Math.Round(message.FileSize / 1024.0, 1)} KB";
                string fileName = string.IsNullOrWhiteSpace(message.FileName) ? "Tệp đính kèm" : message.FileName;
                fileName = AddLongTokenBreakOpportunities(fileName);
                string caption = string.IsNullOrWhiteSpace(message.Content) ? string.Empty : $"\n{message.Content}";
                return $"📎 {fileName} ({sizeText}){caption}";
            }

            string content = string.IsNullOrWhiteSpace(message.Content) ? "(Tin nhắn trống)" : message.Content;
            return AddLongTokenBreakOpportunities(content);
        }

        private static bool IsFileMessage(ChatMessageModel message)
        {
            return string.Equals(message.MessageType, "FILE", StringComparison.OrdinalIgnoreCase);
        }

        private static string AddLongTokenBreakOpportunities(string text)
        {
            var builder = new StringBuilder(text.Length + 8);
            int runLength = 0;
            foreach (char ch in text)
            {
                builder.Append(ch);
                runLength = char.IsLetterOrDigit(ch) ? runLength + 1 : 0;
                if (ch is '_' or '-' or '.' || runLength >= 18)
                {
                    builder.Append('\u200B');
                    runLength = 0;
                }
            }

            return builder.ToString();
        }

        private static string BuildImageCaption(ChatMessageModel message)
        {
            return string.IsNullOrWhiteSpace(message.Content) ? string.Empty : message.Content.Trim();
        }

        private Control CreateImageSurface(int width)
        {
            return ChatImageHelper.IsImageGroupMessage(_message.MessageType, _message.MimeType, _message.FileUrl)
                ? CreateImageGroupSurface(width)
                : CreateSingleImageSurface(width);
        }

        private Control CreateSingleImageSurface(int width)
        {
            int imageHeight = 174;
            var picture = CreateRoundedImageBox(width, imageHeight);
            picture.Margin = Padding.Empty;

            EventHandler openPreview = (_, _) => OpenImagePreview();
            picture.Click += openPreview;
            _ = LoadThumbnailIntoAsync(picture, _message.FileUrl, new Size(width, imageHeight), isGroupAttachment: false);
            return picture;
        }

        private Control CreateImageGroupSurface(int width)
        {
            var attachments = GetImageAttachments();
            int count = Math.Max(1, attachments.Count);
            int columns = count <= 1 ? 1 : Math.Min(3, count);
            int gap = count <= 1 ? 0 : 6;
            int thumbSize = count <= 1 ? 214 : Math.Max(72, (width - gap * columns) / columns);
            int rows = count <= 1 ? 1 : (int)Math.Ceiling(count / (double)columns);

            var grid = new FlowLayoutPanel
            {
                Width = width,
                Height = rows * thumbSize + Math.Max(0, rows - 1) * gap,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = false,
                BackColor = AppColors.BgCard,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };

            foreach (var attachment in attachments)
            {
                var picture = CreateRoundedImageBox(thumbSize, thumbSize);
                picture.Margin = new Padding(0, 0, gap, gap);

                string imagePath = attachment.Url;
                string imageName = string.IsNullOrWhiteSpace(attachment.Name) ? "Ảnh chat" : attachment.Name;
                picture.Click += (_, _) => OpenImagePreview(imagePath, imageName);
                grid.Controls.Add(picture);
                _ = LoadThumbnailIntoAsync(picture, imagePath, new Size(thumbSize, thumbSize), isGroupAttachment: true);
            }

            return grid;
        }

        private RoundedImageBox CreateRoundedImageBox(int width, int height)
        {
            var picture = new RoundedImageBox
            {
                Width = width,
                Height = height,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = _isMine ? Color.FromArgb(235, 242, 255) : AppColors.BgInput,
                Cursor = Cursors.Hand
            };
            picture.SetPlaceholderText("Đang tải ảnh...");

            return picture;
        }

        private async Task LoadThumbnailIntoAsync(PictureBox picture, string imagePath, Size targetSize, bool isGroupAttachment)
        {
            Image? thumbnail = null;
            try
            {
                thumbnail = await _chatImageLoader.LoadThumbnailAsync(_message.Id, _message.CourseId, imagePath, targetSize, _imageLoadCts.Token);
                if (thumbnail == null || IsDisposed || picture.IsDisposed)
                {
                    thumbnail?.Dispose();
                    return;
                }

                if (picture.InvokeRequired)
                {
                    try
                    {
                        picture.BeginInvoke(new Action(() => ApplyThumbnail(picture, thumbnail, isGroupAttachment)));
                    }
                    catch
                    {
                        thumbnail.Dispose();
                    }
                }
                else
                {
                    ApplyThumbnail(picture, thumbnail, isGroupAttachment);
                }
            }
            catch (OperationCanceledException)
            {
                thumbnail?.Dispose();
            }
            catch
            {
                thumbnail?.Dispose();
                if (!picture.IsDisposed)
                {
                    if (picture.InvokeRequired)
                    {
                        try { picture.BeginInvoke(new Action(picture.Invalidate)); } catch { }
                    }
                    else
                    {
                        picture.Invalidate();
                    }
                }
            }
        }

        private void ApplyThumbnail(PictureBox picture, Image thumbnail, bool isGroupAttachment)
        {
            if (_imageLoadCts.IsCancellationRequested || IsDisposed || picture.IsDisposed)
            {
                thumbnail.Dispose();
                return;
            }

            Image? oldImage = picture.Image;
            picture.SizeMode = PictureBoxSizeMode.Zoom;
            picture.Image = thumbnail;
            oldImage?.Dispose();

            if (isGroupAttachment)
            {
                _attachmentThumbnails.Add(thumbnail);
            }
            else
            {
                _thumbnailImage?.Dispose();
                _thumbnailImage = thumbnail;
            }
        }

        private void OpenImagePreview()
        {
            OpenImagePreview(_message.FileUrl, string.IsNullOrWhiteSpace(_message.FileName) ? "Ảnh chat" : _message.FileName);
        }

        private void OpenImagePreview(string imagePath, string title)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                MessageBox.Show("Đường dẫn ảnh không hợp lệ.", "Ảnh", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string resolvedPath = imagePath;
            bool isWebUrl = imagePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            imagePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            if (isWebUrl)
            {
                try
                {
                    string filename = Path.GetFileName(new Uri(imagePath).LocalPath);
                    string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "storage", "chat-files", "cache");
                    resolvedPath = Path.Combine(cacheDir, filename);

                    if (!File.Exists(resolvedPath))
                    {
                        Directory.CreateDirectory(cacheDir);
                        using var httpClient = new System.Net.Http.HttpClient();
                        byte[] webContent = httpClient.GetByteArrayAsync(imagePath).GetAwaiter().GetResult();
                        if (webContent != null && webContent.Length > 0)
                        {
                            File.WriteAllBytes(resolvedPath, webContent);
                        }
                    }
                }
                catch
                {
                    resolvedPath = Helpers.ChatImageHelper.ResolveLocalFilePath(imagePath);
                }
            }
            else
            {
                resolvedPath = Helpers.ChatImageHelper.ResolveLocalFilePath(imagePath);
            }

            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                MessageBox.Show("Đường dẫn ảnh không hợp lệ.", "Ảnh", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!File.Exists(resolvedPath) && _message.Id > 0 && _message.CourseId > 0)
            {
                try
                {
                    var dbContext = new Backend.Data.CourseGuardDbContext("");
                    byte[]? content = dbContext.GetChatFileContentAsync(_message.CourseId, _message.Id).GetAwaiter().GetResult();
                    if (content != null && content.Length > 0)
                    {
                        string? dir = Path.GetDirectoryName(resolvedPath);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                        File.WriteAllBytes(resolvedPath, content);
                    }
                }
                catch { }
            }

            if (!File.Exists(resolvedPath))
            {
                MessageBox.Show("Không tìm thấy ảnh để xem trước.", "Ảnh", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dialog = new ImagePreviewDialog(resolvedPath, string.IsNullOrWhiteSpace(title) ? "Ảnh chat" : title))
            {
                dialog.ShowDialog(this);
            }
        }

        private List<ChatImageAttachmentModel> GetImageAttachments()
        {
            if (_message.ImageAttachments != null && _message.ImageAttachments.Count > 0)
            {
                return _message.ImageAttachments;
            }

            if (ChatImageHelper.TryParseImageAttachments(_message.FileUrl, out var attachments))
            {
                return attachments;
            }

            if (!string.IsNullOrWhiteSpace(_message.FileUrl))
            {
                return new List<ChatImageAttachmentModel>
                {
                    new ChatImageAttachmentModel
                    {
                        Url = _message.FileUrl,
                        Name = _message.FileName,
                        Size = _message.FileSize,
                        Mime = _message.MimeType
                    }
                };
            }

            return new List<ChatImageAttachmentModel>();
        }

        private int CalculateImageSurfaceHeight()
        {
            int count = Math.Max(1, GetImageAttachments().Count);
            bool isImageGroup = ChatImageHelper.IsImageGroupMessage(_message.MessageType, _message.MimeType, _message.FileUrl);
            if (count <= 1)
            {
                return isImageGroup ? 214 : 174;
            }

            int width = CalculateContentWidth(isImage: true, body: string.Empty);
            int columns = Math.Min(3, count);
            int gap = 6;
            int thumbSize = Math.Max(72, (width - gap * columns) / columns);
            int rows = (int)Math.Ceiling(count / (double)columns);
            return rows * thumbSize + Math.Max(0, rows - 1) * gap;
        }

        private Control CreateLeftHost(Control content)
        {
            return new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.BgCard,
                Margin = Padding.Empty,
                Padding = new Padding(0, 0, 80, 0),
                Controls = { content }
            };
        }

        private Control CreateRightHost(Control content)
        {
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.BgCard,
                Margin = Padding.Empty,
                Padding = new Padding(80, 0, 0, 0)
            };
            content.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            host.Controls.Add(content);
            host.Resize += (_, _) => content.Left = Math.Max(0, host.ClientSize.Width - host.Padding.Right - content.Width);
            return host;
        }

        private Control CreateAvatar()
        {
            var avatar = new InitialsAvatarControl(_message.SenderName)
            {
                Width = AvatarSize,
                Height = AvatarSize,
                Margin = _isMine ? new Padding(8, 20, 0, 0) : new Padding(0, 20, 8, 0),
                Anchor = _isMine ? AnchorStyles.Top | AnchorStyles.Right : AnchorStyles.Top | AnchorStyles.Left
            };

            _avatarControl = avatar;

            if (!avatar.SetAvatarImage(LoadLocalAvatarImage(_message.SenderAvatar)))
            {
                _ = LoadRemoteAvatarAsync(_message.SenderAvatar);
            }

            return avatar;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _avatarLoadCts.Cancel();
                _imageLoadCts.Cancel();
                _avatarLoadCts.Dispose();
                _imageLoadCts.Dispose();
                DisposeImageResources();
                _timeToolTip.Dispose();
            }

            base.Dispose(disposing);
        }

        private int CalculatePreferredHeight(int safeWidth)
        {
            bool isImage = ChatImageHelper.IsImageMessage(_message.MessageType, _message.MimeType, _message.FileName, _message.FileUrl);
            int statusHeight = string.Equals(_message.DeliveryStatus, "SENT", StringComparison.OrdinalIgnoreCase) ? 0 : 22;
            const int timestampHeight = 19;

            if (isImage)
            {
                string caption = BuildImageCaption(_message);
                int contentWidth = CalculateContentWidth(isImage: true, caption);
                int captionHeight = string.IsNullOrWhiteSpace(caption) ? 0 : CalculateTextBubbleHeight(caption, contentWidth) + 6;
                return Math.Max(236, 18 + CalculateImageSurfaceHeight() + captionHeight + timestampHeight + statusHeight + 18);
            }

            int available = Math.Min(MaxBubbleWidth - 28, Math.Max(120, safeWidth - 160));
            available = Math.Min(available, Math.Max(80, _contentWidthLimit - 28));
            Size textSize = MeasureWrappedText(BuildBodyText(_message), AppFonts.Body, available);
            return Math.Max(76, textSize.Height + 48 + timestampHeight + statusHeight);
        }

        private void DisposeImageResources()
        {
            NullChildImages(this);
            _thumbnailImage?.Dispose();
            _thumbnailImage = null;
            foreach (Image image in _attachmentThumbnails)
            {
                image.Dispose();
            }

            _attachmentThumbnails.Clear();
        }

        private static void NullChildImages(Control control)
        {
            if (control is PictureBox pb)
            {
                pb.Image = null;
            }
            if (control.Controls.Count > 0)
            {
                foreach (Control child in control.Controls)
                {
                    NullChildImages(child);
                }
            }
        }

        private sealed class RoundedImageBox : PictureBox
        {
            private string _placeholderText = "Ảnh";

            public RoundedImageBox()
            {
                DoubleBuffered = true;
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            }

            public void SetPlaceholderText(string placeholderText)
            {
                _placeholderText = string.IsNullOrWhiteSpace(placeholderText) ? "Ảnh" : placeholderText;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new(0, 0, Width - 1, Height - 1);
                using GraphicsPath path = CreateRoundRect(rect, 18);
                using SolidBrush background = new(BackColor);
                e.Graphics.FillPath(background, path);
                e.Graphics.SetClip(path);

                bool drewImage = false;
                if (Image != null)
                {
                    try
                    {
                        Rectangle imageRect = GetZoomRectangle(Image.Size, ClientRectangle);
                        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        e.Graphics.DrawImage(Image, imageRect);
                        drewImage = true;
                    }
                    catch
                    {
                        // Image might be disposed or corrupt
                    }
                }

                if (!drewImage)
                {
                    TextRenderer.DrawText(e.Graphics, _placeholderText, AppFonts.Caption, ClientRectangle, AppColors.TextSecondary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }

                e.Graphics.ResetClip();
                using Pen border = new(Color.FromArgb(40, AppColors.BorderStrong));
                e.Graphics.DrawPath(border, path);
            }
        }

        private sealed class RoundedBubblePanel : Panel
        {
            private readonly bool _isMine;
            private readonly string _body;

            public RoundedBubblePanel(bool isMine, string body)
            {
                _isMine = isMine;
                _body = body;
                DoubleBuffered = true;
                SetStyle(ControlStyles.SupportsTransparentBackColor, true);
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
                BackColor = AppColors.BgCard;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new(0, 0, Width - 1, Height - 1);
                using GraphicsPath path = CreateRoundRect(rect, 18);
                using SolidBrush brush = new(_isMine ? AppColors.AccentPressed : AppColors.BgElevated);
                using Pen border = new(_isMine ? AppColors.AccentHover : AppColors.BorderStrong);
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(border, path);

                RectangleF textRect = new(Padding.Left, Padding.Top, Math.Max(1, Width - Padding.Horizontal), Math.Max(1, Height - Padding.Vertical));
                Color textColor = _isMine ? Color.White : AppColors.TextPrimary;
                using SolidBrush textBrush = new(textColor);
                using StringFormat format = CreateBubbleTextFormat();
                e.Graphics.DrawString(_body, Font, textBrush, textRect, format);
            }
        }

        private sealed class InitialsAvatarControl : Control
        {
            private readonly string _initials;
            private Image? _avatarImage;

            public InitialsAvatarControl(string name)
            {
                _initials = name.GetInitials();
                DoubleBuffered = true;
                SetStyle(ControlStyles.SupportsTransparentBackColor, true);
                BackColor = AppColors.BgCard;
                Font = AppFonts.Semibold(9f);
            }

            public bool SetAvatarImage(Image? image)
            {
                Image? oldImage = _avatarImage;
                _avatarImage = image;
                oldImage?.Dispose();
                if (!IsDisposed)
                {
                    Invalidate();
                }

                return image != null;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new(0, 0, Width - 1, Height - 1);
                using SolidBrush fill = new(AppColors.AccentSoft);
                using Pen border = new(AppColors.AccentBlue);
                e.Graphics.FillEllipse(fill, rect);

                if (_avatarImage != null)
                {
                    using GraphicsPath clipPath = new();
                    clipPath.AddEllipse(rect);
                    e.Graphics.SetClip(clipPath);
                    e.Graphics.DrawImage(_avatarImage, GetCoverRectangle(_avatarImage.Size, rect));
                    e.Graphics.ResetClip();
                }
                else
                {
                    TextRenderer.DrawText(e.Graphics, _initials, Font, rect, AppColors.AccentBlue, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }

                e.Graphics.DrawEllipse(border, rect);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    Image? oldImage = _avatarImage;
                    _avatarImage = null;
                    oldImage?.Dispose();
                }

                base.Dispose(disposing);
            }
        }

        private static Rectangle GetCoverRectangle(Size sourceSize, Rectangle target)
        {
            if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
            {
                return target;
            }

            float scale = Math.Max((float)target.Width / sourceSize.Width, (float)target.Height / sourceSize.Height);
            int width = (int)Math.Ceiling(sourceSize.Width * scale);
            int height = (int)Math.Ceiling(sourceSize.Height * scale);
            return new Rectangle(target.Left + (target.Width - width) / 2, target.Top + (target.Height - height) / 2, width, height);
        }

        private static Rectangle GetZoomRectangle(Size sourceSize, Rectangle target)
        {
            if (sourceSize.Width <= 0 || sourceSize.Height <= 0 || target.Width <= 0 || target.Height <= 0)
            {
                return target;
            }

            float scale = Math.Min((float)target.Width / sourceSize.Width, (float)target.Height / sourceSize.Height);
            int width = Math.Max(1, (int)Math.Round(sourceSize.Width * scale));
            int height = Math.Max(1, (int)Math.Round(sourceSize.Height * scale));
            return new Rectangle(target.Left + (target.Width - width) / 2, target.Top + (target.Height - height) / 2, width, height);
        }

        private static Image? LoadLocalAvatarImage(string avatarPath)
        {
            if (string.IsNullOrWhiteSpace(avatarPath) || IsHttpUrl(avatarPath) || !File.Exists(avatarPath))
            {
                return null;
            }

            try
            {
                byte[] fileBytes = File.ReadAllBytes(avatarPath);
                using var fileStream = new MemoryStream(fileBytes);
                using Image localSource = Image.FromStream(fileStream);
                return new Bitmap(localSource);
            }
            catch
            {
                return null;
            }
        }

        private async Task LoadRemoteAvatarAsync(string avatarUrl)
        {
            if (string.IsNullOrWhiteSpace(avatarUrl) || !IsHttpUrl(avatarUrl))
            {
                return;
            }

            Image? image = await _avatarImageLoader.LoadAsync(avatarUrl, _avatarLoadCts.Token).ConfigureAwait(true);
            if (image == null || _avatarLoadCts.IsCancellationRequested || IsDisposed || _avatarControl == null || _avatarControl.IsDisposed)
            {
                image?.Dispose();
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => ApplyRemoteAvatar(image)));
                }
                catch
                {
                    image.Dispose();
                }

                return;
            }

            ApplyRemoteAvatar(image);
        }

        private void ApplyRemoteAvatar(Image image)
        {
            if (_avatarLoadCts.IsCancellationRequested || IsDisposed || _avatarControl == null || _avatarControl.IsDisposed)
            {
                image.Dispose();
                return;
            }

            _avatarControl.SetAvatarImage(image);
        }

        private static bool IsHttpUrl(string value)
        {
            return Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
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
