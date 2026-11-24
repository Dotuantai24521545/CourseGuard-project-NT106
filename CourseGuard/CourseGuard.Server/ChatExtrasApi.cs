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
            var error=ChatUploadPolicy.CheckImageGroup(r.Caption,r.Items?.Select(i=>new ChatUploadPolicy.ImageItem(i.FileName,i.MimeType,i.Bytes)).ToList());
            if(error is not null)return Results.BadRequest(new{error});
            var attachments=new List<ChatImageAttachmentModel>();
            foreach(var item in r.Items!)
                attachments.Add(new(){Url="data:"+item.MimeType+";base64,"+Convert.ToBase64String(item.Bytes),Name=ChatUploadPolicy.SafeFileName(item.FileName),Size=item.Bytes.Length,Mime=item.MimeType});
            var json=JsonSerializer.Serialize(attachments,new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return Results.Ok(await db.SendChatImageGroupMessageAsync(course,Id(p),r.Caption??"",json,$"{attachments.Count} ảnh",attachments.Sum(a=>a.Size),"application/vnd.courseguard.chat-images+json",ct));
        });
        api.MapPost("/courses/{course:int}/files",async(int course,Attachment r,ClaimsPrincipal p,CourseGuardDbContext db,CancellationToken ct)=>
        {
            var id=Id(p);
            if(!db.CanAccessCourseChat(id,course))return Results.Forbid();
            var check=ChatUploadPolicy.CheckFile(r.FileName,r.Caption,r.Bytes);
            if(!check.Allowed)return Results.BadRequest(new{error=check.Error});
            var message=await db.SendChatFileMessageAsync(course,id,r.Caption??"","",check.FileName,r.Bytes.Length,check.MimeType,r.Bytes,ct);
            if(message is null)return Results.Problem("Không lưu được tệp.");
            return Results.Ok(message);
        });
        api.MapGet("/courses/{course:int}/files/{message:int}",async(int course,int message,ClaimsPrincipal p,CourseGuardDbContext db,CancellationToken ct)=>
        {
            if(!db.CanAccessCourseChat(Id(p),course))return Results.Forbid();
            // null (not 404) for a missing file: the client treats it as "no content" while hydrating chat images.
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
