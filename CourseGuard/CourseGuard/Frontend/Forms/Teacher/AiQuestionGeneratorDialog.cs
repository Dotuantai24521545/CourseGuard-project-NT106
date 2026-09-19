using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Services;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls.Teacher;

namespace CourseGuard.Frontend.Forms.Teacher
{
    public class AiQuestionGeneratorDialog : ThemedDialogBase
    {
        private readonly int _teacherId;
        private readonly int _examId;
        private readonly int _courseId;
        private readonly TeacherController _controller = new(new CourseGuardDbContext(""));
        private readonly AiQuestionGeneratorService _aiService = new();

        private readonly TextBox _txtTopic = new() { Multiline = true, ScrollBars = ScrollBars.Vertical };
        private readonly NumericUpDown _numEasy = new() { Minimum = 0, Maximum = 20, Value = 3 };
        private readonly NumericUpDown _numMedium = new() { Minimum = 0, Maximum = 20, Value = 2 };
        private readonly NumericUpDown _numHard = new() { Minimum = 0, Maximum = 20, Value = 1 };
        private readonly Button _btnOpenFile = TeacherTabChrome.SecondaryButton("📁 Chọn file (.txt)");
        private readonly Button _btnGenerate = TeacherTabChrome.PrimaryButton("🤖 Sinh câu hỏi AI (Qwen)");
        private readonly Button _btnSaveSelected = TeacherTabChrome.PrimaryButton("💾 Thêm câu hỏi đã chọn vào bài thi");
        private readonly Button _btnClose = TeacherTabChrome.SecondaryButton("Đóng");
        private readonly Label _lblStatus = new() { AutoSize = true, ForeColor = Color.LightGray };
        private readonly DataGridView _grid = new();
        private readonly List<AiGeneratedQuestion> _generatedQuestions = new();

        public bool QuestionsAdded { get; private set; }

        public AiQuestionGeneratorDialog(int teacherId, int examId, int courseId)
        {
            _teacherId = teacherId;
            _examId = examId;
            _courseId = courseId;
            Text = "🤖 Tạo đề thi tự động bằng AI (OpenRouter Qwen)";
            Width = 1000;
            Height = 680;
            BuildLayout();
            WireEvents();
        }

