using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.Forms.Teacher
{
    public class ThemedDialogBase : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int CornerRadius = 18;
        private readonly TableLayoutPanel _root;
        private readonly Panel _contentPanel;
        private readonly TableLayoutPanel _footerPanel;
        private readonly Label _lblTitle;

        public Panel ContentPanel => _contentPanel;
        public TableLayoutPanel FooterPanel => _footerPanel;

        [AllowNull]
        public override string Text
        {
            get => base.Text;
            set
            {
                string text = value ?? string.Empty;
                base.Text = text;
                if (_lblTitle != null)
                    _lblTitle.Text = text;
            }
        }

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        public ThemedDialogBase()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            Width = 700;
            Height = 500;
            BackColor = AppColors.BorderStrong;
            Tag = "dialog";
            ShowInTaskbar = false;
            ForeColor = AppColors.TextPrimary;
            Font = AppFonts.Body;
            DoubleBuffered = true;
            Padding = new Padding(1);

            _root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.BgCard,
                ColumnCount = 1,
                RowCount = 4
            };
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 4f));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66f));

            _root.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = AppColors.AccentBlue }, 0, 0);

            // Header
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.BgCard,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(20, 10, 14, 8)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34f));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            header.MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, 0x2, 0);
                }
            };

            _lblTitle = new Label
            {
                Text = base.Text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = AppFonts.Semibold(13f),
                ForeColor = AppColors.TextPrimary,
                BackColor = AppColors.BgCard,
                AutoEllipsis = true,
                UseCompatibleTextRendering = false
            };
            _lblTitle.MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, 0x2, 0);
                }
            };

            var closeButton = new Panel
            {
                Anchor = AnchorStyles.None,
                Size = new Size(30, 30),
                BackColor = AppColors.IsDarkMode ? Color.FromArgb(78, 91, 111) : Color.FromArgb(229, 231, 235),
                Cursor = Cursors.Hand,
                TabStop = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            closeButton.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(AppColors.TextPrimary, 2.2f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                e.Graphics.DrawLine(pen, 10, 10, 20, 20);
                e.Graphics.DrawLine(pen, 20, 10, 10, 20);
            };
            closeButton.MouseEnter += (_, _) => closeButton.BackColor = AppColors.IsDarkMode ? Color.FromArgb(95, 108, 132) : Color.FromArgb(209, 213, 219);
            closeButton.MouseLeave += (_, _) => closeButton.BackColor = AppColors.IsDarkMode ? Color.FromArgb(78, 91, 111) : Color.FromArgb(229, 231, 235);
            closeButton.MouseDown += (_, _) => closeButton.BackColor = AppColors.BgElevated;
            closeButton.Resize += (_, _) =>
            {
                closeButton.Region?.Dispose();
                using var path = GraphicsHelpers.RoundedRect(new Rectangle(0, 0, closeButton.Width, closeButton.Height), 15);
                closeButton.Region = new Region(path);
            };
            closeButton.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            header.Controls.Add(_lblTitle, 0, 0);
            header.Controls.Add(closeButton, 1, 0);

            _root.Controls.Add(header, 0, 1);

            // Body
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(24, 8, 24, 10)
            };
            _root.Controls.Add(_contentPanel, 0, 2);

            // Footer
            _footerPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 1,
                Padding = new Padding(16, 12, 16, 12)
            };
            _footerPanel.Paint += (_, e) =>
            {
                using var pen = new Pen(AppColors.Border, 1f);
                e.Graphics.DrawLine(pen, 1, 0, _footerPanel.Width - 1, 0);
            };
            _root.Controls.Add(_footerPanel, 0, 3);

            Controls.Add(_root);

        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;

            RectangleF borderRect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            float clampedRadius = Math.Min(CornerRadius, Math.Min(borderRect.Width, borderRect.Height) / 2f);

            using var path = GraphicsHelpers.RoundedRectF(borderRect, clampedRadius);
            using var bgBrush = new SolidBrush(AppColors.BgCard);
            e.Graphics.FillPath(bgBrush, path);
            using var pen = new Pen(AppColors.BorderStrong, 1f);
            e.Graphics.DrawPath(pen, path);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyRoundedRegion();
            AppColors.ApplyTheme(this);

            // Restore BackColor which AppColors.ApplyTheme overwrites
            BackColor = AppColors.BorderStrong;

            // Re-apply to custom drawn elements
            _root.BackColor = Color.Transparent;
            _contentPanel.BackColor = Color.Transparent;
            _footerPanel.BackColor = Color.Transparent;
            DialogInputStyler.ApplyAdminStyle(ContentPanel);
            DialogInputStyler.ApplyAdminStyle(FooterPanel);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRoundedRegion();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (Environment.OSVersion.Version.Major >= 10 && AppColors.IsDarkMode)
            {
                int useImmersiveDarkMode = 1;
                DwmSetWindowAttribute(Handle, 20, ref useImmersiveDarkMode, sizeof(int));
            }
            ApplyRoundedRegion();
        }

        private void ApplyRoundedRegion()
        {
            if (Width <= 0 || Height <= 0)
                return;

            Region?.Dispose();
            using var path = GraphicsHelpers.RoundedRect(new Rectangle(0, 0, Width, Height), CornerRadius);
            Region = new Region(path);
        }

        // Helper to add buttons to footer easily
        protected void AddFooterButtons(params Button[] buttons)
        {
            _footerPanel.ColumnCount = buttons.Length;
            _footerPanel.ColumnStyles.Clear();
            _footerPanel.Controls.Clear();
            
            // Add a spacer column to push buttons to the right
            _footerPanel.ColumnCount = buttons.Length + 1;
            _footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            
            for (int i = 0; i < buttons.Length; i++)
            {
                _footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                buttons[i].Anchor = AnchorStyles.Right;
                buttons[i].Margin = new Padding(8, 0, 0, 0);
                buttons[i].Height = 36;
                _footerPanel.Controls.Add(buttons[i], i + 1, 0);
            }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                base.Dispose(disposing);
            }
            catch (Exception)
            {
                // Suppress exception during GC finalization of uninitialized objects in tests
            }
        }
    }
}
