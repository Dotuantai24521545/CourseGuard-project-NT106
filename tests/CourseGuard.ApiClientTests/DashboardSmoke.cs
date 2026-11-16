using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;
using CourseGuard.Backend.Services;
using CourseGuard.Backend.Security;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Forms.Admin;
using CourseGuard.Frontend.Forms.Teacher;
using CourseGuard.Frontend.Forms.Student;

internal static class DashboardSmoke
{
    public static void Run(string cookieFile)
    {
        Exception? failure=null;
        var thread=new Thread(()=>
        {
            try
            {
                Environment.SetEnvironmentVariable("COURSEGUARD_API_URL","http://127.0.0.1:5087");
                Environment.SetEnvironmentVariable("COURSEGUARD_DB_CONNECTION","");
                Environment.SetEnvironmentVariable("SMTP_PASS","");
                var cookies=(CookieContainer)typeof(ServerApiClient).GetField("Cookies",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
                using var parsed=JsonDocument.Parse(File.ReadAllText(cookieFile));
                var items=parsed.RootElement.ValueKind==JsonValueKind.Array?parsed.RootElement.EnumerateArray().ToArray():new[]{parsed.RootElement};
                foreach(var c in items)cookies.Add(new Uri("http://127.0.0.1:5087"),new Cookie(c.GetProperty("Name").GetString()!,c.GetProperty("Value").GetString()!,c.GetProperty("Path").GetString()!));
                var user=ServerApiClient.GetAsync<UserModel>("api/me").GetAwaiter().GetResult();
                typeof(ServerApiClient).GetProperty("CurrentUser")!.SetValue(null,user);
                UserSessionContext.SetCurrentUser(user.Id,user.Role,user.Username,user.FullName,user.AvatarPath);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException+=(_,e)=>failure??=e.Exception;
                using Form dashboard=user.Role.ToUpperInvariant() switch
                {
                    "ADMIN"=>new AdminDashboard(user),
                    "TEACHER"=>new TeacherDashboard(user,false),
                    _=>new StudentDashboard(user,false)
                };
                dashboard.ShowInTaskbar=false;
                dashboard.StartPosition=FormStartPosition.Manual;
                dashboard.Location=new System.Drawing.Point(-10000,-10000);
                using var timer=new System.Windows.Forms.Timer {Interval=200};
                timer.Tick+=(_,_)=>
                {
                    foreach(var form in Application.OpenForms.Cast<Form>().Where(f=>f!=dashboard).ToArray())
                    {
                        failure??=new Exception("Unexpected dialog: "+form.Text+" "+Texts(form));
                        form.Close();
                    }
                };
                timer.Start();dashboard.Show();
                var until=DateTime.UtcNow.AddSeconds(7);
                while(DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(20);}
                dashboard.Close();Application.DoEvents();
                if(failure is null)Console.WriteLine("PASS live "+user.Role+" dashboard loads without database/SMTP credentials");
            }
            catch(Exception e){failure=e;}
        }) {IsBackground=true};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();
        if(!thread.Join(TimeSpan.FromSeconds(45)))throw new TimeoutException("Dashboard smoke did not finish");
        if(failure is not null)throw failure;
    }
    private static string Texts(Control control)=>string.Join(" ",control.Controls.Cast<Control>().Select(c=>c.Text+" "+Texts(c)));
}
