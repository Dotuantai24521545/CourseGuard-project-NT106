using CourseGuard.Backend.Data;
using System.Security.Claims;
using CourseGuard.Backend.Models;
using System.Text.Json;
internal static class ChatExtrasApi
{
    public static void MapChatExtras(this RouteGroupBuilder api)
    {
        api.MapPost("/courses/{course:int}/images",async(int course,Images r,ClaimsPrincipal p,CourseGuardDbContext db,CancellationToken ct)=>
        {
            if(!db.CanAccessCourseChat(Id(p),course))return Results.Forbid();
            if(r.Items is null||r.Items.Count is <1 or >5||r.Caption.Length>4000||r.Items.Any(i=>i.Bytes is null||i.Bytes.Length==0)||r.Items.Sum(i=>(long)i.Bytes.Length)>5*1024*1024)return Results.BadRequest();
            var attachments=new List<ChatImageAttachmentModel>();
            foreach(var item in r.Items)
            {
                if(item.MimeType is not ("image/jpeg" or "image/png"))return Results.BadRequest();
                var png=item.Bytes.Length>=8&&item.Bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10});
                var jpg=item.Bytes.Length>=3&&item.Bytes[0]==255&&item.Bytes[1]==216&&item.Bytes[2]==255;
                if(!(item.MimeType=="image/png"?png:jpg))return Results.BadRequest();
                attachments.Add(new(){Url="data:"+item.MimeType+";base64,"+Convert.ToBase64String(item.Bytes),Name=Path.GetFileName(item.FileName),Size=item.Bytes.Length,Mime=item.MimeType});
            }
            var json=JsonSerializer.Serialize(attachments,new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return Results.Ok(await db.SendChatImageGroupMessageAsync(course,Id(p),r.Caption,json,$"{attachments.Count} ảnh",attachments.Sum(a=>a.Size),"application/vnd.courseguard.chat-images+json",ct));
        });
        api.MapPost("/courses/{course:int}/files",async(int course,Attachment r,ClaimsPrincipal p,CourseGuardDbContext db,CancellationToken ct)=>
        {
            var id=Id(p);
            if(!db.CanAccessCourseChat(id,course))return Results.Forbid();
            if(r.Bytes is null||r.Bytes.Length is 0 or >5242880||r.Caption.Length>4000||r.FileName.Length>250||r.MimeType.Length>120)return Results.BadRequest();
            var name=Path.GetFileName(r.FileName);
            var message=await db.SendChatFileMessageAsync(course,id,r.Caption,"",name,r.Bytes.Length,r.MimeType,r.Bytes,ct);
            return Results.Ok(message);
        });
        api.MapGet("/courses/{course:int}/files/{message:int}",async(int course,int message,ClaimsPrincipal p,CourseGuardDbContext db,CancellationToken ct)=>
        {
            if(!db.CanAccessCourseChat(Id(p),course))return Results.Forbid();
            return Results.Ok(await db.GetChatFileContentAsync(course,message,ct));
        });
        api.MapPost("/courses/{course:int}/polls",async(int course,Poll r,ClaimsPrincipal p,CourseGuardDbContext db,CancellationToken ct)=>
        {
            if(!db.CanCreateCoursePoll(Id(p),course))return Results.Forbid();
            if(string.IsNullOrWhiteSpace(r.Question)||r.Question.Length>1000||r.Options is null||r.Options.Count is <2 or >10||r.Options.Any(v=>string.IsNullOrWhiteSpace(v)||v.Length>250))return Results.BadRequest();
            return Results.Ok(await db.CreatePollMessageAsync(Id(p),course,r.Question,r.Options,ct));
        }).RequireAuthorization(p=>p.RequireRole("TEACHER"));
        api.MapPost("/polls/{poll:int}/vote",async(int poll,Vote r,ClaimsPrincipal p,CourseGuardDbContext db,CancellationToken ct)=>Results.Ok(await db.VotePollAsync(Id(p),poll,r.OptionId,ct)));
        api.MapPost("/polls/{poll:int}/close",async(int poll,ClaimsPrincipal p,CourseGuardDbContext db,CancellationToken ct)=>Results.Ok(await db.ClosePollAsync(Id(p),poll,ct))).RequireAuthorization(p=>p.RequireRole("TEACHER"));
    }
    private static int Id(ClaimsPrincipal p)=>int.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
    internal record Attachment(string FileName,string MimeType,string Caption,byte[] Bytes);
    internal record Poll(string Question,List<string> Options);
    internal record Vote(int OptionId);
    internal record Images(string Caption,List<Attachment> Items);
}
