using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Shared.Chat
{
    public sealed class ImagePreviewDialog : Form
    {
        private readonly string _imagePath;
        private Image? _previewImage;

        public ImagePreviewDialog(string imagePath, string title)
        {
            _imagePath = imagePath;
            Text = string.IsNullOrWhiteSpace(title) ? "Xem ảnh" : title;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(720, 520);
            Size = new Size(900, 650);
            BackColor = AppColors.BgCard;
            Font = AppFonts.Body;
            BuildLayout();
        }

        private void BuildLayout()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                Padding = new Padding(16, 10, 16, 10),
                BackColor = AppColors.BgCard
            };

            var titleLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = Text,
                Font = AppFonts.Semibold(12f),
                ForeColor = AppColors.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            var saveButton = new Button
            {
                Dock = DockStyle.Right,
                Width = 120,
                Text = "Tải ảnh",
                BackColor = AppColors.AccentBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            saveButton.FlatAppearance.BorderSize = 0;
            saveButton.Click += (_, _) => SaveImageCopy();

            var closeButton = new Button
            {
                Dock = DockStyle.Right,
                Width = 92,
                Text = "Đóng",
                BackColor = AppColors.BgCardHover,
                ForeColor = AppColors.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 0, 0, 0)
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.Click += (_, _) => Close();

            header.Controls.Add(titleLabel);
            header.Controls.Add(saveButton);
            header.Controls.Add(closeButton);

            var picture = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 24, 38),
                SizeMode = PictureBoxSizeMode.Zoom
            };

            if (File.Exists(_imagePath))
            {
                using (Image source = Image.FromFile(_imagePath))
                {
                    _previewImage = new Bitmap(source);
                }

                picture.Image = _previewImage;
            }

            Controls.Add(picture);
            Controls.Add(header);
        }

        private void SaveImageCopy()
        {
            if (!File.Exists(_imagePath))
            {
                MessageBox.Show("Không tìm thấy ảnh để tải.", "Tải ảnh", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Lưu ảnh chat";
                dialog.FileName = Path.GetFileName(_imagePath);
                dialog.Filter = "Ảnh JPG (*.jpg)|*.jpg|Ảnh PNG (*.png)|*.png|Tất cả tệp (*.*)|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                File.Copy(_imagePath, dialog.FileName, overwrite: true);
                MessageBox.Show("Đã lưu ảnh thành công.", "Tải ảnh", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _previewImage?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
