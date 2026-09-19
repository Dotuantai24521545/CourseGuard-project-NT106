using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls.Teacher;

namespace CourseGuard.Frontend.UserControls.Admin
{
    public class UC_DevicesManage : UserControl
    {
        private readonly CourseGuardDbContext _dbContext;
        private DataGridView _dgvDevices = new();
        private TextBox _txtSearch = new();
        private Button _btnBlock = new();
        private Button _btnUnblock = new();
        private Button _btnForceLogout = new();
        private Button _btnRefresh = new();
        private List<DeviceModel> _allDevices = new();
        private System.Windows.Forms.Timer? _refreshTimer;

        public UC_DevicesManage()
        {
            _dbContext = new CourseGuardDbContext(string.Empty);
            BuildLayout();
            LoadDataAsync();

            _refreshTimer = new System.Windows.Forms.Timer { Interval = 10000 }; // 10 seconds
            _refreshTimer.Tick += (s, e) => AutoRefreshData();
            _refreshTimer.Start();

            this.Disposed += (s, e) => {
                _refreshTimer?.Stop();
                _refreshTimer?.Dispose();
            };
        }

        private void BuildLayout()
        {
            var root = TeacherTabChrome.CreateRoot(this);

            _txtSearch.PlaceholderText = "Tìm theo tên máy, tài khoản...";
            _txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) FilterDevices(); };
            SearchBoxPanel searchBox = new SearchBoxPanel(_txtSearch, 360);

            var btnSearch = new Button
            {
                Text = "Tìm kiếm",
                Cursor = Cursors.Hand
            };
            TeacherTabChrome.StylePrimaryButton(btnSearch);
            PrepareToolbarButton(btnSearch);
            btnSearch.Click += (s, e) => FilterDevices();

            _btnRefresh.Text = "Tải lại";
            _btnRefresh.Cursor = Cursors.Hand;
            TeacherTabChrome.StyleSecondaryButton(_btnRefresh);
            PrepareToolbarButton(_btnRefresh);
            _btnRefresh.Click += (s, e) => LoadDataAsync();

            _dgvDevices.Dock = DockStyle.Fill;
            _dgvDevices.AllowUserToAddRows = false;
            _dgvDevices.AllowUserToDeleteRows = false;
            _dgvDevices.ReadOnly = true;
            _dgvDevices.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _dgvDevices.MultiSelect = false;
            _dgvDevices.RowHeadersVisible = false;
            _dgvDevices.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            
            _dgvDevices.Columns.Add("Id", "Mã TB");
            _dgvDevices.Columns.Add("Username", "Tài khoản");
            _dgvDevices.Columns.Add("UserFullName", "Họ tên");
            _dgvDevices.Columns.Add("DeviceName", "Tên thiết bị");
            _dgvDevices.Columns.Add("IpAddress", "Địa chỉ IP");
            _dgvDevices.Columns.Add("Status", "Trạng thái");
            _dgvDevices.Columns.Add("LastActive", "Hoạt động cuối");

            var colId = _dgvDevices.Columns["Id"];
            if (colId != null)
            {
                colId.Width = 60;
                colId.Visible = false;
            }

            _dgvDevices.CellFormatting += DgvDevices_CellFormatting;
            TeacherTabChrome.StyleGrid(_dgvDevices);

            _btnBlock.Text = "Khóa thiết bị";
            _btnBlock.Cursor = Cursors.Hand;
            TeacherTabChrome.StyleDangerButton(_btnBlock);
            PrepareFooterButton(_btnBlock, 142);
            _btnBlock.Click += BtnBlock_Click;

            _btnUnblock.Text = "Mở khóa thiết bị";
            _btnUnblock.Cursor = Cursors.Hand;
            TeacherTabChrome.StylePrimaryButton(_btnUnblock);
            PrepareFooterButton(_btnUnblock, 158);
            _btnUnblock.Click += BtnUnblock_Click;

            _btnForceLogout.Text = "Đăng xuất từ xa";
            _btnForceLogout.Cursor = Cursors.Hand;
            TeacherTabChrome.StyleSecondaryButton(_btnForceLogout);
            PrepareFooterButton(_btnForceLogout, 156);
            _btnForceLogout.Click += BtnForceLogout_Click;

            var headerCard = TeacherTabChrome.CreateHeader(
                "Thiết bị",
                "Giám sát và quản lý các thiết bị đang truy cập hệ thống",
                searchBox,
                btnSearch,
                _btnRefresh);

