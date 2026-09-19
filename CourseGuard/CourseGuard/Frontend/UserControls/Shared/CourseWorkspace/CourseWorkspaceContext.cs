namespace CourseGuard.Frontend.UserControls.Shared.CourseWorkspace;

public sealed record CourseWorkspaceContext(
    int UserId,
    int CourseId,
    string CourseName,
    string SecondaryText,
    string Status,
    CourseWorkspaceRole Role);
