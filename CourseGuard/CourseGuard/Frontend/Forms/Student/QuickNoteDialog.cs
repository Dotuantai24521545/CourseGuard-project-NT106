using System;
using System.Drawing;
using System.Windows.Forms;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls.Student;

namespace CourseGuard.Frontend.Forms.Student
{
    public class QuickNoteDialog : Form
    {
        private readonly int _userId;
        private readonly int _sessionId;
        private readonly CourseGuardDbContext _db;
        private TextBox _txtNote = null!;
        private Button _btnSave = null!;
        private Button _btnCancel = null!;

        public QuickNoteDialog(int userId, int sessionId)
        {
            _userId = userId;
            _sessionId = sessionId;
            _db = new CourseGuardDbContext("");
            InitializeComponent();
            ApplyMetaTheme();
            LoadNoteAsync();
        }

        private void InitializeComponent()
        {
            this.Text = "Ghi chú nhanh";
            this.Size = new Size(500, 350);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var lblTitle = new Label
            {
                Text = "Ghi chú cho buổi học này:",
                Font = MetaTheme.Fonts.HeadingSm(),
                Location = new Point(20, 20),
                AutoSize = true
            };

            _txtNote = new TextBox
            {
                Multiline = true,
                Location = new Point(20, 60),
                Size = new Size(440, 180),
                Font = MetaTheme.Fonts.SubtitleMd()
            };

            _btnSave = new Button
            {
                Text = "Lưu Ghi Chú",
                Location = new Point(200, 260),
                Size = new Size(120, 40)
            };
            _btnSave.Click += BtnSave_Click;

            _btnCancel = new Button
            {
                Text = "Hủy",
                Location = new Point(340, 260),
                Size = new Size(120, 40)
            };
            _btnCancel.Click += (s, e) => this.Close();

            this.Controls.Add(lblTitle);
            this.Controls.Add(_txtNote);
            this.Controls.Add(_btnSave);
            this.Controls.Add(_btnCancel);
        }

        private void ApplyMetaTheme()
        {
            this.BackColor = AppColors.BgBase;
            foreach (Control c in this.Controls)
            {
                if (c is Label lbl) lbl.ForeColor = AppColors.TextPrimary;
            }
            
            _txtNote.BackColor = AppColors.BgElevated;
            _txtNote.ForeColor = AppColors.TextPrimary;
            DialogInputStyler.ApplyAdminStyle(this);

            StudentTabChrome.StylePrimaryButton(_btnSave);
            StudentTabChrome.StyleSecondaryButton(_btnCancel);
            RoundedButtonHelper.Apply(10, _btnSave, _btnCancel);
        }

        private async void LoadNoteAsync()
        {
            try
            {
                var note = await _db.GetQuickNoteAsync(_userId, _sessionId);
                if (note != null && !string.IsNullOrEmpty(note.Content))
                {
                    _txtNote.Text = note.Content;
                }
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog("Không thể tải ghi chú: " + ex.Message, "Lỗi");
            }
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            _btnSave.Enabled = false;
            try
            {
                var note = new QuickNoteModel
                {
                    UserId = _userId,
                    SessionId = _sessionId,
                    Content = _txtNote.Text.Trim()
                };
                await _db.SaveQuickNoteAsync(note);
                MetaTheme.ShowModernDialog("Lưu ghi chú thành công!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog("Lỗi lưu ghi chú: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _btnSave.Enabled = true;
            }
        }
    }
}
