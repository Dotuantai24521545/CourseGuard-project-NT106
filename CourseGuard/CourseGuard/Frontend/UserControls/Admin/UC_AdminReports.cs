/*
 * UC_AdminReports.cs
 * 
 * Layer: Presentation (UserControls)
 * Vai trò: Màn hình báo cáo thống kê. Hiển thị các biểu đồ hoặc số liệu tổng hợp.
 * Phụ thuộc: (Chưa implement logic sâu).
 */
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using CourseGuard.Backend.Data;
using System.IO;
using System.Drawing.Printing;
using CourseGuard.Frontend.Forms.Admin;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls.Teacher;

namespace CourseGuard.Frontend.UserControls.Admin
{
    public partial class UC_AdminReports : UserControl
    {
        private bool _hasLoaded;
        private readonly DarkDatePicker _reportStartDate = new();
        private readonly DarkDatePicker _reportEndDate = new();

        public UC_AdminReports()
        {
            InitializeComponent();
            ApplyThemeStyle();

            // Bo góc + cursor tay cho tất cả buttons
            CourseGuard.Frontend.Theme.RoundedButtonHelper.Apply(10,
                btnFilter, btnExportCSV, btnExportExcel, btnExportPDF);
            
            // Default Date Range: Last 30 days
            dtpStartDate.Value = DateTime.Now.AddDays(-30);
            dtpEndDate.Value = DateTime.Now;
            _reportStartDate.Value = dtpStartDate.Value;
            _reportEndDate.Value = dtpEndDate.Value;

            // Wire up events
            btnFilter.Click += BtnFilter_Click;
            btnExportCSV.Click += BtnExportCSV_Click;
            btnExportExcel.Click += BtnExportExcel_Click;
            btnExportPDF.Click += BtnExportPDF_Click;
            this.VisibleChanged += UC_AdminReports_VisibleChanged;

            // Initial Load
            cboReportType.Items.Clear();
            cboReportType.Items.Add("Danh sách học viên");
            cboReportType.Items.Add("Danh sách giảng viên");
            cboReportType.Items.Add("Danh sách vi phạm");
            cboReportType.Items.Add("Danh sách đăng nhập");

            cboReportType.SelectedIndex = 0; 
        }

        private void ApplyThemeStyle()
        {
            btnFilter.Tag = "primary";
            btnExportCSV.Tag = "secondary";
            btnExportExcel.Tag = "secondary";
            btnExportPDF.Tag = "secondary";

            lblTitle.Text = "BÁO CÁO VÀ THỐNG KÊ";
            BuildReportLayout();
            AppColors.ApplyTheme(this);
            TeacherTabChrome.StylePrimaryButton(btnFilter);
            PrepareFilterButton();
            TeacherTabChrome.StyleSecondaryButton(btnExportCSV);
            TeacherTabChrome.StyleSecondaryButton(btnExportExcel);
            TeacherTabChrome.StyleSecondaryButton(btnExportPDF);
            TeacherTabChrome.StyleGrid(dataGridView1);
        }

