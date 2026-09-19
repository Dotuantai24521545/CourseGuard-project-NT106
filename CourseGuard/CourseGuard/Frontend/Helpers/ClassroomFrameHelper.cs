using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace CourseGuard.Frontend.Helpers
{
    public static class ClassroomFrameHelper
    {
        public static Bitmap ResizeFrame(Bitmap source, int maxWidth, int maxHeight)
        {
            ArgumentNullException.ThrowIfNull(source);
            if (maxWidth <= 0) throw new ArgumentOutOfRangeException(nameof(maxWidth));
            if (maxHeight <= 0) throw new ArgumentOutOfRangeException(nameof(maxHeight));

            double ratio = Math.Min((double)maxWidth / source.Width, (double)maxHeight / source.Height);
            int width = Math.Max(1, (int)(source.Width * ratio));
            int height = Math.Max(1, (int)(source.Height * ratio));
            var resized = new Bitmap(width, height);

            using Graphics graphics = Graphics.FromImage(resized);
            graphics.CompositingQuality = CompositingQuality.HighSpeed;
            graphics.InterpolationMode = InterpolationMode.Low;
            graphics.SmoothingMode = SmoothingMode.HighSpeed;
            graphics.DrawImage(source, 0, 0, width, height);
            return resized;
        }

        public static string EncodeJpegFrame(Bitmap bitmap, long quality)
        {
            ArgumentNullException.ThrowIfNull(bitmap);

            using var stream = new MemoryStream();
            ImageCodecInfo encoder = ImageCodecInfo.GetImageEncoders()
                .First(codec => codec.MimeType == "image/jpeg");
            using var encoderParameters = new EncoderParameters(1);
            encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
            bitmap.Save(stream, encoder, encoderParameters);
            return Convert.ToBase64String(stream.ToArray());
        }

        public static Bitmap DecodeFrame(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
            {
                throw new ArgumentException("Frame payload is empty.", nameof(base64));
            }

            byte[] bytes = Convert.FromBase64String(base64);
            using var stream = new MemoryStream(bytes);
            using Image image = Image.FromStream(stream);
            return new Bitmap(image);
        }

        public static bool TryDecodeFrame(string? base64, out Bitmap? frame)
        {
            frame = null;
            if (string.IsNullOrWhiteSpace(base64))
            {
                return false;
            }

            try
            {
                frame = DecodeFrame(base64);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryBeginReplaceImage(
            Control owner,
            PictureBox target,
            Image frame,
            Action? afterReplace = null)
        {
            return TryBeginReplaceImage(owner, frame, () => target, afterReplace);
        }

        public static bool TryBeginReplaceImage(
            Control owner,
            Image frame,
            Func<PictureBox> targetProvider,
            Action? afterReplace = null)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(frame);
            ArgumentNullException.ThrowIfNull(targetProvider);

            if (owner.IsDisposed || !owner.IsHandleCreated)
            {
                frame.Dispose();
                return false;
            }

            try
            {
                owner.BeginInvoke(() =>
                {
                    if (owner.IsDisposed)
                    {
                        frame.Dispose();
                        return;
                    }

                    PictureBox target;
                    try
                    {
                        target = targetProvider();
                    }
                    catch
                    {
                        frame.Dispose();
                        return;
                    }

                    TryReplaceImage(target, frame, afterReplace);
                });
                return true;
            }
            catch
            {
                frame.Dispose();
                return false;
            }
        }

        public static bool TryReplaceImage(PictureBox target, Image frame, Action? afterReplace = null)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(frame);

            if (target.IsDisposed)
            {
                frame.Dispose();
                return false;
            }

            bool assigned = false;
            try
            {
                Image? old = target.Image;
                target.Image = frame;
                assigned = true;
                old?.Dispose();
                afterReplace?.Invoke();
                return true;
            }
            catch
            {
                if (!assigned)
                {
                    frame.Dispose();
                }

                return false;
            }
        }
    }
}
