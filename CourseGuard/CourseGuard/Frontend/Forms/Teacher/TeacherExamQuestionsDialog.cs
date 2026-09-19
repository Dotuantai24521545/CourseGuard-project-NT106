using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls.Teacher;

namespace CourseGuard.Frontend.Forms.Teacher
{
    public class TeacherExamQuestionsDialog : ThemedDialogBase
    {
        private const int QuestionActionRowHeight = 96;
        private const int EditorInputRowHeight = 52;

        private readonly int _teacherId;
        private readonly int _examId;
        private readonly TeacherController _controller = new(new CourseGuardDbContext(""));
        private readonly DataGridView _grid = new();
        private readonly TextBox _question = new();
        private readonly TextBox _a = new();
        private readonly TextBox _b = new();
        private readonly TextBox _c = new();
        private readonly TextBox _d = new();
        private readonly ComboBox _correct = new();
        private readonly Label _points = new();
        private Label _readinessSummary = null!;
        private Label _readOnlyHint = null!;
        private readonly Button _importExcel = TeacherTabChrome.SecondaryButton("Nhập từ file (Excel)");
        private readonly Button _questionBank = TeacherTabChrome.SecondaryButton("Ngân hàng câu hỏi");
        private readonly Button _add = TeacherTabChrome.PrimaryButton("Thêm câu");
        private readonly Button _save = TeacherTabChrome.SecondaryButton("Lưu sửa");
        private readonly Button _delete = TeacherTabChrome.DangerButton("Xóa câu");
        private readonly Button _close = TeacherTabChrome.SecondaryButton("Đóng");
        private readonly Button _randomBuilder = TeacherTabChrome.SecondaryButton("Tao ngau nhien");
        private readonly Button _aiGenerate = TeacherTabChrome.PrimaryButton("🤖 Tạo bằng AI");
        private bool _canEdit;
        private readonly int _courseId;

        public TeacherExamQuestionsDialog(int teacherId, int examId, string examTitle, int courseId)
        {
            _teacherId = teacherId;
            _examId = examId;
            _courseId = courseId;
            _canEdit = IsExamDraft();
            Text = $"Soạn câu hỏi - {examTitle}";
            Width = 1040;
            Height = 680;
            BuildLayout();
            WireEvents();
            RefreshEditState();
            LoadQuestions();
        }

