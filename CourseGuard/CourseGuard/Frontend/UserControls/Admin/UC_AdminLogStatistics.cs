using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls.Teacher;

namespace CourseGuard.Frontend.UserControls.Admin
{
    public class UC_AdminLogStatistics : UserControl
    {
        private readonly DashboardController _dashboardController;
        private Label? _totalAccountsValue;
        private Label? _activeAccountsValue;
        private Label? _pendingAccountsValue;
        private Label? _teacherAccountsValue;
        private Label? _studentAccountsValue;
        private DataGridView? _loginStatsGrid;
        private DataGridView? _courseStatsGrid;

        public UC_AdminLogStatistics()
        {
            _dashboardController = new DashboardController(new CourseGuardDbContext(string.Empty));
            BuildLayout();
            LoadStatisticsAsync().FireAndForgetSafe(this);
        }

        private void BuildLayout()
        {
            var root = TeacherTabChrome.CreateRoot(this);
            var title = TeacherTabChrome.CreateHeader(
                "Nhật ký",
                "Theo dõi đăng nhập, tài khoản và thống kê hoạt động hệ thống");

            var summarySection = CreateAccountSummarySection();
            var loginSection = CreateLoginFrequencySection();
            var courseSection = CreateCourseListSection();

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 216F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 320F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 460F));

            summarySection.Margin = new Padding(0, 0, 0, 16);
            loginSection.Margin = new Padding(0, 0, 0, 16);
            courseSection.Margin = Padding.Empty;

            content.Controls.Add(summarySection, 0, 0);
            content.Controls.Add(loginSection, 0, 1);
            content.Controls.Add(courseSection, 0, 2);

            root.Controls.Add(title, 0, 0);
            root.Controls.Add(content, 0, 1);

