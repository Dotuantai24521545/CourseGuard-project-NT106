using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Frontend.Forms.Student;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Student
{
    public partial class UC_Notification : UserControl, IStudentSearchTarget
    {
        private readonly NotificationRepository _repository = new();
        private DataTable? _notificationTable;
        private string _globalSearchKeyword = string.Empty;
        private RoundedPanel _notificationBody = null!;
        private Label _emptyStateLabel = null!;
        private readonly HashSet<int> _markingReadIds = new();

        public UC_Notification()
        {
            InitializeComponent();
            BuildCardLayout();
            ApplyAcademicStyle();
            btnMarkAsRead.Click += async (_, _) => await MarkSelectedNotificationAsRead();
            dgvNotifications.CellClick += async (_, e) => await MarkClickedNotificationAsRead(e.RowIndex);
            LoadDataAsync().FireAndForgetSafe(this);

            RoundedButtonHelper.Apply(btnMarkAsRead, 10);
        }

        private void ApplyAcademicStyle()
        {
            BackColor = AppColors.BgBase;
            StudentTabChrome.StylePrimaryButton(btnMarkAsRead);
            StudentTabChrome.StyleGrid(dgvNotifications);
            ApplyNotificationBodyTheme(dgvNotifications.Visible);
        }

        private void BuildCardLayout()
        {
            btnMarkAsRead.Text = "Đánh dấu đã đọc";
            var root = StudentTabChrome.CreateRoot(this);
            root.Controls.Add(StudentTabChrome.CreateHeader(
                "Thông báo",
                "Các nhắc lịch, bài kiểm tra, tài liệu mới và phản hồi từ hệ thống.",
                btnMarkAsRead), 0, 0);

            _notificationBody = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                FillColor = AppColors.BgCard,
                BorderColor = Color.Transparent,
                CornerRadius = 12,
                Padding = Padding.Empty
            };

            _emptyStateLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                BackColor = AppColors.IsDarkMode ? AppColors.BgCardHover : ColorTranslator.FromHtml("#F8FAFC"),
                ForeColor = AppColors.TextMuted,
                Font = AppFonts.Body,
                Text = "Chưa có thông báo gần đây",
                TextAlign = ContentAlignment.MiddleCenter,
                UseCompatibleTextRendering = false,
                Visible = false
            };

            dgvNotifications.Dock = DockStyle.Fill;
            dgvNotifications.Margin = Padding.Empty;
            _notificationBody.Controls.Add(dgvNotifications);
            _notificationBody.Controls.Add(_emptyStateLabel);

            root.Controls.Add(StudentTabChrome.CreateDataCard("Thông báo gần đây", _notificationBody), 0, 1);
            StudentTabChrome.EnableNaturalFocusClear(this, dgvNotifications);
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            this.ShowSkeleton(SkeletonType.NotificationList);
            try
            {
                _notificationTable = await System.Threading.Tasks.Task.Run(LoadNotificationTable);
                BindNotificationTable(FilterNotificationTable(_notificationTable));
            }
            catch (Exception ex)
            {
                _notificationTable = CreateNotificationTableSchema();
                BindNotificationTable(_notificationTable, $"Không thể tải thông báo: {ex.Message}");
            }
            finally
            {
                this.HideSkeleton();
            }
        }

        public System.Threading.Tasks.Task RefreshAsync()
        {
            return LoadDataAsync();
        }

        private DataTable LoadNotificationTable()
        {
            int userId = UserSessionContext.CurrentUserId ?? 0;
            if (userId <= 0)
                return CreateNotificationTableSchema();

            var notifications = _repository
                .LoadByUserId(userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToList();

            DataTable dt = CreateNotificationTableSchema();
            foreach (NotificationModel notification in notifications)
            {
                dt.Rows.Add(
                    notification.Id,
                    notification.IsRead,
                    SystemTimeFormatter.FormatVietnamTime(notification.CreatedAt),
                    notification.Title,
                    notification.Content,
                    notification.IsRead ? "Đã đọc" : "Chưa đọc");
            }

            return dt;
        }

        private void BindNotificationTable(DataTable table, string emptyMessage = "Chưa có thông báo gần đây")
        {
            if (table.Rows.Count == 0)
            {
                ShowEmptyState(emptyMessage);
                return;
            }

            dgvNotifications.DataSource = table;
            dgvNotifications.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            HideInternalColumn("Id");
            HideInternalColumn("IsRead");
            dgvNotifications.Visible = true;
            _emptyStateLabel.Visible = false;
            ApplyNotificationBodyTheme(showTable: true);
            btnMarkAsRead.Enabled = true;
            dgvNotifications.ClearSelection();
            dgvNotifications.CurrentCell = null;
        }

        private void ShowEmptyState(string message)
        {
            dgvNotifications.DataSource = null;
            dgvNotifications.Visible = false;
            _emptyStateLabel.Text = message;
            _emptyStateLabel.Visible = true;
            ApplyNotificationBodyTheme(showTable: false);
            _emptyStateLabel.BringToFront();
            btnMarkAsRead.Enabled = false;
        }

        private void ApplyNotificationBodyTheme(bool showTable)
        {
            if (_notificationBody == null || _emptyStateLabel == null)
                return;

            Color emptyFill = AppColors.IsDarkMode ? AppColors.BgCardHover : ColorTranslator.FromHtml("#F8FAFC");
            _notificationBody.Padding = showTable ? Padding.Empty : new Padding(18);
            _notificationBody.FillColor = showTable ? AppColors.BgCard : emptyFill;
            _notificationBody.BorderColor = showTable ? Color.Transparent : AppColors.Border;
            _notificationBody.BackColor = AppColors.BgCard;
            _emptyStateLabel.BackColor = emptyFill;
            _emptyStateLabel.ForeColor = AppColors.TextMuted;
            _notificationBody.Invalidate();
        }

        public void ApplyGlobalSearch(string keyword)
        {
            _globalSearchKeyword = keyword ?? string.Empty;
            if (_notificationTable != null)
            {
                DataTable filtered = FilterNotificationTable(_notificationTable);
                string emptyMessage = string.IsNullOrWhiteSpace(_globalSearchKeyword)
                    ? "Chưa có thông báo gần đây"
                    : "Không tìm thấy thông báo phù hợp";
                BindNotificationTable(filtered, emptyMessage);
            }
        }

        private DataTable FilterNotificationTable(DataTable source)
        {
            string keyword = _globalSearchKeyword.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(keyword))
                return source;

            DataTable filtered = source.Clone();
            foreach (DataRow row in source.Rows)
            {
                string text = $"{row["Tiêu đề"]} {row["Nội dung"]} {row["Trạng thái"]}".ToLowerInvariant();
                if (text.Contains(keyword))
                    filtered.ImportRow(row);
            }

            return filtered;
        }

        private void HideInternalColumn(string columnName)
        {
            if (dgvNotifications.Columns[columnName] != null)
                dgvNotifications.Columns[columnName]!.Visible = false;
        }

        private static DataTable CreateNotificationTableSchema()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("IsRead", typeof(bool));
            dt.Columns.Add("Thời gian", typeof(string));
            dt.Columns.Add("Tiêu đề", typeof(string));
            dt.Columns.Add("Nội dung", typeof(string));
            dt.Columns.Add("Trạng thái", typeof(string));
            return dt;
        }

        private async System.Threading.Tasks.Task MarkClickedNotificationAsRead(int rowIndex)
        {
            if (!dgvNotifications.Visible || rowIndex < 0 || rowIndex >= dgvNotifications.Rows.Count)
                return;

            DataGridViewRow row = dgvNotifications.Rows[rowIndex];
            if (row.IsNewRow)
                return;

            int notificationId = Convert.ToInt32(row.Cells["Id"].Value);
            bool isRead = row.Cells["IsRead"].Value is bool value && value;
            if (notificationId <= 0 || isRead || !_markingReadIds.Add(notificationId))
                return;

            try
            {
                bool updated = await System.Threading.Tasks.Task.Run(() => _repository.MarkAsRead(notificationId));
                if (!updated)
                    return;

                row.Cells["IsRead"].Value = true;
                row.Cells["Trạng thái"].Value = "Đã đọc";
                row.DataGridView?.InvalidateRow(row.Index);
                UpdateCachedNotificationRow(notificationId);

                if (FindForm() is StudentDashboard dashboard)
                    dashboard.RefreshNotificationSummary();
            }
            finally
            {
                _markingReadIds.Remove(notificationId);
            }
        }

        private void UpdateCachedNotificationRow(int notificationId)
        {
            if (_notificationTable == null)
                return;

            foreach (DataRow dataRow in _notificationTable.Rows)
            {
                if (Convert.ToInt32(dataRow["Id"]) != notificationId)
                    continue;

                dataRow["IsRead"] = true;
                dataRow["Trạng thái"] = "Đã đọc";
                break;
            }
        }

        private async System.Threading.Tasks.Task MarkSelectedNotificationAsRead()
        {
            if (!dgvNotifications.Visible || dgvNotifications.CurrentRow == null || dgvNotifications.CurrentRow.IsNewRow)
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn một thông báo thật để cập nhật.", "Thông báo");
                return;
            }

            int notificationId = Convert.ToInt32(dgvNotifications.CurrentRow.Cells["Id"].Value);
            if (notificationId <= 0)
            {
                MetaTheme.ShowModernDialog("Không có thông báo để cập nhật.", "Thông báo");
                return;
            }

            bool updated = await System.Threading.Tasks.Task.Run(() => _repository.MarkAsRead(notificationId));
            if (!updated)
            {
                MetaTheme.ShowModernDialog("Không tìm thấy thông báo cần cập nhật.", "Thông báo");
                return;
            }

            await LoadDataAsync();
            if (FindForm() is StudentDashboard dashboard)
                dashboard.RefreshNotificationSummary();
        }
    }
}
