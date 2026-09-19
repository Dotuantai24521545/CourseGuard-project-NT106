/*
 * MetaTheme.cs
 *
 * Layer: Presentation (Theme)
 * Dark modern design system inspired by todesktop.com.
 * Deep blue-black backgrounds, indigo accent, clean white text.
 *
 * Every public constant maps to the user-defined token palette.
 * Usage: MetaTheme.Colors.FormBg, MetaTheme.Fonts.BodyMd(), MetaTheme.Radius.Full, etc.
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace CourseGuard.Frontend.Theme
{
    /// <summary>
    /// Centralized design tokens — dark modern (todesktop.com style).
    /// </summary>
    public static class MetaTheme
    {
        // ═══════════════════════════════════════════════════════════════
        //  COLORS
        // ═══════════════════════════════════════════════════════════════

        public static class Colors
        {
            // ── Core surfaces ───────────────────────────────────────
            // MUST use => (not readonly) so values update when AppColors.IsDarkMode toggles
            public static Color FormBg          => AppColors.BgBase;
            public static Color CardBg          => AppColors.BgCard;
            public static Color SidebarBg       => AppColors.BgSidebar;
            public static Color ElevatedBg      => AppColors.BgCard;
            public static Color InputBg         => AppColors.BgInput;

            // ── Accent / Highlight ──────────────────────────────────
            public static Color Accent          => AppColors.AccentBlue;
            public static Color AccentHover     => AppColors.AccentHover;
            public static Color AccentPressed   => AppColors.AccentPressed;
            public static Color AccentSoft      => AppColors.AccentSoft;
            public static Color AccentMuted     => AppColors.IsDarkMode
                ? Color.FromArgb(30, 58, 138)
                : Color.FromArgb(219, 234, 254);

            // ── Text ────────────────────────────────────────────────
            public static Color TextPrimary     => AppColors.TextPrimary;
            public static Color TextSecondary   => AppColors.TextSecondary;
            public static Color TextMuted       => AppColors.TextMuted;
            public static Color TextDisabled    => AppColors.IsDarkMode
                ? Color.FromArgb(71, 85, 105)
                : Color.FromArgb(148, 163, 184);

            // ── Borders ─────────────────────────────────────────────
            public static Color Border          => AppColors.Border;
            public static Color BorderSoft      => AppColors.IsDarkMode
                ? Color.FromArgb(30, 30, 36)
                : Color.FromArgb(226, 232, 240);
            public static Color BorderFocus     => Accent;

            // ── Semantic ────────────────────────────────────────────
            public static readonly Color Success         = Color.FromArgb(34, 197, 94);
            public static readonly Color SuccessBg       = Color.FromArgb(20, 34, 197, 94);
            public static readonly Color Warning         = Color.FromArgb(251, 191, 36);
            public static readonly Color WarningBg       = Color.FromArgb(20, 251, 191, 36);
            public static readonly Color Critical        = Color.FromArgb(239, 68, 68);
            public static readonly Color CriticalBg      = Color.FromArgb(20, 239, 68, 68);
            public static readonly Color Info            = Color.FromArgb(56, 189, 248);
            public static readonly Color InfoBg          = Color.FromArgb(20, 56, 189, 248);

            // ── Button-specific ─────────────────────────────────────
            public static Color ButtonPrimary       => Accent;
            public static Color ButtonPrimaryHover  => AccentHover;
            public static Color ButtonSecondaryBg   => AppColors.IsDarkMode
                ? Color.FromArgb(30, 30, 50)
                : Color.FromArgb(241, 245, 249);
            public static Color ButtonSecondaryHover => AppColors.IsDarkMode
                ? Color.FromArgb(40, 40, 65)
                : Color.FromArgb(226, 232, 240);
            public static Color ButtonGhostHover    => AppColors.IsDarkMode
                ? Color.FromArgb(25, 255, 255, 255)
                : Color.FromArgb(25, 0, 0, 0);

            // ── Logout ──────────────────────────────────────────────
            public static readonly Color LogoutRed       = Color.FromArgb(220, 38, 38);
            public static readonly Color LogoutRedHover  = Color.FromArgb(239, 68, 68);

            // ── Sidebar ─────────────────────────────────────────────
            public static Color SidebarHover    => AppColors.IsDarkMode
                ? Color.FromArgb(30, 30, 50)
                : Color.FromArgb(226, 232, 240);
            public static Color SidebarActive   => Accent;

            // ── Backward-compat aliases ─────────────────────────────
            public static Color Primary      => Accent;
            public static Color PrimaryDeep  => AccentPressed;
            public static Color PrimarySoft  => AccentSoft;
            public static Color Canvas       => FormBg;
            public static Color SurfaceSoft  => CardBg;
            public static Color HairlineSoft => Border;
            public static Color Hairline     => BorderSoft;
            public static Color InkDeep      => TextPrimary;
            public static Color Ink          => TextPrimary;
            public static Color Charcoal     => TextSecondary;
            public static Color Slate        => TextMuted;
            public static Color Steel        => TextMuted;
            public static Color Stone        => TextDisabled;
            public static Color InkButton    => Accent;
            public static Color OnInkButton  => TextPrimary;
            public static Color OnPrimary    => TextPrimary;
            public static Color FbBlue       => Accent;
            public static Color ActivePill   => SidebarActive;
        }

        // ═══════════════════════════════════════════════════════════════
        //  TYPOGRAPHY
        // ═══════════════════════════════════════════════════════════════

        public static class Fonts
        {
            private static readonly string[] FamilyChain = { "Inter", "Segoe UI", "Helvetica", "Arial", "Noto Sans" };

            private static string ResolvedFamily
            {
                get
                {
                    foreach (var name in FamilyChain)
                    {
                        try
                        {
                            using var test = new Font(name, 10f);
                            if (string.Equals(test.Name, name, StringComparison.OrdinalIgnoreCase))
                                return name;
                        }
                        catch { /* skip */ }
                    }
                    return SystemFonts.MessageBoxFont?.FontFamily?.Name ?? "Segoe UI";
                }
            }

            private static string? _cached;
            private static string Family => _cached ??= ResolvedFamily;
            private static string EmphasisFamily => string.Equals(Family, "Segoe UI", StringComparison.OrdinalIgnoreCase)
                ? "Segoe UI Semibold"
                : Family;

            // ── Heading hierarchy ───────────────────────────────────
            public static Font HeroDisplay()  => new Font(EmphasisFamily, 28f, FontStyle.Regular);
            public static Font DisplayLg()    => new Font(EmphasisFamily, 22f, FontStyle.Regular);
            public static Font HeadingLg()    => new Font(EmphasisFamily, 18f, FontStyle.Regular);
            public static Font HeadingMd()    => new Font(EmphasisFamily, 14f, FontStyle.Regular);
            public static Font HeadingSm()    => new Font(EmphasisFamily, 12f, FontStyle.Regular);

            // ── Body + Subtitle ─────────────────────────────────────
            public static Font SubtitleLg()   => new Font(EmphasisFamily, 11f, FontStyle.Regular);
            public static Font SubtitleMd()   => new Font(Family, 11f, FontStyle.Regular);
            public static Font BodyMd()       => new Font(Family, 10f, FontStyle.Regular);
            public static Font BodyMdBold()   => new Font(EmphasisFamily, 10f, FontStyle.Regular);
            public static Font BodySm()       => new Font(Family, 9f,  FontStyle.Regular);
            public static Font BodySmBold()   => new Font(EmphasisFamily, 9f,  FontStyle.Regular);

            // ── Micro ───────────────────────────────────────────────
            public static Font CaptionBold()  => new Font(EmphasisFamily, 8f,  FontStyle.Regular);
            public static Font Caption()      => new Font(Family, 8f,  FontStyle.Regular);
            public static Font ButtonMd()     => new Font(EmphasisFamily, 10f, FontStyle.Regular);
        }

        // ═══════════════════════════════════════════════════════════════
        //  SPACING (px)
        // ═══════════════════════════════════════════════════════════════

        public static class Spacing
        {
            public const int XXS       = 4;
            public const int XS        = 8;
            public const int SM        = 10;
            public const int MD        = 12;
            public const int Base      = 16;
            public const int LG        = 20;
            public const int XL        = 24;
            public const int XXL       = 32;
            public const int XXXL      = 40;
            public const int SectionSm = 48;
            public const int Section   = 64;
            public const int SectionLg = 80;
            public const int Hero      = 120;
        }

        // ═══════════════════════════════════════════════════════════════
        //  BORDER RADIUS
        // ═══════════════════════════════════════════════════════════════

        public static class Radius
        {
            public const int XS      = 2;
            public const int SM      = 4;
            public const int MD      = 6;
            public const int LG      = 8;
            public const int XL      = 12;
            public const int XXL     = 16;
            public const int XXXL    = 20;
            public const int Feature = 24;
            public const int Full    = 100;   // Pill buttons
        }

        // ═══════════════════════════════════════════════════════════════
        //  COMPONENT HELPERS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Style a DataGridView — dark header, alternating dark rows, no harsh borders.
        /// </summary>
        public static void StyleGrid(DataGridView grid)
        {
            DashboardGridStyler.Apply(grid);
            grid.CellPainting -= Grid_CellPainting;
            grid.CellPainting += Grid_CellPainting;
        }

        private static void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.Graphics != null)
            {
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            }
        }

        /// <summary>
        /// Style a panel as a dark card with rounded corners and subtle border.
        /// </summary>
        public static void StyleCard(Panel panel, int radius = 12)
        {
            panel.BackColor = Colors.CardBg;
            panel.Paint -= PanelCardPaint;
            panel.Paint += PanelCardPaint;
            ApplyRoundedRegion(panel, radius);
            panel.Tag = radius;
            panel.Resize -= PanelResizeHandler;
            panel.Resize += PanelResizeHandler;
        }

        /// <summary>
        /// Style a sidebar button — dark inactive state with muted text.
        /// </summary>
        public static void StyleSidebarButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = Color.Transparent;
            btn.ForeColor = Colors.TextSecondary;
            btn.Font = Fonts.BodyMd();
            btn.TextAlign = ContentAlignment.MiddleLeft;
            btn.Padding = new Padding(Spacing.Base, 0, 0, 0);
            btn.Cursor = Cursors.Hand;
            btn.FlatAppearance.MouseOverBackColor = Colors.SidebarHover;
        }

        /// <summary>
        /// Set sidebar button active — indigo background pill.
        /// </summary>
        public static void SetSidebarActive(Button btn)
        {
            btn.BackColor = Colors.SidebarActive;
            btn.ForeColor = Colors.TextPrimary;
            btn.Font = Fonts.BodyMdBold();
        }

        /// <summary>
        /// Reset sidebar button to inactive.
        /// </summary>
        public static void SetSidebarInactive(Button btn)
        {
            btn.BackColor = Color.Transparent;
            btn.ForeColor = Colors.TextSecondary;
            btn.Font = Fonts.BodyMd();
        }

        /// <summary>
        /// Style logout button — red pill.
        /// </summary>
        public static void StyleLogoutButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = Colors.LogoutRed;
            btn.ForeColor = Colors.TextPrimary;
            btn.Font = Fonts.ButtonMd();
            btn.Cursor = Cursors.Hand;
            btn.FlatAppearance.MouseOverBackColor = Colors.LogoutRedHover;
        }

        /// <summary>
        /// Style a primary indigo CTA button.
        /// </summary>
        public static void StylePrimaryButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = Colors.ButtonPrimary;
            btn.ForeColor = Colors.TextPrimary;
            btn.Font = Fonts.ButtonMd();
            btn.Cursor = Cursors.Hand;
            btn.FlatAppearance.MouseOverBackColor = Colors.ButtonPrimaryHover;
        }

        /// <summary>
        /// Style a dark marketing CTA button (same as primary in dark mode).
        /// </summary>
        public static void StyleDarkButton(Button btn)
        {
            StylePrimaryButton(btn);
        }

        /// <summary>
        /// Style a secondary (ghost/outlined) button — subtle border on dark bg.
        /// </summary>
        public static void StyleSecondaryButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Colors.Border;
            btn.BackColor = Colors.ButtonSecondaryBg;
            btn.ForeColor = Colors.TextPrimary;
            btn.Font = Fonts.ButtonMd();
            btn.Cursor = Cursors.Hand;
            btn.FlatAppearance.MouseOverBackColor = Colors.ButtonSecondaryHover;
        }

        /// <summary>
        /// Style a ghost button — transparent with subtle border.
        /// </summary>
        public static void StyleGhostButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Colors.Border;
            btn.BackColor = Color.Transparent;
            btn.ForeColor = Colors.TextSecondary;
            btn.Font = Fonts.ButtonMd();
            btn.Cursor = Cursors.Hand;
            btn.FlatAppearance.MouseOverBackColor = Colors.ButtonGhostHover;
        }

        /// <summary>
        /// Style a Form with the dark background.
        /// </summary>
        public static void StyleForm(Form form)
        {
            form.BackColor = Colors.FormBg;
            form.ForeColor = Colors.TextPrimary;
            form.Font = Fonts.BodyMd();
        }

        /// <summary>
        /// Style a TextBox — dark background, white text, subtle border.
        /// </summary>
        public static void StyleTextInput(TextBox textBox)
        {
            textBox.Font = Fonts.BodyMd();
            textBox.ForeColor = Colors.TextPrimary;
            textBox.BackColor = Colors.InputBg;
            textBox.BorderStyle = BorderStyle.FixedSingle;
        }

        /// <summary>
        /// Style a ComboBox — dark background, white text.
        /// </summary>
        public static void StyleComboBox(ComboBox combo)
        {
            combo.Font = Fonts.BodyMd();
            combo.ForeColor = Colors.TextPrimary;
            combo.BackColor = Colors.InputBg;
            combo.FlatStyle = FlatStyle.Flat;

            // Fix WinForms DropDownList white background issue
            combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.DrawItem -= Combo_DrawItem;
            combo.DrawItem += Combo_DrawItem;
        }

        private static void Combo_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (sender is not ComboBox cb) return;

            e.DrawBackground();

            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color bg = isSelected ? Colors.AccentMuted : Colors.InputBg;
            Color fg = Colors.TextPrimary;

            using (SolidBrush brush = new SolidBrush(bg))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            if (e.Index >= 0)
            {
                string text = cb.Items[e.Index]?.ToString() ?? "";
                
                StringFormat sf = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Alignment = StringAlignment.Near,
                    FormatFlags = StringFormatFlags.NoWrap,
                    Trimming = StringTrimming.EllipsisCharacter
                };

                using (SolidBrush textBrush = new SolidBrush(fg))
                {
                    Rectangle textRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 4, e.Bounds.Height);
                    e.Graphics.DrawString(text, e.Font ?? cb.Font, textBrush, textRect, sf);
                }
            }

            e.DrawFocusRectangle();
        }

        /// <summary>
        /// Recursively apply dark theme to all controls in a container.
        /// Useful for UserControls that don't manually style each child.
        /// </summary>
        public static void ApplyDarkRecursive(Control root)
        {
            if (root is UserControl || root is Panel)
            {
                if (root.BackColor == SystemColors.Control || root.BackColor.GetBrightness() > 0.8f)
                {
                    root.BackColor = root is UserControl ? Colors.FormBg : Colors.CardBg;
                }
            }

            foreach (Control c in root.Controls)
            {
                if (c is Label lbl)
                {
                    if (lbl.Font.Size >= 12f)
                        lbl.ForeColor = Colors.TextPrimary;
                    else
                        lbl.ForeColor = lbl.Font.Bold ? Colors.TextPrimary : Colors.TextSecondary;
                }
                else if (c is TextBox tb)
                {
                    StyleTextInput(tb);
                }
                else if (c is ComboBox cb)
                {
                    StyleComboBox(cb);
                }
                else if (c is DataGridView dgv)
                {
                    StyleGrid(dgv);
                }
                else if (c is ListBox lb)
                {
                    lb.BackColor = Colors.InputBg;
                    lb.ForeColor = Colors.TextPrimary;
                    lb.BorderStyle = BorderStyle.FixedSingle;
                }
                else if (c is RichTextBox rtb)
                {
                    rtb.BackColor = Colors.InputBg;
                    rtb.ForeColor = Colors.TextPrimary;
                    rtb.BorderStyle = BorderStyle.FixedSingle;
                }
                else if (c is NumericUpDown nud)
                {
                    nud.BackColor = Colors.InputBg;
                    nud.ForeColor = Colors.TextPrimary;
                }
                else if (c is DateTimePicker dtp)
                {
                    // Basic styling for dtp calendar
                    dtp.CalendarMonthBackground = Colors.InputBg;
                    dtp.CalendarForeColor = Colors.TextPrimary;
                    dtp.CalendarTitleBackColor = Colors.SidebarBg;
                    dtp.CalendarTitleForeColor = Colors.TextPrimary;
                    dtp.CalendarTrailingForeColor = Colors.TextMuted;
                }
                else if (c is CheckBox chk)
                {
                    chk.ForeColor = Colors.TextPrimary;
                }
                else if (c is GroupBox gb)
                {
                    gb.BackColor = Colors.CardBg;
                    gb.ForeColor = Colors.TextPrimary;
                }
                else if (c is Panel pnl && !(c is FlowLayoutPanel) && !(c is TableLayoutPanel))
                {
                    if (pnl.BackColor == SystemColors.Control || pnl.BackColor.GetBrightness() > 0.8f)
                    {
                        pnl.BackColor = Colors.CardBg;
                    }
                }

                if (c.HasChildren)
                {
                    ApplyDarkRecursive(c);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  INTERNAL HELPERS
        // ═══════════════════════════════════════════════════════════════

        private static void PanelResizeHandler(object? sender, EventArgs e)
        {
            if (sender is Panel panel)
            {
                int r = panel.Tag is int rad ? rad : Radius.XL;
                ApplyRoundedRegion(panel, r);
            }
        }

        private static void ApplyRoundedRegion(Panel panel, int radius)
        {
            if (panel.Width <= 0 || panel.Height <= 0) return;
            using GraphicsPath path = CreateRoundedRect(new Rectangle(0, 0, panel.Width, panel.Height), radius);
            panel.Region = new Region(path);
        }

        private static void PanelCardPaint(object? sender, PaintEventArgs e)
        {
            if (sender is not Panel panel) return;
            int r = panel.Tag is int rad ? rad : Radius.XL;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using Pen p = new Pen(Colors.Border, 1f);
            using GraphicsPath path = CreateRoundedRect(new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), r);
            e.Graphics.DrawPath(p, path);
        }

        public static GraphicsPath CreateRoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
        /// <summary>
        /// Shows an app-themed dialog to replace standard Windows MessageBox.
        /// </summary>
        public static void ShowModernDialog(string content, string title = "Thông báo")
        {
            ShowModernDialog(content, title, MessageBoxButtons.OK, MessageBoxIcon.None);
        }

        public static DialogResult ShowModernDialog(
            string content,
            string title,
            MessageBoxButtons buttons,
            MessageBoxIcon icon = MessageBoxIcon.None,
            string? primaryButtonText = null,
            string? secondaryButtonText = null,
            string? cancelButtonText = null)
        {
            using var dialog = new ThemedMessageDialog(content, title, buttons, icon, primaryButtonText, secondaryButtonText, cancelButtonText);
            Form? owner = Form.ActiveForm;
            if (owner != null && !owner.IsDisposed)
            {
                if (owner.TopMost) dialog.TopMost = true;
                return dialog.ShowDialog(owner);
            }
            return dialog.ShowDialog();
        }

        public static void ShowModernDialog(IWin32Window? owner, string content, string title = "Thông báo")
        {
            ShowModernDialog(owner, content, title, MessageBoxButtons.OK, MessageBoxIcon.None);
        }

        public static DialogResult ShowModernDialog(
            IWin32Window? owner,
            string content,
            string title,
            MessageBoxButtons buttons,
            MessageBoxIcon icon = MessageBoxIcon.None)
        {
            using var dialog = new ThemedMessageDialog(content, title, buttons, icon);
            if (owner != null)
            {
                if (owner is Form formOwner && formOwner.TopMost)
                {
                    dialog.TopMost = true;
                }
                return dialog.ShowDialog(owner);
            }

            Form? active = Form.ActiveForm;
            if (active != null && !active.IsDisposed)
            {
                if (active.TopMost) dialog.TopMost = true;
                return dialog.ShowDialog(active);
            }
            return dialog.ShowDialog();
        }

        private enum DialogTone
        {
            Info,
            Warning,
            Error,
            Confirm
        }

        private sealed class ThemedMessageDialog : Form
        {
            private const int CornerRadius = 18;
            private readonly MessageBoxButtons _buttons;
            private readonly MessageBoxIcon _icon;
            private readonly string? _primaryButtonText;
            private readonly string? _secondaryButtonText;
            private readonly string? _cancelButtonText;
            private readonly DialogTone _tone;

            public ThemedMessageDialog(
                string content,
                string title,
                MessageBoxButtons buttons,
                MessageBoxIcon icon,
                string? primaryButtonText = null,
                string? secondaryButtonText = null,
                string? cancelButtonText = null)
            {
                _buttons = buttons;
                _icon = icon;
                _primaryButtonText = primaryButtonText;
                _secondaryButtonText = secondaryButtonText;
                _cancelButtonText = cancelButtonText;
                _tone = ResolveTone(icon, buttons);

                Text = title;
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                BackColor = AppColors.BgCard;
                ForeColor = AppColors.TextPrimary;
                Font = AppFonts.Body;
                DoubleBuffered = true;
                Padding = new Padding(1);
                Tag = "dialog";

                int width = 460;
                int measuredHeight = TextRenderer.MeasureText(
                    content,
                    AppFonts.Body,
                    new Size(width - 150, 0),
                    TextFormatFlags.WordBreak).Height;
                Size = new Size(width, Math.Clamp(measuredHeight + 166, 220, 380));

                Controls.Add(BuildRoot(content, title));
            }

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                ApplyRoundedRegion();
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                ApplyRoundedRegion();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                using var path = GraphicsHelpers.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius);
                using var pen = new Pen(AppColors.BorderStrong, 1.5f);
                e.Graphics.DrawPath(pen, path);
            }

            private Control BuildRoot(string content, string title)
            {
                var root = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    BackColor = AppColors.BgCard,
                    ColumnCount = 1,
                    RowCount = 4,
                    Margin = new Padding(0)
                };
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 4f));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66f));

                root.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = ToneColor, Margin = new Padding(CornerRadius, 0, CornerRadius, 0) }, 0, 0);
                root.Controls.Add(BuildHeader(title), 0, 1);
                root.Controls.Add(BuildBody(content), 0, 2);
                root.Controls.Add(BuildFooter(), 0, 3);
                return root;
            }

            private Control BuildHeader(string title)
            {
                var header = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Transparent,
                    ColumnCount = 2,
                    RowCount = 1,
                    Padding = new Padding(20, 14, 20, 8),
                    Margin = new Padding(0)
                };
                header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40f));
                header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                header.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                header.MouseDown += (_, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                        NativeMethods.ReleaseCaptureAndDrag(Handle);
                };

                var icon = new DialogIconView(_icon, _tone)
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Transparent,
                    Margin = new Padding(0, 0, 10, 0)
                };

                var lblTitle = new Label
                {
                    Text = title,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = AppFonts.Semibold(13f),
                    ForeColor = AppColors.TextPrimary,
                    BackColor = Color.Transparent,
                    AutoEllipsis = true,
                    UseCompatibleTextRendering = false
                };

                header.Controls.Add(icon, 0, 0);
                header.Controls.Add(lblTitle, 1, 0);
                return header;
            }

            private Control BuildBody(string content)
            {
                var body = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    BackColor = AppColors.BgCard,
                    ColumnCount = 2,
                    RowCount = 1,
                    Padding = new Padding(24, 8, 24, 10),
                    Margin = new Padding(0)
                };
                body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86f));

                body.Controls.Add(new Label
                {
                    Text = content,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.TopLeft,
                    Font = AppFonts.Body,
                    ForeColor = AppColors.TextSecondary,
                    BackColor = Color.Transparent,
                    UseCompatibleTextRendering = false
                }, 0, 0);
                body.Controls.Add(BuildToneBadge(), 1, 0);

                return body;
            }

            private Control BuildToneBadge()
            {
                var badge = new RoundedPanel
                {
                    Width = 78,
                    Height = 22,
                    FillColor = ToneSoftColor,
                    BorderColor = Color.Transparent,
                    CornerRadius = 10,
                    Margin = new Padding(8, 2, 0, 0),
                    Anchor = AnchorStyles.Top | AnchorStyles.Right
                };

                badge.Controls.Add(new Label
                {
                    Dock = DockStyle.Fill,
                    Text = ToneLabel,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = AppFonts.Semibold(7.5f),
                    ForeColor = ToneColor,
                    BackColor = Color.Transparent,
                    UseCompatibleTextRendering = false
                });
                return badge;
            }

            private Control BuildFooter()
            {
                var footer = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Transparent,
                    ColumnCount = 1,
                    RowCount = 1,
                    Padding = new Padding(16, 12, 16, 12),
                    Margin = new Padding(0)
                };
                footer.Paint += (_, e) =>
                {
                    using var pen = new Pen(AppColors.Border, 1f);
                    e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
                };

                List<DialogButtonSpec> specs = CreateButtonSpecs();
                footer.ColumnCount = specs.Count;
                footer.ColumnStyles.Clear();
                for (int i = 0; i < specs.Count; i++)
                    footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / specs.Count));

                for (int i = 0; i < specs.Count; i++)
                    footer.Controls.Add(CreateButton(specs[i]), i, 0);

                return footer;
            }

            private List<DialogButtonSpec> CreateButtonSpecs()
            {
                List<DialogButtonSpec> specs = _buttons switch
                {
                    MessageBoxButtons.OK => new()
                    {
                        Primary(DialogResult.OK, LabelOk)
                    },
                    MessageBoxButtons.OKCancel => new()
                    {
                        Secondary(DialogResult.Cancel, LabelCancel, cancel: true),
                        Primary(DialogResult.OK, LabelAgree)
                    },
                    MessageBoxButtons.YesNo => new()
                    {
                        Secondary(DialogResult.No, LabelNo, cancel: true),
                        Primary(DialogResult.Yes, LabelAgree)
                    },
                    MessageBoxButtons.YesNoCancel => new()
                    {
                        Secondary(DialogResult.Cancel, LabelCancel, cancel: true),
                        Secondary(DialogResult.No, LabelNo, cancel: true),
                        Primary(DialogResult.Yes, LabelAgree)
                    },
                    MessageBoxButtons.RetryCancel => new()
                    {
                        Secondary(DialogResult.Cancel, LabelCancel, cancel: true),
                        Primary(DialogResult.Retry, LabelRetry)
                    },
                    MessageBoxButtons.AbortRetryIgnore => new()
                    {
                        Secondary(DialogResult.Abort, LabelAbort),
                        Secondary(DialogResult.Ignore, LabelIgnore),
                        Primary(DialogResult.Retry, LabelRetry)
                    },
                    _ => new()
                    {
                        Primary(DialogResult.OK, LabelOk)
                    }
                };

                ApplyCustomLabels(specs);
                return specs;
            }

            private void ApplyCustomLabels(List<DialogButtonSpec> specs)
            {
                DialogButtonSpec? primary = null;
                DialogButtonSpec? cancel = null;
                DialogButtonSpec? secondary = null;

                foreach (DialogButtonSpec spec in specs)
                {
                    if (spec.Primary)
                        primary ??= spec;
                    else if (spec.Cancel)
                        cancel ??= spec;
                    else
                        secondary ??= spec;
                }

                if (!string.IsNullOrWhiteSpace(_primaryButtonText) && primary != null)
                    primary.Text = _primaryButtonText;
                if (!string.IsNullOrWhiteSpace(_secondaryButtonText))
                {
                    DialogButtonSpec? target = specs.FirstOrDefault(spec => !spec.Primary && spec.Result != DialogResult.Cancel)
                        ?? secondary
                        ?? cancel;
                    if (target != null)
                        target.Text = _secondaryButtonText;
                }
                if (!string.IsNullOrWhiteSpace(_cancelButtonText) && cancel != null)
                    cancel.Text = _cancelButtonText;
            }

            private Button CreateButton(DialogButtonSpec spec)
            {
                var button = new Button
                {
                    Text = spec.Text,
                    DialogResult = spec.Result,
                    Dock = DockStyle.Fill,
                    Height = 34,
                    Margin = new Padding(4, 0, 4, 0),
                    UseVisualStyleBackColor = false,
                    FlatStyle = FlatStyle.Flat,
                    Font = AppFonts.Semibold(9f),
                    Cursor = Cursors.Hand,
                    TabStop = true
                };

                if (spec.Primary)
                    ApplyPrimaryButtonStyle(button);
                else
                    ApplySecondaryButtonStyle(button);

                button.Click += (_, _) =>
                {
                    DialogResult = spec.Result;
                    Close();
                };
                if (spec.Primary)
                    AcceptButton = button;
                if (spec.Cancel || CancelButton == null && spec.Result == DialogResult.No)
                    CancelButton = button;

                RoundedButtonHelper.Apply(button, 8);
                return button;
            }

            private static void ApplyPrimaryButtonStyle(Button button)
            {
                button.BackColor = AppColors.AccentBlue;
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = AppColors.AccentHover;
                button.FlatAppearance.MouseDownBackColor = AppColors.AccentPressed;
            }

            private static void ApplySecondaryButtonStyle(Button button)
            {
                button.BackColor = AppColors.IsDarkMode
                    ? Color.FromArgb(45, 53, 78)
                    : Color.FromArgb(238, 242, 255);
                button.ForeColor = AppColors.AccentBlue;
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = AppColors.IsDarkMode
                    ? Color.FromArgb(55, 64, 95)
                    : Color.FromArgb(224, 231, 255);
                button.FlatAppearance.MouseDownBackColor = AppColors.IsDarkMode
                    ? Color.FromArgb(39, 47, 70)
                    : Color.FromArgb(219, 234, 254);
            }

            private IEnumerable<Button> CreateButtons()
            {
                if (_buttons == MessageBoxButtons.YesNo)
                {
                    Button yes = CreateButton("Đồng ý", DialogResult.Yes, primary: true);
                    Button no = CreateButton("Hủy", DialogResult.No, primary: false);
                    AcceptButton = yes;
                    CancelButton = no;
                    return new[] { yes, no };
                }

                Button ok = CreateButton("Đồng ý", DialogResult.OK, primary: true);
                AcceptButton = ok;
                CancelButton = ok;
                return new[] { ok };
            }

            private Button CreateButton(string text, DialogResult result, bool primary)
            {
                var button = new Button
                {
                    Text = text,
                    DialogResult = result,
                    Width = 112,
                    Height = 38,
                    Margin = new Padding(8, 0, 0, 0),
                    UseVisualStyleBackColor = false
                };

                if (primary)
                    StylePrimaryButton(button);
                else
                    StyleSecondaryButton(button);

                button.Click += (_, _) =>
                {
                    DialogResult = result;
                    Close();
                };
                return button;
            }

            private Color ToneColor => _tone switch
            {
                DialogTone.Warning => AppColors.Warning,
                DialogTone.Error => AppColors.Danger,
                DialogTone.Confirm => AppColors.AccentBlue,
                _ => AppColors.AccentBlue
            };

            private Color ToneSoftColor => _tone switch
            {
                DialogTone.Warning => AppColors.WarningSoft,
                DialogTone.Error => AppColors.DangerSoft,
                DialogTone.Confirm => AppColors.AccentSoft,
                _ => AppColors.AccentSoft
            };

            private string ToneLabel => _tone switch
            {
                DialogTone.Warning => "WARNING",
                DialogTone.Error => "ERROR",
                DialogTone.Confirm => "CONFIRM",
                _ => "INFO"
            };

            private DialogResult CancelDialogResult
            {
                get
                {
                    return _buttons switch
                    {
                        MessageBoxButtons.OK => DialogResult.OK,
                        MessageBoxButtons.YesNo => DialogResult.No,
                        MessageBoxButtons.YesNoCancel => DialogResult.Cancel,
                        MessageBoxButtons.OKCancel => DialogResult.Cancel,
                        MessageBoxButtons.RetryCancel => DialogResult.Cancel,
                        _ => DialogResult.Cancel
                    };
                }
            }

            private static DialogTone ResolveTone(MessageBoxIcon icon, MessageBoxButtons buttons)
            {
                return icon switch
                {
                    MessageBoxIcon.Warning => DialogTone.Warning,
                    MessageBoxIcon.Error => DialogTone.Error,
                    MessageBoxIcon.Question => DialogTone.Confirm,
                    _ when buttons != MessageBoxButtons.OK => DialogTone.Confirm,
                    _ => DialogTone.Info
                };
            }

            private static Color OpaqueOn(Color foreground, Color background)
            {
                if (foreground.A == 255)
                    return foreground;

                int alpha = foreground.A;
                int inverse = 255 - alpha;
                return Color.FromArgb(
                    (foreground.R * alpha + background.R * inverse) / 255,
                    (foreground.G * alpha + background.G * inverse) / 255,
                    (foreground.B * alpha + background.B * inverse) / 255);
            }

            private static DialogButtonSpec Primary(DialogResult result, string text)
            {
                return new DialogButtonSpec(result, text, primary: true, cancel: false);
            }

            private static DialogButtonSpec Secondary(DialogResult result, string text, bool cancel = false)
            {
                return new DialogButtonSpec(result, text, primary: false, cancel);
            }

            private const string LabelOk = "\u0110\u00e3 hi\u1ec3u";
            private const string LabelAgree = "\u0110\u1ed3ng \u00fd";
            private const string LabelNo = "Kh\u00f4ng";
            private const string LabelCancel = "H\u1ee7y";
            private const string LabelRetry = "Th\u1eed l\u1ea1i";
            private const string LabelAbort = "D\u1eebng";
            private const string LabelIgnore = "B\u1ecf qua";

            private void ApplyRoundedRegion()
            {
                if (Width <= 0 || Height <= 0)
                    return;

                Region?.Dispose();
                using var path = GraphicsHelpers.RoundedRect(new Rectangle(-1, -1, Width + 2, Height + 2), CornerRadius + 1);
                Region = new Region(path);
            }

        }

        private sealed class DialogButtonSpec
        {
            public DialogButtonSpec(DialogResult result, string text, bool primary, bool cancel)
            {
                Result = result;
                Text = text;
                Primary = primary;
                Cancel = cancel;
            }

            public DialogResult Result { get; }
            public string Text { get; set; }
            public bool Primary { get; }
            public bool Cancel { get; }
        }

        private sealed class DialogIconView : Control
        {
            private readonly MessageBoxIcon _icon;
            private readonly DialogTone _tone;

            public DialogIconView(MessageBoxIcon icon, DialogTone tone)
            {
                SetStyle(ControlStyles.SupportsTransparentBackColor, true);
                _icon = icon;
                _tone = tone;
                BackColor = Color.Transparent;
                DoubleBuffered = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                Color color = _tone switch
                {
                    DialogTone.Warning => AppColors.Warning,
                    DialogTone.Error => AppColors.Danger,
                    DialogTone.Confirm => AppColors.Danger,
                    _ => AppColors.AccentBlue
                };
                Color soft = _tone switch
                {
                    DialogTone.Warning => AppColors.WarningSoft,
                    DialogTone.Error => AppColors.DangerSoft,
                    DialogTone.Confirm => AppColors.DangerSoft,
                    _ => AppColors.AccentSoft
                };

                Rectangle circle = new Rectangle(1, Math.Max(1, (Height - 30) / 2), 30, 30);
                using SolidBrush bg = new SolidBrush(soft);
                using SolidBrush fg = new SolidBrush(color);
                using Pen pen = new Pen(color, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                e.Graphics.FillEllipse(bg, circle);

                int cx = circle.Left + circle.Width / 2;
                int cy = circle.Top + circle.Height / 2;
                if (_icon == MessageBoxIcon.Error)
                {
                    e.Graphics.DrawLine(pen, cx - 6, cy - 6, cx + 6, cy + 6);
                    e.Graphics.DrawLine(pen, cx + 6, cy - 6, cx - 6, cy + 6);
                }
                else if (_icon == MessageBoxIcon.Question)
                {
                    using Font font = AppFonts.Semibold(12f);
                    e.Graphics.DrawString("?", font, fg, circle, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                }
                else
                {
                    e.Graphics.DrawLine(pen, cx, cy - 7, cx, cy + 2);
                    e.Graphics.FillEllipse(fg, cx - 2, cy + 7, 4, 4);
                }
            }
        }

        private static class NativeMethods
        {
            private const int WmNclButtonDown = 0xA1;
            private const int HtCaption = 0x2;

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            private static extern bool ReleaseCapture();

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

            public static void ReleaseCaptureAndDrag(IntPtr handle)
            {
                ReleaseCapture();
                SendMessage(handle, WmNclButtonDown, HtCaption, 0);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  BACKWARD-COMPAT REDIRECTS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Thin redirect layer — existing code referencing AcademicTheme now gets
    /// dark modern tokens from MetaTheme.
    /// </summary>
    internal static class AcademicTheme
    {
        public static Color AppBackground  => MetaTheme.Colors.FormBg;
        public static Color Surface        => MetaTheme.Colors.CardBg;
        public static Color SurfaceLow     => MetaTheme.Colors.ElevatedBg;
        public static Color Primary        => MetaTheme.Colors.Accent;
        public static Color PrimaryStrong  => MetaTheme.Colors.AccentPressed;
        public static Color TextPrimary    => MetaTheme.Colors.TextPrimary;
        public static Color TextSecondary  => MetaTheme.Colors.TextSecondary;
        public static Color BorderSoft     => MetaTheme.Colors.Border;

        public static void StyleCard(Panel panel, int radius = 12) => MetaTheme.StyleCard(panel, radius);
        public static void StyleGrid(DataGridView grid) => MetaTheme.StyleGrid(grid);
        public static void ShowModernDialog(string content, string title = "Thông báo") => MetaTheme.ShowModernDialog(content, title);
    }
}
