/*
 * UC_UsersManage.cs
 * 
 * Layer: Presentation (UserControls)
 * Vai trò: Màn hình quản lý người dùng (CRUD). Hiển thị danh sách, thêm/xóa/sửa user.
 * Phụ thuộc: UserService.
 */
using System;
using System.Data; // Keep for rare cases, but mostly replaced
using System.Windows.Forms;
// Remove SqlClient
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Backend.Data;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Admin
{
    public partial class UC_UsersManage : UserControl
    {
        private readonly CourseGuard.Backend.Controllers.UserController _userService;

        public UC_UsersManage()
        {
            InitializeComponent();
            _userService = new CourseGuard.Backend.Controllers.UserController(new CourseGuardDbContext(""));
            ApplyThemeStyle();

            // Rounded buttons (matching Courses tab style)
            RoundedButtonHelper.Apply(10,
                btn_insert, btn_delete, btn_search, btn_Approve);

            // Default: Empty grid, only load on search
            dataGridView1.ReadOnly = true;
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            
            this.btn_delete.Click += new System.EventHandler(this.btn_delete_Click);
            this.btn_search.Click += new System.EventHandler(this.btn_search_Click);
            this.btn_Approve.Click += btn_Approve_Click;
            
            // Fix: Default values for Comboboxes
            this.cb_roleID.Items.Insert(0, "ALL");
            this.cb_roleID.SelectedIndex = 0;
            this.cb_StatusFilter.SelectedIndex = 0; // "ALL"
        }

        private void ApplyThemeStyle()
        {
            btn_insert.Tag = "primary";
            btn_search.Tag = "primary";
            btn_Approve.Tag = "secondary";
            btn_delete.Tag = "danger";
            panel1.Tag = "card";

            RefactorFormLayout();
            
            AppColors.ApplyTheme(this);
            WrapWithCards();
        }

        private RoundedPanel WrapInput(TextBox tb)
        {
            // Tự động tính toán padding dọc để chữ nằm ngay giữa, không bị cắt chân
            int vpad = Math.Max(0, (40 - tb.PreferredHeight) / 2);
            
            var pnl = new RoundedPanel
            {
                CornerRadius = 8,
                Height = 40,
                FillColor = CourseGuard.Frontend.Theme.AppColors.BgInput,
                // Chỉnh lề trái thành 12 để chữ bên trong (do TextBox có lề ẩn) thẳng hàng với chữ của ComboBox
                Padding = new Padding(12, vpad, 15, vpad), 
                Dock = DockStyle.Fill,
                Margin = new Padding(5, 10, 5, 10)
            };
            tb.Dock = DockStyle.Fill;
            tb.BorderStyle = BorderStyle.None;
            tb.BackColor = CourseGuard.Frontend.Theme.AppColors.BgInput; // Đồng bộ màu nền TextBox
            pnl.Controls.Add(tb);
            return pnl;
        }

        private void RefactorFormLayout()
        {
            panel1.Controls.Clear();
            
            TableLayoutPanel tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 3,
                Padding = new Padding(15)
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 55F));
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 55F));
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F)); // Increased to give the 40px buttons + 15px margin plenty of room

            // Fix: Wrap TextBoxes in RoundedPanels for rounded corners
            var pnlUsername = WrapInput(txt_Username);
            var pnlPassword = WrapInput(txt_Password);
            txt_Password.UseSystemPasswordChar = true;
            var pnlFullName = WrapInput(txt_FullName);
            var pnlEmail = WrapInput(txt_Email);
            
            cb_roleID.Anchor = AnchorStyles.Left | AnchorStyles.Right; cb_roleID.Margin = new Padding(5, 10, 5, 10);
            cb_StatusFilter.Anchor = AnchorStyles.Left | AnchorStyles.Right; cb_StatusFilter.Margin = new Padding(5, 10, 5, 10);

            tlp.Controls.Add(pnlUsername, 0, 0);
            tlp.Controls.Add(pnlPassword, 1, 0);
            tlp.Controls.Add(pnlFullName, 2, 0);
            tlp.Controls.Add(pnlEmail, 0, 1);
            tlp.Controls.Add(cb_roleID, 1, 1);
            tlp.Controls.Add(cb_StatusFilter, 2, 1);

            FlowLayoutPanel flp = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0),
                Margin = new Padding(0, 15, 0, 0) // Fix: Add space above buttons
            };
            
            // Add buttons to flow layout
            btn_Approve.Margin = new Padding(10, 0, 0, 0);
            btn_search.Margin = new Padding(10, 0, 0, 0);
            btn_delete.Margin = new Padding(10, 0, 0, 0);
            btn_insert.Margin = new Padding(10, 0, 0, 0);
            
            flp.Controls.Add(btn_Approve);
            flp.Controls.Add(btn_search);
            flp.Controls.Add(btn_delete);
            flp.Controls.Add(btn_insert);

            tlp.Controls.Add(flp, 0, 2);
            tlp.SetColumnSpan(flp, 3);

            panel1.Controls.Add(tlp);
        }

        private void WrapWithCards()
        {
            this.Controls.Remove(panel1);
            
            // Fix: Add top spacer so the card isn't hugging the Topbar
            var topSpacer = new Panel { Dock = DockStyle.Top, Height = 20, BackColor = Color.Transparent, Tag = "custom" };
            this.Controls.Add(topSpacer);
            topSpacer.BringToFront();

            var cardFilter = CourseGuard.Frontend.UserControls.Teacher.TeacherTabChrome.CreateDataCard("Quản lý người dùng", panel1);
            cardFilter.Dock = DockStyle.Top;
            cardFilter.Height = 280; // Increased height to prevent cutoff
            this.Controls.Add(cardFilter);
            cardFilter.BringToFront();
            
            var spacer = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = Color.Transparent, Tag = "custom" };
            this.Controls.Add(spacer);
            spacer.BringToFront();

            this.Controls.Remove(dataGridView1);
            var cardGrid = CourseGuard.Frontend.UserControls.Teacher.TeacherTabChrome.CreateDataCard("Danh sách người dùng", dataGridView1);
            cardGrid.Dock = DockStyle.Fill;
            this.Controls.Add(cardGrid);
            cardGrid.BringToFront();
        }

        private async void LoadData()
        {
            string status = cb_StatusFilter.SelectedItem?.ToString() ?? "ALL";
            string role = (cb_roleID.SelectedItem == null || cb_roleID.Text == "Select Role") ? "ALL" : cb_roleID.Text.ToUpper();

            this.ShowSkeleton(SkeletonType.FormWithTable);
            try
            {
                var users = await Task.Run(() => _userService.SearchUsers(status, role));
                dataGridView1.DataSource = users;
                
                // Hide Password Hash if present in grid?
                if (dataGridView1.Columns.Contains("PasswordHash"))
                {
                    DataGridViewColumn? passwordColumn = dataGridView1.Columns["PasswordHash"];
                    if (passwordColumn != null)
                    {
                        passwordColumn.Visible = false;
                    }
                }
            }
            catch (Exception ex)
            {
                CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Lỗi tải dữ liệu: " + ex.Message);
            }
            finally
            {
                this.HideSkeleton();
            }
        }

        private async void btn_insert_Click(object sender, EventArgs e)
        {
            // Validate
            if (string.IsNullOrWhiteSpace(txt_Username.Text) ||
                string.IsNullOrWhiteSpace(txt_Password.Text) ||
                string.IsNullOrWhiteSpace(txt_FullName.Text) ||
                string.IsNullOrWhiteSpace(txt_Email.Text) ||
                string.IsNullOrWhiteSpace(cb_roleID.Text))
            {
                CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Vui lòng điền đầy đủ thông tin.");
                return;
            }

            var user = new UserModel
            {
                Username = txt_Username.Text.Trim(),
                FullName = txt_FullName.Text.Trim(),
                Email = txt_Email.Text.Trim(),
                Role = cb_roleID.Text, // "Teacher" or "Student"
                Status = "ACTIVE"
            };

            string password = txt_Password.Text.Trim();

            this.ShowSkeleton(SkeletonType.FormWithTable);
            try
            {
                string result = await Task.Run(() => _userService.AddUser(user, password));
                if (result == "Success")
                {
                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Thêm user thành công.");
                    ClearForm();
                    LoadData();
                }
                else
                {
                    string detail = !string.IsNullOrWhiteSpace(_userService.LastErrorMessage)
                        ? _userService.LastErrorMessage
                        : result;
                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Thêm thất bại: " + detail);
                }
            }
            catch (Exception ex)
            {
                CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Lỗi: " + ex.Message);
            }
            finally
            {
                this.HideSkeleton();
            }
        }

        private async void btn_delete_Click(object? sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count > 0)
            {
                try 
                {
                    var cell = dataGridView1.SelectedRows[0].Cells["Id"]; // UserModel property is "Id"
                    if (cell != null && cell.Value != null)
                    {
                        int userId = Convert.ToInt32(cell.Value);
                        if (CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Bạn có chắc chắn muốn xóa user này?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                        {
                            this.ShowSkeleton(SkeletonType.FormWithTable);
                            try
                            {
                                bool success = await Task.Run(() => _userService.DeleteUser(userId));
                                if (success)
                                {
                                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Xóa thành công!");
                                    LoadData(); 
                                }
                                else
                                {
                                    string detail = string.IsNullOrWhiteSpace(_userService.LastErrorMessage)
                                        ? $"Xóa thất bại (ID: {userId})"
                                        : _userService.LastErrorMessage;
                                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog(detail);
                                }
                            }
                            finally
                            {
                                this.HideSkeleton();
                            }
                        }
                    }
                    else 
                    {
                         CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Không chọn được ID.");
                    }
                }
                catch(Exception ex)
                {
                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Lỗi khi xóa: " + ex.Message);
                }
            }
            else
            {
                CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Vui lòng chọn user để xóa.");
            }
        }

        private void btn_search_Click(object? sender, EventArgs e)
        {
            LoadData();
        }

        private void ClearForm()
        {
            txt_Username.Clear();
            txt_Password.Clear();
            txt_FullName.Clear();
            txt_Email.Clear();
            cb_roleID.SelectedIndex = -1;
        }

        private async void btn_Approve_Click(object? sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count > 0)
            {
                try
                {
                    var userIdCell = dataGridView1.SelectedRows[0].Cells["Id"];
                    var statusCell = dataGridView1.SelectedRows[0].Cells["Status"];

                    if (userIdCell != null && userIdCell.Value != null)
                    {
                        int userId = Convert.ToInt32(userIdCell.Value);
                        string currentStatus = statusCell?.Value?.ToString() ?? "";

                        bool isResetRequest = currentStatus.Equals("RESET_REQUEST", StringComparison.OrdinalIgnoreCase);
                        string action = isResetRequest ? "RESET" : "APPROVE";
                        string confirmMsg = isResetRequest
                            ? "Bạn muốn gửi email cấp lại mật khẩu cho user này?"
                            : "Bạn muốn kích hoạt tài khoản này?";

                        if (CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog(confirmMsg, "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            this.ShowSkeleton(SkeletonType.FormWithTable);
                            try
                            {
                                bool success = await Task.Run(() => _userService.ApproveUserRequest(userId, action));
                                if (success)
                                {
                                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog(action == "RESET"
                                        ? "Đã gửi mật khẩu tạm thời qua email cho user."
                                        : "Thực hiện thành công!");
                                    LoadData();
                                }
                                else
                                {
                                    string detail = string.IsNullOrWhiteSpace(_userService.LastErrorMessage)
                                        ? "Kiểm tra email user hoặc cấu hình SMTP_USER/SMTP_PASS."
                                        : _userService.LastErrorMessage;
                                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog($"Thao tác thất bại.\nChi tiết: {detail}");
                                }
                            }
                            finally
                            {
                                this.HideSkeleton();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Lỗi: " + ex.Message);
                }
            }
            else
            {
                CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Vui lòng chọn user cần phê duyệt.");
            }
        }
    }
}
