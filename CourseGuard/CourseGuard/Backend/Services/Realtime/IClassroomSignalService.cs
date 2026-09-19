using System.Threading.Tasks;

namespace CourseGuard.Backend.Services.Realtime
{
    public interface IClassroomSignalService
    {
        void StartListening();
        Task BroadcastClassOpened(int sessionId);
        Task BroadcastClassClosed(int sessionId);
    }
}
