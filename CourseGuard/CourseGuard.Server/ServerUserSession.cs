using System.Security.Claims;
namespace CourseGuard.Backend.Security;
// Request identity is isolated by async execution context, never shared across clients.
public static class UserSessionContext
{
    private static readonly AsyncLocal<ClaimsPrincipal?> Current = new();
    public static void SetPrincipal(ClaimsPrincipal principal) => Current.Value = principal;
    public static int? CurrentUserId => int.TryParse(Current.Value?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public static string CurrentRole => Current.Value?.FindFirstValue(ClaimTypes.Role) ?? "";
    public static bool IsAdmin() => CurrentRole == "ADMIN";
}
