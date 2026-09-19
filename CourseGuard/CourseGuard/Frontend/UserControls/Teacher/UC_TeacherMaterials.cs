using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Services;
using CourseGuard.Frontend.Forms.Teacher;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Teacher
{
    public partial class UC_TeacherMaterials : TeacherGridPageBase, ITeacherQuickSearchTarget
    {
        private readonly ComboBox _courseFilter = new();
        private List<TeacherMaterialModel> _materials = new();
        private bool _isLoadingCourseFilter;
        private readonly int? _initialCourseId;
        private string _quickSearchKeyword = string.Empty;
        private Label _materialSummaryTitle = null!;
        private Label _materialSummaryDetail = null!;
        private Label _materialSummaryAction = null!;

        public UC_TeacherMaterials(int teacherId, TeacherController controller)
            : this(teacherId, controller, null)
        {
        }

        public UC_TeacherMaterials(int teacherId, TeacherController controller, int? initialCourseId) : base(teacherId, controller, "Tài liệu", "Đăng và quản lý tài liệu cho khóa học thuộc quyền.", "Danh sách tài liệu")
        {
            _initialCourseId = initialCourseId;
            _courseFilter.Width = 220;
            _courseFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            AppColors.ApplyTheme(_courseFilter);
            _courseFilter.SelectedIndexChanged += async (_, _) =>
            {
                if (_isLoadingCourseFilter)
                    return;

                await LoadDataAsync();
            };
            AddHeaderAction(_courseFilter);
            LoadCourseFilter();
            SetBelowGridContent(BuildMaterialSummaryPanel(), 108);
            Grid.SelectionChanged += (_, _) => UpdateMaterialSummary();
            Grid.DataBindingComplete += (_, _) => UpdateMaterialSummary();
            ReloadForInitialCourse();
        }

        protected override async Task<DataTable> CreateTableAsync()
        {
            List<TeacherMaterialModel> rows = await Task.Run(() => Controller.GetMaterials(TeacherId, null).ToList());
            _materials = rows;

            return TeacherTabChrome.ToTable(
                new[] { "Id", "CourseId", "Khóa học", "Tên file", "Kích thước", "Nguồn", "Hành động", "Ngày đăng", "Đường dẫn" },
                ApplyLocalFilter(rows),
                m =>
                {
                    LearningUxPresentation view = TeacherContentUxPresenter.PresentMaterial(m);
                    return new object?[]
                    {
                        m.Id,
                        m.CourseId,
                        m.CourseName,
                        m.FileName,
                        FormatSize(m.FileSize),
                        m.HasStoredContent ? "Database" : "Đường dẫn",
                        view.PrimaryActionText,
                        m.UploadedAt.ToString("dd/MM/yyyy HH:mm"),
                        m.FilePath
                    };
                });
        }

        protected override async Task AddAsync()
        {
            var courses = GetCoursesForCurrentScope().Where(c => c.Status != WorkflowConstants.CourseStatus.Closed).ToList();
            using var dialog = new TeacherMaterialUploadDialog(
                courses,
                SelectedCourseId ?? 0,
                lockCourseSelection: _initialCourseId.HasValue);
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
            {
                var model = await Task.Run(() => BuildMaterialModel(dialog.CourseId, dialog.SelectedFilePath));
                await Task.Run(() => Controller.CreateMaterial(TeacherId, model));
                LoadCourseFilter();
                await LoadDataAsync();
                MetaTheme.ShowModernDialog(
                    "Đã thêm tài liệu. Học viên sẽ thấy tài liệu khi đã được duyệt tham gia và khóa học đang hoạt động/mở.",
                    "Thông báo");
            }
        }

        protected override async Task EditAsync()
        {
            int id = CurrentInt("Id");
            if (id <= 0)
                return;

            using var dialog = new TeacherMaterialUploadDialog(
                GetCoursesForCurrentScope(),
                CurrentInt("CourseId"),
                lockCourseSelection: _initialCourseId.HasValue);
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
            {
                TeacherMaterialModel model = await Task.Run(() => BuildMaterialModel(dialog.CourseId, dialog.SelectedFilePath));
                model.Id = id;
                await Task.Run(() => Controller.UpdateMaterial(TeacherId, model));
                LoadCourseFilter();
                await LoadDataAsync();
            }
        }

        protected override async Task DeleteAsync()
        {
            int id = CurrentInt("Id");
            if (id <= 0)
                return;

            DialogResult result = MetaTheme.ShowModernDialog(
                "Xóa tài liệu đã chọn?",
                "Xác nhận",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result != DialogResult.Yes)
                return;

            await Task.Run(() => Controller.DeleteMaterial(TeacherId, id));
            await LoadDataAsync();
        }

        public async Task ApplyQuickSearchAsync(TeacherQuickSearchRequest request)
        {
            if (!string.Equals(request.Kind, TeacherQuickSearchKinds.Material, StringComparison.OrdinalIgnoreCase))
                return;

            _quickSearchKeyword = request.Keyword ?? string.Empty;
            SelectCourseFilter(request.ParentId ?? 0, suppressLoad: true);
            await LoadDataAsync();
            SelectMaterialRow(request.Id);
        }

        private int? SelectedCourseId => _initialCourseId ?? (_courseFilter.SelectedItem is CourseFilterItem item && item.CourseId > 0
            ? item.CourseId
            : null);

        private void LoadCourseFilter()
        {
            object? selected = _courseFilter.SelectedItem;
            int selectedCourseId = _initialCourseId ?? (selected is CourseFilterItem item ? item.CourseId : 0);
            _isLoadingCourseFilter = true;
            try
            {
                _courseFilter.Items.Clear();
                _courseFilter.Enabled = !_initialCourseId.HasValue;
                if (!_initialCourseId.HasValue)
                {
                    _courseFilter.Items.Add(new CourseFilterItem(0, "Toàn bộ khóa học"));
                }
                foreach (TeacherCourseModel course in GetCoursesForCurrentScope())
                    _courseFilter.Items.Add(new CourseFilterItem(course.Id, course.Name));

                if (_initialCourseId.HasValue && !_courseFilter.Items.OfType<CourseFilterItem>().Any(item => item.CourseId == _initialCourseId.Value))
                    _courseFilter.Items.Add(new CourseFilterItem(_initialCourseId.Value, "Khoa hoc da chon"));

                SelectCourseFilter(selectedCourseId, suppressLoad: true);
            }
            finally
            {
                _isLoadingCourseFilter = false;
            }
        }

        private void SelectCourseFilter(int courseId, bool suppressLoad = false)
        {
            if (_initialCourseId.HasValue)
                courseId = _initialCourseId.Value;

            int index = 0;
            for (int i = 0; i < _courseFilter.Items.Count; i++)
            {
                if (_courseFilter.Items[i] is CourseFilterItem candidate && candidate.CourseId == courseId)
                {
                    index = i;
                    break;
                }
            }

            if (_courseFilter.Items.Count > 0)
            {
                bool restoreSuppression = _isLoadingCourseFilter;
                if (suppressLoad)
                    _isLoadingCourseFilter = true;

                try
                {
                    _courseFilter.SelectedIndex = index;
                }
                finally
                {
                    _isLoadingCourseFilter = restoreSuppression;
                }
            }
        }

        private List<TeacherCourseModel> GetCoursesForCurrentScope()
        {
            List<TeacherCourseModel> courses = Controller.GetCourses(TeacherId).ToList();
            return _initialCourseId.HasValue
                ? courses.Where(course => course.Id == _initialCourseId.Value).ToList()
                : courses;
        }

        private void ReloadForInitialCourse()
        {
            if (_initialCourseId.HasValue)
                LoadDataAsync().FireAndForgetSafe(this);
        }

        private IEnumerable<TeacherMaterialModel> ApplyLocalFilter(IEnumerable<TeacherMaterialModel> materials)
        {
            IEnumerable<TeacherMaterialModel> filtered = materials;

            if (SelectedCourseId.HasValue)
                filtered = filtered.Where(m => m.CourseId == SelectedCourseId.Value);

            string keyword = _quickSearchKeyword.Trim();
            if (string.IsNullOrWhiteSpace(keyword))
                return filtered;

            return filtered.Where(m => ContainsKeyword(m.FileName, keyword)
                || ContainsKeyword(m.CourseName, keyword)
                || ContainsKeyword(m.FilePath, keyword)
                || ContainsKeyword(m.ContentType, keyword));
        }

        private void SelectMaterialRow(int materialId)
        {
            if (materialId <= 0 || !Grid.Columns.Contains("Id"))
                return;

            Grid.ClearSelection();
            foreach (DataGridViewRow row in Grid.Rows)
            {
                if (row.IsNewRow || row.Cells["Id"].Value == null)
                    continue;

                if (Convert.ToInt32(row.Cells["Id"].Value) != materialId)
                    continue;

                row.Selected = true;
                Grid.CurrentCell = GetFirstVisibleCell(row);
                Grid.FirstDisplayedScrollingRowIndex = Math.Max(0, row.Index);
                UpdateMaterialSummary();
                break;
            }
        }

        private RoundedPanel BuildMaterialSummaryPanel()
        {
            RoundedPanel panel = new()
            {
                Dock = DockStyle.Fill,
                FillColor = AppColors.IsDarkMode ? AppColors.BgCardHover : ColorTranslator.FromHtml("#F8FAFC"),
                BorderColor = AppColors.Border,
                CornerRadius = 12,
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

            _materialSummaryTitle = CreateSummaryLabel(AppFonts.Semibold(11f), AppColors.TextPrimary, ContentAlignment.MiddleLeft, true);
            _materialSummaryDetail = CreateSummaryLabel(AppFonts.Body, AppColors.TextSecondary, ContentAlignment.TopLeft, true);
            _materialSummaryAction = CreateSummaryLabel(AppFonts.Semibold(9f), AppColors.AccentBlue, ContentAlignment.MiddleLeft, true);

            layout.Controls.Add(_materialSummaryTitle, 0, 0);
            layout.Controls.Add(_materialSummaryDetail, 0, 1);
            layout.Controls.Add(_materialSummaryAction, 0, 2);
            panel.Controls.Add(layout);

            ClearMaterialSummary();
            return panel;
        }

        private static Label CreateSummaryLabel(Font font, Color foreColor, ContentAlignment textAlign, bool autoEllipsis)
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

        private void UpdateMaterialSummary()
        {
            TeacherMaterialModel? material = SelectedMaterial();
            if (material == null)
            {
                ClearMaterialSummary();
                return;
            }

            LearningUxPresentation view = TeacherContentUxPresenter.PresentMaterial(material);
            _materialSummaryTitle.Text = string.IsNullOrWhiteSpace(material.FileName) ? "Tài liệu chưa đặt tên" : material.FileName;
            _materialSummaryDetail.Text = view.DetailText;
            _materialSummaryAction.Text = $"Hành động tiếp theo: {view.PrimaryActionText}";
        }

        private void ClearMaterialSummary()
        {
            _materialSummaryTitle.Text = "Chọn một tài liệu";
            _materialSummaryDetail.Text = "Thông tin khóa học, nguồn tệp và bước tiếp theo sẽ hiển thị tại đây.";
            _materialSummaryAction.Text = "Hành động tiếp theo: Tải xuống";
        }

        private TeacherMaterialModel? SelectedMaterial()
        {
            int materialId = CurrentInt("Id");
            return materialId <= 0
                ? null
                : _materials.FirstOrDefault(item => item.Id == materialId);
        }

        private DataGridViewCell? GetFirstVisibleCell(DataGridViewRow row)
        {
            return row.Cells.Cast<DataGridViewCell>().FirstOrDefault(cell => cell.Visible);
        }

        private static TeacherMaterialModel BuildMaterialModel(int courseId, string filePath)
        {
            var info = new FileInfo(filePath);
            return new TeacherMaterialModel
            {
                CourseId = courseId,
                FileName = info.Name,
                FilePath = filePath,
                ContentType = MaterialFilePolicy.ResolveMimeType(info.Name),
                FileSize = info.Length,
                FileContent = File.ReadAllBytes(filePath),
                HasStoredContent = true
            };
        }

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0)
                return "";
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024d:0.#} KB";
            return $"{bytes / 1024d / 1024d:0.#} MB";
        }

        private static bool ContainsKeyword(string? value, string keyword)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class CourseFilterItem
        {
            public CourseFilterItem(int courseId, string name)
            {
                CourseId = courseId;
                Name = name;
            }

            public int CourseId { get; }
            public string Name { get; }
            public override string ToString() => Name;
        }
    }
}
