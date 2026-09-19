using System;
using System.Collections.Generic;

namespace CourseGuard.Backend.Models
{
    public class PollModel
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public int MessageId { get; set; }
        public int CreatedBy { get; set; }
        public string Question { get; set; } = string.Empty;
        public bool IsClosed { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public int TotalVotes { get; set; }
        public int? MySelectedOptionId { get; set; }
        public List<PollOptionModel> Options { get; set; } = new();
    }
}
