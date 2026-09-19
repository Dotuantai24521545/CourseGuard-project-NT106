namespace CourseGuard.Frontend.UserControls.Shared.CourseWorkspace;

public sealed record CourseSelectorItem(
    int CourseId,
    string Name,
    string SecondaryText,
    string Status,
    CourseWorkspaceRole Role);
