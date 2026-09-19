using System.Collections.Generic;

namespace CourseGuard.Backend.Models
{
    public class StudentExamReviewModel
    {
        public int AttemptId { get; set; }
        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public double Score { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public List<StudentExamReviewQuestionModel> Questions { get; } = new();
    }

    public class StudentExamReviewQuestionModel
    {
        public int DisplayOrder { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string OptionA { get; set; } = string.Empty;
        public string OptionB { get; set; } = string.Empty;
        public string OptionC { get; set; } = string.Empty;
        public string OptionD { get; set; } = string.Empty;
        public string CorrectOption { get; set; } = string.Empty;
        public string SelectedOption { get; set; } = string.Empty;
        public decimal Points { get; set; }
    }
}
