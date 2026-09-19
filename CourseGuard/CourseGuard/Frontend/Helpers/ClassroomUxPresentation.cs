namespace CourseGuard.Frontend.Helpers
{
    public sealed class ClassroomUxPresentation
    {
        public string StatusText { get; init; } = string.Empty;
        public string DetailText { get; init; } = string.Empty;
        public string CameraActionText { get; init; } = string.Empty;
        public string MicActionText { get; init; } = string.Empty;
        public string ShareActionText { get; init; } = string.Empty;
        public string Tone { get; init; } = "Neutral";
    }

    public static class ClassroomUxPresenter
    {
        public static ClassroomUxPresentation Present(
            bool isTeacher,
            bool isConnected,
            bool isCameraOn,
            bool isMicOn,
            bool isSharingScreen,
            int participantCount)
        {
            return new ClassroomUxPresentation
            {
                StatusText = GetStatusText(isTeacher, isConnected),
                DetailText = GetDetailText(isTeacher, isCameraOn, isMicOn, participantCount),
                CameraActionText = isCameraOn ? "Tắt Camera" : "Bật Camera",
                MicActionText = isMicOn ? "Tắt Mic" : "Bật Mic",
                ShareActionText = isSharingScreen ? "Dừng trình bày" : "Trình bày màn hình",
                Tone = isConnected ? "Success" : "Danger"
            };
        }

        private static string GetStatusText(bool isTeacher, bool isConnected)
        {
            if (!isConnected)
                return "Mất kết nối";

            return isTeacher ? "Đang dạy trực tuyến" : "Đã vào lớp";
        }

        private static string GetDetailText(bool isTeacher, bool isCameraOn, bool isMicOn, int participantCount)
        {
            string cameraState = isCameraOn ? "camera đang bật" : "camera đang tắt";
            string micState = isMicOn ? "mic đang bật" : "mic đang tắt";

            return isTeacher
                ? $"{participantCount} người tham gia - {cameraState} - {micState}."
                : $"{cameraState} - {micState}.";
        }
    }
}
