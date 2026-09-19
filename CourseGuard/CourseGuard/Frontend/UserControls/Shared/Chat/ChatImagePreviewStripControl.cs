using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Shared.Chat
{
    public class ChatImagePreviewStripControl : UserControl
    {
        private readonly FlowLayoutPanel _flow = new();
        private readonly List<string> _selectedImages = new();

        public event EventHandler? ImagesChanged;
        public event EventHandler? RequestInputFocus;

        public IReadOnlyList<string> SelectedImages => _selectedImages.ToArray();
        public bool HasImages => _selectedImages.Count > 0;

        public ChatImagePreviewStripControl()
        {
            DoubleBuffered = true;
            Height = 104;
            Dock = DockStyle.Top;
            Visible = false;
            BackColor = AppColors.BgCard;
            Padding = new Padding(8, 6, 8, 4);

            _flow.Dock = DockStyle.Fill;
            _flow.AutoScroll = true;
            _flow.WrapContents = false;
            _flow.FlowDirection = FlowDirection.LeftToRight;
            _flow.BackColor = AppColors.BgCard;
            _flow.Margin = Padding.Empty;
            Controls.Add(_flow);
        }

        public void AddImages(IEnumerable<string> paths)
        {
            var normalized = ChatImageHelper.NormalizeAndValidateImagePaths(_selectedImages.Concat(paths ?? Array.Empty<string>()));
            _selectedImages.Clear();
            _selectedImages.AddRange(normalized);
            RebuildPreviewItems();
            ImagesChanged?.Invoke(this, EventArgs.Empty);
            RequestInputFocus?.Invoke(this, EventArgs.Empty);
        }

        public void ClearImages()
        {
            _selectedImages.Clear();
            DisposePreviewItems();
            Visible = false;
            ImagesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RemoveImage(string path)
        {
            _selectedImages.RemoveAll(item => string.Equals(item, path, StringComparison.OrdinalIgnoreCase));
            RebuildPreviewItems();
            ImagesChanged?.Invoke(this, EventArgs.Empty);
            RequestInputFocus?.Invoke(this, EventArgs.Empty);
        }

        private void RebuildPreviewItems()
        {
            DisposePreviewItems();
            Visible = _selectedImages.Count > 0;
            foreach (string path in _selectedImages)
            {
                _flow.Controls.Add(CreatePreviewItem(path));
            }
        }

        private Control CreatePreviewItem(string path)
        {
            var panel = new Panel
            {
                Width = 116,
                Height = 82,
                Margin = new Padding(0, 0, 8, 0),
                BackColor = AppColors.BgElevated,
                Padding = new Padding(4)
            };

            var picture = new PictureBox
            {
                Width = 72,
                Height = 54,
                Left = 4,
                Top = 4,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = AppColors.BgInput
            };
            picture.Image = LoadThumbnail(path, picture.Size);

            var removeButton = new Button
            {
                Text = "×",
                Width = 26,
                Height = 24,
                Left = 84,
                Top = 4,
                FlatStyle = FlatStyle.Flat,
                BackColor = AppColors.Danger,
                ForeColor = Color.White,
                Font = AppFonts.Semibold(9f),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            removeButton.FlatAppearance.BorderSize = 0;
            removeButton.Click += (_, _) => RemoveImage(path);

            var label = new Label
            {
                Left = 4,
                Top = 61,
                Width = 106,
                Height = 16,
                Text = $"{Path.GetFileName(path)} · {ChatImageHelper.FormatFileSize(new FileInfo(path).Length)}",
                AutoEllipsis = true,
                Font = AppFonts.Caption,
                ForeColor = AppColors.TextSecondary,
                BackColor = Color.Transparent
            };

            panel.Controls.Add(picture);
            panel.Controls.Add(removeButton);
            panel.Controls.Add(label);
            return panel;
        }

        private static Image? LoadThumbnail(string path, Size size)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                using var stream = new MemoryStream(bytes);
                using Image source = Image.FromStream(stream);
                var bitmap = new Bitmap(size.Width, size.Height);
                using Graphics graphics = Graphics.FromImage(bitmap);
                graphics.Clear(AppColors.BgInput);
                graphics.DrawImage(source, GetCoverRectangle(source.Size, new Rectangle(Point.Empty, size)));
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private static Rectangle GetCoverRectangle(Size sourceSize, Rectangle target)
        {
            float scale = Math.Max((float)target.Width / sourceSize.Width, (float)target.Height / sourceSize.Height);
            int width = (int)Math.Ceiling(sourceSize.Width * scale);
            int height = (int)Math.Ceiling(sourceSize.Height * scale);
            return new Rectangle(target.Left + (target.Width - width) / 2, target.Top + (target.Height - height) / 2, width, height);
        }

        private void DisposePreviewItems()
        {
            var controls = _flow.Controls.Cast<Control>().ToArray();
            _flow.Controls.Clear();
            foreach (Control control in controls)
            {
                DisposeImages(control);
                control.Dispose();
            }
        }

        private static void DisposeImages(Control control)
        {
            foreach (Control child in control.Controls)
            {
                DisposeImages(child);
            }

            if (control is PictureBox picture)
            {
                Image? image = picture.Image;
                picture.Image = null;
                image?.Dispose();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposePreviewItems();
            }

            base.Dispose(disposing);
        }
    }
}
