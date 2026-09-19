using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls.Teacher;

namespace CourseGuard.Frontend.Forms.Teacher
{
    public class QuestionBankDialog : ThemedDialogBase
    {
        private const string AllValue = "Tat ca";

        private readonly int _teacherId;
        private readonly int _examId;
        private readonly int _courseId;
        private readonly TeacherController _controller = new(new CourseGuardDbContext(""));
        private readonly DataGridView _grid = new();
        private readonly ComboBox _difficulty = new();
        private readonly ComboBox _chapter = new();
        private readonly Button _add = TeacherTabChrome.PrimaryButton("Them da chon");
        private readonly Button _close = TeacherTabChrome.SecondaryButton("Dong");
        private readonly List<TeacherExamQuestionModel> _allQuestions = new();
        private bool _bindingFilters;

        public bool QuestionsAdded { get; private set; }

        public QuestionBankDialog(int teacherId, int examId, int courseId)
        {
            _teacherId = teacherId;
            _examId = examId;
            _courseId = courseId;
            Text = "Ngan hang cau hoi khoa hoc";
            Width = 980;
            Height = 640;
            BuildLayout();
            WireEvents();
            LoadData();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(0) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            _difficulty.DropDownStyle = ComboBoxStyle.DropDownList;
            _difficulty.Width = 160;
            _chapter.DropDownStyle = ComboBoxStyle.DropDownList;
            _chapter.Width = 220;
            filters.Controls.Add(new Label { Text = "Do kho", AutoSize = true, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Padding = new Padding(0, 8, 6, 0) });
            filters.Controls.Add(_difficulty);
            filters.Controls.Add(new Label { Text = "Chuong", AutoSize = true, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Padding = new Padding(14, 8, 6, 0) });
            filters.Controls.Add(_chapter);
            root.Controls.Add(filters, 0, 0);

            TeacherTabChrome.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.ReadOnly = false;
            root.Controls.Add(_grid, 0, 1);

            ContentPanel.Controls.Add(root);
            AddFooterButtons(_close, _add);
            CancelButton = _close;
        }

        private void WireEvents()
        {
            _close.Click += (_, _) => Close();
            _add.Click += async (_, _) => await AddSelectedQuestionsAsync();
            _difficulty.SelectedIndexChanged += (_, _) => ApplyFilter();
            _chapter.SelectedIndexChanged += (_, _) => ApplyFilter();
        }

        private void LoadData()
        {
            _allQuestions.Clear();
            _allQuestions.AddRange(_controller.GetCourseQuestionBank(_teacherId, _courseId));
            BuildColumns();
            BindFilters();
            BindGrid(_allQuestions);
        }

        private void BuildColumns()
        {
            _grid.Columns.Clear();
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Select", HeaderText = "Chon", Width = 50, ReadOnly = false });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Cau hoi", HeaderText = "Cau hoi", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Dap an", HeaderText = "Dap an", Width = 80, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Diem", HeaderText = "Diem", Width = 80, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Do kho", HeaderText = "Do kho", Width = 100, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Chuong", HeaderText = "Chuong", Width = 140, ReadOnly = true });
        }

        private void BindFilters()
        {
            _bindingFilters = true;
            _difficulty.Items.Clear();
            _difficulty.Items.AddRange(new object[] { AllValue, "EASY", "MEDIUM", "HARD" });
            _difficulty.SelectedIndex = 0;

            _chapter.Items.Clear();
            _chapter.Items.Add(AllValue);
            foreach (string chapter in _allQuestions
                .Select(q => q.Chapter)
                .OfType<string>()
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c))
            {
                _chapter.Items.Add(chapter);
            }
            _chapter.SelectedIndex = 0;
            _bindingFilters = false;
        }

        private void ApplyFilter()
        {
            if (_bindingFilters)
                return;

            string? selectedDifficulty = _difficulty.SelectedItem?.ToString();
            string? selectedChapter = _chapter.SelectedItem?.ToString();
            IEnumerable<TeacherExamQuestionModel> filtered = _allQuestions;

            if (!string.IsNullOrWhiteSpace(selectedDifficulty) && selectedDifficulty != AllValue)
                filtered = filtered.Where(q => string.Equals(q.Difficulty, selectedDifficulty, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(selectedChapter) && selectedChapter != AllValue)
                filtered = filtered.Where(q => string.Equals(q.Chapter, selectedChapter, StringComparison.OrdinalIgnoreCase));

            BindGrid(filtered);
        }

        private void BindGrid(IEnumerable<TeacherExamQuestionModel> questions)
        {
            _grid.Rows.Clear();
            foreach (var q in questions)
            {
                _grid.Rows.Add(false, q.Id, q.QuestionText, q.CorrectOption, q.Points.ToString("0.##"), q.Difficulty, q.Chapter ?? string.Empty);
            }

            _grid.ClearSelection();
        }

        private async System.Threading.Tasks.Task AddSelectedQuestionsAsync()
        {
            var selectedIds = new List<int>();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Cells["Select"].Value is bool isSelected && isSelected)
                    selectedIds.Add(Convert.ToInt32(row.Cells["Id"].Value));
            }

            if (selectedIds.Count == 0)
            {
                MetaTheme.ShowModernDialog("Vui long chon it nhat mot cau hoi.", "Canh bao", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _add.Enabled = false;
                int addedCount = await _controller.AddQuestionsFromBankAsync(_teacherId, _examId, _courseId, selectedIds);
                if (addedCount <= 0)
                {
                    QuestionsAdded = false;
                    ShowGuardedQuestionAddFailure();
                    _add.Enabled = true;
                    return;
                }

                if (addedCount > 0)
                {
                    QuestionsAdded = true;
                    MetaTheme.ShowModernDialog($"Da them {addedCount} cau hoi vao bai thi.", "Thanh cong", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                }
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog("Co loi xay ra: " + ex.Message, "Loi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _add.Enabled = true;
            }
        }

        private static void ShowGuardedQuestionAddFailure()
        {
            MetaTheme.ShowModernDialog("Khong the them cau hoi. Bai kiem tra co the khong con o trang thai nhap hoac cac cau hoi da co trong de.", "Khong the them cau hoi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
