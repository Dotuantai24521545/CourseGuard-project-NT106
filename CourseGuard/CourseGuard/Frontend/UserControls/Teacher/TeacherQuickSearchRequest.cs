namespace CourseGuard.Frontend.UserControls.Teacher
{
    public sealed class TeacherQuickSearchRequest
    {
        public string Kind { get; init; } = string.Empty;
        public int Id { get; init; }
        public int? ParentId { get; init; }
        public string Keyword { get; init; } = string.Empty;
    }
}