            AppColors.ApplyTheme(this);
        }

        private Control CreateAccountSummarySection()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1
            };
            for (int i = 0; i < 5; i++)
            {
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            }

            layout.Controls.Add(CreateMetricCard("Tổng", out _totalAccountsValue), 0, 0);
            layout.Controls.Add(CreateMetricCard("Hoạt động", out _activeAccountsValue), 1, 0);
            layout.Controls.Add(CreateMetricCard("Chờ duyệt", out _pendingAccountsValue), 2, 0);
            layout.Controls.Add(CreateMetricCard("Giảng viên", out _teacherAccountsValue), 3, 0);
            layout.Controls.Add(CreateMetricCard("Học viên", out _studentAccountsValue), 4, 0);

            var panel = TeacherTabChrome.CreateDataCard("Tổng quan tài khoản", layout);
            return panel;
        }

        private Control CreateLoginFrequencySection()
        {
            _loginStatsGrid = CreateGrid();
            _loginStatsGrid.Columns.Add("LoginDate", "Ngày");
            _loginStatsGrid.Columns.Add("LoginCount", "Số lượt đăng nhập");
            
            TeacherTabChrome.StyleGrid(_loginStatsGrid);
            var panel = TeacherTabChrome.CreateDataCard("Tần suất đăng nhập (14 ngày gần nhất)", _loginStatsGrid);
            return panel;
        }

        private Control CreateCourseListSection()
        {
            _courseStatsGrid = CreateGrid();
            _courseStatsGrid.Columns.Add("CourseId", "Mã khóa học");
            _courseStatsGrid.Columns.Add("CourseName", "Tên khóa học");
            _courseStatsGrid.Columns.Add("TeacherName", "Giảng viên");
            _courseStatsGrid.Columns.Add("Status", "Trạng thái");
            _courseStatsGrid.Columns.Add("EnrollmentCount", "Số lượt ghi danh");
            
            TeacherTabChrome.StyleGrid(_courseStatsGrid);
            var panel = TeacherTabChrome.CreateDataCard("Thống kê danh sách khóa học", _courseStatsGrid);
            return panel;
        }

        private static Control CreateMetricCard(string title, out Label valueLabel)
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(6),
                Padding = new Padding(12),
                FillColor = AppColors.BgCard,
                CornerRadius = 12,
                Tag = "card"
            };

            var titleLabel = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 26,
                Font = MetaTheme.Fonts.BodySmBold(),
                ForeColor = AppColors.TextSecondary
            };

            valueLabel = new Label
            {
                Text = "0",
                Dock = DockStyle.Fill,
                Font = AppFonts.Semibold(19F),
                ForeColor = AppColors.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            card.Controls.Add(valueLabel);
            card.Controls.Add(titleLabel);
            return card;
        }

        private static DataGridView CreateGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = AppColors.BgCard,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = MetaTheme.Fonts.BodySm(),
                EnableHeadersVisualStyles = false
            };

            grid.ColumnHeadersDefaultCellStyle.Font = MetaTheme.Fonts.BodySmBold();
            grid.ColumnHeadersHeight = 30;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.RowTemplate.Height = 30;

            return grid;
        }

        private async Task LoadStatisticsAsync()
        {
            this.ShowSkeleton(SkeletonType.DashboardOverview);
            try
            {
                var summaryTask = Task.Run(() => _dashboardController.GetAccountSummaryStatistics());
                var frequencyTask = Task.Run(() => _dashboardController.GetLoginFrequencyStatistics(14));
                var courseTask = Task.Run(() => _dashboardController.GetCourseListStatistics(100));

                await Task.WhenAll(summaryTask, frequencyTask, courseTask);
                if (IsDisposed || Disposing)
                {
                    return;
                }

                BindAccountSummary(await summaryTask);
                BindLoginFrequency(await frequencyTask);
                BindCourseList(await courseTask);
            }
            catch (Exception ex)
            {
                if (_loginStatsGrid != null)
                {
                    _loginStatsGrid.Rows.Clear();
                    _loginStatsGrid.Rows.Add("Lỗi", ex.Message);
                }

                if (_courseStatsGrid != null)
                {
                    _courseStatsGrid.Rows.Clear();
                    _courseStatsGrid.Rows.Add("Lỗi", ex.Message, "-", "-", "-");
                }
            }
            finally
            {
                this.HideSkeleton();
            }
        }

        private void BindAccountSummary(AccountSummaryModel summary)
        {
            _totalAccountsValue?.SetTextSafely(summary.TotalAccounts.ToString("N0"));
            _activeAccountsValue?.SetTextSafely(summary.ActiveAccounts.ToString("N0"));
            _pendingAccountsValue?.SetTextSafely(summary.PendingAccounts.ToString("N0"));
            _teacherAccountsValue?.SetTextSafely(summary.TeacherAccounts.ToString("N0"));
            _studentAccountsValue?.SetTextSafely(summary.StudentAccounts.ToString("N0"));
        }

        private void BindLoginFrequency(System.Collections.Generic.List<LoginFrequencyModel> frequencyRows)
        {
            if (_loginStatsGrid == null)
            {
                return;
            }

            _loginStatsGrid.Rows.Clear();
            foreach (var row in frequencyRows)
            {
                _loginStatsGrid.Rows.Add(row.LoginDate.ToString("dd/MM/yyyy"), row.LoginCount);
            }

            if (frequencyRows.Count == 0)
            {
                _loginStatsGrid.Rows.Add("-", 0);
            }
        }

        private void BindCourseList(System.Collections.Generic.List<CourseListItemModel> courseRows)
        {
            if (_courseStatsGrid == null)
            {
                return;
            }

            _courseStatsGrid.Rows.Clear();
            foreach (var row in courseRows)
            {
                _courseStatsGrid.Rows.Add(
                    row.CourseId,
                    row.CourseName,
                    row.TeacherName,
                    row.Status,
                    row.EnrollmentCount);
            }

            if (courseRows.Count == 0)
            {
                _courseStatsGrid.Rows.Add("-", "Không có khóa học", "-", "-", 0);
            }
        }


    }
}
