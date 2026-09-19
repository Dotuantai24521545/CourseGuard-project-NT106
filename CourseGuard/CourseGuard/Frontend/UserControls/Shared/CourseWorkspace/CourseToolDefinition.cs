using System;
using System.Windows.Forms;

namespace CourseGuard.Frontend.UserControls.Shared.CourseWorkspace;

public sealed record CourseToolDefinition(
    string Id,
    string Label,
    string IconKey,
    string Description,
    Func<CourseWorkspaceContext, Control> CreatePreview,
    Action<CourseWorkspaceContext> OpenDetail,
    string BadgeText = "");
