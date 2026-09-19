using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Student
{
    public sealed class UC_StudentLessons : UserControl, IStudentSearchTarget
    {
        private const string AllCoursesText = "Tất cả khóa học";

        private readonly CourseGuardDbContext _dbContext = new("");
        private readonly List<TeacherLessonModel> _items = new();
        private readonly BindingSource _lessonSource = new();
        private readonly BindingSource _materialSource = new();
        private readonly int? _selectedCourseId;
        private readonly DataGridView _lessonGrid = new();
        private readonly DataGridView _materialGrid = new();
        private readonly Label _lessonEmptyLabel;
        private readonly Label _materialEmptyLabel;
        private readonly RoundedPanel _lessonBody;
        private readonly RoundedPanel _materialBody;
        private readonly TextBox _searchBox = new();
        private readonly ComboBox _courseFilter = new();
        private readonly Button _searchButton = new() { Text = "Tìm kiếm" };
        private readonly Button _refreshButton = new() { Text = "Tải lại" };
        private readonly Button _detailButton = new() { Text = "Xem chi tiết" };
        private RoundedPanel _selectedItemSummary = null!;
        private Label _selectedItemTitle = null!;
        private Label _selectedItemDetail = null!;
        private Label _selectedItemAction = null!;

        private bool _isLoading;

        public UC_StudentLessons()
            : this(null)
        {
        }

        public UC_StudentLessons(int? selectedCourseId)
        {
            _selectedCourseId = selectedCourseId.GetValueOrDefault() > 0 ? selectedCourseId : null;
            Name = "UC_StudentLessons";
            Size = new Size(960, 560);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = AppColors.BgBase;

            _searchBox.PlaceholderText = "Tìm theo tên bài học, tài liệu hoặc khóa học...";
            StudentTabChrome.StyleSearchInput(_searchBox);
            _searchBox.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                ApplyFilter();
            };

            _courseFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _courseFilter.Width = 220;
            StudentTabChrome.StyleInput(_courseFilter);
            _courseFilter.SelectedIndexChanged += (_, _) =>
            {
                if (!_isLoading) ApplyFilter();
            };

            StudentTabChrome.StylePrimaryButton(_searchButton);
            StudentTabChrome.StyleSecondaryButton(_refreshButton);
            StudentTabChrome.StylePrimaryButton(_detailButton);
            _detailButton.Enabled = false;

            _searchButton.Click += (_, _) => ApplyFilter();
            _refreshButton.Click += async (_, _) => await LoadDataAsync();
            _detailButton.Click += (_, _) => OpenSelectedItem();

            StudentTabChrome.StyleGrid(_lessonGrid);
            StudentTabChrome.StyleGrid(_materialGrid);
            _lessonGrid.Name = "StudentLessonsGrid";
            _materialGrid.Name = "StudentMaterialsGrid";
            _lessonGrid.DataSource = _lessonSource;
            _materialGrid.DataSource = _materialSource;
            _lessonGrid.CellDoubleClick += (_, _) => OpenLessonFromGrid();
            _materialGrid.CellDoubleClick += (_, _) => OpenMaterialFromGrid();
            _lessonGrid.SelectionChanged += (_, _) =>
            {
                SyncDetailButton();
                UpdateSelectedItemSummary();
            };
            _materialGrid.SelectionChanged += (_, _) =>
            {
                SyncDetailButton();
                UpdateSelectedItemSummary();
            };
            _lessonGrid.Enter += (_, _) =>
            {
                ClearGridSelection(_materialGrid);
                SyncDetailButton();
                UpdateSelectedItemSummary();
            };
            _materialGrid.Enter += (_, _) =>
            {
                ClearGridSelection(_lessonGrid);
                SyncDetailButton();
                UpdateSelectedItemSummary();
            };

            _lessonBody = StudentTabChrome.CreateTableBody(_lessonGrid, out _lessonEmptyLabel);
            _materialBody = StudentTabChrome.CreateTableBody(_materialGrid, out _materialEmptyLabel);

            BuildLayout();
            LoadDataAsync().FireAndForgetSafe(this);
        }

        public int? SelectedCourseId => _selectedCourseId;

        public void ApplyGlobalSearch(string keyword)
        {
            _searchBox.Text = keyword ?? string.Empty;
            ApplyFilter();
        }

        private void BuildLayout()
        {
            TableLayoutPanel root = StudentTabChrome.CreateRoot(this);
            RoundedPanel header = StudentTabChrome.CreateHeader(
                "Bài học và Tài liệu",
                "Xem bài học và tài liệu độc lập từ các khóa học bạn đã tham gia.",
                StudentTabChrome.CreateSearchBox(_searchBox, 330),
                _courseFilter,
                _searchButton,
                _refreshButton,
                _detailButton);
            root.Controls.Add(header, 0, 0);

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.BgBase,
                ColumnCount = 1,
                RowCount = 4
            };
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 16f));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 112f));

            content.Controls.Add(StudentTabChrome.CreateDataCard("Danh sách bài học", _lessonBody), 0, 0);
            content.Controls.Add(StudentTabChrome.CreateDataCard("Danh sách tài liệu", _materialBody), 0, 2);
            content.Controls.Add(BuildSelectedItemSummary(), 0, 3);
            root.Controls.Add(content, 0, 1);

            StudentTabChrome.EnableNaturalFocusClear(this, _lessonGrid, _materialGrid);
        }

        private async Task LoadDataAsync()
        {
            this.ShowSkeleton(SkeletonType.TableWithToolbar);
            _isLoading = true;
            try
            {
                int studentId = UserSessionContext.CurrentUserId ?? 0;
                if (studentId == 0)
                {
                    MetaTheme.ShowModernDialog("Không xác định được tài khoản. Vui lòng đăng nhập lại.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _items.Clear();
                }
                else
                {
                    var rows = await _dbContext.GetStudentLessonsAsync(studentId);
                    _items.Clear();
                    _items.AddRange(rows);
                }

                ReloadCourseFilter();
                BindTables();
            }
            catch (Exception ex)
            {
                ClearSelectedContextAfterLoadFailure();
                MetaTheme.ShowModernDialog("Lỗi tải bài học và tài liệu: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isLoading = false;
                this.HideSkeleton();
            }
        }

        private void ReloadCourseFilter()
        {
            string? selected = _courseFilter.SelectedItem?.ToString();
            var courses = _items
                .Where(i => !_selectedCourseId.HasValue || i.CourseId == _selectedCourseId.Value)
                .Select(i => i.CourseName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .OrderBy(name => name)
                .ToList();

            _courseFilter.Items.Clear();
            if (!_selectedCourseId.HasValue)
                _courseFilter.Items.Add(AllCoursesText);

            foreach (string course in courses)
                _courseFilter.Items.Add(course);

            if (_courseFilter.Items.Count == 0)
                _courseFilter.Items.Add(_selectedCourseId.HasValue ? "Khóa học đã chọn" : AllCoursesText);

            _courseFilter.Enabled = !_selectedCourseId.HasValue;
            int selectedIndex = selected != null ? _courseFilter.Items.IndexOf(selected) : -1;
            _courseFilter.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
        }

        private void ApplyFilter() => BindTables();

        private IEnumerable<TeacherLessonModel> GetFilteredItems()
        {
            string keyword = _searchBox.Text.Trim().ToLowerInvariant();
            string courseFilter = _courseFilter.SelectedItem?.ToString() ?? AllCoursesText;

            IEnumerable<TeacherLessonModel> filtered = _items;
            if (_selectedCourseId.HasValue)
                filtered = filtered.Where(i => i.CourseId == _selectedCourseId.Value);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                filtered = filtered.Where(i =>
                    (i.Title ?? string.Empty).ToLowerInvariant().Contains(keyword) ||
                    (i.FileName ?? string.Empty).ToLowerInvariant().Contains(keyword) ||
                    (i.CourseName ?? string.Empty).ToLowerInvariant().Contains(keyword));
            }

            if (!_selectedCourseId.HasValue && courseFilter != AllCoursesText)
                filtered = filtered.Where(i => i.CourseName == courseFilter);

            return filtered;
        }

        private void BindTables()
        {
            var filtered = GetFilteredItems().ToList();
            BindGrid(_lessonGrid, _lessonSource, _lessonBody, _lessonEmptyLabel,
                CreateLessonsTable(filtered.Where(i => i.Id > 0).ToList()),
                string.IsNullOrWhiteSpace(_searchBox.Text) ? "Chưa có bài học nào từ giáo viên." : "Không tìm thấy bài học phù hợp.");
            BindGrid(_materialGrid, _materialSource, _materialBody, _materialEmptyLabel,
                CreateMaterialsTable(filtered.Where(i => i.Id < 0).ToList()),
                string.IsNullOrWhiteSpace(_searchBox.Text) ? "Chưa có tài liệu nào từ giáo viên." : "Không tìm thấy tài liệu phù hợp.");
            ClearSelectedItemSummary();
            SyncDetailButton();
        }

        private static void BindGrid(DataGridView grid, BindingSource source, RoundedPanel body, Label emptyLabel, DataTable table, string emptyMessage)
        {
            source.DataSource = table;
            grid.DataSource = source;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (column.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) || column.Name == "ID")
                    column.Visible = false;
            }
            bool hasRows = table.Rows.Count > 0;
            StudentTabChrome.SetTableState(body, grid, emptyLabel, hasRows, emptyMessage);
            ClearGridSelection(grid);
        }

        private static DataTable CreateLessonsTable(List<TeacherLessonModel> rows)
        {
            DataTable table = new();
            table.Columns.Add("ID", typeof(int));
            table.Columns.Add("Khóa học", typeof(string));
            table.Columns.Add("Tên bài học", typeof(string));
            table.Columns.Add("Ngày đăng", typeof(string));
            table.Columns.Add("Tài liệu đính kèm", typeof(string));
            table.Columns.Add("Kích thước", typeof(string));

            foreach (var row in rows)
            {
                table.Rows.Add(row.Id, row.CourseName, row.Title,
                    row.PublishAt.HasValue ? row.PublishAt.Value.ToString("dd/MM/yyyy HH:mm") : "",
                    row.HasStoredContent ? row.FileName : "Không có",
                    FormatSize(row.FileSize ?? 0));
            }
            return table;
        }

        private static DataTable CreateMaterialsTable(List<TeacherLessonModel> rows)
        {
            DataTable table = new();
            table.Columns.Add("ID", typeof(int));
            table.Columns.Add("Khóa học", typeof(string));
            table.Columns.Add("Tên tài liệu", typeof(string));
            table.Columns.Add("Ngày đăng", typeof(string));
            table.Columns.Add("Kích thước", typeof(string));

            foreach (var row in rows)
            {
                table.Rows.Add(row.Id, row.CourseName, row.FileName ?? row.Title,
                    row.PublishAt.HasValue ? row.PublishAt.Value.ToString("dd/MM/yyyy HH:mm") : "",
                    FormatSize(row.FileSize ?? 0));
            }
            return table;
        }

        private void SyncDetailButton()
        {
            TeacherLessonModel? item = GetSelectedItem();
            if (item == null)
            {
                _detailButton.Text = "Xem chi tiết";
                _detailButton.Enabled = false;
                return;
            }

            LearningUxPresentation view = StudentLearningItemUxPresenter.Present(item);
            _detailButton.Text = view.PrimaryActionText;
            _detailButton.Enabled = view.CanUsePrimaryAction;
        }

        private void OpenSelectedItem()
        {
            int lessonId = GetSelectedId(_lessonGrid);
            if (lessonId != 0)
            {
                OpenItem(lessonId);
                return;
            }

            int materialId = GetSelectedId(_materialGrid);
            if (materialId != 0)
            {
                OpenItem(materialId);
                return;
            }

            MetaTheme.ShowModernDialog("Vui lòng chọn một bài học hoặc tài liệu.", "Thông báo");
        }

        private void OpenLessonFromGrid()
        {
            int id = GetSelectedId(_lessonGrid);
            if (id != 0) OpenItem(id);
        }

        private void OpenMaterialFromGrid()
        {
            int id = GetSelectedId(_materialGrid);
            if (id != 0) OpenItem(id);
        }

        private void OpenItem(int itemId)
        {
            var item = _items.FirstOrDefault(i => i.Id == itemId);
            if (item == null) return;

            int studentId = UserSessionContext.CurrentUserId ?? 0;
            using var dialog = new CourseGuard.Frontend.Forms.Student.StudentLessonDetailDialog(_dbContext, item, studentId);
            dialog.ShowDialog(FindForm());
        }

        private void ClearSelectedContextAfterLoadFailure()
        {
            ClearGridSelection(_lessonGrid);
            ClearGridSelection(_materialGrid);
            ClearSelectedItemSummary();
            SyncDetailButton();
        }

        private RoundedPanel BuildSelectedItemSummary()
        {
            _selectedItemSummary = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                FillColor = AppColors.IsDarkMode ? AppColors.BgCardHover : ColorTranslator.FromHtml("#F8FAFC"),
                BorderColor = AppColors.Border,
                CornerRadius = 12,
                Margin = new Padding(0, 12, 0, 0),
                Padding = new Padding(18, 12, 18, 12)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));

            _selectedItemTitle = StyleSummaryLabel(AppFonts.Semibold(11f), AppColors.TextPrimary, ContentAlignment.MiddleLeft, autoEllipsis: true);
            _selectedItemDetail = StyleSummaryLabel(AppFonts.Body, AppColors.TextSecondary, ContentAlignment.TopLeft, autoEllipsis: true);
            _selectedItemAction = StyleSummaryLabel(AppFonts.Semibold(9f), AppColors.AccentBlue, ContentAlignment.MiddleLeft, autoEllipsis: true);

            layout.Controls.Add(_selectedItemTitle, 0, 0);
            layout.Controls.Add(_selectedItemDetail, 0, 1);
            layout.Controls.Add(_selectedItemAction, 0, 2);
            _selectedItemSummary.Controls.Add(layout);

            ClearSelectedItemSummary();
            return _selectedItemSummary;
        }

        private static Label StyleSummaryLabel(Font font, Color foreColor, ContentAlignment textAlign, bool autoEllipsis)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = autoEllipsis,
                BackColor = Color.Transparent,
                Font = font,
                ForeColor = foreColor,
                TextAlign = textAlign,
                UseCompatibleTextRendering = false
            };
        }

        private void UpdateSelectedItemSummary()
        {
            TeacherLessonModel? item = GetSelectedItem();
            if (item == null)
            {
                ClearSelectedItemSummary();
                return;
            }

            LearningUxPresentation view = StudentLearningItemUxPresenter.Present(item);
            _selectedItemTitle.Text = item.Id < 0
                ? (string.IsNullOrWhiteSpace(item.FileName) ? item.Title : item.FileName)
                : item.Title;
            _selectedItemDetail.Text = view.DetailText;
            _selectedItemAction.Text = $"Hành động tiếp theo: {view.PrimaryActionText}";
        }

        private void ClearSelectedItemSummary()
        {
            _selectedItemTitle.Text = "Chọn một bài học hoặc tài liệu";
            _selectedItemDetail.Text = "Thông tin khóa học, thời gian đăng và hành động phù hợp sẽ hiển thị tại đây.";
            _selectedItemAction.Text = "Hành động tiếp theo: Xem chi tiết";
        }

        private TeacherLessonModel? GetSelectedItem()
        {
            int lessonId = GetSelectedId(_lessonGrid);
            if (lessonId != 0)
                return _items.FirstOrDefault(i => i.Id == lessonId);

            int materialId = GetSelectedId(_materialGrid);
            if (materialId != 0)
                return _items.FirstOrDefault(i => i.Id == materialId);

            return null;
        }

        private static void ClearGridSelection(DataGridView grid)
        {
            grid.ClearSelection();
            grid.CurrentCell = null;
        }

        private static int GetSelectedId(DataGridView grid)
        {
            if (!grid.Visible || grid.CurrentRow == null || grid.CurrentRow.IsNewRow || !grid.Columns.Contains("ID"))
                return 0;
            object? value = grid.CurrentRow.Cells["ID"].Value;
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "";
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024d:0.#} KB";
            return $"{bytes / 1024d / 1024d:0.#} MB";
        }
    }
}
