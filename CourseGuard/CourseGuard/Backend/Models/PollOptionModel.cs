namespace CourseGuard.Backend.Models
{
    public class PollOptionModel
    {
        public int Id { get; set; }
        public int PollId { get; set; }
        public string OptionText { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public int VoteCount { get; set; }
    }
}
