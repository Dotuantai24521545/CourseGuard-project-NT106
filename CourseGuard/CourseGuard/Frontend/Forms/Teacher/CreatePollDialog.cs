using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.Forms.Teacher
{
    public sealed class CreatePollDialog : Form
    {
        private const int MinOptions = 2;
        private const int MaxOptions = 6;
        private const int QuestionMaxLength = 200;

        private readonly TextBox _questionBox = new();
        private readonly Label _counterLabel = new();
        private readonly FlowLayoutPanel _optionsPanel = new();
        private readonly Label _validationLabel = new();
        private readonly Button _addOptionButton = new();
        private readonly Button _cancelButton = new();
        private readonly Button _createButton = new();
        private readonly List<TextBox> _optionBoxes = new();

        private readonly Color _surface = AppColors.BgCard;
        private readonly Color _text = AppColors.TextPrimary;
        private readonly Color _muted = AppColors.TextSecondary;
        private readonly Color _border = AppColors.BorderStrong;
        private readonly Color _optionFill = AppColors.BgInput;
        private readonly Color _primary = AppColors.AccentBlue;
        private readonly Color _primaryDisabled = AppColors.IsDarkMode ? Color.FromArgb(51, 91, 145) : Color.FromArgb(190, 216, 255);
        private readonly Color _cancelFill = AppColors.IsDarkMode ? AppColors.BgCardHover : Color.FromArgb(241, 245, 249);

        public string PollQuestion => _questionBox.Text.Trim();

        public IReadOnlyList<string> PollOptions => _optionBoxes
            .Select(box => box.Text.Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        public CreatePollDialog()
        {
            Text = "Tạo bình chọn";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Width = 616;
            Height = 594;
            BackColor = _surface;
            ForeColor = _text;
            Font = new Font("Segoe UI", 10f, FontStyle.Regular);

            BuildLayout();
            AddOptionBox();
            AddOptionBox();
            DialogInputStyler.ApplyAdminStyle(this);
            ValidateForm();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = _surface,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86f));

            root.Controls.Add(CreateHeader(), 0, 0);
            root.Controls.Add(CreateBody(), 0, 1);
            root.Controls.Add(CreateFooter(), 0, 2);
            Controls.Add(root);
        }

        private Control CreateHeader()
        {
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = _surface,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(20, 0, 16, 0)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42f));

            header.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Tạo bình chọn",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = _text,
                BackColor = _surface,
                Font = new Font("Segoe UI Semibold", 13f, FontStyle.Regular)
            }, 0, 0);

            var close = new Button
            {
                Dock = DockStyle.Fill,
                Text = "×",
                FlatStyle = FlatStyle.Flat,
                BackColor = _surface,
                ForeColor = _text,
                Font = new Font("Segoe UI", 24f, FontStyle.Regular),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 8, 0, 8)
            };
            close.FlatAppearance.BorderSize = 0;
            close.FlatAppearance.MouseOverBackColor = AppColors.BgCardHover;
            close.FlatAppearance.MouseDownBackColor = AppColors.BgElevated;
            close.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            header.Controls.Add(close, 1, 0);
            header.Paint += (_, e) => DrawBottomLine(e.Graphics, header.Width, 59);
            return header;
        }

        private Control CreateBody()
        {
            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = _surface,
                ColumnCount = 1,
                RowCount = 7,
                Padding = new Padding(20, 18, 20, 0)
            };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 132f));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 112f));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, TeacherCourseDialog.DefaultInputRowHeight));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            body.Controls.Add(CreateSectionLabel("Chủ đề bình chọn"), 0, 0);
            body.Controls.Add(CreateQuestionBox(), 0, 1);
            body.Controls.Add(CreateSectionLabel("Các lựa chọn"), 0, 2);

            _optionsPanel.Dock = DockStyle.Fill;
            _optionsPanel.BackColor = _surface;
            _optionsPanel.FlowDirection = FlowDirection.TopDown;
            _optionsPanel.WrapContents = false;
            _optionsPanel.AutoScroll = true;
            _optionsPanel.Margin = Padding.Empty;
            body.Controls.Add(_optionsPanel, 0, 3);

            _addOptionButton.Text = "+ Thêm lựa chọn";
            _addOptionButton.Dock = DockStyle.Left;
            _addOptionButton.Width = 178;
            _addOptionButton.FlatStyle = FlatStyle.Flat;
            _addOptionButton.FlatAppearance.BorderSize = 0;
            _addOptionButton.BackColor = _surface;
            _addOptionButton.ForeColor = _primary;
            _addOptionButton.Font = new Font("Segoe UI Semibold", 11f, FontStyle.Regular);
            _addOptionButton.Cursor = Cursors.Hand;
            _addOptionButton.Margin = new Padding(0, 6, 0, 0);
            _addOptionButton.TextAlign = ContentAlignment.MiddleLeft;
            _addOptionButton.Click += (_, _) => AddOptionBox();
            body.Controls.Add(_addOptionButton, 0, 4);

            _validationLabel.Dock = DockStyle.Fill;
            _validationLabel.ForeColor = AppColors.Danger;
            _validationLabel.BackColor = _surface;
            _validationLabel.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            _validationLabel.TextAlign = ContentAlignment.MiddleLeft;
            body.Controls.Add(_validationLabel, 0, 5);
            return body;
        }

        private Control CreateQuestionBox()
        {
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = _surface,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(12, 12, 12, 28)
            };
            host.Paint += (_, e) => DrawRoundedBorder(e.Graphics, host.ClientRectangle, 5, _surface, _border);

            _questionBox.BorderStyle = BorderStyle.None;
            _questionBox.Multiline = true;
            _questionBox.ScrollBars = ScrollBars.None;
            _questionBox.MaxLength = QuestionMaxLength;
            _questionBox.PlaceholderText = "Đặt câu hỏi bình chọn";
            _questionBox.Dock = DockStyle.Fill;
            _questionBox.BackColor = _surface;
            _questionBox.ForeColor = _text;
            _questionBox.Font = new Font("Segoe UI", 12f, FontStyle.Regular);
            _questionBox.TextChanged += (_, _) =>
            {
                _counterLabel.Text = $"{_questionBox.TextLength}/{QuestionMaxLength}";
                ValidateForm();
            };

            _counterLabel.AutoSize = false;
            _counterLabel.Dock = DockStyle.Bottom;
            _counterLabel.Height = 22;
            _counterLabel.Text = $"0/{QuestionMaxLength}";
            _counterLabel.TextAlign = ContentAlignment.MiddleRight;
            _counterLabel.ForeColor = _text;
            _counterLabel.BackColor = _surface;
            _counterLabel.Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            host.Controls.Add(_questionBox);
            host.Controls.Add(_counterLabel);
            return host;
        }

        private Control CreateFooter()
        {
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = _surface,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(20, 18, 20, 18)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52f));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88f));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180f));
            footer.Paint += (_, e) => DrawTopLine(e.Graphics, footer.Width, 0);

            var settings = new Button
            {
                Dock = DockStyle.Fill,
                Text = "⚙",
                BackColor = _surface,
                ForeColor = _text,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Symbol", 21f, FontStyle.Regular),
                Cursor = Cursors.Hand,
                Margin = new Padding(0)
            };
            settings.FlatAppearance.BorderSize = 0;
            settings.FlatAppearance.MouseOverBackColor = AppColors.BgCardHover;
            settings.FlatAppearance.MouseDownBackColor = AppColors.BgElevated;
            footer.Controls.Add(settings, 0, 0);

            _cancelButton.Text = "Hủy";
            _cancelButton.Dock = DockStyle.Fill;
            _cancelButton.BackColor = _cancelFill;
            _cancelButton.ForeColor = _text;
            _cancelButton.FlatStyle = FlatStyle.Flat;
            _cancelButton.FlatAppearance.BorderSize = 0;
            _cancelButton.FlatAppearance.MouseOverBackColor = AppColors.BgCardHover;
            _cancelButton.FlatAppearance.MouseDownBackColor = AppColors.BgElevated;
            _cancelButton.Font = new Font("Segoe UI Semibold", 11f, FontStyle.Regular);
            _cancelButton.Cursor = Cursors.Hand;
            _cancelButton.Margin = new Padding(0, 0, 12, 0);
            RoundedButtonHelper.Apply(_cancelButton, 4);
            _cancelButton.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            footer.Controls.Add(_cancelButton, 2, 0);

            _createButton.Text = "Tạo bình chọn";
            _createButton.Dock = DockStyle.Fill;
            _createButton.ForeColor = Color.White;
            _createButton.BackColor = _primary;
            _createButton.FlatStyle = FlatStyle.Flat;
            _createButton.FlatAppearance.BorderSize = 0;
            _createButton.FlatAppearance.MouseOverBackColor = AppColors.AccentHover;
            _createButton.FlatAppearance.MouseDownBackColor = AppColors.AccentPressed;
            _createButton.Font = new Font("Segoe UI Semibold", 11f, FontStyle.Regular);
            _createButton.Cursor = Cursors.Hand;
            _createButton.Margin = new Padding(4, 0, 0, 0);
            RoundedButtonHelper.Apply(_createButton, 4);
            _createButton.Click += (_, _) =>
            {
                if (!ValidateForm())
                {
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            };
            footer.Controls.Add(_createButton, 3, 0);

            AcceptButton = _createButton;
            CancelButton = _cancelButton;
            return footer;
        }

        private Label CreateSectionLabel(string text)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Text = text,
                ForeColor = _text,
                BackColor = _surface,
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private void AddOptionBox()
        {
            if (_optionBoxes.Count >= MaxOptions)
            {
                return;
            }

            int number = _optionBoxes.Count + 1;
            var box = new TextBox
            {
                Width = 560,
                Height = 48,
                Margin = new Padding(0, 0, 0, 12),
                PlaceholderText = $"Lựa chọn {number}",
                MaxLength = 180,
                BorderStyle = BorderStyle.None,
                BackColor = _optionFill,
                ForeColor = _text,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Regular),
                Padding = new Padding(12, 0, 12, 0)
            };
            box.TextChanged += (_, _) => ValidateForm();
            _optionBoxes.Add(box);
            _optionsPanel.Controls.Add(DialogInputStyler.CreateInputHost(box));
            RenumberOptions();
            ValidateForm();
            box.Focus();
        }

        private void RenumberOptions()
        {
            for (int i = 0; i < _optionBoxes.Count; i++)
            {
                _optionBoxes[i].PlaceholderText = $"Lựa chọn {i + 1}";
                _optionBoxes[i].BackColor = _optionFill;
            }

            _addOptionButton.Enabled = _optionBoxes.Count < MaxOptions;
            _addOptionButton.ForeColor = _addOptionButton.Enabled ? _primary : _muted;
        }

        private bool ValidateForm()
        {
            string question = _questionBox.Text.Trim();
            List<string> options = _optionBoxes
                .Select(box => box.Text.Trim())
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();
            int distinctCount = options.Distinct(StringComparer.OrdinalIgnoreCase).Count();

            string error = string.Empty;
            if (string.IsNullOrWhiteSpace(question))
            {
                error = string.Empty;
            }
            else if (distinctCount < MinOptions)
            {
                error = "Cần ít nhất 2 lựa chọn khác nhau.";
            }
            else if (distinctCount > MaxOptions)
            {
                error = "Chỉ hỗ trợ tối đa 6 lựa chọn.";
            }
            else if (options.Count != distinctCount)
            {
                error = "Các lựa chọn không được trùng nhau.";
            }

            bool valid = !string.IsNullOrWhiteSpace(question)
                && distinctCount >= MinOptions
                && distinctCount <= MaxOptions
                && options.Count == distinctCount;

            _validationLabel.Text = error;
            _createButton.Enabled = valid;
            _createButton.BackColor = valid ? _primary : _primaryDisabled;
            _createButton.Cursor = valid ? Cursors.Hand : Cursors.Default;
            return valid;
        }

        private void DrawRoundedBorder(Graphics graphics, Rectangle rectangle, int radius, Color fill, Color border)
        {
            using var path = GraphicsHelpers.RoundedRect(new Rectangle(rectangle.X, rectangle.Y, rectangle.Width - 1, rectangle.Height - 1), radius);
            using var fillBrush = new SolidBrush(fill);
            using var pen = new Pen(border, 1f);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.FillPath(fillBrush, path);
            graphics.DrawPath(pen, path);
        }

        private void DrawBottomLine(Graphics graphics, int width, int y)
        {
            using var pen = new Pen(_border, 1f);
            graphics.DrawLine(pen, 0, y, width, y);
        }

        private void DrawTopLine(Graphics graphics, int width, int y)
        {
            using var pen = new Pen(_border, 1f);
            graphics.DrawLine(pen, 0, y, width, y);
        }
    }
}
