using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.Forms.Student
{
    public class StudentLessonDetailDialog : Form
    {
        private readonly CourseGuardDbContext _dbContext;
        private readonly TeacherLessonModel _lesson;
        private readonly int _studentId;

        private Label lblTitle = null!;
        private Label lblCourse = null!;
        private Label lblPublishDate = null!;
        
        private Label lblContentTitle = null!;
        private TextBox txtContent = null!;
        
        private Label lblDownloadTitle = null!;
        private Button btnDownloadFile = null!;
        
        private Button btnClose = null!;

        public StudentLessonDetailDialog(CourseGuardDbContext dbContext, TeacherLessonModel lesson, int studentId)
        {
            _dbContext = dbContext;
            _lesson = lesson;
            _studentId = studentId;

            InitializeComponent();
            ApplyStyle();
            LoadData();
        }

        private void InitializeComponent()
        {
            this.Text = "Chi tiết bài học";
            this.Size = new Size(650, 550);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            lblTitle = new Label { Location = new Point(20, 20), AutoSize = true, Font = AppFonts.Semibold(16F) };
            lblCourse = new Label { Location = new Point(20, 55), AutoSize = true, Font = MetaTheme.Fonts.BodyMd() };
            lblPublishDate = new Label { Location = new Point(20, 80), AutoSize = true, Font = new Font(AppFonts.Body.FontFamily, 10F, FontStyle.Italic), ForeColor = Color.Gray };

            lblContentTitle = new Label { Location = new Point(20, 115), AutoSize = true, Font = MetaTheme.Fonts.SubtitleLg(), Text = "Nội dung bài học:" };

            txtContent = new TextBox
            {
                Location = new Point(20, 145),
                Size = new Size(590, 200),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = MetaTheme.Fonts.BodyMd()
            };

            lblDownloadTitle = new Label { Location = new Point(20, 365), AutoSize = true, Font = MetaTheme.Fonts.SubtitleLg(), Text = "Tài liệu đính kèm:" };

            btnDownloadFile = new Button
            {
                Location = new Point(20, 395),
                Size = new Size(590, 40),
                Text = "Tải tài liệu",
                Font = MetaTheme.Fonts.ButtonMd(),
                FlatStyle = FlatStyle.Flat,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 10, 0)
            };
            btnDownloadFile.Click += BtnDownloadFile_Click;

            btnClose = new Button
            {
                Location = new Point(510, 450),
                Size = new Size(100, 40),
                Text = "Đóng",
                Font = MetaTheme.Fonts.BodyMd(),
                FlatStyle = FlatStyle.Flat
            };
            btnClose.Click += (s, e) => this.Close();

            this.Controls.Add(lblTitle);
            this.Controls.Add(lblCourse);
            this.Controls.Add(lblPublishDate);
            this.Controls.Add(lblContentTitle);
            this.Controls.Add(txtContent);
            this.Controls.Add(lblDownloadTitle);
            this.Controls.Add(btnDownloadFile);
            this.Controls.Add(btnClose);
        }

        private void ApplyStyle()
        {
            this.BackColor = AppColors.BgBase;
            lblTitle.ForeColor = AppColors.TextPrimary;
            lblCourse.ForeColor = AppColors.TextSecondary;
            lblContentTitle.ForeColor = AppColors.TextPrimary;
            lblDownloadTitle.ForeColor = AppColors.TextPrimary;

            txtContent.BackColor = AppColors.BgCard;
            txtContent.ForeColor = AppColors.TextPrimary;
            DialogInputStyler.ApplyAdminStyle(this);

            btnDownloadFile.BackColor = AppColors.AccentBlue;
            btnDownloadFile.ForeColor = Color.White;
            btnDownloadFile.FlatAppearance.BorderSize = 0;

            btnClose.BackColor = AppColors.BgCard;
            btnClose.ForeColor = AppColors.TextPrimary;
            btnClose.FlatAppearance.BorderColor = AppColors.Border;
        }

        private void LoadData()
        {
            lblTitle.Text = _lesson.Title;
            lblCourse.Text = $"Khóa học: {_lesson.CourseName}";
            lblPublishDate.Text = $"Ngày đăng: {(_lesson.PublishAt.HasValue ? _lesson.PublishAt.Value.ToString("dd/MM/yyyy HH:mm") : "N/A")}";
            
            txtContent.Text = _lesson.Content;

            if (_lesson.HasStoredContent && !string.IsNullOrWhiteSpace(_lesson.FileName))
            {
                btnDownloadFile.Visible = true;
                lblDownloadTitle.Visible = true;
                btnDownloadFile.Text = $"Tải xuống: {_lesson.FileName}";
            }
            else
            {
                btnDownloadFile.Visible = false;
                lblDownloadTitle.Visible = false;
            }
        }

        private async void BtnDownloadFile_Click(object? sender, EventArgs e)
        {
            try
            {
                btnDownloadFile.Enabled = false;
                btnDownloadFile.Text = "Đang tải...";

                byte[]? content = await _dbContext.GetStudentLessonFileContentAsync(_lesson.Id, _studentId);
                
                if (content == null || content.Length == 0)
                {
                    MetaTheme.ShowModernDialog("Không thể tải tài liệu từ máy chủ.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using var sfd = new SaveFileDialog
                {
                    FileName = _lesson.FileName ?? "TaiLieuBaiHoc.pdf",
                    Filter = "All Files (*.*)|*.*",
                    Title = "Lưu tài liệu bài học"
                };

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    await File.WriteAllBytesAsync(sfd.FileName, content);
                    MetaTheme.ShowModernDialog("Tải tài liệu thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = sfd.FileName,
                            UseShellExecute = true
                        });
                    }
                    catch { /* Ignore if no default app to open */ }
                }
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog($"Lỗi khi tải tài liệu: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnDownloadFile.Enabled = true;
                btnDownloadFile.Text = $"Tải xuống: {_lesson.FileName}";
            }
        }
    }
}
