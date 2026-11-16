using System.Security.Claims;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
internal static class ProfileApi
{
    public static void MapProfiles(this RouteGroupBuilder api)
    {
        api.MapPost("/me/deadlines/check",async(ClaimsPrincipal p,CourseGuardDbContext db,CancellationToken ct)=>
        {
            using var service=new CourseGuard.Backend.Services.DeadlineReminderService(Id(p),db);
            int count=0;service.NotificationCreated+=(_,_)=>count++;
            await service.CheckNowAsync(ct);return Results.Ok(count);
        });
        api.MapGet("/me/notifications/count",(ClaimsPrincipal p,CourseGuardDbContext db)=>db.CountUnreadNotifications(Id(p)));
        api.MapGet("/me/activities",(int? limit,ClaimsPrincipal p,CourseGuardDbContext db)=>db.GetRecentUserActivitiesByUser(Id(p),Math.Clamp(limit??10,1,100)));
        api.MapPost("/student/profile",(StudentProfileModel profile,ClaimsPrincipal p,CourseGuardDbContext db)=>
        {
            profile.UserId=Id(p);return Results.Ok(db.UpsertStudentProfile(profile));
        }).RequireAuthorization(p=>p.RequireRole("STUDENT"));
        api.MapGet("/me/deadlines",(DateTime from,DateTime to,ClaimsPrincipal p,CourseGuardDbContext db)=>
            to<from||to-from>TimeSpan.FromDays(366)?Results.BadRequest():Results.Ok(db.GetUpcomingDeadlines(Id(p),from,to)));
        var admin=api.MapGroup("/admin").RequireAuthorization(p=>p.RequireRole("ADMIN"));
        admin.MapGet("/settings",()=>
        {
            var values=new CourseGuard.Backend.Controllers.SettingsController().LoadSettings();
            values["SMTP_PASS"]="";return Results.Ok(values);
        });
        admin.MapPost("/settings",(Dictionary<string,string> values)=>
        {
            var allowed=new[]{"SMTP_HOST","SMTP_PORT","SMTP_USER","SMTP_PASS","SMTP_FROM_EMAIL","SMTP_FROM_NAME","BRUTE_FORCE_LIMIT","BRUTE_FORCE_LOCKOUT_MINUTES","OTP_EXPIRY_MINUTES"};
            if(values.Any(v=>!allowed.Contains(v.Key)||v.Value is null||v.Value.Length>1000||v.Value.Contains('\r')||v.Value.Contains('\n')))return Results.BadRequest();
            if(values.TryGetValue("SMTP_PASS",out var password)&&string.IsNullOrWhiteSpace(password))values.Remove("SMTP_PASS");
            return Results.Ok(new CourseGuard.Backend.Controllers.SettingsController().SaveSettings(values));
        });
        admin.MapGet("/devices",(CourseGuardDbContext db)=>db.GetActiveDevices());
        admin.MapPost("/devices/{id:int}/status",(int id,DeviceStatus r,CourseGuardDbContext db)=>
            r.Status is "ACTIVE" or "BLOCKED"?Results.Ok(db.UpdateDeviceStatus(id,r.Status)):Results.BadRequest());
        admin.MapPost("/devices/{id:int}/delete",(int id,CourseGuardDbContext db)=>db.DeleteDevice(id));
    }
    private static int Id(ClaimsPrincipal p)=>int.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
    internal record DeviceStatus(string Status);
}
