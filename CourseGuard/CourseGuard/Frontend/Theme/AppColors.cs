/*
 * AppColors.cs
 *
 * Layer: Presentation (Theme)
 * Centralized color token registry for the dashboard design system.
 * Supports Dark / Light mode toggle with a single boolean flip.
 *
 * Usage:
 *   AppColors.BgBase          → current-mode background
 *   AppColors.IsDarkMode      → read/write toggle
 *   AppColors.ApplyTheme(ctrl)→ recursive re-paint
 *
 * All tokens derived from the Yann UI/UX dashboard reference design.
 */

using System;
using System.Drawing;
using System.Windows.Forms;

namespace CourseGuard.Frontend.Theme
{
    /// <summary>
    /// Design-system color tokens with Dark/Light mode support.
    /// Every public property resolves through <see cref="IsDarkMode"/>.
    /// </summary>
    public static class AppColors
    {
        // ═══════════════════════════════════════════════════════════════
        //  MODE TOGGLE
        // ═══════════════════════════════════════════════════════════════

        private static bool _isDarkMode = true;

        /// <summary>Gets or sets the current theme mode. Changing this
        /// does NOT automatically repaint — call <see cref="ApplyTheme"/>.</summary>
        public static bool IsDarkMode
        {
            get => _isDarkMode;
            set => _isDarkMode = value;
        }

        // ═══════════════════════════════════════════════════════════════
        //  DARK PALETTE (constants — never change at runtime)
        // ═══════════════════════════════════════════════════════════════

        private static class Dark
        {
            // Surfaces
            public static readonly Color BgBase       = ColorTranslator.FromHtml("#16161A");
            public static readonly Color BgCard       = ColorTranslator.FromHtml("#1E1E2E");
            public static readonly Color BgCardHover  = ColorTranslator.FromHtml("#252535");
            public static readonly Color BgSidebar    = ColorTranslator.FromHtml("#131320");
            public static readonly Color BgInput      = ColorTranslator.FromHtml("#191930");
            public static readonly Color BgElevated   = ColorTranslator.FromHtml("#1C1C30");

            // Text
            public static readonly Color TextPrimary  = ColorTranslator.FromHtml("#F1F5F9");
            public static readonly Color TextSecondary= ColorTranslator.FromHtml("#94A3B8");
            public static readonly Color TextMuted    = ColorTranslator.FromHtml("#475569");

            // Borders
            public static readonly Color Border       = Color.FromArgb(20, 255, 255, 255);
            public static readonly Color BorderStrong = Color.FromArgb(40, 255, 255, 255);
            public static readonly Color GridBorder   = Color.FromArgb(40, 40, 55);

            // Chart helpers
            public static readonly Color ChartFill    = Color.FromArgb(40, 59, 130, 246);
            public static readonly Color GridLine     = Color.FromArgb(15, 255, 255, 255);
            public static readonly Color BarInactive  = ColorTranslator.FromHtml("#252535");
            public static readonly Color HeatmapOff   = ColorTranslator.FromHtml("#252535");
            public static readonly Color ProgressTrack= ColorTranslator.FromHtml("#16161A");
        }

        // ═══════════════════════════════════════════════════════════════
        //  LIGHT PALETTE
        // ═══════════════════════════════════════════════════════════════

        private static class Light
        {
            // Surfaces
            public static readonly Color BgBase       = ColorTranslator.FromHtml("#F1F5F9");
            public static readonly Color BgCard       = ColorTranslator.FromHtml("#FCFCFD");
            public static readonly Color BgCardHover  = ColorTranslator.FromHtml("#F8FAFC");
            public static readonly Color BgSidebar    = ColorTranslator.FromHtml("#2563EB");
            public static readonly Color BgInput      = ColorTranslator.FromHtml("#F8FAFC");
            public static readonly Color BgElevated   = ColorTranslator.FromHtml("#F8FAFC");

            // Text
            public static readonly Color TextPrimary  = ColorTranslator.FromHtml("#0F172A");
            public static readonly Color TextSecondary= ColorTranslator.FromHtml("#64748B");
            public static readonly Color TextMuted    = ColorTranslator.FromHtml("#94A3B8");

