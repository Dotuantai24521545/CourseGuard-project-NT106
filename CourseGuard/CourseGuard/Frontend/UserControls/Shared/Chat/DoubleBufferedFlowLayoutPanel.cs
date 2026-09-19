using System.Windows.Forms;

namespace CourseGuard.Frontend.UserControls.Shared.Chat
{
    public class DoubleBufferedFlowLayoutPanel : FlowLayoutPanel
    {
        public DoubleBufferedFlowLayoutPanel()
        {
            DoubleBuffered = true;
            AutoScroll = true;
            WrapContents = false;
            FlowDirection = FlowDirection.TopDown;
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.UserPaint, true);
            UpdateStyles();
        }
    }
}
