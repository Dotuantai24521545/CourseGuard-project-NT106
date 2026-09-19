using CourseGuard.Backend.Models;

namespace CourseGuard.Backend.Services.Classroom
{
    public sealed class ClassroomSignalReceivedEventArgs : EventArgs
    {
        public ClassroomSignalReceivedEventArgs(ClassroomSignalModel signal)
        {
            Signal = signal;
        }

        public ClassroomSignalModel Signal { get; }
    }

    public sealed class ClassroomClientConnectedEventArgs : EventArgs
    {
        public ClassroomClientConnectedEventArgs(ClassroomConnectionState client)
        {
            Client = client;
        }

        public ClassroomConnectionState Client { get; }
    }

    public sealed class ClassroomStatusEventArgs : EventArgs
    {
        public ClassroomStatusEventArgs(string status)
        {
            Status = status;
        }

        public string Status { get; }
    }
}
