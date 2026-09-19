using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Frontend.Forms.Login;
using CourseGuard.Frontend.Forms.Student;
using CourseGuard.Frontend.Forms.Teacher;

namespace CourseGuard.Frontend.Forms.Admin
{
    public partial class RedirectForm : Form
    {
        private System.Windows.Forms.Timer? _heartbeatTimer;

        public RedirectForm()
        {
            InitializeComponent();
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.Opacity = 0; // Completely invisible
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            _heartbeatTimer = new System.Windows.Forms.Timer { Interval = 30000 }; // 30 seconds
            _heartbeatTimer.Tick += HeartbeatTimer_Tick;
            _heartbeatTimer.Start();

            this.FormClosed += (s, ev) =>
            {
                _heartbeatTimer?.Stop();
                _heartbeatTimer?.Dispose();
            };

            bool isRunning = true;
            while (isRunning)
            {
                using (LoginPage login = new LoginPage())
                {
                    if (login.ShowDialog() == DialogResult.OK)
                    {
                        UserModel? user = login.CurrentUser;
                        if (user == null)
                        {
                            isRunning = false;
                            break;
                        }

                        Form? dashboard = null;
                        string normalizedRole = (user.Role ?? string.Empty).Trim().ToUpperInvariant();

                        switch (normalizedRole)
                        {
                            case "ADMIN":
                                dashboard = new AdminDashboard(user);
                                break;
                            case "TEACHER":
                                dashboard = new TeacherDashboard(user, login.ForceChangePassword);
                                break;
                            case "STUDENT":
                                dashboard = new StudentDashboard(user, login.ForceChangePassword);
                                break;
                            default:
                                CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog($"Quyền không xác định: {user.Role}");
                                break;
                        }

                        if (dashboard != null)
                        {
                            if (dashboard.ShowDialog() == DialogResult.OK)
                            {
                                // Người dùng bấm đăng xuất (Logout) trả về DialogResult.OK
                                // Tiếp tục vòng lặp để hiện lại Form Đăng nhập
                                continue;
                            }
                            else
                            {
                                // Người dùng tắt bằng nút X hoặc cách khác
                                isRunning = false;
                            }
                        }
                        else
                        {
                            isRunning = false;
                        }
                    }
                    else
                    {
                        // Người dùng tắt Form đăng nhập chữ không đăng nhập
                        isRunning = false;
                    }
                }
            }

            System.Windows.Forms.Application.Exit();
        }

        private void HeartbeatTimer_Tick(object? sender, EventArgs e)
        {
            int? currentUserId = UserSessionContext.CurrentUserId;
            if (currentUserId.HasValue)
            {
                int userId = currentUserId.Value;
                Task.Run(async () =>
                {
                    try
                    {
                        var dbContext = new CourseGuard.Backend.Data.CourseGuardDbContext("");
                        string ipAddress = GetLocalIpAddress();
                        await dbContext.LogDeviceActivityAsync(userId, Environment.MachineName, ipAddress);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Heartbeat] Error: {ex.Message}");
                    }
                });
            }
        }

        private static string GetLocalIpAddress()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up)
                        continue;

                    foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip.Address))
                            return ip.Address.ToString();
                    }
                }
            }
            catch
            {
            }
            return "127.0.0.1";
        }
    }
}
