using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CourseGuard.Frontend.Theme
{
    public sealed class SearchBoxPanel : RoundedPanel
    {
        private readonly TextBox _input;
        private bool _isHovered;

        public SearchBoxPanel(TextBox input, int width = 330)
        {
            _input = input;

            Width = width;
            Height = 36;
            MinimumSize = new Size(180, 36);
            Margin = Padding.Empty;
            CornerRadius = 12;
            Cursor = Cursors.IBeam;

            SearchFocusManager.MarkSearchInput(_input);
            _input.BorderStyle = BorderStyle.None;
            _input.Font = AppFonts.Body;
            _input.TabStop = false;
            _input.Margin = Padding.Empty;
            _input.Cursor = Cursors.IBeam;

            Controls.Add(_input);
            ApplyTheme();
            LayoutInput();

            MouseDown += (_, _) => _input.Focus();
            MouseEnter += (_, _) => SetHover(true);
            MouseLeave += (_, _) => SetHover(false);
            _input.MouseEnter += (_, _) => SetHover(true);
            _input.MouseLeave += (_, _) => SetHover(ClientRectangle.Contains(PointToClient(Cursor.Position)));
            _input.GotFocus += (_, _) => ApplyTheme();
            _input.LostFocus += (_, _) => ApplyTheme();
        }

        public void ApplyTheme()
        {
            FillColor = AppColors.IsDarkMode ? AppColors.BgInput : ColorTranslator.FromHtml("#F8FAFC");
            BorderColor = CurrentBorderColor();
            _input.BackColor = FillColor;
            _input.ForeColor = AppColors.TextPrimary;
            Invalidate();
        }

        protected override void OnResize(System.EventArgs eventargs)
        {
            base.OnResize(eventargs);
            Height = 36;
            LayoutInput();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            base.OnPaint(e);
            int iconSize = 17;
            int iconY = (Height - iconSize) / 2;
            DrawSearchIcon(e.Graphics, new Rectangle(11, iconY, iconSize, iconSize), AppColors.TextMuted);
        }

        private void LayoutInput()
        {
            if (_input.IsDisposed)
                return;

            int inputHeight = Math.Min(_input.PreferredHeight, Height - 8);
            int inputTop = (Height - inputHeight) / 2;
            _input.SetBounds(36, inputTop, Math.Max(40, Width - 48), inputHeight);
        }

        private static void DrawSearchIcon(Graphics g, Rectangle bounds, Color color)
        {
            using Pen pen = new Pen(color, 1.7f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };

            g.DrawEllipse(pen, bounds.Left + 1, bounds.Top + 1, 9, 9);
            g.DrawLine(pen, bounds.Left + 10, bounds.Top + 10, bounds.Right - 1, bounds.Bottom - 1);
        }

        private void SetHover(bool hovered)
        {
            if (_isHovered == hovered)
                return;

            _isHovered = hovered;
            ApplyTheme();
        }

        private Color CurrentBorderColor()
        {
            if (_input.Focused)
                return MetaTheme.Colors.BorderFocus;

            if (_isHovered)
            {
                return AppColors.IsDarkMode
                    ? Color.FromArgb(160, 148, 163, 184)
                    : ColorTranslator.FromHtml("#94A3B8");
            }

            return AppColors.IsDarkMode
                ? Color.FromArgb(115, 148, 163, 184)
                : ColorTranslator.FromHtml("#CBD5E1");
        }
    }
}
