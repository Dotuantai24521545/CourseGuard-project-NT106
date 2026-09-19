namespace CourseGuard.Backend.Services.Classroom
{
    public static class ClassroomMessageType
    {
        public const string JoinRoom = "JOIN_ROOM";
        public const string LeaveRoom = "LEAVE_ROOM";
        public const string Chat = "CHAT";
        public const string RaiseHand = "RAISE_HAND";
        public const string LowerHand = "LOWER_HAND";
        public const string MicOn = "MIC_ON";
        public const string MicOff = "MIC_OFF";
        public const string CamOn = "CAM_ON";
        public const string CamOff = "CAM_OFF";
        public const string ClassOpened = "CLASS_OPENED";
        public const string ClassClosed = "CLASS_CLOSED";
        public const string ParticipantList = "PARTICIPANT_LIST";
        public const string TeacherMuteStudent = "TEACHER_MUTE_STUDENT";
        public const string TeacherMuteAll = "TEACHER_MUTE_ALL";
        public const string TeacherKickStudent = "TEACHER_KICK_STUDENT";
        public const string Reaction = "REACTION";
        public const string Ping = "PING";
        public const string Pong = "PONG";
        public const string VideoFrame = "VIDEO_FRAME";
        public const string ScreenShareOn = "SCREEN_SHARE_ON";
        public const string ScreenShareOff = "SCREEN_SHARE_OFF";
        public const string ScreenShareFrame = "SCREEN_SHARE_FRAME";
        public const string WindowShareOn = "WINDOW_SHARE_ON";
        public const string Error = "ERROR";
    }
}