        private void BuildLayout()
        {
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(0)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

            // Top Panel (Inputs & Controls)
            var topGroup = new GroupBox
            {
                Text = "Cấu hình đầu vào AI",
                Dock = DockStyle.Fill,
                ForeColor = AppColors.TextPrimary,
                Font = AppFonts.Semibold(10)
            };
            var topGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(8)
            };
            topGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            topGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            topGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            topGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, TeacherCourseDialog.DefaultInputRowHeight));

            _txtTopic.Dock = DockStyle.Fill;
            _txtTopic.PlaceholderText = "Dán nội dung bài giảng, tóm tắt giáo trình hoặc chủ đề kiến thức tại đây...";
            topGrid.Controls.Add(_txtTopic, 0, 0);

            var optionsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(6, 0, 0, 0)
            };
            optionsPanel.Controls.Add(new Label { Text = "Số câu Nhận biết (Easy):", AutoSize = true, Font = AppFonts.Body });
            optionsPanel.Controls.Add(_numEasy);
            optionsPanel.Controls.Add(new Label { Text = "Số câu Thông hiểu (Medium):", AutoSize = true, Font = AppFonts.Body });
            optionsPanel.Controls.Add(_numMedium);
            optionsPanel.Controls.Add(new Label { Text = "Số câu Vận dụng (Hard):", AutoSize = true, Font = AppFonts.Body });
            optionsPanel.Controls.Add(_numHard);
            topGrid.Controls.Add(optionsPanel, 1, 0);

            var actionFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = true
            };
            actionFlow.Controls.Add(_btnOpenFile);
            actionFlow.Controls.Add(_btnGenerate);
            topGrid.Controls.Add(actionFlow, 0, 1);
            topGrid.SetColumnSpan(actionFlow, 2);

            topGroup.Controls.Add(topGrid);
            mainLayout.Controls.Add(topGroup, 0, 0);

            // Middle Grid (Results)
            TeacherTabChrome.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            SetupGridColumns();
            mainLayout.Controls.Add(_grid, 0, 1);

            // Bottom Status
            mainLayout.Controls.Add(_lblStatus, 0, 2);

            ContentPanel.Controls.Add(mainLayout);
            AddFooterButtons(_btnClose, _btnSaveSelected);
            CancelButton = _btnClose;
        }

        private void SetupGridColumns()
        {
            _grid.Columns.Clear();
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Select", HeaderText = "Chọn", Width = 50 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Content", HeaderText = "Câu hỏi AI sinh ra", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Options", HeaderText = "Các lựa chọn A/B/C/D", Width = 260 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Correct", HeaderText = "Độ kho / Đáp án", Width = 130 });
        }

        private void WireEvents()
        {
            _btnClose.Click += (_, _) => Close();
            _btnOpenFile.Click += OpenFile_Click;
            _btnGenerate.Click += async (_, _) => await GenerateAiQuestionsAsync();
            _btnSaveSelected.Click += async (_, _) => await SaveSelectedQuestionsAsync();
        }

        private void OpenFile_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog { Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    _txtTopic.Text = File.ReadAllText(ofd.FileName);
                    _lblStatus.Text = $"Đã tải file thành công: {Path.GetFileName(ofd.FileName)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi đọc file: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async Task GenerateAiQuestionsAsync()
        {
            string topic = _txtTopic.Text.Trim();
            if (string.IsNullOrWhiteSpace(topic))
            {
                MessageBox.Show("Vui lòng nhập hoặc chọn file nội dung kiến thức trước khi sinh câu hỏi.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _btnGenerate.Enabled = false;
            _lblStatus.Text = "⏳ Đang kết nối OpenRouter Qwen AI để sinh câu hỏi... Vui lòng đợi trong giây lát.";
            _generatedQuestions.Clear();
            _grid.Rows.Clear();

            try
            {
                int easy = (int)_numEasy.Value;
                int medium = (int)_numMedium.Value;
                int hard = (int)_numHard.Value;

                var result = await _aiService.GenerateQuestionsAsync(topic, easy, medium, hard);
                if (result.Count == 0)
                {
                    _lblStatus.Text = "❌ AI không trả về câu hỏi hợp lệ. Vui lòng thử lại hoặc rút gọn nội dung.";
                    MessageBox.Show("Khởi tạo thất bại. Vui lòng kiểm tra kết nối mạng hoặc thử lại.", "Lỗi AI", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                _generatedQuestions.AddRange(result);
                foreach (var q in _generatedQuestions)
                {
                    string optionsStr = $"A. {q.OptionA} | B. {q.OptionB} | C. {q.OptionC} | D. {q.OptionD}";
                    string infoStr = $"[{q.Level}] - Đáp án {q.CorrectOption}";
                    _grid.Rows.Add(true, q.Content, optionsStr, infoStr);
                }

                _lblStatus.Text = $"✅ Đã sinh thành công {result.Count} câu hỏi từ OpenRouter AI!";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "❌ Lỗi thực thi sinh câu hỏi AI.";
                MessageBox.Show($"Lỗi AI: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnGenerate.Enabled = true;
            }
        }

        private async Task SaveSelectedQuestionsAsync()
        {
            var selectedList = new List<TeacherExamQuestionModel>();
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                var row = _grid.Rows[i];
                bool isChecked = Convert.ToBoolean(row.Cells["Select"].Value ?? false);
                if (isChecked && i < _generatedQuestions.Count)
                {
                    var q = _generatedQuestions[i];
                    selectedList.Add(new TeacherExamQuestionModel
                    {
                        ExamId = _examId,
                        QuestionText = q.Content,
                        OptionA = q.OptionA,
                        OptionB = q.OptionB,
                        OptionC = q.OptionC,
                        OptionD = q.OptionD,
                        CorrectOption = q.CorrectOption,
                        Difficulty = q.Level,
                        Points = 1.0m,
                        QuestionType = "MULTIPLE_CHOICE",
                        Chapter = "AI Generated"
                    });
                }
            }

            if (selectedList.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất một câu hỏi để lưu.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                int added = await _controller.ImportQuestionsToExamAsync(_teacherId, _examId, _courseId, selectedList);
                QuestionsAdded = true;
                MessageBox.Show($"Đã lưu thành công {added} câu hỏi AI vào bài thi!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu câu hỏi: {ex.Message}", "Lỗi CSDL", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
