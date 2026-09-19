using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.Forms.Teacher
{
    public partial class TeacherExamDialog : ThemedDialogBase
    {
        private readonly ComboBox _course = new();
        private readonly DarkTextInput _title = new();
        private readonly ComboBox _status = new();
        private readonly DarkDateTimePicker _openTime = new();
        private readonly DarkDateTimePicker _closeTime = new();
        private readonly DarkNumericInput _duration = new();
        private readonly DarkNumericInput _maxAttempts = new();
        private readonly DarkNumericInput _maxViolations = new();
        private readonly bool _lockCourseSelection;
        private readonly Label _courseScopeLabel = new();
        private Label _validationSummary = null!;

        public int CourseId => _course.SelectedItem is TeacherCourseModel course ? course.Id : 0;
        public string ItemTitle => _title.Text.Trim();
        public string Status => _status.SelectedItem?.ToString() ?? WorkflowConstants.ExamStatus.Draft;
        public DateTime? OpenTime => _openTime.Checked ? _openTime.Value : null;
        public DateTime? CloseTime => _closeTime.Checked ? _closeTime.Value : null;
        public int DurationMinutes => _duration.Value;
        public int MaxAttempts => _maxAttempts.Value;
        public int MaxViolations => Math.Max(0, _maxViolations.Value);

        public TeacherExamDialog(
            IEnumerable<TeacherCourseModel> courses,
            TeacherExamModel? existing = null,
            int selectedCourseId = 0,
            bool lockCourseSelection = false)
        {
            List<TeacherCourseModel> courseList = courses.ToList();
            _lockCourseSelection = lockCourseSelection && (selectedCourseId > 0 || existing?.CourseId > 0);
            Text = existing == null ? "Thêm bài kiểm tra" : "Sửa bài kiểm tra";
            Width = 700;
            Height = 500;

            _course.DropDownStyle = ComboBoxStyle.DropDownList;
            _course.DisplayMember = nameof(TeacherCourseModel.Name);
            foreach (var course in courseList)
                _course.Items.Add(course);

            _status.DropDownStyle = ComboBoxStyle.DropDownList;
            ConfigureStatusOptions(existing);
            _validationSummary = new Label
            {
                Text = "Tạo nháp bài kiểm tra, thêm câu hỏi, sau đó kích hoạt từ danh sách.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = AppColors.TextSecondary,
                Font = AppFonts.Semibold(9f),
                BackColor = AppColors.BgCard,
                AutoSize = false,
                Padding = new Padding(0, 0, 0, 8)
            };
            
            if (existing != null)
            {
                SelectCourse(existing.CourseId);
                _title.Text = existing.Title;
                if (existing.OpenTime.HasValue) _openTime.Value = existing.OpenTime.Value;
                else _openTime.Clear();
                
                if (existing.CloseTime.HasValue) _closeTime.Value = existing.CloseTime.Value;
                else _closeTime.Clear();

                _duration.Value = existing.DurationMinutes > 0 ? existing.DurationMinutes : 60;
                _maxAttempts.Value = existing.MaxAttempts > 0 ? existing.MaxAttempts : 1;
                _maxViolations.Value = Math.Max(0, existing.MaxViolations);
                SelectStatus(existing.Status);
            }
            else
            {
                SelectCourse(selectedCourseId);
                _closeTime.Value = DateTime.Now.AddHours(1);
                _openTime.Value = DateTime.Now;
                _duration.Value = 60;
                _maxAttempts.Value = 1;
                _maxViolations.Value = 0;
                SelectStatus(WorkflowConstants.ExamStatus.Draft);
            }

            var save = new Button { Text = "Lưu", DialogResult = DialogResult.OK, Width = 100, Cursor = Cursors.Hand };
            var cancel = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel, Width = 100, Cursor = Cursors.Hand };
            
            save.Tag = "primary";
            cancel.Tag = "secondary";
            
            save.Click += (s, e) =>
            {
                ResetValidationSummary();
                if (CourseId <= 0 || string.IsNullOrWhiteSpace(ItemTitle))
                {
                    ShowValidationSummary("Vui lòng chọn khóa học và nhập tiêu đề.");
                    DialogResult = DialogResult.None;
                    return;
                }
                if (_openTime.Checked && _closeTime.Checked && _closeTime.Value <= _openTime.Value)
                {
                    ShowValidationSummary("Thời gian đóng phải sau thời gian mở.");
                    DialogResult = DialogResult.None;
                    return;
                }
            };

            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(0) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            for (int i = 0; i < 4; i++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, TeacherCourseDialog.DefaultInputRowHeight));

            _title.Placeholder = "Nhập tiêu đề bài kiểm tra...";

            grid.Controls.Add(CreateLabel("Khóa học *"), 0, 0);
            grid.Controls.Add(CreateCourseScopeDisplay(), 1, 0);
            grid.Controls.Add(CreateLabel("Thời gian mở"), 2, 0);
            grid.Controls.Add(_openTime, 3, 0);

            grid.Controls.Add(CreateLabel("Tiêu đề *"), 0, 1);
            grid.Controls.Add(StyleInput(_title), 1, 1);
            grid.Controls.Add(CreateLabel("Thời gian đóng"), 2, 1);
            grid.Controls.Add(_closeTime, 3, 1);

            grid.Controls.Add(CreateLabel("Trạng thái *"), 0, 2);
            grid.Controls.Add(StyleInput(_status), 1, 2);
            grid.Controls.Add(CreateLabel("Thời lượng (phút) *"), 2, 2);
            grid.Controls.Add(_duration, 3, 2);

            grid.Controls.Add(CreateLabel("Vi phạm tối đa\n(0 = vô hạn)"), 0, 3);
            grid.Controls.Add(_maxViolations, 1, 3);
            grid.Controls.Add(CreateLabel("Số lần làm bài *"), 2, 3);
            grid.Controls.Add(_maxAttempts, 3, 3);

            var shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(0),
                BackColor = AppColors.BgCard
            };
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.Controls.Add(_validationSummary, 0, 0);
            shell.Controls.Add(grid, 0, 1);

            ContentPanel.Controls.Add(shell);
            AddFooterButtons(cancel, save);
            
            AcceptButton = save;
            CancelButton = cancel;

            RoundedButtonHelper.Apply(8, save, cancel);
        }

        private void SelectCourse(int courseId)
        {
            int idx = _course.Items.Cast<TeacherCourseModel>().ToList().FindIndex(c => c.Id == courseId);
            if (idx >= 0)
                _course.SelectedIndex = idx;
            else if (_course.Items.Count > 0 && _course.SelectedIndex < 0)
                _course.SelectedIndex = 0;

            RefreshCourseScopeLabel();
        }

        private Control CreateCourseScopeDisplay()
        {
            var host = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                FillColor = AppColors.BgInput,
                BorderColor = AppColors.BorderStrong,
                CornerRadius = 8,
                BackColor = AppColors.BgCard,
                Padding = new Padding(12, 0, 12, 0),
                Margin = new Padding(0, 4, 0, 4)
            };

            _course.Visible = !_lockCourseSelection;
            _course.Dock = DockStyle.Fill;

            _courseScopeLabel.Dock = DockStyle.Fill;
            _courseScopeLabel.TextAlign = ContentAlignment.MiddleLeft;
            _courseScopeLabel.Font = MetaTheme.Fonts.BodyMd();
            _courseScopeLabel.ForeColor = AppColors.TextPrimary;
            _courseScopeLabel.BackColor = AppColors.BgInput;
            _courseScopeLabel.Visible = _lockCourseSelection;
            RefreshCourseScopeLabel();

            if (_lockCourseSelection)
            {
                host.Controls.Add(_courseScopeLabel);
                return host;
            }

            return StyleInput(_course);
        }

        private void RefreshCourseScopeLabel()
        {
            _courseScopeLabel.Text = _course.SelectedItem?.ToString() ?? string.Empty;
        }

        private Label CreateLabel(string text)
        {
            return new Label 
            { 
                Text = text, 
                Dock = DockStyle.Fill, 
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = AppColors.TextSecondary,
                Font = AppFonts.Semibold(9f),
                BackColor = AppColors.BgCard,
                Margin = new Padding(0, 4, 8, 4),
                AutoSize = false
            };
        }

        private Control StyleInput(Control c)
        {
            c.Dock = DockStyle.Fill;
            c.BackColor = AppColors.BgInput;
            c.ForeColor = AppColors.TextPrimary;
            c.Font = MetaTheme.Fonts.BodyMd();
            c.Margin = new Padding(0, 6, 0, 6);
            DialogInputStyler.StyleExistingControl(c);
            return c;
        }

        private void ConfigureStatusOptions(TeacherExamModel? existing)
        {
            _status.Items.Clear();
            if (existing == null)
            {
                _status.Items.Add(WorkflowConstants.ExamStatus.Draft);
                return;
            }

            string status = (existing.Status ?? WorkflowConstants.ExamStatus.Draft).Trim().ToUpperInvariant();
            if (status == WorkflowConstants.ExamStatus.Draft)
            {
                _status.Items.Add(WorkflowConstants.ExamStatus.Draft);
                _status.Items.Add(WorkflowConstants.ExamStatus.Closed);
                return;
            }

            if (status == WorkflowConstants.ExamStatus.Active)
            {
                _status.Items.Add(WorkflowConstants.ExamStatus.Active);
                _status.Items.Add(WorkflowConstants.ExamStatus.Closed);
                return;
            }

            _status.Items.Add(WorkflowConstants.ExamStatus.Closed);
        }

        private void SelectStatus(string? status)
        {
            string normalized = string.IsNullOrWhiteSpace(status)
                ? WorkflowConstants.ExamStatus.Draft
                : status.Trim().ToUpperInvariant();

            foreach (object item in _status.Items)
            {
                if (string.Equals(item.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    _status.SelectedItem = item;
                    return;
                }
            }

            if (_status.Items.Count > 0)
                _status.SelectedIndex = 0;
        }

        private void ResetValidationSummary()
        {
            _validationSummary.ForeColor = AppColors.TextSecondary;
            _validationSummary.Text = "Tạo nháp bài kiểm tra, thêm câu hỏi, sau đó kích hoạt từ danh sách.";
        }

        private void ShowValidationSummary(string message)
        {
            _validationSummary.ForeColor = AppColors.Danger;
            _validationSummary.Text = message;
        }
    }

    public class DarkTextInput : Panel
    {
        private readonly TextBox _inner = new TextBox();
        private bool _isHovered;
        private bool _isFocused;
        private const int CornerRadius = 8;

        public DarkTextInput()
        {
            Height = 36;
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            Margin = new Padding(0, 6, 0, 6);
            Padding = new Padding(12, 8, 12, 8);
            DoubleBuffered = true;

            _inner.BorderStyle = BorderStyle.None;
            _inner.BackColor = AppColors.BgInput;
            _inner.ForeColor = AppColors.TextPrimary;
            _inner.Font = MetaTheme.Fonts.BodyMd();
            _inner.Dock = DockStyle.Fill;
            _inner.GotFocus += (_, _) => { _isFocused = true; Invalidate(); };
            _inner.LostFocus += (_, _) => { _isFocused = false; Invalidate(); };

            Controls.Add(_inner);

            MouseEnter += (_, _) => { _isHovered = true; Invalidate(); };
            MouseLeave += (_, _) => { _isHovered = false; Invalidate(); };
            _inner.MouseEnter += (_, _) => { _isHovered = true; Invalidate(); };
            _inner.MouseLeave += (_, _) => { _isHovered = false; Invalidate(); };
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern Int32 SendMessage(IntPtr hWnd, int msg, int wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);
        private const int EM_SETCUEBANNER = 0x1501;
        private string _placeholder = "";

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string Placeholder
        {
            get => _placeholder;
            set
            {
                _placeholder = value;
                if (IsHandleCreated) SendMessage(_inner.Handle, EM_SETCUEBANNER, 1, _placeholder);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!string.IsNullOrEmpty(_placeholder))
                SendMessage(_inner.Handle, EM_SETCUEBANNER, 1, _placeholder);
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string Text
        {
            get => _inner.Text;
            set => _inner.Text = value;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using var bgPath = GraphicsHelpers.RoundedRectF(rect, CornerRadius);
            using var bgBrush = new SolidBrush(AppColors.BgInput);
            e.Graphics.FillPath(bgBrush, bgPath);

            Color borderColor = _isFocused ? AppColors.AccentBlue
                : _isHovered ? AppColors.BorderStrong
                : AppColors.Border;
            float borderWidth = _isFocused ? 1.5f : 1f;
            using var borderPen = new Pen(borderColor, borderWidth);
            e.Graphics.DrawPath(borderPen, bgPath);
        }
    }

    public class DarkNumericInput : Panel
    {
        private readonly TextBox _inner = new TextBox();
        private bool _isHovered;
        private bool _isFocused;
        private const int CornerRadius = 8;

        public DarkNumericInput()
        {
            Height = 36;
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            Margin = new Padding(0, 6, 0, 6);
            Padding = new Padding(12, 8, 12, 8);
            DoubleBuffered = true;

            _inner.BorderStyle = BorderStyle.None;
            _inner.BackColor = AppColors.BgInput;
            _inner.ForeColor = AppColors.TextPrimary;
            _inner.Font = MetaTheme.Fonts.BodyMd();
            _inner.Dock = DockStyle.Fill;
            _inner.KeyPress += (_, e) => {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                    e.Handled = true;
            };
            _inner.GotFocus += (_, _) => { _isFocused = true; Invalidate(); };
            _inner.LostFocus += (_, _) => { _isFocused = false; Invalidate(); };

            Controls.Add(_inner);

            MouseEnter += (_, _) => { _isHovered = true; Invalidate(); };
            MouseLeave += (_, _) => { _isHovered = false; Invalidate(); };
            _inner.MouseEnter += (_, _) => { _isHovered = true; Invalidate(); };
            _inner.MouseLeave += (_, _) => { _isHovered = false; Invalidate(); };
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int Value
        {
            get => int.TryParse(_inner.Text, out int v) ? v : 0;
            set => _inner.Text = value.ToString();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using var bgPath = GraphicsHelpers.RoundedRectF(rect, CornerRadius);
            using var bgBrush = new SolidBrush(AppColors.BgInput);
            e.Graphics.FillPath(bgBrush, bgPath);

            Color borderColor = _isFocused ? AppColors.AccentBlue
                : _isHovered ? AppColors.BorderStrong
                : AppColors.Border;
            float borderWidth = _isFocused ? 1.5f : 1f;
            using var borderPen = new Pen(borderColor, borderWidth);
            e.Graphics.DrawPath(borderPen, bgPath);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
        }
    }

    public class DarkDateTimePicker : Panel
    {
        private TextBox _textBox = new TextBox();
        private Panel _iconBtn = new Panel();
        private Panel _innerPanel = new Panel();
        private DateTime? _value;
        private bool _isHovered;
        private bool _isFocused;
        private const int CornerRadius = 8;

        public bool Checked => _value.HasValue;
        
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public DateTime Value 
        { 
            get => _value ?? DateTime.Now; 
            set { _value = value; UpdateText(); }
        }

        public DarkDateTimePicker()
        {
            Height = 36;
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            Margin = new Padding(0, 6, 0, 6);
            DoubleBuffered = true;
            
            _iconBtn = new Panel
            {
                Dock = DockStyle.Right,
                Width = 36,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            Controls.Add(_iconBtn);

            _innerPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(12, 8, 0, 8) };
            
            _textBox.BorderStyle = BorderStyle.None;
            _textBox.BackColor = AppColors.BgInput;
            _textBox.ForeColor = AppColors.TextPrimary;
            _textBox.Font = MetaTheme.Fonts.BodyMd();
            _textBox.Dock = DockStyle.Fill;
            _textBox.TextChanged += (s, e) => {
                if (DateTime.TryParseExact(_textBox.Text, "dd/MM/yyyy HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime dt))
                    _value = dt;
            };
            _textBox.GotFocus += (_, _) => { _isFocused = true; Invalidate(); };
            _textBox.LostFocus += (_, _) => { _isFocused = false; Invalidate(); };

            _iconBtn.Paint += (_, e) => {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var pen = new Pen(_isFocused ? AppColors.AccentBlue : AppColors.TextSecondary, 1.5f)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round
                };
                int cx = _iconBtn.Width / 2;
                int cy = _iconBtn.Height / 2;
                // Calendar icon
                var r = new Rectangle(cx - 7, cy - 7, 14, 14);
                e.Graphics.DrawRectangle(pen, r);
                e.Graphics.DrawLine(pen, r.Left, cy - 3, r.Right, cy - 3);
                e.Graphics.DrawLine(pen, cx - 3, r.Top - 2, cx - 3, r.Top + 2);
                e.Graphics.DrawLine(pen, cx + 3, r.Top - 2, cx + 3, r.Top + 2);
                // Dots for days
                using var dotBrush = new SolidBrush(_isFocused ? AppColors.AccentBlue : AppColors.TextSecondary);
                e.Graphics.FillEllipse(dotBrush, cx - 4, cy + 1, 3, 3);
                e.Graphics.FillEllipse(dotBrush, cx + 1, cy + 1, 3, 3);
                pen.Dispose();
            };
            _iconBtn.Click += Btn_Click;

            _innerPanel.Controls.Add(_textBox);
            Controls.Add(_innerPanel);

            MouseEnter += (_, _) => { _isHovered = true; Invalidate(); };
            MouseLeave += (_, _) => { _isHovered = false; Invalidate(); };
            _textBox.MouseEnter += (_, _) => { _isHovered = true; Invalidate(); };
            _textBox.MouseLeave += (_, _) => { _isHovered = false; Invalidate(); };
            _iconBtn.MouseEnter += (_, _) => { _isHovered = true; _iconBtn.Invalidate(); Invalidate(); };
            _iconBtn.MouseLeave += (_, _) => { _isHovered = false; _iconBtn.Invalidate(); Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using var bgPath = GraphicsHelpers.RoundedRectF(rect, CornerRadius);
            using var bgBrush = new SolidBrush(AppColors.BgInput);
            e.Graphics.FillPath(bgBrush, bgPath);

            Color borderColor = _isFocused ? AppColors.AccentBlue 
                : _isHovered ? AppColors.BorderStrong 
                : AppColors.Border;
            float borderWidth = _isFocused ? 1.5f : 1f;
            using var borderPen = new Pen(borderColor, borderWidth);
            e.Graphics.DrawPath(borderPen, bgPath);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width > 0 && Height > 0)
            {
                Region?.Dispose();
                using var path = GraphicsHelpers.RoundedRect(new Rectangle(-1, -1, Width + 2, Height + 2), CornerRadius + 1);
                Region = new Region(path);
            }
        }

        public void Clear()
        {
            _value = null;
            _textBox.Text = "";
        }

        private void UpdateText()
        {
            _textBox.Text = _value.HasValue ? _value.Value.ToString("dd/MM/yyyy HH:mm") : "";
        }

        private void Btn_Click(object? sender, EventArgs e)
        {
            using var popup = new Form { 
                StartPosition = FormStartPosition.Manual, 
                FormBorderStyle = FormBorderStyle.None, 
                Width = 260, Height = 280,
                Text = "Chọn ngày giờ",
                BackColor = AppColors.BgCard,
                ShowInTaskbar = false
            };
            popup.Location = PointToScreen(new Point(0, Height + 4));
            
            // Apply rounded region to popup
            popup.Paint += (_, pe) => {
                pe.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                GraphicsHelpers.DrawRoundedBorder(pe.Graphics, new Rectangle(0, 0, popup.Width - 1, popup.Height - 1), 12, AppColors.BorderStrong, 1f);
            };
            popup.Shown += (_, _) => {
                popup.Region?.Dispose();
                using var rp = GraphicsHelpers.RoundedRect(new Rectangle(-1, -1, popup.Width + 2, popup.Height + 2), 13);
                popup.Region = new Region(rp);
            };

            var dtp = new DateTimePicker { 
                Format = DateTimePickerFormat.Custom, 
                CustomFormat = "dd/MM/yyyy HH:mm", 
                Dock = DockStyle.Top,
                CalendarMonthBackground = AppColors.BgInput,
                CalendarForeColor = AppColors.TextPrimary,
                CalendarTitleBackColor = AppColors.BgCard,
                CalendarTitleForeColor = AppColors.TextPrimary
            };
            var mc = new MonthCalendar { Dock = DockStyle.Fill, MaxSelectionCount = 1 };
            
            if (_value.HasValue) {
                dtp.Value = _value.Value;
                mc.SetDate(_value.Value);
            }
            
            mc.DateChanged += (s, ev) => {
                dtp.Value = new DateTime(ev.Start.Year, ev.Start.Month, ev.Start.Day, dtp.Value.Hour, dtp.Value.Minute, 0);
            };
            
            var btnOk = new Button { Text = "Xác nhận", Dock = DockStyle.Bottom, DialogResult = DialogResult.OK, Height = 36 };
            btnOk.Tag = "primary";
            RoundedButtonHelper.Apply(8, btnOk);
            AppColors.ApplyTheme(popup);
            
            popup.Controls.Add(mc);
            popup.Controls.Add(dtp);
            popup.Controls.Add(btnOk);
            popup.AcceptButton = btnOk;
            
            // Close popup when clicking outside
            popup.Deactivate += (_, _) => {
                if (popup.Visible)
                {
                    popup.DialogResult = DialogResult.Cancel;
                    popup.Close();
                }
            };
            
            if (popup.ShowDialog() == DialogResult.OK)
                Value = dtp.Value;
        }
    }
}