        private void BuildReportLayout()
        {
            var root = TeacherTabChrome.CreateRoot(this);
            var headerCard = TeacherTabChrome.CreateHeader(
                lblTitle.Text,
                "Xuất và phân tích dữ liệu hệ thống");

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 176f));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var topRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 16),
                Padding = Padding.Empty
            };
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 820f));
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            topRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var cardFilter = TeacherTabChrome.CreateDataCard("Bộ lọc và Tìm kiếm", BuildFilterContent());
            var cardExport = TeacherTabChrome.CreateDataCard("Xuất dữ liệu", BuildExportContent());
            var cardGrid = TeacherTabChrome.CreateDataCard("Dữ liệu báo cáo", dataGridView1);

            cardFilter.Margin = new Padding(0, 0, 16, 0);
            cardExport.Margin = Padding.Empty;
            cardGrid.Margin = Padding.Empty;

            topRow.Controls.Add(cardFilter, 0, 0);
            topRow.Controls.Add(cardExport, 1, 0);

            content.Controls.Add(topRow, 0, 0);
            content.Controls.Add(cardGrid, 0, 1);

            root.Controls.Add(headerCard, 0, 0);
            root.Controls.Add(content, 0, 1);
        }

        private Control BuildFilterContent()
        {
            grpFilter.Controls.Clear();
            grpFilter.Dock = DockStyle.Fill;
            grpFilter.Margin = Padding.Empty;
            grpFilter.Padding = new Padding(10, 0, 10, 8);
            grpFilter.BackColor = Color.Transparent;
            grpFilter.Tag = "custom";

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 5,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 212f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 174f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 174f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));

            PrepareFilterLabel(lblReportType, "Loại báo cáo:");
            PrepareFilterLabel(lblStartDate, "Từ ngày:");
            PrepareFilterLabel(lblEndDate, "Đến ngày:");
            PrepareFilterInput(cboReportType);
            PrepareFilterInput(_reportStartDate);
            PrepareFilterInput(_reportEndDate);

            btnFilter.Text = "Xem Báo Cáo";
            btnFilter.Width = 132;
            btnFilter.Height = 40;
            btnFilter.MinimumSize = new Size(132, 40);
            btnFilter.Margin = new Padding(0, 8, 0, 0);
            btnFilter.Anchor = AnchorStyles.Left | AnchorStyles.Top;

            grid.Controls.Add(lblReportType, 0, 0);
            grid.Controls.Add(lblStartDate, 1, 0);
            grid.Controls.Add(lblEndDate, 2, 0);
            grid.Controls.Add(cboReportType, 0, 1);
            grid.Controls.Add(_reportStartDate, 1, 1);
            grid.Controls.Add(_reportEndDate, 2, 1);
            grid.Controls.Add(btnFilter, 3, 1);

            grpFilter.Controls.Add(grid);
            return grpFilter;
        }

        private Control BuildExportContent()
        {
            grpExport.Controls.Clear();
            grpExport.Dock = DockStyle.Fill;
            grpExport.Margin = Padding.Empty;
            grpExport.Padding = new Padding(10, 2, 10, 8);
            grpExport.BackColor = Color.Transparent;
            grpExport.Tag = "custom";

            var actions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 3,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            PrepareExportButton(btnExportCSV, "Xuất CSV");
            PrepareExportButton(btnExportExcel, "Xuất Excel");
            PrepareExportButton(btnExportPDF, "Xuất PDF");
            btnExportPDF.Margin = new Padding(0, 2, 0, 0);

            actions.Controls.Add(btnExportCSV, 0, 0);
            actions.Controls.Add(btnExportExcel, 1, 0);
            actions.Controls.Add(btnExportPDF, 2, 0);
            grpExport.Controls.Add(actions);
            return grpExport;
        }

        private static void PrepareFilterLabel(Label label, string text)
        {
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.Margin = Padding.Empty;
            label.TextAlign = ContentAlignment.BottomLeft;
            label.AutoSize = false;
            label.BackColor = Color.Transparent;
        }

        private static void PrepareFilterInput(Control control)
        {
            control.Tag = "custom";
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(0, 8, 18, 0);
        }

        private void PrepareFilterButton()
        {
            btnFilter.Width = 132;
            btnFilter.Height = 40;
            btnFilter.MinimumSize = new Size(132, 40);
            btnFilter.Padding = new Padding(18, 0, 18, 1);
            btnFilter.Margin = new Padding(0, 8, 0, 0);
        }

        private static void PrepareExportButton(Button button, string text)
        {
            button.Text = text;
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0, 2, 12, 0);
            button.MinimumSize = new Size(92, 36);
            button.Height = 36;
        }

        private void WrapWithCards()
        {
            // Wrap Filter
            this.Controls.Remove(grpFilter);
            var cardFilter = CourseGuard.Frontend.UserControls.Teacher.TeacherTabChrome.CreateDataCard("Bộ lọc và Tìm kiếm", grpFilter);
            cardFilter.Dock = DockStyle.Top;
            cardFilter.Height = 160;
            cardFilter.Padding = new Padding(12);
            this.Controls.Add(cardFilter);
            
            // Wrap Export
            this.Controls.Remove(grpExport);
            var cardExport = CourseGuard.Frontend.UserControls.Teacher.TeacherTabChrome.CreateDataCard("Xuất dữ liệu", grpExport);
            cardExport.Dock = DockStyle.Top;
            cardExport.Height = 140;
            cardExport.Padding = new Padding(12);
            this.Controls.Add(cardExport);
            
            // Wrap Grid
            this.Controls.Remove(dataGridView1);
            var cardGrid = CourseGuard.Frontend.UserControls.Teacher.TeacherTabChrome.CreateDataCard("Dữ liệu báo cáo", dataGridView1);
            cardGrid.Dock = DockStyle.Fill;
            cardGrid.Padding = new Padding(12);
            this.Controls.Add(cardGrid);
            
            // Wrap Header
            this.Controls.Remove(panelHeader);
            var headerCard = CourseGuard.Frontend.UserControls.Teacher.TeacherTabChrome.CreateHeader(
                lblTitle.Text,
                "Xuất và phân tích dữ liệu hệ thống");
            headerCard.Dock = DockStyle.Top;
            this.Controls.Add(headerCard);
            
            // Set Z-Order for correct Docking
            headerCard.SendToBack(); // Docks first (Top)
            
            var spacer1 = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = Color.Transparent, Tag = "custom" };
            this.Controls.Add(spacer1);
            spacer1.SendToBack();
            
            cardFilter.SendToBack();  // Docks second (Top)
            
            var spacer2 = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = Color.Transparent, Tag = "custom" };
            this.Controls.Add(spacer2);
            spacer2.SendToBack();
            
            cardExport.SendToBack();  // Docks third (Top)
            
            var spacer3 = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = Color.Transparent, Tag = "custom" };
            this.Controls.Add(spacer3);
            spacer3.SendToBack();
            
            cardGrid.BringToFront();  // Docks last (Fill)
        }

        private void UC_AdminReports_VisibleChanged(object? sender, EventArgs e)
        {
            if (!Visible || _hasLoaded) return;

            try
            {
                LoadData();
                _hasLoaded = true;
            }
            catch (ObjectDisposedException)
            {
                // Ignore when control is being disposed during view switching.
            }
        }

        private void BtnFilter_Click(object? sender, EventArgs e)
        {
            LoadData();
        }

        private async void LoadData()
        {
            string? reportType = cboReportType.SelectedItem?.ToString();
            DateTime start = _reportStartDate.Value.Date;
            DateTime end = _reportEndDate.Value.Date.AddDays(1).AddTicks(-1); // End of day

            string query = "";
            
            if (reportType == "Danh sách học viên")
            {
                query = @"
                    SELECT u.ID, u.USERNAME, u.FULL_NAME, u.EMAIL, u.CREATED_AT, u.STATUS
                    FROM USERS u
                    JOIN ROLES r ON u.ROLE_ID = r.ID
                    WHERE r.NAME = 'STUDENT'
                    AND u.CREATED_AT BETWEEN @start AND @end
                    ORDER BY u.CREATED_AT DESC";
            }
            else if (reportType == "Danh sách giảng viên")
            {
                query = @"
                    SELECT u.ID, u.USERNAME, u.FULL_NAME, u.EMAIL, u.CREATED_AT, u.STATUS
                    FROM USERS u
                    JOIN ROLES r ON u.ROLE_ID = r.ID
                    WHERE r.NAME = 'TEACHER'
                    AND u.CREATED_AT BETWEEN @start AND @end
                    ORDER BY u.CREATED_AT DESC";
            }
            else if (reportType == "Danh sách vi phạm")
            {
                query = @"
                    SELECT v.ID, u.USERNAME, u.FULL_NAME, e.TITLE AS EXAM_TITLE, v.TYPE AS VIOLATION_TYPE, v.CREATED_AT
                    FROM VIOLATIONS v
                    JOIN USERS u ON v.USER_ID = u.ID
                    LEFT JOIN EXAM_ATTEMPTS ea ON v.EXAM_ATTEMPT_ID = ea.ID
                    LEFT JOIN EXAMS e ON ea.EXAM_ID = e.ID
                    WHERE v.CREATED_AT BETWEEN @start AND @end
                    ORDER BY v.CREATED_AT DESC";
            }
            else if (reportType == "Danh sách đăng nhập")
            {
                query = @"
                    SELECT a.ID, COALESCE(u.USERNAME, 'SYSTEM') AS USERNAME, a.ACTION, a.DETAILS, a.IP_ADDRESS, a.CREATED_AT
                    FROM AUDIT_LOGS a
                    LEFT JOIN USERS u ON a.USER_ID = u.ID
                    WHERE a.ACTION = 'LOGIN'
                    AND a.CREATED_AT BETWEEN @start AND @end
                    ORDER BY a.CREATED_AT DESC";
            }
            else
            {
                // Fallback or empty
                dataGridView1.DataSource = null;
                return;
            }

            var parameters = new Dictionary<string, (SqlDbType, object)>
            {
                { "@start", (SqlDbType.DateTime, start) },
                { "@end", (SqlDbType.DateTime, end) }
            };

            this.ShowSkeleton(SkeletonType.TableWithToolbar);
            try
            {
                DataTable dt = await Task.Run(() => DatabaseAction.ExecuteQuery(query, parameters));
                dataGridView1.DataSource = dt;
            }
            catch (Exception ex)
            {
                CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Lỗi tải dữ liệu báo cáo: " + ex.Message);
            }
            finally
            {
                this.HideSkeleton();
            }
        }

        // Export to CSV
        private void BtnExportCSV_Click(object? sender, EventArgs e)
        {
            ExportToTextFile(",", "CSV File (*.csv)|*.csv");
        }

        // Export to Excel using MiniExcel (Generates professional .xlsx file in background thread)
        private async void BtnExportExcel_Click(object? sender, EventArgs e)
        {
            if (dataGridView1.Rows.Count == 0)
            {
                CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Không có dữ liệu để xuất.");
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Excel Workbook (*.xlsx)|*.xlsx";
            sfd.FileName = "Report_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                this.ShowSkeleton(SkeletonType.FormWithTable);
                try
                {
                    string filePath = sfd.FileName;

                    // 1. Prepare DataTable on UI thread (safe and thread-safe)
                    DataTable? dtToExport = null;
                    if (dataGridView1.DataSource is DataTable dt)
                    {
                        dtToExport = dt.Copy(); // Copy to be safe from cross-thread access during serialization
                        for (int i = 0; i < dataGridView1.Columns.Count; i++)
                        {
                            string colName = dataGridView1.Columns[i].DataPropertyName;
                            if (!string.IsNullOrEmpty(colName) && dtToExport.Columns.Contains(colName))
                            {
                                var col = dtToExport.Columns[colName];
                                if (col != null)
                                {
                                    col.ColumnName = dataGridView1.Columns[i].HeaderText;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Build DataTable from DataGridView rows/columns manually if not bound to a DataTable
                        dtToExport = new DataTable();
                        for (int i = 0; i < dataGridView1.Columns.Count; i++)
                        {
                            dtToExport.Columns.Add(dataGridView1.Columns[i].HeaderText);
                        }

                        foreach (DataGridViewRow row in dataGridView1.Rows)
                        {
                            if (!row.IsNewRow)
                            {
                                var cells = new object[dataGridView1.Columns.Count];
                                for (int i = 0; i < dataGridView1.Columns.Count; i++)
                                {
                                    cells[i] = row.Cells[i].Value ?? DBNull.Value;
                                }
                                dtToExport.Rows.Add(cells);
                            }
                        }
                    }

                    // 2. Save in background thread using MiniExcel
                    await Task.Run(() =>
                    {
                        MiniExcelLibs.MiniExcel.SaveAs(filePath, dtToExport, overwriteFile: true);
                    });

                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Xuất file Excel thành công!");
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = filePath,
                            UseShellExecute = true
                        });
                    }
                    catch { /* Ignore if no default app */ }
                }
                catch (Exception ex)
                {
                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Lỗi xuất file Excel: " + ex.Message);
                }
                finally
                {
                    this.HideSkeleton();
                }
            }
        }

        private async void ExportToTextFile(string separator, string filter)
        {
            if (dataGridView1.Rows.Count == 0)
            {
                CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Không có dữ liệu để xuất.");
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = filter;
            sfd.FileName = "Report_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                this.ShowSkeleton(SkeletonType.FormWithTable);
                try
                {
                    // 1. Extract data from UI on UI thread (safe and fast)
                    var headers = new List<string>();
                    for (int i = 0; i < dataGridView1.Columns.Count; i++)
                    {
                        headers.Add(dataGridView1.Columns[i].HeaderText);
                    }

                    var rowData = new List<string[]>();
                    foreach (DataGridViewRow row in dataGridView1.Rows)
                    {
                        if (!row.IsNewRow)
                        {
                            var cells = new string[dataGridView1.Columns.Count];
                            for (int i = 0; i < dataGridView1.Columns.Count; i++)
                            {
                                cells[i] = row.Cells[i].Value?.ToString() ?? "";
                            }
                            rowData.Add(cells);
                        }
                    }

                    string filePath = sfd.FileName;

                    // 2. Perform string building and file writing on background thread
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        StringBuilder sb = new StringBuilder();

                        // Headers
                        for (int i = 0; i < headers.Count; i++)
                        {
                            sb.Append(headers[i]);
                            if (i < headers.Count - 1) sb.Append(separator);
                        }
                        sb.AppendLine();

                        // Rows
                        foreach (var cells in rowData)
                        {
                            for (int i = 0; i < cells.Length; i++)
                            {
                                string value = cells[i].Replace("\n", " ").Replace("\r", " ");
                                if (value.Contains(separator))
                                {
                                    value = "\"" + value + "\"";
                                }
                                sb.Append(value);
                                if (i < cells.Length - 1) sb.Append(separator);
                            }
                            sb.AppendLine();
                        }

                        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
                    });

                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Xuất file thành công!");
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = filePath,
                            UseShellExecute = true
                        });
                    }
                    catch { /* Ignore if no default app */ }
                }
                catch (Exception ex)
                {
                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Lỗi xuất file: " + ex.Message);
                }
                finally
                {
                    this.HideSkeleton();
                }
            }
        }

        // Export to PDF (Printing)
        private void BtnExportPDF_Click(object? sender, EventArgs e)
        {
            if (dataGridView1.Rows.Count == 0)
            {
                CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Không có dữ liệu để in.");
                return;
            }

            PrintDocument printDoc = new PrintDocument();
            printDoc.DocumentName = "Report Output";
            printDoc.PrintPage += PrintDoc_PrintPage;

            PrintDialog printDialog = new PrintDialog();
            printDialog.Document = printDoc;
            
            if (printDialog.ShowDialog() == DialogResult.OK)
            {
                try 
                {
                    printDoc.Print();
                }
                catch(Exception ex)
                {
                    CourseGuard.Frontend.Theme.MetaTheme.ShowModernDialog("Lỗi in: " + ex.Message);
                }
            }
        }

        // Simple Grid Printing Logic
        private int _printRowIndex = 0;
        private void PrintDoc_PrintPage(object sender, PrintPageEventArgs e)
        {
            int startX = e.MarginBounds.Left;
            int startY = e.MarginBounds.Top;
            int tableWidth = e.MarginBounds.Width;
            int rowHeight = dataGridView1.RowTemplate.Height;
            
            int cellWidth = tableWidth / Math.Max(1, dataGridView1.Columns.Count);
            
            Font font = new Font("Segoe UI", 9);
            Font headerFont = new Font("Segoe UI", 10, FontStyle.Bold);
            Brush brush = Brushes.Black;

            // Draw Headers (Only on first page or repeated?) Let's repeat.
            int currentY = startY;
            
            for (int i = 0; i < dataGridView1.Columns.Count; i++)
            {
                e.Graphics?.DrawRectangle(Pens.Black, startX + (i * cellWidth), currentY, cellWidth, rowHeight);
                e.Graphics?.DrawString(dataGridView1.Columns[i].HeaderText, headerFont, brush, 
                    new RectangleF(startX + (i * cellWidth) + 2, currentY + 5, cellWidth - 4, rowHeight - 4));
            }
            currentY += rowHeight;

            while (_printRowIndex < dataGridView1.Rows.Count)
            {
                DataGridViewRow row = dataGridView1.Rows[_printRowIndex];
                
                // Check if we reached bottom of page
                if (currentY + rowHeight > e.MarginBounds.Bottom)
                {
                    e.HasMorePages = true;
                    return; // Continue on next page
                }

                for (int i = 0; i < dataGridView1.Columns.Count; i++)
                {
                    string val = row.Cells[i].Value?.ToString() ?? "";
                    e.Graphics?.DrawRectangle(Pens.Black, startX + (i * cellWidth), currentY, cellWidth, rowHeight);
                    e.Graphics?.DrawString(val, font, brush, 
                        new RectangleF(startX + (i * cellWidth) + 2, currentY + 5, cellWidth - 4, rowHeight - 4));
                }
                
                currentY += rowHeight;
                _printRowIndex++;
            }

            e.HasMorePages = false;
            _printRowIndex = 0; // Reset for next print
        }
    }
}
