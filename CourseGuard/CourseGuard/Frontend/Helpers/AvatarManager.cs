using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.Helpers
{
    public sealed class AvatarManager : IDisposable
    {
        private readonly IWin32Window _dialogOwner;
        private readonly Control _avatarControl;
        private readonly Func<string?> _displayNameProvider;
        private readonly Func<string?> _usernameProvider;
        private readonly Action<string> _avatarPathSetter;
        private readonly string _fallbackInitials;
        private Image? _avatarImage;
        private bool _disposed;

        public AvatarManager(
            Control owner,
            Control avatarControl,
            Func<string?> displayNameProvider,
            Func<string?> usernameProvider,
            Action<string> avatarPathSetter,
            string fallbackInitials = "U")
        {
            _dialogOwner = owner;
            _avatarControl = avatarControl;
            _displayNameProvider = displayNameProvider;
            _usernameProvider = usernameProvider;
            _avatarPathSetter = avatarPathSetter;
            _fallbackInitials = string.IsNullOrWhiteSpace(fallbackInitials) ? "U" : fallbackInitials.Trim();
        }

        public void Draw(Graphics graphics, Rectangle bounds)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Rectangle ellipse = new(bounds.Left, bounds.Top, Math.Max(1, bounds.Width - 1), Math.Max(1, bounds.Height - 1));
            using var fillBrush = new SolidBrush(MetaTheme.Colors.Accent);
            graphics.FillEllipse(fillBrush, ellipse);

            if (_avatarImage != null)
            {
                using var path = new GraphicsPath();
                path.AddEllipse(ellipse);
                graphics.SetClip(path);
                graphics.DrawImage(_avatarImage, GetCoverRectangle(_avatarImage.Size, ellipse));
                graphics.ResetClip();
            }
            else
            {
                using var textBrush = new SolidBrush(Color.White);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                graphics.DrawString(GetInitials(_displayNameProvider(), _usernameProvider(), _fallbackInitials), AppFonts.Semibold(22f), textBrush, ellipse, sf);
            }

            GraphicsHelpers.DrawRoundedBorder(graphics, ellipse, ellipse.Width / 2, MetaTheme.Colors.BorderSoft, 1f);
        }

        public bool LoadFromPath(string avatarPath)
        {
            if (string.IsNullOrWhiteSpace(avatarPath) || !File.Exists(avatarPath))
            {
                ReplaceAvatarImage(null);
                return false;
            }

            try
            {
                SetAvatarImage(avatarPath);
                return true;
            }
            catch
            {
                _avatarPathSetter(string.Empty);
                return false;
            }
        }

        public bool TrySelectAvatar(out string selectedPath, out string? errorMessage)
        {
            selectedPath = string.Empty;
            errorMessage = null;

            using var dialog = new OpenFileDialog
            {
                Title = "Chọn ảnh đại diện",
                Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog(_dialogOwner) != DialogResult.OK)
                return false;

            try
            {
                SetAvatarImage(dialog.FileName);
                selectedPath = dialog.FileName;
                _avatarPathSetter(selectedPath);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            ReplaceAvatarImage(null, invalidateControl: false);
        }

        public static Rectangle GetCoverRectangle(Size sourceSize, Rectangle target)
        {
            if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
                return target;

            float scale = Math.Max((float)target.Width / sourceSize.Width, (float)target.Height / sourceSize.Height);
            int width = (int)Math.Ceiling(sourceSize.Width * scale);
            int height = (int)Math.Ceiling(sourceSize.Height * scale);
            return new Rectangle(target.Left + (target.Width - width) / 2, target.Top + (target.Height - height) / 2, width, height);
        }

        public static string GetInitials(string? displayName, string? username, string fallback = "U")
        {
            string source = !string.IsNullOrWhiteSpace(displayName)
                ? displayName
                : (!string.IsNullOrWhiteSpace(username) ? username : fallback);

            if (string.IsNullOrWhiteSpace(source))
                source = "U";

            string[] parts = source.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();

            return parts[0].Length >= 2
                ? parts[0][..2].ToUpperInvariant()
                : parts[0][..1].ToUpperInvariant();
        }

        private void SetAvatarImage(string filePath)
        {
            using Image selected = Image.FromFile(filePath);
            ReplaceAvatarImage(new Bitmap(selected));
        }

        private void ReplaceAvatarImage(Image? newAvatar, bool invalidateControl = true)
        {
            Image? oldAvatar = _avatarImage;
            _avatarImage = newAvatar;
            oldAvatar?.Dispose();
            if (invalidateControl && !_avatarControl.IsDisposed)
                _avatarControl.Invalidate();
        }
    }
}
