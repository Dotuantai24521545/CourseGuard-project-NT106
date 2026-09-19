using System;

namespace CourseGuard.Backend.Services.Monitoring
{
    public sealed class ScreenMonitorConnectionLostEventArgs : EventArgs
    {
        public ScreenMonitorConnectionLostEventArgs(int examId, int studentId, int attemptId, TimeSpan disconnectedFor)
        {
            ExamId = examId;
            StudentId = studentId;
            AttemptId = attemptId;
            DisconnectedFor = disconnectedFor;
        }

        public int ExamId { get; }
        public int StudentId { get; }
        public int AttemptId { get; }
        public TimeSpan DisconnectedFor { get; }
    }
}
