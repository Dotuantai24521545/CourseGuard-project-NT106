using CourseGuard.Backend.Models;

namespace CourseGuard.Backend.Services
{
    public static class ExamScoringService
    {
        public static decimal CalculateScore(IEnumerable<TeacherExamQuestionModel> questions, IReadOnlyDictionary<int, string> selectedOptions)
        {
            decimal total = 0m;
            foreach (TeacherExamQuestionModel question in questions)
            {
                if (!selectedOptions.TryGetValue(question.Id, out string? selected))
                    continue;

                if (StudentExamScoringService.IsCorrect(
                        new StudentExamScoringQuestion(question.Id, question.CorrectOption, question.Points), selected))
                    total += question.Points;
            }

            return Math.Round(total, 2, MidpointRounding.AwayFromZero);
        }

    }
}
