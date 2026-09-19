using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Backend.Controllers;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Teacher
{
    public abstract class TeacherGridPageBase : UserControl
    {
        protected readonly int TeacherId;
        protected readonly TeacherController Controller;
        protected readonly DataGridView Grid = new();
        protected readonly BindingSource BindingSource = new();
        protected readonly Button RefreshButton = TeacherTabChrome.SecondaryButton("Tải lại");
        protected readonly Button AddButton = TeacherTabChrome.PrimaryButton("Thêm");
        protected readonly Button EditButton = TeacherTabChrome.SecondaryButton("Sửa");
        protected readonly Button DeleteButton = TeacherTabChrome.DangerButton("Xóa");

        private readonly string _emptyMessage;
        private TableLayoutPanel _contentLayout = null!;
        private RowStyle _belowGridRow = null!;
        private Control? _belowGridContent;
        private int _loadVersion;
        private RoundedPanel _gridBody = null!;
        private Label _emptyStateLabel = null!;

        protected TeacherGridPageBase(int teacherId, TeacherController controller, string title, string subtitle, string cardTitle, bool showCrud = true)
        {
            TeacherId = teacherId;
            Controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _emptyMessage = $"Chưa có dữ liệu trong {cardTitle.ToLowerInvariant()}.";
            InitializeComponent(title, subtitle, cardTitle, showCrud);
            RefreshButton.Click += async (_, _) => await LoadDataAsync();
            AddButton.Click += async (_, _) => await AddAsync();
            EditButton.Click += async (_, _) => {
                if (RequiresSelectionForEdit && CurrentInt(EditSelectionColumnName) <= 0) {
                    MetaTheme.ShowModernDialog("Vui lòng chọn một dòng dữ liệu để Sửa.", "Chưa chọn dữ liệu");
                    return;
                }
                await EditAsync();
            };
            DeleteButton.Click += async (_, _) => {
                if (CurrentInt("Id") <= 0) {
                    MetaTheme.ShowModernDialog("Vui lòng chọn một dòng dữ liệu để Xóa.", "Chưa chọn dữ liệu");
                    return;
                }
                await DeleteAsync();
            };
            LoadDataAsync().FireAndForgetSafe(this);
        }

        protected virtual Task AddAsync() => Task.CompletedTask;
        protected virtual Task EditAsync() => Task.CompletedTask;
        protected virtual Task DeleteAsync() => Task.CompletedTask;
        protected virtual bool RequiresSelectionForEdit => true;
        protected virtual string EditSelectionColumnName => "Id";
        protected abstract Task<DataTable> CreateTableAsync();

        protected void AddHeaderAction(Control action)
        {
            FlowLayoutPanel? panel = FindActionPanel(this);
            if (panel == null)
                return;

            action.Margin = new Padding(8, 0, 0, 0);
            panel.Controls.Add(action);
        }

        protected void AddHeaderActionBefore(Control action, Control before)
        {
            FlowLayoutPanel? panel = FindActionPanel(this);
            if (panel == null)
                return;

            action.Margin = new Padding(8, 0, 0, 0);
            int beforeIndex = panel.Controls.GetChildIndex(before);
            panel.Controls.Add(action);
            if (beforeIndex >= 0)
                panel.Controls.SetChildIndex(action, beforeIndex);
        }

        protected int CurrentInt(string columnName)
        {
            if (!Grid.Visible || Grid.CurrentRow == null || Grid.CurrentRow.IsNewRow || !Grid.Columns.Contains(columnName))
                return 0;
            object? value = Grid.CurrentRow.Cells[columnName].Value;
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        protected string CurrentString(string columnName)
        {
            if (!Grid.Visible || Grid.CurrentRow == null || Grid.CurrentRow.IsNewRow || !Grid.Columns.Contains(columnName))
                return string.Empty;
            return Grid.CurrentRow.Cells[columnName].Value?.ToString() ?? string.Empty;
        }

        protected async Task LoadDataAsync()
        {
            int loadVersion = ++_loadVersion;
            this.ShowSkeleton(SkeletonType.TableWithToolbar);
            try
            {
                DataTable table = await CreateTableAsync();
                if (loadVersion != _loadVersion)
                    return;

                BindingSource.DataSource = table;
                Grid.DataSource = BindingSource;
                foreach (DataGridViewColumn column in Grid.Columns)
                {
                    if (column.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) || column.Name == "ID")
                        column.Visible = false;
                }

                bool hasRows = table.Rows.Count > 0;
                TeacherTabChrome.SetTableState(_gridBody, Grid, _emptyStateLabel, hasRows, _emptyMessage);
                EditButton.Enabled = hasRows;
                DeleteButton.Enabled = hasRows;
                Grid.ClearSelection();
                Grid.CurrentCell = null;
            }
            catch (Exception ex)
            {
                if (loadVersion != _loadVersion)
                    return;

                BindingSource.DataSource = null;
                TeacherTabChrome.SetTableState(_gridBody, Grid, _emptyStateLabel, showTable: false, "Không thể tải dữ liệu.");
                EditButton.Enabled = false;
                DeleteButton.Enabled = false;
                MetaTheme.ShowModernDialog("Không thể tải dữ liệu: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (loadVersion == _loadVersion)
                    this.HideSkeleton();
            }
        }

        protected void SetBelowGridContent(Control content, int height)
        {
            if (_belowGridContent != null)
            {
                _contentLayout.Controls.Remove(_belowGridContent);
                _belowGridContent.Dispose();
            }

            _belowGridContent = content;
            _belowGridRow.Height = Math.Max(0, height);
            content.Dock = DockStyle.Fill;
            content.Margin = new Padding(0, 12, 0, 0);
            _contentLayout.Controls.Add(content, 0, 1);
        }

        private void InitializeComponent(string title, string subtitle, string cardTitle, bool showCrud)
        {
            var root = TeacherTabChrome.CreateRoot(this);
            Control[] actions = showCrud
                ? new Control[] { AddButton, EditButton, DeleteButton, RefreshButton }
                : new Control[] { RefreshButton };
            root.Controls.Add(TeacherTabChrome.CreateHeader(title, subtitle, actions), 0, 0);
            TeacherTabChrome.StyleGrid(Grid);
            _gridBody = TeacherTabChrome.CreateTableBody(Grid, out _emptyStateLabel);
            _contentLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.BgBase,
                ColumnCount = 1,
                RowCount = 2
            };
            _contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _belowGridRow = new RowStyle(SizeType.Absolute, 0f);
            _contentLayout.RowStyles.Add(_belowGridRow);
            _contentLayout.Controls.Add(TeacherTabChrome.CreateDataCard(cardTitle, _gridBody), 0, 0);
            root.Controls.Add(_contentLayout, 0, 1);
        }

        private static FlowLayoutPanel? FindActionPanel(Control root)
        {
            foreach (Control child in root.Controls)
            {
                if (child is FlowLayoutPanel { FlowDirection: FlowDirection.LeftToRight } panel)
                    return panel;

                FlowLayoutPanel? nested = FindActionPanel(child);
                if (nested != null)
                    return nested;
            }

            return null;
        }
    }
}
