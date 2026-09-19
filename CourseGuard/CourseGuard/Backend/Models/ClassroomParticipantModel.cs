namespace CourseGuard.Backend.Models
{
    public class ClassroomParticipantModel
    {
        public int Id { get; set; }
        public int SessionId { get; set; }
        public int UserId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public bool IsHandRaised { get; set; }
        public DateTime? RaisedHandAt { get; set; }
        public bool IsMuted { get; set; }
        public bool IsCameraOn { get; set; }
        public string AvatarPath { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
        public DateTime? LeftAt { get; set; }
        public DateTime LastSeenAt { get; set; }
    }
}
