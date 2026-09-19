using System;
using System.Collections.Generic;

namespace CourseGuard.Backend.Models
{
    public class StudentExamTakingModel
    {
        public int AttemptId { get; set; }
        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public int MaxViolations { get; set; }
        public DateTime StartTime { get; set; }
        public List<StudentExamTakingQuestionModel> Questions { get; } = new();
    }

    public class StudentExamTakingQuestionModel
    {
        public int Id { get; set; }
        public int DisplayOrder { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string OptionA { get; set; } = string.Empty;
        public string OptionB { get; set; } = string.Empty;
        public string OptionC { get; set; } = string.Empty;
        public string OptionD { get; set; } = string.Empty;
        public string SelectedOption { get; set; } = string.Empty;
        public bool IsMarkedForReview { get; set; }
    }

    public class StudentExamSubmitResultModel
    {
        public bool Success { get; set; }
        public decimal Score { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
