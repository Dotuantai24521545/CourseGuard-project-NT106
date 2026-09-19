using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Services;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls.Teacher;

namespace CourseGuard.Frontend.Forms.Teacher
{
    public class RandomExamBuilderDialog : ThemedDialogBase
    {
        private const string AllValue = "Tat ca";

        private readonly int _teacherId;
        private readonly int _examId;
        private readonly int _courseId;
        private readonly TeacherController _controller = new(new CourseGuardDbContext(""));
        private readonly ComboBox _chapter = new();
        private readonly DarkNumericInput _easy = new();
        private readonly DarkNumericInput _medium = new();
        private readonly DarkNumericInput _hard = new();
        private readonly DataGridView _previewGrid = new();
        private readonly Button _preview = TeacherTabChrome.SecondaryButton("Xem truoc");
        private readonly Button _add = TeacherTabChrome.PrimaryButton("Them vao de");
        private readonly Button _close = TeacherTabChrome.SecondaryButton("Dong");
        private List<TeacherExamQuestionModel> _previewQuestions = new();
        private string _previewCriteriaSignature = string.Empty;
        private bool _previewLoaded;

        public bool QuestionsAdded { get; private set; }

        public RandomExamBuilderDialog(int teacherId, int examId, int courseId)
        {
            _teacherId = teacherId;
            _examId = examId;
            _courseId = courseId;
            Text = "Tao de ngau nhien";
            Width = 920;
            Height = 620;
            BuildLayout();
            WireEvents();
            LoadChapters();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var criteria = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 2 };
            for (int i = 0; i < 8; i++)
                criteria.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5f));

            _chapter.DropDownStyle = ComboBoxStyle.DropDownList;
            _easy.Value = 0;
            _medium.Value = 0;
            _hard.Value = 0;

            criteria.Controls.Add(new Label { Text = "Chuong", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 0);
            criteria.Controls.Add(_chapter, 1, 0);
            criteria.SetColumnSpan(_chapter, 3);
            criteria.Controls.Add(new Label { Text = "EASY", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 1);
            criteria.Controls.Add(_easy, 1, 1);
            criteria.Controls.Add(new Label { Text = "MEDIUM", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 2, 1);
            criteria.Controls.Add(_medium, 3, 1);
            criteria.Controls.Add(new Label { Text = "HARD", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 4, 1);
            criteria.Controls.Add(_hard, 5, 1);
            criteria.Controls.Add(_preview, 6, 1);
            criteria.Controls.Add(_add, 7, 1);
            root.Controls.Add(criteria, 0, 0);

            TeacherTabChrome.StyleGrid(_previewGrid);
            _previewGrid.Dock = DockStyle.Fill;
            _previewGrid.ReadOnly = true;
            _previewGrid.AllowUserToAddRows = false;
            _previewGrid.Columns.Add("Id", "Id");
            _previewGrid.Columns["Id"]!.Visible = false;
            _previewGrid.Columns.Add("Difficulty", "Do kho");
            _previewGrid.Columns.Add("Chapter", "Chuong");
            _previewGrid.Columns.Add("Question", "Cau hoi");
            _previewGrid.Columns["Question"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            root.Controls.Add(_previewGrid, 0, 1);

            ContentPanel.Controls.Add(root);
            AddFooterButtons(_close);
            CancelButton = _close;
        }

        private void WireEvents()
        {
            _close.Click += (_, _) => Close();
            _preview.Click += (_, _) => PreviewQuestions();
            _add.Click += async (_, _) => await AddQuestionsAsync();
        }

        private void LoadChapters()
        {
            _chapter.Items.Clear();
            _chapter.Items.Add(AllValue);
            foreach (string chapter in _controller.GetQuestionBankChapters(_teacherId, _courseId))
                _chapter.Items.Add(chapter);
            _chapter.SelectedIndex = 0;
        }

        private IReadOnlyList<RandomQuestionCriteria> BuildCriteria()
        {
            string? chapter = _chapter.SelectedItem?.ToString();
            if (string.Equals(chapter, AllValue, StringComparison.OrdinalIgnoreCase))
                chapter = null;

            return new[]
            {
                new RandomQuestionCriteria { Difficulty = "EASY", Chapter = chapter, Count = Math.Max(0, _easy.Value) },
                new RandomQuestionCriteria { Difficulty = "MEDIUM", Chapter = chapter, Count = Math.Max(0, _medium.Value) },
                new RandomQuestionCriteria { Difficulty = "HARD", Chapter = chapter, Count = Math.Max(0, _hard.Value) }
            }.Where(c => c.Count > 0).ToList();
        }

        private void PreviewQuestions()
        {
            IReadOnlyList<RandomQuestionCriteria> criteria = BuildCriteria();
            if (criteria.Count == 0)
            {
                MetaTheme.ShowModernDialog("Vui long nhap so cau can lay.", "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            LoadPreview(criteria);
        }

        private void BindPreview()
        {
            _previewGrid.Rows.Clear();
            foreach (TeacherExamQuestionModel question in _previewQuestions)
                _previewGrid.Rows.Add(question.Id, question.Difficulty, question.Chapter ?? string.Empty, question.QuestionText);
        }

        private void LoadPreview(IReadOnlyList<RandomQuestionCriteria> criteria)
        {
            _previewQuestions = _controller.GetRandomQuestionsByCriteria(_teacherId, _courseId, criteria);
            _previewCriteriaSignature = CriteriaSignature(criteria);
            _previewLoaded = true;
            BindPreview();
        }

        private async System.Threading.Tasks.Task AddQuestionsAsync()
        {
            IReadOnlyList<RandomQuestionCriteria> criteria = BuildCriteria();
            if (criteria.Count == 0)
            {
                MetaTheme.ShowModernDialog("Vui long nhap so cau can them.", "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string criteriaSignature = CriteriaSignature(criteria);
            if (!_previewLoaded || !string.Equals(_previewCriteriaSignature, criteriaSignature, StringComparison.Ordinal))
                LoadPreview(criteria);

            if (_previewQuestions.Count == 0)
            {
                MetaTheme.ShowModernDialog("Khong tim thay cau hoi phu hop.", "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _add.Enabled = false;
            try
            {
                List<int> previewQuestionIds = _previewQuestions.Select(q => q.Id).Where(id => id > 0).Distinct().ToList();
                int addedCount = await _controller.AddQuestionsFromBankAsync(_teacherId, _examId, _courseId, previewQuestionIds);
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
                    MetaTheme.ShowModernDialog($"Da them {addedCount} cau hoi ngau nhien vao de.", "Thanh cong", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                }
            }
            catch (Exception ex)
            {
                MetaTheme.ShowModernDialog("Khong the them cau hoi ngau nhien: " + ex.Message, "Loi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _add.Enabled = true;
            }
        }

        private static void ShowGuardedQuestionAddFailure()
        {
            MetaTheme.ShowModernDialog("Khong the them cau hoi. Bai kiem tra co the khong con o trang thai nhap hoac cac cau hoi da co trong de.", "Khong the them cau hoi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static string CriteriaSignature(IReadOnlyList<RandomQuestionCriteria> criteria) =>
            string.Join("|", criteria.Select(c =>
                $"{QuestionMetadataNormalizer.NormalizeDifficulty(c.Difficulty)}:{QuestionMetadataNormalizer.NormalizeChapter(c.Chapter) ?? string.Empty}:{Math.Max(0, c.Count)}"));
    }
}