            var tableBody = TeacherTabChrome.CreateTableBody(_dgvDevices, out _);
            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
            content.Controls.Add(tableBody, 0, 0);
            content.Controls.Add(CreateFooterActions(), 0, 1);

            var cardGrid = TeacherTabChrome.CreateDataCard("Danh sách thiết bị truy cập", content);

            root.Controls.Add(headerCard, 0, 0);
            root.Controls.Add(cardGrid, 0, 1);
        }

        private FlowLayoutPanel CreateFooterActions()
        {
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 18, 0, 0),
                Margin = Padding.Empty
            };

            actions.Controls.Add(_btnBlock);
            actions.Controls.Add(_btnUnblock);
            actions.Controls.Add(_btnForceLogout);
            return actions;
        }

        private static void PrepareToolbarButton(Button button)
        {
            button.Width = 116;
            button.Height = 40;
            button.MinimumSize = new Size(116, 40);
            button.Margin = new Padding(8, 0, 0, 0);
            button.Padding = new Padding(16, 0, 16, 1);
        }

        private static void PrepareFooterButton(Button button, int width)
        {
            button.Width = width;
            button.Height = 40;
            button.MinimumSize = new Size(width, 40);
            button.Margin = new Padding(0, 0, 12, 0);
            button.Padding = new Padding(16, 0, 16, 1);
        }

        private async void LoadDataAsync()
        {
            this.ShowSkeleton(SkeletonType.FormWithTable);
            try
            {
                _allDevices = await Task.Run(() => _dbContext.GetActiveDevices());
                if (IsDisposed || Disposing) return;
                BindDevices(_allDevices);
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog("Không thể tải danh sách thiết bị: " + ex.Message, "Lỗi");
            }
            finally
            {
                this.HideSkeleton();
            }
        }

        private void BindDevices(List<DeviceModel> devices)
        {
            _dgvDevices.Rows.Clear();
            foreach (var d in devices)
            {
                string displayStatus = d.Status;
                if (d.Status == "ACTIVE")
                {
                    var diff = DateTime.Now - d.LastActive;
                    if (diff.TotalSeconds <= 90)
                    {
                        displayStatus = "● Online";
                    }
                    else
                    {
                        displayStatus = "○ Offline";
                    }
                }
                else if (d.Status == "BLOCKED")
                {
                    displayStatus = "🚫 Blocked";
                }

                _dgvDevices.Rows.Add(
                    d.Id,
                    d.Username,
                    d.UserFullName,
                    d.DeviceName,
                    d.IpAddress,
                    displayStatus,
                    d.LastActive.ToString("dd/MM/yyyy HH:mm:ss")
                );
            }
        }

        private void DgvDevices_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_dgvDevices.Columns[e.ColumnIndex].Name == "Status" && e.Value != null)
            {
                string status = e.Value.ToString() ?? "";
                if (status.Contains("Online"))
                {
                    e.CellStyle.ForeColor = Color.FromArgb(34, 197, 94); // Green
                    e.CellStyle.SelectionForeColor = Color.FromArgb(34, 197, 94);
                }
                else if (status.Contains("Offline"))
                {
                    e.CellStyle.ForeColor = Color.FromArgb(156, 163, 175); // Gray
                    e.CellStyle.SelectionForeColor = Color.FromArgb(156, 163, 175);
                }
                else if (status.Contains("Blocked"))
                {
                    e.CellStyle.ForeColor = Color.FromArgb(239, 68, 68); // Red
                    e.CellStyle.SelectionForeColor = Color.FromArgb(239, 68, 68);
                }
            }
        }

        private async void AutoRefreshData()
        {
            try
            {
                var devices = await Task.Run(() => _dbContext.GetActiveDevices());
                if (IsDisposed || Disposing) return;

                int selectedDeviceId = -1;
                if (_dgvDevices.SelectedRows.Count > 0)
                {
                    selectedDeviceId = Convert.ToInt32(_dgvDevices.SelectedRows[0].Cells["Id"].Value);
                }

                _allDevices = devices;
                FilterDevices();

                // Restore selection
                if (selectedDeviceId != -1)
                {
                    foreach (DataGridViewRow row in _dgvDevices.Rows)
                    {
                        if (Convert.ToInt32(row.Cells["Id"].Value) == selectedDeviceId)
                        {
                            _dgvDevices.ClearSelection();
                            row.Selected = true;
                            break;
                        }
                    }
                }
            }
            catch
            {
                // Silent error on auto-refresh
            }
        }

        private void FilterDevices()
        {
            string keyword = _txtSearch.Text.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(keyword))
            {
                BindDevices(_allDevices);
                return;
            }

            var filtered = _allDevices.FindAll(d =>
                d.DeviceName.ToLowerInvariant().Contains(keyword) ||
                d.Username.ToLowerInvariant().Contains(keyword) ||
                d.UserFullName.ToLowerInvariant().Contains(keyword) ||
                d.IpAddress.ToLowerInvariant().Contains(keyword)
            );
            BindDevices(filtered);
        }

        private async void BtnBlock_Click(object? sender, EventArgs e)
        {
            if (_dgvDevices.SelectedRows.Count == 0)
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn một thiết bị để khóa.", "Thông báo");
                return;
            }

            int deviceId = Convert.ToInt32(_dgvDevices.SelectedRows[0].Cells["Id"].Value);
            string deviceName = _dgvDevices.SelectedRows[0].Cells["DeviceName"].Value?.ToString() ?? "";
            string username = _dgvDevices.SelectedRows[0].Cells["Username"].Value?.ToString() ?? "";

            if (MetaTheme.ShowModernDialog($"Bạn có chắc chắn muốn KHÓA thiết bị '{deviceName}' của tài khoản '{username}'?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                this.ShowSkeleton(SkeletonType.FormWithTable);
                try
                {
                    bool success = await Task.Run(() => _dbContext.UpdateDeviceStatus(deviceId, "BLOCKED"));
                    if (success)
                    {
                        MetaTheme.ShowModernDialog("Khóa thiết bị thành công!", "Thành công");
                        LoadDataAsync();
                    }
                    else
                    {
                        MetaTheme.ShowModernDialog("Không thể cập nhật trạng thái thiết bị.", "Thất bại");
                    }
                }
                finally
                {
                    this.HideSkeleton();
                }
            }
        }

        private async void BtnUnblock_Click(object? sender, EventArgs e)
        {
            if (_dgvDevices.SelectedRows.Count == 0)
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn một thiết bị để mở khóa.", "Thông báo");
                return;
            }

            int deviceId = Convert.ToInt32(_dgvDevices.SelectedRows[0].Cells["Id"].Value);
            string deviceName = _dgvDevices.SelectedRows[0].Cells["DeviceName"].Value?.ToString() ?? "";
            string username = _dgvDevices.SelectedRows[0].Cells["Username"].Value?.ToString() ?? "";

            if (MetaTheme.ShowModernDialog($"Bạn có chắc chắn muốn MỞ KHÓA thiết bị '{deviceName}' của tài khoản '{username}'?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                this.ShowSkeleton(SkeletonType.FormWithTable);
                try
                {
                    bool success = await Task.Run(() => _dbContext.UpdateDeviceStatus(deviceId, "ACTIVE"));
                    if (success)
                    {
                        MetaTheme.ShowModernDialog("Mở khóa thiết bị thành công!", "Thành công");
                        LoadDataAsync();
                    }
                    else
                    {
                        MetaTheme.ShowModernDialog("Không thể cập nhật trạng thái thiết bị.", "Thất bại");
                    }
                }
                finally
                {
                    this.HideSkeleton();
                }
            }
        }

        private async void BtnForceLogout_Click(object? sender, EventArgs e)
        {
            if (_dgvDevices.SelectedRows.Count == 0)
            {
                MetaTheme.ShowModernDialog("Vui lòng chọn một thiết bị để đăng xuất cưỡng bức.", "Thông báo");
                return;
            }

            int deviceId = Convert.ToInt32(_dgvDevices.SelectedRows[0].Cells["Id"].Value);
            string deviceName = _dgvDevices.SelectedRows[0].Cells["DeviceName"].Value?.ToString() ?? "";
            string username = _dgvDevices.SelectedRows[0].Cells["Username"].Value?.ToString() ?? "";

            if (MetaTheme.ShowModernDialog($"Bạn có chắc chắn muốn ĐĂNG XUẤT CƯỠNG BỨC thiết bị '{deviceName}' của tài khoản '{username}'?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                this.ShowSkeleton(SkeletonType.FormWithTable);
                try
                {
                    bool success = await Task.Run(() => _dbContext.DeleteDevice(deviceId));
                    if (success)
                    {
                        MetaTheme.ShowModernDialog("Đăng xuất cưỡng bức thành công!", "Thành công");
                        LoadDataAsync();
                    }
                    else
                    {
                        MetaTheme.ShowModernDialog("Không thể xóa phiên thiết bị.", "Thất bại");
                    }
                }
                finally
                {
                    this.HideSkeleton();
                }
            }
        }
    }
}