            // Borders
            public static readonly Color Border       = ColorTranslator.FromHtml("#E2E8F0");
            public static readonly Color BorderStrong = ColorTranslator.FromHtml("#CBD5E1");
            public static readonly Color GridBorder   = ColorTranslator.FromHtml("#E2E8F0");

            // Chart helpers
            public static readonly Color ChartFill    = Color.FromArgb(25, 59, 130, 246);
            public static readonly Color GridLine     = Color.FromArgb(20, 0, 0, 0);
            public static readonly Color BarInactive  = ColorTranslator.FromHtml("#E2E8F0");
            public static readonly Color HeatmapOff   = ColorTranslator.FromHtml("#F1F5F9");
            public static readonly Color ProgressTrack= ColorTranslator.FromHtml("#E2E8F0");
        }

        // ═══════════════════════════════════════════════════════════════
        //  SHARED (mode-invariant) accent & status colors
        // ═══════════════════════════════════════════════════════════════

        // Accent: richer in light mode, calmer in dark mode.
        public static Color AccentBlue     => _isDarkMode ? ColorTranslator.FromHtml("#60A5FA") : ColorTranslator.FromHtml("#2563EB");
        public static Color AccentHover    => _isDarkMode ? ColorTranslator.FromHtml("#3B82F6") : ColorTranslator.FromHtml("#1D4ED8");
        public static Color AccentPressed  => _isDarkMode ? ColorTranslator.FromHtml("#2563EB") : ColorTranslator.FromHtml("#1E40AF");
        public static Color AccentSoft     => _isDarkMode ? Color.FromArgb(36, 96, 165, 250) : Color.FromArgb(28, 37, 99, 235);

        // Status
        public static readonly Color Success        = ColorTranslator.FromHtml("#22C55E");
        public static readonly Color SuccessSoft    = Color.FromArgb(25, 34, 197, 94);
        public static readonly Color Warning        = ColorTranslator.FromHtml("#F59E0B");
        public static readonly Color WarningSoft    = Color.FromArgb(25, 245, 158, 11);
        public static readonly Color Danger         = ColorTranslator.FromHtml("#EF4444");
        public static readonly Color DangerSoft     = Color.FromArgb(25, 239, 68, 68);

        // Star rating
        public static readonly Color StarFilled     = ColorTranslator.FromHtml("#F59E0B");
        public static readonly Color StarEmpty      = ColorTranslator.FromHtml("#475569");

        // ═══════════════════════════════════════════════════════════════
        //  DYNAMIC PROPERTIES — resolve via IsDarkMode
        // ═══════════════════════════════════════════════════════════════

        // ── Surfaces ────────────────────────────────────────────────
        public static Color BgBase       => _isDarkMode ? Dark.BgBase       : Light.BgBase;
        public static Color BgCard       => _isDarkMode ? Dark.BgCard       : Light.BgCard;
        public static Color BgCardHover  => _isDarkMode ? Dark.BgCardHover  : Light.BgCardHover;
        public static Color BgSidebar    => _isDarkMode ? Dark.BgSidebar    : Light.BgSidebar;
        public static Color BgInput      => _isDarkMode ? Dark.BgInput      : Light.BgInput;
        public static Color BgElevated   => _isDarkMode ? Dark.BgElevated   : Light.BgElevated;

        // ── Text ────────────────────────────────────────────────────
        public static Color TextPrimary  => _isDarkMode ? Dark.TextPrimary  : Light.TextPrimary;
        public static Color TextSecondary=> _isDarkMode ? Dark.TextSecondary: Light.TextSecondary;
        public static Color TextMuted    => _isDarkMode ? Dark.TextMuted    : Light.TextMuted;

        // ── Borders ─────────────────────────────────────────────────
        public static Color Border       => _isDarkMode ? Dark.Border       : Light.Border;
        public static Color BorderStrong => _isDarkMode ? Dark.BorderStrong : Light.BorderStrong;
        public static Color GridBorder   => _isDarkMode ? Dark.GridBorder   : Light.GridBorder;

