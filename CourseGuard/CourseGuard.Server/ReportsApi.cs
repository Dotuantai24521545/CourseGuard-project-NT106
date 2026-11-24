using System.Data;
using CourseGuard.Backend.Data;
internal static class ReportsApi
{
    public static void MapReports(this RouteGroupBuilder api)
    {
        api.MapPost("/admin/reports",(ReportRequest request)=>
        {
            var reportType=request.Type; var start=request.Start;var end=request.End;
            if(end<start || end-start>TimeSpan.FromDays(3660)) return Results.BadRequest();
            string query = "";

            if (reportType == "Danh sách học viên")
            {
                query = @"
                    SELECT u.ID, u.USERNAME, u.FULL_NAME, u.EMAIL, u.CREATED_AT, u.STATUS
                    FROM USERS u
                    JOIN ROLES r ON u.ROLE_ID = r.ID
                    WHERE r.NAME = 'STUDENT'
                    AND u.CREATED_AT BETWEEN @start AND @end
                    ORDER BY u.CREATED_AT DESC";
            }
            else if (reportType == "Danh sách giảng viên")
            {
                query = @"
                    SELECT u.ID, u.USERNAME, u.FULL_NAME, u.EMAIL, u.CREATED_AT, u.STATUS
                    FROM USERS u
                    JOIN ROLES r ON u.ROLE_ID = r.ID
                    WHERE r.NAME = 'TEACHER'
                    AND u.CREATED_AT BETWEEN @start AND @end
                    ORDER BY u.CREATED_AT DESC";
            }
            else if (reportType == "Danh sách vi phạm")
            {
                query = @"
                    SELECT v.ID, u.USERNAME, u.FULL_NAME, e.TITLE AS EXAM_TITLE, v.TYPE AS VIOLATION_TYPE, v.CREATED_AT
                    FROM VIOLATIONS v
                    JOIN USERS u ON v.USER_ID = u.ID
                    LEFT JOIN EXAM_ATTEMPTS ea ON v.EXAM_ATTEMPT_ID = ea.ID
                    LEFT JOIN EXAMS e ON ea.EXAM_ID = e.ID
                    WHERE v.CREATED_AT BETWEEN @start AND @end
                    ORDER BY v.CREATED_AT DESC";
            }
            else if (reportType == "Danh sách đăng nhập")
            {
                query = @"
                    SELECT a.ID, COALESCE(u.USERNAME, 'SYSTEM') AS USERNAME, a.ACTION, a.DETAILS, a.IP_ADDRESS, a.CREATED_AT
                    FROM AUDIT_LOGS a
                    LEFT JOIN USERS u ON a.USER_ID = u.ID
                    WHERE a.ACTION = 'LOGIN'
                    AND a.CREATED_AT BETWEEN @start AND @end
                    ORDER BY a.CREATED_AT DESC";
            }
            else
            {
                // Fallback or empty
                return Results.BadRequest();
            }

            var parameters = new Dictionary<string, (SqlDbType, object)>
            {
                { "@start", (SqlDbType.DateTime, start) },
                { "@end", (SqlDbType.DateTime, end) }
            };


            var table=DatabaseAction.ExecuteQuery(query,parameters);
            var columns=table.Columns.Cast<DataColumn>().Select(c=>c.ColumnName).ToArray();
            var rows=table.Rows.Cast<DataRow>().Select(row=>columns.Select(c=>row[c]==DBNull.Value?null:Convert.ToString(row[c],System.Globalization.CultureInfo.InvariantCulture)).ToArray()).ToArray();
            return Results.Ok(new {columns,rows});
        }).RequireAuthorization(p=>p.RequireRole("ADMIN"));
    }
    internal record ReportRequest(string Type,DateTime Start,DateTime End);
}
