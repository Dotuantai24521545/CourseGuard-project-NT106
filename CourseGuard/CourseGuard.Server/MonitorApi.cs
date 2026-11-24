using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using System.Security.Claims;
using Npgsql;
internal static class MonitorApi
{
    public static void MapMonitor(this RouteGroupBuilder api)
    {
        api.MapPost("/storage/evidence",async(Evidence r,ClaimsPrincipal p,CourseGuardDbContext db,CancellationToken ct)=>
        {
            if(r.Bytes is null||r.Bytes.Length is <3 or >5242880||r.ContentType!="image/jpeg"||r.Bytes[0]!=255||r.Bytes[1]!=216||r.Bytes[2]!=255)return Results.BadRequest();
            await using var c=db.CreateConnection();await c.OpenAsync(ct);
            await using var schema=new NpgsqlCommand("CREATE TABLE IF NOT EXISTS server_evidence(id UUID PRIMARY KEY, owner_id INT NOT NULL REFERENCES users(id), content BYTEA NOT NULL, created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP)",c);
            await schema.ExecuteNonQueryAsync(ct);
            var key=Guid.NewGuid();
            await using var q=new NpgsqlCommand("INSERT INTO server_evidence(id,owner_id,content) VALUES(@id,@owner,@content)",c);
            q.Parameters.AddWithValue("id",key);q.Parameters.AddWithValue("owner",int.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!));q.Parameters.AddWithValue("content",r.Bytes);
            await q.ExecuteNonQueryAsync(ct);
            return Results.Ok("api/monitor/evidence/"+key);
        }).RequireAuthorization(p=>p.RequireRole("TEACHER"));
        api.MapPost("/monitor/violations", async (ViolationModel input, ClaimsPrincipal p, CourseGuardDbContext db, CancellationToken ct) =>
        {
            int user = int.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if(input.ExamAttemptId is not int attempt || input.Type.Length>100 || input.ImageUrl?.Length>7*1024*1024) return Results.BadRequest();
            var teacher=p.IsInRole("TEACHER");
            if(!await CanRead(db,user,attempt,teacher,ct)) return Results.Forbid();
            await using var c=db.CreateConnection();await c.OpenAsync(ct);
            if(!string.IsNullOrEmpty(input.ImageUrl))
            {
                if(!input.ImageUrl.StartsWith("api/monitor/evidence/")||!Guid.TryParse(input.ImageUrl[21..],out var evidence))return Results.BadRequest();
                await using var check=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM server_evidence WHERE id=@id AND owner_id=@owner)",c);
                check.Parameters.AddWithValue("id",evidence);check.Parameters.AddWithValue("owner",user);
                if(!Convert.ToBoolean(await check.ExecuteScalarAsync(ct)))return Results.Forbid();
            }
            await using var q=new NpgsqlCommand("SELECT student_id FROM exam_attempts WHERE id=@id",c);q.Parameters.AddWithValue("id",attempt);
            input.UserId=Convert.ToInt32(await q.ExecuteScalarAsync(ct)); input.Severity="MEDIUM"; input.ActionTaken=teacher?"RECORDED_MANUALLY":null;
            return Results.Ok(await new ViolationRepository().InsertViolationAsync(input));
        }).RequireAuthorization(p=>p.RequireRole("STUDENT","TEACHER"));
        api.MapGet("/monitor/attempts/{id:int}/violations", async (int id, ClaimsPrincipal p, CourseGuardDbContext db, CancellationToken ct) =>
        {
            if(!await CanRead(db,int.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!),id,true,ct))return Results.Forbid();
            return Results.Ok(await new ViolationRepository().GetViolationsByAttemptIdAsync(id));
        });
        api.MapGet("/monitor/evidence/{id:guid}",async(Guid id,ClaimsPrincipal p,CourseGuardDbContext db,CancellationToken ct)=>
        {
            await using var c=db.CreateConnection();await c.OpenAsync(ct);
            await using var q=new NpgsqlCommand("SELECT f.content FROM server_evidence f WHERE f.id=@id AND (f.owner_id=@user OR EXISTS(SELECT 1 FROM violations v JOIN exam_attempts a ON a.id=v.exam_attempt_id JOIN exams e ON e.id=a.exam_id JOIN courses c ON c.id=e.course_id WHERE v.image_url=@url AND (a.student_id=@user OR c.teacher_id=@user)))",c);
            q.Parameters.AddWithValue("id",id);q.Parameters.AddWithValue("user",int.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!));q.Parameters.AddWithValue("url","api/monitor/evidence/"+id);
            var bytes=await q.ExecuteScalarAsync(ct);return bytes is byte[] b?Results.Ok(b):Results.NotFound();
        });
    }
    internal record Evidence(byte[] Bytes,string FileName,string ContentType);
    private static async Task<bool> CanRead(CourseGuardDbContext db,int user,int attempt,bool includeTeacher,CancellationToken ct)
    {
        await using var c=db.CreateConnection();await c.OpenAsync(ct);
        await using var q=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM exam_attempts a JOIN exams e ON e.id=a.exam_id JOIN courses c ON c.id=e.course_id WHERE a.id=@attempt AND (a.student_id=@user OR (@teacher AND c.teacher_id=@user)))",c);
        q.Parameters.AddWithValue("attempt",attempt);q.Parameters.AddWithValue("user",user);q.Parameters.AddWithValue("teacher",includeTeacher);
        return Convert.ToBoolean(await q.ExecuteScalarAsync(ct));
    }
}
