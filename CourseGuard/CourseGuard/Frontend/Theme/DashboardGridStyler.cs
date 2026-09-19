using System.Drawing;
using System.Windows.Forms;

namespace CourseGuard.Frontend.Theme
{
    internal static class DashboardGridStyler
    {
        public static void Apply(DataGridView grid)
        {
            Color headerBg = AppColors.IsDarkMode
                ? ColorTranslator.FromHtml("#181827")
                : ColorTranslator.FromHtml("#EEF2F7");
            Color altRow = AppColors.IsDarkMode
                ? ColorTranslator.FromHtml("#191928")
                : ColorTranslator.FromHtml("#F8FAFC");

            grid.BackgroundColor = AppColors.BgCard;
            grid.BorderStyle = BorderStyle.None;
            grid.GridColor = AppColors.IsDarkMode
                ? Color.FromArgb(40, 40, 55)
                : Color.FromArgb(226, 232, 240);
            grid.EnableHeadersVisualStyles = false;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.RowHeadersVisible = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ReadOnly = true;

            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 44;
            grid.RowTemplate.Height = 40;

            grid.DefaultCellStyle.BackColor = AppColors.BgCard;
            grid.DefaultCellStyle.ForeColor = AppColors.TextPrimary;
            grid.DefaultCellStyle.SelectionBackColor = AppColors.AccentBlue;
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.DefaultCellStyle.Font = AppFonts.Body;
            grid.DefaultCellStyle.Padding = new Padding(10, 2, 10, 2);

            grid.AlternatingRowsDefaultCellStyle.BackColor = altRow;
            grid.AlternatingRowsDefaultCellStyle.ForeColor = AppColors.TextPrimary;
            grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = AppColors.AccentBlue;
            grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;

            grid.RowsDefaultCellStyle.BackColor = AppColors.BgCard;
            grid.RowsDefaultCellStyle.ForeColor = AppColors.TextPrimary;
            grid.RowsDefaultCellStyle.SelectionBackColor = AppColors.AccentBlue;
            grid.RowsDefaultCellStyle.SelectionForeColor = Color.White;

            grid.ColumnHeadersDefaultCellStyle.BackColor = headerBg;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = AppColors.TextPrimary;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = headerBg;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = AppColors.TextPrimary;
            grid.ColumnHeadersDefaultCellStyle.Font = AppFonts.Semibold(9f);
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 10, 0);

            grid.RowHeadersDefaultCellStyle.BackColor = headerBg;
            grid.RowHeadersDefaultCellStyle.ForeColor = AppColors.TextSecondary;
            grid.RowHeadersDefaultCellStyle.SelectionBackColor = headerBg;
            grid.RowHeadersDefaultCellStyle.SelectionForeColor = AppColors.TextPrimary;
            grid.Invalidate();
        }
    }
}
