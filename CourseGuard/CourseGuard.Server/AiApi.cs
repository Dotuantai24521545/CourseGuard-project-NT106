using CourseGuard.Backend.Services;
internal static class AiApi
{
    public static void MapAi(this RouteGroupBuilder api)
    {
        var group=api.MapGroup("/ai").RequireRateLimiting("ai");
        group.MapPost("/questions", async (Questions r) =>
        {
            if (!Text(r.TopicContent) || r.EasyCount<0 || r.MediumCount<0 || r.HardCount<0 || (long)r.EasyCount+r.MediumCount+r.HardCount is <1 or >30) return Results.BadRequest();
            return Results.Ok(await new AiQuestionGeneratorService().GenerateQuestionsAsync(r.TopicContent,r.EasyCount,r.MediumCount,r.HardCount));
        }).RequireAuthorization(p=>p.RequireRole("TEACHER","ADMIN"));
        group.MapPost("/grade", async (Grade r) =>
        {
            if(!Text(r.QuestionText)||!Text(r.SampleAnswer)||!Text(r.StudentSubmission)||r.MaxPoints<=0||r.MaxPoints>100) return Results.BadRequest();
            return Results.Ok(await new AiQuestionGeneratorService().GradeEssayAsync(r.QuestionText,r.SampleAnswer,r.StudentSubmission,r.MaxPoints));
        }).RequireAuthorization(p=>p.RequireRole("TEACHER","ADMIN"));
        group.MapPost("/tutor", async (Tutor r) =>
        {
            if(!Text(r.CourseName)||!Text(r.LectureContext)||!Text(r.StudentQuestion))return Results.BadRequest();
            return Results.Ok(await new AiQuestionGeneratorService().AskVirtualTutorAsync(r.CourseName,r.LectureContext,r.StudentQuestion));
        });
        group.MapPost("/flashcards", async (Cards r) =>
        {
            if(!Text(r.LectureContent)||r.Count is <1 or >30)return Results.BadRequest();
            return Results.Ok(await new AiQuestionGeneratorService().GenerateFlashcardsAsync(r.LectureContent,r.Count));
        });
    }
    private static bool Text(string value)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=30000;
    internal record Questions(string TopicContent,int EasyCount,int MediumCount,int HardCount);
    internal record Grade(string QuestionText,string SampleAnswer,string StudentSubmission,decimal MaxPoints);
    internal record Tutor(string CourseName,string LectureContext,string StudentQuestion);
    internal record Cards(string LectureContent,int Count);
}