        // ── Chart / Data-viz ────────────────────────────────────────
        public static Color ChartLine    => AccentBlue;                                        // Always blue
        public static Color ChartFill    => _isDarkMode ? Dark.ChartFill    : Light.ChartFill;
        public static Color GridLine     => _isDarkMode ? Dark.GridLine     : Light.GridLine;
        public static Color BarActive    => AccentBlue;
        public static Color BarInactive  => _isDarkMode ? Dark.BarInactive  : Light.BarInactive;
        public static Color HeatmapOn   => AccentBlue;
        public static Color HeatmapOff  => _isDarkMode ? Dark.HeatmapOff   : Light.HeatmapOff;
        public static Color ProgressTrack=> _isDarkMode ? Dark.ProgressTrack: Light.ProgressTrack;

        // ── Sidebar-specific tokens ─────────────────────────────────
        public static Color SidebarTextPrimary => _isDarkMode
            ? TextPrimary
            : Color.White;
        public static Color SidebarTextSecondary => _isDarkMode
            ? TextSecondary
            : ColorTranslator.FromHtml("#DBEAFE");
        public static Color SidebarTextMuted => _isDarkMode
            ? TextMuted
            : ColorTranslator.FromHtml("#BFDBFE");
        public static Color SidebarHeadingText => _isDarkMode
            ? ColorTranslator.FromHtml("#E0F2FE")
            : Color.White;
        public static Color SidebarHeadingAccent => _isDarkMode
            ? AccentBlue
            : Color.White;
        public static Color SidebarHeadingBg => _isDarkMode
            ? Color.FromArgb(28, 96, 165, 250)
            : Color.FromArgb(36, 255, 255, 255);
        public static Color SidebarItemHover => _isDarkMode
            ? BgCardHover
            : Color.FromArgb(38, 255, 255, 255);
        public static Color SidebarItemActive => _isDarkMode
            ? AccentSoft
            : AccentHover;
        public static Color SidebarActiveIndicator => _isDarkMode
            ? AccentBlue
            : Color.White;
        public static Color SidebarIconActive => _isDarkMode
            ? AccentBlue
            : Color.White;
        public static Color SidebarLogoutText => _isDarkMode
            ? TextSecondary
            : ColorTranslator.FromHtml("#DBEAFE");
        public static Color SidebarLogoutHoverText => _isDarkMode
            ? Danger
            : Color.White;
        public static Color SidebarLogoutHoverBg => _isDarkMode
            ? BgCardHover
            : Color.FromArgb(42, 255, 255, 255);

        private static readonly Color TransparentBackColor = Color.Transparent;

        // ═══════════════════════════════════════════════════════════════
        //  THEME APPLICATION
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Recursively repaint all controls in a container to match the
        /// current <see cref="IsDarkMode"/> setting. Call after toggling mode.
        /// </summary>
        public static void ApplyTheme(Control root)
        {
            ApplyThemeRecursive(root);
            root.Invalidate(true);
        }

        private static void ApplyThemeRecursive(Control root)
        {
            ApplyControlTheme(root);

            foreach (Control c in root.Controls)
            {
                ApplyThemeRecursive(c);
            }
        }

