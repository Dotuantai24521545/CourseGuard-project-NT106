using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Forms.Teacher;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Teacher
{
    public partial class UC_TeacherLessons : TeacherGridPageBase
    {
        private readonly Button _btnDownloadFile;
        private readonly ComboBox _courseFilter = new();
        private List<TeacherLessonModel> _lessons = new();
        private bool _isLoadingCourseFilter;
        private readonly int? _initialCourseId;
        private Label _lessonSummaryTitle = null!;
        private Label _lessonSummaryDetail = null!;
        private Label _lessonSummaryAction = null!;

        public UC_TeacherLessons(int teacherId, TeacherController controller)
            : this(teacherId, controller, null)
        {
        }

        public UC_TeacherLessons(int teacherId, TeacherController controller, int? initialCourseId) : base(teacherId, controller, "Bài học", "Quản lý bài học theo khóa học thuộc quyền.", "Danh sách bài học")
        {
            _initialCourseId = initialCourseId;
            _btnDownloadFile = TeacherTabChrome.SecondaryButton("Tải giáo trình");
            _btnDownloadFile.Enabled = false;
            _btnDownloadFile.Click += async (_, _) => await DownloadFileAsync();

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
            AddHeaderAction(_btnDownloadFile);
            LoadCourseFilter();
            SetBelowGridContent(BuildLessonSummaryPanel(), 108);

            Grid.SelectionChanged += (_, _) => UpdateLessonSelectionState();
            Grid.DataBindingComplete += (_, _) => UpdateLessonSelectionState();
            ReloadForInitialCourse();
        }

        protected override async Task<DataTable> CreateTableAsync()
        {
            List<TeacherLessonModel> rows = await Task.Run(() => Controller.GetLessons(TeacherId).ToList());
            _lessons = rows;

            IEnumerable<TeacherLessonModel> filtered = rows;
            int? selectedCourseId = SelectedCourseId;
            if (selectedCourseId.HasValue)
                filtered = filtered.Where(l => l.CourseId == selectedCourseId.Value);

            return TeacherTabChrome.ToTable(
                new[] { "Id", "CourseId", "Khóa học", "Tiêu đề", "Trạng thái", "Hành động", "Ngày đăng", "Giáo trình", "Nội dung" },
                filtered,
                l =>
                {
                    LearningUxPresentation view = TeacherContentUxPresenter.PresentLesson(l);
                    return new object?[]
                    {
                        l.Id,
                        l.CourseId,
                        l.CourseName,
                        l.Title,
                        view.StatusText,
                        view.PrimaryActionText,
                        l.PublishAt?.ToString("dd/MM/yyyy HH:mm") ?? string.Empty,
                        string.IsNullOrEmpty(l.FileName) ? "Không có" : l.FileName,
                        l.Content
                    };
                });
        }

        protected override async Task AddAsync()
        {
            using var dialog = new TeacherLessonDialog(
                GetCoursesForCurrentScope(),
                selectedCourseId: SelectedCourseId ?? 0,
                lockCourseSelection: _initialCourseId.HasValue);
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
            {
                var model = new TeacherLessonModel
                {
                    CourseId = dialog.CourseId,
                    Title = dialog.ItemTitle,
                    Content = dialog.ItemContent,
                    PublishAt = dialog.PublishAt,
                    Status = dialog.Status
                };
                if (dialog.FileContent != null)
                {
                    model.FileName = Path.GetFileName(dialog.SelectedFilePath);
                    model.FilePath = dialog.SelectedFilePath;
                    model.FileSize = dialog.FileSize;
                    model.FileContent = dialog.FileContent;
                    model.ContentType = dialog.ContentType;
                }

                await Task.Run(() => Controller.CreateLesson(TeacherId, model));
                LoadCourseFilter();
                await LoadDataAsync();
            }
        }

        protected override async Task EditAsync()
        {
            TeacherLessonModel? existing = SelectedLesson();
            if (existing == null)
                return;

            using var dialog = new TeacherLessonDialog(
                GetCoursesForCurrentScope(),
                existing,
                selectedCourseId: existing.CourseId,
                lockCourseSelection: _initialCourseId.HasValue);
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
            {
                var model = new TeacherLessonModel
                {
                    Id = existing.Id,
                    CourseId = dialog.CourseId,
                    Title = dialog.ItemTitle,
                    Content = dialog.ItemContent,
                    PublishAt = dialog.PublishAt,
                    Status = dialog.Status
                };

                if (dialog.FileContent != null)
                {
                    model.FileName = Path.GetFileName(dialog.SelectedFilePath);
                    model.FilePath = dialog.SelectedFilePath;
                    model.FileSize = dialog.FileSize;
                    model.FileContent = dialog.FileContent;
                    model.ContentType = dialog.ContentType;
                }
                else
                {
                    model.FileName = string.IsNullOrEmpty(dialog.SelectedFilePath) ? null : existing.FileName;
                    model.FilePath = string.IsNullOrEmpty(dialog.SelectedFilePath) ? null : existing.FilePath;
                    model.ContentType = string.IsNullOrEmpty(dialog.SelectedFilePath) ? null : existing.ContentType;
                    model.FileSize = string.IsNullOrEmpty(dialog.SelectedFilePath) ? null : existing.FileSize;
                    model.HasStoredContent = !string.IsNullOrEmpty(dialog.SelectedFilePath) && existing.HasStoredContent;
                }

                await Task.Run(() => Controller.UpdateLesson(TeacherId, model));
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
                "Xóa bài học đã chọn?",
                "Xác nhận",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result != DialogResult.Yes)
                return;

            await Task.Run(() => Controller.DeleteLesson(TeacherId, id));
            await LoadDataAsync();
        }

        private int? SelectedCourseId => _initialCourseId ?? (_courseFilter.SelectedItem is CourseFilterItem item && item.CourseId > 0
            ? item.CourseId
            : null);

        private void LoadCourseFilter()
        {
            int selectedCourseId = _initialCourseId ?? (_courseFilter.SelectedItem is CourseFilterItem item ? item.CourseId : 0);
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

        private RoundedPanel BuildLessonSummaryPanel()
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

            _lessonSummaryTitle = CreateSummaryLabel(AppFonts.Semibold(11f), AppColors.TextPrimary, ContentAlignment.MiddleLeft, true);
            _lessonSummaryDetail = CreateSummaryLabel(AppFonts.Body, AppColors.TextSecondary, ContentAlignment.TopLeft, true);
            _lessonSummaryAction = CreateSummaryLabel(AppFonts.Semibold(9f), AppColors.AccentBlue, ContentAlignment.MiddleLeft, true);

            layout.Controls.Add(_lessonSummaryTitle, 0, 0);
            layout.Controls.Add(_lessonSummaryDetail, 0, 1);
            layout.Controls.Add(_lessonSummaryAction, 0, 2);
            panel.Controls.Add(layout);

            ClearLessonSummary();
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

        private void UpdateLessonSelectionState()
        {
            TeacherLessonModel? lesson = SelectedLesson();
            if (lesson == null)
            {
                ClearLessonSummary();
                _btnDownloadFile.Text = "Tải giáo trình";
                _btnDownloadFile.Enabled = false;
                return;
            }

            LearningUxPresentation view = TeacherContentUxPresenter.PresentLesson(lesson);
            _lessonSummaryTitle.Text = string.IsNullOrWhiteSpace(lesson.Title) ? "Bài học chưa đặt tên" : lesson.Title;
            _lessonSummaryDetail.Text = view.DetailText;
            _lessonSummaryAction.Text = $"Hành động tiếp theo: {view.PrimaryActionText}";
            _btnDownloadFile.Text = view.PrimaryActionText;
            _btnDownloadFile.Enabled = view.CanDownload;
        }

        private void ClearLessonSummary()
        {
            _lessonSummaryTitle.Text = "Chọn một bài học";
            _lessonSummaryDetail.Text = "Thông tin khóa học, giáo trình đính kèm và bước tiếp theo sẽ hiển thị tại đây.";
            _lessonSummaryAction.Text = "Hành động tiếp theo: Tải giáo trình";
        }

        private TeacherLessonModel? SelectedLesson()
        {
            int lessonId = CurrentInt("Id");
            return lessonId <= 0
                ? null
                : _lessons.FirstOrDefault(l => l.Id == lessonId);
        }

        private async Task DownloadFileAsync()
        {
            TeacherLessonModel? existing = SelectedLesson();
            if (existing == null)
                return;

            LearningUxPresentation view = TeacherContentUxPresenter.PresentLesson(existing);
            if (!view.CanDownload || !existing.HasStoredContent || string.IsNullOrEmpty(existing.FileName))
            {
                MetaTheme.ShowModernDialog("Bài học này không có file đính kèm.", "Thông báo");
                return;
            }

            using var sfd = new SaveFileDialog
            {
                FileName = existing.FileName,
                Filter = "All files (*.*)|*.*"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                var bytes = await Controller.GetLessonFileContentAsync(TeacherId, existing.Id);
                if (bytes != null && bytes.Length > 0)
                {
                    await File.WriteAllBytesAsync(sfd.FileName, bytes);
                    MetaTheme.ShowModernDialog("Tải file thành công!", "Thông báo");
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = sfd.FileName,
                            UseShellExecute = true
                        });
                    }
                    catch
                    {
                    }
                }
                else
                {
                    MetaTheme.ShowModernDialog("Không thể tải nội dung file từ máy chủ.", "Lỗi");
                }
            }
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
