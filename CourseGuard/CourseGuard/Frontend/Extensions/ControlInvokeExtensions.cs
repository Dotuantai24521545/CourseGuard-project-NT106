namespace CourseGuard.Frontend.Extensions
{
    public static class ControlInvokeExtensions
    {
        public static void InvokeIfRequired(this Control control, Action action)
        {
            if (control == null || action == null)
            {
                return;
            }

            if (control.IsDisposed || !control.IsHandleCreated)
            {
                return;
            }

            if (control.InvokeRequired)
            {
                try
                {
                    control.BeginInvoke(action);
                }
                catch (InvalidOperationException)
                {
                    // Control was disposed between the checks and BeginInvoke.
                }

                return;
            }

            action();
        }
    }
}
