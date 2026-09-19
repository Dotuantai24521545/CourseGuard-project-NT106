namespace CourseGuard.Backend.Models
{
    public class TeacherExamQuestionModel
    {
        public int Id { get; set; }
        public int ExamId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string OptionA { get; set; } = string.Empty;
        public string OptionB { get; set; } = string.Empty;
        public string OptionC { get; set; } = string.Empty;
        public string OptionD { get; set; } = string.Empty;
        public string CorrectOption { get; set; } = "A";
        public decimal Points { get; set; }
        public int DisplayOrder { get; set; }
        public string Difficulty { get; set; } = "MEDIUM";
        public string? Chapter { get; set; }
        public string QuestionType { get; set; } = "MULTIPLE_CHOICE";
    }
}
