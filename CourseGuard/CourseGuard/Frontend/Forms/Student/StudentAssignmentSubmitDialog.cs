using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Backend.Services;
using CourseGuard.Frontend.Forms.Teacher;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.Forms.Student
{
    public class RoundedInputPanel : Panel
    {
        private const int CornerRadius = 8;
        private bool _isFocused;
        private bool _isHovered;

        public RoundedInputPanel(Control innerControl)
        {
            BackColor = Color.Transparent;
            Padding = new Padding(12, 8, 12, 8);
            DoubleBuffered = true;
            
            innerControl.Dock = DockStyle.Fill;
            innerControl.GotFocus += (_, _) => { _isFocused = true; Invalidate(); };
            innerControl.LostFocus += (_, _) => { _isFocused = false; Invalidate(); };
            innerControl.MouseEnter += (_, _) => { _isHovered = true; Invalidate(); };
            innerControl.MouseLeave += (_, _) => { _isHovered = false; Invalidate(); };
            MouseEnter += (_, _) => { _isHovered = true; Invalidate(); };
            MouseLeave += (_, _) => { _isHovered = false; Invalidate(); };

            Controls.Add(innerControl);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Color parentColor = AppColors.BgCard;
            Control? p = Parent;
            while(p != null)
            {
                if (p.BackColor != Color.Transparent)
                {
                    if (p is Form f && f.Tag?.ToString() == "dialog") parentColor = AppColors.BgCard;
                    else parentColor = p.BackColor;
                    break;
                }
                p = p.Parent;
            }
            e.Graphics.Clear(parentColor);

            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using var bgPath = GraphicsHelpers.RoundedRectF(rect, CornerRadius);
            using var bgBrush = new SolidBrush(AppColors.BgInput);
            e.Graphics.FillPath(bgBrush, bgPath);

            Color borderColor = _isFocused ? AppColors.AccentBlue : _isHovered ? AppColors.BorderStrong : AppColors.Border;
            using var borderPen = new Pen(borderColor, _isFocused ? 1.5f : 1f);
            e.Graphics.DrawPath(borderPen, bgPath);
        }
    }

    public class StudentAssignmentSubmitDialog : CourseGuard.Frontend.Forms.Teacher.ThemedDialogBase
    {
        private readonly CourseGuardDbContext _dbContext;
        private readonly StudentAssignmentRow _assignment;
        private readonly int _studentId;

        private Label lblTitle = null!;
        private Label lblCourse = null!;
        private Label lblDueDate = null!;
        private Label lblDescriptionTitle = null!;
        private TextBox txtDescription = null!;
        private Label lblDownloadTitle = null!;
        private Button btnDownloadTeacherFile = null!;
        private Label _statusBanner = null!;
        private Label _submissionSummary = null!;
        private Panel _feedbackPanel = null!;
        private Label _feedbackText = null!;
        private Label _submitStatus = null!;
        private AssignmentUxPresentation _presentation = null!;

        private Label lblUploadTitle = null!;
        private TextBox txtSelectedFile = null!;
        private Button btnSelectFile = null!;
        private Button btnSubmit = null!;
        private Button btnCancel = null!;

        private string? _selectedFilePath;
        private byte[]? _selectedFileContent;

        public StudentAssignmentSubmitDialog(CourseGuardDbContext dbContext, StudentAssignmentRow assignment, int studentId)
        {
            _dbContext = dbContext;
            _assignment = assignment;
            _studentId = studentId;

            InitializeComponent();
            ApplyStyle();
            LoadData();
        }

        private void InitializeComponent()
        {
            Text = "Nộp bài tập";
            Width = 600;
            Height = 500;

            // Title info panel
            lblTitle = new Label { AutoSize = true, Font = MetaTheme.Fonts.HeadingMd(), Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 2) };
            lblCourse = new Label { AutoSize = true, Font = MetaTheme.Fonts.BodyMd(), Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 0) };
            lblDueDate = new Label { AutoSize = true, Font = MetaTheme.Fonts.BodyMdBold(), ForeColor = Color.Red, Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 8) };
            _presentation = StudentAssignmentUxPresenter.Present(_assignment, DateTime.Now);

            _statusBanner = new Label
            {
                AutoSize = false,
                Height = 34,
                Dock = DockStyle.Top,
                Font = AppFonts.Semibold(9f),
                ForeColor = AppColors.TextPrimary,
                BackColor = AppColors.BgInput,
                Padding = new Padding(10, 0, 10, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = $"{_presentation.StatusText} - {_presentation.DetailText}",
                UseCompatibleTextRendering = false
            };

            _submissionSummary = new Label
            {
                AutoSize = false,
                Height = 28,
                Dock = DockStyle.Top,
                Font = AppFonts.Caption,
                ForeColor = AppColors.TextSecondary,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = false
            };

            _feedbackPanel = new Panel
            {
                Height = 70,
                Dock = DockStyle.Top,
                BackColor = AppColors.BgInput,
                Padding = new Padding(10),
                Visible = false
            };
            _feedbackText = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                Font = AppFonts.Caption,
                ForeColor = AppColors.TextPrimary,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = false
            };
            _feedbackPanel.Controls.Add(_feedbackText);

            lblDescriptionTitle = new Label { AutoSize = true, Font = MetaTheme.Fonts.BodyMdBold(), Text = "Mô tả đề bài:", Dock = DockStyle.Top, Padding = new Padding(0, 4, 0, 4) };

            txtDescription = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = MetaTheme.Fonts.BodyMd(),
                BackColor = AppColors.BgInput,
                ForeColor = AppColors.TextPrimary,
                BorderStyle = BorderStyle.None,
                Margin = new Padding(0),
                TabStop = false
            };
            var pnlDescription = new RoundedInputPanel(txtDescription)
            {
                Height = 120,
                Margin = new Padding(0, 0, 0, 8)
            };

            lblDownloadTitle = new Label { AutoSize = true, Font = MetaTheme.Fonts.BodyMdBold(), Text = "Download bài tập:", Dock = DockStyle.Top, Padding = new Padding(0, 8, 0, 4) };

            btnDownloadTeacherFile = new Button
            {
                Size = new Size(200, 35),
                Text = "Tải đề bài đính kèm",
                Font = MetaTheme.Fonts.BodyMd(),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Dock = DockStyle.Top
            };
            btnDownloadTeacherFile.Tag = "secondary";
            btnDownloadTeacherFile.Click += BtnDownloadTeacherFile_Click;
            RoundedButtonHelper.Apply(8, btnDownloadTeacherFile);

            lblUploadTitle = new Label { AutoSize = true, Font = MetaTheme.Fonts.BodyMdBold(), Text = "Bài làm của bạn (Tối đa 10MB):", Dock = DockStyle.Top, Padding = new Padding(0, 8, 0, 4) };

            // File selection row
            var fileRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 38,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 8)
            };
            fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120f));

            txtSelectedFile = new TextBox
            {
                ReadOnly = true,
                Font = MetaTheme.Fonts.BodyMd(),
                BackColor = AppColors.BgInput,
                ForeColor = AppColors.TextPrimary,
                BorderStyle = BorderStyle.None,
                Margin = new Padding(0)
            };
            var pnlSelectedFile = new RoundedInputPanel(txtSelectedFile)
            {
                Dock = DockStyle.Fill,
                Height = 36,
                Margin = new Padding(0)
            };

            btnSelectFile = new Button
            {
                Dock = DockStyle.Fill,
                Text = "Chọn file...",
                Font = MetaTheme.Fonts.BodyMd(),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 0, 0, 0)
            };
            btnSelectFile.Tag = "secondary";
            btnSelectFile.Click += BtnSelectFile_Click;
            RoundedButtonHelper.Apply(8, btnSelectFile);

            fileRow.Controls.Add(pnlSelectedFile, 0, 0);
            fileRow.Controls.Add(btnSelectFile, 1, 0);

            // Build content using FlowLayoutPanel for auto-layout
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = AppColors.BgCard,
                Padding = new Padding(4)
            };

            flow.Resize += (s, e) =>
            {
                int w = flow.ClientSize.Width - flow.Padding.Horizontal;
                if (w > 0)
                {
                    lblTitle.MaximumSize = new Size(w, 0);
                    lblCourse.MaximumSize = new Size(w, 0);
                    lblDueDate.MaximumSize = new Size(w, 0);
                    _statusBanner.Width = w;
                    _submissionSummary.Width = w;
                    _submissionSummary.MaximumSize = new Size(w, 0);
                    _feedbackPanel.Width = w;
                    lblDescriptionTitle.MaximumSize = new Size(w, 0);
                    pnlDescription.Width = w;
                    lblDownloadTitle.MaximumSize = new Size(w, 0);
                    btnDownloadTeacherFile.Width = w;
                    lblUploadTitle.MaximumSize = new Size(w, 0);
                    fileRow.Width = w;
                }
            };

            flow.Controls.Add(lblTitle);
            flow.Controls.Add(lblCourse);
            flow.Controls.Add(lblDueDate);
            flow.Controls.Add(_statusBanner);
            flow.Controls.Add(_submissionSummary);
            flow.Controls.Add(_feedbackPanel);
            flow.Controls.Add(lblDescriptionTitle);
            flow.Controls.Add(pnlDescription);
            flow.Controls.Add(lblDownloadTitle);
            flow.Controls.Add(btnDownloadTeacherFile);
            flow.Controls.Add(lblUploadTitle);
            flow.Controls.Add(fileRow);

            ContentPanel.Controls.Add(flow);

            _submitStatus = new Label
            {
                AutoSize = false,
                Height = 24,
                Dock = DockStyle.Bottom,
                Font = AppFonts.Caption,
                ForeColor = AppColors.TextSecondary,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleRight,
                UseCompatibleTextRendering = false
            };
            ContentPanel.Controls.Add(_submitStatus);

            // Footer buttons
            btnSubmit = new Button
            {
                Text = "Nộp bài",
                Font = MetaTheme.Fonts.ButtonMd(),
                Width = 132,
                Cursor = Cursors.Hand
            };
            btnSubmit.Tag = "primary";
            btnSubmit.Click += async (s, e) => await BtnSubmit_Click();

            btnCancel = new Button
            {
                Text = "Đóng",
                Font = MetaTheme.Fonts.BodyMd(),
                Width = 100,
                Cursor = Cursors.Hand
            };
            btnCancel.Tag = "secondary";
            btnCancel.Click += (s, e) => this.Close();

            AddFooterButtons(btnCancel, btnSubmit);
            RoundedButtonHelper.Apply(8, btnSubmit, btnCancel);
            AcceptButton = btnSubmit;
            CancelButton = btnCancel;

            Load += (s, e) =>
            {
                btnCancel.Focus();
            };
        }

        private void ApplyStyle()
        {
            btnSubmit.Enabled = _presentation.CanSubmit;
            btnSelectFile.Enabled = _presentation.CanSubmit;
            btnSubmit.Text = _presentation.ActionText;
        }

        private void LoadData()
        {
            lblTitle.Text = _assignment.Title;
            lblCourse.Text = $"Khóa học: {_assignment.CourseName}";
            lblDueDate.Text = $"Hạn nộp: {_assignment.DueDate:dd/MM/yyyy HH:mm}";

            if (string.IsNullOrWhiteSpace(_assignment.Description))
            {
                lblDescriptionTitle.Visible = false;
                txtDescription.Visible = false;
            }
            else
            {
                txtDescription.Text = _assignment.Description;
            }

            if (_assignment.HasTeacherFile)
            {
                lblDownloadTitle.Visible = true;
                btnDownloadTeacherFile.Visible = true;
                btnDownloadTeacherFile.Enabled = true;
                btnDownloadTeacherFile.Text = $"Tải đính kèm ({_assignment.TeacherFileName})";
            }
            else
            {
                lblDownloadTitle.Visible = false;
                btnDownloadTeacherFile.Visible = false;
            }

            if (_assignment.IsSubmitted)
            {
                txtSelectedFile.Text = _assignment.StudentFileName;
                lblUploadTitle.Text = $"Đã nộp lúc: {_assignment.SubmittedAt:dd/MM/yyyy HH:mm}";
            }

            string submittedAtText = _assignment.SubmittedAt.HasValue
                ? _assignment.SubmittedAt.Value.ToString("dd/MM/yyyy HH:mm")
                : "không rõ thời gian";
            _submissionSummary.Text = _assignment.IsSubmitted
                ? $"Bài đã nộp: {_assignment.StudentFileName} - {submittedAtText}"
                : "Chưa có bài nộp.";

            if (_presentation.ShowsFeedback)
            {
                _feedbackPanel.Visible = true;
                _feedbackText.Text = $"Điểm: {_assignment.Score.GetValueOrDefault():0.##}/10\nNhận xét: {_assignment.Feedback}";
            }

            btnSubmit.Enabled = _presentation.CanSubmit;
            btnSelectFile.Enabled = _presentation.CanSubmit;
            btnSubmit.Text = _presentation.ActionText;
        }

        private async void BtnDownloadTeacherFile_Click(object? sender, EventArgs e)
        {
            try
            {
                btnDownloadTeacherFile.Enabled = false;
                byte[]? content = await _dbContext.GetAssignmentContentAsync(_assignment.AssignmentId, _studentId);
                
                if (content == null || content.Length == 0)
                {
                    MetaTheme.ShowModernDialog("Không thể tải file đính kèm từ máy chủ.", "Lỗi");
                    return;
                }

                using var sfd = new SaveFileDialog
                {
                    FileName = _assignment.TeacherFileName,
                    Filter = "All Files (*.*)|*.*"
                };

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    await File.WriteAllBytesAsync(sfd.FileName, content);
                    MetaTheme.ShowModernDialog("Tải file thành công!", "Thông báo");
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = sfd.FileName,
                            UseShellExecute = true
                        });
                    }
                    catch { /* Ignore if no default app */ }
                }
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog($"Lỗi khi tải file: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnDownloadTeacherFile.Enabled = true;
            }
        }

        private void BtnSelectFile_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Chọn bài làm",
                Filter = "Tài liệu (*.pdf;*.doc;*.docx;*.txt)|*.pdf;*.doc;*.docx;*.txt|All files (*.*)|*.*"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                var fileInfo = new FileInfo(ofd.FileName);
                if (fileInfo.Length > 10 * 1024 * 1024)
                {
                    MetaTheme.ShowModernDialog("File bài làm không được vượt quá 10MB.", "Cảnh báo");
                    return;
                }

                _selectedFilePath = ofd.FileName;
                txtSelectedFile.Text = fileInfo.Name;
                _selectedFileContent = File.ReadAllBytes(ofd.FileName);
            }
        }

        private async Task BtnSubmit_Click()
        {
            if (!_presentation.CanSubmit)
            {
                _submitStatus.Text = "Bài nộp hiện chỉ có thể xem.";
                return;
            }

            if (_selectedFileContent == null && !_assignment.IsSubmitted)
            {
                _submitStatus.Text = "Vui lòng chọn file bài làm trước khi nộp.";
                MetaTheme.ShowModernDialog("Vui lòng chọn file bài làm trước khi nộp.", "Cảnh báo");
                return;
            }

            if (_selectedFileContent == null && _assignment.IsSubmitted)
            {
                // Already submitted, but no new file selected
                _submitStatus.Text = "Bạn chưa chọn file mới để cập nhật bài làm.";
                MetaTheme.ShowModernDialog("Bạn chưa chọn file mới để cập nhật bài làm.", "Thông báo");
                return;
            }

            try
            {
                btnSubmit.Enabled = false;
                btnSubmit.Text = "Đang nộp...";
                _submitStatus.Text = "Đang tải bài làm lên...";

                var submission = new AssignmentSubmissionModel
                {
                    AssignmentId = _assignment.AssignmentId,
                    StudentId = _studentId,
                    FileName = Path.GetFileName(_selectedFilePath) ?? "submission.txt",
                    ContentType = Path.GetExtension(_selectedFilePath) ?? "",
                    FileSize = _selectedFileContent!.Length,
                    FileContent = _selectedFileContent
                };

                bool success = await _dbContext.SubmitAssignmentAsync(submission);

                if (success)
                {
                    _submitStatus.Text = "Đã nộp bài thành công.";

                    // Update in-app notification
                    var notifRepo = new NotificationRepository();
                    notifRepo.Create(
                        userId: _studentId,
                        title: "Nộp bài thành công",
                        content: $"Bạn đã nộp bài làm cho '{_assignment.Title}'.",
                        category: WorkflowConstants.NotificationCategory.Assignment,
                        notificationType: WorkflowConstants.NotificationType.Informational,
                        sourceType: "ASSIGNMENT",
                        sourceId: _assignment.AssignmentId
                    );

                    // Get student email via DB or session, fallback to generic
                    string studentEmail = UserSessionContext.CurrentUsername + "@student.edu.vn";
                    string assignmentTitle = _assignment.Title;

                    string emailBody = $"Chào bạn,\n\nBạn đã nộp bài làm thành công cho bài tập '{assignmentTitle}'.\n\nTrân trọng,\nCourseGuard";
                    EmailQueueService.QueueEmail(studentEmail, "Xác nhận nộp bài tập: " + assignmentTitle, emailBody);

                    MetaTheme.ShowModernDialog("Nộp bài thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    _submitStatus.Text = "Nộp bài chưa thành công.";
                    MetaTheme.ShowModernDialog("Nộp bài thất bại. Vui lòng thử lại sau.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnSubmit.Enabled = true;
                    btnSubmit.Text = _presentation.ActionText;
                }
            }
            catch (Exception ex)
            {
                _submitStatus.Text = "Nộp bài chưa thành công.";
                MetaTheme.ShowModernDialog($"Lỗi khi nộp bài: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnSubmit.Enabled = true;
                btnSubmit.Text = _presentation.ActionText;
            }
        }
    }
}
