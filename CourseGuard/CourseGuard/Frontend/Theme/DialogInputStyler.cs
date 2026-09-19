using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace CourseGuard.Frontend.Theme
{
    public static class DialogInputStyler
    {
        private const int InputRadius = 8;
        private const int InputHeight = 38;

        public static void ApplyAdminStyle(Control root)
        {
            if (root == null || root.IsDisposed)
                return;

            StyleChildren(root);
            root.Invalidate(true);
        }

        public static Control CreateInputHost(Control control)
        {
            if (control.Parent is DialogInputShell existingShell)
            {
                existingShell.RefreshTheme();
                return existingShell;
            }

            StyleExistingControl(control);

            if (control is ComboBox || !ShouldWrap(control))
                return control;

            return CreateShell(control, CaptureLayout(control));
        }

        public static void StyleExistingControl(Control control)
        {
            if (control.IsDisposed)
                return;

            switch (control)
            {
                case ComboBox combo:
                    combo.Font = MetaTheme.Fonts.BodyMd();
                    combo.ForeColor = AppColors.TextPrimary;
                    StudentDropdownStyler.StyleComboBox(combo, useCustomPopup: true);
                    combo.Margin = NormalizeMargin(combo.Margin);
                    break;
                case TextBoxBase textBox:
                    textBox.Font = MetaTheme.Fonts.BodyMd();
                    textBox.ForeColor = AppColors.TextPrimary;
                    textBox.BorderStyle = BorderStyle.None;
                    textBox.BackColor = AppColors.BgInput;
                    if (textBox is TextBox tb && !tb.Multiline)
                    {
                        tb.AutoSize = false;
                        tb.Height = Math.Max(tb.Height, tb.PreferredHeight);
                    }
                    break;
                case DateTimePicker dateTimePicker:
                    dateTimePicker.Font = MetaTheme.Fonts.BodyMd();
                    dateTimePicker.ForeColor = AppColors.TextPrimary;
                    dateTimePicker.CalendarForeColor = AppColors.TextPrimary;
                    dateTimePicker.CalendarMonthBackground = AppColors.BgInput;
                    dateTimePicker.CalendarTitleBackColor = AppColors.BgCard;
                    dateTimePicker.CalendarTitleForeColor = AppColors.TextPrimary;
                    dateTimePicker.BackColor = AppColors.BgInput;
                    break;
                case NumericUpDown numeric:
                    numeric.Font = MetaTheme.Fonts.BodyMd();
                    numeric.ForeColor = AppColors.TextPrimary;
                    numeric.BorderStyle = BorderStyle.None;
                    numeric.BackColor = AppColors.BgInput;
                    break;
                case CheckedListBox checkedList:
                    checkedList.Font = MetaTheme.Fonts.BodyMd();
                    checkedList.BorderStyle = BorderStyle.None;
                    checkedList.BackColor = AppColors.BgInput;
                    checkedList.ForeColor = AppColors.TextPrimary;
                    break;
            }
        }

        private static void StyleChildren(Control root)
        {
            foreach (Control child in root.Controls.Cast<Control>().ToArray())
            {
                if (child is DialogInputShell shell)
                {
                    shell.RefreshTheme();
                    continue;
                }

                if (child is DialogNumericInputShell numericShell)
                {
                    numericShell.RefreshTheme();
                    continue;
                }

                if (IsCustomInputContainer(child))
                {
                    child.BackColor = Color.Transparent;
                    child.Invalidate();
                    continue;
                }

                if (child is ComboBox)
                {
                    StyleExistingControl(child);
                    continue;
                }

                if (ShouldWrap(child))
                {
                    WrapAttachedControl(child);
                    continue;
                }

                StyleExistingControl(child);
                StyleChildren(child);
            }
        }

        private static void WrapAttachedControl(Control control)
        {
            Control? parent = control.Parent;
            if (parent == null || parent is DialogInputShell)
                return;

            StyleExistingControl(control);

            ControlLayout layout = CaptureLayout(control);

            int index = -1;
            int column = -1, row = -1, columnSpan = 1, rowSpan = 1;

            if (parent is TableLayoutPanel tableInfo)
            {
                column = tableInfo.GetColumn(control);
                row = tableInfo.GetRow(control);
                columnSpan = tableInfo.GetColumnSpan(control);
                rowSpan = tableInfo.GetRowSpan(control);
            }
            else
            {
                index = parent.Controls.GetChildIndex(control);
            }

            Control shell = CreateShell(control, layout);

            parent.SuspendLayout();
            try
            {
                if (parent is TableLayoutPanel table)
                {
                    table.Controls.Remove(control);
                    table.Controls.Add(shell, Math.Max(0, column), Math.Max(0, row));
                    table.SetColumnSpan(shell, Math.Max(1, columnSpan));
                    table.SetRowSpan(shell, Math.Max(1, rowSpan));
                }
                else
                {
                    parent.Controls.Remove(control);
                    parent.Controls.Add(shell);
                    parent.Controls.SetChildIndex(shell, index);
                }
            }
            finally
            {
                parent.ResumeLayout(true);
            }
        }

        private static bool ShouldWrap(Control control)
        {
            return control is TextBoxBase
                || control is DateTimePicker
                || control is NumericUpDown
                || control is CheckedListBox;
        }

        private static Control CreateShell(Control control, ControlLayout layout)
        {
            return control is NumericUpDown numeric
                ? new DialogNumericInputShell(numeric, layout)
                : new DialogInputShell(control, layout);
        }

        private static bool IsCustomInputContainer(Control control)
        {
            string typeName = control.GetType().Name;
            return typeName is "DialogInputShell"
                or "DialogNumericInputShell"
                or "RoundedInputPanel"
                or "DarkTextInput"
                or "DarkNumericInput"
                or "DarkDateTimePicker"
                or "DarkDatePicker"
                or "SearchBoxPanel";
        }

        private static ControlLayout CaptureLayout(Control control)
        {
            return new ControlLayout(
                control.Dock,
                control.Anchor,
                control.Location,
                control.Size,
                NormalizeMargin(control.Margin));
        }

        private static Padding NormalizeMargin(Padding margin)
        {
            return margin == Padding.Empty ? new Padding(0, 4, 0, 4) : margin;
        }

        private readonly record struct ControlLayout(
            DockStyle Dock,
            AnchorStyles Anchor,
            Point Location,
            Size Size,
            Padding Margin);

        private sealed class DialogInputShell : Panel
        {
            private readonly Control _inner;
            private bool _isFocused;
            private bool _isHovered;

            public DialogInputShell(Control inner, ControlLayout layout)
            {
                _inner = inner;
                Tag = "custom";
                BackColor = Color.Transparent;
                DoubleBuffered = true;
                Dock = layout.Dock;
                Anchor = layout.Anchor;
                Location = layout.Location;
                Size = ResolveShellSize(inner, layout.Size);
                MinimumSize = new Size(0, InputHeight);
                Margin = layout.Margin;
                Padding = ResolvePadding(inner);

                inner.Margin = Padding.Empty;
                inner.Dock = DockStyle.Fill;
                inner.GotFocus += (_, _) => SetFocused(true);
                inner.LostFocus += (_, _) => SetFocused(false);
                inner.MouseEnter += (_, _) => SetHovered(true);
                inner.MouseLeave += (_, _) => SetHovered(false);
                MouseEnter += (_, _) => SetHovered(true);
                MouseLeave += (_, _) => SetHovered(false);

                Controls.Add(inner);
                RefreshTheme();
            }

            public void RefreshTheme()
            {
                FillColor = AppColors.BgInput;
                StyleExistingControl(_inner);
                Invalidate();
            }

            private Color FillColor { get; set; } = AppColors.BgInput;

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;

                Color parentColor = RoundedPanel.ResolveParentBackground(Parent);
                using (var parentBrush = new SolidBrush(parentColor))
                    e.Graphics.FillRectangle(parentBrush, ClientRectangle);

                RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
                float radius = Math.Min(InputRadius, Math.Min(rect.Width, rect.Height) / 2f);
                using var path = GraphicsHelpers.RoundedRectF(rect, radius);
                using var fillBrush = new SolidBrush(FillColor);
                e.Graphics.FillPath(fillBrush, path);

                Color borderColor = _isFocused
                    ? AppColors.AccentBlue
                    : _isHovered
                        ? AppColors.BorderStrong
                        : AppColors.Border;
                using var borderPen = new Pen(borderColor, _isFocused ? 1.5f : 1f);
                e.Graphics.DrawPath(borderPen, path);
            }

            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);
                ApplyRoundedRegion();
            }

            private void SetFocused(bool focused)
            {
                _isFocused = focused;
                Invalidate();
            }

            private void SetHovered(bool hovered)
            {
                _isHovered = hovered;
                Invalidate();
            }

            private void ApplyRoundedRegion()
            {
                if (Width <= 0 || Height <= 0 || IsDisposed)
                    return;

                Region?.Dispose();
                using var path = GraphicsHelpers.RoundedRect(new Rectangle(-1, -1, Width + 2, Height + 2), InputRadius + 1);
                Region = new Region(path);
            }

            private static Size ResolveShellSize(Control inner, Size originalSize)
            {
                int width = Math.Max(originalSize.Width, inner.Width);
                int height = Math.Max(originalSize.Height, InputHeight);

                if (inner is TextBox textBox && textBox.Multiline)
                    height = Math.Max(height, textBox.Height);
                else if (inner is CheckedListBox)
                    height = Math.Max(height, inner.Height);
                else
                    height = Math.Max(InputHeight, Math.Min(height, 44));

                return new Size(width, height);
            }

            private static Padding ResolvePadding(Control inner)
            {
                return inner switch
                {
                    TextBox textBox when textBox.Multiline => new Padding(12, 10, 12, 10),
                    CheckedListBox => new Padding(8),
                    DateTimePicker => new Padding(8, 6, 8, 6),
                    NumericUpDown => new Padding(8, 7, 8, 7),
                    _ => new Padding(12, 8, 12, 8)
                };
            }
        }

        private sealed class DialogNumericInputShell : Panel
        {
            private readonly NumericUpDown _numeric;
            private readonly TextBox _textBox = new();
            private readonly DialogNumericSpinnerPanel _spinner = new();
            private bool _isFocused;
            private bool _isHovered;
            private bool _updatingText;

            public DialogNumericInputShell(NumericUpDown numeric, ControlLayout layout)
            {
                _numeric = numeric;
                Tag = "custom";
                BackColor = Color.Transparent;
                DoubleBuffered = true;
                Dock = layout.Dock;
                Anchor = layout.Anchor;
                Location = layout.Location;
                Size = new Size(Math.Max(layout.Size.Width, numeric.Width), Math.Max(InputHeight, layout.Size.Height));
                MinimumSize = new Size(0, InputHeight);
                Margin = layout.Margin;
                Padding = new Padding(10, 5, 5, 5);

                _textBox.BorderStyle = BorderStyle.None;
                _textBox.Dock = DockStyle.Fill;
                _textBox.Margin = Padding.Empty;
                _textBox.TextAlign = HorizontalAlignment.Left;
                _textBox.GotFocus += (_, _) => SetFocused(true);
                _textBox.LostFocus += (_, _) =>
                {
                    CommitText();
                    SetFocused(false);
                };
                _textBox.KeyDown += TextBox_KeyDown;
                _textBox.KeyPress += TextBox_KeyPress;
                _textBox.MouseEnter += (_, _) => SetHovered(true);
                _textBox.MouseLeave += (_, _) => SetHovered(false);

                _spinner.Dock = DockStyle.Right;
                _spinner.Width = 26;
                _spinner.Margin = Padding.Empty;
                _spinner.IncrementRequested += (_, _) => ChangeValue(_numeric.Increment);
                _spinner.DecrementRequested += (_, _) => ChangeValue(-_numeric.Increment);

                _numeric.Visible = false;
                _numeric.Margin = Padding.Empty;
                _numeric.ValueChanged += (_, _) => SyncTextFromNumeric();

                MouseEnter += (_, _) => SetHovered(true);
                MouseLeave += (_, _) => SetHovered(false);

                Controls.Add(_textBox);
                Controls.Add(_spinner);
                Controls.Add(_numeric);
                RefreshTheme();
                SyncTextFromNumeric();
            }

            public void RefreshTheme()
            {
                BackColor = Color.Transparent;
                StyleExistingControl(_numeric);
                _textBox.BackColor = AppColors.BgInput;
                _textBox.ForeColor = AppColors.TextPrimary;
                _textBox.Font = MetaTheme.Fonts.BodyMd();
                _spinner.ApplyTheme();
                Invalidate();
            }

            protected override void OnEnabledChanged(EventArgs e)
            {
                base.OnEnabledChanged(e);
                _textBox.Enabled = Enabled;
                _spinner.Enabled = Enabled;
                _numeric.Enabled = Enabled;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;

                Color parentColor = RoundedPanel.ResolveParentBackground(Parent);
                using (var parentBrush = new SolidBrush(parentColor))
                    e.Graphics.FillRectangle(parentBrush, ClientRectangle);

                RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
                float radius = Math.Min(InputRadius, Math.Min(rect.Width, rect.Height) / 2f);
                using var path = GraphicsHelpers.RoundedRectF(rect, radius);
                using var fillBrush = new SolidBrush(AppColors.BgInput);
                e.Graphics.FillPath(fillBrush, path);

                Color borderColor = _isFocused
                    ? AppColors.AccentBlue
                    : _isHovered
                        ? AppColors.BorderStrong
                        : AppColors.BorderStrong;
                using var borderPen = new Pen(borderColor, _isFocused ? 1.5f : 1f);
                e.Graphics.DrawPath(borderPen, path);
            }

            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);
                if (Width <= 0 || Height <= 0 || IsDisposed)
                    return;

                Region?.Dispose();
                using var path = GraphicsHelpers.RoundedRect(new Rectangle(-1, -1, Width + 2, Height + 2), InputRadius + 1);
                Region = new Region(path);
            }

            private void TextBox_KeyDown(object? sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    CommitText();
                    e.SuppressKeyPress = true;
                    return;
                }

                if (e.KeyCode == Keys.Up)
                {
                    ChangeValue(_numeric.Increment);
                    e.SuppressKeyPress = true;
                    return;
                }

                if (e.KeyCode == Keys.Down)
                {
                    ChangeValue(-_numeric.Increment);
                    e.SuppressKeyPress = true;
                }
            }

            private void TextBox_KeyPress(object? sender, KeyPressEventArgs e)
            {
                if (char.IsControl(e.KeyChar) || char.IsDigit(e.KeyChar))
                    return;

                string decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
                if (_numeric.DecimalPlaces > 0
                    && decimalSeparator.Length == 1
                    && e.KeyChar.ToString() == decimalSeparator
                    && !_textBox.Text.Contains(decimalSeparator, StringComparison.CurrentCulture))
                {
                    return;
                }

                if (_numeric.Minimum < 0 && e.KeyChar == '-' && _textBox.SelectionStart == 0 && !_textBox.Text.Contains('-'))
                    return;

                e.Handled = true;
            }

            private void ChangeValue(decimal delta)
            {
                decimal next = Math.Max(_numeric.Minimum, Math.Min(_numeric.Maximum, _numeric.Value + delta));
                _numeric.Value = next;
                SyncTextFromNumeric();
            }

            private void CommitText()
            {
                if (_updatingText)
                    return;

                if (decimal.TryParse(_textBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal parsed)
                    || decimal.TryParse(_textBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
                {
                    _numeric.Value = Math.Max(_numeric.Minimum, Math.Min(_numeric.Maximum, parsed));
                }

                SyncTextFromNumeric();
            }

            private void SyncTextFromNumeric()
            {
                _updatingText = true;
                try
                {
                    _textBox.Text = FormatValue(_numeric.Value, _numeric.DecimalPlaces);
                }
                finally
                {
                    _updatingText = false;
                }
            }

            private static string FormatValue(decimal value, int decimalPlaces)
            {
                string format = decimalPlaces <= 0
                    ? "0"
                    : "0." + new string('#', decimalPlaces);
                return value.ToString(format, CultureInfo.CurrentCulture);
            }

            private void SetFocused(bool focused)
            {
                _isFocused = focused;
                Invalidate();
            }

            private void SetHovered(bool hovered)
            {
                _isHovered = hovered;
                Invalidate();
            }
        }

        private sealed class DialogNumericSpinnerPanel : Panel
        {
            private int _hoverZone = -1;

            public event EventHandler? IncrementRequested;
            public event EventHandler? DecrementRequested;

            public DialogNumericSpinnerPanel()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.ResizeRedraw, true);
                Cursor = Cursors.Hand;
                Tag = "custom";
                BackColor = AppColors.BgInput;
            }

            public void ApplyTheme()
            {
                BackColor = AppColors.BgInput;
                Invalidate();
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int nextZone = e.Y < Height / 2 ? 0 : 1;
                if (_hoverZone == nextZone) return;
                _hoverZone = nextZone;
                Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                _hoverZone = -1;
                Invalidate();
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button != MouseButtons.Left || !Enabled)
                    return;

                if (e.Y < Height / 2)
                    IncrementRequested?.Invoke(this, EventArgs.Empty);
                else
                    DecrementRequested?.Invoke(this, EventArgs.Empty);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.Clear(AppColors.BgInput);

                int half = Height / 2;
                using var hoverBrush = new SolidBrush(AppColors.IsDarkMode ? AppColors.BgCardHover : ColorTranslator.FromHtml("#EEF2F7"));
                if (Enabled && _hoverZone == 0)
                    e.Graphics.FillRectangle(hoverBrush, 1, 1, Width - 2, Math.Max(1, half - 1));
                else if (Enabled && _hoverZone == 1)
                    e.Graphics.FillRectangle(hoverBrush, 1, half, Width - 2, Math.Max(1, Height - half - 1));

                Color glyphColor = Enabled ? AppColors.TextSecondary : AppColors.TextMuted;
                using var borderPen = new Pen(AppColors.BorderStrong, 1f);
                e.Graphics.DrawLine(borderPen, 0, 4, 0, Height - 5);
                e.Graphics.DrawLine(borderPen, 4, half, Width - 5, half);

                using var glyphPen = new Pen(glyphColor, 1.6f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };

                int cx = Width / 2;
                int plusY = Math.Max(6, half / 2);
                int minusY = half + Math.Max(5, (Height - half) / 2);
                int arm = 4;

                e.Graphics.DrawLine(glyphPen, cx - arm, plusY, cx + arm, plusY);
                e.Graphics.DrawLine(glyphPen, cx, plusY - arm, cx, plusY + arm);
                e.Graphics.DrawLine(glyphPen, cx - arm, minusY, cx + arm, minusY);
            }
        }
    }
}
