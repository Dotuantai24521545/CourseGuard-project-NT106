using System.Security.Claims;
using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
internal static class CourseApi
{
    public static void MapCourseApi(this RouteGroupBuilder api)
    {
        var group=api.MapGroup("/course");
        group.MapPost("/GetAllCourses", (ClaimsPrincipal principal, CourseGuardDbContext db) => Results.Ok(new CourseController(db).GetAllCourses())).RequireAuthorization(p=>p.RequireRole("ADMIN"));
        group.MapPost("/AddCourse", (AddCourseRequest request, ClaimsPrincipal principal, CourseGuardDbContext db) => Results.Ok(new CourseController(db).AddCourse(request.course))).RequireAuthorization(p => p.RequireRole("ADMIN"));
        group.MapPost("/UpdateCourse", (UpdateCourseRequest request, ClaimsPrincipal principal, CourseGuardDbContext db) => Results.Ok(new CourseController(db).UpdateCourse(request.course))).RequireAuthorization(p => p.RequireRole("ADMIN"));
        group.MapPost("/DeleteCourse", (DeleteCourseRequest request, ClaimsPrincipal principal, CourseGuardDbContext db) => Results.Ok(new CourseController(db).DeleteCourse(request.courseId))).RequireAuthorization(p => p.RequireRole("ADMIN"));
        group.MapPost("/ApproveCourse", (ApproveCourseRequest request, ClaimsPrincipal principal, CourseGuardDbContext db) => Results.Ok(new CourseController(db).ApproveCourse(request.courseId))).RequireAuthorization(p => p.RequireRole("ADMIN"));
        group.MapPost("/RejectCourse", (RejectCourseRequest request, ClaimsPrincipal principal, CourseGuardDbContext db) => Results.Ok(new CourseController(db).RejectCourse(request.courseId, request.reason))).RequireAuthorization(p => p.RequireRole("ADMIN"));
        group.MapPost("/EnrollStudent", (EnrollStudentRequest request, ClaimsPrincipal principal, CourseGuardDbContext db) => Results.Ok(new CourseController(db).EnrollStudent(request.courseId, request.studentId))).RequireAuthorization(p => p.RequireRole("ADMIN"));
        group.MapPost("/GetStudentEnrolledCourseIds", (ClaimsPrincipal principal, CourseGuardDbContext db) => Results.Ok(new CourseController(db).GetStudentEnrolledCourseIds(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!)))).RequireAuthorization(p => p.RequireRole("STUDENT"));
        group.MapPost("/StudentJoinCourse", (StudentJoinCourseRequest request, ClaimsPrincipal principal, CourseGuardDbContext db) => Results.Ok(new CourseController(db).StudentJoinCourse(request.courseId, int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!)))).RequireAuthorization(p => p.RequireRole("STUDENT"));
        group.MapPost("/GetPendingEnrollments", (GetPendingEnrollmentsRequest request, ClaimsPrincipal principal, CourseGuardDbContext db) => Results.Ok(new CourseController(db).GetPendingEnrollments(request.courseId))).RequireAuthorization(p => p.RequireRole("ADMIN"));
        group.MapPost("/GetEnrollmentsByStatus", (GetEnrollmentsByStatusRequest request, ClaimsPrincipal principal, CourseGuardDbContext db) => Results.Ok(new CourseController(db).GetEnrollmentsByStatus(request.courseId, request.status))).RequireAuthorization(p => p.RequireRole("ADMIN"));
        group.MapPost("/ApproveEnrollment", (ApproveEnrollmentRequest request, ClaimsPrincipal principal, CourseGuardDbContext db) => Results.Ok(new CourseController(db).ApproveEnrollment(request.courseId, request.studentId))).RequireAuthorization(p => p.RequireRole("ADMIN"));
        group.MapPost("/RejectEnrollment", (RejectEnrollmentRequest request, ClaimsPrincipal principal, CourseGuardDbContext db) => Results.Ok(new CourseController(db).RejectEnrollment(request.courseId, request.studentId))).RequireAuthorization(p => p.RequireRole("ADMIN"));
        group.MapPost("/GetAvailableCourses", (ClaimsPrincipal principal, CourseGuardDbContext db) => Results.Ok(new CourseController(db).GetAvailableCourses(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!)))).RequireAuthorization(p => p.RequireRole("STUDENT"));
        group.MapPost("/GetEnrollmentStatus", (GetEnrollmentStatusRequest request, ClaimsPrincipal principal, CourseGuardDbContext db) => Results.Ok(new CourseController(db).GetEnrollmentStatus(request.courseId, int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!)))).RequireAuthorization(p => p.RequireRole("STUDENT"));
        group.MapPost("/GetMyEnrollments", (ClaimsPrincipal principal, CourseGuardDbContext db) => Results.Ok(new CourseController(db).GetMyEnrollments(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!)))).RequireAuthorization(p => p.RequireRole("STUDENT"));
        group.MapPost("/RequestEnrollment", (RequestEnrollmentRequest request, ClaimsPrincipal principal, CourseGuardDbContext db) => Results.Ok(new CourseController(db).RequestEnrollment(request.courseId, int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!)))).RequireAuthorization(p => p.RequireRole("STUDENT"));
        group.MapPost("/DropCourse", (DropCourseRequest request, ClaimsPrincipal principal, CourseGuardDbContext db) => Results.Ok(new CourseController(db).DropCourse(request.courseId, int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!)))).RequireAuthorization(p => p.RequireRole("STUDENT"));
        group.MapPost("/GetEnrolledCount", (GetEnrolledCountRequest request, ClaimsPrincipal principal, CourseGuardDbContext db) => Results.Ok(new CourseController(db).GetEnrolledCount(request.courseId)));
        group.MapPost("/GetStudentOnlineSessions", (ClaimsPrincipal principal, CourseGuardDbContext db) => Results.Ok(new CourseController(db).GetStudentOnlineSessions(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!)))).RequireAuthorization(p => p.RequireRole("STUDENT"));
    }
    internal record AddCourseRequest(CourseModel course);
    internal record UpdateCourseRequest(CourseModel course);
    internal record DeleteCourseRequest(int courseId);
    internal record ApproveCourseRequest(int courseId);
    internal record RejectCourseRequest(int courseId, string reason);
    internal record EnrollStudentRequest(int courseId, int studentId);
    internal record StudentJoinCourseRequest(int courseId);
    internal record GetPendingEnrollmentsRequest(int? courseId);
    internal record GetEnrollmentsByStatusRequest(int courseId, string status);
    internal record ApproveEnrollmentRequest(int courseId, int studentId);
    internal record RejectEnrollmentRequest(int courseId, int studentId);
    internal record GetEnrollmentStatusRequest(int courseId);
    internal record RequestEnrollmentRequest(int courseId);
    internal record DropCourseRequest(int courseId);
    internal record GetEnrolledCountRequest(int courseId);
}