        private static void ApplyControlTheme(Control c)
        {
            SmoothControlFont(c);

            if (c.Tag?.ToString() == "custom")
            {
                c.Invalidate();
                return;
            }

            if (c is SidebarPanel sp)
            {
                sp.BackColor = BgSidebar;
                sp.Invalidate();
                return;
            }

            if (c is TopbarPanel tp)
            {
                tp.BackColor = BgCard;
                tp.Invalidate();
                return;
            }

            if (c is StatCard statCard)
            {
                SetTransparentBackColor(statCard);
                statCard.Invalidate();
                return;
            }

            if (c is SearchBoxPanel searchBox)
            {
                searchBox.ApplyTheme();
                return;
            }

            if (c is Form f)
            {
                f.BackColor = BgBase;
                f.ForeColor = TextPrimary;
            }
            else if (c is DataGridView dgv)
            {
                ApplyGridTheme(dgv);
            }
            else if (c is ListBox lb)
            {
                // ── Activities log panel fix ──────────────────────
                lb.BackColor = BgCard;
                lb.ForeColor = TextPrimary;
                lb.BorderStyle = BorderStyle.None;
                lb.Invalidate();
            }
            else if (c is ComboBox cb)
            {
                if (cb.Tag?.ToString() == "sidebar-course-selector")
                    StudentDropdownStyler.StyleComboBox(cb, true, displayTextOverride: "Khóa học", useSidebarChrome: true);
                else
                    StudentDropdownStyler.StyleComboBox(cb, true, cb.Tag?.ToString() == "card-input");
            }
            else if (c is FlowLayoutPanel flp)
            {
                if (flp.Tag?.ToString() != "custom")
                    SetTransparentBackColor(flp);
            }
            else if (c is TableLayoutPanel tlp)
            {
                SetTransparentBackColor(tlp);
            }
            else if (c is Panel pnl)
            {
                if (pnl.Tag?.ToString() == "custom")
                    return;

                if (pnl.Tag?.ToString() == "card")
                    pnl.BackColor = BgCard;
                else if (pnl.Dock == DockStyle.Fill || pnl.Tag?.ToString() == "base")
                    pnl.BackColor = BgBase;
                else
                    pnl.BackColor = BgCard;
                pnl.Invalidate();
            }
            else if (c is Label lbl)
            {
                SetTransparentBackColor(lbl);
                lbl.ForeColor = lbl.Font.Bold || lbl.Font.Size >= 12f
                    ? TextPrimary
                    : TextSecondary;
            }
            else if (c is TextBox tb)
            {
                tb.BackColor = tb.Tag?.ToString() == "card-input" ? BgCard : BgInput;
                tb.ForeColor = TextPrimary;
            }
            else if (c is RichTextBox rtb)
            {
                rtb.BackColor = BgInput;
                rtb.ForeColor = TextPrimary;
            }
            else if (c is Button btn)
            {
                ApplyButtonTheme(btn);
            }
            else if (c is GroupBox gb)
            {
                gb.BackColor = BgCard;
                gb.ForeColor = gb.Tag is Color accent ? accent : TextPrimary;
            }
            else if (c is UserControl uc)
            {
                uc.BackColor = BgBase;
                uc.ForeColor = TextPrimary;
                uc.Invalidate();
            }
        }

        private static void SmoothControlFont(Control c)
        {
            if (!c.Font.Bold)
                return;

            c.Font = AppFonts.Semibold(c.Font.SizeInPoints);
        }

        /// <summary>
        /// Unified button styling — 3 variants:
        ///   Primary (AccentBlue bg) → keep accent, update hover
        ///   Danger  (Red bg)        → keep danger, update hover
        ///   Secondary (everything else) → card bg, visible border, proper text
        /// </summary>
        private static void ApplyButtonTheme(Button btn)
        {
            // Always enforce Flat appearance, otherwise borders/colors are ignored
            btn.FlatStyle = FlatStyle.Flat;
            btn.UseVisualStyleBackColor = false;
            btn.TextAlign = ContentAlignment.MiddleCenter;
            SmoothControlFont(btn);

            // ── Action buttons with accent text color (stored in Tag) ──
            if (btn.Tag is Color accentColor)
            {
                btn.BackColor = BgCard;
                btn.ForeColor = accentColor;   // Preserve the unique accent
                btn.FlatAppearance.BorderColor = IsDarkMode
                    ? Color.FromArgb(50, 50, 70)
                    : Color.FromArgb(203, 213, 225);
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.MouseOverBackColor = BgCardHover;
                btn.FlatAppearance.MouseDownBackColor = BgElevated;
                btn.Cursor = Cursors.Hand;
                return;
            }

            string? variant = btn.Tag as string;
            if (variant == "primary")
            {
                btn.BackColor = AccentBlue;
                btn.ForeColor = Color.White;
                btn.FlatAppearance.BorderColor = AccentHover;
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.MouseOverBackColor = AccentHover;
                btn.FlatAppearance.MouseDownBackColor = AccentPressed;
                btn.Cursor = Cursors.Hand;
                return;
            }

            if (variant == "secondary")
            {
                btn.BackColor = IsDarkMode ? BgCardHover : ColorTranslator.FromHtml("#F8FAFC");
                btn.ForeColor = TextPrimary;
                btn.FlatAppearance.BorderColor = BorderStrong;
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.MouseOverBackColor = IsDarkMode ? BgElevated : ColorTranslator.FromHtml("#EEF2F7");
                btn.FlatAppearance.MouseDownBackColor = BgElevated;
                btn.Cursor = Cursors.Hand;
                return;
            }

            if (variant == "success")
            {
                btn.ForeColor = Color.White;
                btn.BackColor = Success;
                btn.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#16A34A");
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#16A34A");
                btn.FlatAppearance.MouseDownBackColor = ColorTranslator.FromHtml("#15803D");
                btn.Cursor = Cursors.Hand;
                return;
            }

            if (variant == "danger")
            {
                btn.ForeColor = Color.White;
                btn.BackColor = Danger;
                btn.FlatAppearance.BorderColor = Color.FromArgb(220, 38, 38);
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 38, 38);
                btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(185, 28, 28);
                btn.Cursor = Cursors.Hand;
                return;
            }

