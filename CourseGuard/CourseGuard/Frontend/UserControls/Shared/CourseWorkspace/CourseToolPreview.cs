using System.Collections.Generic;

namespace CourseGuard.Frontend.UserControls.Shared.CourseWorkspace;

public sealed record CourseToolPreview(
    string Title,
    string Summary,
    IReadOnlyList<string> Rows,
    string PrimaryActionLabel);
