using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Teacher
{
    public partial class UC_TeacherNotifications : UserControl
    {
        private readonly int _teacherId;
        private readonly Action? _onNotificationReadStateChanged;
        private readonly NotificationRepository _notifications = new();
        private readonly DataGridView _grid = new();
        private readonly ComboBox _categoryFilter = new();
        private readonly ComboBox _typeFilter = new();
        private readonly Button _markRead = TeacherTabChrome.PrimaryButton("Đánh dấu đã đọc");
        private readonly Button _refresh = TeacherTabChrome.SecondaryButton("Tải lại");
        private DataTable _source = new();
        private RoundedPanel _gridBody = null!;
        private Label _emptyStateLabel = null!;

        public UC_TeacherNotifications(int teacherId)
            : this(teacherId, null)
        {
        }

        public UC_TeacherNotifications(int teacherId, Action? onNotificationReadStateChanged)
        {
            _teacherId = teacherId;
            _onNotificationReadStateChanged = onNotificationReadStateChanged;
            BuildLayout();
            WireEvents();
            LoadDataAsync().FireAndForgetSafe(this);
        }

        private void BuildLayout()
        {
            _categoryFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _typeFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _categoryFilter.Width = 180;
            _typeFilter.Width = 150;
            AppColors.ApplyTheme(_categoryFilter);
            AppColors.ApplyTheme(_typeFilter);
            _categoryFilter.Items.AddRange(new object[] { "Tất cả nhóm", "Enrollment", "Assignment", "Exam", "Monitoring", "SystemAdmin" });
            _typeFilter.Items.AddRange(new object[] { "Tất cả", "Cần xử lý", "Chưa đọc", "Đã đọc" });
            _categoryFilter.SelectedIndex = 0;
            _typeFilter.SelectedIndex = 0;

            var root = TeacherTabChrome.CreateRoot(this);
            root.Controls.Add(TeacherTabChrome.CreateHeader(
                "Thông báo",
                "Các sự kiện giáo viên cần biết hoặc cần xử lý.",
                _categoryFilter, _typeFilter, _markRead, _refresh), 0, 0);
            TeacherTabChrome.StyleGrid(_grid);
            _gridBody = TeacherTabChrome.CreateTableBody(_grid, out _emptyStateLabel);
            root.Controls.Add(TeacherTabChrome.CreateDataCard("Danh sách thông báo", _gridBody), 0, 1);
            TeacherTabChrome.EnableNaturalFocusClear(this, _grid);
        }

        private void WireEvents()
        {
            _refresh.Click += async (_, _) => await LoadDataAsync();
            _markRead.Click += async (_, _) => await MarkSelectedAsReadAsync();
            _categoryFilter.SelectedIndexChanged += (_, _) => ApplyFilters();
            _typeFilter.SelectedIndexChanged += (_, _) => ApplyFilters();
        }

        private async Task LoadDataAsync()
        {
            this.ShowSkeleton(SkeletonType.NotificationList);
            try
            {
                var rows = await Task.Run(() => _notifications.LoadByUserId(_teacherId));
                _source = CreateSchema();
                foreach (NotificationModel notification in rows)
                {
                    _source.Rows.Add(
                        notification.Id,
                        notification.IsRead,
                        SystemTimeFormatter.FormatVietnamTime(notification.CreatedAt),
                        notification.Category,
                        notification.NotificationType,
                        notification.Title,
                        notification.Content,
                        notification.IsRead ? "Đã đọc" : "Chưa đọc");
                }

                ApplyFilters();
            }
            finally
            {
                this.HideSkeleton();
            }
        }

        private void ApplyFilters()
        {
            DataTable filtered = _source.Clone();
            string category = _categoryFilter.SelectedItem?.ToString() ?? "Tất cả nhóm";
            string type = _typeFilter.SelectedItem?.ToString() ?? "Tất cả";

            foreach (DataRow row in _source.Rows)
            {
                bool categoryOk = category == "Tất cả nhóm" || Convert.ToString(row["Nhóm"]) == category;
                bool typeOk = type switch
                {
                    "Cần xử lý" => Convert.ToString(row["Loại"]) == WorkflowConstants.NotificationType.ActionRequired,
                    "Chưa đọc" => !Convert.ToBoolean(row["IsRead"]),
                    "Đã đọc" => Convert.ToBoolean(row["IsRead"]),
                    _ => true
                };

                if (categoryOk && typeOk)
                    filtered.ImportRow(row);
            }

            _grid.DataSource = filtered;
            if (_grid.Columns["Id"] != null) _grid.Columns["Id"]!.Visible = false;
            if (_grid.Columns["IsRead"] != null) _grid.Columns["IsRead"]!.Visible = false;

            bool hasRows = filtered.Rows.Count > 0;
            TeacherTabChrome.SetTableState(_gridBody, _grid, _emptyStateLabel, hasRows, "Chưa có thông báo phù hợp.");
            _markRead.Enabled = hasRows;
            _grid.ClearSelection();
            _grid.CurrentCell = null;
        }

        private async Task MarkSelectedAsReadAsync()
        {
            if (!_grid.Visible || _grid.CurrentRow == null || _grid.CurrentRow.IsNewRow)
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn một thông báo.", "Thông báo");
                return;
            }

            int id = Convert.ToInt32(_grid.CurrentRow.Cells["Id"].Value);
            await Task.Run(() => _notifications.MarkAsRead(id));
            await LoadDataAsync();
            _onNotificationReadStateChanged?.Invoke();
        }

        private static DataTable CreateSchema()
        {
            DataTable table = new();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("IsRead", typeof(bool));
            table.Columns.Add("Thời gian", typeof(string));
            table.Columns.Add("Nhóm", typeof(string));
            table.Columns.Add("Loại", typeof(string));
            table.Columns.Add("Tiêu đề", typeof(string));
            table.Columns.Add("Nội dung", typeof(string));
            table.Columns.Add("Trạng thái", typeof(string));
            return table;
        }
    }
}