            // Detect button variant by current BackColor
            bool isPrimary = (btn.BackColor == AccentBlue || btn.BackColor == AccentHover || btn.BackColor == AccentPressed);
            bool isDanger  = (btn.BackColor == Danger || btn.BackColor == Color.FromArgb(220, 38, 38) || btn.BackColor == Color.FromArgb(239, 68, 68));

            if (isPrimary)
            {
                btn.BackColor = AccentBlue;
                btn.ForeColor = Color.FromArgb(255, 255, 255);
                btn.FlatAppearance.BorderColor = AccentHover;
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.MouseOverBackColor = AccentHover;
                btn.FlatAppearance.MouseDownBackColor = AccentPressed;
            }
            else if (isDanger)
            {
                btn.ForeColor = Color.FromArgb(255, 255, 255);
                btn.BackColor = Color.FromArgb(239, 68, 68); // EF4444 (Red-500)
                btn.FlatAppearance.BorderColor = Color.FromArgb(220, 38, 38);
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 38, 38);
            }
            else
            {
                // Secondary: visible bg + border in BOTH modes
                btn.BackColor = IsDarkMode ? BgCard : Color.FromArgb(241, 245, 249); // slate-100 in light
                btn.ForeColor = TextPrimary;
                btn.FlatAppearance.BorderColor = IsDarkMode
                    ? Color.FromArgb(50, 50, 70)
                    : Color.FromArgb(203, 213, 225); // slate-300
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.MouseOverBackColor = IsDarkMode ? BgCardHover : Color.FromArgb(226, 232, 240); // slate-200 in light
                btn.FlatAppearance.MouseDownBackColor = BgElevated;
            }

            btn.Cursor = Cursors.Hand;
        }

        private static void ApplyGridTheme(DataGridView dgv)
        {
            DashboardGridStyler.Apply(dgv);
        }

        private static void SetTransparentBackColor(Control control)
        {
            control.BackColor = SupportsTransparentBackground(control)
                ? TransparentBackColor
                : ResolveConcreteBackColor(control);
        }

        private static bool SupportsTransparentBackground(Control control)
        {
            return control is not TextBoxBase
                && control is not ComboBox
                && control is not ListBox
                && control is not ListView
                && control is not TreeView
                && control is not DataGridView
                && control is not DateTimePicker
                && control is not MonthCalendar
                && control is not NumericUpDown;
        }

        private static Color ResolveConcreteBackColor(Control control)
        {
            Control? parent = control.Parent;
            while (parent != null)
            {
                if (parent.BackColor != TransparentBackColor)
                    return parent.BackColor;

                parent = parent.Parent;
            }

            return BgBase;
        }
    }
}
