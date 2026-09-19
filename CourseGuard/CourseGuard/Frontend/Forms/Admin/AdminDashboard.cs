/*
 * AdminDashboard.cs
 * 
 * Layer: Presentation (Forms)
 * Layout: Sidebar(Left) → RightPanel(Fill) → Topbar(Top) + mainboard(Fill)
 * 
 * UI/UX Rules applied:
 *   Rule 01 (Layout Skeleton) — Sidebar fixed left, Header top, Content fills rest
 *   Rule 09 — No pure black/white
 *   Rule 40 — Single accent color in navigation
 *   Rule 46 — Short navigation labels (1-2 words)
 *   Rule 05 — Visual hierarchy
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.UserControls.Admin;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.Forms.Admin
{
    public partial class AdminDashboard : Form
    {
        private Dictionary<string, Func<UserControl>> _nav = new();
        private UserModel? currentUser;
        private SidebarPanel _sidebar;
        private TopbarPanel _topbar;
        private Panel _rightPanel;   // container: topbar + mainboard
        private string _currentPageName = "Tổng quan";

        public AdminDashboard()
        {
            InitializeComponent();
            SearchFocusManager.Install(this);

            // ── 1. Build layout skeleton (Skill 01) ──────────────────
            // Sidebar docks Left on Form
            _sidebar = new SidebarPanel { Dock = DockStyle.Left };
            _sidebar.SetNavItems(new[]
            {
                new SidebarNavItem("Tổng quan", string.Empty, isHeading: true),
                new SidebarNavItem("Tổng quan", "home"),
                new SidebarNavItem("Quản lý", string.Empty, isHeading: true),
                new SidebarNavItem("Người dùng", "user"),
                new SidebarNavItem("Khóa học", "folder-check"),
                new SidebarNavItem("Phân tích", string.Empty, isHeading: true),
                new SidebarNavItem("Báo cáo", "chart"),
                new SidebarNavItem("Thiết bị", "monitor"),
                new SidebarNavItem("Hệ thống", string.Empty, isHeading: true),
                new SidebarNavItem("Nhật ký", "document"),
                new SidebarNavItem("Cài đặt", "settings")
            });
            _sidebar.NavItemClicked += Sidebar_NavItemClicked;

            // Right container holds Topbar(Top) + mainboard(Fill)
            _rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 0) };
            _topbar = new TopbarPanel
            {
                Dock = DockStyle.Top,
                PageTitle = "Tổng quan",
                Subtitle = string.Empty,
                UseStudentTopbar = true
            };
            _topbar.ThemeToggled += (_, _) => ReloadCurrentPage();
            _topbar.LogoutRequested += (_, _) => Sidebar_NavItemClicked(this, "Logout");

            // mainboard (from Designer) fills remaining space under topbar
            mainboard.Dock = DockStyle.Fill;

            // Assemble: topbar on top, mainboard fills below
            _rightPanel.Controls.Add(mainboard);   // Fill goes first in Controls
            _rightPanel.Controls.Add(_topbar);      // Top docks above Fill

            // Sidebar on the left, right panel fills the rest
            this.Controls.Add(_rightPanel);
            this.Controls.Add(_sidebar);

            // ── 2. Apply theme (Rule 09: no pure white/black) ────────
            BackColor = AppColors.BgBase;
            _rightPanel.BackColor = AppColors.BgBase;
            mainboard.BackColor = AppColors.BgBase;

            // ── 3. Navigation logic ──────────────────────────────────
            InitializeNavigation();
            LoadUI(_nav["Tổng quan"]());
        }

        public AdminDashboard(UserModel user) : this()
        {
            this.currentUser = user;
            this.Text = $"Admin Dashboard - {user.Username}";
            _topbar.UserName = user.Username ?? "Admin";
        }

        private void InitializeNavigation()
        {
            var adminOverview = new Func<UserControl>(() =>
            {
                var uc = new UC_AdminDashboard();
                uc.QuickActionRequested += AdminOverview_QuickActionRequested;
                return uc;
            });

            _nav = new Dictionary<string, Func<UserControl>>
            {
                { "Tổng quan", adminOverview },
                { "Người dùng", () => new UC_UsersManage() },
                { "Khóa học", () => new UC_CoursesManage() },
                { "Báo cáo", () => new UC_AdminReports() },
                { "Thiết bị", () => new UC_DevicesManage() },
                { "Nhật ký", () => new UC_AdminLogStatistics() },
                { "Cài đặt", () => new UC_SystemSettings() }
            };
        }

        private void Sidebar_NavItemClicked(object? sender, string pageName)
        {
            if (pageName == "Logout")
            {
                if (!LogoutConfirmation.Confirm())
                    return;

                var authService = new CourseGuard.Backend.Controllers.AuthController(
                    new CourseGuard.Backend.Data.CourseGuardDbContext(""));
                Task.Run(() => authService.Logout(currentUser?.Id, currentUser?.Username ?? string.Empty));
                this.DialogResult = DialogResult.OK;
                this.Close();
                return;
            }

            if (_nav.ContainsKey(pageName))
            {
                _currentPageName = pageName;
                _topbar.PageTitle = pageName;
                LoadUI(_nav[pageName]());
            }
        }

        private void AdminOverview_QuickActionRequested(string target)
        {
            string? targetPage = target switch
            {
                "USERS"   => "Người dùng",
                "COURSES" => "Khóa học",
                "REPORTS" => "Báo cáo",
                "AUDIT"   => "Nhật ký",
                _         => null
            };

            if (targetPage != null && _nav.ContainsKey(targetPage))
            {
                _currentPageName = targetPage;
                _sidebar.SetActiveByName(targetPage);
                _topbar.PageTitle = targetPage;
                LoadUI(_nav[targetPage]());
            }
        }

        private void ReloadCurrentPage()
        {
            BackColor = AppColors.BgBase;
            _rightPanel.BackColor = AppColors.BgBase;
            mainboard.BackColor = AppColors.BgBase;
            _topbar.BackColor = AppColors.BgCard;
            _topbar.PageTitle = _currentPageName;
            _sidebar.SetActiveByName(_currentPageName);

            if (_nav.TryGetValue(_currentPageName, out var factory))
                LoadUI(factory());

            _sidebar.Invalidate();
            _topbar.Invalidate();
        }

        private void LoadUI(UserControl uc)
        {
            // Dispose old UC to prevent memory leak
            foreach (Control ctrl in mainboard.Controls)
            {
                ctrl.Dispose();
            }
            mainboard.Controls.Clear();

            uc.Dock = DockStyle.Fill;
            uc.BackColor = AppColors.BgBase;
            mainboard.Controls.Add(uc);

            AppColors.ApplyTheme(uc);
        }

        private static UserControl CreateEmptyView(string title)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.BgBase
            };

            var label = new Label
            {
                Dock = DockStyle.Fill,
                Text = $"{title}\n(Chưa có logic)",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = AppFonts.Title,
                ForeColor = AppColors.TextMuted
            };

            panel.Controls.Add(label);
            var uc = new UserControl { Dock = DockStyle.Fill, BackColor = AppColors.BgBase };
            uc.Controls.Add(panel);
            return uc;
        }
    }
}
