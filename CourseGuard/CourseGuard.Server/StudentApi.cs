using System.Security.Claims;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
internal static class StudentApi
{
    public static void MapStudentApi(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/student").RequireAuthorization(p => p.RequireRole("STUDENT"));
        group.MapPost("/submissions", async (AssignmentSubmissionModel submission, ClaimsPrincipal p, CourseGuardDbContext db, CancellationToken ct) =>
        {
            if (submission.FileContent is null || submission.FileContent.Length > 5 * 1024 * 1024 || submission.FileName.Length > 250) return Results.BadRequest();
            submission.StudentId = int.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
            submission.FileName = Path.GetFileName(submission.FileName);
            submission.FileSize = submission.FileContent.Length;
            submission.Score = null; submission.Feedback = "";
            var saved=await db.SubmitAssignmentAsync(submission, ct);
            if(saved)
            {
                new NotificationRepository().Create(submission.StudentId,"Nộp bài thành công","Bài làm đã được lưu trên server.","Assignment","Informational","Assignment",submission.AssignmentId);
            }
            return Results.Ok(saved);
        });
        group.MapPost("/attendance/{id:int}/in", async (int id, ClaimsPrincipal p, CourseGuardDbContext db, CancellationToken ct) =>
            Results.Ok(await db.LogAttendanceInAsync(int.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!), id, ct)));
        group.MapPost("/attendance/{id:int}/out", async (int id, ClaimsPrincipal p, CourseGuardDbContext db, CancellationToken ct) =>
        {
            await using var connection = db.CreateConnection(); await connection.OpenAsync(ct);
            await using var command = new Npgsql.NpgsqlCommand("UPDATE attendance_logs SET left_at=CURRENT_TIMESTAMP, duration_minutes=GREATEST(CAST(EXTRACT(EPOCH FROM (CURRENT_TIMESTAMP-joined_at))/60 AS INTEGER),0), is_valid=(EXTRACT(EPOCH FROM (CURRENT_TIMESTAMP-joined_at))/60)>=30 WHERE id=@id AND student_id=@user AND left_at IS NULL", connection);
            command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("user", int.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!));
            return Results.Ok(await command.ExecuteNonQueryAsync(ct) == 1);
        });
        group.MapGet("/documents", (ClaimsPrincipal p, CourseGuardDbContext db) =>
            new MaterialStore(db).QueryDocuments(int.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!)));
        group.MapGet("/documents/{id:int}/content", (int id, ClaimsPrincipal p, CourseGuardDbContext db) =>
            Results.Ok(new MaterialStore(db).LoadDocumentContent(id, int.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!))));
        group.MapPost("/GetStudentProfile", (ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(db.GetStudentProfile(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!))));
        group.MapPost("/CountActiveEnrollments", (ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(db.CountActiveEnrollments(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!))));
        group.MapPost("/CountUnreadNotifications", (ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(db.CountUnreadNotifications(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!))));
        group.MapPost("/CountStudentScoreRecords", (ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(db.CountStudentScoreRecords(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!))));
        group.MapPost("/GetStudentAverageScore", (ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(db.GetStudentAverageScore(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!))));
        group.MapPost("/GetStudentExamAverageScore", (ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(db.GetStudentExamAverageScore(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!))));
        group.MapPost("/CountOpenExamsForStudent", (ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(db.CountOpenExamsForStudent(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!))));
        group.MapPost("/CountAvailableExamsForStudent", (ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(db.CountAvailableExamsForStudent(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!))));
        group.MapPost("/CountCompletedExamsForStudent", (ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(db.CountCompletedExamsForStudent(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!))));
        group.MapPost("/GetAvailableExamsForStudent", (GetAvailableExamsForStudentRequest request, ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(db.GetAvailableExamsForStudent(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), request.limit)));
        group.MapPost("/StartOrResumeStudentExam", (StartOrResumeStudentExamRequest request, ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(db.StartOrResumeStudentExam(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), request.examId)));
        group.MapPost("/SaveStudentExamAnswer", (SaveStudentExamAnswerRequest request, ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(db.SaveStudentExamAnswer(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), request.attemptId, request.examQuestionId, request.selectedOption)));
        group.MapPost("/SubmitStudentExamAttempt", (SubmitStudentExamAttemptRequest request, ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(db.SubmitStudentExamAttempt(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), request.attemptId)));
        group.MapPost("/GetActiveResultCourseFiltersForStudent", (ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(db.GetActiveResultCourseFiltersForStudent(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!))));
        group.MapPost("/GetStudentResultItems", (GetStudentResultItemsRequest request, ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(db.GetStudentResultItems(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), request.courseId, request.examTitleKeyword, request.limit)));
        group.MapPost("/HideStudentResult", (HideStudentResultRequest request, ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(db.HideStudentResult(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), request.attemptId)));
        group.MapPost("/GetStudentExamReview", (GetStudentExamReviewRequest request, ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(db.GetStudentExamReview(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), request.attemptId)));
        group.MapPost("/SearchStudentGlobal", (SearchStudentGlobalRequest request, ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(db.SearchStudentGlobal(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), request.keyword, request.limitPerGroup)));
        group.MapPost("/GetStudentEnrolledCourseIds", (ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(db.GetStudentEnrolledCourseIds(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!))));
        group.MapPost("/GetEnrollmentsByStudent", (ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(db.GetEnrollmentsByStudent(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!))));
        group.MapPost("/GetStudentOnlineSessions", (ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(db.GetStudentOnlineSessions(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!))));
        group.MapPost("/GetAvailableCourses", (ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(db.GetAvailableCourses(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!))));
        group.MapPost("/GetStudentLessonsAsync", async (ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(await db.GetStudentLessonsAsync(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), ct)));
        group.MapPost("/GetStudentLessonFileContentAsync", async (GetStudentLessonFileContentAsyncRequest request, ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(await db.GetStudentLessonFileContentAsync(request.lessonId, int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), ct)));
        group.MapPost("/GetStudentAssignmentsAsync", async (ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(await db.GetStudentAssignmentsAsync(int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), ct)));
        group.MapPost("/GetAssignmentContentAsync", async (GetAssignmentContentAsyncRequest request, ClaimsPrincipal principal, CourseGuardDbContext db, CancellationToken ct) => Results.Ok(await db.GetAssignmentContentAsync(request.assignmentId, int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), ct)));
    }
    internal record GetAvailableExamsForStudentRequest(int limit);
    internal record StartOrResumeStudentExamRequest(int examId);
    internal record SaveStudentExamAnswerRequest(int attemptId, int examQuestionId, string selectedOption);
    internal record SubmitStudentExamAttemptRequest(int attemptId);
    internal record GetStudentResultItemsRequest(int? courseId, string? examTitleKeyword, int limit);
    internal record HideStudentResultRequest(int attemptId);
    internal record GetStudentExamReviewRequest(int attemptId);
    internal record SearchStudentGlobalRequest(string keyword, int limitPerGroup);
    internal record GetStudentLessonFileContentAsyncRequest(int lessonId);
    internal record GetAssignmentContentAsyncRequest(int assignmentId);
}
