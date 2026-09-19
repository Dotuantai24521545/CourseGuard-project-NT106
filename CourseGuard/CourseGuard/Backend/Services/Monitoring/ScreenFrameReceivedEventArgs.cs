using System;

namespace CourseGuard.Backend.Services.Monitoring
{
    public class ScreenFrameReceivedEventArgs : EventArgs
    {
        public ScreenFrameReceivedEventArgs(byte frameType, int examId, int studentId, int attemptId, byte[] jpegBytes)
        {
            FrameType = frameType;
            ExamId = examId;
            StudentId = studentId;
            AttemptId = attemptId;
            JpegBytes = jpegBytes;
        }

        public byte FrameType { get; }
        public int ExamId { get; }
        public int StudentId { get; }
        public int AttemptId { get; }
        public byte[] JpegBytes { get; }
    }
}
