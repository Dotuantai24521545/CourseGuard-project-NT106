namespace CourseGuard.Backend.Services
{
    public static class QuestionMetadataNormalizer
    {
        public const string Easy = "EASY";
        public const string Medium = "MEDIUM";
        public const string Hard = "HARD";
        public const string MultipleChoice = "MULTIPLE_CHOICE";
        public const string TrueFalse = "TRUE_FALSE";
        public const string FillBlank = "FILL_BLANK";

        public static string NormalizeDifficulty(string? value)
        {
            string normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
            return normalized is Easy or Medium or Hard ? normalized : Medium;
        }

        public static string NormalizeQuestionType(string? value)
        {
            string normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
            return normalized is MultipleChoice or TrueFalse or FillBlank ? normalized : MultipleChoice;
        }

        public static string? NormalizeChapter(string? value)
        {
            string normalized = (value ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }
    }
}
