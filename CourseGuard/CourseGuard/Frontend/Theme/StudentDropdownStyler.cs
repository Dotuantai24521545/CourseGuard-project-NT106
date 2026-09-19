using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace CourseGuard.Frontend.Theme
{
    public static class StudentDropdownStyler
    {
        private const int MenuRadius = 8;
        private const int MenuMinWidth = 230;
        private const int MenuItemHeight = 36;
        private const int ComboItemHeight = 34;
        private const int ComboRadius = 10;
        private const int ComboHeight = 36;
        private const long PopupReopenSuppressMs = 150;
        private static readonly ConditionalWeakTable<ComboBox, ComboPopupState> ComboStates = new();

        public static void Apply(ContextMenuStrip menu, int minimumWidth = MenuMinWidth)
        {
            if (menu.IsDisposed)
                return;

            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.Font = AppFonts.Body;
            menu.Padding = new Padding(5);
            menu.MinimumSize = new Size(Math.Max(MenuMinWidth, minimumWidth), 0);
            menu.BackColor = MenuBackColor;
            menu.ForeColor = AppColors.TextPrimary;
            menu.Cursor = Cursors.Hand;
            menu.DropShadowEnabled = false;
            menu.Renderer = StudentDropdownRenderer.Instance;
            menu.Opened -= Menu_Opened;
            menu.Opened += Menu_Opened;
            menu.SizeChanged -= Menu_SizeChanged;
            menu.SizeChanged += Menu_SizeChanged;

            int itemMinWidth = Math.Max(MenuMinWidth, minimumWidth);
            foreach (ToolStripItem item in menu.Items)
                StyleItem(item, itemMinWidth);
        }

        public static void StyleComboBox(
            ComboBox combo,
            bool? useCustomPopup = null,
            bool blendWithCard = false,
            string? displayTextOverride = null,
            bool useSidebarChrome = false)
        {
            ComboPopupState state = ComboStates.GetOrCreateValue(combo);
            if (useCustomPopup.HasValue)
                state.UseCustomPopup = useCustomPopup.Value;
            state.BlendWithCard = blendWithCard;
            state.DisplayTextOverride = string.IsNullOrWhiteSpace(displayTextOverride) ? null : displayTextOverride.Trim();
            state.UseSidebarChrome = useSidebarChrome;

            combo.FlatStyle = FlatStyle.Flat;
            combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.IntegralHeight = false;
            combo.ItemHeight = ComboItemHeight;
            combo.Height = Math.Max(combo.Height, ComboHeight);
            combo.MinimumSize = new Size(Math.Max(combo.MinimumSize.Width, 160), ComboHeight);
            combo.DropDownHeight = state.UseCustomPopup ? 1 : 250;
            combo.Font = AppFonts.Body;
            Color nativeBackColor = useSidebarChrome
                ? ToNativeBackColor(AppColors.SidebarItemHover, AppColors.BgSidebar)
                : ToNativeBackColor(blendWithCard ? AppColors.BgCard : AppColors.BgInput, AppColors.BgInput);
            combo.BackColor = nativeBackColor;
            combo.ForeColor = useSidebarChrome ? AppColors.SidebarTextPrimary : AppColors.TextPrimary;
            combo.Cursor = Cursors.Hand;

            combo.DrawItem -= Combo_DrawItem;
            combo.DrawItem += Combo_DrawItem;
            combo.DropDown -= Combo_DropDown;
            combo.DropDown += Combo_DropDown;
            combo.KeyDown -= Combo_KeyDown;
            combo.MouseDown -= Combo_MouseDown;
            combo.MouseEnter -= Combo_MouseEnter;
            combo.MouseLeave -= Combo_MouseLeave;
            combo.GotFocus -= Combo_FocusChanged;
            combo.LostFocus -= Combo_FocusChanged;
            combo.SizeChanged -= Combo_SizeChanged;
            combo.SizeChanged += Combo_SizeChanged;
            combo.MouseEnter += Combo_MouseEnter;
            combo.MouseLeave += Combo_MouseLeave;
            combo.GotFocus += Combo_FocusChanged;
            combo.LostFocus += Combo_FocusChanged;

            state.Attach(combo);

            if (state.UseCustomPopup)
            {
                combo.KeyDown += Combo_KeyDown;
                combo.MouseDown += Combo_MouseDown;
            }

            ApplyComboRegion(combo);
        }

        private static Color ToNativeBackColor(Color preferred, Color fallback)
        {
            return preferred.A == 255 ? preferred : Color.FromArgb(fallback.R, fallback.G, fallback.B);
        }

        private static Color MenuBackColor => AppColors.IsDarkMode
            ? ColorTranslator.FromHtml("#1B1B1F")
            : AppColors.BgCard;

        private static Color HoverBackColor => MetaTheme.Colors.AccentSoft;

        private static Color ComboBorderColor => AppColors.IsDarkMode
            ? Color.FromArgb(115, 148, 163, 184)
            : ColorTranslator.FromHtml("#CBD5E1");

        private static Color ComboHoverBorderColor => AppColors.IsDarkMode
            ? Color.FromArgb(160, 148, 163, 184)
            : ColorTranslator.FromHtml("#94A3B8");

        private static GraphicsPath RoundedRectF(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();
            float maxRadius = Math.Min(bounds.Width, bounds.Height) / 2f;
            radius = Math.Min(radius, maxRadius);

            if (radius <= 0f)
            {
                if (bounds.Width > 0f && bounds.Height > 0f)
                    path.AddRectangle(bounds);
                return path;
            }

            float d = radius * 2f;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void FillSmoothRoundedRect(Graphics graphics, RectangleF bounds, float radius, Color color)
        {
            using var path = RoundedRectF(bounds, radius);
            using var brush = new SolidBrush(color);
            graphics.FillPath(brush, path);
        }

        private static void DrawSmoothRoundedBorder(Graphics graphics, RectangleF bounds, float radius, Color color, float width)
        {
            using var path = RoundedRectF(bounds, radius);
            using var pen = new Pen(color, width)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            graphics.DrawPath(pen, path);
        }

        private static void StyleItem(ToolStripItem item, int minimumWidth)
        {
            if (item is ToolStripSeparator separator)
            {
                separator.AutoSize = false;
                separator.Height = 12;
                return;
            }

            item.BackColor = MenuBackColor;
            item.ForeColor = AppColors.TextPrimary;
            item.AutoSize = false;
            item.Padding = new Padding(18, 0, 18, 0);
            item.Margin = new Padding(0, 2, 0, 2);
            item.Size = new Size(Math.Max(minimumWidth, TextRenderer.MeasureText(item.Text, AppFonts.Body).Width + 56), MenuItemHeight);

            if (item is ToolStripMenuItem menuItem)
            {
                menuItem.DisplayStyle = ToolStripItemDisplayStyle.Text;
                menuItem.DropDown.BackColor = MenuBackColor;
                menuItem.DropDown.ForeColor = AppColors.TextPrimary;
                if (menuItem.DropDown is ContextMenuStrip childMenu)
                    Apply(childMenu);
            }
        }

        private static void Menu_Opened(object? sender, System.EventArgs e)
        {
            if (sender is not ContextMenuStrip menu || menu.IsDisposed || menu.Width <= 0 || menu.Height <= 0)
                return;

            menu.BackColor = MenuBackColor;
            menu.ForeColor = AppColors.TextPrimary;
            menu.Cursor = Cursors.Hand;
            menu.DropShadowEnabled = false;

            ApplyRoundedRegion(menu);
        }

        private static void Menu_SizeChanged(object? sender, System.EventArgs e)
        {
            if (sender is ContextMenuStrip menu && !menu.IsDisposed && menu.Width > 0 && menu.Height > 0)
                ApplyRoundedRegion(menu);
        }

        private static void ApplyRoundedRegion(ContextMenuStrip menu)
        {
            menu.Region?.Dispose();
            // Use an expanded rect so the Region doesn't clip the anti-aliased border corners
            using var path = GraphicsHelpers.RoundedRect(
                new Rectangle(-1, -1, menu.Width + 2, menu.Height + 2), MenuRadius + 1);
            menu.Region = new Region(path);
        }

        private static void Combo_DropDown(object? sender, System.EventArgs e)
        {
            if (sender is not ComboBox combo)
                return;

            ComboPopupState state = ComboStates.GetOrCreateValue(combo);
            if (!state.UseCustomPopup)
            {
                StyleComboBox(combo);
                return;
            }

            combo.BeginInvoke(new MethodInvoker(() =>
            {
                combo.DroppedDown = false;
                ShowComboPopup(combo);
            }));
        }

        private static void Combo_MouseDown(object? sender, MouseEventArgs e)
        {
            if (sender is ComboBox combo && e.Button == MouseButtons.Left)
                ShowComboPopup(combo);
        }

        private static void Combo_KeyDown(object? sender, KeyEventArgs e)
        {
            if (sender is not ComboBox combo)
                return;

            bool shouldOpen = e.KeyCode is Keys.Enter or Keys.Space or Keys.Down
                || (e.Alt && e.KeyCode == Keys.Down)
                || e.KeyCode == Keys.F4;

            if (!shouldOpen)
                return;

            e.Handled = true;
            e.SuppressKeyPress = true;
            ShowComboPopup(combo);
        }

        private static void Combo_SizeChanged(object? sender, System.EventArgs e)
        {
            if (sender is ComboBox combo)
            {
                ApplyComboRegion(combo);
                combo.Invalidate();
            }
        }

        private static void ApplyComboRegion(ComboBox combo)
        {
            if (combo.Width <= 0 || combo.Height <= 0 || combo.IsDisposed)
                return;

            combo.Region?.Dispose();
            // Use a 1px-expanded rect so the Region never clips the anti-aliased
            // border corners drawn at the inset float rect (0.5, 0.5, W-1, H-1).
            using var path = GraphicsHelpers.RoundedRect(
                new Rectangle(-1, -1, combo.Width + 2, combo.Height + 2), ComboRadius + 1);
            combo.Region = new Region(path);
        }

        private static void Combo_MouseEnter(object? sender, System.EventArgs e)
        {
            if (sender is ComboBox combo)
            {
                ComboPopupState state = ComboStates.GetOrCreateValue(combo);
                state.IsHovered = true;
                combo.Invalidate();
            }
        }

        private static void Combo_MouseLeave(object? sender, System.EventArgs e)
        {
            if (sender is ComboBox combo)
            {
                ComboPopupState state = ComboStates.GetOrCreateValue(combo);
                state.IsHovered = false;
                combo.Invalidate();
            }
        }

        private static void Combo_FocusChanged(object? sender, System.EventArgs e)
        {
            if (sender is ComboBox combo)
            {
                ComboPopupState state = ComboStates.GetOrCreateValue(combo);
                state.IsFocused = combo.Focused;
                combo.Invalidate();
            }
        }



        private static void ShowComboPopup(ComboBox combo)
        {
            ComboPopupState state = ComboStates.GetOrCreateValue(combo);
            if (!state.UseCustomPopup || state.IsOpen || combo.IsDisposed)
                return;

            long elapsedSinceClose = System.Environment.TickCount64 - state.LastCloseTick;
            if (state.LastCloseTick > 0 && elapsedSinceClose >= 0 && elapsedSinceClose < PopupReopenSuppressMs)
                return;

            state.IsOpen = true;
            var menu = new ContextMenuStrip();

            if (combo.Items.Count == 0)
            {
                var emptyItem = new ToolStripMenuItem("Không có lựa chọn")
                {
                    Enabled = false
                };
                menu.Items.Add(emptyItem);
            }
            else
            {
                for (int i = 0; i < combo.Items.Count; i++)
                {
                    int selectedIndex = i;
                    string text = combo.GetItemText(combo.Items[i]) ?? string.Empty;
                    var item = new ToolStripMenuItem(text)
                    {
                        Tag = selectedIndex
                    };
                    item.Click += (_, _) =>
                    {
                        combo.SelectedIndex = selectedIndex;
                        combo.Focus();
                        combo.Invalidate();
                    };
                    menu.Items.Add(item);
                }
            }

            Apply(menu, combo.Width);
            menu.Closed += (_, _) =>
            {
                state.LastCloseTick = System.Environment.TickCount64;
                state.IsOpen = false;
                combo.Invalidate();
            };

            if (!combo.IsDisposed && combo.IsHandleCreated && !menu.IsDisposed)
                menu.Show(combo, new Point(0, combo.Height + 4));
            else
                state.IsOpen = false;
        }

        private static void Combo_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (sender is not ComboBox combo)
                return;

            bool isEditArea = (e.State & DrawItemState.ComboBoxEdit) == DrawItemState.ComboBoxEdit;
            bool selected = !isEditArea && (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            ComboPopupState state = ComboStates.GetOrCreateValue(combo);
            Color editBackColor = state.UseSidebarChrome ? AppColors.SidebarItemHover : state.BlendWithCard ? AppColors.BgCard : AppColors.BgInput;
            Color bg = isEditArea ? editBackColor : selected ? HoverBackColor : MenuBackColor;
            Color fg = isEditArea && state.UseSidebarChrome ? AppColors.SidebarTextPrimary : AppColors.TextPrimary;

            using (SolidBrush bgBrush = new SolidBrush(bg))
                e.Graphics.FillRectangle(bgBrush, e.Bounds);

            string? text = null;
            if (isEditArea && !string.IsNullOrWhiteSpace(state.DisplayTextOverride))
                text = state.DisplayTextOverride;
            else if (e.Index >= 0 && e.Index < combo.Items.Count)
                text = combo.GetItemText(combo.Items[e.Index]) ?? string.Empty;

            if (text != null)
            {
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                using StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                using SolidBrush textBrush = new SolidBrush(fg);
                Rectangle textRect = new Rectangle(e.Bounds.Left + 14, e.Bounds.Top, e.Bounds.Width - 24, e.Bounds.Height);
                e.Graphics.DrawString(text, AppFonts.Body, textBrush, textRect, sf);
            }
        }

        private sealed class ComboPopupState
        {
            private ComboBoxPainter? _painter;

            public bool UseCustomPopup { get; set; }
            public bool IsOpen { get; set; }
            public bool IsHovered { get; set; }
            public bool IsFocused { get; set; }
            public long LastCloseTick { get; set; }
            public bool BlendWithCard { get; set; }
            public string? DisplayTextOverride { get; set; }
            public bool UseSidebarChrome { get; set; }

            public void Attach(ComboBox combo)
            {
                _painter ??= new ComboBoxPainter();
                _painter.Attach(combo);
            }
        }

        private sealed class ComboBoxPainter : NativeWindow
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            private static extern IntPtr GetWindowDC(IntPtr hWnd);

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

            private const int WmPaint = 0x000F;
            private const int WmNcPaint = 0x0085;
            private ComboBox? _combo;

            public void Attach(ComboBox combo)
            {
                if (_combo == combo && Handle == combo.Handle)
                    return;

                ReleaseHandle();
                _combo = combo;

                if (combo.IsHandleCreated)
                    AssignHandle(combo.Handle);

                combo.HandleCreated -= Combo_HandleCreated;
                combo.HandleDestroyed -= Combo_HandleDestroyed;
                combo.HandleCreated += Combo_HandleCreated;
                combo.HandleDestroyed += Combo_HandleDestroyed;
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);

                if ((m.Msg == WmPaint || m.Msg == WmNcPaint) && _combo != null && !_combo.IsDisposed)
                    PaintComboChrome(_combo);
            }

            private void Combo_HandleCreated(object? sender, System.EventArgs e)
            {
                if (sender is ComboBox combo)
                {
                    AssignHandle(combo.Handle);
                    ApplyComboRegion(combo);
                }
            }

            private void Combo_HandleDestroyed(object? sender, System.EventArgs e)
            {
                ReleaseHandle();
            }

            private static Color GetEffectiveBackColor(Control? control)
            {
                return RoundedPanel.ResolveParentBackground(control);
            }

            private static void PaintComboChrome(ComboBox combo)
            {
                IntPtr hdc = GetWindowDC(combo.Handle);
                if (hdc == IntPtr.Zero) return;

                int actualWidth = combo.Width;
                int actualHeight = combo.Height;

                try
                {
                    using Graphics g = Graphics.FromHdc(hdc);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                    Color parentBg = GetEffectiveBackColor(combo.Parent);
                    using (SolidBrush parentBrush = new SolidBrush(parentBg))
                    {
                        g.FillRectangle(parentBrush, 0, 0, actualWidth, actualHeight);
                    }

                    ComboPopupState state = ComboStates.GetOrCreateValue(combo);
                    Color bgColor = state.UseSidebarChrome ? AppColors.SidebarItemHover : state.BlendWithCard ? AppColors.BgCard : MetaTheme.Colors.InputBg;
                    Color borderColor = state.UseSidebarChrome
                        ? (state.IsFocused || state.IsOpen ? Color.FromArgb(180, Color.White) : Color.FromArgb(72, Color.White))
                        : state.IsFocused || state.IsOpen
                            ? MetaTheme.Colors.BorderFocus
                            : state.IsHovered
                                ? ComboHoverBorderColor
                                : ComboBorderColor;

                    RectangleF chromeRect = new RectangleF(1f, 1f, combo.Width - 2f, combo.Height - 2f);
                    float borderWidth = state.IsFocused || state.IsOpen ? 1.35f : 1f;
                    FillSmoothRoundedRect(g, chromeRect, ComboRadius, bgColor);

                    string text = !string.IsNullOrWhiteSpace(state.DisplayTextOverride)
                        ? state.DisplayTextOverride
                        : combo.SelectedIndex >= 0
                        ? combo.GetItemText(combo.SelectedItem) ?? string.Empty
                        : combo.Text;

                    using StringFormat sf = new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter
                    };
                    Color textColor = state.UseSidebarChrome ? AppColors.SidebarTextPrimary : AppColors.TextPrimary;
                    using SolidBrush textBrush = new SolidBrush(textColor);
                    Rectangle textRect = new Rectangle(14, 1, Math.Max(1, actualWidth - 48), Math.Max(1, actualHeight - 2));
                    g.DrawString(text, AppFonts.Body, textBrush, textRect, sf);

                    Point center = new Point(actualWidth - 15, actualHeight / 2);
                    Color arrowColor = state.UseSidebarChrome
                        ? AppColors.SidebarTextPrimary
                        : state.IsFocused || state.IsOpen ? MetaTheme.Colors.Accent : AppColors.TextSecondary;
                    {
                        using Pen pen = new Pen(arrowColor, 1.8f)
                        {
                            StartCap = LineCap.Round,
                            EndCap = LineCap.Round,
                            LineJoin = LineJoin.Round
                        };

                        if (state.IsOpen)
                        {
                            g.DrawLine(pen, center.X - 4, center.Y + 2, center.X, center.Y - 2);
                            g.DrawLine(pen, center.X, center.Y - 2, center.X + 4, center.Y + 2);
                        }
                        else
                        {
                            g.DrawLine(pen, center.X - 4, center.Y - 2, center.X, center.Y + 2);
                            g.DrawLine(pen, center.X, center.Y + 2, center.X + 4, center.Y - 2);
                        }
                    }
                    DrawSmoothRoundedBorder(g, chromeRect, ComboRadius, borderColor, borderWidth);
                    {
                        int bottomY = combo.Height - 2;
                        using Pen pen = new Pen(borderColor, borderWidth)
                        {
                            StartCap = LineCap.Round,
                            EndCap = LineCap.Round,
                            LineJoin = LineJoin.Round
                        };
                        g.DrawLine(pen, 10, bottomY, combo.Width - 11, bottomY);
                    }
                }
                finally
                {
                    ReleaseDC(combo.Handle, hdc);
                }
            }
        }

        private sealed class StudentDropdownRenderer : ToolStripProfessionalRenderer
        {
            public static readonly StudentDropdownRenderer Instance = new();

            private StudentDropdownRenderer() : base(new StudentDropdownColorTable())
            {
                RoundedEdges = true;
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
                RectangleF rect = new RectangleF(0.5f, 0.5f, e.ToolStrip.Width - 1f, e.ToolStrip.Height - 1f);
                DrawSmoothRoundedBorder(e.Graphics, rect, MenuRadius, ComboBorderColor, 1f);
            }

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                if (e.ToolStrip == null)
                    return;

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
                RectangleF rect = new RectangleF(0.5f, 0.5f, e.ToolStrip.Width - 1f, e.ToolStrip.Height - 1f);
                FillSmoothRoundedRect(e.Graphics, rect, MenuRadius, MenuBackColor);
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (e.Item == null)
                    return;

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
                RectangleF rect = new RectangleF(5.5f, 2.5f, e.Item.Width - 11f, e.Item.Height - 5f);
                Color bg = e.Item.Selected ? HoverBackColor : MenuBackColor;
                FillSmoothRoundedRect(e.Graphics, rect, 6, bg);
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                using Pen pen = new Pen(AppColors.Border);
                e.Graphics.DrawLine(pen, 8, e.Item.Height / 2, e.Item.Width - 8, e.Item.Height / 2);
            }

            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = AppColors.TextSecondary;
                base.OnRenderArrow(e);
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                if (e.Item == null)
                    return;

                Color textColor = AppColors.TextPrimary;
                Rectangle textRect = new Rectangle(20, 0, e.Item.Width - 40, e.Item.Height);
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                
                using StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                using SolidBrush textBrush = new SolidBrush(textColor);
                e.Graphics.DrawString(e.Text, AppFonts.Body, textBrush, textRect, sf);
            }
        }

        private sealed class StudentDropdownColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => MenuBackColor;
            public override Color ImageMarginGradientBegin => MenuBackColor;
            public override Color ImageMarginGradientMiddle => MenuBackColor;
            public override Color ImageMarginGradientEnd => MenuBackColor;
            public override Color MenuBorder => AppColors.BorderStrong;
            public override Color MenuItemBorder => HoverBackColor;
            public override Color MenuItemSelected => HoverBackColor;
            public override Color MenuItemSelectedGradientBegin => HoverBackColor;
            public override Color MenuItemSelectedGradientEnd => HoverBackColor;
            public override Color MenuItemPressedGradientBegin => HoverBackColor;
            public override Color MenuItemPressedGradientEnd => HoverBackColor;
            public override Color SeparatorDark => AppColors.Border;
            public override Color SeparatorLight => AppColors.Border;
        }
    }
}
