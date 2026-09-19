namespace CourseGuard.Backend.Models
{
    public class ClassroomChatMessageModel
    {
        public int Id { get; set; }
        public int SessionId { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
    }
}
