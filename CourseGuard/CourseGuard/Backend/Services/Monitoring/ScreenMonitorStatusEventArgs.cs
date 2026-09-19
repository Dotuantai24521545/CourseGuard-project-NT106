using System;

namespace CourseGuard.Backend.Services.Monitoring
{
    public class ScreenMonitorStatusEventArgs : EventArgs
    {
        public ScreenMonitorStatusEventArgs(string status)
        {
            Status = status;
        }

        public string Status { get; }
    }
}
