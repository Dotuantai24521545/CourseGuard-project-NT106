using System.Windows.Forms;
using System.Drawing;

namespace CourseGuard.Frontend.Theme
{
    public static class SkeletonHelper
    {
        private static readonly string SKELETON_CONTROL_KEY = "_DynamicSkeletonLoaderControl";

        public static void ShowSkeleton(this UserControl control, SkeletonType type)
        {
            if (control.InvokeRequired)
            {
                control.Invoke(new System.Action(() => ShowSkeleton(control, type)));
                return;
            }

            if (control.Controls[SKELETON_CONTROL_KEY] is not SkeletonLoaderControl loader)
            {
                loader = new SkeletonLoaderControl
                {
                    Name = SKELETON_CONTROL_KEY,
                    SkeletonType = type,
                    Dock = DockStyle.Fill
                };
                control.Controls.Add(loader);
            }

            loader.SkeletonType = type;
            loader.BackColor = AppColors.IsDarkMode
                ? ColorTranslator.FromHtml("#111318")
                : ColorTranslator.FromHtml("#F9FAFB");
            loader.Visible = true;
            loader.BringToFront();
            loader.Start();
        }

        public static void HideSkeleton(this UserControl control)
        {
            if (control.InvokeRequired)
            {
                control.Invoke(new System.Action(() => HideSkeleton(control)));
                return;
            }

            if (control.Controls[SKELETON_CONTROL_KEY] is SkeletonLoaderControl loader)
            {
                loader.Stop();
                loader.Visible = false;
                loader.SendToBack();
            }
        }
    }
}
