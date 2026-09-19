using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.UserControls.Student
{
    public abstract class StudentGridPageBase : UserControl
    {
        protected readonly DataGridView Grid = new();
        protected readonly BindingSource BindingSource = new();
        protected readonly Button RefreshButton = CreateButton("Tải lại");

        private readonly string _emptyMessage;
        private readonly FlowLayoutPanel _headerActionPanel;
        private TableLayoutPanel _contentLayout = null!;
        private RowStyle _belowGridRow = null!;
        private Control? _belowGridContent;

        protected readonly RoundedPanel GridBody;
        protected readonly Label EmptyStateLabel;
        protected readonly TextBox? SearchBox;
        protected readonly ComboBox? CourseFilter;
        protected readonly Button? SearchButton;

        protected StudentGridPageBase(
            string title,
            string subtitle,
            string cardTitle,
            string emptyMessage,
            string? hintText = null,
            bool showSearch = false,
            string searchPlaceholder = "",
            bool showCourseFilter = false,
            bool showSearchButton = false,
            int searchWidth = 330)
        {
            _emptyMessage = emptyMessage;
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = AppColors.BgBase;

            if (showSearch)
            {
                SearchBox = new TextBox
                {
                    PlaceholderText = searchPlaceholder,
                    Width = Math.Max(120, searchWidth - 48)
                };
                StudentTabChrome.StyleSearchInput(SearchBox);
                SearchBox.KeyDown += (_, e) =>
                {
                    if (e.KeyCode != Keys.Enter)
                        return;

                    e.SuppressKeyPress = true;
                    OnSearchRequested();
                };
            }

            if (showCourseFilter)
            {
                CourseFilter = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Width = 220
                };
                StudentTabChrome.StyleInput(CourseFilter);
                CourseFilter.SelectedIndexChanged += (_, _) =>
                {
                    if (!IsLoadingData)
                        OnCourseFilterChanged();
                };
            }

            if (showSearchButton)
            {
                SearchButton = CreateButton("Tìm kiếm");
                StudentTabChrome.StylePrimaryButton(SearchButton);
                SearchButton.Click += (_, _) => OnSearchRequested();
            }

            StudentTabChrome.StyleSecondaryButton(RefreshButton);
            RefreshButton.Click += async (_, _) => await LoadDataAsync();

            TableLayoutPanel root = StudentTabChrome.CreateRoot(this);
            RoundedPanel header = StudentTabChrome.CreateHeader(
                title,
                subtitle,
                out FlowLayoutPanel actionPanel,
                CreateHeaderActions(searchWidth).ToArray());
            _headerActionPanel = actionPanel;
            root.Controls.Add(header, 0, 0);

            Grid.Name = "StudentGrid";
            StudentTabChrome.StyleGrid(Grid);
            GridBody = StudentTabChrome.CreateTableBody(Grid, out Label emptyLabel);
            EmptyStateLabel = emptyLabel;

            Control content = CreateContent(cardTitle, hintText);
            root.Controls.Add(content, 0, 1);
            StudentTabChrome.EnableNaturalFocusClear(this, Grid);
        }

        protected bool IsLoadingData { get; private set; }
        protected bool HasBoundTable { get; private set; }
        protected Label? HintLabel { get; private set; }

        protected virtual string LoadErrorMessagePrefix => "Không thể tải dữ liệu";
        protected virtual string LoadErrorEmptyMessage => "Không thể tải dữ liệu.";

        protected abstract Task<DataTable> CreateTableAsync();

        protected virtual string GetEmptyMessage() => _emptyMessage;

        protected virtual void OnSearchRequested()
        {
        }

        protected virtual void OnCourseFilterChanged()
        {
        }

        protected virtual void OnTableBound(DataTable table, bool hasRows)
        {
        }

        protected async Task LoadDataAsync()
        {
            this.ShowSkeleton(SkeletonType.TableWithToolbar);
            IsLoadingData = true;

            try
            {
                DataTable table = await CreateTableAsync();
                BindLoadedTable(table);
            }
            catch (Exception ex)
            {
                if (!HasBoundTable)
                {
                    BindingSource.DataSource = null;
                    Grid.DataSource = BindingSource;
                    StudentTabChrome.SetTableState(GridBody, Grid, EmptyStateLabel, showTable: false, LoadErrorEmptyMessage);
                    OnTableBound(new DataTable(), hasRows: false);
                }

                MetaTheme.ShowModernDialog(
                    LoadErrorMessagePrefix + ": " + ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                IsLoadingData = false;
                this.HideSkeleton();
            }
        }

        protected void SetGridTable(DataTable table, string? emptyMessage = null)
        {
            if (!HasBoundTable)
                return;

            BindGridTable(table, emptyMessage);
        }

        private void BindLoadedTable(DataTable table)
        {
            BindGridTable(table);
            HasBoundTable = true;
        }

        private void BindGridTable(DataTable table, string? emptyMessage = null)
        {
            BindingSource.DataSource = table;
            Grid.DataSource = BindingSource;
            Grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            foreach (DataGridViewColumn column in Grid.Columns)
            {
                if (column.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) || column.Name == "ID")
                    column.Visible = false;
            }

            bool hasRows = table.Rows.Count > 0;
            StudentTabChrome.SetTableState(GridBody, Grid, EmptyStateLabel, hasRows, emptyMessage ?? GetEmptyMessage());
            Grid.ClearSelection();
            Grid.CurrentCell = null;
            OnTableBound(table, hasRows);
        }

        protected void AddHeaderAction(Control action)
        {
            action.Anchor = AnchorStyles.None;
            action.Margin = new Padding(8, 0, 0, 0);
            _headerActionPanel.Controls.Add(action);
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

        protected void HideColumn(string columnName)
        {
            if (Grid.Columns[columnName] != null)
                Grid.Columns[columnName]!.Visible = false;
        }

        private List<Control> CreateHeaderActions(int searchWidth)
        {
            var actions = new List<Control>();
            if (SearchBox != null)
                actions.Add(StudentTabChrome.CreateSearchBox(SearchBox, searchWidth));
            if (CourseFilter != null)
                actions.Add(CourseFilter);
            if (SearchButton != null)
                actions.Add(SearchButton);
            actions.Add(RefreshButton);
            return actions;
        }

        private Control CreateContent(string cardTitle, string? hintText)
        {
            bool hasHint = !string.IsNullOrWhiteSpace(hintText);
            _contentLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.BgBase,
                ColumnCount = 1,
                RowCount = hasHint ? 3 : 2
            };
            _contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _belowGridRow = new RowStyle(SizeType.Absolute, 0f);
            _contentLayout.RowStyles.Add(_belowGridRow);
            _contentLayout.Controls.Add(StudentTabChrome.CreateDataCard(cardTitle, GridBody), 0, 0);

            if (!hasHint)
                return _contentLayout;

            HintLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                BackColor = Color.Transparent,
                Font = AppFonts.Body,
                ForeColor = AppColors.TextSecondary,
                Margin = new Padding(0, 12, 0, 0),
                Text = hintText,
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = false
            };
            _contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            _contentLayout.Controls.Add(HintLabel, 0, 2);
            return _contentLayout;
        }

        private static Button CreateButton(string text)
        {
            return new Button
            {
                FlatStyle = FlatStyle.Flat,
                Text = text
            };
        }

    }
}