        private void BuildLayout()
        {
            var shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var topArea = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(0, 0, 0, 10),
                BackColor = Color.Transparent
            };
            topArea.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            topArea.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            _readinessSummary = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = AppColors.TextPrimary,
                Font = AppFonts.Semibold(9f),
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };
            _readOnlyHint = new Label
            {
                Text = "Bài kiểm tra không còn ở trạng thái nháp nên chỉ có thể xem câu hỏi.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = AppColors.Warning,
                Font = AppFonts.Semibold(9f),
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                Visible = !_canEdit
            };
            topArea.Controls.Add(_readinessSummary, 0, 0);
            topArea.Controls.Add(_readOnlyHint, 0, 1);
            shell.Controls.Add(topArea, 0, 0);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));

            var leftPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent };
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, QuestionActionRowHeight));

            TeacherTabChrome.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            leftPanel.Controls.Add(_grid, 0, 0);

            var leftButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = true,
                Padding = new Padding(0, 10, 0, 0)
            };
            leftButtons.Controls.Add(_importExcel);
            leftButtons.Controls.Add(_questionBank);
            leftButtons.Controls.Add(_randomBuilder);
            leftButtons.Controls.Add(_aiGenerate);
            leftPanel.Controls.Add(leftButtons, 0, 1);

            root.Controls.Add(leftPanel, 0, 0);

            var editor = TeacherCourseDialog.CreateGrid();
            editor.RowCount = 8;
            editor.RowStyles.Clear();
            editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            for (int i = 0; i < 7; i++)
                editor.RowStyles.Add(new RowStyle(SizeType.Absolute, EditorInputRowHeight));

            _question.Multiline = true;
            _correct.DropDownStyle = ComboBoxStyle.DropDownList;
            _correct.Items.AddRange(new object[] { "A", "B", "C", "D" });
            _correct.SelectedIndex = 0;
            _points.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            AddRow(editor, 0, "Câu hỏi", _question);
            AddRow(editor, 1, "A", _a);
            AddRow(editor, 2, "B", _b);
            AddRow(editor, 3, "C", _c);
            AddRow(editor, 4, "D", _d);
            AddRow(editor, 5, "Đáp án đúng", _correct);
            AddRow(editor, 6, "Điểm/câu", _points);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = true,
                AutoScroll = true,
                Padding = new Padding(0, 8, 0, 0)
            };
            buttons.Controls.Add(_delete);
            buttons.Controls.Add(_save);
            buttons.Controls.Add(_add);
            editor.Controls.Add(buttons, 0, 7);
            editor.SetColumnSpan(buttons, 2);
            root.Controls.Add(editor, 1, 0);

            shell.Controls.Add(root, 0, 1);
            ContentPanel.Controls.Add(shell);
            AddFooterButtons(_close);
            CancelButton = _close;
        }

        private static void AddRow(TableLayoutPanel grid, int row, string label, Control control)
        {
            grid.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, row);
            control.Dock = DockStyle.Fill;
            grid.Controls.Add(control, 1, row);
        }

        private void WireEvents()
        {
            _grid.SelectionChanged += (_, _) => LoadSelectedQuestion();
            _add.Click += (_, _) => SaveNewQuestion();
            _save.Click += (_, _) => SaveExistingQuestion();
            _delete.Click += (_, _) => DeleteSelectedQuestion();
            _close.Click += (_, _) => Close();
            _importExcel.Click += async (_, _) => await ImportFromExcelAsync();
            _questionBank.Click += (_, _) => OpenQuestionBank();
            _randomBuilder.Click += (_, _) => OpenRandomBuilder();
            _aiGenerate.Click += (_, _) => OpenAiGenerator();
            ApplyEditState();
        }

        private bool IsExamDraft() =>
            string.Equals(_controller.GetExamStatus(_teacherId, _examId), WorkflowConstants.ExamStatus.Draft, StringComparison.OrdinalIgnoreCase);

        private bool RefreshEditState()
        {
            _canEdit = IsExamDraft();
            ApplyEditState();
            return _canEdit;
        }

        private void ApplyEditState()
        {
            _readOnlyHint.Visible = !_canEdit;
            _add.Enabled = _canEdit;
            _save.Enabled = _canEdit;
            _delete.Enabled = _canEdit;
            _importExcel.Enabled = _canEdit;
            _questionBank.Enabled = _canEdit;
            _randomBuilder.Enabled = _canEdit;
            _aiGenerate.Enabled = _canEdit;
            if (!_canEdit)
                ClearEditor();
        }

        private void ShowGuardedQuestionWriteFailure()
        {
            MetaTheme.ShowModernDialog("Bài kiểm tra không còn ở trạng thái nháp nên không thể chỉnh sửa câu hỏi.", "Không thể chỉnh sửa", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private async System.Threading.Tasks.Task ImportFromExcelAsync()
        {
            if (!RefreshEditState())
            {
                ShowGuardedQuestionWriteFailure();
                LoadQuestions();
                return;
            }

            using var ofd = new OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx;*.xls",
                Title = "Chọn file Excel mẫu",
                Multiselect = false
            };

            if (ofd.ShowDialog() != DialogResult.OK) return;

            try
            {
                _importExcel.Enabled = false;
                _importExcel.Text = "Đang nhập...";

                var questions = await _controller.ParseAndValidateExcelAsync(ofd.FileName);
                if (questions == null || questions.Count == 0)
                {
                    MetaTheme.ShowModernDialog("File không có dữ liệu hoặc không đúng định dạng.", "Lỗi nhập liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // In-memory data validation
                var errors = new System.Collections.Generic.List<string>();
                for (int i = 0; i < questions.Count; i++)
                {
                    var q = questions[i];
                    if (string.IsNullOrWhiteSpace(q.QuestionText) || 
                        string.IsNullOrWhiteSpace(q.OptionA) || string.IsNullOrWhiteSpace(q.OptionB) || 
                        string.IsNullOrWhiteSpace(q.OptionC) || string.IsNullOrWhiteSpace(q.OptionD))
                    {
                        errors.Add($"Dòng {i + 2}: Thiếu câu hỏi hoặc đáp án.");
                    }
                    if (string.IsNullOrWhiteSpace(q.CorrectOption) || !new[] { "A", "B", "C", "D" }.Contains(q.CorrectOption.Trim().ToUpperInvariant()))
                    {
                        errors.Add($"Dòng {i + 2}: Đáp án đúng phải là A, B, C hoặc D.");
                    }
                    if (q.Points <= 0)
                    {
                        errors.Add($"Dòng {i + 2}: Điểm số phải lớn hơn 0.");
                    }
                }

                if (errors.Count > 0)
                {
                    MetaTheme.ShowModernDialog("Dữ liệu không hợp lệ:\n" + string.Join("\n", errors.Take(5)) + (errors.Count > 5 ? "\n..." : ""), "Lỗi xác thực", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                int importedCount = await _controller.ImportQuestionsToExamAsync(_teacherId, _examId, _courseId, questions);
                if (importedCount <= 0)
                {
                    RefreshEditState();
                    LoadQuestions();
                    MetaTheme.ShowModernDialog("Bài kiểm tra không còn ở trạng thái nháp hoặc không thuộc khóa học đã chọn. Vui lòng tải lại danh sách câu hỏi.", "Không thể nhập câu hỏi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                MetaTheme.ShowModernDialog($"Nhập thành công {importedCount} câu hỏi!", "Hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadQuestions();
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog("Không thể đọc file. Vui lòng đóng file nếu đang mở.\nLỗi: " + ex.Message, "Lỗi đọc file", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _importExcel.Text = "Nhập từ file (Excel)";
                ApplyEditState();
            }
        }

        private void OpenQuestionBank()
        {
            if (!RefreshEditState())
            {
                ShowGuardedQuestionWriteFailure();
                LoadQuestions();
                return;
            }

            using var qbd = new QuestionBankDialog(_teacherId, _examId, _courseId);
            qbd.ShowDialog(this);
            if (qbd.QuestionsAdded)
            {
                RefreshEditState();
                LoadQuestions();
            }
        }

        private void OpenRandomBuilder()
        {
            if (!RefreshEditState())
            {
                ShowGuardedQuestionWriteFailure();
                LoadQuestions();
                return;
            }

            using var dialog = new RandomExamBuilderDialog(_teacherId, _examId, _courseId);
            dialog.ShowDialog(this);
            if (dialog.QuestionsAdded)
            {
                RefreshEditState();
                LoadQuestions();
            }
        }

        private void OpenAiGenerator()
        {
            if (!RefreshEditState())
            {
                ShowGuardedQuestionWriteFailure();
                LoadQuestions();
                return;
            }

            using var dialog = new AiQuestionGeneratorDialog(_teacherId, _examId, _courseId);
            dialog.ShowDialog(this);
            if (dialog.QuestionsAdded)
            {
                RefreshEditState();
                LoadQuestions();
            }
        }

        private void LoadQuestions()
        {
            DataTable table = new();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("STT", typeof(int));
            table.Columns.Add("Câu hỏi", typeof(string));
            table.Columns.Add("Đáp án", typeof(string));
            table.Columns.Add("Điểm", typeof(string));

            var questions = _controller.GetExamQuestions(_teacherId, _examId);
            _readinessSummary.Text = questions.Count == 0
                ? "Chưa có câu hỏi. Cần ít nhất 1 câu để kích hoạt bài kiểm tra."
                : $"Đã có {questions.Count} câu hỏi. Điểm mỗi câu sẽ được phân bổ lại về tổng 10 điểm.";
            foreach (var q in questions)
                table.Rows.Add(q.Id, q.DisplayOrder, q.QuestionText, q.CorrectOption, q.Points.ToString("0.##", CultureInfo.InvariantCulture));

            _grid.DataSource = table;
            if (_grid.Columns["Id"] != null)
                _grid.Columns["Id"]!.Visible = false;
            _points.Text = questions.Count == 0 ? "0" : (10m / questions.Count).ToString("0.##", CultureInfo.InvariantCulture);
            _grid.ClearSelection();
            _grid.CurrentCell = null;
            ClearEditor();
        }

        private void LoadSelectedQuestion()
        {
            int id = CurrentQuestionId();
            if (id <= 0)
                return;
            var item = _controller.GetExamQuestions(_teacherId, _examId).FirstOrDefault(q => q.Id == id);
            if (item == null)
                return;
            _question.Text = item.QuestionText;
            _a.Text = item.OptionA;
            _b.Text = item.OptionB;
            _c.Text = item.OptionC;
            _d.Text = item.OptionD;
            _correct.SelectedItem = item.CorrectOption;
        }

        private void SaveNewQuestion()
        {
            if (!RefreshEditState())
            {
                ShowGuardedQuestionWriteFailure();
                LoadQuestions();
                return;
            }

            if (!TryBuildQuestion(out var model))
                return;
            int createdId = _controller.CreateExamQuestion(_teacherId, model);
            if (createdId <= 0)
            {
                RefreshEditState();
                ShowGuardedQuestionWriteFailure();
                LoadQuestions();
                return;
            }
            LoadQuestions();
        }

        private void SaveExistingQuestion()
        {
            if (!RefreshEditState())
            {
                ShowGuardedQuestionWriteFailure();
                LoadQuestions();
                return;
            }

            int id = CurrentQuestionId();
            if (id <= 0 || !TryBuildQuestion(out var model))
                return;
            model.Id = id;
            bool updated = _controller.UpdateExamQuestion(_teacherId, model);
            if (!updated)
            {
                RefreshEditState();
                ShowGuardedQuestionWriteFailure();
                LoadQuestions();
                return;
            }
            LoadQuestions();
        }

        private void DeleteSelectedQuestion()
        {
            if (!RefreshEditState())
            {
                ShowGuardedQuestionWriteFailure();
                LoadQuestions();
                return;
            }

            int id = CurrentQuestionId();
            if (id <= 0)
                return;
            if (MetaTheme.ShowModernDialog("Xóa câu hỏi này khỏi bài kiểm tra?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            bool deleted = _controller.DeleteExamQuestion(_teacherId, _examId, id);
            if (!deleted)
            {
                RefreshEditState();
                ShowGuardedQuestionWriteFailure();
                LoadQuestions();
                return;
            }
            LoadQuestions();
        }

        private bool TryBuildQuestion(out TeacherExamQuestionModel model)
        {
            model = new TeacherExamQuestionModel();
            if (string.IsNullOrWhiteSpace(_question.Text) || string.IsNullOrWhiteSpace(_a.Text) || string.IsNullOrWhiteSpace(_b.Text) || string.IsNullOrWhiteSpace(_c.Text) || string.IsNullOrWhiteSpace(_d.Text))
            {
                MetaTheme.ShowModernDialog("Vui lòng nhập câu hỏi và đủ 4 lựa chọn.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            model.ExamId = _examId;
            model.QuestionText = _question.Text;
            model.OptionA = _a.Text;
            model.OptionB = _b.Text;
            model.OptionC = _c.Text;
            model.OptionD = _d.Text;
            model.CorrectOption = _correct.SelectedItem?.ToString() ?? "A";
            return true;
        }

        private int CurrentQuestionId()
        {
            if (_grid.CurrentRow == null || _grid.CurrentRow.IsNewRow)
                return 0;
            return Convert.ToInt32(_grid.CurrentRow.Cells["Id"].Value);
        }

        private void ClearEditor()
        {
            _question.Clear();
            _a.Clear();
            _b.Clear();
            _c.Clear();
            _d.Clear();
            _correct.SelectedIndex = 0;
        }
    }
}
