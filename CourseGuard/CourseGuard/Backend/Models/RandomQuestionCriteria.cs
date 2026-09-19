namespace CourseGuard.Backend.Models
{
    public class RandomQuestionCriteria
    {
        public string Difficulty { get; set; } = "MEDIUM";
        public string? Chapter { get; set; }
        public int Count { get; set; }
    }
}
