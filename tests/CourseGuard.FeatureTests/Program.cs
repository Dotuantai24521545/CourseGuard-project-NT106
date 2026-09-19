using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Config;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Security;
using CourseGuard.Backend.Services;
using CourseGuard.Backend.Services.Classroom;
using CourseGuard.Backend.Services.Monitoring;
using CourseGuard.Backend.Services.Realtime;
using CourseGuard.Frontend.Forms.Student;
using CourseGuard.Frontend.Forms.Teacher;
using CourseGuard.Frontend.Helpers;
using CourseGuard.Frontend.Theme;
using CourseGuard.Frontend.UserControls;
using CourseGuard.Frontend.UserControls.Shared.Chat;
using CourseGuard.Frontend.UserControls.Shared.CourseWorkspace;
using CourseGuard.Frontend.UserControls.Student;
using CourseGuard.Frontend.UserControls.Teacher;
using System.Data;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;
using EmitOpCode = System.Reflection.Emit.OpCode;
using EmitOpCodes = System.Reflection.Emit.OpCodes;
using EmitOperandType = System.Reflection.Emit.OperandType;
using static TestRunner;

if (args.Contains("--dev-exam-smoke", StringComparer.OrdinalIgnoreCase))
{
    string projectRef = args.FirstOrDefault(a => a.StartsWith("--project-ref=", StringComparison.Ordinal))?[14..] ?? "";
    Run("development database exam workflow", () => DevExamSmokeTests.RunAsync(projectRef));
    TestRunner.PrintSummary();
    return;
}

if (args.Contains("--initialize-empty-database", StringComparer.OrdinalIgnoreCase))
{
    string projectRef = args.FirstOrDefault(a => a.StartsWith("--project-ref=", StringComparison.Ordinal))?[14..] ?? "";
    Run("initialize empty development database", () => DevDatabaseSetup.InitializeAsync(projectRef));
    TestRunner.PrintSummary();
    return;
}

if (args.Contains("--integration", StringComparer.OrdinalIgnoreCase))
{
    LiveIntegrationTests.Run(args.Contains("--database-only", StringComparer.OrdinalIgnoreCase));
    return;
}

if (args.Contains("--offline", StringComparer.OrdinalIgnoreCase))
{
    TestRunner.Offline = true;
    // Do not load developer credentials during offline UI and logic tests.
    typeof(AppEnvironment).GetField("_dotEnvLoaded", BindingFlags.Static | BindingFlags.NonPublic)!
        .SetValue(null, true);
    Environment.SetEnvironmentVariable("COURSEGUARD_DB_CONNECTION",
        "Host=127.0.0.1;Port=1;Username=test;Password=test;Database=test;Timeout=1;Pooling=false");
}

AiServiceTests.Run();

Run("app environment required values load .env on demand", () =>
{
    string envPath = Path.Combine(AppContext.BaseDirectory, ".env");
    AssertTrue(File.Exists(envPath), "feature test output must include .env");

    string? originalCourseGuard = Environment.GetEnvironmentVariable("COURSEGUARD_DB_CONNECTION");
    string? originalSupabase = Environment.GetEnvironmentVariable("SUPABASE_DB_CONNECTION");
    string? originalConnection = Environment.GetEnvironmentVariable("CONNECTION_STRING");
    try
    {
        Environment.SetEnvironmentVariable("COURSEGUARD_DB_CONNECTION", null);
        Environment.SetEnvironmentVariable("SUPABASE_DB_CONNECTION", null);
        Environment.SetEnvironmentVariable("CONNECTION_STRING", null);
        ResetAppEnvironmentDotEnvLoadedForTests();

        string connectionString = AppEnvironment.GetRequired(
            "COURSEGUARD_DB_CONNECTION",
            "SUPABASE_DB_CONNECTION",
            "CONNECTION_STRING");

        AssertFalse(string.IsNullOrWhiteSpace(connectionString),
            "GetRequired should load .env before reading required keys");
    }
    finally
    {
        Environment.SetEnvironmentVariable("COURSEGUARD_DB_CONNECTION", originalCourseGuard);
        Environment.SetEnvironmentVariable("SUPABASE_DB_CONNECTION", originalSupabase);
        Environment.SetEnvironmentVariable("CONNECTION_STRING", originalConnection);
        ResetAppEnvironmentDotEnvLoadedForTests();
        AppEnvironment.LoadDotEnvIfExists();
    }
});

Run("student exam scoring sums matching answers", () =>
{
    var questions = new[]
    {
        new StudentExamScoringQuestion(1, "A", 2.5m),
        new StudentExamScoringQuestion(2, "C", 2.5m),
        new StudentExamScoringQuestion(3, "D", 2.5m),
        new StudentExamScoringQuestion(4, "B", 2.5m)
    };
    var answers = new Dictionary<int, string>
    {
        [1] = "a",
        [2] = "B",
        [3] = "D"
    };

    StudentExamScoreResult result = StudentExamScoringService.Calculate(questions, answers);

    AssertEqual(2, result.CorrectCount);
    AssertEqual(4, result.TotalQuestions);
    AssertEqual(5.0, result.Score);
});

Run("ai services expose essay grading virtual tutor and flashcard generation", () =>
{
    var service = new AiQuestionGeneratorService();

    var essayTask = service.GradeEssayAsync("Giải thích TCP/IP", "Mô hình 4 tầng...", "Bài làm của sinh viên...", 10m);
    var tutorTask = service.AskVirtualTutorAsync("Lập trình mạng", "Khái niệm Socket...", "Socket là gì?");
    var flashcardsTask = service.GenerateFlashcardsAsync("Khái niệm TCP Handshake...", 3);

    Task.WaitAll(essayTask, tutorTask, flashcardsTask);
    AssertTrue(essayTask.Result != null && essayTask.Result.SuggestedScore >= 0m, "essay grading must return valid score");
    AssertFalse(string.IsNullOrWhiteSpace(tutorTask.Result), "virtual tutor must return non-empty answer");
    AssertTrue(flashcardsTask.Result.Count > 0, "flashcard generation must return provider cards");
});

Run("calendar export service generates valid ics string and alarms", () =>
{
    var events = new[]
    {
        new CalendarEventItem
        {
            Summary = "Thi giữa kỳ Mạng máy tính",
            Description = "Phòng 302 - Giám sát tự động",
            Location = "Tòa A",
            StartTime = new DateTime(2026, 7, 1, 8, 0, 0),
            EndTime = new DateTime(2026, 7, 1, 9, 30, 0)
        }
    };

    string ics = CalendarExportService.ExportToIcsString(events);
    AssertTrue(ics.Contains("BEGIN:VCALENDAR"), "ics output must start with VCALENDAR header");
    AssertTrue(ics.Contains("SUMMARY:Thi giữa kỳ Mạng máy tính"), "ics must contain escaped summary");
    AssertTrue(ics.Contains("TRIGGER:-PT30M"), "ics must contain 30-minute advance alarm");
});

Run("gamification and schedule conflict service calculates badges and overlaps", () =>
{
    var service = new GamificationAndScheduleService();
    var badges = service.GetStudentBadges(95, 9.0m);

    AssertTrue(badges.Exists(b => b.Title == "Chăm chỉ"), "student with 95% attendance gets Cham chi badge");
    AssertTrue(badges.Exists(b => b.Title == "Thủ khoa"), "student with 9.0 avg score gets Thu khoa badge");

    var conflictTask = service.HasExamScheduleConflictAsync(-1, new DateTime(2026, 7, 1, 8, 0, 0), new DateTime(2026, 7, 1, 10, 0, 0), -1);
    conflictTask.Wait();
    AssertFalse(conflictTask.Result, "nonexistent test student must not have a schedule conflict");
});

Run("teacher exam scoring sums only correct selected options", () =>
{
    var questions = new[]
    {
        new TeacherExamQuestionModel { Id = 10, CorrectOption = "A", Points = 2.5m },
        new TeacherExamQuestionModel { Id = 20, CorrectOption = "B", Points = 2.5m },
        new TeacherExamQuestionModel { Id = 30, CorrectOption = "D", Points = 5m }
    };
    var selected = new Dictionary<int, string>
    {
        [10] = "a",
        [20] = "C",
        [30] = "D"
    };

    decimal score = ExamScoringService.CalculateScore(questions, selected);

    AssertEqual(7.5m, score);
});

Run("teacher grid pages accept shared teacher controller", () =>
{
    Type[] pageTypes =
    {
        typeof(UC_TeacherCourses),
        typeof(UC_TeacherLessons),
        typeof(UC_TeacherAssignments),
        typeof(UC_TeacherExams),
        typeof(UC_ExamMonitor),
        typeof(UC_TeacherResults),
        typeof(UC_TeacherStudents),
        typeof(UC_TeacherMaterials),
        typeof(UC_TeacherSchedule)
    };
    Type[] parameterTypes = { typeof(int), typeof(TeacherController) };

    foreach (Type pageType in pageTypes)
    {
        AssertTrue(
            pageType.GetConstructor(parameterTypes) != null,
            $"{pageType.Name} must expose public constructor (int, TeacherController)");
    }
});

Run("teacher course detail pages keep legacy constructors and expose course context constructors", () =>
{
    Type[] courseScopedPageTypes =
    {
        typeof(UC_TeacherLessons),
        typeof(UC_TeacherAssignments),
        typeof(UC_TeacherExams),
        typeof(UC_TeacherResults),
        typeof(UC_TeacherStudents),
        typeof(UC_TeacherMaterials),
        typeof(UC_TeacherSchedule),
        typeof(UC_ExamMonitor)
    };

    Type[] legacyParameters = { typeof(int), typeof(TeacherController) };
    Type[] courseContextParameters = { typeof(int), typeof(TeacherController), typeof(int?) };

    foreach (Type pageType in courseScopedPageTypes)
    {
        AssertTrue(
            pageType.GetConstructor(legacyParameters) != null,
            $"{pageType.Name} must keep public constructor (int, TeacherController)");
        AssertTrue(
            pageType.GetConstructor(courseContextParameters) != null,
            $"{pageType.Name} must expose public constructor (int, TeacherController, int?) for course workspace routes");
    }
});

Run("course workspace contracts expose role-aware selected course context", () =>
{
    var selected = new CourseSelectorItem(
        42,
        "Network Programming",
        "NT106 - Semester 4",
        "Active",
        CourseWorkspaceRole.Teacher);

    var context = new CourseWorkspaceContext(
        7,
        selected.CourseId,
        selected.Name,
        selected.SecondaryText,
        selected.Status,
        selected.Role);

    AssertEqual(7, context.UserId);
    AssertEqual(selected.CourseId, context.CourseId);
    AssertEqual(selected.Name, context.CourseName);
    AssertEqual(selected.SecondaryText, context.SecondaryText);
    AssertEqual(selected.Status, context.Status);
    AssertEqual(CourseWorkspaceRole.Teacher, context.Role);
});

Run("course chat panel exposes course-scoped load method", () =>
{
    Type panelType = typeof(CourseChatPanel);

    ConstructorInfo? constructor = panelType.GetConstructor(new[] { typeof(int), typeof(ChatController), typeof(bool) });
    MethodInfo? loadMethod = panelType.GetMethod(
        nameof(CourseChatPanel.LoadCourseAsync),
        new[] { typeof(int), typeof(string), typeof(CancellationToken) });
    EventInfo? markedReadEvent = panelType.GetEvent(nameof(CourseChatPanel.MessagesMarkedRead));

    AssertTrue(constructor != null, "CourseChatPanel must expose public constructor (int, ChatController, bool)");
    AssertTrue(loadMethod != null, "CourseChatPanel must expose LoadCourseAsync(int, string, CancellationToken)");
    AssertEqual(typeof(Task), loadMethod!.ReturnType);
    AssertTrue(markedReadEvent != null, "CourseChatPanel must expose MessagesMarkedRead event");
});

Run("student course tool factory returns approved tool ids in order", () =>
{
    IReadOnlyList<CourseToolDefinition> tools = StudentCourseToolFactory.Create((_, _) => { });

    AssertEqual(
        "materials,schedule,lessons,assignments,exams,results,notifications",
        string.Join(",", tools.Select(tool => tool.Id)));
    AssertEqual(
        "Tài liệu,Lịch học,Bài học,Bài tập,Bài kiểm tra,Kết quả,Thông báo",
        string.Join(",", tools.Select(tool => tool.Label)));
});

Run("student course tool factory does not include chat as a dock tool", () =>
{
    IReadOnlyList<CourseToolDefinition> tools = StudentCourseToolFactory.Create((_, _) => { });

    AssertFalse(
        tools.Any(tool => string.Equals(tool.Id, "chat", StringComparison.OrdinalIgnoreCase)),
        "student course dock tools must not include chat");
});

Run("student course tool factory open detail uses selected tool id and context", () =>
{
    string? openedToolId = null;
    CourseWorkspaceContext? openedContext = null;
    var context = new CourseWorkspaceContext(
        7,
        42,
        "Network Programming",
        "NT106.01",
        "Active",
        CourseWorkspaceRole.Student);

    IReadOnlyList<CourseToolDefinition> tools = StudentCourseToolFactory.Create((toolId, selectedContext) =>
    {
        openedToolId = toolId;
        openedContext = selectedContext;
    });
    CourseToolDefinition assignments = tools.Single(tool => tool.Id == "assignments");

    assignments.OpenDetail(context);

    AssertEqual("assignments", openedToolId);
    AssertTrue(ReferenceEquals(context, openedContext), "OpenDetail must receive the exact selected course context instance");
});

Run("student course tool factory preview stores rows in metadata without duplicate child labels", () =>
{
    var context = new CourseWorkspaceContext(
        7,
        42,
        "Network Programming",
        "NT106.01",
        "Active",
        CourseWorkspaceRole.Student);
    string[] expectedRows =
    {
        $"Kh\u00f3a h\u1ecdc: {context.CourseName}",
        $"M\u00e3/L\u1edbp: {context.SecondaryText}",
        $"Tr\u1ea1ng th\u00e1i: {context.Status}"
    };

    CourseToolDefinition tool = StudentCourseToolFactory.Create((_, _) => { }).Single(tool => tool.Id == "materials");
    using Control previewControl = tool.CreatePreview(context);

    CourseToolPreview preview = previewControl.Tag as CourseToolPreview
        ?? throw new InvalidOperationException("Student tool preview control Tag must be CourseToolPreview metadata");
    AssertEqual("T\u00e0i li\u1ec7u", preview.Title);
    AssertEqual("Xem t\u00e0i li\u1ec7u c\u1ee7a kh\u00f3a h\u1ecdc", preview.Summary);
    AssertEqual("V\u00e0o trang T\u00e0i li\u1ec7u", preview.PrimaryActionLabel);
    AssertEqual(string.Join("|", expectedRows), string.Join("|", preview.Rows));

    string[] childTexts = GetAllControls(previewControl)
        .Select(control => control.Text)
        .Where(text => !string.IsNullOrWhiteSpace(text))
        .Select(text => text.Trim())
        .ToArray();
    AssertFalse(
        childTexts.Any(text => expectedRows.Contains(text)),
        "Student preview child controls must not duplicate CourseToolPreview rows rendered by CourseToolDrawer");
});

Run("teacher course tool factory returns approved tool ids in order", () =>
{
    IReadOnlyList<CourseToolDefinition> tools = TeacherCourseToolFactory.Create((_, _) => { });

    AssertEqual(
        "lessons,materials,assignments,exams,results,students,schedule,notifications",
        string.Join(",", tools.Select(tool => tool.Id)));
    AssertEqual(
        string.Join(",", new[]
        {
            "B\u00e0i h\u1ecdc",
            "T\u00e0i li\u1ec7u",
            "B\u00e0i t\u1eadp",
            "B\u00e0i ki\u1ec3m tra",
            "K\u1ebft qu\u1ea3",
            "Sinh vi\u00ean",
            "L\u1ecbch d\u1ea1y",
            "Th\u00f4ng b\u00e1o"
        }),
        string.Join(",", tools.Select(tool => tool.Label)));
});

Run("teacher course tool factory does not include chat as a dock tool", () =>
{
    IReadOnlyList<CourseToolDefinition> tools = TeacherCourseToolFactory.Create((_, _) => { });

    AssertFalse(
        tools.Any(tool => string.Equals(tool.Id, "chat", StringComparison.OrdinalIgnoreCase)),
        "teacher course dock tools must not include chat");
});

Run("teacher course tool factory open detail uses selected tool id and context", () =>
{
    string? openedToolId = null;
    CourseWorkspaceContext? openedContext = null;
    var context = new CourseWorkspaceContext(
        11,
        42,
        "Network Programming",
        "NT106.01",
        "Active",
        CourseWorkspaceRole.Teacher);

    IReadOnlyList<CourseToolDefinition> tools = TeacherCourseToolFactory.Create((toolId, selectedContext) =>
    {
        openedToolId = toolId;
        openedContext = selectedContext;
    });
    CourseToolDefinition exams = tools.Single(tool => tool.Id == "exams");

    exams.OpenDetail(context);

    AssertEqual("exams", openedToolId);
    AssertTrue(ReferenceEquals(context, openedContext), "OpenDetail must receive the exact selected course context instance");
});

Run("teacher course tool factory preview stores rows in metadata without duplicate child labels", () =>
{
    var context = new CourseWorkspaceContext(
        11,
        42,
        "Network Programming",
        "NT106.01",
        "Active",
        CourseWorkspaceRole.Teacher);
    string[] expectedRows =
    {
        $"Kh\u00f3a d\u1ea1y: {context.CourseName}",
        $"M\u00e3/L\u1edbp: {context.SecondaryText}",
        $"Tr\u1ea1ng th\u00e1i: {context.Status}"
    };

    CourseToolDefinition tool = TeacherCourseToolFactory.Create((_, _) => { }).Single(tool => tool.Id == "lessons");
    using Control previewControl = tool.CreatePreview(context);

    CourseToolPreview preview = previewControl.Tag as CourseToolPreview
        ?? throw new InvalidOperationException("Teacher tool preview control Tag must be CourseToolPreview metadata");
    AssertEqual("B\u00e0i h\u1ecdc", preview.Title);
    AssertEqual("Qu\u1ea3n l\u00fd b\u00e0i h\u1ecdc c\u1ee7a kh\u00f3a d\u1ea1y", preview.Summary);
    AssertEqual("V\u00e0o trang B\u00e0i h\u1ecdc", preview.PrimaryActionLabel);
    AssertEqual(string.Join("|", expectedRows), string.Join("|", preview.Rows));

    string[] childTexts = GetAllControls(previewControl)
        .Select(control => control.Text)
        .Where(text => !string.IsNullOrWhiteSpace(text))
        .Select(text => text.Trim())
        .ToArray();
    AssertFalse(
        childTexts.Any(text => expectedRows.Contains(text)),
        "Teacher preview child controls must not duplicate CourseToolPreview rows rendered by CourseToolDrawer");
});

Run("student course tool previews use real course scoped data sources", () =>
{
    string factorySource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Student", "StudentCourseToolFactory.cs"));
    string dashboardSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "StudentDashboard.cs"));

    AssertTrue(factorySource.Contains("int studentId")
        && factorySource.Contains("CourseController courseController")
        && factorySource.Contains("CourseGuardDbContext dbContext"),
        "student course tool factory must accept real student/course data dependencies");
    AssertTrue(factorySource.Contains("BuildStudentPreviewRows"),
        "student course tool factory must centralize role-aware preview rows");
    AssertTrue(factorySource.Contains("GetStudentLessonsAsync(studentId)"),
        "student lessons/materials previews must come from the real student lesson/material query");
    AssertTrue(factorySource.Contains("GetStudentAssignmentsAsync(studentId)"),
        "student assignment previews must come from the real student assignment query");
    AssertTrue(factorySource.Contains("GetAvailableExamsForStudent(studentId)"),
        "student exam previews must come from the real available-exam query");
    AssertTrue(factorySource.Contains("GetStudentResultItems(studentId, context.CourseId"),
        "student result previews must be scoped to the selected course");
    AssertTrue(factorySource.Contains("GetStudentOnlineSessions(studentId)"),
        "student schedule previews must come from the real schedule query");
    AssertTrue(factorySource.Contains("LoadByUserId(studentId)"),
        "student notification previews must come from the real notification repository");
    AssertTrue(factorySource.Contains("row.CourseId == context.CourseId"),
        "student preview rows must filter real rows by the selected course id");
    AssertTrue(dashboardSource.Contains("StudentCourseToolFactory.Create(OpenStudentCourseToolDetail, currentUser.Id, _courseController, _dbContext"),
        "student dashboard must pass its real dependencies into course tool previews");
});

Run("teacher course tool previews use real course scoped data sources", () =>
{
    string factorySource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "TeacherCourseToolFactory.cs"));
    string dashboardSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherDashboard.cs"));

    AssertTrue(factorySource.Contains("int teacherId")
        && factorySource.Contains("TeacherController teacherController"),
        "teacher course tool factory must accept the real teacher controller dependency");
    AssertTrue(factorySource.Contains("BuildTeacherPreviewRows"),
        "teacher course tool factory must centralize role-aware preview rows");
    AssertTrue(factorySource.Contains("GetLessons(teacherId)"),
        "teacher lesson previews must come from the real teacher lesson query");
    AssertTrue(factorySource.Contains("GetMaterials(teacherId, context.CourseId)"),
        "teacher material previews must be scoped through the real material query");
    AssertTrue(factorySource.Contains("GetAssignments(teacherId)"),
        "teacher assignment previews must come from the real assignment query");
    AssertTrue(factorySource.Contains("GetExams(teacherId)"),
        "teacher exam previews must come from the real exam query");
    AssertTrue(factorySource.Contains("GetResults(teacherId, context.CourseId"),
        "teacher result previews must be scoped to the selected course");
    AssertTrue(factorySource.Contains("GetEnrolledStudents(teacherId, context.CourseId)"),
        "teacher student previews must use the real enrolled-student query");
    AssertTrue(factorySource.Contains("GetSchedule(teacherId)"),
        "teacher schedule previews must come from the real schedule query");
    AssertTrue(factorySource.Contains("row.CourseId == context.CourseId"),
        "teacher preview rows must filter real rows by the selected course id");
    AssertTrue(dashboardSource.Contains("TeacherCourseToolFactory.Create(OpenTeacherCourseToolDetail, _teacherId, _teacherController"),
        "teacher dashboard must pass its real dependencies into course tool previews");
});

Run("course workspace control exposes load update and open tool methods", () =>
{
    Type workspaceType = typeof(CourseWorkspaceControl);

    ConstructorInfo? constructor = workspaceType.GetConstructor(new[]
    {
        typeof(CourseWorkspaceContext),
        typeof(ChatController),
        typeof(IReadOnlyList<CourseToolDefinition>),
        typeof(bool)
    });
    MethodInfo? loadMethod = workspaceType.GetMethod(nameof(CourseWorkspaceControl.LoadAsync), new[] { typeof(CancellationToken) });
    MethodInfo? updateMethod = workspaceType.GetMethod(
        nameof(CourseWorkspaceControl.UpdateContext),
        new[] { typeof(CourseWorkspaceContext), typeof(IReadOnlyList<CourseToolDefinition>) });
    MethodInfo? openToolMethod = workspaceType.GetMethod(nameof(CourseWorkspaceControl.OpenTool), new[] { typeof(string) });

    AssertTrue(constructor != null, "CourseWorkspaceControl must expose the course workspace constructor");
    AssertTrue(loadMethod != null, "CourseWorkspaceControl must expose LoadAsync(CancellationToken)");
    AssertEqual(typeof(Task), loadMethod!.ReturnType);
    AssertTrue(updateMethod != null, "CourseWorkspaceControl must expose UpdateContext(CourseWorkspaceContext, IReadOnlyList<CourseToolDefinition>)");
    AssertTrue(openToolMethod != null, "CourseWorkspaceControl must expose OpenTool(string)");

    Exception? failure = null;
    bool previousStartRealtime = CourseChatPanel.StartRealtimeForTesting;
    try
    {
        CourseChatPanel.StartRealtimeForTesting = false;
        var thread = new Thread(() =>
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

                var initialContext = new CourseWorkspaceContext(
                    0,
                    0,
                    "Network Programming",
                    "NT106.01",
                    "Active",
                    CourseWorkspaceRole.Student);
                var updatedContext = initialContext with
                {
                    CourseId = 42,
                    CourseName = "Distributed Systems",
                    SecondaryText = "DS301"
                };
                int previewCreateCount = 0;
                int openCount = 0;
                CourseToolDefinition tool = NewCourseTool(
                    "lessons",
                    "Lessons",
                    "View current lessons",
                    () => previewCreateCount++,
                    () => openCount++);

                using var workspace = new CourseWorkspaceControl(
                    initialContext,
                    (ChatController)RuntimeHelpers.GetUninitializedObject(typeof(ChatController)),
                    new[] { tool },
                    allowPollCreation: false);

                WaitForTaskWithMessagePump(workspace.LoadAsync());
                workspace.UpdateContext(updatedContext, new[] { tool });
                workspace.OpenTool("lessons");

                Label courseTitle = GetAllControls(workspace).OfType<Label>().First(label => label.Text == "Distributed Systems");
                AssertEqual("Distributed Systems", courseTitle.Text);
                AssertEqual(0, previewCreateCount);
                AssertEqual(1, openCount);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
    finally
    {
        CourseChatPanel.StartRealtimeForTesting = previousStartRealtime;
    }

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
});

Run("course workspace theme pass preserves white embedded course surfaces", () =>
{
    Exception? failure = null;
    bool previousDarkMode = AppColors.IsDarkMode;
    bool previousStartRealtime = CourseChatPanel.StartRealtimeForTesting;
    try
    {
        AppColors.IsDarkMode = false;
        CourseChatPanel.StartRealtimeForTesting = false;

        var thread = new Thread(() =>
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

                var context = new CourseWorkspaceContext(
                    7,
                    42,
                    "Distributed Systems",
                    "2 sinh vien",
                    "Active",
                    CourseWorkspaceRole.Teacher);
                CourseToolDefinition[] tools =
                {
                    NewCourseTool("lessons", "Lessons", "View lessons"),
                    NewCourseTool("materials", "Materials", "View materials"),
                    NewCourseTool("assignments", "Assignments", "View assignments"),
                    NewCourseTool("exams", "Exams", "View exams"),
                    NewCourseTool("results", "Results", "View results"),
                    NewCourseTool("students", "Students", "View students"),
                    NewCourseTool("schedule", "Schedule", "View schedule"),
                    NewCourseTool("notifications", "Notifications", "View notifications")
                };

                using var host = new Form
                {
                    ClientSize = new Size(1360, 720),
                    StartPosition = FormStartPosition.Manual,
                    ShowInTaskbar = false
                };
                using var workspace = new CourseWorkspaceControl(
                    context,
                    (ChatController)RuntimeHelpers.GetUninitializedObject(typeof(ChatController)),
                    tools,
                    allowPollCreation: true);

                host.Controls.Add(workspace);
                workspace.Dock = DockStyle.Fill;
                host.CreateControl();
                host.PerformLayout();
                workspace.PerformLayout();
                AppColors.ApplyTheme(workspace);
                host.PerformLayout();
                workspace.PerformLayout();
                Application.DoEvents();

                var chatPanel = GetAllControls(workspace).OfType<CourseChatPanel>().Single();
                var messageList = GetAllControls(workspace).OfType<ChatMessageListControl>().Single();
                var actionDock = GetAllControls(workspace).OfType<CourseActionDock>().Single();
                var actionGrid = GetAllControls(actionDock).OfType<FlowLayoutPanel>().Single();
                Color actionGridVisibleBackColor = actionGrid.BackColor == Color.Transparent
                    ? actionGrid.Parent?.BackColor ?? Color.Empty
                    : actionGrid.BackColor;

                AssertTrue(messageList.BackColor.ToArgb() == Color.White.ToArgb(),
                    "message list must remain white after the dashboard theme pass");
                AssertTrue(actionDock.BackColor.ToArgb() == Color.White.ToArgb(),
                    "course action dock must remain white after the dashboard theme pass");
                AssertTrue(actionGridVisibleBackColor.ToArgb() == Color.White.ToArgb(),
                    "course action grid must visibly remain white after the dashboard theme pass");

                Button[] composerButtons = GetAllControls(chatPanel)
                    .OfType<Button>()
                    .Where(button => button.Parent is TableLayoutPanel)
                    .ToArray();
                AssertTrue(composerButtons.Length >= 2 && composerButtons.Any(button => button.Text == "+"),
                    "teacher chat composer must expose a single attachment menu button and send button");
                foreach (Button button in composerButtons)
                {
                    AssertTrue(button.Height >= 44, "composer buttons must keep a usable non-clipped height");
                    AssertTrue(button.Bottom <= button.Parent!.ClientSize.Height,
                        $"composer button chrome must stay inside the composer row: {button.Text} bottom={button.Bottom}, rowHeight={button.Parent.ClientSize.Height}");
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
    finally
    {
        CourseChatPanel.StartRealtimeForTesting = previousStartRealtime;
        AppColors.IsDarkMode = previousDarkMode;
    }

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
});

Run("course workspace uses module panels instead of loose controls", () =>
{
    string workspaceSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "CourseWorkspace", "CourseWorkspaceControl.cs"));
    string actionDockSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "CourseWorkspace", "CourseActionDock.cs"));
    string chatPanelSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "CourseWorkspace", "CourseChatPanel.cs"));

    AssertTrue(workspaceSource.Contains("BuildCourseInfoCard"),
        "course workspace must render a course info card in the right content panel");
    AssertTrue(workspaceSource.Contains("BuildActionGridCard"),
        "course workspace must wrap role tools in an action grid card");
    AssertTrue(workspaceSource.Contains("BuildUpcomingCard"),
        "course workspace must reserve an upcoming/summary card like the course-centric prototype");
    AssertFalse(workspaceSource.Contains("Lab 02", StringComparison.OrdinalIgnoreCase),
        "course workspace must not render hardcoded fake upcoming items");
    AssertFalse(workspaceSource.Contains("Subnet Calculator", StringComparison.OrdinalIgnoreCase),
        "course workspace must not render hardcoded fake upcoming items");
    AssertFalse(workspaceSource.Contains("Ki\u1ec3m tra ch\u01b0\u01a1ng", StringComparison.OrdinalIgnoreCase),
        "course workspace must not render hardcoded fake upcoming items");
    AssertTrue(workspaceSource.Contains("new ColumnStyle(SizeType.Percent, 36f)"),
        "course workspace must use the prototype split with chat on the left and course actions on the right");
    AssertTrue(actionDockSource.Contains("RoundedToolCard"),
        "course action dock must render rounded action cards instead of raw rectangular buttons");
    AssertTrue(actionDockSource.Contains("FlowDirection.LeftToRight"),
        "course action dock must render tool cards as a grid, not a vertical dock list");
    AssertTrue(chatPanelSource.Contains("ShowEmbeddedHeader"),
        "chat panel must support hiding its internal title when wrapped by the workspace module panel");
});

Run("course workspace chrome keeps redesigned rounded surfaces", () =>
{
    string workspaceSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "CourseWorkspace", "CourseWorkspaceControl.cs"));
    string actionDockSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "CourseWorkspace", "CourseActionDock.cs"));
    string chatPanelSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "CourseWorkspace", "CourseChatPanel.cs"));
    string sidebarSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "CourseWorkspace", "CourseFirstSidebarControl.cs"));

    AssertTrue(sidebarSource.Contains("BuildBrandHeader"),
        "course sidebar must render the CourseGuard logo/header from the HTML prototype");
    AssertTrue(sidebarSource.Contains("CourseSelectorPlaceholder = \"Ch\u1ecdn kh\u00f3a h\u1ecdc...\""),
        "course sidebar selector must use the prototype placeholder text");
    AssertTrue(sidebarSource.Contains("CourseDropdownPanel"),
        "course sidebar must use a custom white dropdown panel with course badge rows");
    AssertFalse(sidebarSource.Contains("role-row", StringComparison.OrdinalIgnoreCase),
        "real Student/Teacher dashboards must not include the prototype role switch");
    AssertTrue(sidebarSource.Contains("RightRoundedSidebarPath"),
        "course sidebar must restore the whole-panel right rounded silhouette");
    AssertTrue(sidebarSource.Contains("ClearCourseSelectorFocus"),
        "course sidebar must clear sticky combo focus when users click blank sidebar space");
    AssertTrue(workspaceSource.Contains("BuildStatusBadge"),
        "course workspace must render role/status inside the course info card");
    AssertTrue(workspaceSource.Contains("AppFonts.Semibold(18f)"),
        "course title must use a larger semibold font so it does not look clipped or weak");
    AssertTrue(chatPanelSource.Contains("_composerInputShell"),
        "course chat composer must host the TextBox inside a rounded input shell");
    AssertTrue(chatPanelSource.Contains("RoundedButtonHelper.Apply(12, _send, _attachMenu)")
        && chatPanelSource.Contains("ContextMenuStrip _attachStrip"),
        "course chat composer buttons must use the shared rounded button helper and a single attachment menu");
    AssertFalse(chatPanelSource.Contains("🖼") || chatPanelSource.Contains("📎") || chatPanelSource.Contains("📊"),
        "attachment menu text must avoid emoji glyphs that render as tofu boxes in WinForms ToolStrip");
    AssertTrue(actionDockSource.Contains("CourseToolIcon"),
        "course action cards must render friendly icon chips instead of raw technical tags");
    AssertFalse(actionDockSource.Contains("[{tool.IconKey.Trim().ToUpperInvariant()}]"),
        "course action cards must not show raw [MATERIALS] style technical labels");
});

Run("course workspace chat and action grid keep prototype sizing", () =>
{
    string actionDockSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "CourseWorkspace", "CourseActionDock.cs"));
    string chatPanelSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "CourseWorkspace", "CourseChatPanel.cs"));
    string workspaceSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "CourseWorkspace", "CourseWorkspaceControl.cs"));
    string messageListSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "Chat", "ChatMessageListControl.cs"));

    AssertTrue(chatPanelSource.Contains("private readonly RoundedPanel _messageListShell"),
        "course chat message area must sit inside its own rounded shell instead of a raw gray rectangle");
    AssertTrue(chatPanelSource.Contains("CreateMessageListShell"),
        "course chat panel must build a rounded message-list shell");
    AssertTrue(chatPanelSource.Contains("new RowStyle(SizeType.Absolute, 112f)"),
        "course chat composer row must be tall enough that rounded input/buttons are not clipped");
    AssertTrue(chatPanelSource.Contains("private const int ComposerHostHeight")
        && chatPanelSource.Contains("Dock = DockStyle.Bottom")
        && chatPanelSource.Contains("MinimumSize = new Size(0, ComposerMinimumHeight)")
        && chatPanelSource.Contains("Padding = new Padding(0, 0, 0, 8)"),
        "course chat composer must sit in a fixed bottom-docked host with bottom padding so buttons are not clipped");
    AssertFalse(chatPanelSource.Contains("AutoScroll = true"),
        "course chat panel must not use AutoScroll parents around the bottom-docked composer");
    AssertTrue(chatPanelSource.Contains("Margin = new Padding(0, 12, 0, 10)") && chatPanelSource.Contains("_send.Margin = new Padding(0, 8, 0, 10)"),
        "course chat composer controls must reserve bottom breathing room so rounded chrome is fully visible");
    AssertTrue(chatPanelSource.Contains("new ColumnStyle(SizeType.Absolute, 74f)")
        && chatPanelSource.Contains("new ColumnStyle(SizeType.Absolute, 96f)"),
        "course chat composer action buttons must keep enough width to look balanced with their height");
    AssertTrue(chatPanelSource.Contains("CourseSurfaceColor") && chatPanelSource.Contains("_messageListShell.FillColor = CourseSurfaceColor"),
        "course chat message shell must use the same white light-mode fill as its parent card");
    AssertTrue(chatPanelSource.Contains("_messageList.BackColor = CourseSurfaceColor"),
        "course chat message list must not reintroduce a separate gray panel background");
    AssertTrue(messageListSource.Contains("Chưa có tin nhắn nào. Hãy bắt đầu cuộc trò chuyện!"),
        "course chat message list must show a quiet empty state instead of a blank gray block");
    AssertTrue(chatPanelSource.Contains("Padding = new Padding(10)") && chatPanelSource.Contains("_messageList.Margin = new Padding(2)"),
        "course chat message list must be inset inside the rounded shell so square child corners do not cover rounded chrome");
    int titledCardIndex = workspaceSource.IndexOf("private static RoundedPanel BuildTitledCard", StringComparison.Ordinal);
    int titledLayoutIndex = workspaceSource.IndexOf("var layout = new TableLayoutPanel", titledCardIndex, StringComparison.Ordinal);
    int titledLayoutStyleIndex = workspaceSource.IndexOf("layout.RowStyles.Add", titledLayoutIndex, StringComparison.Ordinal);
    int titledLayoutBackColorIndex = workspaceSource.IndexOf("BackColor = CourseSurfaceColor", titledLayoutIndex, StringComparison.Ordinal);
    AssertTrue(titledLayoutBackColorIndex >= titledLayoutIndex && titledLayoutBackColorIndex < titledLayoutStyleIndex,
        "course workspace titled-card layout must paint the same fill as the rounded card instead of exposing the page background");
    AssertTrue(actionDockSource.Contains("ToolGridColumns = 4"),
        "course action dock must use a four-column grid so teacher tools form two balanced rows");
    int toolRowsIndex = actionDockSource.IndexOf("_toolRows = new FlowLayoutPanel", StringComparison.Ordinal);
    int controlsAddIndex = actionDockSource.IndexOf("Controls.Add(_toolRows)", toolRowsIndex, StringComparison.Ordinal);
    int toolRowsBackColorIndex = actionDockSource.IndexOf("BackColor = CourseSurfaceColor", toolRowsIndex, StringComparison.Ordinal);
    AssertTrue(actionDockSource.Contains("CourseSurfaceColor") && toolRowsBackColorIndex >= toolRowsIndex && toolRowsBackColorIndex < controlsAddIndex,
        "course action dock and grid host must use the same fill as the parent card");
    AssertTrue(workspaceSource.Contains("private static Color CourseSurfaceColor") && workspaceSource.Contains("Color.White"),
        "course workspace card surfaces must use a true white fill in light mode");
    AssertTrue(workspaceSource.Contains("RowStyle(SizeType.AutoSize)") && workspaceSource.Contains("UpdateActionGridCardHeight"),
        "course workspace action card must size to its grid content instead of filling all vertical whitespace");
    AssertTrue(workspaceSource.Contains("CourseStatsInlineControl") && workspaceSource.Contains("AppColors.AccentBlue"),
        "course workspace stats must render numeric values with distinct accent text");
    AssertTrue(actionDockSource.Contains("UpdateToolCardLayout"),
        "course action dock must recalculate card widths from the available grid width");
    AssertTrue(actionDockSource.Contains("ToolCardGap"),
        "course action dock must reserve visible spacing between action cards");
    AssertFalse(actionDockSource.Contains("ToolCardWidth = 126"),
        "course action dock must not keep the old fixed narrow card width that forced clipped text");
    AssertTrue(actionDockSource.Contains("TextFormatFlags.WordBreak"),
        "course action card descriptions must be allowed to wrap instead of being clipped on one line");
});

Run("course chat message list reserves scrollbar gutter for right avatars", () =>
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            using var messageList = new ChatMessageListControl();
            FlowLayoutPanel panel = GetFieldValue<FlowLayoutPanel>(messageList, "_panel");

            AssertTrue(panel.Padding.Right >= SystemInformation.VerticalScrollBarWidth + 12,
                "chat message list must reserve enough right gutter so the native scrollbar does not overlap right-side avatars");
            AssertTrue(panel.Padding.Right > panel.Padding.Left,
                "chat message list right gutter must be wider than the normal left inset");
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
});

Run("course chat file attachments are downloadable from persisted content", () =>
{
    string dbSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Data", "CourseGuardDbContext.cs"));
    string controllerSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Controllers", "ChatController.cs"));
    string chatPanelSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "CourseWorkspace", "CourseChatPanel.cs"));
    string bubbleSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "Chat", "ChatBubbleControl.cs"));
    string listSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "Chat", "ChatMessageListControl.cs"));

    AssertTrue(dbSource.Contains("FILE_CONTENT BYTEA")
        && dbSource.Contains("GetChatFileContentAsync")
        && dbSource.Contains("@file_content"),
        "chat files must persist binary content in MESSAGES so recipients on another machine can download them");
    AssertTrue(controllerSource.Contains("GetFileMessageContentAsync"),
        "chat controller must expose a permission-checked file content download method");
    AssertTrue(bubbleSource.Contains("FileDownloadRequested")
        && bubbleSource.Contains("RequestFileDownload")
        && bubbleSource.Contains("Cursor = Cursors.Hand"),
        "chat file bubbles must be clickable and raise a file download request");
    AssertTrue(listSource.Contains("FileDownloadRequested"),
        "message list must forward file download requests from bubbles");
    AssertTrue(chatPanelSource.Contains("DownloadChatFileAsync")
        && chatPanelSource.Contains("SaveFileDialog")
        && chatPanelSource.Contains("GetFileMessageContentAsync"),
        "course chat panel must download file bytes and let the user save the attachment");
});

Run("course chat image previews hydrate from persisted content when local path is missing", () =>
{
    string dbSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Data", "CourseGuardDbContext.cs"));
    string controllerSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Controllers", "ChatController.cs"));

    string sendImage = ExtractMethodSource(controllerSource, "public async Task<ChatMessageModel?> SendImageMessageAsync");
    AssertTrue(sendImage.Contains("File.ReadAllBytesAsync(saved.SavedPath, cancellationToken)"),
        "single image chat messages must persist compressed image bytes, not only the sender's local path");
    AssertTrue(sendImage.Contains("fileContent"),
        "single image chat send path must pass stored image bytes into the chat message insert");

    AssertTrue(dbSource.Contains("m.FILE_CONTENT"),
        "chat message queries must read stored image bytes so recipients can hydrate previews");
    AssertTrue(dbSource.Contains("HydrateStoredChatImage"),
        "chat messages must hydrate missing local image paths from persisted content");
    AssertTrue(dbSource.Contains("ChatImageHelper.IsImageMessage"),
        "hydration must be limited to chat image messages");
    AssertTrue(dbSource.Contains("File.WriteAllBytes(cachePath, content)"),
        "hydration must write image content to a local preview cache");
});

Run("course chat own file bubble stays inside row before right avatar", () =>
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            using var avatarLoader = new AvatarImageLoader();
            using var imageLoader = new ChatImageLoader();
            var message = new ChatMessageModel
            {
                Id = 77,
                CourseId = 10,
                SenderId = 42,
                SenderName = "Bạn",
                MessageType = "FILE",
                FileName = "NT106_QuanLyKhoaHoc_ThiOnline-main.zip",
                FileSize = 202_700,
                MimeType = "application/zip",
                SentAt = DateTime.Now,
                DeliveryStatus = "SENT"
            };

            using var bubble = new ChatBubbleControl(message, currentUserId: 42, avatarLoader, imageLoader);
            bubble.UpdateContainerWidth(340);
            bubble.CreateControl();
            bubble.PerformLayout();

            TableLayoutPanel row = bubble.Controls.OfType<TableLayoutPanel>().Single();
            row.PerformLayout();
            Panel rightHost = row.Controls.OfType<Panel>().First(panel => panel.Controls.Count == 1 && panel.Controls[0] is FlowLayoutPanel);
            rightHost.PerformLayout();
            Control contentStack = rightHost.Controls[0];
            Control fileBubble = GetAllControls(contentStack).First(control => control.GetType().Name == "RoundedBubblePanel");

            AssertTrue(contentStack.Width <= rightHost.ClientSize.Width,
                $"own file message content stack must not be wider than the row cell before the right avatar (stack={contentStack.Width}, host={rightHost.ClientSize.Width}, fileBubble={fileBubble.Width})");
            AssertTrue(fileBubble.Width <= rightHost.ClientSize.Width,
                $"own file message bubble must fit inside its row cell instead of being clipped under the avatar (stack={contentStack.Width}, host={rightHost.ClientSize.Width}, fileBubble={fileBubble.Width})");
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
});

Run("course chat long unbroken text wraps instead of clipping", () =>
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            using var avatarLoader = new AvatarImageLoader();
            using var imageLoader = new ChatImageLoader();
            string longContent = new string('a', 52) + new string('b', 28);
            var message = new ChatMessageModel
            {
                Id = 78,
                CourseId = 10,
                SenderId = 42,
                SenderName = "Bạn",
                MessageType = "TEXT",
                Content = longContent,
                SentAt = DateTime.Now,
                DeliveryStatus = "SENT"
            };

            using var bubble = new ChatBubbleControl(message, currentUserId: 42, avatarLoader, imageLoader);
            bubble.UpdateContainerWidth(340);
            bubble.CreateControl();
            bubble.PerformLayout();

            Control textBubble = GetAllControls(bubble).First(control => control.GetType().Name == "RoundedBubblePanel");
            string drawnBody = GetFieldValue<string>(textBubble, "_body");

            AssertTrue(drawnBody.Contains('\u200B'),
                "long unbroken chat text must include soft break opportunities before painting so TextRenderer can wrap it");
            AssertTrue(textBubble.Height > 44,
                $"long unbroken chat text bubble must grow beyond a single line instead of clipping horizontally (height={textBubble.Height})");
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
});

Run("course workspace selected course and tools follow direct dashboard flow", () =>
{
    string workspaceSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "CourseWorkspace", "CourseWorkspaceControl.cs"));
    string sidebarSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "CourseWorkspace", "CourseFirstSidebarControl.cs"));
    string studentFactorySource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Student", "StudentCourseToolFactory.cs"));
    string teacherFactorySource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "TeacherCourseToolFactory.cs"));

    AssertTrue(sidebarSource.Contains("_courseSelectorButton.Text = FormatSelectedCourseText(course)"),
        "sidebar course selector must reflect the active course name after course workspace loads");
    AssertTrue(workspaceSource.Contains("tool.OpenDetail(_context)") && !workspaceSource.Contains("_toolDrawer.ShowTool(_context, tool)"),
        "course action cards must navigate directly instead of opening a low-value details drawer first");
    AssertFalse(workspaceSource.Contains("ExtractMetricFromPreview") || workspaceSource.Contains("tool.CreatePreview(_context)"),
        "course header must not synchronously build stats from tool previews because student preview data can use async database calls");
    AssertTrue(workspaceSource.Contains("_secondaryText.SetStats(BuildStatsParts())"),
        "course header should still render lightweight selected-course context");
    AssertTrue(studentFactorySource.Contains("RunBackground(() => dbContext.GetStudentLessonsAsync(studentId))")
        && studentFactorySource.Contains("RunBackground(() => dbContext.GetStudentAssignmentsAsync(studentId))"),
        "student preview factory must not wait directly on async database methods from the UI synchronization context");
    AssertTrue(studentFactorySource.Contains("Vào trang Bài học") && teacherFactorySource.Contains("Vào trang Bài học"),
        "course tool primary labels must use explicit page navigation wording");
});

Run("course subpages keep a visible return path to course hub", () =>
{
    string topbarSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Theme", "TopbarPanel.cs"));
    string sidebarSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "CourseWorkspace", "CourseFirstSidebarControl.cs"));
    string studentDashboardSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "StudentDashboard.cs"));
    string teacherDashboardSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherDashboard.cs"));

    AssertTrue(topbarSource.Contains("BreadcrumbBackRequested") && topbarSource.Contains("SetBreadcrumb"),
        "topbar must expose a clickable breadcrumb parent action for course subpages");
    AssertTrue(sidebarSource.Contains("CourseHubReturnRequested") && sidebarSource.Contains("SetCourseReturnVisible") && sidebarSource.Contains("Quay về khóa học"),
        "course sidebar must expose a conditional return-to-course-hub nav row");
    AssertTrue(studentDashboardSource.Contains("ConfigureCourseSubpageChrome") && studentDashboardSource.Contains("ReturnToCurrentCourseWorkspace"),
        "student course subpages must configure breadcrumb chrome and return to the selected course workspace");
    AssertTrue(teacherDashboardSource.Contains("ConfigureCourseSubpageChrome") && teacherDashboardSource.Contains("ReturnToCurrentCourseWorkspace"),
        "teacher course subpages must configure breadcrumb chrome and return to the selected course workspace");
});

Run("course workspace update context reloads chat for selected course", () =>
{
    Exception? failure = null;
    bool previousStartRealtime = CourseChatPanel.StartRealtimeForTesting;
    try
    {
        CourseChatPanel.StartRealtimeForTesting = false;
        var thread = new Thread(() =>
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

                var initialContext = new CourseWorkspaceContext(
                    7,
                    10,
                    "Course A",
                    "A101",
                    "Active",
                    CourseWorkspaceRole.Student);
                var updatedContext = initialContext with
                {
                    CourseId = 20,
                    CourseName = "Course B",
                    SecondaryText = "B202"
                };
                CourseToolDefinition tool = NewCourseTool("lessons", "Lessons", "View lessons");

                using var workspace = new CourseWorkspaceControl(
                    initialContext,
                    (ChatController)RuntimeHelpers.GetUninitializedObject(typeof(ChatController)),
                    new[] { tool },
                    allowPollCreation: false);
                CourseChatPanel chatPanel = GetFieldValue<CourseChatPanel>(workspace, "_chatPanel");
                List<int> requestedCourses = new();
                chatPanel.MessageLoaderForTesting = (_, courseId, _, _) =>
                {
                    requestedCourses.Add(courseId);
                    return Task.FromResult(new List<ChatMessageModel>
                    {
                        NewChatMessage(courseId + 1000, courseId, $"Message for course {courseId}")
                    });
                };

                WaitForTaskWithMessagePump(workspace.LoadAsync());
                workspace.UpdateContext(updatedContext, new[] { tool });

                ChatMessageListControl messageList = GetFieldValue<ChatMessageListControl>(chatPanel, "_messageList");
                DateTime deadline = DateTime.UtcNow.AddSeconds(3);
                while (!requestedCourses.Contains(20) && DateTime.UtcNow < deadline)
                {
                    Application.DoEvents();
                    Thread.Sleep(10);
                }

                int[] loadedIds = messageList.LoadedMessageIds.OrderBy(id => id).ToArray();
                AssertEqual("10,20", string.Join(",", requestedCourses));
                AssertEqual("1020", string.Join(",", loadedIds));
                AssertFalse(loadedIds.Contains(1010), "UpdateContext must not leave stale chat messages from the previous course");
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
    finally
    {
        CourseChatPanel.StartRealtimeForTesting = previousStartRealtime;
    }

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
});

Run("course action dock renders row content and raises selected tool event", () =>
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            CourseToolDefinition tool = NewCourseTool("materials", "Materials", "Open shared files", badgeText: "New");
            using var dock = new CourseActionDock(new[] { tool });
            CourseToolDefinition? selected = null;

            dock.ToolSelected += (_, selectedTool) => selected = selectedTool;

            Button row = GetAllControls(dock).OfType<Button>().Single(button => ReferenceEquals(button.Tag, tool));
            string rowText = row.Text;

            AssertTrue(rowText.Contains("Materials"), "dock row must render the tool label");
            AssertTrue(rowText.Contains("Open shared files"), "dock row must render the tool description");
            AssertTrue(rowText.Contains("New"), "dock row must render optional badge text");
            AssertFalse(rowText.Contains("[MATERIALS]"), "dock row must not expose raw technical icon tags");
            AssertFalse(rowText.Contains(">"), "dock row must render chevron as chrome instead of inline text");

            row.PerformClick();

            AssertEqual(tool, selected);
            AssertTrue(row.AutoEllipsis, "dock rows must ellipsize long labels");
            AssertTrue(row.Height >= 64, "dock rows must use a stable compact height");
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
});

Run("course tool drawer accepts preview and primary action", () =>
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            var context = new CourseWorkspaceContext(
                10,
                20,
                "Network Programming",
                "NT106",
                "Active",
                CourseWorkspaceRole.Teacher);
            bool opened = false;
            CourseToolDefinition tool = NewCourseTool("exam", "Exam Center", "Prepare exams", onOpen: () => opened = true);
            var preview = new CourseToolPreview(
                "Exam Center",
                "2 exams need attention",
                new[] { "Draft: TCP Basics", "Ready: Socket Lab" },
                "Open exams");

            using var drawer = new CourseToolDrawer();
            drawer.ShowPreview(context, tool, preview);

            Label[] labels = GetAllControls(drawer).OfType<Label>().ToArray();
            AssertTrue(labels.Any(label => label.Text == "Exam Center"), "drawer must render preview title");
            AssertTrue(labels.Any(label => label.Text == "2 exams need attention"), "drawer must render preview summary");
            AssertTrue(labels.Any(label => label.Text == "Draft: TCP Basics"), "drawer must render preview rows");

            Button primary = GetAllControls(drawer).OfType<Button>().Single(button => button.Text == "Open exams");
            AssertTrue(primary.Region != null || primary.FlatStyle == FlatStyle.Flat,
                "drawer primary action must be styled as a modern rounded button");
            primary.PerformClick();
            AssertTrue(opened, "drawer primary action must invoke the tool detail action");
            AssertFalse(drawer.Visible, "drawer must close after a successful primary action so the user can return to the course workspace");

            drawer.ShowPreview(context, tool, preview);
            Button close = GetAllControls(drawer).OfType<Button>().Single(button => button.Text == "< Quay lai");
            AssertTrue(close.Region != null || close.FlatStyle == FlatStyle.Flat,
                "drawer back action must be styled as a modern rounded icon button");
            close.PerformClick();
            AssertFalse(drawer.Visible, "drawer back button must hide the drawer");
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
});

Run("course tool drawer preview factory failure renders fallback without stale preview", () =>
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            var context = new CourseWorkspaceContext(
                10,
                20,
                "Network Programming",
                "NT106",
                "Active",
                CourseWorkspaceRole.Teacher);
            CourseToolDefinition staleTool = NewCourseTool("exam", "Exam Center", "Prepare exams");
            CourseToolDefinition brokenTool = NewCourseTool(
                "materials",
                "Materials",
                "Open shared files",
                onPreview: () => throw new InvalidOperationException("Preview source offline"));

            using var drawer = new CourseToolDrawer();
            drawer.ShowPreview(
                context,
                staleTool,
                new CourseToolPreview(
                    "Exam Center",
                    "2 exams need attention",
                    new[] { "Draft: TCP Basics" },
                    "Open exams"));

            drawer.ShowTool(context, brokenTool);

            Label[] labels = GetAllControls(drawer).OfType<Label>().ToArray();
            AssertTrue(drawer.Visible, "drawer must remain visible when preview factory fails");
            AssertTrue(labels.Any(label => label.Text == "Materials"), "drawer must keep the failed tool title context");
            AssertTrue(labels.Any(label => label.Text.Contains("Khong the tai du lieu") || label.Text.Contains("Không thể tải dữ liệu")), "drawer must render a preview load failure message");
            AssertTrue(labels.Any(label => label.Text.Contains("Preview source offline")), "drawer fallback should include useful failure detail");
            AssertFalse(labels.Any(label => label.Text == "Draft: TCP Basics"), "failed preview must not leave stale preview rows visible");
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
});

Run("course tool drawer primary action failure renders safe fallback", () =>
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            var context = new CourseWorkspaceContext(
                10,
                20,
                "Network Programming",
                "NT106",
                "Active",
                CourseWorkspaceRole.Teacher);
            CourseToolDefinition brokenTool = NewCourseTool(
                "materials",
                "Materials",
                "Open shared files",
                onOpen: () => throw new InvalidOperationException("Route unavailable"));

            using var drawer = new CourseToolDrawer();
            drawer.ShowPreview(
                context,
                brokenTool,
                new CourseToolPreview(
                    "Materials",
                    "Open shared files",
                    new[] { "Course: Network Programming" },
                    "Open materials"));

            Button primary = GetAllControls(drawer).OfType<Button>().Single(button => button.Text == "Open materials");
            primary.PerformClick();

            Label[] labels = GetAllControls(drawer).OfType<Label>().ToArray();
            AssertTrue(drawer.Visible, "drawer must remain visible when primary action fails");
            AssertTrue(labels.Any(label => label.Text.Contains("Route unavailable")),
                "drawer primary action fallback should include useful failure detail");
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
});

Run("course tool drawer uses workspace-style preview surface", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "CourseWorkspace", "CourseToolDrawer.cs"));

    AssertTrue(source.Contains("private readonly RoundedPanel _previewSurface"),
        "tool drawer must group preview content inside a rounded surface instead of loose labels on the drawer background");
    AssertTrue(source.Contains("private readonly Label _eyebrow"),
        "tool drawer must show a small detail label so the drawer reads as a module, not a detached form");
    AssertTrue(source.Contains("BuildPreviewSurface"),
        "tool drawer must build preview chrome through a dedicated method");
    AssertTrue(source.Contains("surface.Controls.Add(_previewSurfaceLayout)"),
        "tool drawer rows and custom preview controls must live inside the rounded preview surface");
    AssertTrue(source.Contains("RowStyles.Add(new RowStyle(SizeType.Percent, 100f))"),
        "tool drawer preview surface must reserve flexible space for real preview controls");
    AssertTrue(source.Contains("RoundedButtonHelper.Apply(12, _primaryAction)") || source.Contains("RoundedButtonHelper.Apply(12, _primaryAction, _closeButton)"),
        "tool drawer primary action must use the same 12px rounded button radius as course workspace actions");
});

Run("course chat panel ignores stale messages when course loads complete out of order", () =>
{
    Exception? failure = null;
    bool previousStartRealtime = CourseChatPanel.StartRealtimeForTesting;
    try
    {
        CourseChatPanel.StartRealtimeForTesting = false;
        var thread = new Thread(() =>
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

                using var panel = new CourseChatPanel(
                    99,
                    (ChatController)RuntimeHelpers.GetUninitializedObject(typeof(ChatController)),
                    allowPollCreation: false);

                TaskCompletionSource<List<ChatMessageModel>> courseALoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
                TaskCompletionSource<List<ChatMessageModel>> courseBLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
                List<int> requestedCourses = new();

                panel.MessageLoaderForTesting = (_, courseId, _, _) =>
                {
                    requestedCourses.Add(courseId);
                    return courseId == 10 ? courseALoad.Task : courseBLoad.Task;
                };

                Task loadA = panel.LoadCourseAsync(10, "Course A");
                Task loadB = panel.LoadCourseAsync(20, "Course B");

                courseBLoad.SetResult(new List<ChatMessageModel>
                {
                    NewChatMessage(201, 20, "B is current")
                });
                WaitForTaskWithMessagePump(loadB);

                courseALoad.SetResult(new List<ChatMessageModel>
                {
                    NewChatMessage(101, 10, "A completed late")
                });
                WaitForTaskWithMessagePump(Task.WhenAll(loadA, loadB));

                ChatMessageListControl messageList = GetFieldValue<ChatMessageListControl>(panel, "_messageList");
                int[] loadedIds = messageList.LoadedMessageIds.OrderBy(id => id).ToArray();

                AssertEqual("10,20", string.Join(",", requestedCourses));
                AssertEqual("201", string.Join(",", loadedIds));
                AssertFalse(loadedIds.Contains(101), "stale course A messages must not render after course B has loaded");
                AssertTrue(loadedIds.Contains(201), "course B messages must render instead of leaving the panel blank");
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
    finally
    {
        CourseChatPanel.StartRealtimeForTesting = previousStartRealtime;
    }

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
});

Run("legacy student and teacher chat constructors remain available", () =>
{
    AssertTrue(
        typeof(UC_Chat).GetConstructor(new[] { typeof(int), typeof(ChatController) }) != null,
        "UC_Chat must keep public constructor (int, ChatController)");
    AssertTrue(
        typeof(UC_TeacherMessages).GetConstructor(new[] { typeof(int) }) != null,
        "UC_TeacherMessages must keep public constructor (int)");
});

Run("course selector recent list preserves recency and removes duplicates", () =>
{
    CourseSelectorItem[] courses =
    {
        new(1, "Socket Programming", "NT106.01", "Active", CourseWorkspaceRole.Student),
        new(2, "Database Systems", "DB101", "Active", CourseWorkspaceRole.Student),
        new(3, "Network Security", "NS201", "Archived", CourseWorkspaceRole.Teacher)
    };
    int[] recentCourseIds = { 3, 2, 3, 99, 1 };

    IReadOnlyList<CourseSelectorItem> recent = CourseSelectorState.BuildRecentCourses(courses, recentCourseIds, maxCount: 3);

    AssertEqual(3, recent.Count);
    AssertEqual(3, recent[0].CourseId);
    AssertEqual(2, recent[1].CourseId);
    AssertEqual(1, recent[2].CourseId);

    IReadOnlyList<CourseSelectorItem> limited = CourseSelectorState.BuildRecentCourses(courses, recentCourseIds, maxCount: 2);
    AssertEqual(2, limited.Count);
    AssertEqual(3, limited[0].CourseId);
    AssertEqual(2, limited[1].CourseId);
});

Run("course selector initial course prefers existing course id", () =>
{
    CourseSelectorItem[] courses =
    {
        new(10, "Computer Networks", "NT101", "Active", CourseWorkspaceRole.Student),
        new(20, "Network Programming", "NT106", "Active", CourseWorkspaceRole.Teacher)
    };

    CourseSelectorItem? preferred = CourseSelectorState.FindInitialCourse(courses, preferredCourseId: 20);
    CourseSelectorItem? fallback = CourseSelectorState.FindInitialCourse(courses, preferredCourseId: 99);
    CourseSelectorItem? empty = CourseSelectorState.FindInitialCourse(Array.Empty<CourseSelectorItem>(), preferredCourseId: 20);

    AssertEqual(20, preferred?.CourseId);
    AssertEqual(10, fallback?.CourseId);
    AssertEqual<CourseSelectorItem?>(null, empty);
});

Run("course sidebar badge text ignores secondary course metadata", () =>
{
    MethodInfo extractCourseCode = typeof(CourseFirstSidebarControl).GetMethod("ExtractCourseCode", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("CourseFirstSidebarControl.ExtractCourseCode was not found");

    string Extract(CourseSelectorItem course)
    {
        return (string)(extractCourseCode.Invoke(null, new object[] { course }) ?? string.Empty);
    }

    AssertEqual("TN", Extract(new CourseSelectorItem(1, "test new ui", "1 sinh viên", "Active", CourseWorkspaceRole.Teacher)));
    AssertEqual("TE", Extract(new CourseSelectorItem(2, "testt", "0 sinh viên", "Active", CourseWorkspaceRole.Teacher)));
    AssertEqual("NT106", Extract(new CourseSelectorItem(3, "NT106.P12 Mang may tinh", "Nguyen Thi Hoa", "Active", CourseWorkspaceRole.Student)));
});

Run("course first sidebar exposes global navigation and course selection events", () =>
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            CourseSelectorItem[] courses =
            {
                new(1, "Socket Programming With A Very Long Course Name", "NT106.01", "Active", CourseWorkspaceRole.Student),
                new(2, "Database Systems", "DB101", "Active", CourseWorkspaceRole.Student),
                new(3, "Network Security", "NS201", "Archived", CourseWorkspaceRole.Student),
                new(4, "Distributed Systems", "DS301", "Active", CourseWorkspaceRole.Student)
            };

            using var sidebar = new CourseFirstSidebarControl();
            List<string> globalRequests = new();
            List<CourseSelectorItem> selectedCourses = new();

            sidebar.GlobalNavigationRequested += (_, label) => globalRequests.Add(label);
            sidebar.CourseSelected += (_, course) => selectedCourses.Add(course);

            sidebar.Size = new Size(192, 600);
            sidebar.Configure(CourseWorkspaceRole.Student, courses, selectedCourseId: 2);
            sidebar.PerformLayout();
            selectedCourses.Clear();

            Button overview = GetAllControls(sidebar).OfType<Button>().Single(button => button.Text == "Tổng quan");
            AssertTrue(overview.GetType().Name.Contains("RoundedSidebarButton", StringComparison.Ordinal),
                "global sidebar nav rows must use rounded buttons");
            overview.PerformClick();

            Button selector = GetAllControls(sidebar).OfType<Button>().Single(button => button.Tag as string == "course-selector-button");
            AssertEqual("Database Systems", selector.Text);
            selector.PerformClick();
            sidebar.PerformLayout();

            RoundedPanel dropdown = GetAllControls(sidebar)
                .OfType<RoundedPanel>()
                .Single(panel => panel.GetType().Name == "CourseDropdownPanel");
            AssertTrue(dropdown.Visible, "course dropdown must become visible after clicking the selector");
            AssertTrue(dropdown.Width >= selector.Width - 2,
                "course dropdown must render as a full-width panel instead of a narrow clipped pill");
            AssertTrue(dropdown.Top >= selector.Bottom,
                "course dropdown must open below the selector inside the course section");

            Button[] courseRows = GetAllControls(sidebar)
                .OfType<Button>()
                .Where(button => button.Tag is CourseSelectorItem)
                .ToArray();

            AssertEqual(4, courseRows.Length);
            AssertTrue(courseRows.All(button => button.Height == courseRows[0].Height), "course dropdown rows must keep stable height");
            AssertTrue(courseRows.All(button => button.AutoEllipsis), "course dropdown rows must ellipsize long names");
            AssertTrue(courseRows.All(button => button.GetType().Name.Contains("CourseDropdownRowButton", StringComparison.Ordinal)),
                "course dropdown rows must use custom badge row buttons");

            courseRows.Single(button => ((CourseSelectorItem)button.Tag!).CourseId == 3).PerformClick();
            AssertEqual("Network Security", selector.Text);

            sidebar.SetActiveCourse(4);
            AssertEqual("Distributed Systems", selector.Text);

            Button courseWorkspaceButton = GetAllControls(sidebar)
                .OfType<Button>()
                .Single(button => button.Text == "Trang khóa học");
            Control courseSection = courseWorkspaceButton.Parent
                ?? throw new InvalidOperationException("Course workspace nav item has no parent");
            int courseSectionContentWidth = courseSection.ClientSize.Width - courseSection.Padding.Horizontal;
            AssertTrue(courseWorkspaceButton.Width + courseWorkspaceButton.Margin.Horizontal <= courseSectionContentWidth,
                "course workspace nav item must fit inside the padded course section so rounded right corners are not clipped");
            Rectangle workspaceButtonScreenBounds = courseWorkspaceButton.RectangleToScreen(courseWorkspaceButton.ClientRectangle);
            Rectangle sidebarScreenBounds = sidebar.RectangleToScreen(sidebar.ClientRectangle);
            AssertTrue(workspaceButtonScreenBounds.Right <= sidebarScreenBounds.Right - 10,
                "course workspace nav item must keep the same right inset as the course selector instead of being clipped by the sidebar edge");

            Button activeCourseRow = GetAllControls(sidebar)
                .OfType<Button>()
                .Single(button => button.Tag is CourseSelectorItem course && course.CourseId == 4);

            AssertTrue(GetAllControls(sidebar).OfType<Button>().Any(button => button.Text == "Khóa học"),
                "course sidebar must expose the legacy course page as a global nav item");
            AssertEqual(AppColors.AccentSoft, activeCourseRow.BackColor);
            sidebar.SetActiveGlobalItem("Tổng quan");
            AssertEqual(AppColors.BgCard, activeCourseRow.BackColor);
            AssertEqual(1, globalRequests.Count);
            AssertEqual("Tổng quan", globalRequests[0]);
            AssertEqual(1, selectedCourses.Count);
            AssertEqual(3, selectedCourses[0].CourseId);
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
});

Run("course first sidebar uses prototype course dropdown without role switch", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "CourseWorkspace", "CourseFirstSidebarControl.cs"));

    AssertTrue(source.Contains("CourseWorkspaceLabel = \"Kh\u00f3a h\u1ecdc\""),
        "course sidebar must keep a stable Kh\u00f3a h\u1ecdc module label");
    AssertTrue(source.Contains("CourseSelectorPlaceholder = \"Ch\u1ecdn kh\u00f3a h\u1ecdc...\""),
        "course sidebar selector must use the prototype placeholder");
    AssertTrue(source.Contains("CourseDropdownRowButton"),
        "course sidebar dropdown must render badge rows for each course");
    AssertTrue(source.Contains("BuildBrandHeader"),
        "course sidebar must include the CourseGuard brand header");
    AssertFalse(source.Contains("ComboBox"),
        "course sidebar must not use native ComboBox for the prototype dropdown");
    AssertFalse(source.Contains("H\u1ecdc sinh") && source.Contains("Gi\u1ea3ng vi\u00ean"),
        "course sidebar must not merge Student and Teacher accounts through a role switch");
});

Run("course tool drawer uses rounded chrome for buttons and preview rows", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "CourseWorkspace", "CourseToolDrawer.cs"));

    AssertTrue(source.Contains("RoundedButtonHelper.Apply(12, _primaryAction, _closeButton)"),
        "tool drawer must round both the primary action and the back/close button");
    AssertTrue(source.Contains("new RoundedPanel"),
        "tool drawer preview metadata rows must use rounded panels instead of raw rectangular labels");
    AssertTrue(source.Contains("Text = \"< Quay lai\""),
        "tool drawer must present a readable back affordance instead of a raw X button or font-sensitive glyph");
    AssertTrue(source.Contains("HideDrawer();") && source.Contains("_tool.OpenDetail(_context);"),
        "tool drawer must close after successful primary navigation so users are not trapped behind the drawer");
});

Run("mfa otp dialog keeps hidden native input on concrete background", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Login", "MfaOtpDialog.cs"));

    AssertTrue(source.Contains("BackColor = OtpInputBackColor"),
        "hidden OTP TextBox must use a concrete background because native TextBox cannot be transparent");
    AssertFalse(source.Contains("_txtOtp.BackColor = Color.Transparent"),
        "OTP TextBox must never be assigned a transparent background");
});

Run("app theme applies transparent background only through native-safe helper", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Theme", "AppColors.cs"));

    AssertTrue(source.Contains("SetTransparentBackColor"),
        "theme must centralize transparent background assignment");
    AssertTrue(source.Contains("SupportsTransparentBackground"),
        "theme must detect native controls that cannot accept Color.Transparent");
    AssertFalse(source.Contains(".BackColor = Color.Transparent"),
        "theme must not assign transparent backcolor directly during recursive login/dashboard theming");
});

Run("sidebar course combo uses opaque native backcolor in light mode", () =>
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            bool previousMode = AppColors.IsDarkMode;
            AppColors.IsDarkMode = false;
            try
            {
                using var combo = new ComboBox();
                StudentDropdownStyler.StyleComboBox(combo, useCustomPopup: true, displayTextOverride: "Khóa học", useSidebarChrome: true);
                AssertEqual(255, combo.BackColor.A);
            }
            finally
            {
                AppColors.IsDarkMode = previousMode;
            }
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
});

Run("course first sidebar docks logout at bottom", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "CourseWorkspace", "CourseFirstSidebarControl.cs"));

    AssertTrue(source.Contains("_footerRows"),
        "course sidebar must have a footer region for bottom actions");
    AssertTrue(source.Contains("_footerButtons"),
        "course sidebar must track footer buttons separately from top navigation");
    AssertTrue(source.Contains("_layout.Controls.Add(_footerRows, 0, 2)"),
        "footer region must be added to the bottom layout row");
    AssertTrue(source.Contains("CreateFooterRows"),
        "logout must be rendered through footer rows instead of the main nav list");
});

Run("course first sidebar shows student and teacher role-specific global labels", () =>
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            using var studentSidebar = new CourseFirstSidebarControl();
            using var teacherSidebar = new CourseFirstSidebarControl();

            studentSidebar.Configure(CourseWorkspaceRole.Student, Array.Empty<CourseSelectorItem>(), selectedCourseId: null);
            teacherSidebar.Configure(CourseWorkspaceRole.Teacher, Array.Empty<CourseSelectorItem>(), selectedCourseId: null);

            string[] studentLabels = GetAllControls(studentSidebar).OfType<Button>()
                .Where(IsSidebarNavigationButton)
                .Select(button => button.Text)
                .ToArray();
            string[] teacherLabels = GetAllControls(teacherSidebar).OfType<Button>()
                .Where(IsSidebarNavigationButton)
                .Select(button => button.Text)
                .ToArray();

            AssertTrue(studentLabels.Contains("Tổng quan"), "student sidebar must show overview");
            AssertTrue(studentLabels.Contains("Khóa học"), "student sidebar must show the legacy course page as a global item");
            AssertTrue(studentLabels.Contains("Hồ sơ"), "student sidebar must show profile");
            AssertTrue(studentLabels.Contains("Đăng xuất"), "student sidebar must show logout");
            AssertTrue(Array.IndexOf(studentLabels, "Hồ sơ") > Array.IndexOf(studentLabels, "Khóa học"),
                "profile must sit directly below the global course item instead of the footer");
            AssertFalse(studentLabels.Contains("Quản lý khóa học"), "student sidebar must not show teacher course management");
            AssertEqual(0, GetAllControls(studentSidebar).OfType<ComboBox>().Count());

            AssertTrue(teacherLabels.Contains("Tổng quan"), "teacher sidebar must show overview");
            AssertTrue(teacherLabels.Contains("Khóa học"), "teacher sidebar must show the legacy course page as a global item");
            AssertTrue(teacherLabels.Contains("Hồ sơ"), "teacher sidebar must show profile");
            AssertTrue(teacherLabels.Contains("Đăng xuất"), "teacher sidebar must show logout");
            AssertTrue(Array.IndexOf(teacherLabels, "Hồ sơ") > Array.IndexOf(teacherLabels, "Khóa học"),
                "profile must sit directly below the global course item instead of the footer");
            AssertFalse(teacherLabels.Contains("Tìm khóa học"), "teacher sidebar must not show student course search");
            AssertEqual(0, GetAllControls(teacherSidebar).OfType<ComboBox>().Count());

            Label emptyState = GetAllControls(studentSidebar).OfType<Label>().Single(label => label.Text == "Chưa có khóa học");
            AssertTrue(emptyState.Visible, "empty state must show when no courses are available");
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
});

Run("course first sidebar nests selector and course rows under the prototype course box", () =>
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            CourseSelectorItem[] courses =
            {
                new(1, "Socket Programming", "NT106.01", "Active", CourseWorkspaceRole.Student),
                new(2, "Database Systems", "DB101", "Active", CourseWorkspaceRole.Student)
            };

            using var sidebar = new CourseFirstSidebarControl();
            sidebar.Configure(CourseWorkspaceRole.Student, courses, selectedCourseId: 1);

            TableLayoutPanel layout = SingleChild<TableLayoutPanel>(sidebar);
            FlowLayoutPanel navRows = layout.Controls
                .OfType<FlowLayoutPanel>()
                .Single(panel => panel.Controls.OfType<Button>().Any(button => button.Text == "Tổng quan"));

            Control[] orderedRows = navRows.Controls.Cast<Control>().ToArray();
            AssertTrue(orderedRows.Any(control => control.Name == "BrandHeader"), "brand header must be first-class sidebar chrome");
            AssertTrue(orderedRows.Any(control => control.Name == "CourseSection"), "course box must be a first-class sidebar section");
            AssertTrue(orderedRows.OfType<Label>().Any(label => label.Text == "TỔNG QUAN"), "overview section heading must be visible");
            AssertTrue(orderedRows.OfType<Button>().Any(button => button.Text == "Khóa học"),
                "legacy course page must sit under the overview heading as a global item");

            FlowLayoutPanel courseSection = AssertControl<FlowLayoutPanel>(
                orderedRows.Single(control => control.Name == "CourseSection"),
                "course selector section must use the prototype course box");

            AssertTrue(courseSection.Controls.OfType<Button>().Any(button => button.Tag as string == "course-selector-button"),
                "course selector button must be inside the course section");
            AssertTrue(courseSection.Controls.OfType<RoundedPanel>().Any(panel => panel.Controls.OfType<FlowLayoutPanel>().Any(rows => rows.Controls.OfType<Button>().Any(button => button.Tag is CourseSelectorItem))),
                "course dropdown rows must be inside the course section");
            AssertTrue(courseSection.Controls.OfType<Label>().Any(label => label.Text == "KHÓA HỌC"),
                "course heading must be inside the course section");
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
});
Run("plan 04 login result contract exists", () =>
{
    Type type = typeof(LoginResultModel);

    AssertTrue(type.GetProperty(nameof(LoginResultModel.User)) != null,
        "LoginResultModel must expose User");
    AssertTrue(type.GetProperty(nameof(LoginResultModel.ErrorCode)) != null,
        "LoginResultModel must expose ErrorCode");
    AssertTrue(type.GetProperty(nameof(LoginResultModel.MustChangePassword)) != null,
        "LoginResultModel must expose MustChangePassword");
    AssertTrue(typeof(LoginErrorCodes).GetField(nameof(LoginErrorCodes.TempPasswordExpired)) != null,
        "LoginErrorCodes must expose TempPasswordExpired");
});

Run("plan 04 login async returns structured result", () =>
{
    MethodInfo? method = typeof(AuthController).GetMethod(
        nameof(AuthController.LoginAsync),
        new[] { typeof(string), typeof(string), typeof(CancellationToken) });

    AssertTrue(method != null, "AuthController.LoginAsync must exist");
    AssertEqual(typeof(Task<LoginResultModel>), method!.ReturnType);
});

Run("plan 04 temp password schema and mappings are guarded", () =>
{
    string root = RepoRoot();
    string dbSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Backend", "Data", "CourseGuardDbContext.cs"));
    string userModelSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Backend", "Models", "UserModel.cs"));
    string migrationPath = Path.Combine(root, "CourseGuard", "CourseGuard", "Backend", "Database", "Scripts", "Migrations", "20260615_security_temp_password_expiry.sql");

    AssertTrue(userModelSource.Contains("DateTime? TempPasswordExpiresAt"),
        "UserModel must carry temp password expiry");
    AssertTrue(dbSource.Contains("EnsureUserSecuritySchema"),
        "CourseGuardDbContext must add a runtime schema guard for users security columns");
    AssertTrue(dbSource.Contains("temp_password_expires_at"),
        "CourseGuardDbContext must read and write temp_password_expires_at");
    AssertTrue(File.Exists(migrationPath),
        "temp password expiry migration must be present");
});

Run("plan 04 keeps student code computed with HS prefix", () =>
{
    string root = RepoRoot();
    string profileSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Backend", "Models", "StudentProfileModel.cs"));
    string dbSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Backend", "Data", "CourseGuardDbContext.cs"));
    string migrations = string.Join(
        Environment.NewLine,
        Directory.GetFiles(Path.Combine(root, "CourseGuard", "CourseGuard", "Backend", "Database", "Scripts", "Migrations"), "*.sql")
            .Select(File.ReadAllText));

    AssertTrue(profileSource.Contains("$\"HS{UserId:00000}\""),
        "StudentProfileModel must keep computed HS student code");
    AssertTrue(dbSource.Contains("$\"HS{userId:00000}\""),
        "CourseGuardDbContext score lookups must keep HS student code");
    AssertFalse(migrations.Contains("student_code", StringComparison.OrdinalIgnoreCase),
        "Plan 04 must not add a persisted student_code column");
});

Run("plan 04 student pages accept shared controllers", () =>
{
    ConstructorInfo? courseListCtor = typeof(UC_CourseList).GetConstructor(new[] { typeof(int), typeof(CourseController) });
    ConstructorInfo? myCoursesCtor = typeof(UC_MyCourses).GetConstructor(new[] { typeof(int), typeof(CourseController) });
    ConstructorInfo? chatCtor = typeof(UC_Chat).GetConstructor(new[] { typeof(int), typeof(ChatController) });

    AssertTrue(courseListCtor != null, "UC_CourseList must accept studentId and shared CourseController");
    AssertTrue(myCoursesCtor != null, "UC_MyCourses must accept studentId and shared CourseController");
    AssertTrue(chatCtor != null, "UC_Chat must accept userId and shared ChatController");

    string root = RepoRoot();
    string dashboardSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "StudentDashboard.cs"));

    AssertTrue(dashboardSource.Contains("new UC_CourseList(currentUser.Id, _courseController)"),
        "StudentDashboard must pass the shared course controller to UC_CourseList");
    AssertTrue(dashboardSource.Contains("new CourseWorkspaceControl"),
        "StudentDashboard must route course chat through CourseWorkspaceControl");
});

Run("student dashboard source uses CourseFirstSidebarControl", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "StudentDashboard.cs"));

    AssertTrue(source.Contains("CourseFirstSidebarControl"), "student dashboard must use the shared course-first sidebar");
    AssertTrue(source.Contains("GlobalNavigationRequested"), "student dashboard must handle course-first global navigation");
    AssertTrue(source.Contains("CourseSelected"), "student dashboard must handle course selector changes");
    AssertTrue(source.Contains("_courseController.GetMyEnrollments(currentUser.Id)"), "student dashboard must load sidebar courses from student enrollments");
    AssertTrue(source.Contains("new CourseSelectorItem("), "student dashboard must map enrollments to CourseSelectorItem");
    AssertTrue(source.Contains("CourseWorkspaceRole.Student"), "student course selector items must use the student workspace role");
    AssertFalse(source.Contains("new SidebarPanel"), "student dashboard must not build the old module-first sidebar");
});

Run("student dashboard maps global course item to the legacy course page", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "StudentDashboard.cs"));
    string navigation = ExtractMethodSource(source, "private void InitializeNavigation()");
    string globalHandler = ExtractMethodSource(source, "private void Sidebar_GlobalNavigationRequested");

    AssertTrue(source.Contains("CourseGlobalPage = \"Kh\u00f3a h\u1ecdc\""),
        "student dashboard must expose Kh\u00f3a h\u1ecdc as the visible global course item");
    AssertTrue(navigation.Contains("UC_CourseList"), "student dashboard must keep course search as a global route");
    AssertTrue(navigation.Contains("CourseGlobalPage"), "student dashboard must route the visible Kh\u00f3a h\u1ecdc item");
    AssertTrue(navigation.Contains("currentUser.Id") && navigation.Contains("_courseController"),
        "student course search route must use the current student and shared course controller");
    AssertFalse(globalHandler.Contains("pageName == CourseWorkspacePage"),
        "student global Kh\u00f3a h\u1ecdc item must not be intercepted as the selected-course workspace");
});

Run("student dashboard no longer registers course scoped tools as global sidebar pages", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "StudentDashboard.cs"));
    string navigation = ExtractMethodSource(source, "private void InitializeNavigation()");

    AssertFalse(navigation.Contains("UC_MyCourses"), "student course list must move out of global navigation");
    AssertFalse(navigation.Contains("UC_TakeExam"), "student exams must move out of global navigation");
    AssertFalse(navigation.Contains("UC_StudentAssignments"), "student assignments must move out of global navigation");
    AssertFalse(navigation.Contains("UC_Result"), "student results must move out of global navigation");
    AssertFalse(navigation.Contains("UC_StudentLessons"), "student lessons/materials must move out of global navigation");
    AssertFalse(navigation.Contains("UC_Schedule"), "student schedule must move out of global navigation");
    AssertFalse(navigation.Contains("UC_Chat"), "student chat must move out of global navigation");
});

Run("student dashboard preserves force password profile guard", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "StudentDashboard.cs"));
    string globalHandler = ExtractMethodSource(source, "private void Sidebar_GlobalNavigationRequested");
    string courseHandler = ExtractMethodSource(source, "private void Sidebar_CourseSelected");
    string navigate = ExtractMethodSource(source, "private void NavigateToPage");

    AssertTrue(globalHandler.Contains("_isForceChangePasswordActive"), "global navigation handler must keep force-password guard");
    AssertTrue(globalHandler.Contains("ProfilePage"), "force-password guard must continue allowing profile navigation");
    AssertTrue(globalHandler.Contains("LogoutCurrentUser"), "force-password guard must continue allowing logout");
    AssertTrue(courseHandler.Contains("_isForceChangePasswordActive"), "course selection must be blocked during force-password flow");
    AssertTrue(courseHandler.Contains("return"), "course selection guard must exit without opening a workspace");
    AssertTrue(navigate.Contains("_isForceChangePasswordActive") && navigate.Contains("pageName != ProfilePage") && navigate.Contains("ShowForceChangePasswordWarning"),
        "direct/global navigation, including notifications, must remain blocked during force-password flow");
    AssertTrue(source.Contains("NavigateToPage(ProfilePage, focusSecurity: true)"),
        "forced password change start must still open profile security");
});

// ── plan 04 behavioral security tests (no DB required) ──────────────

Run("teacher dashboard source uses CourseFirstSidebarControl", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherDashboard.cs"));

    AssertTrue(source.Contains("CourseFirstSidebarControl"), "teacher dashboard must use the shared course-first sidebar");
    AssertTrue(source.Contains("GlobalNavigationRequested"), "teacher dashboard must handle course-first global navigation");
    AssertTrue(source.Contains("CourseSelected"), "teacher dashboard must handle course selector changes");
    AssertTrue(source.Contains("_teacherController.GetCourses(_teacherId)"), "teacher dashboard must load sidebar courses from teacher courses");
    AssertTrue(source.Contains("new CourseSelectorItem("), "teacher dashboard must map teacher courses to CourseSelectorItem");
    AssertTrue(source.Contains("course.StudentCount") && source.Contains("sinh vi\u00ean"),
        "teacher course selector secondary text must include the student count");
    AssertTrue(source.Contains("CourseWorkspaceRole.Teacher"), "teacher course selector items must use the teacher workspace role");
    AssertFalse(source.Contains("new SidebarPanel"), "teacher dashboard must not build the old module-first sidebar");
});

Run("teacher dashboard maps global course item to the legacy manage courses page", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherDashboard.cs"));
    string navigation = ExtractMethodSource(source, "private void InitializeNavigation()");
    string globalHandler = ExtractMethodSource(source, "private void Sidebar_GlobalNavigationRequested");

    AssertTrue(source.Contains("CourseGlobalPage = \"Kh\u00f3a h\u1ecdc\""),
        "teacher dashboard must expose Kh\u00f3a h\u1ecdc as the visible global course item");
    AssertTrue(navigation.Contains("CourseGlobalPage"), "teacher dashboard must route the visible Kh\u00f3a h\u1ecdc item");
    AssertTrue(navigation.Contains("UC_TeacherCourses"), "teacher course management route must open UC_TeacherCourses");
    AssertTrue(navigation.Contains("_teacherId") && navigation.Contains("_teacherController"),
        "teacher course management route must use the current teacher and shared teacher controller");
    AssertFalse(globalHandler.Contains("pageName == CourseWorkspacePage"),
        "teacher global Kh\u00f3a h\u1ecdc item must not be intercepted as the selected-course workspace");
});

Run("teacher dashboard no longer registers course scoped tools as global sidebar pages", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherDashboard.cs"));
    string navigation = ExtractMethodSource(source, "private void InitializeNavigation()");

    AssertFalse(navigation.Contains("UC_TeacherLessons"), "teacher lessons must move out of global navigation");
    AssertFalse(navigation.Contains("UC_TeacherMaterials"), "teacher materials must move out of global navigation");
    AssertFalse(navigation.Contains("UC_TeacherAssignments"), "teacher assignments must move out of global navigation");
    AssertFalse(navigation.Contains("UC_TeacherExams"), "teacher exams must move out of global navigation");
    AssertFalse(navigation.Contains("UC_ExamMonitor"), "teacher exam monitor must not remain a global sidebar route");
    AssertFalse(navigation.Contains("UC_TeacherResults"), "teacher results must move out of global navigation");
    AssertFalse(navigation.Contains("UC_TeacherStudents"), "teacher students must move out of global navigation");
    AssertFalse(navigation.Contains("UC_TeacherSchedule"), "teacher schedule must move out of global navigation");
    AssertFalse(navigation.Contains("UC_TeacherMessages"), "teacher messages must move into the course workspace chat");
});

Run("teacher dashboard preserves force password profile guard", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherDashboard.cs"));
    string globalHandler = ExtractMethodSource(source, "private void Sidebar_GlobalNavigationRequested");
    string courseHandler = ExtractMethodSource(source, "private void Sidebar_CourseSelected");
    string navigate = ExtractMethodSource(source, "public void NavigateToPage");

    AssertTrue(globalHandler.Contains("_isForceChangePasswordActive"), "global navigation handler must keep force-password guard");
    AssertTrue(globalHandler.Contains("ProfilePage"), "force-password guard must continue allowing profile navigation");
    AssertTrue(globalHandler.Contains("LogoutCurrentUser"), "force-password guard must continue allowing logout");
    AssertTrue(courseHandler.Contains("_isForceChangePasswordActive"), "course selection must be blocked during force-password flow");
    AssertTrue(courseHandler.Contains("return"), "course selection guard must exit without opening a workspace");
    AssertTrue(navigate.Contains("_isForceChangePasswordActive") && navigate.Contains("pageName != ProfilePage") && navigate.Contains("ShowForceChangePasswordWarning"),
        "direct/global navigation must remain blocked during force-password flow");
    AssertTrue(source.Contains("NavigateToPage(ProfilePage, focusSecurity: true)"),
        "forced password change start must still open profile security");
});

Run("teacher dashboard adapts legacy routes instead of returning on missing global nav", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherDashboard.cs"));
    string navigate = ExtractMethodSource(source, "public void NavigateToPage");
    string adapter = ExtractMethodSource(source, "private bool TryNavigateLegacyTeacherRoute");
    MethodInfo? mapper = typeof(TeacherDashboard).GetMethod("TryGetLegacyTeacherCourseToolId", BindingFlags.Static | BindingFlags.NonPublic);

    AssertTrue(navigate.Contains("TryNavigateLegacyTeacherRoute(pageName)"),
        "NavigateToPage must hand missing global routes to the legacy teacher route adapter");
    AssertTrue(adapter.Contains("_isForceChangePasswordActive"),
        "legacy adapter must preserve the forced-password guard");
    AssertTrue(adapter.Contains("OpenSelectedOrFirstCourseTool(toolId)"),
        "legacy course-scoped pages must open the selected or first teacher course tool");
    AssertTrue(adapter.Contains("NavigateToPage(ManageCoursesPage)"),
        "legacy course workspace routes must fall back to course management when no course is available");
    AssertTrue(mapper != null, "teacher dashboard must expose a private legacy page-name to course-tool mapper");

    var expected = new Dictionary<string, string>
    {
        ["B\u00e0i h\u1ecdc"] = "lessons",
        ["T\u00e0i li\u1ec7u"] = "materials",
        ["B\u00e0i t\u1eadp"] = "assignments",
        ["B\u00e0i ki\u1ec3m tra"] = "exams",
        ["K\u1ebft qu\u1ea3"] = "results",
        ["Sinh vi\u00ean"] = "students",
        ["L\u1ecbch d\u1ea1y"] = "schedule"
    };

    foreach ((string pageName, string expectedToolId) in expected)
    {
        object?[] args = { pageName, null };
        bool mapped = (bool)mapper!.Invoke(null, args)!;

        AssertTrue(mapped, $"legacy teacher page '{pageName}' must map to a course tool");
        AssertEqual(expectedToolId, args[1]?.ToString());
    }

    object?[] notificationArgs = { "Th\u00f4ng b\u00e1o", null };
    AssertFalse((bool)mapper!.Invoke(null, notificationArgs)!,
        "legacy/global teacher notifications must remain a user-level route instead of a selected-course tool");
});

Run("teacher dashboard opens global notifications as user level page", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherDashboard.cs"));
    string adapter = ExtractMethodSource(source, "private bool TryNavigateLegacyTeacherRoute");
    string openNotifications = ExtractMethodSource(source, "private void OpenTeacherNotificationPage");

    AssertTrue(adapter.Contains("string.Equals(normalized, NotificationPage, StringComparison.Ordinal)")
        && adapter.Contains("OpenTeacherNotificationPage()"),
        "teacher legacy/global notification navigation must directly open the user-level notification page");
    AssertFalse(adapter.Contains("string.Equals(normalized, NotificationPage, StringComparison.Ordinal) && GetSelectedOrFirstCourse() == null"),
        "teacher global notifications must not depend on whether a course is selected or available");
    AssertTrue(openNotifications.Contains("_currentCourseToolId = null") && openNotifications.Contains("_currentCourseContext = null"),
        "teacher user-level notification page must clear course tool state");
    AssertTrue(openNotifications.Contains("new UC_TeacherNotifications(_teacherId, RefreshNotificationChromeSafe)"),
        "teacher user-level notification page must keep the read-state refresh callback");
});

Run("teacher dashboard course dropdown opens workspace while global course opens management", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherDashboard.cs"));
    string globalHandler = ExtractMethodSource(source, "private void Sidebar_GlobalNavigationRequested");
    string courseHandler = ExtractMethodSource(source, "private void Sidebar_CourseSelected");

    AssertFalse(globalHandler.Contains("pageName == CourseWorkspacePage"),
        "global Kh\u00f3a h\u1ecdc item must not be intercepted as a course workspace route");
    AssertTrue(courseHandler.Contains("OpenCourseWorkspace(course)"),
        "course dropdown selection must still open the selected course workspace");
    AssertTrue(source.Contains("{ CourseGlobalPage, () => new UC_TeacherCourses(_teacherId, _teacherController) }"),
        "teacher global Kh\u00f3a h\u1ecdc item must open the real course management page");
});

Run("teacher dashboard passes selected course context into course tool pages", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherDashboard.cs"));
    string openDetail = ExtractMethodSource(source, "private void OpenTeacherCourseToolDetail");

    string[] expectedCreations =
    {
        "new UC_TeacherLessons(_teacherId, _teacherController, context.CourseId)",
        "new UC_TeacherMaterials(_teacherId, _teacherController, context.CourseId)",
        "new UC_TeacherAssignments(_teacherId, _teacherController, context.CourseId)",
        "new UC_TeacherExams(_teacherId, _teacherController, context.CourseId)",
        "new UC_TeacherResults(_teacherId, _teacherController, context.CourseId)",
        "new UC_TeacherStudents(_teacherId, _teacherController, context.CourseId)",
        "new UC_TeacherSchedule(_teacherId, _teacherController, context.CourseId)"
    };

    foreach (string expectedCreation in expectedCreations)
        AssertTrue(openDetail.Contains(expectedCreation), $"OpenTeacherCourseToolDetail must pass context.CourseId with {expectedCreation}");

    AssertTrue(openDetail.Contains("new UC_TeacherNotifications(_teacherId, RefreshNotificationChromeSafe)"),
        "teacher workspace notifications must refresh dashboard notification chrome after read-state changes");
});

Run("teacher dashboard routes legacy exam monitor to monitor control", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherDashboard.cs"));
    string adapter = ExtractMethodSource(source, "private bool TryNavigateLegacyTeacherRoute");
    MethodInfo? courseMapper = typeof(TeacherDashboard).GetMethod("TryGetLegacyTeacherCourseToolId", BindingFlags.Static | BindingFlags.NonPublic);

    object?[] args = { "Gi\u00e1m s\u00e1t thi", null };
    bool mappedAsCourseTool = courseMapper != null && (bool)courseMapper.Invoke(null, args)!;

    AssertFalse(mappedAsCourseTool, "legacy exam monitor must not map to the exams course tool");
    AssertTrue(adapter.Contains("OpenTeacherExamMonitor()"), "legacy exam monitor route must open the real monitor page");
    AssertTrue(source.Contains("new UC_ExamMonitor(_teacherId, _teacherController)"),
        "teacher dashboard must create UC_ExamMonitor for legacy Gi\u00e1m s\u00e1t thi");
});

Run("teacher quick search applies payload course routing before page fallback", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherDashboard.cs"));
    string selected = ExtractMethodSource(source, "private async void Topbar_SearchResultSelected");
    string routePayload = ExtractMethodSource(source, "private bool TryOpenQuickSearchPayloadRoute");

    int payloadIndex = selected.IndexOf("e.Result.Payload is TeacherQuickSearchRequest request", StringComparison.Ordinal);
    int navigateIndex = selected.IndexOf("NavigateToPage(e.Result.PageName)", StringComparison.Ordinal);

    AssertTrue(payloadIndex >= 0, "teacher quick search selection must inspect TeacherQuickSearchRequest payload");
    AssertTrue(navigateIndex < 0 || payloadIndex < navigateIndex,
        "teacher quick search must route payload before falling back to PageName navigation");
    AssertTrue(selected.Contains("TryOpenQuickSearchPayloadRoute(request, e.Result.PageName)"),
        "teacher quick search must ask the payload router before generic navigation");
    AssertTrue(routePayload.Contains("TeacherQuickSearchKinds.Course"),
        "quick search payload router must handle searched courses explicitly");
    AssertTrue(routePayload.Contains("request.ParentId ?? request.Id"),
        "quick search payload router must prefer payload parent course id and fall back to result id");
    AssertTrue(routePayload.Contains("OpenCourseWorkspace(course)"),
        "course quick search results must activate the searched course workspace");
    AssertTrue(routePayload.Contains("OpenTeacherCourseToolDetail(toolId, BuildCourseWorkspaceContext(course))"),
        "non-course quick search payloads must open the matching course tool with the payload course context");
});

Run("teacher result student quick search records include parent course context", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Data", "TeacherRepository.cs"));
    string searchResults = ExtractMethodSource(source, "public List<TeacherQuickSearchResultModel> SearchResultQuickAccess");
    int resultStudentIndex = searchResults.IndexOf("Kind = TeacherQuickSearchKinds.ResultStudent", StringComparison.Ordinal);
    int parentIdIndex = searchResults.IndexOf("ParentId =", resultStudentIndex < 0 ? 0 : resultStudentIndex, StringComparison.Ordinal);

    AssertTrue(searchResults.Contains("SELECT c.id, s.id"),
        "result student quick-search SQL must select the owning course id before the student id");
    AssertTrue(searchResults.Contains("GROUP BY c.id"),
        "result student quick-search SQL must group by course id so ParentId is unambiguous");
    AssertTrue(resultStudentIndex >= 0, "result student quick-search mapping must still emit ResultStudent records");
    AssertTrue(parentIdIndex > resultStudentIndex,
        "ResultStudent quick-search mapping must set ParentId to the owning course id");
});

Run("teacher notifications expose read-state callback for dashboard chrome refresh", () =>
{
    Type notificationsType = typeof(UC_TeacherNotifications);
    AssertTrue(
        notificationsType.GetConstructor(new[] { typeof(int) }) != null,
        "UC_TeacherNotifications must keep public constructor (int)");
    AssertTrue(
        notificationsType.GetConstructor(new[] { typeof(int), typeof(Action) }) != null,
        "UC_TeacherNotifications must expose public constructor (int, Action) for dashboard refresh callbacks");

    string root = RepoRoot();
    string notifications = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherNotifications.cs"));
    string dashboard = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherDashboard.cs"));
    string markRead = ExtractMethodSource(notifications, "private async Task MarkSelectedAsReadAsync");

    AssertTrue(markRead.Contains("_onNotificationReadStateChanged?.Invoke()"),
        "teacher notifications must invoke the dashboard refresh callback after marking a notification read");
    AssertTrue(dashboard.Contains("RefreshNotificationChromeSafe"),
        "teacher dashboard must expose a safe notification chrome refresh method");
    AssertTrue(dashboard.Contains("new UC_TeacherNotifications(_teacherId, RefreshNotificationChromeSafe)"),
        "teacher dashboard must pass the refresh callback when opening teacher notifications");
});

Run("teacher dashboard workspace reloads unread chat through message read events", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherDashboard.cs"));

    AssertTrue(source.Contains("MessagesMarkedRead"), "teacher dashboard must observe workspace message-read events");
    AssertTrue(source.Contains("ReloadChatUnreadCountSafe"), "teacher dashboard must reload total unread after workspace reads");
    AssertTrue(source.Contains("chatPanel.MessagesMarkedRead += (_, _) => ReloadChatUnreadCountSafe();"),
        "teacher dashboard must wire workspace read events to a total unread badge reload");
    AssertFalse(source.Contains("MarkAllRead"), "teacher workspace reads must not mark every course read");
});

Run("student dashboard maps legacy course page names to course tools", () =>
{
    MethodInfo? mapper = typeof(StudentDashboard).GetMethod("TryGetLegacyStudentCourseToolId", BindingFlags.Static | BindingFlags.NonPublic);
    AssertTrue(mapper != null, "student dashboard must expose a private legacy page-name to course-tool mapper");

    var expected = new Dictionary<string, string>
    {
        ["T\u00e0i li\u1ec7u"] = "materials",
        ["B\u00e0i h\u1ecdc"] = "lessons",
        ["B\u00e0i h\u1ecdc v\u00e0 T\u00e0i li\u1ec7u"] = "lessons",
        ["L\u1ecbch h\u1ecdc"] = "schedule",
        ["B\u00e0i t\u1eadp"] = "assignments",
        ["B\u00e0i ki\u1ec3m tra"] = "exams",
        ["K\u1ebft qu\u1ea3"] = "results"
    };

    foreach ((string pageName, string expectedToolId) in expected)
    {
        object?[] args = { pageName, null };
        bool mapped = (bool)mapper!.Invoke(null, args)!;

        AssertTrue(mapped, $"legacy student page '{pageName}' must map to a course tool");
        AssertEqual(expectedToolId, args[1]?.ToString());
    }

    object?[] unknownArgs = { "Kh\u00f4ng t\u1ed3n t\u1ea1i", null };
    AssertFalse((bool)mapper!.Invoke(null, unknownArgs)!, "unknown page names must not map to a course tool");
});

Run("student dashboard treats notifications as user level navigation", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "StudentDashboard.cs"));
    string navigation = ExtractMethodSource(source, "private void InitializeNavigation()");
    string mapper = ExtractMethodSource(source, "private static bool TryGetLegacyStudentCourseToolId");
    string courseToolDetail = ExtractMethodSource(source, "private void OpenStudentCourseToolDetail");

    AssertTrue(navigation.Contains("NotificationPage") && navigation.Contains("new UC_Notification()"),
        "student notifications must be reachable as a user-level page without selected course context");
    AssertFalse(mapper.Contains("NotificationPage => \"notifications\""),
        "legacy global notification navigation must not be treated as a course-tool route");
    AssertTrue(courseToolDetail.Contains("\"notifications\" => new UC_Notification()"),
        "selected-course notification tool behavior must remain available from the course workspace");
});

Run("student dashboard passes selected course context into course detail pages", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "StudentDashboard.cs"));
    string openDetail = ExtractMethodSource(source, "private void OpenStudentCourseToolDetail");

    string[] expectedCreations =
    {
        "new UC_StudentLessons(context.CourseId)",
        "new UC_Schedule(context.CourseId)",
        "new UC_StudentAssignments(context.CourseId)",
        "new UC_TakeExam(context.CourseId)",
        "new UC_Result(context.CourseId)"
    };

    foreach (string expectedCreation in expectedCreations)
        AssertTrue(openDetail.Contains(expectedCreation), $"OpenStudentCourseToolDetail must pass context.CourseId with {expectedCreation}");

    AssertTrue(openDetail.Contains("\"notifications\" => new UC_Notification()"),
        "student course notifications must preserve the existing user-level notification control");
});

Run("student course-scoped controls keep legacy constructors and expose selected course context", () =>
{
    Type[] courseScopedPageTypes =
    {
        typeof(UC_StudentLessons),
        typeof(UC_StudentAssignments),
        typeof(UC_Result),
        typeof(UC_Schedule),
        typeof(UC_TakeExam)
    };

    foreach (Type pageType in courseScopedPageTypes)
    {
        AssertTrue(pageType.GetConstructor(Type.EmptyTypes) != null,
            $"{pageType.Name} must keep its legacy public parameterless constructor");
        AssertTrue(pageType.GetConstructor(new[] { typeof(int?) }) != null,
            $"{pageType.Name} must expose public constructor (int?) for course workspace routes");
        AssertTrue(pageType.GetProperty("SelectedCourseId")?.PropertyType == typeof(int?),
            $"{pageType.Name} must expose optional SelectedCourseId context");
    }
});

Run("student course-scoped controls filter by selected course before UI filters", () =>
{
    string root = RepoRoot();
    var expected = new Dictionary<string, string>
    {
        ["UC_StudentLessons.cs"] = "i.CourseId == _selectedCourseId.Value",
        ["UC_StudentAssignments.cs"] = "d.CourseId == _selectedCourseId.Value",
        ["UC_Result.cs"] = "_selectedCourseId.Value",
        ["UC_Schedule.cs"] = "s.CourseId == _selectedCourseId.Value",
        ["UC_TakeExam.cs"] = "exam.CourseId == _selectedCourseId.Value"
    };

    foreach ((string fileName, string expectedFilter) in expected)
    {
        string source = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Student", fileName));
        AssertTrue(source.Contains(expectedFilter), $"{fileName} must apply SelectedCourseId as an authoritative course scope");
    }
});

Run("student dashboard course dropdown opens workspace while global course opens legacy course page", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "StudentDashboard.cs"));
    string globalHandler = ExtractMethodSource(source, "private void Sidebar_GlobalNavigationRequested");
    string courseHandler = ExtractMethodSource(source, "private void Sidebar_CourseSelected");

    AssertFalse(globalHandler.Contains("pageName == CourseWorkspacePage"),
        "global Kh\u00f3a h\u1ecdc item must not be intercepted as a course workspace route");
    AssertTrue(courseHandler.Contains("OpenCourseWorkspace(course)"),
        "course dropdown selection must still open the selected course workspace");
    AssertTrue(source.Contains("{ CourseGlobalPage, () => new UC_CourseList(currentUser.Id, _courseController) }"),
        "student global Kh\u00f3a h\u1ecdc item must open the real legacy course page");
});

Run("student dashboard adapts legacy routes instead of returning on missing global nav", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "StudentDashboard.cs"));
    string navigate = ExtractMethodSource(source, "private void NavigateToPage");
    string adapter = ExtractMethodSource(source, "private bool TryNavigateLegacyStudentRoute");
    string topbarSelection = ExtractMethodSource(source, "private void Topbar_SearchResultSelected");

    AssertTrue(navigate.Contains("TryNavigateLegacyStudentRoute(pageName)"),
        "NavigateToPage must hand missing global routes to the legacy student route adapter");
    AssertFalse(navigate.Contains("if (!_nav.TryGetValue(pageName, out var factory))\r\n                return;"),
        "NavigateToPage must not silently return immediately when a legacy route is missing from _nav");
    AssertTrue(adapter.Contains("_isForceChangePasswordActive"),
        "legacy adapter must preserve the forced-password guard");
    AssertTrue(adapter.Contains("OpenSelectedOrFirstCourseTool(toolId)"),
        "legacy course-scoped pages must open the selected or first course tool");
    AssertTrue(adapter.Contains("NavigateToPage(FindCoursePage)"),
        "legacy my-courses route must fall back to course search when no enrolled course is available");
    AssertFalse(topbarSelection.Contains("!_nav.ContainsKey(pageName)"),
        "topbar search selection must not reject supported legacy route names before NavigateToPage can adapt them");
});

Run("plan 04 legacy SHA-256 hash is verified and detected for rehash", () =>
{
    // Simulate legacy SHA-256 hash (pre-PBKDF2 migration)
    string password = "TestPassword123";
    using var sha256 = System.Security.Cryptography.SHA256.Create();
    byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
    string legacyHash = string.Concat(bytes.Select(b => b.ToString("x2")));

    // Legacy hash must still verify correctly (backward compat)
    AssertTrue(PasswordHasher.VerifyPassword(password, legacyHash),
        "PasswordHasher must accept legacy SHA-256 hashes");
    AssertFalse(PasswordHasher.VerifyPassword("wrong", legacyHash),
        "PasswordHasher must reject wrong password against legacy hash");

    // Legacy hash must NOT start with PBKDF2$ prefix → rehash needed
    AssertFalse(legacyHash.StartsWith("PBKDF2$", StringComparison.Ordinal),
        "legacy SHA-256 hash must not carry PBKDF2 prefix");

    // After rehash, new hash must be PBKDF2
    string rehashed = PasswordHasher.HashPassword(password);
    AssertTrue(rehashed.StartsWith("PBKDF2$", StringComparison.Ordinal),
        "rehashed password must use PBKDF2 format");
    AssertTrue(PasswordHasher.VerifyPassword(password, rehashed),
        "rehashed PBKDF2 hash must verify the same password");
});

Run("plan 04 PBKDF2 hash round-trips and rejects wrong password", () =>
{
    string password = "Secure#Pass99";
    string hash = PasswordHasher.HashPassword(password);

    AssertTrue(hash.StartsWith("PBKDF2$", StringComparison.Ordinal),
        "HashPassword must produce PBKDF2 format");
    string[] parts = hash.Split('$');
    AssertEqual(4, parts.Length);
    AssertEqual("120000", parts[1]);

    AssertTrue(PasswordHasher.VerifyPassword(password, hash),
        "correct password must verify");
    AssertFalse(PasswordHasher.VerifyPassword("WrongPassword", hash),
        "wrong password must not verify");
    AssertFalse(PasswordHasher.VerifyPassword("", hash),
        "empty password must not verify");
    AssertFalse(PasswordHasher.VerifyPassword(password, ""),
        "empty hash must not verify");
});

Run("plan 04 expired temp password returns TEMP_PASSWORD_EXPIRED error", () =>
{
    var user = new UserModel
    {
        Id = 999,
        Username = "test_expired",
        PasswordHash = PasswordHasher.HashPassword("temp123"),
        FullName = "Test User",
        Email = "test@example.com",
        Role = "STUDENT",
        Status = "ACTIVE",
        TempPasswordExpiresAt = DateTime.Now.AddHours(-1) // expired 1 hour ago
    };

    // Use the pure helper to test logic directly without DB dependencies
    LoginResultModel expiredResult = LoginResultModel.Evaluate(user);

    AssertFalse(expiredResult.Succeeded, "expired temp password must not succeed");
    AssertEqual(LoginErrorCodes.TempPasswordExpired, expiredResult.ErrorCode);
    AssertEqual(null, expiredResult.User);
});

Run("plan 04 valid temp password sets MustChangePassword flag", () =>
{
    var user = new UserModel
    {
        Id = 888,
        Username = "test_temp",
        FullName = "Temp User",
        Role = "STUDENT",
        Status = "ACTIVE",
        TempPasswordExpiresAt = DateTime.Now.AddHours(23) // still valid
    };

    // Evaluate the pure logic
    LoginResultModel result = LoginResultModel.Evaluate(user);

    AssertTrue(result.Succeeded, "valid temp password login must succeed");
    AssertTrue(result.MustChangePassword, "valid temp password must force password change");
    AssertEqual(user.Id, result.User!.Id);
    AssertEqual(string.Empty, result.ErrorCode);
});

Run("plan 04 normal login has no MustChangePassword flag", () =>
{
    var user = new UserModel
    {
        Id = 777,
        Username = "normal_user",
        FullName = "Normal User",
        Role = "STUDENT",
        Status = "ACTIVE",
        TempPasswordExpiresAt = null // no temp password
    };

    // Evaluate the pure logic
    LoginResultModel result = LoginResultModel.Evaluate(user);

    AssertTrue(result.Succeeded, "normal login must succeed");
    AssertFalse(result.MustChangePassword, "normal login must not force password change");
    AssertEqual(user.Id, result.User!.Id);
});

Run("plan 04 LoginResultModel.Failed returns non-succeeded result", () =>
{
    LoginResultModel failed = LoginResultModel.Failed();
    AssertFalse(failed.Succeeded, "failed login must not succeed");
    AssertEqual(null, failed.User);
    AssertFalse(failed.MustChangePassword, "failed login must not set MustChangePassword");
});

Run("plan 04 ChangePassword clears temp_password_expires_at via default parameter", () =>
{
    // The UpdateUserPassword signature: (int userId, string passwordHash, DateTime? tempPasswordExpiresAt = null)
    // When ChangePassword calls _dbContext.UpdateUserPassword(userId, newPasswordHash) with 2 args,
    // the default null for tempPasswordExpiresAt means it clears the expiry column.
    MethodInfo? updateMethod = typeof(CourseGuardDbContext).GetMethod(
        "UpdateUserPassword",
        new[] { typeof(int), typeof(string), typeof(DateTime?) });
    AssertTrue(updateMethod != null, "UpdateUserPassword must accept 3 params (int, string, DateTime?)");

    ParameterInfo[] parameters = updateMethod!.GetParameters();
    AssertEqual(3, parameters.Length);
    AssertTrue(parameters[2].HasDefaultValue, "tempPasswordExpiresAt parameter must have a default value");
    AssertEqual(null, parameters[2].DefaultValue);

    // Verify ChangePassword source calls UpdateUserPassword with only 2 args (relying on default null)
    string root = RepoRoot();
    string authSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Backend", "Controllers", "AuthController.cs"));
    string changePasswordBody = ExtractMethodSource(authSource, "public bool ChangePassword(");
    AssertTrue(
        changePasswordBody.Contains("UpdateUserPassword(userId, newPasswordHash)"),
        "ChangePassword must call UpdateUserPassword with 2 args so default null clears temp expiry");
    AssertFalse(
        changePasswordBody.Contains("UpdateUserPassword(userId, newPasswordHash, "),
        "ChangePassword must not pass an explicit third argument");
});

Run("plan 04 RehashLegacyPasswordIfNeeded preserves temp expiry during rehash", () =>
{
    // When rehashing, the temp_password_expires_at must be carried over (not cleared)
    string root = RepoRoot();
    string authSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Backend", "Controllers", "AuthController.cs"));
    string rehashBody = ExtractMethodSource(authSource, "private void RehashLegacyPasswordIfNeeded(");

    AssertTrue(
        rehashBody.Contains("user.TempPasswordExpiresAt"),
        "RehashLegacyPasswordIfNeeded must preserve TempPasswordExpiresAt during hash upgrade");
    AssertTrue(
        rehashBody.Contains("UpdateUserPassword(user.Id, newHash, user.TempPasswordExpiresAt)"),
        "RehashLegacyPasswordIfNeeded must pass the existing expiry to UpdateUserPassword");
});

Run("plan 04 UserController sets temp expiry when approving password reset", () =>
{
    string root = RepoRoot();
    string userCtrlSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Backend", "Controllers", "UserController.cs"));

    // The RESET branch must set a tempPasswordExpiresAt
    AssertTrue(
        userCtrlSource.Contains("DateTime.Now.AddHours(24)"),
        "UserController RESET action must set 24-hour temp password expiry");
    AssertTrue(
        userCtrlSource.Contains("UpdateUserPassword(userId, passwordHash, tempPasswordExpiresAt)"),
        "UserController RESET action must pass expiry to UpdateUserPassword");
});

Run("plan 04 LoginPage handles TempPasswordExpired error code", () =>
{
    string root = RepoRoot();
    string loginPageSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Login", "LoginPage.cs"));

    AssertTrue(
        loginPageSource.Contains("LoginErrorCodes.TempPasswordExpired"),
        "LoginPage must check for TempPasswordExpired error code");
    AssertTrue(
        loginPageSource.Contains("LoginResultModel loginResult"),
        "LoginPage must use LoginResultModel for login results");
    AssertTrue(
        loginPageSource.Contains("loginResult.MustChangePassword"),
        "LoginPage must check MustChangePassword flag");
});

// ── end plan 04 behavioral security tests ────────────────────────────

Run("student exam launch preloads session before opening form", () =>
{
    Type[] preloadedConstructorTypes = { typeof(int), typeof(StudentExamTakingModel) };
    AssertTrue(
        typeof(DoExamForm).GetConstructor(preloadedConstructorTypes) != null,
        "DoExamForm must expose public constructor (int, StudentExamTakingModel)");

    Type[] scopedSessionStatusTypes = { typeof(int), typeof(int), typeof(bool), typeof(string) };
    AssertTrue(
        typeof(TeacherController).GetMethod("UpdateSessionStatusAsync", scopedSessionStatusTypes) != null,
        "TeacherController session status updates must be scoped by teacherId");
    Type[] unscopedSessionStatusTypes = { typeof(int), typeof(bool), typeof(string) };
    AssertTrue(
        typeof(TeacherController).GetMethod("UpdateSessionStatusAsync", unscopedSessionStatusTypes) == null,
        "TeacherController must not expose unscoped session status updates");

    MethodInfo? preloadMethod = typeof(UC_TakeExam).GetMethod(
        "PreloadExamSessionAsync",
        BindingFlags.Instance | BindingFlags.NonPublic);
    AssertTrue(preloadMethod != null, "UC_TakeExam must preload the exam session before opening DoExamForm");
    AssertEqual(typeof(Task<StudentExamTakingModel?>), preloadMethod!.ReturnType);
});

Run("student exam launch context snapshots selected exam before preload", () =>
{
    MethodInfo? createContext = typeof(UC_TakeExam).GetMethod(
        "CreateExamLaunchContext",
        BindingFlags.Static | BindingFlags.NonPublic);
    AssertTrue(createContext != null, "UC_TakeExam must snapshot selected exam context before await");

    var exam = new StudentExamListItemModel
    {
        Id = 7,
        Title = "Giữa kỳ"
    };

    object context = createContext!.Invoke(null, new object?[] { exam })
        ?? throw new InvalidOperationException("launch context must not be null for a valid exam");
    exam.Title = "Đã đổi";

    AssertEqual(7, GetPropertyValue<int>(context, "ExamId"));
    AssertEqual("Giữa kỳ", GetPropertyValue<string>(context, "ExamName"));
    AssertTrue(
        context.GetType().GetProperty("CanStart", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) == null,
        "launch context must not snapshot stale CanStart");
});

Run("student exam launch reuses selected exam helper and fresh presenter state", () =>
{
    const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    MethodInfo selectedExam = typeof(UC_TakeExam).GetMethod("SelectedExam", privateInstance)
        ?? throw new InvalidOperationException("UC_TakeExam must expose SelectedExam helper");
    MethodInfo selectedExamRow = typeof(UC_TakeExam).GetMethod("SelectedExamRow", privateInstance)
        ?? throw new InvalidOperationException("UC_TakeExam must expose SelectedExamRow helper");
    MethodInfo click = typeof(UC_TakeExam).GetMethod("btnStartExam_Click", privateInstance)
        ?? throw new InvalidOperationException("UC_TakeExam must expose start click handler");
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Student", "UC_TakeExam.cs"));

    AssertTrue(MethodCalls(selectedExam, selectedExamRow), "SelectedExam must resolve rows through SelectedExamRow");
    AssertTrue(source.Contains("StudentExamListItemModel? exam = SelectedExam();"), "start click must use the same selected exam helper as preview");
    AssertTrue(source.Contains("StudentExamUxPresenter.Present(exam, DateTime.Now)"), "start click must recompute launchability through the presenter");
    AssertFalse(source.Contains("CreateExamLaunchContext(dgvExams.CurrentRow)"), "start click must not launch from CurrentRow only");
    AssertFalse(source.Contains("!launchContext.CanStart"), "start click must not trust stale hidden CanStart");
});

Run("screen monitor connection loss tracker reports after threshold once", () =>
{
    Type trackerType = typeof(StudentScreenStreamClient).Assembly.GetType("CourseGuard.Backend.Services.Monitoring.ScreenMonitorConnectionLossTracker")
        ?? throw new InvalidOperationException("ScreenMonitorConnectionLossTracker must exist");
    FieldInfo violationType = trackerType.GetField("ViolationType", BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException("ScreenMonitorConnectionLossTracker must expose ViolationType");
    AssertEqual("CONNECTION_LOST", violationType.GetValue(null)?.ToString());

    DateTimeOffset now = new(2026, 6, 5, 0, 0, 0, TimeSpan.Zero);
    Func<DateTimeOffset> clock = () => now;
    object tracker = Activator.CreateInstance(trackerType, TimeSpan.FromSeconds(30), clock)
        ?? throw new InvalidOperationException("Cannot create connection loss tracker");
    MethodInfo observeDisconnected = trackerType.GetMethod("ObserveDisconnected")
        ?? throw new InvalidOperationException("Tracker must expose ObserveDisconnected");
    MethodInfo observeConnected = trackerType.GetMethod("ObserveConnected")
        ?? throw new InvalidOperationException("Tracker must expose ObserveConnected");

    observeConnected.Invoke(tracker, Array.Empty<object>());
    AssertFalse((bool)observeDisconnected.Invoke(tracker, Array.Empty<object>())!, "initial disconnect should start the timer only");
    now = now.AddSeconds(29);
    AssertFalse((bool)observeDisconnected.Invoke(tracker, Array.Empty<object>())!, "disconnect below threshold must not report");
    now = now.AddSeconds(1);
    AssertTrue((bool)observeDisconnected.Invoke(tracker, Array.Empty<object>())!, "disconnect at threshold must report");
    now = now.AddSeconds(10);
    AssertFalse((bool)observeDisconnected.Invoke(tracker, Array.Empty<object>())!, "same outage must report once");

    observeConnected.Invoke(tracker, Array.Empty<object>());
    AssertFalse((bool)observeDisconnected.Invoke(tracker, Array.Empty<object>())!, "reconnect should reset the outage window");
});

Run("screen monitor connection loss tracker ignores initial unavailable monitor", () =>
{
    Type trackerType = typeof(StudentScreenStreamClient).Assembly.GetType("CourseGuard.Backend.Services.Monitoring.ScreenMonitorConnectionLossTracker")
        ?? throw new InvalidOperationException("ScreenMonitorConnectionLossTracker must exist");
    DateTimeOffset now = new(2026, 6, 5, 0, 0, 0, TimeSpan.Zero);
    object tracker = Activator.CreateInstance(trackerType, TimeSpan.FromSeconds(30), () => now)
        ?? throw new InvalidOperationException("Cannot create connection loss tracker");
    MethodInfo observeDisconnected = trackerType.GetMethod("ObserveDisconnected")
        ?? throw new InvalidOperationException("Tracker must expose ObserveDisconnected");
    MethodInfo observeConnected = trackerType.GetMethod("ObserveConnected")
        ?? throw new InvalidOperationException("Tracker must expose ObserveConnected");

    AssertFalse((bool)observeDisconnected.Invoke(tracker, Array.Empty<object>())!, "initial unavailable monitor should not start a violation window");
    now = now.AddSeconds(60);
    AssertFalse((bool)observeDisconnected.Invoke(tracker, Array.Empty<object>())!, "initial unavailable monitor should not be reported as connection lost");

    observeConnected.Invoke(tracker, Array.Empty<object>());
    AssertFalse((bool)observeDisconnected.Invoke(tracker, Array.Empty<object>())!, "disconnect after a connection should start the violation window");
    now = now.AddSeconds(30);
    AssertTrue((bool)observeDisconnected.Invoke(tracker, Array.Empty<object>())!, "disconnect after established connection should report at threshold");
});

Run("student screen stream client exposes connection loss notification", () =>
{
    Type eventArgsType = typeof(StudentScreenStreamClient).Assembly.GetType("CourseGuard.Backend.Services.Monitoring.ScreenMonitorConnectionLostEventArgs")
        ?? throw new InvalidOperationException("ScreenMonitorConnectionLostEventArgs must exist");
    EventInfo connectionLostEvent = typeof(StudentScreenStreamClient).GetEvent("ConnectionLostThresholdReached")
        ?? throw new InvalidOperationException("StudentScreenStreamClient must expose ConnectionLostThresholdReached");

    AssertEqual(typeof(EventHandler<>).MakeGenericType(eventArgsType), connectionLostEvent.EventHandlerType);
    AssertTrue(eventArgsType.GetProperty("ExamId") != null, "connection lost args must include ExamId");
    AssertTrue(eventArgsType.GetProperty("StudentId") != null, "connection lost args must include StudentId");
    AssertTrue(eventArgsType.GetProperty("AttemptId") != null, "connection lost args must include AttemptId");
    AssertTrue(eventArgsType.GetProperty("DisconnectedFor") != null, "connection lost args must include DisconnectedFor");
});

Run("student exam form handles monitor connection lost violation", () =>
{
    MethodInfo? handler = typeof(DoExamForm).GetMethod("HandleMonitoringConnectionLost", BindingFlags.Instance | BindingFlags.NonPublic);
    AssertTrue(handler != null, "DoExamForm must handle monitor connection lost events");

    MethodInfo? recorder = typeof(DoExamForm).GetMethod("RecordConnectionLostViolationAsync", BindingFlags.Instance | BindingFlags.NonPublic);
    AssertTrue(recorder != null, "DoExamForm must record connection lost violations");
    AssertEqual(typeof(Task), recorder!.ReturnType);
});

Run("plan 03 models expose metadata and violation threshold fields", () =>
{
    AssertProperty(typeof(TeacherExamQuestionModel), "Difficulty");
    AssertProperty(typeof(TeacherExamQuestionModel), "Chapter");
    AssertProperty(typeof(TeacherExamQuestionModel), "QuestionType");
    AssertProperty(typeof(ExcelQuestionRowModel), "Difficulty");
    AssertProperty(typeof(ExcelQuestionRowModel), "Chapter");
    string excelModelSource = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Models", "ExcelQuestionRowModel.cs"));
    AssertTrue(excelModelSource.Contains("Độ khó") && excelModelSource.Contains("Chương"), "Excel metadata columns must use Vietnamese template headers");
    AssertProperty(typeof(TeacherExamModel), "MaxViolations");
    AssertProperty(typeof(StudentExamTakingModel), "MaxViolations");
    AssertProperty(typeof(ViolationModel), "Severity");
    AssertProperty(typeof(ViolationModel), "ActionTaken");

    Type criteriaType = typeof(TeacherExamQuestionModel).Assembly.GetType("CourseGuard.Backend.Models.RandomQuestionCriteria")
        ?? throw new InvalidOperationException("RandomQuestionCriteria must exist");
    AssertProperty(criteriaType, "Difficulty");
    AssertProperty(criteriaType, "Chapter");
    AssertProperty(criteriaType, "Count");
});

Run("plan 03 normalizers expose expected question metadata and severity behavior", () =>
{
    Type metadataNormalizer = typeof(TeacherExamQuestionModel).Assembly.GetType("CourseGuard.Backend.Services.QuestionMetadataNormalizer")
        ?? throw new InvalidOperationException("QuestionMetadataNormalizer must exist");
    MethodInfo normalizeDifficulty = metadataNormalizer.GetMethod("NormalizeDifficulty", BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException("QuestionMetadataNormalizer must expose NormalizeDifficulty");
    MethodInfo normalizeQuestionType = metadataNormalizer.GetMethod("NormalizeQuestionType", BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException("QuestionMetadataNormalizer must expose NormalizeQuestionType");
    MethodInfo normalizeChapter = metadataNormalizer.GetMethod("NormalizeChapter", BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException("QuestionMetadataNormalizer must expose NormalizeChapter");

    AssertEqual("EASY", normalizeDifficulty.Invoke(null, new object?[] { "easy" })?.ToString());
    AssertEqual("MEDIUM", normalizeDifficulty.Invoke(null, new object?[] { "invalid" })?.ToString());
    AssertEqual("MULTIPLE_CHOICE", normalizeQuestionType.Invoke(null, new object?[] { "" })?.ToString());
    AssertEqual(null, normalizeChapter.Invoke(null, new object?[] { "   " }));

    Type severityMap = typeof(StudentScreenStreamClient).Assembly.GetType("CourseGuard.Backend.Services.Monitoring.ViolationSeverityMap")
        ?? throw new InvalidOperationException("ViolationSeverityMap must exist");
    MethodInfo getSeverity = severityMap.GetMethod("Get", BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException("ViolationSeverityMap must expose Get");

    AssertEqual("HIGH", getSeverity.Invoke(null, new object?[] { "CONNECTION_LOST" })?.ToString());
    AssertEqual("LOW", getSeverity.Invoke(null, new object?[] { "KEY_PRESS" })?.ToString());
    AssertEqual("MEDIUM", getSeverity.Invoke(null, new object?[] { "Mat Focus / Chuyen Tab" })?.ToString());
    AssertEqual("MEDIUM", getSeverity.Invoke(null, new object?[] { "UNKNOWN" })?.ToString());
});

Run("plan 03 source wiring matches question bank and violation threshold architecture", () =>
{
    string repoRoot = RepoRoot();
    string db = File.ReadAllText(Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Backend", "Data", "CourseGuardDbContext.cs"));
    string teacherRepo = File.ReadAllText(Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Backend", "Data", "TeacherRepository.cs"));
    string violationRepo = File.ReadAllText(Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Backend", "Data", "ViolationRepository.cs"));
    string teacherController = File.ReadAllText(Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Backend", "Controllers", "TeacherController.cs"));
    string doExam = File.ReadAllText(Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "DoExamForm.cs"));
    string questionDialog = File.ReadAllText(Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherExamQuestionsDialog.cs"));
    string randomDialog = File.ReadAllText(Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "RandomExamBuilderDialog.cs"));

    AssertTrue(db.Contains("EnsureQuestionBankMetadataSchema"), "db context must guard question bank metadata schema");
    AssertTrue(db.Contains("ALTER TABLE questions"), "metadata must be added to the questions bank table");
    AssertTrue(db.Contains("ALTER TABLE exam_questions"), "metadata must also be added to exam question snapshots");
    AssertTrue(db.Contains("max_violations"), "db context must persist exam violation threshold");
    AssertTrue(violationRepo.Contains("COUNT(*)") && violationRepo.Contains("FROM violations"), "auto-submit must use persisted violation count");
    AssertTrue(db.Contains("GetRandomQuestionsByCriteria"), "db context must expose random question selection");
    AssertTrue(db.Contains("questions WHERE course_id = @course_id"), "random question selection must use questions.course_id");
    AssertFalse(db.Contains("exam_questions\n            WHERE course_id"), "random bank selection must not query exam_questions.course_id");
    AssertTrue(db.Contains("COALESCE(MAX(display_order), 0)") && db.Contains("NOT EXISTS") && db.Contains("existing.exam_id = @exam_id") && db.Contains("existing.question_id = q.id"), "bank/random snapshots must append display order and skip duplicate bank questions");
    AssertFalse(db.Contains("row_number() over (order by id),"), "bank/random snapshots must not reset display order to 1 for every insert");
    AssertTrue(teacherRepo.Contains("max_violations"), "teacher repository must read and write max violations");
    AssertTrue(teacherController.Contains("AddRandomQuestionsToExamAsync"), "teacher controller must expose random question add flow");
    AssertTrue(randomDialog.Contains("_previewQuestions.Select(q => q.Id)") && randomDialog.Contains("AddQuestionsFromBankAsync(_teacherId, _examId, _courseId"), "random builder must add the exact previewed question ids");
    AssertTrue(doExam.Contains("HandleViolationAsync"), "student exam form must centralize violation handling");
    AssertTrue(doExam.Contains("EvaluateViolationThresholdAsync"), "student exam form must evaluate violation threshold");
    AssertTrue(doExam.Contains("SubmitExam(confirm: false)"), "threshold breach must submit without confirmation");
    AssertTrue(questionDialog.Contains("RandomExamBuilderDialog"), "question editor must open random exam builder");
});

Run("chat send status formatter labels pending sent and failed sends", () =>
{
    Type formatterType = typeof(UC_Chat).Assembly.GetType("CourseGuard.Frontend.Helpers.ChatSendStatusLineFormatter")
        ?? throw new InvalidOperationException("ChatSendStatusLineFormatter must exist");
    MethodInfo render = formatterType.GetMethod("Render", BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException("ChatSendStatusLineFormatter must expose Render");

    AssertEqual(
        "[Đang gửi] Bạn: Xin chào",
        render.Invoke(null, new object?[] { "Bạn", "Xin chào", "TEXT", "PENDING", null })?.ToString());
    AssertEqual(
        "[Đã gửi] Bạn: [FILE] bai.pdf",
        render.Invoke(null, new object?[] { "Bạn", "bai.pdf", "FILE", "SENT", null })?.ToString());
    AssertEqual(
        "[Lỗi] Bạn: Xin chào - Mất mạng",
        render.Invoke(null, new object?[] { "Bạn", "Xin chào", "TEXT", "FAILED", "Mất mạng" })?.ToString());
});

Run("student and teacher chat pages can append local send statuses", () =>
{
    AssertTrue(
        typeof(UC_Chat).GetMethod("AppendLocalSendStatus", BindingFlags.Instance | BindingFlags.NonPublic) != null,
        "student chat page must append local send statuses");
    AssertTrue(
        typeof(UC_TeacherMessages).GetMethod("AppendLocalSendStatus", BindingFlags.Instance | BindingFlags.NonPublic) != null,
        "teacher chat page must append local send statuses");
});

Run("student and teacher profile pages share profile page base", () =>
{
    AssertTrue(typeof(ProfilePageBase).IsAssignableFrom(typeof(UC_Profile)), "student profile page must inherit ProfilePageBase");
    AssertTrue(typeof(ProfilePageBase).IsAssignableFrom(typeof(UC_TeacherProfile)), "teacher profile page must inherit ProfilePageBase");

    string[] helperNames =
    {
        "CreateInputGroup",
        "CreateMultilineInputGroup",
        "CreateComboGroup",
        "CreateFieldWrapper",
        "WireInputFocus"
    };

    foreach (string helperName in helperNames)
    {
        AssertTrue(
            typeof(ProfilePageBase).GetMember(helperName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Length > 0,
            $"ProfilePageBase must expose {helperName}");
    }

    TestProfilePage profileBase = new();

    TextBox passwordInput = new() { Tag = "student-input" };
    Panel passwordWrapper = AssertControl<Panel>(
        profileBase.BuildInput("Password", passwordInput, password: true, inputWidth: 320),
        "password input helper should return a wrapper panel");
    RoundedPanel passwordPanel = SingleChild<RoundedPanel>(passwordWrapper);

    AssertEqual('*', passwordInput.PasswordChar);
    AssertFalse(passwordInput.Multiline, "single-line input must remain single-line");
    AssertEqual(BorderStyle.None, passwordInput.BorderStyle);
    AssertEqual(DockStyle.Fill, passwordInput.Dock);
    AssertEqual(Padding.Empty, passwordInput.Margin);
    AssertEqual<object?>(null, passwordInput.Tag);
    AssertEqual(new Size(320, 42), passwordPanel.Size);
    AssertEqual(new Size(240, 42), passwordPanel.MinimumSize);
    AssertEqual(new Padding(12, 9, 12, 9), passwordPanel.Padding);
    AssertEqual(MetaTheme.Colors.BorderSoft, passwordPanel.BorderColor);

    RaiseFocusChanged(passwordInput, focused: true);
    AssertEqual(MetaTheme.Colors.BorderFocus, passwordPanel.BorderColor);
    RaiseFocusChanged(passwordInput, focused: false);
    AssertEqual(MetaTheme.Colors.BorderSoft, passwordPanel.BorderColor);

    TextBox teacherInput = new() { Tag = "teacher-input" };
    profileBase.BuildInput("Teacher", teacherInput, clearInputTag: false);
    AssertEqual("teacher-input", teacherInput.Tag);

    TextBox studentMultiline = new() { Tag = "student-bio", Margin = new Padding(7) };
    Panel studentMultilineWrapper = AssertControl<Panel>(
        profileBase.BuildMultiline("Bio", studentMultiline),
        "student multiline helper should return a wrapper panel");
    RoundedPanel studentMultilinePanel = SingleChild<RoundedPanel>(studentMultilineWrapper);

    AssertTrue(studentMultiline.Multiline, "multiline input should enable multiline mode");
    AssertEqual(ScrollBars.Vertical, studentMultiline.ScrollBars);
    AssertEqual(DockStyle.Fill, studentMultiline.Dock);
    AssertEqual(new Padding(7), studentMultiline.Margin);
    AssertEqual<object?>(null, studentMultiline.Tag);
    AssertEqual(new Size(280, 86), studentMultilinePanel.Size);
    AssertEqual(AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, studentMultilinePanel.Anchor);

    TextBox teacherMultiline = new() { Tag = "teacher-bio", Margin = new Padding(7) };
    profileBase.BuildMultiline("Teacher bio", teacherMultiline, clearTextBoxMargin: true, clearInputTag: false);
    AssertEqual(Padding.Empty, teacherMultiline.Margin);
    AssertEqual("teacher-bio", teacherMultiline.Tag);

    ComboBox comboBox = new() { Tag = "gender" };
    Panel comboWrapper = AssertControl<Panel>(
        profileBase.BuildCombo("Gender", comboBox),
        "combo helper should return a wrapper panel");

    AssertEqual<object?>(null, comboBox.Tag);
    AssertEqual(new Point(0, 25), comboBox.Location);
    AssertEqual(280, comboBox.Width);
    AssertTrue(comboBox.Height >= 36, "styled combo height should preserve at least the theme minimum");
    AssertEqual(240, comboBox.MinimumSize.Width);
    AssertEqual(AnchorStyles.Left | AnchorStyles.Top, comboBox.Anchor);
    AssertEqual(FlatStyle.Flat, comboBox.FlatStyle);
    AssertEqual(DrawMode.OwnerDrawFixed, comboBox.DrawMode);
    AssertEqual(ComboBoxStyle.DropDownList, comboBox.DropDownStyle);
    AssertFalse(comboBox.IntegralHeight, "styled combo should disable integral height");
    AssertEqual(34, comboBox.ItemHeight);
    AssertEqual(1, comboBox.DropDownHeight);
    AssertTrue(comboWrapper.Controls.Contains(comboBox), "combo wrapper should contain the styled combo");
});

Run("profile inline validation helper returns field level messages", () =>
{
    Type helperType = typeof(UC_Profile).Assembly.GetType("CourseGuard.Frontend.Helpers.ProfileInlineValidationHelper")
        ?? throw new InvalidOperationException("ProfileInlineValidationHelper must exist");
    MethodInfo validateFullName = helperType.GetMethod("ValidateFullName", BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException("ProfileInlineValidationHelper must expose ValidateFullName");
    MethodInfo validateEmail = helperType.GetMethod("ValidateEmail", BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException("ProfileInlineValidationHelper must expose ValidateEmail");
    MethodInfo validatePhone = helperType.GetMethod("ValidatePhone", BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException("ProfileInlineValidationHelper must expose ValidatePhone");
    MethodInfo validateBirthDate = helperType.GetMethod("ValidateBirthDate", BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException("ProfileInlineValidationHelper must expose ValidateBirthDate");

    AssertEqual("Họ và tên không được để trống.", validateFullName.Invoke(null, new object?[] { "   " })?.ToString());
    AssertEqual(string.Empty, validateFullName.Invoke(null, new object?[] { "Nguyễn Văn A" })?.ToString());

    AssertEqual("Email không hợp lệ.", validateEmail.Invoke(null, new object?[] { string.Empty, true })?.ToString());
    AssertEqual("Email không hợp lệ.", validateEmail.Invoke(null, new object?[] { "bad-email", true })?.ToString());
    AssertEqual(string.Empty, validateEmail.Invoke(null, new object?[] { string.Empty, false })?.ToString());
    AssertEqual(string.Empty, validateEmail.Invoke(null, new object?[] { "student@example.com", false })?.ToString());

    AssertEqual("Số điện thoại không hợp lệ.", validatePhone.Invoke(null, new object?[] { "abc123" })?.ToString());
    AssertEqual("Số điện thoại không hợp lệ.", validatePhone.Invoke(null, new object?[] { "٠٩٠١٢٣٤٥٦" })?.ToString());
    AssertEqual(string.Empty, validatePhone.Invoke(null, new object?[] { "0901 234 567" })?.ToString());

    AssertEqual("Ngày sinh cần có định dạng dd/MM/yyyy.", validateBirthDate.Invoke(null, new object?[] { "2026-06-05" })?.ToString());
    AssertEqual(string.Empty, validateBirthDate.Invoke(null, new object?[] { "05/06/2026" })?.ToString());
});

Run("student and teacher profile pages wire inline validation", () =>
{
    AssertTrue(
        typeof(UC_Profile).GetField("_validationErrors", BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType == typeof(ErrorProvider),
        "student profile page must own an ErrorProvider for inline validation");
    AssertTrue(
        typeof(UC_TeacherProfile).GetField("_validationErrors", BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType == typeof(ErrorProvider),
        "teacher profile page must own an ErrorProvider for inline validation");

    string[] inlineMethods =
    {
        "WireInlineValidation",
        "ValidateFullNameInline",
        "ValidateEmailInline",
        "ValidatePhoneInline",
        "ValidateBirthDateInline"
    };

    foreach (string methodName in inlineMethods)
    {
        AssertTrue(
            typeof(UC_Profile).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic) != null,
            $"student profile page must expose {methodName}");
        AssertTrue(
            typeof(UC_TeacherProfile).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic) != null,
            $"teacher profile page must expose {methodName}");
    }
});

Run("profile avatar saves use validated save path", () =>
{
    AssertProfileUsesValidatedSave(typeof(UC_Profile), "student");
    AssertProfileUsesValidatedSave(typeof(UC_TeacherProfile), "teacher");
});

Run("student profile validated save blocks invalid input behavior", () =>
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            UserSessionContext.Clear();
            using ContainerControl validationContainer = new();
            using ErrorProvider errors = new()
            {
                BlinkStyle = ErrorBlinkStyle.NeverBlink,
                ContainerControl = validationContainer
            };
            UC_Profile page = (UC_Profile)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(UC_Profile));

            TextBox fullName = new();
            TextBox email = new();
            TextBox phone = new();
            TextBox birthDate = new();
            SetStudentProfileTestFields(page, fullName, email, phone, birthDate, errors);
            MethodInfo save = typeof(UC_Profile).GetMethod("SaveProfileIfValid", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("student profile must expose SaveProfileIfValid");

            fullName.Text = "Nguyen Van A";
            email.Text = "bad-email";
            phone.Text = "0901234567";
            birthDate.Text = "05/06/2026";

            bool result = InvokeProfileSaveWithDialogClose(page, save);
            AssertFalse(result, "invalid email must block student profile save");
            AssertEqual("Email không hợp lệ.", errors.GetError(email));

            email.Text = "student@example.com";
            phone.Text = "abc123";
            result = InvokeProfileSaveWithDialogClose(page, save);
            AssertFalse(result, "invalid phone must block student profile save");
            AssertEqual("Số điện thoại không hợp lệ.", errors.GetError(phone));

            phone.Text = "0901234567";
            birthDate.Text = "2026-06-05";
            result = InvokeProfileSaveWithDialogClose(page, save);
            AssertFalse(result, "invalid birth date must block student profile save");
            AssertEqual("Ngày sinh cần có định dạng dd/MM/yyyy.", errors.GetError(birthDate));

            birthDate.Text = "05/06/2026";
            fullName.Text = " ";
            result = InvokeProfileSaveWithDialogClose(page, save);
            AssertFalse(result, "blank full name must block student profile save");
            AssertEqual("Họ và tên không được để trống.", errors.GetError(fullName));

            fullName.Text = "Nguyen Van A";
            result = (bool)save.Invoke(page, Array.Empty<object>())!;
            AssertTrue(result, "valid student profile fields should allow save when no DB user is active");
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            UserSessionContext.Clear();
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw failure;
});

Run("material file policy accepts common documents and rejects unsafe files", () =>
{
    MaterialFileValidation valid = MaterialFilePolicy.Validate("lesson.docx", 1024);
    MaterialFileValidation badExtension = MaterialFilePolicy.Validate("tool.exe", 1024);
    MaterialFileValidation tooLarge = MaterialFilePolicy.Validate("deck.pdf", 21L * 1024 * 1024);

    AssertTrue(valid.IsValid, valid.ErrorMessage);
    AssertFalse(badExtension.IsValid, "exe must be rejected");
    AssertFalse(tooLarge.IsValid, "files above 20MB must be rejected");
});

Run("avatar manager cover rectangle preserves aspect ratio and fills target", () =>
{
    Rectangle wideCover = AvatarManager.GetCoverRectangle(new Size(200, 100), new Rectangle(10, 20, 80, 80));
    Rectangle tallCover = AvatarManager.GetCoverRectangle(new Size(100, 200), new Rectangle(0, 0, 80, 40));

    AssertEqual(new Rectangle(-30, 20, 160, 80), wideCover);
    AssertEqual(new Rectangle(0, -60, 80, 160), tallCover);
});

Run("avatar manager initials handles blank users and multi word names", () =>
{
    AssertEqual("U", AvatarManager.GetInitials(null, "   "));
    AssertEqual("NN", AvatarManager.GetInitials("Nhat Quoc Nguyen", "ignored"));
    AssertEqual("AB", AvatarManager.GetInitials("   ", "admin beta"));
    AssertEqual("A", AvatarManager.GetInitials("a", null));
});

Run("student exam availability separates visibility from start eligibility", () =>
{
    var future = new StudentExamListItemModel
    {
        Title = "Future active exam",
        OpenTime = DateTime.Now.AddHours(1),
        MaxAttempts = 1,
        AttemptCount = 0,
        QuestionCount = 5
    };

    var noQuestions = new StudentExamListItemModel
    {
        Title = "Active shell",
        MaxAttempts = 1,
        AttemptCount = 0,
        QuestionCount = 0
    };

    var resumable = new StudentExamListItemModel
    {
        Title = "Resume exam",
        MaxAttempts = 1,
        AttemptCount = 1,
        InProgressAttemptCount = 1,
        QuestionCount = 3
    };

    var missingAttemptStorage = new StudentExamListItemModel
    {
        Title = "Storage missing",
        MaxAttempts = 1,
        AttemptCount = 0,
        QuestionCount = 3,
        AttemptStorageAvailable = false
    };

    AssertFalse(StudentExamAvailabilityService.CanStart(future), "future active exam must be visible but not startable");
    AssertEqual(StudentExamAvailabilityService.StatusNotOpenYet, StudentExamAvailabilityService.GetStatusText(future));
    AssertFalse(StudentExamAvailabilityService.CanStart(noQuestions), "active shell without questions must be visible but not startable");
    AssertEqual(StudentExamAvailabilityService.StatusNoQuestions, StudentExamAvailabilityService.GetStatusText(noQuestions));
    AssertTrue(StudentExamAvailabilityService.CanStart(resumable), "in-progress attempt must be resumable even when max attempts is reached");
    AssertEqual(StudentExamAvailabilityService.StatusInProgress, StudentExamAvailabilityService.GetStatusText(resumable));
    AssertFalse(StudentExamAvailabilityService.CanStart(missingAttemptStorage), "exam must not be startable without attempt storage");
    AssertEqual(StudentExamAvailabilityService.StatusStorageUnavailable, StudentExamAvailabilityService.GetStatusText(missingAttemptStorage));
});

Run("exam progress presenter summarizes answered and marked questions", () =>
{
    AssertEqual("Đã trả lời 3/10 - Đánh dấu 2", ExamProgressPresenter.BuildProgressText(3, 10, 2));
    AssertEqual("Đã trả lời 10/10 - Không đánh dấu", ExamProgressPresenter.BuildProgressText(10, 10, 0));
    AssertEqual("Còn 4 câu chưa trả lời và 1 câu đang đánh dấu xem lại. Bạn có chắc chắn muốn nộp bài không?",
        ExamProgressPresenter.BuildSubmitConfirmMessage(unansweredCount: 4, markedCount: 1));
    AssertEqual("Đã lưu lựa chọn lúc 09:15", ExamProgressPresenter.BuildSaveStatus(success: true, new DateTime(2026, 6, 13, 9, 15, 0)));
    AssertEqual("Chưa lưu được lựa chọn. Vui lòng thử lại.", ExamProgressPresenter.BuildSaveStatus(success: false, new DateTime(2026, 6, 13, 9, 15, 0)));
});

Run("student exam list shows selected exam action preview", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Student", "UC_TakeExam.cs"));
    AssertTrue(source.Contains("_examPreviewPanel"), "student exam page must include selected exam preview panel");
    AssertTrue(source.Contains("UpdateSelectedExamPreview"), "student exam page must update selected exam preview");
    AssertTrue(source.Contains("StudentExamUxPresenter.Present"), "student exam page must use StudentExamUxPresenter");
    AssertTrue(source.Contains("\"Hành động\""), "student exam table must expose action text column");
    AssertTrue(source.Contains("btnStartExam.Enabled = false"), "student exam start action should stay disabled until a row is selected");
});

Run("student exam UX presenter exposes clear launch states", () =>
{
    DateTime now = new DateTime(2026, 6, 13, 9, 0, 0);

    var open = new StudentExamListItemModel
    {
        Title = "Quiz",
        CourseName = "NT106",
        DurationMinutes = 45,
        MaxAttempts = 2,
        AttemptCount = 1,
        QuestionCount = 10,
        OpenTime = now.AddHours(-1),
        CloseTime = now.AddHours(1),
        AttemptStorageAvailable = true
    };

    object openView = StudentExamUxPresenter.Present(open, now);
    AssertEqual(StudentExamAvailabilityService.StatusCanStart, GetPropertyValue<string>(openView, "StatusText"));
    AssertEqual("Bắt đầu làm bài", GetPropertyValue<string>(openView, "ActionText"));
    AssertTrue(GetPropertyValue<bool>(openView, "CanLaunch"), "open exam with attempts should launch");
    AssertEqual("Success", GetPropertyValue<string>(openView, "Tone"));

    var resumable = new StudentExamListItemModel
    {
        Title = "Midterm",
        CourseName = "NT106",
        MaxAttempts = 1,
        AttemptCount = 1,
        InProgressAttemptCount = 1,
        QuestionCount = 20,
        OpenTime = now.AddHours(-1),
        CloseTime = now.AddHours(1),
        AttemptStorageAvailable = true
    };

    object resumeView = StudentExamUxPresenter.Present(resumable, now);
    AssertEqual("Tiếp tục làm bài", GetPropertyValue<string>(resumeView, "ActionText"));
    AssertTrue(GetPropertyValue<bool>(resumeView, "CanLaunch"), "in-progress exam should be launchable");
    AssertEqual("Warning", GetPropertyValue<string>(resumeView, "Tone"));
    AssertEqual("Tiếp tục lượt làm bài đang dở. Không cần lượt mới.", GetPropertyValue<string>(resumeView, "DetailText"));

    var future = new StudentExamListItemModel
    {
        Title = "Final",
        CourseName = "NT106",
        MaxAttempts = 1,
        AttemptCount = 0,
        QuestionCount = 15,
        OpenTime = now.AddHours(2),
        CloseTime = now.AddHours(4),
        AttemptStorageAvailable = true
    };

    object futureView = StudentExamUxPresenter.Present(future, now);
    AssertEqual("Chưa đến giờ", GetPropertyValue<string>(futureView, "ActionText"));
    AssertFalse(GetPropertyValue<bool>(futureView, "CanLaunch"), "future exam should not launch");
    AssertTrue(GetPropertyValue<string>(futureView, "DetailText").Contains("11:00"), "future detail should include open time");

    var expired = new StudentExamListItemModel
    {
        Title = "Expired",
        CourseName = "NT106",
        MaxAttempts = 1,
        AttemptCount = 0,
        QuestionCount = 15,
        OpenTime = now.AddHours(-4),
        CloseTime = now.AddMinutes(-1),
        AttemptStorageAvailable = true
    };

    object expiredView = StudentExamUxPresenter.Present(expired, now);
    AssertEqual(StudentExamAvailabilityService.StatusExpired, GetPropertyValue<string>(expiredView, "StatusText"));
    AssertEqual("Đã hết hạn", GetPropertyValue<string>(expiredView, "ActionText"));
    AssertFalse(GetPropertyValue<bool>(expiredView, "CanLaunch"), "expired exam should not launch");
    AssertEqual("Muted", GetPropertyValue<string>(expiredView, "Tone"));

    var noQuestions = new StudentExamListItemModel
    {
        Title = "Empty",
        CourseName = "NT106",
        MaxAttempts = 1,
        AttemptCount = 0,
        QuestionCount = 0,
        OpenTime = now.AddHours(-1),
        CloseTime = now.AddHours(1),
        AttemptStorageAvailable = true
    };

    object noQuestionsView = StudentExamUxPresenter.Present(noQuestions, now);
    AssertEqual(StudentExamAvailabilityService.StatusNoQuestions, GetPropertyValue<string>(noQuestionsView, "StatusText"));
    AssertEqual("Chưa có câu hỏi", GetPropertyValue<string>(noQuestionsView, "ActionText"));
    AssertFalse(GetPropertyValue<bool>(noQuestionsView, "CanLaunch"), "exam without questions should not launch");
    AssertEqual("Muted", GetPropertyValue<string>(noQuestionsView, "Tone"));

    var storageUnavailable = new StudentExamListItemModel
    {
        Title = "Storage",
        CourseName = "NT106",
        MaxAttempts = 1,
        AttemptCount = 0,
        QuestionCount = 10,
        OpenTime = now.AddHours(-1),
        CloseTime = now.AddHours(1),
        AttemptStorageAvailable = false
    };

    object storageView = StudentExamUxPresenter.Present(storageUnavailable, now);
    AssertEqual(StudentExamAvailabilityService.StatusStorageUnavailable, GetPropertyValue<string>(storageView, "StatusText"));
    AssertEqual("Chưa sẵn sàng", GetPropertyValue<string>(storageView, "ActionText"));
    AssertFalse(GetPropertyValue<bool>(storageView, "CanLaunch"), "exam without attempt storage should not launch");
    AssertEqual("Warning", GetPropertyValue<string>(storageView, "Tone"));

    var outOfAttempts = new StudentExamListItemModel
    {
        Title = "Done",
        CourseName = "NT106",
        MaxAttempts = 1,
        AttemptCount = 1,
        QuestionCount = 10,
        OpenTime = now.AddHours(-1),
        CloseTime = now.AddHours(1),
        AttemptStorageAvailable = true
    };

    object outOfAttemptsView = StudentExamUxPresenter.Present(outOfAttempts, now);
    AssertEqual(StudentExamAvailabilityService.StatusOutOfAttempts, GetPropertyValue<string>(outOfAttemptsView, "StatusText"));
    AssertEqual("Hết lượt", GetPropertyValue<string>(outOfAttemptsView, "ActionText"));
    AssertFalse(GetPropertyValue<bool>(outOfAttemptsView, "CanLaunch"), "exam with no remaining attempts should not launch");
    AssertEqual("Muted", GetPropertyValue<string>(outOfAttemptsView, "Tone"));

    var unlimitedAttempts = new StudentExamListItemModel
    {
        Title = "Practice",
        CourseName = "NT106",
        DurationMinutes = 30,
        MaxAttempts = 0,
        AttemptCount = 5,
        QuestionCount = 8,
        OpenTime = now.AddHours(-1),
        CloseTime = now.AddHours(1),
        AttemptStorageAvailable = true
    };

    object unlimitedView = StudentExamUxPresenter.Present(unlimitedAttempts, now);
    AssertEqual(StudentExamAvailabilityService.StatusCanStart, GetPropertyValue<string>(unlimitedView, "StatusText"));
    AssertEqual("Bắt đầu làm bài", GetPropertyValue<string>(unlimitedView, "ActionText"));
    AssertTrue(GetPropertyValue<bool>(unlimitedView, "CanLaunch"), "unlimited attempts exam should launch");
    AssertTrue(GetPropertyValue<string>(unlimitedView, "DetailText").Contains("không giới hạn lượt"), "unlimited detail should explain attempts");
});

Run("student assignment UX presenter exposes submission states", () =>
{
    DateTime now = new DateTime(2026, 6, 13, 9, 0, 0);

    var urgent = new StudentAssignmentRow
    {
        Title = "Report",
        CourseName = "NT106",
        DueDate = now.AddHours(3),
        Status = "OPEN"
    };

    object urgentView = StudentAssignmentUxPresenter.Present(urgent, now);
    AssertEqual("Sắp hết hạn", GetPropertyValue<string>(urgentView, "StatusText"));
    AssertEqual("Nộp bài", GetPropertyValue<string>(urgentView, "ActionText"));
    AssertTrue(GetPropertyValue<bool>(urgentView, "CanSubmit"), "urgent open assignment should be submittable");

    var graded = new StudentAssignmentRow
    {
        Title = "Lab",
        CourseName = "NT106",
        DueDate = now.AddDays(-2),
        Status = "OPEN",
        SubmissionId = 7,
        StudentFileName = "lab.pdf",
        SubmittedAt = now.AddDays(-3),
        Score = 8.5m,
        Feedback = "Tốt"
    };

    object gradedView = StudentAssignmentUxPresenter.Present(graded, now);
    AssertEqual("Đã chấm", GetPropertyValue<string>(gradedView, "StatusText"));
    AssertEqual("Xem phản hồi", GetPropertyValue<string>(gradedView, "ActionText"));
    AssertFalse(GetPropertyValue<bool>(gradedView, "CanSubmit"), "graded assignment should not accept a replacement upload");
    AssertTrue(GetPropertyValue<bool>(gradedView, "ShowsFeedback"), "graded assignment should show feedback");

    var submittedBeforeDue = new StudentAssignmentRow
    {
        Title = "Draft",
        CourseName = "NT106",
        DueDate = now.AddHours(3),
        Status = "OPEN",
        SubmissionId = 8,
        StudentFileName = "draft.pdf",
        SubmittedAt = now.AddHours(-1)
    };

    object submittedBeforeDueView = StudentAssignmentUxPresenter.Present(submittedBeforeDue, now);
    AssertEqual("Đã nộp", GetPropertyValue<string>(submittedBeforeDueView, "StatusText"));
    AssertEqual("Nộp lại", GetPropertyValue<string>(submittedBeforeDueView, "ActionText"));
    AssertTrue(GetPropertyValue<bool>(submittedBeforeDueView, "CanSubmit"), "submitted assignment before due date should allow replacement upload");
});

Run("student assignment UX presenter covers closed and overdue states", () =>
{
    DateTime now = new DateTime(2026, 6, 13, 9, 0, 0);

    var closedUnsubmitted = new StudentAssignmentRow
    {
        Title = "Closed",
        CourseName = "NT106",
        DueDate = now.AddHours(3),
        Status = "CLOSED"
    };

    object closedUnsubmittedView = StudentAssignmentUxPresenter.Present(closedUnsubmitted, now);
    AssertEqual("Đã đóng", GetPropertyValue<string>(closedUnsubmittedView, "StatusText"));
    AssertEqual("Đã đóng", GetPropertyValue<string>(closedUnsubmittedView, "ActionText"));
    AssertFalse(GetPropertyValue<bool>(closedUnsubmittedView, "CanSubmit"), "closed unsubmitted assignment should not submit");

    var closedSubmitted = new StudentAssignmentRow
    {
        Title = "Closed submitted",
        CourseName = "NT106",
        DueDate = now.AddHours(3),
        Status = "CLOSED",
        SubmissionId = 10,
        StudentFileName = "closed.pdf",
        SubmittedAt = now.AddHours(-1)
    };

    object closedSubmittedView = StudentAssignmentUxPresenter.Present(closedSubmitted, now);
    AssertEqual("Đã đóng", GetPropertyValue<string>(closedSubmittedView, "StatusText"));
    AssertEqual("Xem bài nộp", GetPropertyValue<string>(closedSubmittedView, "ActionText"));
    AssertFalse(GetPropertyValue<bool>(closedSubmittedView, "CanSubmit"), "closed submitted assignment should be view-only");

    var overdueUnsubmitted = new StudentAssignmentRow
    {
        Title = "Late",
        CourseName = "NT106",
        DueDate = now.AddHours(-2),
        Status = "OPEN"
    };

    object overdueUnsubmittedView = StudentAssignmentUxPresenter.Present(overdueUnsubmitted, now);
    AssertEqual("Quá hạn", GetPropertyValue<string>(overdueUnsubmittedView, "StatusText"));
    AssertEqual("Quá hạn", GetPropertyValue<string>(overdueUnsubmittedView, "ActionText"));
    AssertFalse(GetPropertyValue<bool>(overdueUnsubmittedView, "CanSubmit"), "overdue unsubmitted assignment should not submit");

    var submittedOverdue = new StudentAssignmentRow
    {
        Title = "Essay",
        CourseName = "NT106",
        DueDate = now.AddHours(-2),
        Status = "OPEN",
        SubmissionId = 9,
        StudentFileName = "essay.pdf",
        SubmittedAt = now.AddHours(-3)
    };

    object view = StudentAssignmentUxPresenter.Present(submittedOverdue, now);
    AssertEqual("Đã nộp", GetPropertyValue<string>(view, "StatusText"));
    AssertEqual("Xem bài nộp", GetPropertyValue<string>(view, "ActionText"));
    AssertFalse(GetPropertyValue<bool>(view, "CanSubmit"), "overdue submitted assignment should be view-only");
});

Run("student assignment UX uses presenter and shows status details", () =>
{
    string page = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Student", "UC_StudentAssignments.cs"));
    string basePage = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Student", "StudentGridPageBase.cs"));
    string dialog = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "StudentAssignmentSubmitDialog.cs"));

    AssertTrue(basePage.Contains("SetBelowGridContent"), "student grid base must expose below-grid content slot");
    AssertTrue(page.Contains("StudentAssignmentUxPresenter.Present"), "student assignment list must use StudentAssignmentUxPresenter");
    AssertTrue(page.Contains("\"Hành động\""), "student assignment table must include Hành động column");
    AssertTrue(page.Contains("_assignmentPreviewPanel"), "student assignment page must include selected assignment preview panel");
    AssertTrue(dialog.Contains("_statusBanner"), "student assignment dialog must show a status banner");
    AssertTrue(dialog.Contains("_submissionSummary"), "student assignment dialog must show current submission summary");
    AssertTrue(dialog.Contains("_feedbackPanel"), "student assignment dialog must show grade feedback panel");
});

Run("student assignment grid double click ignores non-data rows", () =>
{
    string page = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Student", "UC_StudentAssignments.cs"));
    string openFromRow = ExtractMethodSource(page, "private void OpenAssignmentFromGridRow");

    AssertTrue(page.Contains("Grid.CellDoubleClick += (_, e)"), "assignment grid double-click handler must receive DataGridViewCellEventArgs");
    AssertTrue(page.Contains("OpenAssignmentFromGridRow(e.RowIndex)"), "assignment grid double-click handler must use e.RowIndex");
    AssertTrue(openFromRow.Contains("rowIndex < 0"), "assignment double-click helper must ignore header and non-data rows");
    AssertTrue(openFromRow.Contains("rowIndex >= Grid.Rows.Count"), "assignment double-click helper must guard rows beyond the grid");
    AssertTrue(openFromRow.Contains("OpenSelectedAssignment();"), "valid assignment row double-click should reuse the selected assignment open path");
    AssertFalse(
        page.Contains("Grid.CellDoubleClick += (_, _) => OpenSelectedAssignment();"),
        "assignment grid double-click must not reuse stale CurrentRow for header clicks");
});

Run("student assignment submit is guarded by current database state", () =>
{
    string dbContext = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Data", "CourseGuardDbContext.cs"));
    string submit = ExtractMethodSource(dbContext, "public async Task<bool> SubmitAssignmentAsync");

    AssertTrue(submit.Contains("UPDATE assignment_submissions s"), "existing submissions must be updated through a joined guarded update");
    AssertTrue(submit.Contains("INSERT INTO assignment_submissions") && submit.Contains("SELECT @assignment_id"), "new submissions must insert from a guarded SELECT so guard failure returns zero rows");
    AssertTrue(submit.Contains("JOIN teacher_assignments a"), "submit guard must verify the assignment row exists");
    AssertTrue(submit.Contains("JOIN courses c ON c.id = a.course_id"), "submit guard must join the assignment course");
    AssertTrue(submit.Contains("JOIN enrollments e ON e.course_id = c.id"), "submit guard must join active student enrollment");
    AssertTrue(submit.Contains("e.student_id = @student_id"), "submit guard must scope enrollment to the submitting student");
    AssertTrue(submit.Contains("UPPER(COALESCE(e.status, '')) IN ('ACTIVE', 'APPROVED')"), "submit guard must require active or approved enrollment");
    AssertTrue(submit.Contains("UPPER(COALESCE(c.status, '')) = 'ACTIVE'"), "submit guard must require an active course");
    AssertTrue(submit.Contains("UPPER(COALESCE(a.status, '')) = 'OPEN'"), "submit guard must require an open assignment");
    AssertTrue(
        submit.Contains("a.due_at IS NULL") && submit.Contains("a.due_at >= CURRENT_TIMESTAMP"),
        "submit guard must allow null deadlines and reject non-null past deadlines");
    AssertTrue(submit.Contains("s.score IS NULL"), "replacement submissions must be blocked after a score is set");
    AssertTrue(submit.Contains("UPPER(COALESCE(s.status, '')) <> 'GRADED'"), "replacement submissions must be blocked after graded status");
    AssertTrue(submit.Contains("NOT EXISTS"), "guarded insert must produce zero rows when a submission already exists");
    AssertTrue(submit.Contains("return rows > 0;"), "SubmitAssignmentAsync must return false when guarded writes affect zero rows");
    AssertFalse(
        submit.Contains("SELECT id FROM assignment_submissions WHERE assignment_id"),
        "SubmitAssignmentAsync must not decide writability from an unguarded cached existence query");
});

Run("teacher controller scopes submission content and grading calls by teacher", () =>
{
    string controller = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Controllers", "TeacherController.cs"));
    string getContent = ExtractMethodSource(controller, "public System.Threading.Tasks.Task<byte[]?> GetSubmissionContentAsync");
    string updateGrade = ExtractMethodSource(controller, "public System.Threading.Tasks.Task<bool> UpdateGradeAsync");

    AssertTrue(
        getContent.Contains("_repository.GetSubmissionContentAsync(teacherId, submissionId)"),
        "controller content reads must pass teacherId to the repository");
    AssertFalse(
        getContent.Contains("_repository.GetSubmissionContentAsync(submissionId)"),
        "controller content reads must not call the unscoped repository overload");
    AssertTrue(
        updateGrade.Contains("_repository.UpdateGradeAsync(teacherId, submissionId, score, feedback)"),
        "controller grade updates must pass teacherId to the repository");
    AssertFalse(
        updateGrade.Contains("_repository.UpdateGradeAsync(submissionId, score, feedback)"),
        "controller grade updates must not call the unscoped repository overload");
});

Run("teacher repository submission content and grade updates require ownership", () =>
{
    string repository = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Data", "TeacherRepository.cs"));
    string getContent = ExtractMethodSource(repository, "public async Task<byte[]?> GetSubmissionContentAsync");
    string updateGrade = ExtractMethodSource(repository, "public async Task<bool> UpdateGradeAsync");

    AssertTrue(
        repository.Contains("public async Task<byte[]?> GetSubmissionContentAsync(int teacherId, int submissionId"),
        "repository content read must accept teacherId");
    AssertTrue(
        repository.Contains("public async Task<bool> UpdateGradeAsync(int teacherId, int submissionId"),
        "repository grade update must accept teacherId");

    AssertTrue(getContent.Contains("FROM assignment_submissions s"), "content read must query submissions through an alias");
    AssertTrue(getContent.Contains("JOIN teacher_assignments a ON s.assignment_id = a.id"), "content read must join the assignment");
    AssertTrue(getContent.Contains("JOIN courses c ON a.course_id = c.id"), "content read must join the course");
    AssertTrue(getContent.Contains("c.teacher_id = @teacher_id"), "content read must require course ownership");
    AssertTrue(getContent.Contains("command.Parameters.AddWithValue(\"@teacher_id\", teacherId)"), "content read must bind teacherId");

    AssertTrue(updateGrade.Contains("UPDATE assignment_submissions s"), "grade update must update submissions through an alias");
    AssertTrue(updateGrade.Contains("JOIN teacher_assignments a ON s.assignment_id = a.id"), "grade update must join the assignment");
    AssertTrue(updateGrade.Contains("JOIN courses c ON a.course_id = c.id"), "grade update must join the course");
    AssertTrue(updateGrade.Contains("c.teacher_id = @teacher_id"), "grade update must require course ownership");
    AssertTrue(updateGrade.Contains("command.Parameters.AddWithValue(\"@teacher_id\", teacherId)"), "grade update must bind teacherId");
});

Run("teacher assignment UX presenter exposes grading states", () =>
{
    DateTime submittedAt = new DateTime(2026, 6, 13, 8, 30, 0);

    var ungraded = new StudentSubmissionModel
    {
        SubmissionId = 1,
        AssignmentTitle = "Report",
        StudentName = "Nguyen Van A",
        FileName = "report.pdf",
        SubmittedAt = submittedAt,
        Status = "SUBMITTED"
    };

    object ungradedView = TeacherAssignmentUxPresenter.PresentSubmission(ungraded);
    AssertEqual("Chưa chấm", GetPropertyValue<string>(ungradedView, "StatusText"));
    AssertEqual("Chấm điểm", GetPropertyValue<string>(ungradedView, "ActionText"));
    AssertEqual("Warning", GetPropertyValue<string>(ungradedView, "Tone"));
    AssertFalse(GetPropertyValue<bool>(ungradedView, "ShowsFeedback"), "ungraded submission should not show feedback");

    var graded = new StudentSubmissionModel
    {
        SubmissionId = 2,
        AssignmentTitle = "Lab",
        StudentName = "Tran Thi B",
        FileName = "lab.pdf",
        SubmittedAt = submittedAt,
        Status = "GRADED",
        Score = 9m,
        Feedback = "Tốt"
    };

    object gradedView = TeacherAssignmentUxPresenter.PresentSubmission(graded);
    AssertEqual("Đã chấm", GetPropertyValue<string>(gradedView, "StatusText"));
    AssertEqual("Cập nhật điểm", GetPropertyValue<string>(gradedView, "ActionText"));
    AssertEqual("Success", GetPropertyValue<string>(gradedView, "Tone"));
    AssertTrue(GetPropertyValue<bool>(gradedView, "ShowsFeedback"), "graded submission should show feedback");
});

Run("teacher assignment UX exposes grading filters and inline validation", () =>
{
    string assignments = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherAssignments.cs"));
    string assignmentDialog = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherAssignmentDialog.cs"));
    string submissionsDialog = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherSubmissionsDialog.cs"));

    AssertTrue(assignments.Contains("Mở bài nộp"), "teacher assignment page must label submissions action clearly");
    AssertTrue(assignmentDialog.Contains("_validationSummary"), "teacher assignment dialog must show inline validation summary");
    AssertTrue(assignmentDialog.Contains("_fileSummary"), "teacher assignment dialog must show selected file summary");
    AssertTrue(submissionsDialog.Contains("_cboStatus"), "teacher submissions dialog must include grading status filter");
    AssertTrue(submissionsDialog.Contains("_cboAssignment"), "teacher submissions dialog must include assignment filter");
    AssertTrue(submissionsDialog.Contains("_saveStatus"), "teacher submissions dialog must show save status");
    AssertTrue(submissionsDialog.Contains("TeacherAssignmentUxPresenter.PresentSubmission"), "teacher submissions dialog must use TeacherAssignmentUxPresenter");
});

Run("teacher assignment dialog rejects empty selected attachment files before save", () =>
{
    string dialog = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherAssignmentDialog.cs"));
    string validateBeforeSave = ExtractMethodSource(dialog, "private bool ValidateBeforeSave");

    AssertTrue(
        validateBeforeSave.Contains("MaterialFilePolicy.Validate") || validateBeforeSave.Contains("info.Length <= 0") || validateBeforeSave.Contains("info.Length == 0"),
        "ValidateBeforeSave must reject empty selected files before allowing an assignment save");
    AssertTrue(
        validateBeforeSave.Contains("ShowValidationError"),
        "empty selected files must be reported through inline validation text");
    AssertTrue(
        validateBeforeSave.Contains("MaxAttachmentBytes") && validateBeforeSave.Contains("10MB"),
        "assignment attachment validation must preserve the Task 6 10MB rule and message");
});

Run("teacher assignment dialog rejects missing selected attachment files before save", () =>
{
    string dialog = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherAssignmentDialog.cs"));
    string validateBeforeSave = ExtractMethodSource(dialog, "private bool ValidateBeforeSave");

    int selectedPathGuard = validateBeforeSave.IndexOf("!string.IsNullOr", StringComparison.Ordinal);
    int missingFileCheck = validateBeforeSave.IndexOf("!File.Exists(SelectedFilePath)", StringComparison.Ordinal);
    int inlineError = missingFileCheck < 0
        ? -1
        : validateBeforeSave.IndexOf("ShowValidationError", missingFileCheck, StringComparison.Ordinal);

    AssertTrue(
        selectedPathGuard >= 0 && missingFileCheck > selectedPathGuard,
        "ValidateBeforeSave must check for a non-empty selected local file path that no longer exists");
    AssertTrue(
        inlineError > missingFileCheck,
        "missing selected files must show inline validation and block save");
});

Run("teacher assignment update preserves stored attachment content unless replaced or cleared", () =>
{
    string repository = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Data", "TeacherRepository.cs"));
    string assignments = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherAssignments.cs"));
    string updateAssignment = ExtractMethodSource(repository, "public bool UpdateAssignment");
    string editAssignment = ExtractMethodSource(assignments, "protected override async Task EditAsync");

    AssertTrue(updateAssignment.Contains("@update_file"), "UpdateAssignment must include an explicit update-file parameter");
    AssertTrue(
        updateAssignment.Contains("file_content = CASE WHEN @update_file = 1 THEN @file_content ELSE a.file_content END"),
        "UpdateAssignment must preserve existing file_content unless a replacement or clear is requested");
    AssertFalse(
        updateAssignment.Contains("file_content = @file_content"),
        "UpdateAssignment must not directly overwrite file_content with a nullable parameter");

    int replacementBranch = updateAssignment.IndexOf("input.FileContent != null && input.FileContent.Length > 0", StringComparison.Ordinal);
    AssertTrue(replacementBranch >= 0, "UpdateAssignment must branch on positive FileContent replacements");
    int replacementUpdateFile = updateAssignment.IndexOf("command.Parameters.AddWithValue(\"@update_file\", 1)", replacementBranch, StringComparison.Ordinal);
    int replacementContent = updateAssignment.IndexOf("command.Parameters.AddWithValue(\"@file_content\", input.FileContent)", replacementBranch, StringComparison.Ordinal);
    AssertTrue(
        replacementUpdateFile >= 0 && replacementContent > replacementUpdateFile,
        "positive FileContent replacements must set @update_file to 1 and send the input bytes");

    int preserveBranch = updateAssignment.IndexOf("input.HasStoredContent && input.FileContent == null", StringComparison.Ordinal);
    AssertTrue(preserveBranch > replacementBranch, "UpdateAssignment must branch on unchanged stored assignment content");
    int preserveUpdateFile = updateAssignment.IndexOf("command.Parameters.AddWithValue(\"@update_file\", 0)", preserveBranch, StringComparison.Ordinal);
    AssertTrue(preserveUpdateFile >= 0, "unchanged stored content must set @update_file to 0");

    int clearUpdateFile = updateAssignment.IndexOf("command.Parameters.AddWithValue(\"@update_file\", 1)", preserveUpdateFile, StringComparison.Ordinal);
    int clearContent = updateAssignment.IndexOf("command.Parameters.AddWithValue(\"@file_content\", DBNull.Value)", clearUpdateFile, StringComparison.Ordinal);
    AssertTrue(
        clearUpdateFile > preserveUpdateFile && clearContent > clearUpdateFile,
        "cleared assignment attachments must set @update_file to 1 and send DBNull.Value");

    AssertTrue(
        editAssignment.Contains("dialog.ClearFileRequested") || editAssignment.Contains("dialog.ClearExistingFile"),
        "assignment edit flow must read the dialog clear-file request");
    AssertTrue(
        editAssignment.Contains("model.HasStoredContent = existing.HasStoredContent") || editAssignment.Contains("model.HasStoredContent = true"),
        "assignment edit flow must retain HasStoredContent when preserving an unchanged attachment");
    AssertTrue(
        editAssignment.Contains("model.HasStoredContent = false"),
        "assignment edit flow must clear HasStoredContent when clearing an attachment");
});

Run("teacher assignment dialog keeps selected file path separate from existing attachment state", () =>
{
    string dialog = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherAssignmentDialog.cs"));
    string selectedFilePath = ExtractPropertySource(dialog, "public string SelectedFilePath");

    AssertTrue(dialog.Contains("_selectedFilePath"), "assignment dialog must track newly selected local files separately");
    AssertTrue(dialog.Contains("_existingFileName"), "assignment dialog must track the stored filename separately");
    AssertTrue(dialog.Contains("_clearExistingFile"), "assignment dialog must track explicit clear-file intent");
    AssertTrue(
        dialog.Contains("public bool ClearFileRequested") || dialog.Contains("public bool ClearExistingFile"),
        "assignment dialog must expose the clear-file request to the edit flow");
    AssertFalse(
        selectedFilePath.Contains("_file.Text.Trim()"),
        "SelectedFilePath must not return the textbox text, which may be an existing stored filename");
    AssertTrue(
        selectedFilePath.Contains("_selectedFilePath"),
        "SelectedFilePath must return only the newly selected local file path");
});

Run("teacher submissions save status remains readable when grading inputs are disabled", () =>
{
    string submissionsDialog = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherSubmissionsDialog.cs"));
    string resetPanel = ExtractMethodSource(submissionsDialog, "private void ResetGradingPanel");
    string selectRow = ExtractMethodSource(submissionsDialog, "private void Grid_SelectionChanged");
    string setInputs = ExtractMethodSource(submissionsDialog, "private void SetGradingInputsEnabled");

    AssertFalse(resetPanel.Contains("_gradingPanel.Enabled = false"), "reset must not disable the parent grading panel that contains save status");
    AssertFalse(selectRow.Contains("_gradingPanel.Enabled = true"), "row selection must enable only grading inputs, not the parent panel");
    AssertTrue(setInputs.Contains("_btnDownload.Enabled = enabled"), "grading input helper must disable the download button");
    AssertTrue(setInputs.Contains("_numScore.Enabled = enabled"), "grading input helper must disable the score input");
    AssertTrue(setInputs.Contains("_txtFeedback.Enabled = enabled"), "grading input helper must disable the feedback input");
    AssertTrue(setInputs.Contains("_btnSaveGrade.Enabled = enabled"), "grading input helper must disable the save button");
    AssertFalse(setInputs.Contains("_saveStatus.Enabled"), "grading input helper must leave save status readable");
});

Run("teacher exam UX presenter makes draft readiness explicit", () =>
{
    var draftWithoutQuestions = new TeacherExamModel
    {
        Title = "Draft exam",
        Status = WorkflowConstants.ExamStatus.Draft,
        QuestionCount = 0
    };

    object draftView = TeacherExamUxPresenter.Present(draftWithoutQuestions);
    AssertEqual("Cần câu hỏi", GetPropertyValue<string>(draftView, "StatusText"));
    AssertEqual("Soạn câu hỏi", GetPropertyValue<string>(draftView, "PrimaryActionText"));
    AssertFalse(GetPropertyValue<bool>(draftView, "CanActivate"), "exam without questions cannot activate");

    var draftReady = new TeacherExamModel
    {
        Title = "Ready exam",
        Status = WorkflowConstants.ExamStatus.Draft,
        QuestionCount = 12
    };

    object readyView = TeacherExamUxPresenter.Present(draftReady);
    AssertEqual("Sẵn sàng kích hoạt", GetPropertyValue<string>(readyView, "StatusText"));
    AssertTrue(GetPropertyValue<bool>(readyView, "CanActivate"), "draft with questions can activate");
});

Run("teacher exam UX exposes draft readiness and explicit activation", () =>
{
    string exams = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherExams.cs"));
    string examDialog = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherExamDialog.cs"));
    string questionDialog = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherExamQuestionsDialog.cs"));

    AssertTrue(exams.Contains("_activateButton"), "teacher exams page must include explicit activate button");
    AssertTrue(exams.Contains("TeacherExamUxPresenter.Present"), "teacher exams page must use TeacherExamUxPresenter");
    AssertTrue(exams.Contains("ActivateSelectedExamAsync"), "teacher exams page must activate selected draft exams through an explicit action");
    AssertTrue(examDialog.Contains("_validationSummary"), "teacher exam dialog must show inline validation summary");
    AssertTrue(examDialog.Contains("Tạo nháp"), "teacher exam dialog must explain draft-first workflow");
    AssertTrue(questionDialog.Contains("_readinessSummary"), "question dialog must show readiness summary");
    AssertTrue(questionDialog.Contains("_readOnlyHint"), "question dialog must show read-only hint for non-draft exams");
});

Run("phase 4 student course presenter exposes next actions", () =>
{
    var now = new DateTime(2026, 6, 15, 9, 0, 0);
    var active = new EnrollmentModel
    {
        CourseId = 10,
        CourseName = "Lap trinh mang",
        TeacherName = "Nguyen Van A",
        Status = "ACTIVE",
        CourseStatus = WorkflowConstants.CourseStatus.Active,
        CourseStartDate = new DateTime(2026, 6, 1),
        CourseEndDate = new DateTime(2026, 8, 1),
        CourseDescription = "TCP/IP co ban"
    };
    var pending = new EnrollmentModel
    {
        CourseId = 11,
        CourseName = "Co so du lieu",
        TeacherName = "Tran Thi B",
        Status = "PENDING",
        CourseStatus = WorkflowConstants.CourseStatus.Active,
        CourseStartDate = new DateTime(2026, 7, 1),
        CourseEndDate = new DateTime(2026, 9, 1)
    };

    LearningUxPresentation activeView = StudentCourseUxPresenter.PresentEnrollment(active, now);
    LearningUxPresentation pendingView = StudentCourseUxPresenter.PresentEnrollment(pending, now);

    AssertEqual("Đang học", activeView.StatusText);
    AssertEqual("Vào trang Bài học", activeView.PrimaryActionText);
    AssertFalse(activeView.CanUsePrimaryAction, "cross-tab learning action is display-only in Phase 4");
    AssertEqual(NextActionKinds.OpenStudentLessonsTab, activeView.NextActionKind);
    AssertEqual(NavigationTargets.StudentLessons, activeView.TargetTab);
    AssertEqual(10, activeView.TargetCourseId);
    AssertEqual("Chờ duyệt", pendingView.StatusText);
    AssertEqual("Chờ giảng viên/Admin duyệt", pendingView.PrimaryActionText);
    AssertFalse(pendingView.CanUsePrimaryAction, "pending enrollment should not expose learning action");
    AssertEqual(NextActionKinds.None, pendingView.NextActionKind);
});

Run("phase 4 learning presenter hides DateTime.MinValue course ranges", () =>
{
    var now = new DateTime(2026, 6, 15, 9, 0, 0);
    var course = new CourseModel
    {
        Id = 12,
        Name = "Mang can ban",
        TeacherName = "Nguyen Van A",
        Status = WorkflowConstants.CourseStatus.Active,
        StartDate = DateTime.MinValue,
        EndDate = DateTime.MinValue
    };
    var enrollment = new EnrollmentModel
    {
        CourseId = 13,
        CourseName = "Bao mat mang",
        TeacherName = "Tran Thi B",
        Status = WorkflowConstants.EnrollmentStatus.Active,
        CourseStartDate = DateTime.MinValue,
        CourseEndDate = DateTime.MinValue
    };

    LearningUxPresentation courseView = StudentCourseUxPresenter.PresentAvailableCourse(course, enrolledCount: 0, now);
    LearningUxPresentation enrollmentView = StudentCourseUxPresenter.PresentEnrollment(enrollment, now);

    AssertFalse(courseView.DetailText.Contains("0001"), "available course detail must not expose DateTime.MinValue");
    AssertFalse(enrollmentView.DetailText.Contains("0001"), "enrollment detail must not expose DateTime.MinValue");
    AssertTrue(courseView.DetailText.Contains("chưa có ngày bắt đầu"), "available course detail must render missing start date fallback");
    AssertTrue(courseView.DetailText.Contains("chưa có ngày kết thúc"), "available course detail must render missing end date fallback");
    AssertTrue(enrollmentView.DetailText.Contains("chưa có ngày bắt đầu"), "enrollment detail must render missing start date fallback");
    AssertTrue(enrollmentView.DetailText.Contains("chưa có ngày kết thúc"), "enrollment detail must render missing end date fallback");
});

Run("phase 4 student course pages use learning presenters", () =>
{
    string root = RepoRoot();
    string courseList = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Student", "UC_CourseList.cs"));
    string myCourses = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Student", "UC_MyCourses.cs"));

    AssertTrue(courseList.Contains("StudentCourseUxPresenter.PresentAvailableCourse"),
        "UC_CourseList must use StudentCourseUxPresenter for available course status/action text");
    AssertTrue(courseList.Contains("\"Hành động\""),
        "UC_CourseList table must expose an action column");
    AssertTrue(courseList.Contains("_detailAction"),
        "UC_CourseList must show selected course next action in the detail panel");

    AssertTrue(myCourses.Contains("StudentCourseUxPresenter.PresentEnrollment"),
        "UC_MyCourses must use StudentCourseUxPresenter for enrollment status/action text");
    AssertTrue(myCourses.Contains("\"Hành động\""),
        "UC_MyCourses table must expose an action column");
    AssertTrue(myCourses.Contains("_courseNextAction"),
        "UC_MyCourses must show selected enrollment next action in the detail panel");
});

Run("phase 4 schedule pages use schedule presenter and selectable calendar sessions", () =>
{
    string root = RepoRoot();
    string studentSchedule = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Student", "UC_Schedule.cs"));
    string teacherSchedule = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherSchedule.cs"));
    string teacherDialog = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherSimpleItemDialog.cs"));

    AssertTrue(studentSchedule.Contains("ScheduleUxPresenter.PresentStudent"),
        "UC_Schedule must use ScheduleUxPresenter.PresentStudent");
    AssertTrue(studentSchedule.Contains("SelectSessionFromCalendar"),
        "UC_Schedule must expose SelectSessionFromCalendar");
    AssertTrue(studentSchedule.Contains("_selectedSessionSummary"),
        "UC_Schedule must track the selected session summary");
    AssertTrue(studentSchedule.Contains("_selectedCalendarSessionId"),
        "UC_Schedule must track calendar-selected session id");
    AssertTrue(studentSchedule.Contains("private static DateTime StartOfWeek(DateTime value)"),
        "UC_Schedule must centralize Monday-first week calculation in StartOfWeek");
    AssertTrue(studentSchedule.Contains("lblSession.Text = string.Empty"),
        "student pooled calendar labels must clear stale text");
    AssertTrue(studentSchedule.Contains("lblSession.Cursor = Cursors.Default"),
        "student pooled calendar labels must reset cursor when unused");

    string studentCalendarSelection = ExtractMethodSource(studentSchedule, "private void SelectSessionFromCalendar(int sessionId)");
    AssertFalse(studentCalendarSelection.Contains("_isCalendarView = false"),
        "student calendar selection must not force table view");

    AssertTrue(teacherSchedule.Contains("ScheduleUxPresenter.PresentTeacher"),
        "UC_TeacherSchedule must use ScheduleUxPresenter.PresentTeacher");
    AssertTrue(teacherSchedule.Contains("SelectSessionFromCalendar"),
        "UC_TeacherSchedule must expose SelectSessionFromCalendar");
    AssertTrue(teacherSchedule.Contains("_selectedSessionSummary"),
        "UC_TeacherSchedule must track the selected session summary");
    AssertTrue(teacherSchedule.Contains("_selectedCalendarSessionId"),
        "UC_TeacherSchedule must track calendar-selected session id");
    AssertTrue(teacherSchedule.Contains("private static DateTime StartOfWeek(DateTime value)"),
        "UC_TeacherSchedule must centralize Monday-first week calculation in StartOfWeek");
    AssertTrue(teacherSchedule.Contains("ClearScheduleStateAfterLoadFailure"),
        "UC_TeacherSchedule must clear stale state after load failure");

    string teacherCalendarSelection = ExtractMethodSource(teacherSchedule, "private void SelectSessionFromCalendar(int sessionId)");
    AssertFalse(teacherCalendarSelection.Contains("_isCalendarView = false"),
        "teacher calendar selection must not force table view");
    string teacherLoadData = ExtractMethodSource(teacherSchedule, "private async Task LoadDataAsync()");
    AssertTrue(teacherLoadData.Contains("ClearScheduleStateAfterLoadFailure"),
        "teacher schedule load failure path must clear stale state");

    AssertTrue(teacherDialog.Contains("_validationSummary"),
        "TeacherSimpleItemDialog must show inline validation summary");
    AssertTrue(teacherDialog.Contains("HookValidationInputs"),
        "TeacherSimpleItemDialog must wire live validation input hooks");
    AssertTrue(teacherDialog.Contains("CourseCombo.SelectedIndexChanged"),
        "TeacherSimpleItemDialog must react to course changes");
    AssertTrue(teacherDialog.Contains("TitleTextBox.TextChanged"),
        "TeacherSimpleItemDialog must react to title changes");
    AssertTrue(teacherDialog.Contains("StartTimePicker.ValueChanged"),
        "TeacherSimpleItemDialog must react to start time changes");
    AssertTrue(teacherDialog.Contains("EndTimePicker.ValueChanged"),
        "TeacherSimpleItemDialog must react to end time changes");
});

Run("phase 4 schedule week helper keeps Sunday in current week", () =>
{
    MethodInfo? studentMethod = typeof(UC_Schedule).GetMethod("StartOfWeek", BindingFlags.NonPublic | BindingFlags.Static);
    MethodInfo? teacherMethod = typeof(UC_TeacherSchedule).GetMethod("StartOfWeek", BindingFlags.NonPublic | BindingFlags.Static);

    AssertTrue(studentMethod != null, "UC_Schedule must expose non-public static StartOfWeek for filtering");
    AssertTrue(teacherMethod != null, "UC_TeacherSchedule must expose non-public static StartOfWeek for filtering");

    DateTime sunday = Enumerable.Range(0, 7)
        .Select(offset => new DateTime(2026, 6, 1).AddDays(offset))
        .First(day => day.DayOfWeek == DayOfWeek.Sunday);
    DateTime expectedMonday = sunday.Date.AddDays(-6);

    DateTime studentWeekStart = (DateTime)studentMethod!.Invoke(null, new object[] { sunday })!;
    DateTime teacherWeekStart = (DateTime)teacherMethod!.Invoke(null, new object[] { sunday })!;

    AssertEqual(DayOfWeek.Sunday, sunday.DayOfWeek);
    AssertEqual(expectedMonday, studentWeekStart);
    AssertEqual(expectedMonday, teacherWeekStart);
    AssertEqual(DayOfWeek.Monday, studentWeekStart.DayOfWeek);
    AssertEqual(DayOfWeek.Monday, teacherWeekStart.DayOfWeek);
});

Run("phase 4 teacher schedule failure cleanup clears stale runtime state", RunTeacherScheduleFailureCleanupTests);
Run("phase 4 teacher simple dialog updates inline validation while inputs are fixed", RunTeacherSimpleItemDialogValidationTests);
Run("modal dialogs reuse admin input styling across roles", RunModalDialogAdminInputStyleSourceTests);
Run("course scoped teacher dialogs lock course selection and reserve admin input layout", RunCourseScopedDialogLayoutSourceTests);
Run("phase 4 student pooled calendar labels reset stale session state on reuse", RunStudentScheduleCalendarLabelReuseTests);
Run("phase 4 student schedule latest failure cleanup clears stale runtime state", RunStudentScheduleFailureCleanupTests);
Run("native classroom opens detached from dashboards and reuses course chat", () =>
{
    string root = RepoRoot();
    string teacherSchedule = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherSchedule.cs"));
    string studentSchedule = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Student", "UC_Schedule.cs"));
    string teacherClassroom = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherNativeClassroomForm.cs"));
    string studentClassroom = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "StudentNativeClassroomForm.cs"));
    string chatDialog = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Classroom", "ClassroomChatDialog.cs"));

    string teacherOpenClass = ExtractMethodSource(teacherSchedule, "private async Task OpenClassAsync()");
    string studentJoinSession = ExtractMethodSource(studentSchedule, "private void JoinSession(StudentScheduleItemModel? session)");

    AssertFalse(teacherOpenClass.Contains("ShowDialog(FindForm())"),
        "teacher classroom must not be opened as a dashboard-owned modal dialog");
    AssertFalse(studentJoinSession.Contains("ShowDialog(FindForm())"),
        "student classroom must not be opened as a dashboard-owned modal dialog");
    AssertTrue(teacherOpenClass.Contains(".Show();"),
        "teacher classroom must be shown as a detached modeless top-level form");
    AssertTrue(studentJoinSession.Contains(".Show();"),
        "student classroom must be shown as a detached modeless top-level form");
    AssertTrue(teacherOpenClass.Contains("FormClosed += async"),
        "teacher schedule must refresh and unlock classroom actions from the detached form close event");
    AssertTrue(studentJoinSession.Contains("FormClosed +="),
        "student schedule must refresh after the detached classroom form closes");

    AssertTrue(teacherClassroom.Contains("FormStartPosition.CenterScreen"),
        "teacher classroom should center independently instead of centering on the dashboard parent");
    AssertTrue(studentClassroom.Contains("FormStartPosition.CenterScreen"),
        "student classroom should center independently instead of centering on the dashboard parent");
    AssertTrue(teacherClassroom.Contains("int courseId") && teacherClassroom.Contains("string courseName"),
        "teacher classroom must receive course context for course chat");
    AssertTrue(studentClassroom.Contains("int courseId") && studentClassroom.Contains("string courseName"),
        "student classroom must receive course context for course chat");
    AssertTrue(teacherOpenClass.Contains("courseId: selectedSession.CourseId") && teacherOpenClass.Contains("courseName: selectedSession.CourseName"),
        "teacher schedule must pass selected course context into the classroom form");
    AssertTrue(studentJoinSession.Contains("courseId: session.CourseId") && studentJoinSession.Contains("courseName: session.CourseName"),
        "student schedule must pass selected course context into the classroom form");

    AssertTrue(chatDialog.Contains("CourseChatPanel") && chatDialog.Contains("ChatController") && chatDialog.Contains("LoadCourseAsync"),
        "classroom chat dialog must host the same CourseChatPanel used by course workspace chat");
    AssertFalse(teacherClassroom.Contains("_chatDialog.SendRequested += SendChatMessageAsync"),
        "teacher classroom must not wire the old transient classroom chat sender when using course chat");
    AssertFalse(studentClassroom.Contains("_chatDialog.SendRequested += SendChatMessageAsync"),
        "student classroom must not wire the old transient classroom chat sender when using course chat");
});
Run("phase 4 teacher classroom lifecycle defers status changes until native server startup", () =>
{
    string root = RepoRoot();
    string teacherSchedule = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherSchedule.cs"));
    string teacherClassroom = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherNativeClassroomForm.cs"));

    string openClass = ExtractMethodSource(teacherSchedule, "private async Task OpenClassAsync()");
    int formCreationIndex = openClass.IndexOf("new TeacherNativeClassroomForm(", StringComparison.Ordinal);
    int markOpenedIndex = openClass.IndexOf("UpdateSessionStatusAsync(_teacherId, selectedSession.Id, true)", StringComparison.Ordinal);
    int broadcastOpenedIndex = openClass.IndexOf("BroadcastClassOpenedAsync(selectedSession.Id)", StringComparison.Ordinal);

    AssertTrue(formCreationIndex >= 0, "teacher schedule must create the native classroom form when opening class");
    AssertTrue(markOpenedIndex >= 0, "teacher schedule must still mark a session opened after startup succeeds");
    AssertTrue(broadcastOpenedIndex >= 0, "teacher schedule must still broadcast classroom open after startup succeeds");
    AssertTrue(formCreationIndex < markOpenedIndex,
        "teacher schedule must defer opened status updates until after the native classroom form owns startup");
    AssertTrue(formCreationIndex < broadcastOpenedIndex,
        "teacher schedule must defer open broadcasts until after the native classroom form owns startup");
    AssertTrue(openClass.Contains("UpdateSessionStatusAsync(_teacherId, selectedSession.Id, false)"),
        "teacher schedule must mark a session closed when the classroom ends after opening");
    AssertTrue(openClass.Contains("BroadcastClassClosedAsync(selectedSession.Id)"),
        "teacher schedule must broadcast classroom close when the classroom ends after opening");
    AssertTrue(openClass.Contains("bool updated = await _controller.UpdateSessionStatusAsync(_teacherId, selectedSession.Id, false)"),
        "teacher schedule close callback must capture the false/true result from closing the session in storage");
    AssertTrue(openClass.Contains("if (!updated)"),
        "teacher schedule close callback must stop when the session close update fails");

    int closeUpdateIndex = openClass.IndexOf("bool updated = await _controller.UpdateSessionStatusAsync(_teacherId, selectedSession.Id, false)", StringComparison.Ordinal);
    int closeGuardIndex = openClass.IndexOf("if (!updated)", StringComparison.Ordinal);
    int closeBroadcastIndex = openClass.IndexOf("BroadcastClassClosedAsync(selectedSession.Id)", StringComparison.Ordinal);
    int closeReloadIndex = openClass.IndexOf("await LoadDataAsync();", closeBroadcastIndex >= 0 ? closeBroadcastIndex : 0, StringComparison.Ordinal);

    AssertTrue(closeUpdateIndex >= 0 && closeGuardIndex > closeUpdateIndex,
        "teacher schedule must guard the close callback after reading the persisted close result");
    AssertTrue(closeBroadcastIndex > closeGuardIndex,
        "teacher schedule must not broadcast close before the persisted close result is accepted");
    AssertTrue(closeReloadIndex > closeBroadcastIndex,
        "teacher schedule must only reload after the close broadcast succeeds");

    AssertTrue(teacherClassroom.Contains("Func<Task>? onClassroomOpenedAsync"),
        "teacher native classroom form must accept an async startup callback so the schedule page keeps controller work outside the form");
    AssertTrue(teacherClassroom.Contains("Func<Task>? onClassroomClosedAsync"),
        "teacher native classroom form must accept an async close callback so the schedule page keeps controller work outside the form");

    string shown = ExtractMethodSource(teacherClassroom, "protected override async void OnShown(EventArgs e)");
    AssertTrue(shown.Contains("await _server.StartAsync(_sessionId);"),
        "teacher native classroom form must still start the classroom server on show");
    AssertTrue(shown.Contains("await _onClassroomOpenedAsync"),
        "teacher native classroom form must invoke the opened callback only after server startup succeeds");
});

Run("phase 4 teacher native classroom end-class path requires confirmation before teardown", () =>
{
    string root = RepoRoot();
    string teacherClassroom = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherNativeClassroomForm.cs"));

    AssertFalse(teacherClassroom.Contains("_btnEndClass.Click += (_, _) => Close();"),
        "teacher native classroom end button must not tear down the class immediately");
    AssertTrue(
        teacherClassroom.Contains("MessageBoxButtons.YesNo")
        || teacherClassroom.Contains("DialogResult.Yes"),
        "teacher native classroom must prompt for destructive end-class confirmation");

    string formClosing = ExtractMethodSource(teacherClassroom, "private async void TeacherNativeClassroomForm_FormClosing(object? sender, FormClosingEventArgs e)");
    AssertTrue(formClosing.Contains("e.Cancel = true"),
        "teacher native classroom close handling must be able to cancel the first close attempt until confirmation completes");
    AssertTrue(formClosing.Contains("_onClassroomClosedAsync"),
        "teacher native classroom close handling must invoke the close lifecycle callback before shutdown completes");
});

Run("phase 4 teacher native classroom notifies close before local teardown", () =>
{
    string root = RepoRoot();
    string teacherClassroom = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherNativeClassroomForm.cs"));
    string shutdown = ExtractMethodSource(teacherClassroom, "private async Task PerformClassroomShutdownAsync(bool notifyCloseAsync)");

    int callbackIndex = shutdown.IndexOf("await _onClassroomClosedAsync()", StringComparison.Ordinal);
    AssertTrue(callbackIndex >= 0,
        "teacher native classroom shutdown must still await the close lifecycle callback");

    string[] destructiveOperations =
    {
        "StopScreenShare();",
        "StopCamera();",
        "_studentVideoBoxes.Clear();",
        "_studentVideoLabels.Clear();",
        "_screenShareManager.Dispose();",
        "_cameraManager.Dispose();",
        "_isServerReady = false;",
        "await _server.StopAsync();"
    };

    foreach (string operation in destructiveOperations)
    {
        int operationIndex = shutdown.IndexOf(operation, StringComparison.Ordinal);
        AssertTrue(operationIndex >= 0,
            $"teacher native classroom shutdown must still perform local teardown operation: {operation}");
        AssertTrue(callbackIndex < operationIndex,
            $"teacher native classroom must notify closed before local teardown operation: {operation}");
    }
});

Run("phase 4 teacher lesson and material deletes require confirmation before delete", () =>
{
    string root = RepoRoot();
    string lessons = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherLessons.cs"));
    string materials = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherMaterials.cs"));

    AssertTeacherDeleteRequiresConfirmation(
        ExtractMethodSource(lessons, "protected override async Task DeleteAsync()"),
        "Controller.DeleteLesson",
        "teacher lesson delete");
    AssertTeacherDeleteRequiresConfirmation(
        ExtractMethodSource(materials, "protected override async Task DeleteAsync()"),
        "Controller.DeleteMaterial",
        "teacher material delete");
});

Run("phase 4 student lesson detail button honors primary action availability", () =>
{
    string studentLessons = File.ReadAllText(Path.Combine(
        RepoRoot(),
        "CourseGuard",
        "CourseGuard",
        "Frontend",
        "UserControls",
        "Student",
        "UC_StudentLessons.cs"));
    string syncDetailButton = ExtractMethodSource(studentLessons, "private void SyncDetailButton()");

    AssertTrue(syncDetailButton.Contains("view.CanUsePrimaryAction"),
        "student lesson detail button must read the presenter's CanUsePrimaryAction flag");
    AssertTrue(syncDetailButton.Contains("_detailButton.Enabled = view.CanUsePrimaryAction"),
        "student lesson detail button must be disabled when the selected item has no actionable primary action");
    AssertFalse(syncDetailButton.Contains("_detailButton.Enabled = true"),
        "student lesson detail button must not enable every selected row unconditionally");
});

Run("phase 4 classroom lifecycle visible strings use Vietnamese Unicode", () =>
{
    string root = RepoRoot();
    string teacherSchedule = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherSchedule.cs"));
    string teacherClassroom = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherNativeClassroomForm.cs"));

    string openClass = ExtractMethodSource(teacherSchedule, "private async Task OpenClassAsync()");
    AssertNoMojibakeMarkers(openClass, "teacher schedule classroom lifecycle messages");
    AssertTrue(openClass.Contains("Không thể cập nhật trạng thái buổi học sang đang mở."),
        "teacher schedule open callback must show readable Vietnamese when open-status persistence fails");
    AssertTrue(openClass.Contains("Không thể cập nhật trạng thái buổi học sang đã đóng."),
        "teacher schedule close callback must show readable Vietnamese when close-status persistence fails");
    AssertTrue(openClass.Contains("Lỗi khi vào dạy online: "),
        "teacher schedule open failure dialog must show readable Vietnamese");

    string requestClose = ExtractMethodSource(teacherClassroom, "private async Task RequestEndClassCloseAsync()");
    string formClosing = ExtractMethodSource(teacherClassroom, "private async void TeacherNativeClassroomForm_FormClosing(object? sender, FormClosingEventArgs e)");
    AssertNoMojibakeMarkers(requestClose, "teacher native classroom end-class confirmation");
    AssertNoMojibakeMarkers(formClosing, "teacher native classroom end-class error");
    AssertTrue(requestClose.Contains("Bạn có chắc chắn muốn kết thúc lớp học này không?"),
        "teacher native classroom end-class confirmation must render readable Vietnamese");
    AssertTrue(requestClose.Contains("\"Kết thúc lớp\""),
        "teacher native classroom end-class title must render readable Vietnamese");
    AssertTrue(formClosing.Contains("Không thể kết thúc lớp học: "),
        "teacher native classroom end-class failure message must render readable Vietnamese");
    AssertTrue(formClosing.Contains("\"Thông báo\""),
        "teacher native classroom end-class failure title must render readable Vietnamese");
});

Run("phase 4 student schedule tracks load generations to ignore stale schedule refreshes", () =>
{
    string root = RepoRoot();
    string studentSchedule = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Student", "UC_Schedule.cs"));
    string loadSchedule = ExtractMethodSource(studentSchedule, "private async System.Threading.Tasks.Task LoadSchedule()");

    AssertTrue(studentSchedule.Contains("_loadVersion"),
        "UC_Schedule must track load generations so overlapping refreshes cannot overwrite newer state");
    AssertTrue(loadSchedule.Contains("int loadVersion = ++_loadVersion"),
        "UC_Schedule must increment the load generation at the start of LoadSchedule");
    AssertTrue(loadSchedule.Contains("if (IsDisposed || loadVersion != _loadVersion)"),
        "UC_Schedule must ignore stale successful refreshes before rebinding rows");
    AssertTrue(loadSchedule.Contains("if (loadVersion != _loadVersion)"),
        "UC_Schedule must ignore stale failures before clearing or replacing current schedule state");
    AssertTrue(loadSchedule.Contains("if (loadVersion == _loadVersion)"),
        "UC_Schedule must hide skeletons only for the latest in-flight refresh");
    AssertTrue(studentSchedule.Contains("ClearScheduleStateAfterLoadFailure"),
        "UC_Schedule must define a focused cleanup path for latest-load failures");
    AssertTrue(loadSchedule.Contains("ClearScheduleStateAfterLoadFailure"),
        "UC_Schedule latest load failure path must clear stale schedule state instead of rebinding an empty table alone");
});

Run("phase 4 student course pages show next actions and safe detail states", () =>
{
    string root = RepoRoot();
    string courseList = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Student", "UC_CourseList.cs"));
    string myCourses = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Student", "UC_MyCourses.cs"));

    AssertTrue(courseList.Contains("StudentCourseUxPresenter.Present"),
        "UC_CourseList must use StudentCourseUxPresenter.Present for available course status/action text");
    AssertTrue(courseList.Contains("\"Hành động\""),
        "UC_CourseList table must expose an action column");
    AssertTrue(courseList.Contains("_detailAction"),
        "UC_CourseList must show selected course next action in the detail panel");

    AssertTrue(myCourses.Contains("StudentCourseUxPresenter.Present"),
        "UC_MyCourses must use StudentCourseUxPresenter.Present for enrollment status/action text");
    AssertTrue(myCourses.Contains("\"Hành động\""),
        "UC_MyCourses table must expose an action column");
    AssertTrue(myCourses.Contains("_courseNextAction"),
        "UC_MyCourses must show selected enrollment next action in the detail panel");

    string courseListClearDetail = ExtractMethodSource(courseList, "private void ClearDetailPanel");
    string courseListClearGrid = ExtractMethodSource(courseList, "private void ClearCourseGridSelection");
    string courseListSelectionChanged = ExtractMethodSource(courseList, "private void DgvCourses_SelectionChanged");
    string courseListPosition = ExtractMethodSource(courseList, "private void PositionDetailActionLabel");
    string myCoursesClearDetail = ExtractMethodSource(myCourses, "private void ClearDetailPanel");
    string myCoursesPosition = ExtractMethodSource(myCourses, "private void PositionCourseNextActionLabel");

    AssertTrue(courseListClearDetail.Contains("btnJoin.Enabled = false"),
        "UC_CourseList ClearDetailPanel must disable join while no course is selected");
    AssertTrue(courseListClearDetail.Contains("btnViewDetails.Enabled = false"),
        "UC_CourseList ClearDetailPanel must disable details while no course is selected");
    AssertTrue(courseListClearGrid.Contains("_activeCourseId = null"),
        "UC_CourseList grid selection clearing must clear the active course id");
    AssertTrue(courseListClearGrid.Contains("_activeCourseName = string.Empty"),
        "UC_CourseList grid selection clearing must clear the active course name");
    AssertTrue(courseListSelectionChanged.Contains("ClearDetailPanel()"),
        "UC_CourseList selection changed must clear detail when the current row is empty");
    AssertTrue(myCoursesClearDetail.Contains("btnViewDetail.Enabled = false"),
        "UC_MyCourses ClearDetailPanel must disable detail while no enrollment is selected");

    AssertFalse(courseList.Contains("_detailAction.Dock = DockStyle.Bottom"),
        "UC_CourseList next-action label must not be bottom-docked over designer labels");
    AssertFalse(myCourses.Contains("_courseNextAction.Dock = DockStyle.Bottom"),
        "UC_MyCourses next-action label must not be bottom-docked over designer labels");
    AssertFalse(courseListPosition.Contains("lblDetailDesc.Bottom + 8"),
        "UC_CourseList next-action label must not be placed after the current description bottom");
    AssertFalse(myCoursesPosition.Contains("lblDescription.Bottom + 8"),
        "UC_MyCourses next-action label must not be placed after the current description bottom");
    AssertTrue(courseList.Contains("DetailActionBottomPadding"),
        "UC_CourseList must keep a named bottom padding constant for the next-action label");
    AssertTrue(courseListPosition.Contains("pnlCourseDetail.ClientSize.Height - DetailActionBottomPadding - DetailActionHeight"),
        "UC_CourseList next-action label must be placed within the visible detail panel height");
    AssertTrue(courseListPosition.Contains("lblDetailDesc.Height"),
        "UC_CourseList must reserve visible space by resizing the description label");
    AssertTrue(myCourses.Contains("CourseNextActionBottomPadding"),
        "UC_MyCourses must keep a named bottom padding constant for the next-action label");
    AssertTrue(myCoursesPosition.Contains("pnlCourseInfo.ClientSize.Height - CourseNextActionBottomPadding - CourseNextActionHeight"),
        "UC_MyCourses next-action label must be placed within the visible detail panel height");
    AssertTrue(myCoursesPosition.Contains("lblDescription.Height"),
        "UC_MyCourses must reserve visible space by resizing the description label");
});

Run("phase 4 learning item presenter distinguishes lessons and materials", () =>
{
    var lesson = new TeacherLessonModel
    {
        Id = 5,
        CourseName = "Lap trinh mang",
        Title = "Socket TCP",
        PublishAt = new DateTime(2026, 6, 10, 8, 0, 0),
        FileName = "socket.pdf",
        FileSize = 2048,
        HasStoredContent = true
    };
    var material = new TeacherLessonModel
    {
        Id = -7,
        CourseName = "Lap trinh mang",
        Title = "Slide UDP",
        FileName = "udp.pdf",
        PublishAt = new DateTime(2026, 6, 11, 8, 0, 0),
        FileSize = 1024,
        HasStoredContent = true
    };

    LearningUxPresentation lessonView = StudentLearningItemUxPresenter.Present(lesson);
    LearningUxPresentation materialView = StudentLearningItemUxPresenter.Present(material);

    AssertEqual("Bài học", lessonView.StatusText);
    AssertEqual("Xem bài học", lessonView.PrimaryActionText);
    AssertEqual(NextActionKinds.OpenCurrentLesson, lessonView.NextActionKind);
    AssertEqual(5, lessonView.TargetItemId);
    AssertEqual("Tải tài liệu", materialView.PrimaryActionText);
    AssertEqual(NextActionKinds.DownloadCurrentMaterial, materialView.NextActionKind);
    AssertEqual(7, materialView.TargetItemId);
    AssertTrue(materialView.DetailText.Contains("1 KB"), "material detail should include readable size");
});

Run("phase 4 learning presenter treats DateTime.MinValue publish time as missing", () =>
{
    var lesson = new TeacherLessonModel
    {
        Id = 8,
        CourseName = "Lap trinh mang",
        Title = "Socket UDP",
        PublishAt = DateTime.MinValue,
        FileName = "udp.pdf",
        FileSize = 1024,
        HasStoredContent = true
    };

    LearningUxPresentation view = TeacherContentUxPresenter.PresentLesson(lesson);

    AssertFalse(view.DetailText.Contains("0001"), "learning item detail must not expose DateTime.MinValue");
    AssertTrue(view.DetailText.Contains("chưa rõ"), "learning item detail must render missing publish time fallback");
});

Run("phase 4 student material presenter treats DateTime.MinValue upload time as missing", () =>
{
    LearningUxPresentation view = StudentLearningItemUxPresenter.PresentMaterial(
        "Lap trinh mang",
        "missing.pdf",
        DateTime.MinValue,
        2048,
        true);

    AssertFalse(view.DetailText.Contains("0001"), "student material detail must not expose DateTime.MinValue year");
    AssertFalse(view.DetailText.Contains("01/01 00:00"), "student material detail must not expose DateTime.MinValue timestamp");
    AssertTrue(view.DetailText.Contains("chưa rõ"), "student material detail must render missing upload time fallback");
});

Run("phase 4 learning presenter hides non-positive file sizes", () =>
{
    var zeroSizeLesson = new TeacherLessonModel
    {
        Id = 9,
        CourseName = "Lap trinh mang",
        Title = "Tai lieu rong",
        PublishAt = new DateTime(2026, 6, 12, 8, 0, 0),
        FileName = "empty.pdf",
        FileSize = 0,
        HasStoredContent = true
    };
    var negativeSizeMaterial = new TeacherMaterialModel
    {
        Id = 10,
        CourseId = 3,
        CourseName = "Lap trinh mang",
        FileName = "broken.pdf",
        FileSize = -5,
        HasStoredContent = true
    };

    LearningUxPresentation zeroSizeView = StudentLearningItemUxPresenter.Present(zeroSizeLesson);
    LearningUxPresentation negativeSizeView = TeacherContentUxPresenter.PresentMaterial(negativeSizeMaterial);

    AssertTrue(zeroSizeView.DetailText.Contains("không rõ dung lượng"), "zero-byte learning item must render unknown size");
    AssertTrue(negativeSizeView.DetailText.Contains("không rõ dung lượng"), "negative-size material must render unknown size");
    AssertFalse(negativeSizeView.DetailText.Contains("-5 B"), "negative-size material must not expose a negative byte count");
});

Run("phase 4 student learning content pages show selected content context", () =>
{
    string root = RepoRoot();
    string studentLessonsSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Student", "UC_StudentLessons.cs"));
    string documentsSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Student", "UC_Documents.cs"));

    AssertTrue(studentLessonsSource.Contains("StudentLearningItemUxPresenter.Present"),
        "UC_StudentLessons must use StudentLearningItemUxPresenter for selected content context");
    AssertTrue(studentLessonsSource.Contains("_selectedItemSummary"),
        "UC_StudentLessons must define a selected item summary panel");
    AssertTrue(studentLessonsSource.Contains("UpdateSelectedItemSummary"),
        "UC_StudentLessons must update selected item summary when the selection changes");
    AssertTrue(studentLessonsSource.Contains("ClearGridSelection"),
        "UC_StudentLessons must clear both selection and current cell when switching grids");
    AssertTrue(studentLessonsSource.Contains("grid.CurrentCell = null"),
        "UC_StudentLessons grid clearing must reset CurrentCell to avoid stale CurrentRow context");
    AssertTrue(studentLessonsSource.Contains("ClearSelectedContextAfterLoadFailure"),
        "UC_StudentLessons failed refresh path must clear stale selected content context");

    AssertTrue(documentsSource.Contains("SetBelowGridContent"),
        "UC_Documents must host a preview panel below the grid");
    AssertTrue(documentsSource.Contains("_documentPreviewTitle"),
        "UC_Documents must define a document preview title label");
    AssertTrue(documentsSource.Contains("StudentLearningItemUxPresenter.PresentMaterial"),
        "UC_Documents must use StudentLearningItemUxPresenter for document preview context");
    AssertTrue(documentsSource.Contains("PrepareDocumentRefresh"),
        "UC_Documents must clear stale preview/action before refresh attempts because base preserves old rows on failure");
    AssertTrue(documentsSource.Contains("IsDocumentActionable"),
        "UC_Documents must keep the open action disabled when no stored content or usable path exists");
    AssertTrue(documentsSource.Contains("SelectedDocument"),
        "UC_Documents open path must use the backing document row instead of trusting grid cells");
    AssertFalse(documentsSource.Contains("Convert.ToBoolean(Grid.CurrentRow.Cells[\"HasContent\"].Value)"),
        "UC_Documents must not convert the HasContent grid cell without null/DBNull protection");
});

Run("phase 4 teacher content pages expose course context and selected item summaries", () =>
{
    string root = RepoRoot();
    string basePage = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "TeacherGridPageBase.cs"));
    string courses = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherCourses.cs"));
    string lessons = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherLessons.cs"));
    string materials = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherMaterials.cs"));

    AssertTrue(basePage.Contains("SetBelowGridContent"),
        "TeacherGridPageBase.cs must expose SetBelowGridContent");
    AssertTrue(basePage.Contains("_loadVersion"),
        "TeacherGridPageBase.cs must track load generation to ignore stale refresh results");
    AssertTrue(basePage.Contains("int loadVersion = ++_loadVersion"),
        "TeacherGridPageBase.cs must increment load generation at the start of LoadDataAsync");
    AssertTrue(basePage.Contains("if (loadVersion != _loadVersion)"),
        "TeacherGridPageBase.cs must ignore stale load completions before binding or reporting errors");
    AssertTrue(courses.Contains("TeacherCourseUxPresenter.Present"),
        "UC_TeacherCourses.cs must use TeacherCourseUxPresenter.Present");
    AssertTrue(lessons.Contains("_courseFilter"),
        "UC_TeacherLessons.cs must expose a course filter");
    AssertTrue(lessons.Contains("TeacherContentUxPresenter.PresentLesson"),
        "UC_TeacherLessons.cs must use TeacherContentUxPresenter.PresentLesson");
    AssertTrue(lessons.Contains("_isLoadingCourseFilter"),
        "UC_TeacherLessons.cs must suppress re-entrant loads while course filter items are being populated");
    AssertTrue(lessons.Contains("if (_isLoadingCourseFilter)"),
        "UC_TeacherLessons.cs SelectedIndexChanged path must guard against filter population re-entry");
    AssertTrue(lessons.Contains("SelectCourseFilter(selectedCourseId, suppressLoad: true)"),
        "UC_TeacherLessons.cs must restore course filter selection without relying on SelectedIndexChanged reloads");
    AssertTrue(lessons.Contains("await Task.Run"),
        "UC_TeacherLessons.cs must fetch lesson rows off the UI thread");
    AssertTrue(lessons.Contains("_lessons = rows;"),
        "UC_TeacherLessons.cs must assign cached lesson rows after the background fetch completes");
    AssertFalse(lessons.Contains("Task.FromResult(table)"),
        "UC_TeacherLessons.cs must not build the table from a synchronous Task.FromResult fetch path");
    AssertTrue(materials.Contains("TeacherContentUxPresenter.PresentMaterial"),
        "UC_TeacherMaterials.cs must use TeacherContentUxPresenter.PresentMaterial");
    AssertTrue(materials.Contains("_isLoadingCourseFilter"),
        "UC_TeacherMaterials.cs must suppress re-entrant loads while course filter items are being populated");
    AssertTrue(materials.Contains("if (_isLoadingCourseFilter)"),
        "UC_TeacherMaterials.cs SelectedIndexChanged path must guard against filter population re-entry");
    AssertTrue(materials.Contains("SelectCourseFilter(request.ParentId ?? 0, suppressLoad: true)"),
        "UC_TeacherMaterials.cs quick-search filter selection must not rely on unsuppressed SelectedIndexChanged reloads");
    AssertTrue(materials.Contains("await Task.Run"),
        "UC_TeacherMaterials.cs must fetch material rows off the UI thread");
    AssertTrue(materials.Contains("_materials = rows;"),
        "UC_TeacherMaterials.cs must assign cached material rows after the background fetch completes");
    AssertFalse(materials.Contains("Task.FromResult(table)"),
        "UC_TeacherMaterials.cs must not build the table from a synchronous Task.FromResult fetch path");
});

Run("task 8 scoped teacher lesson and material pages lock course filters to initial course", () =>
{
    string root = RepoRoot();
    string lessons = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherLessons.cs"));
    string materials = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherMaterials.cs"));

    AssertScopedTeacherContentPageLocksCourse(lessons, "UC_TeacherLessons.cs");
    AssertScopedTeacherContentPageLocksCourse(materials, "UC_TeacherMaterials.cs");
});

Run("phase 4 schedule presenter gates student and teacher classroom actions", () =>
{
    var now = new DateTime(2026, 6, 15, 9, 30, 0);
    var currentClosed = new StudentScheduleItemModel
    {
        SessionId = 1,
        CourseName = "Lap trinh mang",
        Title = "TCP",
        TeacherName = "Nguyen Van A",
        StartTime = new DateTime(2026, 6, 15, 9, 0, 0),
        EndTime = new DateTime(2026, 6, 15, 10, 0, 0),
        IsOpened = false
    };
    var currentOpen = new StudentScheduleItemModel
    {
        SessionId = 2,
        CourseName = "Lap trinh mang",
        Title = "UDP",
        TeacherName = "Nguyen Van A",
        StartTime = new DateTime(2026, 6, 15, 9, 0, 0),
        EndTime = new DateTime(2026, 6, 15, 10, 0, 0),
        IsOpened = true
    };
    var teacherFuture = new TeacherScheduleItemModel
    {
        Id = 3,
        CourseName = "Lap trinh mang",
        Title = "DNS",
        StartTime = new DateTime(2026, 6, 15, 11, 0, 0),
        EndTime = new DateTime(2026, 6, 15, 12, 0, 0)
    };

    ScheduleUxPresentation waiting = ScheduleUxPresenter.PresentStudent(currentClosed, now);
    ScheduleUxPresentation joinable = ScheduleUxPresenter.PresentStudent(currentOpen, now);
    ScheduleUxPresentation teacherBlocked = ScheduleUxPresenter.PresentTeacher(teacherFuture, now);

    AssertEqual("Chờ giáo viên mở lớp", waiting.StatusText);
    AssertEqual("Chờ mở lớp", waiting.PrimaryActionText);
    AssertFalse(waiting.CanJoin, "student cannot join until teacher opens class");
    AssertEqual("Đang mở", joinable.StatusText);
    AssertEqual("Vào lớp", joinable.PrimaryActionText);
    AssertTrue(joinable.CanJoin, "student can join an opened current class");
    AssertEqual(NextActionKinds.JoinStudentClassroom, joinable.NextActionKind);
    AssertEqual(NavigationTargets.StudentSchedule, joinable.TargetTab);
    AssertEqual(2, joinable.TargetSessionId);
    AssertEqual("Chưa đến giờ", teacherBlocked.StatusText);
    AssertFalse(teacherBlocked.CanOpenClass, "teacher cannot open a future class");
    AssertEqual(NextActionKinds.None, teacherBlocked.NextActionKind);
});

Run("phase 4 schedule presenter keeps student session joinable when end time is unknown", () =>
{
    var now = new DateTime(2026, 6, 15, 9, 30, 0);
    var currentOpen = new StudentScheduleItemModel
    {
        SessionId = 4,
        CourseName = "Lap trinh mang",
        Title = "TCP",
        TeacherName = "Nguyen Van A",
        StartTime = new DateTime(2026, 6, 15, 9, 0, 0),
        EndTime = null,
        IsOpened = true
    };

    ScheduleUxPresentation view = ScheduleUxPresenter.PresentStudent(currentOpen, now);

    AssertEqual("Đang mở", view.StatusText);
    AssertTrue(view.CanJoin, "opened student session with unknown end time must remain joinable after start");
    AssertFalse(view.IsPast, "opened student session with unknown end time must not be marked past");
    AssertEqual(NextActionKinds.JoinStudentClassroom, view.NextActionKind);
    AssertTrue(view.DetailText.Contains("chưa rõ"), "student current detail must render unknown end time fallback");
});

Run("phase 4 schedule presenter keeps teacher session openable when end time is unknown", () =>
{
    var now = new DateTime(2026, 6, 15, 9, 30, 0);
    var currentSession = new TeacherScheduleItemModel
    {
        Id = 5,
        CourseName = "Lap trinh mang",
        Title = "UDP",
        StartTime = new DateTime(2026, 6, 15, 9, 0, 0),
        EndTime = null,
        IsOpened = false
    };

    ScheduleUxPresentation view = ScheduleUxPresenter.PresentTeacher(currentSession, now);

    AssertTrue(view.CanOpenClass, "teacher session with unknown end time must remain openable after start");
    AssertFalse(view.IsPast, "teacher session with unknown end time must not be marked past");
    AssertEqual(NextActionKinds.OpenTeacherClassroom, view.NextActionKind);
    AssertTrue(view.DetailText.Contains("chưa rõ"), "teacher current detail must render unknown end time fallback");
});

Run("phase 4 classroom presenter describes connection and media state", () =>
{
    ClassroomUxPresentation disconnected = ClassroomUxPresenter.Present(
        isTeacher: false,
        isConnected: false,
        isCameraOn: false,
        isMicOn: true,
        isSharingScreen: false,
        participantCount: 0);
    ClassroomUxPresentation teacherLive = ClassroomUxPresenter.Present(
        isTeacher: true,
        isConnected: true,
        isCameraOn: true,
        isMicOn: false,
        isSharingScreen: true,
        participantCount: 12);

    AssertEqual("Mất kết nối", disconnected.StatusText);
    AssertEqual("Bật Camera", disconnected.CameraActionText);
    AssertEqual("Đang dạy trực tuyến", teacherLive.StatusText);
    AssertEqual("Tắt Camera", teacherLive.CameraActionText);
    AssertEqual("Bật Mic", teacherLive.MicActionText);
    AssertEqual("Dừng trình bày", teacherLive.ShareActionText);
    AssertTrue(teacherLive.DetailText.Contains("12 người tham gia"), "teacher detail should show participants");
});

Run("phase 4 native classroom forms use classroom presenter", () =>
{
    string root = RepoRoot();
    string studentClassroom = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "StudentNativeClassroomForm.cs"));
    string teacherClassroom = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherNativeClassroomForm.cs"));

    AssertTrue(studentClassroom.Contains("ClassroomUxPresenter.Present"), "student native classroom must use ClassroomUxPresenter.Present");
    AssertTrue(studentClassroom.Contains("_mediaStateLabel"), "student native classroom must define _mediaStateLabel");
    AssertTrue(studentClassroom.Contains("UpdateClassroomPresentation"), "student native classroom must update presenter-driven classroom copy");

    AssertTrue(teacherClassroom.Contains("ClassroomUxPresenter.Present"), "teacher native classroom must use ClassroomUxPresenter.Present");
    AssertTrue(teacherClassroom.Contains("_participantStateLabel"), "teacher native classroom must define _participantStateLabel");
    AssertTrue(teacherClassroom.Contains("UpdateClassroomPresentation"), "teacher native classroom must update presenter-driven classroom copy");
});

Run("phase 4 native classroom disconnect and invoke handling stay fresh", () =>
{
    string root = RepoRoot();
    string studentClassroom = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "StudentNativeClassroomForm.cs"));
    string teacherClassroom = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherNativeClassroomForm.cs"));

    AssertTrue(studentClassroom.Contains("Disconnected from classroom server."), "student native classroom must react to client disconnect status");
    AssertTrue(studentClassroom.Contains("_isConnectedToClassroom = false;"), "student native classroom must clear connected state on disconnect");
    AssertTrue(studentClassroom.Contains("InvokeIfRequired"), "student native classroom should use safe invoke helper instead of raw BeginInvoke paths");

    AssertTrue(teacherClassroom.Contains("SafeRemoveStudentVideo"), "teacher native classroom must keep a shared student tile cleanup path");
    AssertTrue(teacherClassroom.Contains("_server.ClientDisconnected"), "teacher native classroom must handle client disconnect events");
    AssertTrue(teacherClassroom.Contains("SafeRemoveStudentVideo(e.Client.UserId)"), "teacher native classroom must remove stale student tiles on disconnect");
    AssertTrue(teacherClassroom.Contains("private void SafeResetStageIfDisconnectedStudentWasActivePresenter"), "teacher native classroom must keep a UI-safe wrapper for presenter disconnect cleanup");
    AssertTrue(teacherClassroom.Contains("WrapContents = true"), "teacher native classroom control bar buttons should wrap at narrow widths");
    AssertTrue(teacherClassroom.Contains("InvokeIfRequired"), "teacher native classroom should use safe invoke helper instead of raw BeginInvoke paths");

    int disconnectStart = teacherClassroom.IndexOf("_server.ClientDisconnected += (_, e) =>", StringComparison.Ordinal);
    int disconnectEnd = teacherClassroom.IndexOf("_server.SignalReceived += (_, e) => HandleIncomingSignal(e.Signal);", StringComparison.Ordinal);
    AssertTrue(disconnectStart >= 0 && disconnectEnd > disconnectStart, "teacher native classroom must keep the disconnect handler source shape");

    string disconnectBlock = teacherClassroom.Substring(disconnectStart, disconnectEnd - disconnectStart);
    AssertTrue(
        disconnectBlock.Contains("SafeResetStageIfDisconnectedStudentWasActivePresenter(e.Client.UserId, e.Client.DisplayName)"),
        "teacher native classroom disconnect handler must marshal presenter reset through a safe wrapper");
    AssertFalse(
        disconnectBlock.Contains("bool resetLivePresenter"),
        "teacher native classroom disconnect handler must not rely on a stale bool captured before BeginInvoke runs");
    int removeIndex = disconnectBlock.IndexOf("SafeRemoveStudentVideo(e.Client.UserId)", StringComparison.Ordinal);
    int resetIndex = disconnectBlock.IndexOf("SafeResetStageIfDisconnectedStudentWasActivePresenter(e.Client.UserId, e.Client.DisplayName)", StringComparison.Ordinal);
    int refreshIndex = disconnectBlock.IndexOf("SafeUpdateClassroomPresentation()", StringComparison.Ordinal);
    AssertTrue(refreshIndex >= 0,
        "teacher native classroom disconnect handler must refresh presentation after every client disconnect");
    AssertTrue(refreshIndex > removeIndex && refreshIndex > resetIndex,
        "teacher native classroom disconnect presentation refresh must happen after tile and presenter cleanup are requested");
});

Run("native classroom live status survives presenter refresh until forced baseline update", RunNativeClassroomLiveStatusRefreshTests);

Run("teacher exam activation uses guarded status-only backend update", () =>
{
    string exams = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherExams.cs"));
    string controller = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Controllers", "TeacherController.cs"));
    string repository = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Data", "TeacherRepository.cs"));

    string activateSelected = ExtractMethodSource(exams, "private async Task ActivateSelectedExamAsync");
    string addAsync = ExtractMethodSource(exams, "protected override async Task AddAsync");

    AssertTrue(activateSelected.Contains("Controller.ActivateExam"), "teacher exam activation must use the guarded status-only activation API");
    AssertFalse(activateSelected.Contains("Controller.UpdateExam"), "teacher exam activation must not write stale grid metadata through UpdateExam");
    AssertTrue(addAsync.Contains("Status = WorkflowConstants.ExamStatus.Draft"), "new exams must be created as DRAFT regardless of dialog state");

    AssertTrue(controller.Contains("ActivateExam(int teacherId, int examId)"), "teacher controller must expose ActivateExam");
    AssertTrue(repository.Contains("ActivateExam(int teacherId, int examId)"), "teacher repository must expose ActivateExam");

    string repositoryActivation = ExtractMethodSource(repository, "public bool ActivateExam");
    AssertTrue(repositoryActivation.Contains("COALESCE(ex.status, '')"), "repository activation must require explicit DRAFT status instead of treating NULL as draft");
    AssertFalse(repositoryActivation.Contains("COALESCE(ex.status, 'DRAFT')"), "repository activation must not treat NULL status as draft");
    AssertTrue(repositoryActivation.Contains("UPDATE exams ex"), "repository activation must update the exams table directly");
    AssertTrue(repositoryActivation.Contains("@active_status"), "repository activation must parameterize active status");
    AssertTrue(repositoryActivation.Contains("@draft_status"), "repository activation must guard current draft status");
    AssertTrue(repositoryActivation.Contains("c.teacher_id = @teacher_id"), "repository activation must guard teacher ownership");
    AssertTrue(repositoryActivation.Contains("ex.id = @exam_id"), "repository activation must target the selected exam");
    AssertTrue(repositoryActivation.Contains("EXISTS"), "repository activation must require at least one question");
    AssertFalse(repositoryActivation.Contains("title ="), "repository activation must not update exam title metadata");
    AssertFalse(repositoryActivation.Contains("open_time ="), "repository activation must not update exam schedule metadata");
    AssertFalse(repositoryActivation.Contains("AddExamParameters"), "repository activation must not reuse full exam metadata parameters");
});

Run("teacher exam metadata update cannot activate draft exams", () =>
{
    string repository = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Data", "TeacherRepository.cs"));
    string updateExam = ExtractMethodSource(repository, "public bool UpdateExam");

    AssertTrue(updateExam.Contains("CASE"), "repository update must compute status with a guarded CASE");
    AssertTrue(updateExam.Contains("@active_status"), "repository update must know the active status explicitly");
    AssertTrue(updateExam.Contains("@closed_status"), "repository update must allow closed status explicitly");
    AssertTrue(updateExam.Contains("@draft_status"), "repository update must know the draft status explicitly");
    AssertTrue(
        updateExam.Contains("WHEN @status = @closed_status THEN @closed_status"),
        "repository update must still allow explicit close requests");
    AssertTrue(
        updateExam.Contains("UPPER(COALESCE(ex.status, '')) = @active_status"),
        "repository update must only preserve ACTIVE when the current DB row is already ACTIVE");
    AssertTrue(
        updateExam.Contains("UPPER(COALESCE(ex.status, '')) = @closed_status"),
        "repository update must preserve CLOSED when caller status is DRAFT, blank, or invalid");
    AssertTrue(
        updateExam.Contains("UPPER(COALESCE(ex.status, '')) = @draft_status"),
        "repository update must keep existing DRAFT exams as DRAFT");
    AssertTrue(
        updateExam.Contains("ELSE COALESCE(ex.status, '')"),
        "repository update must preserve unknown or NULL current status instead of defaulting to DRAFT");
    AssertFalse(
        updateExam.Contains("ELSE @draft_status"),
        "repository update must not downgrade existing non-DRAFT or NULL status to DRAFT on metadata updates");
    AssertFalse(
        updateExam.Contains("COALESCE(ex.status, 'DRAFT')"),
        "repository update must not treat NULL current status as DRAFT");
    AssertFalse(updateExam.Contains("status = @status"), "repository update must not blindly write caller status");
});

Run("teacher exam create and import enforce draft ownership guards", () =>
{
    string repository = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Data", "TeacherRepository.cs"));
    string controller = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Controllers", "TeacherController.cs"));
    string dbContext = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Data", "CourseGuardDbContext.cs"));
    string questionDialog = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherExamQuestionsDialog.cs"));

    string addExamParameters = ExtractMethodSource(repository, "private static void AddExamParameters");
    AssertTrue(
        addExamParameters.Contains("includeId ? NormalizeExamStatus(input.Status) : WorkflowConstants.ExamStatus.Draft"),
        "repository create path must force new exams to DRAFT independent of caller status");

    string canActivateExam = ExtractMethodSource(repository, "public bool CanActivateExam");
    AssertTrue(canActivateExam.Contains("@draft_status"), "CanActivateExam must guard current draft status");
    AssertTrue(canActivateExam.Contains("COALESCE(ex.status, '')"), "CanActivateExam must require explicit DRAFT status instead of treating NULL as draft");
    AssertFalse(canActivateExam.Contains("COALESCE(ex.status, 'DRAFT')"), "CanActivateExam must not treat NULL status as draft");
    AssertTrue(canActivateExam.Contains("EXISTS"), "CanActivateExam must require at least one question");
    AssertTrue(canActivateExam.Contains("c.teacher_id = @teacher_id"), "CanActivateExam must guard teacher ownership");
    AssertTrue(canActivateExam.Contains("ex.id = @exam_id"), "CanActivateExam must target the selected exam");

    string controllerImport = ExtractMethodSource(controller, "public async System.Threading.Tasks.Task<int> ImportQuestionsToExamAsync");
    AssertTrue(controllerImport.Contains("BulkInsertQuestionsAndMapToExamAsync(teacherId, examId, courseId, questions)"), "controller import must pass teacherId to guarded DB import");

    string dbImport = ExtractMethodSource(dbContext, "public async Task<int> BulkInsertQuestionsAndMapToExamAsync");
    AssertTrue(dbImport.Contains("int teacherId, int examId"), "DB bulk import must accept teacherId");
    AssertTrue(dbImport.Contains("c.teacher_id = @teacher_id"), "DB bulk import must guard teacher ownership");
    AssertTrue(dbImport.Contains("ex.id = @exam_id"), "DB bulk import must guard selected exam id");
    AssertTrue(dbImport.Contains("ex.course_id = @course_id"), "DB bulk import must guard selected course id");
    AssertTrue(dbImport.Contains("@draft_status"), "DB bulk import must guard current draft status");
    AssertTrue(dbImport.Contains("COALESCE(ex.status, '')"), "DB bulk import must require explicit DRAFT status instead of treating NULL as draft");
    AssertFalse(dbImport.Contains("COALESCE(ex.status, 'DRAFT')"), "DB bulk import must not treat NULL status as draft");
    AssertTrue(dbImport.Contains("return 0;"), "DB bulk import must return zero when guard fails");
    AssertTrue(dbImport.Contains("return insertedQuestionIds.Count;"), "DB bulk import must return imported count");

    string importDialog = ExtractMethodSource(questionDialog, "private async System.Threading.Tasks.Task ImportFromExcelAsync");
    AssertTrue(importDialog.Contains("int importedCount"), "question dialog import must read imported count");
    AssertTrue(importDialog.Contains("importedCount <= 0"), "question dialog import must handle guarded import rejection");
    AssertTrue(importDialog.Contains("LoadQuestions();"), "question dialog import must refresh after import rejection or success");
    AssertTrue(importDialog.Contains("Nhập thành công {importedCount}"), "question dialog import success must report returned imported count");
    AssertFalse(importDialog.Contains("Nhập thành công {questions.Count}"), "question dialog import must not always report input question count");
});

Run("teacher question editing refreshes guarded editability and reports no-op writes", () =>
{
    string questionDialog = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherExamQuestionsDialog.cs"));

    AssertFalse(questionDialog.Contains("private readonly bool _canEdit"), "question dialog editability must be mutable after backend guard rejects writes");
    AssertTrue(questionDialog.Contains("RefreshEditState"), "question dialog must refresh editability from backend status");
    AssertTrue(questionDialog.Contains("ShowGuardedQuestionWriteFailure"), "question dialog must show a guarded no-op write warning");

    string importDialog = ExtractMethodSource(questionDialog, "private async System.Threading.Tasks.Task ImportFromExcelAsync");
    AssertTrue(importDialog.Contains("RefreshEditState"), "import rejection must refresh editability after guarded no-op");

    string saveNew = ExtractMethodSource(questionDialog, "private void SaveNewQuestion");
    AssertTrue(saveNew.Contains("RefreshEditState"), "create question must refresh editability before writing");
    AssertTrue(saveNew.Contains("int createdId"), "create question must read backend create result");
    AssertTrue(saveNew.Contains("createdId <= 0"), "create question must handle guarded create no-op");
    AssertTrue(saveNew.Contains("ShowGuardedQuestionWriteFailure"), "create question no-op must show a guarded warning");

    string saveExisting = ExtractMethodSource(questionDialog, "private void SaveExistingQuestion");
    AssertTrue(saveExisting.Contains("RefreshEditState"), "update question must refresh editability before writing");
    AssertTrue(saveExisting.Contains("bool updated"), "update question must read backend update result");
    AssertTrue(saveExisting.Contains("!updated"), "update question must handle guarded update no-op");
    AssertTrue(saveExisting.Contains("ShowGuardedQuestionWriteFailure"), "update question no-op must show a guarded warning");

    string deleteSelected = ExtractMethodSource(questionDialog, "private void DeleteSelectedQuestion");
    AssertTrue(deleteSelected.Contains("RefreshEditState"), "delete question must refresh editability before writing");
    AssertTrue(deleteSelected.Contains("bool deleted"), "delete question must read backend delete result");
    AssertTrue(deleteSelected.Contains("!deleted"), "delete question must handle guarded delete no-op");
    AssertTrue(deleteSelected.Contains("ShowGuardedQuestionWriteFailure"), "delete question no-op must show a guarded warning");
});

Run("teacher question write guards require explicit draft status", () =>
{
    string repository = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Data", "TeacherRepository.cs"));
    string controller = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Controllers", "TeacherController.cs"));

    AssertExplicitDraftQuestionGuard(repository, "public int CreateExamQuestion");
    AssertExplicitDraftQuestionGuard(repository, "public bool UpdateExamQuestion");
    AssertExplicitDraftQuestionGuard(repository, "public bool DeleteExamQuestion");

    string getExamStatus = ExtractMethodSource(repository, "public string GetExamStatus");
    AssertFalse(getExamStatus.Contains("COALESCE(ex.status, 'DRAFT')"), "GetExamStatus must not treat NULL status as DRAFT");
    AssertFalse(getExamStatus.Contains("?? WorkflowConstants.ExamStatus.Draft"), "GetExamStatus must not treat missing rows as DRAFT");
    AssertTrue(getExamStatus.Contains("SELECT ex.status"), "GetExamStatus must return the stored status value directly");

    string queryExams = ExtractMethodSource(repository, "private List<TeacherExamModel> QueryExams");
    AssertFalse(queryExams.Contains("COALESCE(ex.status, 'DRAFT')"), "teacher exam list must not surface NULL status as draft-ready");
    AssertTrue(queryExams.Contains("COALESCE(ex.status, '')"), "teacher exam list must surface NULL status as a neutral empty status");

    string controllerGetExamStatus = ExtractMethodSource(controller, "public string GetExamStatus");
    AssertFalse(
        controllerGetExamStatus.Contains("WorkflowConstants.ExamStatus.Draft"),
        "controller GetExamStatus invalid-id fallback must not return DRAFT");
    AssertTrue(
        controllerGetExamStatus.Contains("string.Empty"),
        "controller GetExamStatus invalid-id fallback must return a neutral empty status");
    AssertTrue(
        controllerGetExamStatus.Contains("_repository.GetExamStatus(teacherId, examId)"),
        "controller GetExamStatus valid ids must delegate to repository status lookup");
});

Run("teacher question bank and random adds report guarded insertion results", () =>
{
    string controller = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Controllers", "TeacherController.cs"));
    string dbContext = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Data", "CourseGuardDbContext.cs"));
    string questionBankDialog = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "QuestionBankDialog.cs"));
    string randomDialog = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "RandomExamBuilderDialog.cs"));

    AssertTrue(controller.Contains("Task<int> AddQuestionsFromBankAsync"), "controller bank add must return inserted count");
    AssertTrue(dbContext.Contains("public async Task<int> AddQuestionsFromBankAsync"), "DB bank add must return inserted count");
    AssertTrue(controller.Contains("Task<int> AddRandomQuestionsToExamAsync"), "controller random add must return inserted count");
    AssertTrue(dbContext.Contains("public async Task<int> AddRandomQuestionsToExamAsync"), "DB random add must return inserted count");
    AssertTrue(dbContext.Contains("private static async Task<int> InsertQuestionBankSnapshotsAsync"), "question bank snapshot helper must return inserted count");

    string controllerBankAdd = ExtractMethodSource(controller, "public async System.Threading.Tasks.Task<int> AddQuestionsFromBankAsync");
    AssertTrue(controllerBankAdd.Contains("return 0;"), "controller bank add guard failures must return zero");
    AssertTrue(controllerBankAdd.Contains("return await _dbContext.AddQuestionsFromBankAsync"), "controller bank add must return DB inserted count");

    string dbBankAdd = ExtractMethodSource(dbContext, "public async Task<int> AddQuestionsFromBankAsync");
    AssertTrue(dbBankAdd.Contains("COALESCE(status, '')"), "DB bank add must require explicit DRAFT status");
    AssertTrue(dbBankAdd.Contains("return 0;"), "DB bank add no-ops must return zero");
    AssertTrue(dbBankAdd.Contains("int insertedCount"), "DB bank add must capture inserted snapshot count");
    AssertTrue(dbBankAdd.Contains("return insertedCount;"), "DB bank add must return inserted snapshot count");

    string addSelected = ExtractMethodSource(questionBankDialog, "private async System.Threading.Tasks.Task AddSelectedQuestionsAsync");
    AssertTrue(addSelected.Contains("int addedCount"), "question bank dialog must read inserted count");
    AssertTrue(addSelected.Contains("addedCount <= 0"), "question bank dialog must handle guarded no-op");
    AssertTrue(addSelected.Contains("ShowGuardedQuestionAddFailure"), "question bank dialog must warn on guarded no-op");
    AssertTrue(addSelected.Contains("addedCount > 0"), "question bank dialog must only mark success for positive insert count");

    string addRandom = ExtractMethodSource(randomDialog, "private async System.Threading.Tasks.Task AddQuestionsAsync");
    AssertTrue(addRandom.Contains("int addedCount"), "random builder dialog must read inserted count");
    AssertTrue(addRandom.Contains("addedCount <= 0"), "random builder dialog must handle guarded no-op");
    AssertTrue(addRandom.Contains("ShowGuardedQuestionAddFailure"), "random builder dialog must warn on guarded no-op");
    AssertTrue(addRandom.Contains("addedCount > 0"), "random builder dialog must only mark success for positive insert count");
});

Run("activity display helper translates known actions and appends cleaned details", () =>
{
    var activity = new RecentUserActivityModel
    {
        Action = "chat_use",
        Details = "  Lop NT106 - nhom 1  "
    };

    AssertEqual(
        "Trao \u0111\u1ed5i trong l\u1edbp h\u1ecdc - Lop NT106 - nhom 1",
        ActivityDisplayHelper.TranslateActivity(activity));
});

Run("activity display helper preserves student default wording", () =>
{
    AssertEqual(
        "\u0110\u0103ng nh\u1eadp h\u1ec7 th\u1ed1ng",
        ActivityDisplayHelper.TranslateActivity(new RecentUserActivityModel { Action = "LOGIN" }));
    AssertEqual(
        "\u0110\u0103ng xu\u1ea5t h\u1ec7 th\u1ed1ng",
        ActivityDisplayHelper.TranslateActivity(new RecentUserActivityModel { Action = "LOGOUT" }));
    AssertEqual(
        "\u0110\u1ed5i m\u1eadt kh\u1ea9u",
        ActivityDisplayHelper.TranslateActivity(new RecentUserActivityModel { Action = "CHANGE_PASSWORD" }));
    AssertEqual(
        "G\u1eedi y\u00eau c\u1ea7u tham gia kh\u00f3a h\u1ecdc",
        ActivityDisplayHelper.TranslateActivity(new RecentUserActivityModel { Action = "COURSE_ENROLL_REQUEST" }));
    AssertEqual(
        "B\u1eaft \u0111\u1ea7u l\u00e0m b\u00e0i ki\u1ec3m tra",
        ActivityDisplayHelper.TranslateActivity(new RecentUserActivityModel { Action = "EXAM_JOIN" }));
    AssertEqual(
        "C\u1eadp nh\u1eadt ho\u1ea1t \u0111\u1ed9ng",
        ActivityDisplayHelper.TranslateActivity(new RecentUserActivityModel { Action = "UNKNOWN_TEACHER_EVENT" }));
});

Run("activity display helper preserves teacher context wording", () =>
{
    AssertEqual(
        "\u0110\u00e3 \u0111\u0103ng nh\u1eadp v\u00e0o h\u1ec7 th\u1ed1ng",
        ActivityDisplayHelper.TranslateActivity(
            new RecentUserActivityModel { Action = "LOGIN" },
            ActivityDisplayContext.Teacher));
    AssertEqual(
        "\u0110\u00e3 \u0111\u0103ng xu\u1ea5t kh\u1ecfi h\u1ec7 th\u1ed1ng",
        ActivityDisplayHelper.TranslateActivity(
            new RecentUserActivityModel { Action = "LOGOUT" },
            ActivityDisplayContext.Teacher));
    AssertEqual(
        "\u0110\u00e3 \u0111\u1ed5i m\u1eadt kh\u1ea9u",
        ActivityDisplayHelper.TranslateActivity(
            new RecentUserActivityModel { Action = "CHANGE_PASSWORD" },
            ActivityDisplayContext.Teacher));
    AssertEqual(
        "\u0110\u00e3 trao \u0111\u1ed5i v\u1edbi h\u1ecdc vi\u00ean",
        ActivityDisplayHelper.TranslateActivity(
            new RecentUserActivityModel { Action = "CHAT_USE" },
            ActivityDisplayContext.Teacher));
    AssertEqual(
        "\u0110\u00e3 c\u1eadp nh\u1eadt ho\u1ea1t \u0111\u1ed9ng gi\u1ea3ng d\u1ea1y",
        ActivityDisplayHelper.TranslateActivity(
            new RecentUserActivityModel { Action = "UNKNOWN_TEACHER_EVENT" },
            ActivityDisplayContext.Teacher));
});

Run("activity display helper cleans blank and long details", () =>
{
    string longDetails = new string('a', 80);

    AssertEqual(string.Empty, ActivityDisplayHelper.CleanDetails("   "));
    AssertEqual(new string('a', 72) + "...", ActivityDisplayHelper.CleanDetails(longDetails));
});

Run("activity display helper safely reads metric counts", () =>
{
    AssertEqual(7, ActivityDisplayHelper.SafeMetricCount(() => 7));
    AssertEqual(0, ActivityDisplayHelper.SafeMetricCount(() => -3));
    AssertEqual(0, ActivityDisplayHelper.SafeMetricCount(() => throw new InvalidOperationException("boom")));
});

Run("activity display helper safely reads nullable averages", () =>
{
    AssertEqual(8.5, ActivityDisplayHelper.SafeAverageScore(() => 8.5));
    AssertEqual<double?>(null, ActivityDisplayHelper.SafeAverageScore(() => null));
    AssertEqual<double?>(null, ActivityDisplayHelper.SafeAverageScore(() => throw new InvalidOperationException("boom")));
});

Run("activity display helper safely reads lists", () =>
{
    List<int> values = ActivityDisplayHelper.SafeList(() => new List<int> { 1, 2 });
    List<int> nullValues = ActivityDisplayHelper.SafeList<int>(() => null!);
    List<int> failedValues = ActivityDisplayHelper.SafeList<int>(() => throw new InvalidOperationException("boom"));

    AssertEqual(2, values.Count);
    AssertEqual(0, nullValues.Count);
    AssertEqual(0, failedValues.Count);
});

Run("classroom frame helper resize preserves aspect ratio within bounds", () =>
{
    using var wide = new Bitmap(400, 200);
    using var tall = new Bitmap(100, 300);

    using Bitmap resizedWide = ClassroomFrameHelper.ResizeFrame(wide, 120, 120);
    using Bitmap resizedTall = ClassroomFrameHelper.ResizeFrame(tall, 120, 120);

    AssertEqual(new Size(120, 60), resizedWide.Size);
    AssertEqual(new Size(40, 120), resizedTall.Size);
    AssertTrue(resizedWide.Width <= 120 && resizedWide.Height <= 120, "wide resize must stay inside max bounds");
    AssertTrue(resizedTall.Width <= 120 && resizedTall.Height <= 120, "tall resize must stay inside max bounds");
});

Run("classroom frame helper jpeg encode decode round trips a small bitmap", () =>
{
    using var source = new Bitmap(8, 6);
    using (Graphics graphics = Graphics.FromImage(source))
    {
        graphics.Clear(Color.CornflowerBlue);
        graphics.FillRectangle(Brushes.IndianRed, 2, 1, 4, 3);
    }

    string encoded = ClassroomFrameHelper.EncodeJpegFrame(source, 80L);
    using Bitmap decoded = ClassroomFrameHelper.DecodeFrame(encoded);

    AssertEqual(source.Size, decoded.Size);
    AssertTrue(decoded.Width > 0 && decoded.Height > 0, "decoded frame must be a usable bitmap");
});

Run("classroom frame helper replace image transfers ownership and disposes old image", () =>
{
    using var target = new PictureBox();
    var oldImage = new Bitmap(2, 2);
    var newImage = new Bitmap(3, 3);
    target.Image = oldImage;

    bool replaced = ClassroomFrameHelper.TryReplaceImage(target, newImage);

    AssertTrue(replaced, "replacement should succeed for live picture box");
    AssertTrue(ReferenceEquals(newImage, target.Image), "picture box should own the replacement image");
    AssertImageDisposed(oldImage, "old image should be disposed after replacement");

    target.Image = null;
    newImage.Dispose();
});

Run("classroom frame helper replace image disposes dropped image", () =>
{
    var droppedImage = new Bitmap(2, 2);
    using var target = new PictureBox();
    target.Dispose();

    bool replaced = ClassroomFrameHelper.TryReplaceImage(target, droppedImage);

    AssertFalse(replaced, "replacement should fail for disposed picture box");
    AssertImageDisposed(droppedImage, "dropped image should be disposed when replacement fails");
});

Run("classroom screen share manager normalizes empty bounds to available screen area", () =>
{
    Rectangle fallback = new Rectangle(10, 20, 300, 200);

    Rectangle normalized = ClassroomScreenShareManager.NormalizeBounds(Rectangle.Empty, fallback);
    Rectangle explicitBounds = ClassroomScreenShareManager.NormalizeBounds(new Rectangle(1, 2, 3, 4), fallback);

    AssertEqual(fallback, normalized);
    AssertEqual(new Rectangle(1, 2, 3, 4), explicitBounds);
});

Run("classroom open signal coordinator starts lazily and broadcasts selected session", async () =>
{
    var signalService = new FakeClassroomSignalService();
    var coordinator = new ClassroomOpenSignalCoordinator(signalService, replayCount: 0);

    AssertEqual(0, signalService.StartCount);

    await coordinator.BroadcastClassOpenedAsync(42);
    await coordinator.BroadcastClassOpenedAsync(43);

    AssertEqual(1, signalService.StartCount);
    AssertEqual("42,43", string.Join(",", signalService.BroadcastSessionIds));
});

Run("classroom open signal coordinator replays open signal for reconnecting clients", async () =>
{
    var signalService = new FakeClassroomSignalService();
    var coordinator = new ClassroomOpenSignalCoordinator(signalService, replayCount: 2, replayDelay: TimeSpan.FromMilliseconds(20));

    await coordinator.BroadcastClassOpenedAsync(42);

    AssertEqual(1, signalService.StartCount);
    AssertEqual("42", string.Join(",", signalService.BroadcastSessionIds));
    AssertTrue(signalService.WaitForBroadcastCount(3, TimeSpan.FromSeconds(2)), "open signal replay was not observed");
    AssertEqual("42,42,42", string.Join(",", signalService.BroadcastSessionIds));
});

Run("classroom open signal coordinator suppresses pending open replays after close", async () =>
{
    var signalService = new FakeClassroomSignalService();
    var coordinator = new ClassroomOpenSignalCoordinator(signalService, replayCount: 2, replayDelay: TimeSpan.FromMilliseconds(20));

    await coordinator.BroadcastClassOpenedAsync(42).ConfigureAwait(false);
    await coordinator.BroadcastClassClosedAsync(42).ConfigureAwait(false);
    await Task.Delay(120).ConfigureAwait(false);

    AssertEqual(1, signalService.StartCount);
    AssertEqual("42", string.Join(",", signalService.BroadcastSessionIds));
    AssertEqual("42", string.Join(",", signalService.ClosedBroadcastSessionIds));
});

Run("classroom open signal coordinator retries start after failure without broadcasting", async () =>
{
    var signalService = new FakeClassroomSignalService { ThrowOnStartListening = true };
    var coordinator = new ClassroomOpenSignalCoordinator(signalService, replayCount: 0);

    bool threw = false;
    try
    {
        await coordinator.BroadcastClassOpenedAsync(42);
    }
    catch (InvalidOperationException)
    {
        threw = true;
    }

    AssertTrue(threw, "first broadcast should fail when listener start fails");
    AssertEqual(1, signalService.StartCount);
    AssertEqual(string.Empty, string.Join(",", signalService.BroadcastSessionIds));

    signalService.ThrowOnStartListening = false;
    await coordinator.BroadcastClassOpenedAsync(43);

    AssertEqual(2, signalService.StartCount);
    AssertEqual("43", string.Join(",", signalService.BroadcastSessionIds));
});

Run("classroom open signal coordinator starts once for concurrent broadcasts", async () =>
{
    using var startEntered = new ManualResetEventSlim();
    using var allowStartListening = new ManualResetEventSlim();
    using var secondCallStarted = new ManualResetEventSlim();

    var signalService = new FakeClassroomSignalService
    {
        OnStartListening = () =>
        {
            startEntered.Set();
            AssertTrue(allowStartListening.Wait(TimeSpan.FromSeconds(3)), "start listener delay was not released");
        }
    };
    var coordinator = new ClassroomOpenSignalCoordinator(signalService, replayCount: 0);

    Task first = Task.Run(() => coordinator.BroadcastClassOpenedAsync(42));
    Thread? secondThread = null;
    Exception? secondFailure = null;
    bool secondJoined = false;
    try
    {
        AssertTrue(startEntered.Wait(TimeSpan.FromSeconds(3)), "listener start was not attempted");

        secondThread = new Thread(() =>
        {
            try
            {
                secondCallStarted.Set();
                coordinator.BroadcastClassOpenedAsync(43).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                secondFailure = ex;
            }
        });
        secondThread.IsBackground = true;
        secondThread.Start();
        AssertTrue(secondCallStarted.Wait(TimeSpan.FromSeconds(3)), "second broadcast call did not start");
        AssertTrue(
            SpinWait.SpinUntil(
                () => (secondThread.ThreadState & ThreadState.WaitSleepJoin) != 0,
                TimeSpan.FromSeconds(3)),
            "second broadcast did not block while listener start was held");

        allowStartListening.Set();

        await first.WaitAsync(TimeSpan.FromSeconds(3));
        secondJoined = secondThread.Join(TimeSpan.FromSeconds(3));
    }
    finally
    {
        allowStartListening.Set();
        if (secondThread is { IsAlive: true })
            secondJoined = secondThread.Join(TimeSpan.FromSeconds(3));
    }

    AssertTrue(secondJoined, "second broadcast thread did not finish");
    if (secondFailure != null)
        throw secondFailure;

    int[] broadcastIds = signalService.BroadcastSessionIds.OrderBy(id => id).ToArray();
    AssertEqual(1, signalService.StartCount);
    AssertEqual("42,43", string.Join(",", broadcastIds));
});

Run("phase 4 classroom signal stack supports broadcasting closed status", () =>
{
    string root = RepoRoot();
    string signalInterface = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Backend", "Services", "Realtime", "IClassroomSignalService.cs"));
    string coordinator = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Helpers", "ClassroomOpenSignalCoordinator.cs"));
    string tcpService = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Backend", "Services", "Realtime", "TcpClassroomService.cs"));

    AssertTrue(signalInterface.Contains("Task BroadcastClassClosed(int sessionId);"),
        "IClassroomSignalService must expose a closed-status broadcast");
    AssertTrue(coordinator.Contains("BroadcastClassClosedAsync"),
        "ClassroomOpenSignalCoordinator must provide a close broadcast entry point");

    string closeBroadcast = ExtractMethodSource(tcpService, "public async Task BroadcastClassClosed(int sessionId)");
    AssertTrue(closeBroadcast.Contains("payload[0] = 0"),
        "TcpClassroomService must encode classroom close as payload[0] = 0 so existing clients interpret the session as closed");
});

Run("student exam form constructor does not invoke before handle exists", () =>
{
    Environment.SetEnvironmentVariable("COURSEGUARD_DB_CONNECTION", "Host=localhost;Username=test;Password=test;Database=test");
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            using var form = new DoExamForm(0);
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
});

Run("student exam progress and save labels fit inside header", () =>
{
    Environment.SetEnvironmentVariable("COURSEGUARD_DB_CONNECTION", "Host=localhost;Username=test;Password=test;Database=test");
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            using var form = new DoExamForm(0);
            Panel header = GetFieldValue<Panel>(form, "pnlHeader");
            Label progressLabel = GetFieldValue<Label>(form, "_progressLabel");
            Label saveStatusLabel = GetFieldValue<Label>(form, "_saveStatusLabel");

            AssertTrue(header.Controls.Contains(progressLabel), "progress label must be in the exam header");
            AssertTrue(header.Controls.Contains(saveStatusLabel), "save status label must be in the exam header");
            AssertTrue(
                progressLabel.Bottom <= header.Height,
                $"progress label must fit inside header: bottom={progressLabel.Bottom}, headerHeight={header.Height}");
            AssertTrue(
                saveStatusLabel.Bottom <= header.Height,
                $"save status label must fit inside header: bottom={saveStatusLabel.Bottom}, headerHeight={header.Height}");
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
});

Run("student exam answer save failures update inline status without persisting selection", () =>
{
    MethodInfo? saveSelectedAnswer = typeof(DoExamForm).GetMethod(
        "SaveSelectedAnswer",
        BindingFlags.Instance | BindingFlags.NonPublic);
    AssertTrue(saveSelectedAnswer != null, "DoExamForm must keep SaveSelectedAnswer as the answer-save path");

    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "DoExamForm.cs"));
    string methodSource = ExtractMethodSource(source, "private void SaveSelectedAnswer");
    int saveCallIndex = methodSource.IndexOf("SaveStudentExamAnswer", StringComparison.Ordinal);
    int assignmentIndex = methodSource.IndexOf("question.SelectedOption = option", StringComparison.Ordinal);
    int successBlockIndex = methodSource.IndexOf("if (saved)", StringComparison.Ordinal);
    int catchIndex = methodSource.IndexOf("catch", StringComparison.Ordinal);
    int returnedFalseStatusIndex = methodSource.LastIndexOf("UpdateSaveStatus(false)", StringComparison.Ordinal);

    AssertTrue(saveCallIndex >= 0, "SaveSelectedAnswer must persist through SaveStudentExamAnswer");
    AssertTrue(successBlockIndex >= 0, "SaveSelectedAnswer must keep the session mutation in the saved-only branch");
    AssertTrue(assignmentIndex > saveCallIndex, "SelectedOption must not be mutated before the DB save succeeds");
    AssertTrue(assignmentIndex > successBlockIndex, "SelectedOption must be assigned only inside the saved-only branch");
    AssertTrue(catchIndex > saveCallIndex, "SaveSelectedAnswer must catch DB save exceptions near the save path");
    AssertTrue(
        methodSource.Substring(catchIndex).Contains("UpdateSaveStatus(false)", StringComparison.Ordinal),
        "exception path must update the inline save status as failed");
    AssertTrue(
        returnedFalseStatusIndex > assignmentIndex,
        "returned-false save failures must update the inline save status as failed");
});

Run("fire and forget safe with null owner does not throw synchronously", () =>
{
    int reporterCallCount = 0;
    Exception? observedException = null;
    using var observed = new ManualResetEventSlim();
    Task failedTask = Task.FromException(new InvalidOperationException("boom"));
    SynchronizationContext? priorContext = SynchronizationContext.Current;

    CourseGuard.Frontend.Helpers.TaskExtensions.ErrorReporter = (_, _, _, _) => reporterCallCount++;
    CourseGuard.Frontend.Helpers.TaskExtensions.ObservationCompleted = exception =>
    {
        observedException = exception;
        observed.Set();
    };

    try
    {
        SynchronizationContext.SetSynchronizationContext(null);
        failedTask.FireAndForgetSafe(null);

        AssertTrue(observed.Wait(TimeSpan.FromSeconds(3)), "null owner failed task was not observed");
        AssertTrue(observedException is InvalidOperationException, "null owner should observe original exception");
        AssertEqual(0, reporterCallCount);
    }
    finally
    {
        SynchronizationContext.SetSynchronizationContext(priorContext);
        CourseGuard.Frontend.Helpers.TaskExtensions.ErrorReporter = null;
        CourseGuard.Frontend.Helpers.TaskExtensions.ObservationCompleted = null;
    }
});

Run("fire and forget safe with disposed uncreated control does not throw", () =>
{
    using var owner = new Control();
    owner.Dispose();
    int reporterCallCount = 0;
    Exception? observedException = null;
    using var observed = new ManualResetEventSlim();
    Task failedTask = Task.FromException(new InvalidOperationException("boom"));
    SynchronizationContext? priorContext = SynchronizationContext.Current;

    CourseGuard.Frontend.Helpers.TaskExtensions.ErrorReporter = (_, _, _, _) => reporterCallCount++;
    CourseGuard.Frontend.Helpers.TaskExtensions.ObservationCompleted = exception =>
    {
        observedException = exception;
        observed.Set();
    };

    try
    {
        SynchronizationContext.SetSynchronizationContext(null);
        failedTask.FireAndForgetSafe(owner);

        AssertTrue(observed.Wait(TimeSpan.FromSeconds(3)), "disposed owner failed task was not observed");
        AssertTrue(observedException is InvalidOperationException, "disposed owner should observe original exception");
        AssertEqual(0, reporterCallCount);
    }
    finally
    {
        SynchronizationContext.SetSynchronizationContext(priorContext);
        CourseGuard.Frontend.Helpers.TaskExtensions.ErrorReporter = null;
        CourseGuard.Frontend.Helpers.TaskExtensions.ObservationCompleted = null;
    }
});

Run("fire and forget safe observes canceled task without reporting", () =>
{
    int reporterCallCount = 0;
    Exception? observedException = new InvalidOperationException("not observed");
    using var observed = new ManualResetEventSlim();
    using var cts = new CancellationTokenSource();
    var context = new RecordingSynchronizationContext();
    SynchronizationContext? priorContext = SynchronizationContext.Current;

    cts.Cancel();
    CourseGuard.Frontend.Helpers.TaskExtensions.ErrorReporter = (_, _, _, _) => reporterCallCount++;
    CourseGuard.Frontend.Helpers.TaskExtensions.ObservationCompleted = exception =>
    {
        observedException = exception;
        observed.Set();
    };

    try
    {
        SynchronizationContext.SetSynchronizationContext(context);
        Task.FromCanceled(cts.Token).FireAndForgetSafe(null);

        AssertTrue(observed.Wait(TimeSpan.FromSeconds(3)), "canceled task was not observed");
        AssertEqual<Exception?>(null, observedException);
        AssertFalse(context.WaitForPost(TimeSpan.FromMilliseconds(100)), "canceled task should not post a report");
        AssertEqual(0, reporterCallCount);
    }
    finally
    {
        SynchronizationContext.SetSynchronizationContext(priorContext);
        CourseGuard.Frontend.Helpers.TaskExtensions.ErrorReporter = null;
        CourseGuard.Frontend.Helpers.TaskExtensions.ObservationCompleted = null;
    }
});

Run("fire and forget safe falls back to context when supplied owner becomes invalid", () =>
{
    using var owner = new Control();
    IntPtr handle = owner.Handle;
    int reporterCallCount = 0;
    Exception? observedException = null;
    using var observed = new ManualResetEventSlim();
    var context = new RecordingSynchronizationContext();
    var failedLater = new TaskCompletionSource();
    SynchronizationContext? priorContext = SynchronizationContext.Current;

    CourseGuard.Frontend.Helpers.TaskExtensions.ErrorReporter = (_, _, _, _) => reporterCallCount++;
    CourseGuard.Frontend.Helpers.TaskExtensions.ObservationCompleted = exception =>
    {
        observedException = exception;
        observed.Set();
    };

    try
    {
        SynchronizationContext.SetSynchronizationContext(context);
        failedLater.Task.FireAndForgetSafe(owner);
        owner.Dispose();
        failedLater.SetException(new InvalidOperationException("boom"));

        AssertTrue(observed.Wait(TimeSpan.FromSeconds(3)), "invalid owner failed task was not observed");
        AssertTrue(observedException is InvalidOperationException, "invalid owner should observe original exception");
        AssertTrue(context.WaitForPost(TimeSpan.FromSeconds(3)), "invalid supplied owner should fall back to captured context");
        AssertEqual(0, reporterCallCount);

        context.ExecuteAll();

        AssertEqual(1, reporterCallCount);
    }
    finally
    {
        SynchronizationContext.SetSynchronizationContext(priorContext);
        CourseGuard.Frontend.Helpers.TaskExtensions.ErrorReporter = null;
        CourseGuard.Frontend.Helpers.TaskExtensions.ObservationCompleted = null;
    }
});

Run("fire and forget safe posts to captured synchronization context", () =>
{
    Exception? capturedException = null;
    string? capturedTitle = null;
    string? capturedMessage = null;
    var context = new RecordingSynchronizationContext();
    SynchronizationContext? priorContext = SynchronizationContext.Current;
    using var observed = new ManualResetEventSlim();

    CourseGuard.Frontend.Helpers.TaskExtensions.ErrorReporter = (control, title, message, exception) =>
    {
        AssertEqual<Control?>(null, control);
        capturedException = exception;
        capturedTitle = title;
        capturedMessage = message;
        throw new InvalidOperationException("context reporter failure");
    };
    CourseGuard.Frontend.Helpers.TaskExtensions.ObservationCompleted = _ => observed.Set();

    try
    {
        SynchronizationContext.SetSynchronizationContext(context);
        Task.FromException(new InvalidOperationException("boom")).FireAndForgetSafe(null);
        AssertTrue(observed.Wait(TimeSpan.FromSeconds(3)), "context failed task was not observed");
        AssertTrue(context.WaitForPost(TimeSpan.FromSeconds(3)), "context callback was not posted");

        context.ExecuteAll();

        AssertEqual("Loi nen", capturedTitle);
        AssertTrue(capturedMessage?.Contains("boom") == true, "context reporter message should include exception message");
        AssertTrue(capturedException is InvalidOperationException, "context reporter should capture exception");
    }
    finally
    {
        SynchronizationContext.SetSynchronizationContext(priorContext);
        CourseGuard.Frontend.Helpers.TaskExtensions.ErrorReporter = null;
        CourseGuard.Frontend.Helpers.TaskExtensions.ObservationCompleted = null;
    }
});

Run("fire and forget safe returns before reporting already failed live owner task", () =>
{
    using var ready = new ManualResetEventSlim();
    using var reported = new ManualResetEventSlim();
    Exception? threadFailure = null;

    var thread = new Thread(() =>
    {
        try
        {
            using var owner = new Control();
            IntPtr handle = owner.Handle;
            CourseGuard.Frontend.Helpers.TaskExtensions.ErrorReporter = (_, _, _, _) => reported.Set();

            ready.Set();
            Task.FromException(new InvalidOperationException("boom")).FireAndForgetSafe(owner, "Background task error");
            if (reported.IsSet)
                throw new InvalidOperationException("reporter ran before FireAndForgetSafe returned");

            DateTime deadline = DateTime.UtcNow.AddSeconds(3);
            while (!reported.IsSet && DateTime.UtcNow < deadline)
            {
                Application.DoEvents();
                Thread.Sleep(10);
            }
        }
        catch (Exception ex)
        {
            threadFailure = ex;
        }
        finally
        {
            CourseGuard.Frontend.Helpers.TaskExtensions.ErrorReporter = null;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    AssertTrue(ready.Wait(TimeSpan.FromSeconds(3)), "live control test did not start");
    thread.Join();

    if (threadFailure != null)
        throw new InvalidOperationException(threadFailure.Message, threadFailure);
    AssertTrue(reported.IsSet, "live control reporter was not scheduled");
});

Run("fire and forget safe live owner uses begin invoke and swallows reporter exceptions", () =>
{
    Exception? capturedException = null;
    string? capturedTitle = null;
    string? capturedMessage = null;
    int uiThreadId = 0;
    int reporterThreadId = 0;
    using var ready = new ManualResetEventSlim();
    using var reported = new ManualResetEventSlim();
    var failingTask = new TaskCompletionSource();
    Exception? threadFailure = null;

    var thread = new Thread(() =>
    {
        try
        {
            uiThreadId = Thread.CurrentThread.ManagedThreadId;
            using var owner = new Control();
            IntPtr handle = owner.Handle;
            CourseGuard.Frontend.Helpers.TaskExtensions.ErrorReporter = (control, title, message, exception) =>
            {
                AssertTrue(ReferenceEquals(owner, control), "live owner reporter should receive owner");
                reporterThreadId = Thread.CurrentThread.ManagedThreadId;
                capturedException = exception;
                capturedTitle = title;
                capturedMessage = message;
                reported.Set();
                throw new InvalidOperationException("reporter failure");
            };

            ready.Set();
            ThreadPool.QueueUserWorkItem(_ => failingTask.Task.FireAndForgetSafe(owner, "Background task error"));
            failingTask.SetException(new InvalidOperationException("boom"));

            DateTime deadline = DateTime.UtcNow.AddSeconds(3);
            while (!reported.IsSet && DateTime.UtcNow < deadline)
            {
                Application.DoEvents();
                Thread.Sleep(10);
            }
        }
        catch (Exception ex)
        {
            threadFailure = ex;
        }
        finally
        {
            CourseGuard.Frontend.Helpers.TaskExtensions.ErrorReporter = null;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    AssertTrue(ready.Wait(TimeSpan.FromSeconds(3)), "begin invoke live control test did not start");
    thread.Join();

    if (threadFailure != null)
        throw new InvalidOperationException(threadFailure.Message, threadFailure);
    AssertTrue(reported.IsSet, "begin invoke live control reporter was not scheduled");
    AssertEqual(uiThreadId, reporterThreadId);
    AssertEqual("Background task error", capturedTitle);
    AssertTrue(capturedMessage?.Contains("boom") == true, "reported message should include exception message");
    AssertTrue(capturedException is InvalidOperationException, "reported exception should be captured");
});

Run("table grid border token is opaque in both themes", () =>
{
    AppColors.IsDarkMode = true;
    AssertEqual(255, AppColors.GridBorder.A);

    AppColors.IsDarkMode = false;
    AssertEqual(255, AppColors.GridBorder.A);

    AppColors.IsDarkMode = true;
});

Run("vietnam time formatter does not offset database local timestamps", () =>
{
    var databaseLocalTime = new DateTime(2026, 5, 25, 3, 4, 0, DateTimeKind.Unspecified);
    AssertEqual("25/05/2026 03:04", FormatVietnamTime(databaseLocalTime));
});

Run("vietnam time formatter converts explicit utc timestamps", () =>
{
    var utcTime = new DateTime(2026, 5, 25, 3, 4, 0, DateTimeKind.Utc);
    AssertEqual("25/05/2026 10:04", FormatVietnamTime(utcTime));
});

Run("student and teacher data cards wrap grids in rounded bodies", RunDashboardCardTests);
Run("student and teacher page descriptions reserve descender space", RunDashboardHeaderSubtitleLayoutTests);
Run("rounded UI chrome keeps painted borders inside clipping regions", RunRoundedChromeClippingGuardTests);
Run("overview action and indicator models expose required display fields", () =>
{
    Type actionType = typeof(UC_Profile).Assembly.GetType("CourseGuard.Frontend.Helpers.OverviewActionItem")
        ?? throw new InvalidOperationException("OverviewActionItem must exist");
    Type indicatorType = typeof(UC_Profile).Assembly.GetType("CourseGuard.Frontend.Helpers.OverviewIndicatorItem")
        ?? throw new InvalidOperationException("OverviewIndicatorItem must exist");

    foreach (string propertyName in new[] { "Title", "Subtitle", "PageName", "ActionText", "Priority", "CourseId", "CourseName", "ToolId" })
        AssertTrue(actionType.GetProperty(propertyName) != null, $"OverviewActionItem must expose {propertyName}");

    foreach (string propertyName in new[] { "Label", "Value", "Tone" })
        AssertTrue(indicatorType.GetProperty(propertyName) != null, $"OverviewIndicatorItem must expose {propertyName}");
});
Run("student overview next actions follow agreed priority", () =>
{
    Type builderType = typeof(UC_Profile).Assembly.GetType("CourseGuard.Frontend.Helpers.OverviewActionBuilder")
        ?? throw new InvalidOperationException("OverviewActionBuilder must exist");
    MethodInfo build = builderType.GetMethod("BuildStudentActions", new[] { typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })
        ?? throw new InvalidOperationException("BuildStudentActions must exist");

    object[] actions = (object[])build.Invoke(null, new object?[] { true, true, true, true, true })!;
    string joinedTitles = string.Join("|", actions.Select(a => a.GetType().GetProperty("Title")!.GetValue(a)?.ToString()));

    AssertTrue(joinedTitles.StartsWith("Làm bài thi đang mở|Nộp bài tập sắp hết hạn|Vào lớp học hôm nay|Đọc tin nhắn mới|Xem thông báo mới"),
        "student actions must be ordered exam, deadline, class, chat, notification");
});
Run("teacher overview next actions follow agreed priority", () =>
{
    Type builderType = typeof(UC_Profile).Assembly.GetType("CourseGuard.Frontend.Helpers.OverviewActionBuilder")
        ?? throw new InvalidOperationException("OverviewActionBuilder must exist");
    MethodInfo build = builderType.GetMethod("BuildTeacherActions", new[] { typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })
        ?? throw new InvalidOperationException("BuildTeacherActions must exist");

    object[] actions = (object[])build.Invoke(null, new object?[] { true, true, true, true, true })!;
    string joinedTitles = string.Join("|", actions.Select(a => a.GetType().GetProperty("Title")!.GetValue(a)?.ToString()));

    AssertTrue(joinedTitles.StartsWith("Giám sát kỳ thi đang diễn ra|Xử lý việc cần chú ý|Mở buổi dạy sắp tới|Trả lời tin nhắn mới|Xem thông báo mới"),
        "teacher actions must be ordered active exam, required task, class, chat, notification");
});
Run("overview next actions expose Vietnamese navigation labels", () =>
{
    OverviewActionItem[] studentActions = OverviewActionBuilder.BuildStudentActions(true, true, true, true, true);
    OverviewActionItem[] teacherActions = OverviewActionBuilder.BuildTeacherActions(true, true, true, true, true);

    AssertEqual("Làm bài", studentActions[0].ActionText);
    AssertEqual("Bài kiểm tra", studentActions[0].PageName);
    AssertEqual("Lịch học", studentActions[2].PageName);
    AssertEqual("Giám sát", teacherActions[0].ActionText);
    AssertEqual("Giám sát thi", teacherActions[0].PageName);
    AssertEqual("Lịch dạy", teacherActions[2].PageName);
});
Run("overview communication actions route chat and notifications separately", () =>
{
    OverviewActionItem studentChatOnly = OverviewActionBuilder.BuildStudentActions(false, false, false, true, false).Single();
    AssertEqual("Đọc tin nhắn mới", studentChatOnly.Title);
    AssertEqual("Tin nhắn", studentChatOnly.PageName);
    AssertEqual("Mở tin nhắn", studentChatOnly.ActionText);

    OverviewActionItem studentNotificationOnly = OverviewActionBuilder.BuildStudentActions(false, false, false, false, true).Single();
    AssertEqual("Xem thông báo mới", studentNotificationOnly.Title);
    AssertEqual("Thông báo", studentNotificationOnly.PageName);
    AssertEqual("Mở thông báo", studentNotificationOnly.ActionText);

    OverviewActionItem[] studentBoth = OverviewActionBuilder.BuildStudentActions(false, false, false, true, true);
    AssertEqual("Tin nhắn", studentBoth[0].PageName);
    AssertEqual("Thông báo", studentBoth[1].PageName);

    OverviewActionItem teacherChatOnly = OverviewActionBuilder.BuildTeacherActions(false, false, false, true, false).Single();
    AssertEqual("Trả lời tin nhắn mới", teacherChatOnly.Title);
    AssertEqual("Tin nhắn", teacherChatOnly.PageName);

    OverviewActionItem teacherNotificationOnly = OverviewActionBuilder.BuildTeacherActions(false, false, false, false, true).Single();
    AssertEqual("Xem thông báo mới", teacherNotificationOnly.Title);
    AssertEqual("Thông báo", teacherNotificationOnly.PageName);
});
Run("overview notification actions are grouped by resolved course workspace", () =>
{
    NotificationModel[] notifications =
    {
        new()
        {
            Id = 1,
            Title = "Bài tập mới",
            IsRead = false,
            CourseId = 10,
            CourseName = "Lập trình mạng",
            CreatedAt = new DateTime(2026, 7, 4, 8, 0, 0)
        },
        new()
        {
            Id = 2,
            Title = "Tài liệu mới",
            IsRead = false,
            CourseId = 20,
            CourseName = "Cơ sở dữ liệu",
            CreatedAt = new DateTime(2026, 7, 4, 9, 0, 0)
        },
        new()
        {
            Id = 3,
            Title = "Thông báo chung",
            IsRead = false,
            CreatedAt = new DateTime(2026, 7, 4, 10, 0, 0)
        }
    };

    OverviewActionItem[] actions = OverviewActionBuilder.BuildCourseNotificationActions(notifications);

    AssertEqual(2, actions.Length);
    AssertEqual(20, actions[0].CourseId);
    AssertEqual("Cơ sở dữ liệu", actions[0].CourseName);
    AssertEqual("notifications", actions[0].ToolId);
    AssertEqual("Thông báo", actions[0].PageName);
    AssertEqual(10, actions[1].CourseId);
    AssertEqual("Lập trình mạng", actions[1].CourseName);
});
Run("overview chat actions are grouped by unread course workspace", () =>
{
    ChatCourseModel[] courses =
    {
        new() { CourseId = 10, CourseName = "Lập trình mạng", UnreadCount = 2 },
        new() { CourseId = 20, CourseName = "Cơ sở dữ liệu", UnreadCount = 1 },
        new() { CourseId = 30, CourseName = "An toàn mạng", UnreadCount = 0 }
    };

    OverviewActionItem[] actions = OverviewActionBuilder.BuildCourseChatActions(courses, teacher: false);

    AssertEqual(2, actions.Length);
    AssertEqual(10, actions[0].CourseId);
    AssertEqual("Tin nhắn", actions[0].PageName);
    AssertEqual(string.Empty, actions[0].ToolId);
    AssertEqual(20, actions[1].CourseId);
});
Run("overview action labels reserve enough single line space", () =>
{
    string root = RepoRoot();
    string studentSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Student", "UC_StudentDashboard.cs"));
    string teacherSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherOverview.cs"));

    AssertFalse(studentSource.Contains("Width = 96"),
        "student overview action label should not use the old fixed 96px width");
    AssertFalse(teacherSource.Contains("Width = 96"),
        "teacher overview action label should not use the old fixed 96px width");
    AssertTrue(studentSource.Contains("GetActionLabelWidth(item.ActionText)") && studentSource.Contains("TextRenderer.MeasureText"),
        "student overview action label should measure text and reserve single-line width");
    AssertTrue(teacherSource.Contains("GetActionLabelWidth(item.ActionText)") && teacherSource.Contains("TextRenderer.MeasureText"),
        "teacher overview action label should measure text and reserve single-line width");

    IEnumerable<string> actionTexts = OverviewActionBuilder.BuildStudentActions(true, true, true, true, true)
        .Concat(OverviewActionBuilder.BuildTeacherActions(true, true, true, true, true))
        .Select(action => action.ActionText);
    int widestAction = actionTexts.Max(text => TextRenderer.MeasureText(text, AppFonts.Semibold(9.5f)).Width);
    AssertTrue(widestAction + 24 <= 150,
        "overview action label max width should fit current Vietnamese action text on one line");
});
Run("overview action rows can request dashboard navigation", () =>
{
    AssertTrue(typeof(UC_StudentDashboard).GetEvent("ActionNavigationRequested") != null,
        "student overview action rows must expose a navigation event");
    AssertTrue(typeof(UC_TeacherOverview).GetEvent("ActionNavigationRequested") != null,
        "teacher overview action rows must expose a navigation event");

    string root = RepoRoot();
    string studentDashboardSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "StudentDashboard.cs"));
    string teacherDashboardSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherDashboard.cs"));

    AssertTrue(studentDashboardSource.Contains("ActionNavigationRequested += (_, action) => OpenOverviewAction(action)"),
        "student dashboard must route overview action clicks with course context");
    AssertTrue(teacherDashboardSource.Contains("ActionNavigationRequested += (_, action) => OpenOverviewAction(action)"),
        "teacher dashboard must route overview action clicks with course context");
});
Run("teacher overview context panels are visually independent", () =>
{
    string root = RepoRoot();
    string source = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherOverview.cs"));

    AssertTrue(source.Contains("private TableLayoutPanel _contextGrid"),
        "teacher overview should use a context grid instead of one outer context card");
    AssertFalse(source.Contains("private RoundedPanel _contextPanel"),
        "teacher overview should not wrap context cards in a shared outer panel");
    AssertTrue(source.Contains("CreateCardTitle(\"Lịch dạy và việc cần xử lý\")"),
        "teacher context heading should use Vietnamese copy without mnemonic ampersand");
    AssertFalse(source.Contains("Lịch dạy &"),
        "teacher context heading should not use ampersand because WinForms treats it as a mnemonic");
});
Run("sidebar headings render larger than caption text", () =>
{
    string root = RepoRoot();
    string source = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Theme", "SidebarPanel.cs"));
    string colorsSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Theme", "AppColors.cs"));

    AssertTrue(source.Contains("AppFonts.Semibold(10f)"),
        "sidebar headings should use a larger semibold font");
    AssertTrue(source.Contains("return collapsed ? 10 : 32;"),
        "expanded sidebar headings should reserve taller heading rows");
    AssertTrue(source.Contains("SidebarHeadingText") && source.Contains("SidebarHeadingAccent") && source.Contains("SidebarHeadingBg"),
        "sidebar headings should use dedicated heading tokens instead of muted nav text");
    AssertTrue(colorsSource.Contains("SidebarHeadingText") && colorsSource.Contains("SidebarHeadingAccent") && colorsSource.Contains("SidebarHeadingBg"),
        "AppColors must expose light/dark heading tokens");
});
Run("student overview uses compact indicators and next actions instead of stat grid", () =>
{
    Type type = typeof(UC_StudentDashboard);
    AssertTrue(type.GetField("_indicatorStrip", BindingFlags.Instance | BindingFlags.NonPublic) != null,
        "student overview must expose compact indicator strip");
    AssertTrue(type.GetField("_nextActionList", BindingFlags.Instance | BindingFlags.NonPublic) != null,
        "student overview must expose next action list");
    AssertTrue(type.GetField("_statsGrid", BindingFlags.Instance | BindingFlags.NonPublic) == null,
        "student overview should not keep the old large stats grid");
});
Run("student overview indicator values use success green", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Student", "UC_StudentDashboard.cs"));
    string method = ExtractMethodSource(source, "private Control CreateIndicatorPill");

    AssertTrue(method.Contains("ForeColor = AppColors.Success"),
        "student overview indicator value labels should use green success text");
    AssertFalse(method.Contains("ForeColor = AppColors.TextPrimary"),
        "student overview indicator value labels should not use neutral primary text");
});
Run("teacher overview uses compact indicators and next actions instead of stat grid", () =>
{
    Type type = typeof(UC_TeacherOverview);
    AssertTrue(type.GetField("_indicatorStrip", BindingFlags.Instance | BindingFlags.NonPublic) != null,
        "teacher overview must expose compact indicator strip");
    AssertTrue(type.GetField("_nextActionList", BindingFlags.Instance | BindingFlags.NonPublic) != null,
        "teacher overview must expose next action list");
    AssertTrue(type.GetField("_statsGrid", BindingFlags.Instance | BindingFlags.NonPublic) == null,
        "teacher overview should not keep the old large stats grid");
});
Run("profile pages retain role metrics and account security sections", () =>
{
    string root = RepoRoot();
    string studentProfileSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Student", "UC_Profile.cs"));
    string teacherProfileSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherProfile.cs"));

    AssertTrue(studentProfileSource.Contains("Thong tin hoc tap") || studentProfileSource.Contains("Thông tin học tập"),
        "student profile must keep learning metrics in profile");
    AssertTrue(teacherProfileSource.Contains("Thong tin giang day") || teacherProfileSource.Contains("Thông tin giảng dạy"),
        "teacher profile must keep teaching metrics in profile");
    AssertTrue(studentProfileSource.Contains("Bao mat tai khoan") || studentProfileSource.Contains("Bảo mật tài khoản"),
        "student profile must keep account security");
    AssertTrue(teacherProfileSource.Contains("Bao mat tai khoan") || teacherProfileSource.Contains("Bảo mật tài khoản"),
        "teacher profile must keep account security");
});
Run("sidebar supports visible expanded navigation groups without replacing old API", () =>
{
    Type sidebarType = typeof(SidebarPanel);
    AssertTrue(sidebarType.GetMethod("SetNavItems", new[] { typeof(string[]), typeof(string[]) }) != null,
        "SidebarPanel must preserve SetNavItems(string[], string[])");

    Type? itemType = typeof(SidebarPanel).Assembly.GetType("CourseGuard.Frontend.Theme.SidebarNavItem");
    AssertTrue(itemType != null, "SidebarNavItem must exist");
    AssertTrue(itemType!.GetProperty("Label") != null, "SidebarNavItem must expose Label");
    AssertTrue(itemType.GetProperty("Icon") != null, "SidebarNavItem must expose Icon");
    AssertTrue(itemType.GetProperty("IsHeading") != null, "SidebarNavItem must expose IsHeading");

    Type listType = typeof(IEnumerable<>).MakeGenericType(itemType);
    MethodInfo? groupedMethod = sidebarType.GetMethod("SetNavItems", new[] { listType });
    AssertTrue(groupedMethod != null, "SidebarPanel must expose SetNavItems(IEnumerable<SidebarNavItem>)");
});
Run("student and teacher dashboards use course first sidebar navigation", () =>
{
    string root = RepoRoot();
    string studentSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "StudentDashboard.cs"));
    string teacherSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherDashboard.cs"));

    AssertTrue(studentSource.Contains("CourseFirstSidebarControl"),
        "student dashboard must use course-first sidebar navigation");
    AssertFalse(studentSource.Contains("new SidebarNavItem"),
        "student dashboard must not keep module-first grouped sidebar rows");
    AssertTrue(teacherSource.Contains("CourseFirstSidebarControl"),
        "teacher dashboard must use course-first sidebar navigation");
    AssertFalse(teacherSource.Contains("new SidebarNavItem"),
        "teacher dashboard must not keep module-first grouped sidebar rows");
    AssertTrue(!studentSource.Contains("ToggleGroup") && !teacherSource.Contains("ToggleGroup"),
        "course-first sidebar must not introduce collapsible group state in this phase");
});
Run("course first sidebar does not render floating chat unread badge", () =>
{
    string source = File.ReadAllText(Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "CourseWorkspace", "CourseFirstSidebarControl.cs"));

    AssertFalse(source.Contains("_middlePanel.Controls.Add(_chatBadgeLabel)"),
        "course-first sidebar must not add a standalone chat badge to the middle panel because it floats at the left edge without a chat nav row");
    AssertTrue(source.Contains("SetChatUnreadCount"),
        "course-first sidebar should keep the unread-count API for dashboard callers");
});
Run("teacher dashboard honors profile security focus requests", () =>
{
    Type type = typeof(TeacherDashboard);
    MethodInfo? navigate = type.GetMethod("NavigateToPage", BindingFlags.Instance | BindingFlags.Public);
    AssertTrue(navigate != null, "TeacherDashboard must expose NavigateToPage");
    AssertTrue(navigate!.GetParameters().Any(p => p.Name == "focusSecurity" && p.ParameterType == typeof(bool)),
        "TeacherDashboard NavigateToPage must accept focusSecurity");
    AssertTrue(type.GetMethod("FocusProfileSecuritySection", BindingFlags.Instance | BindingFlags.NonPublic) != null,
        "TeacherDashboard must expose FocusProfileSecuritySection");
});
Run("student grid page base builds layout and empty state without db access", RunStudentGridPageBaseTests);
Run("student grid page base appends derived actions to header action strip", RunStudentGridPageHeaderActionTests);
Run("student grid page base shows error empty state on initial failure", RunStudentGridPageInitialFailureTests);
Run("student grid page base preserves initial error state across search rebind", RunStudentGridPageInitialFailureRebindTests);
Run("student grid page base preserves bound rows across failed refresh and rebind", RunStudentGridPageRefreshFailureTests);
Run("teacher grid page base ignores stale overlapping load completions and failures", RunTeacherGridPageStaleLoadVersioningTests);
Run("student grid concrete pages are sealed", () =>
{
    AssertTrue(typeof(UC_StudentLessons).IsSealed, "student lessons page should be sealed");
    AssertTrue(typeof(UC_StudentAssignments).IsSealed, "student assignments page should be sealed");
    AssertTrue(typeof(UC_Documents).IsSealed, "student documents page should be sealed");
});
Run("modern message dialog exposes requested buttons", RunMessageDialogButtonTests);
Run("deadline reminder service sends one action notification", RunDeadlineReminderServiceSendsOneNotification);
Run("deadline reminder service can send 1h reminder after 24h reminder", RunDeadlineReminderServiceSendsOneHourAfterTwentyFourHour);
Run("deadline reminder service retries after notification create failure", RunDeadlineReminderServiceRetriesAfterCreateFailure);
Run("deadline reminder service avoids duplicate concurrent claims", RunDeadlineReminderServiceAvoidsDuplicateConcurrentClaims);
Run("deadline reminder source wiring matches current notification architecture", RunDeadlineReminderSourceTests);
Run("native classroom records attendance in and out", RunNativeClassroomAttendanceSourceTests);
Run("teacher attendance summary is exposed through repository and controller", RunTeacherAttendanceSummarySourceTests);
Run("teacher students page opens attendance dialog", RunTeacherAttendanceUiSourceTests);
Run("attendance close cleanup and teacher schema are guarded", RunAttendanceCleanupAndSchemaSourceTests);
Run("chat unread API uses chat reads", RunChatUnreadSourceTests);
Run("student sidebar exposes chat unread badge", RunSidebarChatBadgeSourceTests);
Run("student dashboard loads and reloads chat unread badge", RunStudentDashboardChatBadgeSourceTests);
Run("course chat panel advances read watermark after refresh", RunStudentChatPageReadWatermarkSourceTests);
Run("course management and security requirements", RunCourseManagementAndSecurityTests);
TestRunner.PrintSummary();

Console.WriteLine(Environment.ExitCode == 0 ? "Feature tests passed." : "Feature tests failed.");

static void RunCourseManagementAndSecurityTests()
{
    string repoRoot = RepoRoot();
    string dbPath = Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Backend", "Data", "CourseGuardDbContext.cs");
    string courseControllerPath = Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Backend", "Controllers", "CourseController.cs");
    string teacherControllerPath = Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Backend", "Controllers", "TeacherController.cs");
    string teacherStudentsPath = Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherStudents.cs");
    string chatImageHelperPath = Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Frontend", "Helpers", "ChatImageHelper.cs");
    string chatImageLoaderPath = Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "Chat", "ChatImageLoader.cs");
    string chatBubblePath = Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "Chat", "ChatBubbleControl.cs");

    AssertTrue(File.Exists(dbPath), "CourseGuardDbContext must exist");
    AssertTrue(File.Exists(courseControllerPath), "CourseController must exist");
    AssertTrue(File.Exists(teacherControllerPath), "TeacherController must exist");
    AssertTrue(File.Exists(teacherStudentsPath), "UC_TeacherStudents must exist");
    AssertTrue(File.Exists(chatImageHelperPath), "ChatImageHelper must exist");

    string db = File.ReadAllText(dbPath);
    string cc = File.ReadAllText(courseControllerPath);
    string tc = File.ReadAllText(teacherControllerPath);
    string ts = File.ReadAllText(teacherStudentsPath);
    string cih = File.ReadAllText(chatImageHelperPath);
    string cil = File.ReadAllText(chatImageLoaderPath);
    string cb = File.ReadAllText(chatBubblePath);

    // 1. Fixed Course Schedule column migrations & cascade deletes
    AssertTrue(db.Contains("teaching_days VARCHAR(255)"), "COURSES schema must migrate teaching_days");
    AssertTrue(db.Contains("session_start_time TIME"), "COURSES schema must migrate session_start_time");
    AssertTrue(db.Contains("session_end_time TIME"), "COURSES schema must migrate session_end_time");
    AssertTrue(db.Contains("InsertGeneratedSessions"), "CourseGuardDbContext must generate session schedule");
    AssertTrue(db.Contains("ON DELETE CASCADE"), "User deletion constraints must use ON DELETE CASCADE");
    AssertTrue(db.Contains("ON DELETE SET NULL"), "User deletion constraints must use ON DELETE SET NULL");

    // 2. Schedule Conflict Checks
    AssertTrue(db.Contains("HasTeacherConflict"), "DbContext must support teacher conflict detection");
    AssertTrue(db.Contains("HasStudentConflict"), "DbContext must support student conflict detection");
    AssertTrue(cc.Contains("HasTeacherConflict"), "CourseController must prevent teacher conflicts");
    AssertTrue(tc.Contains("HasTeacherConflict"), "TeacherController must prevent teacher conflicts");
    AssertTrue(cc.Contains("HasStudentConflict"), "CourseController must prevent student conflicts");

    // 3. Student join conflict check
    AssertTrue(cc.Contains("StudentJoinCourse"), "CourseController must support StudentJoinCourse");
    string studentJoin = ExtractMethodSource(cc, "public bool StudentJoinCourse");
    AssertTrue(studentJoin.Contains("HasStudentConflict"), "StudentJoinCourse must validate student conflicts");

    // 4. Enrollment approval privilege restrictions
    string ccApprove = ExtractMethodSource(cc, "public bool ApproveEnrollment");
    string ccReject = ExtractMethodSource(cc, "public bool RejectEnrollment");
    AssertTrue(ccApprove.Contains("CurrentRole != \"ADMIN\""), "CourseController student approval must restrict to Admin only");
    AssertTrue(ccReject.Contains("CurrentRole != \"ADMIN\""), "CourseController student rejection must restrict to Admin only");

    string tcApprove = ExtractMethodSource(tc, "public bool ApproveEnrollment");
    string tcReject = ExtractMethodSource(tc, "public bool RejectEnrollment");
    string tcCanManageEnrollment = ExtractMethodSource(tc, "private static bool CanManageEnrollment");
    AssertFalse(tcApprove.Contains("CurrentRole != \"ADMIN\""), "TeacherController approval must not block the Teacher role");
    AssertFalse(tcReject.Contains("CurrentRole != \"ADMIN\""), "TeacherController rejection must not block the Teacher role");
    AssertTrue(tcApprove.Contains("CanManageEnrollment(teacherId)"), "TeacherController approval must use the shared teacher/admin enrollment guard");
    AssertTrue(tcReject.Contains("CanManageEnrollment(teacherId)"), "TeacherController rejection must use the shared teacher/admin enrollment guard");
    AssertTrue(tcCanManageEnrollment.Contains("UserSessionContext.CurrentRole == \"ADMIN\""), "enrollment guard must keep Admin approval support");
    AssertTrue(tcCanManageEnrollment.Contains("UserSessionContext.CurrentRole == \"TEACHER\""), "enrollment guard must allow teachers");
    AssertTrue(tcCanManageEnrollment.Contains("UserSessionContext.CurrentUserId == teacherId"), "teacher approval must be limited to the signed-in teacher");

    // UI visibility
    AssertTrue(ts.Contains("AddButton.Text = \"Duyệt\";"), "UC_TeacherStudents must label the approval action clearly");
    AssertTrue(ts.Contains("EditButton.Text = \"Từ chối\";"), "UC_TeacherStudents must label the rejection action clearly");
    AssertFalse(ts.Contains("AddButton.Visible = false;"), "UC_TeacherStudents must show the approval action");
    AssertFalse(ts.Contains("EditButton.Visible = false;"), "UC_TeacherStudents must show the rejection action");
    AssertTrue(ts.Contains("protected override string EditSelectionColumnName => \"EnrollmentId\";"),
        "UC_TeacherStudents rejection action must use the enrollment id selection guard");

    // 5. Recent Activity and Student notifications
    string tcCreateMaterial = ExtractMethodSource(tc, "public int CreateMaterial");
    string tcCreateAssignment = ExtractMethodSource(tc, "public int CreateAssignment");
    string tcCreateLesson = ExtractMethodSource(tc, "public int CreateLesson");

    AssertTrue(tcCreateMaterial.Contains("LogUserActivity"), "CreateMaterial must record recent activity");
    AssertTrue(tcCreateMaterial.Contains("_notifications.Create"), "CreateMaterial must notify students");
    AssertTrue(tcCreateAssignment.Contains("LogUserActivity"), "CreateAssignment must record recent activity");
    AssertTrue(tcCreateAssignment.Contains("_notifications.Create"), "CreateAssignment must notify students");
    AssertTrue(tcCreateLesson.Contains("LogUserActivity"), "CreateLesson must record recent activity");

    // 6. Chat Image viewing path resolution
    AssertTrue(cih.Contains("ResolveLocalFilePath"), "ChatImageHelper must support resolving local paths");
    AssertTrue(cil.Contains("ResolveLocalFilePath"), "ChatImageLoader must resolve image path");
    AssertTrue(cb.Contains("ResolveLocalFilePath"), "ChatBubbleControl must resolve preview image path");
}

static string RepoRoot()
{
    string current = AppContext.BaseDirectory;
    while (!string.IsNullOrWhiteSpace(current))
    {
        if (Directory.Exists(Path.Combine(current, "CourseGuard"))
            && Directory.Exists(Path.Combine(current, "tests")))
        {
            return current;
        }

        DirectoryInfo? parent = Directory.GetParent(current);
        if (parent == null)
            break;

        current = parent.FullName;
    }

    throw new InvalidOperationException("Cannot locate repository root.");
}

static void AssertTeacherDeleteRequiresConfirmation(string methodSource, string deleteCall, string context)
{
    int confirmIndex = methodSource.IndexOf("ShowModernDialog", StringComparison.Ordinal);
    int yesIndex = methodSource.IndexOf("DialogResult.Yes", StringComparison.Ordinal);
    int deleteIndex = methodSource.IndexOf(deleteCall, StringComparison.Ordinal);

    AssertTrue(confirmIndex >= 0, $"{context} must show a confirmation dialog");
    AssertTrue(methodSource.Contains("MessageBoxButtons.YesNo", StringComparison.Ordinal),
        $"{context} confirmation must use Yes/No buttons");
    AssertTrue(yesIndex >= 0, $"{context} must check for DialogResult.Yes before deleting");
    AssertTrue(deleteIndex >= 0, $"{context} must still call the controller delete method");
    AssertTrue(confirmIndex < deleteIndex, $"{context} confirmation must run before the delete call");
    AssertTrue(yesIndex < deleteIndex, $"{context} must gate the delete call behind DialogResult.Yes");
}

static void AssertNoMojibakeMarkers(string source, string context)
{
    string[] markers = { "Ã", "Ä", "áÂ", "â€", "Â" };
    foreach (string marker in markers)
    {
        AssertFalse(source.Contains(marker, StringComparison.Ordinal),
            $"{context} must not contain mojibake marker '{marker}'");
    }
}

static string ExtractMethodSource(string source, string signature)
{
    int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
    if (signatureIndex < 0)
        throw new InvalidOperationException($"Cannot find method signature: {signature}");

    int expressionStart = source.IndexOf("=>", signatureIndex, StringComparison.Ordinal);
    int bodyStart = source.IndexOf('{', signatureIndex);
    if (expressionStart >= 0 && (bodyStart < 0 || expressionStart < bodyStart))
    {
        int expressionEnd = FindExpressionBodyEnd(source, expressionStart, signature);
        return source.Substring(signatureIndex, expressionEnd - signatureIndex + 1);
    }

    if (bodyStart < 0)
        throw new InvalidOperationException($"Cannot find method body: {signature}");

    int depth = 0;
    for (int i = bodyStart; i < source.Length; i++)
    {
        if (source[i] == '{')
        {
            depth++;
        }
        else if (source[i] == '}')
        {
            depth--;
            if (depth == 0)
                return source.Substring(signatureIndex, i - signatureIndex + 1);
        }
    }

    throw new InvalidOperationException($"Cannot find method end: {signature}");
}

static int FindExpressionBodyEnd(string source, int expressionStart, string signature)
{
    int parenDepth = 0;
    int braceDepth = 0;
    int bracketDepth = 0;
    bool inString = false;
    bool inVerbatimString = false;
    bool inChar = false;
    bool inLineComment = false;
    bool inBlockComment = false;

    for (int i = expressionStart + 2; i < source.Length; i++)
    {
        char current = source[i];
        char next = i + 1 < source.Length ? source[i + 1] : '\0';

        if (inLineComment)
        {
            if (current == '\n')
                inLineComment = false;
            continue;
        }

        if (inBlockComment)
        {
            if (current == '*' && next == '/')
            {
                inBlockComment = false;
                i++;
            }
            continue;
        }

        if (inString)
        {
            if (inVerbatimString)
            {
                if (current == '"' && next == '"')
                {
                    i++;
                    continue;
                }

                if (current == '"')
                {
                    inString = false;
                    inVerbatimString = false;
                }
            }
            else
            {
                if (current == '\\')
                {
                    i++;
                    continue;
                }

                if (current == '"')
                    inString = false;
            }
            continue;
        }

        if (inChar)
        {
            if (current == '\\')
            {
                i++;
                continue;
            }

            if (current == '\'')
                inChar = false;
            continue;
        }

        if (current == '/' && next == '/')
        {
            inLineComment = true;
            i++;
            continue;
        }

        if (current == '/' && next == '*')
        {
            inBlockComment = true;
            i++;
            continue;
        }

        if (current == '"')
        {
            inString = true;
            inVerbatimString = i > 0 && source[i - 1] == '@';
            continue;
        }

        if (current == '\'')
        {
            inChar = true;
            continue;
        }

        if (current == '(')
        {
            parenDepth++;
        }
        else if (current == ')')
        {
            parenDepth--;
        }
        else if (current == '{')
        {
            braceDepth++;
        }
        else if (current == '}')
        {
            braceDepth--;
        }
        else if (current == '[')
        {
            bracketDepth++;
        }
        else if (current == ']')
        {
            bracketDepth--;
        }
        else if (current == ';' && parenDepth == 0 && braceDepth == 0 && bracketDepth == 0)
        {
            return i;
        }
    }

    throw new InvalidOperationException($"Cannot find expression-bodied method end: {signature}");
}

static string ExtractPropertySource(string source, string signature)
{
    int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
    if (signatureIndex < 0)
        throw new InvalidOperationException($"Cannot find property signature: {signature}");

    int expressionStart = source.IndexOf("=>", signatureIndex, StringComparison.Ordinal);
    if (expressionStart >= 0)
    {
        int expressionEnd = source.IndexOf(';', expressionStart);
        if (expressionEnd < 0)
            throw new InvalidOperationException($"Cannot find expression-bodied property end: {signature}");

        return source.Substring(signatureIndex, expressionEnd - signatureIndex + 1);
    }

    int bodyStart = source.IndexOf('{', signatureIndex);
    if (bodyStart < 0)
        throw new InvalidOperationException($"Cannot find property body: {signature}");

    int depth = 0;
    for (int i = bodyStart; i < source.Length; i++)
    {
        if (source[i] == '{')
        {
            depth++;
        }
        else if (source[i] == '}')
        {
            depth--;
            if (depth == 0)
                return source.Substring(signatureIndex, i - signatureIndex + 1);
        }
    }

    throw new InvalidOperationException($"Cannot find property end: {signature}");
}

static void AssertExplicitDraftQuestionGuard(string repositorySource, string signature)
{
    string method = ExtractMethodSource(repositorySource, signature);
    AssertTrue(method.Contains("@draft_status"), $"{signature} must use an explicit draft-status parameter");
    AssertTrue(
        method.Contains("UPPER(COALESCE(ex.status, '')) = @draft_status"),
        $"{signature} must require explicit DRAFT status instead of treating NULL as draft");
    AssertFalse(method.Contains("COALESCE(ex.status, 'DRAFT')"), $"{signature} must not treat NULL status as draft");
    AssertFalse(method.Contains("= 'DRAFT'"), $"{signature} must not inline a draft literal guard");
}

static async Task RunDeadlineReminderServiceSendsOneNotification()
{
    DateTime now = new DateTime(2026, 6, 6, 10, 0, 0, DateTimeKind.Unspecified);
    var store = new FakeDeadlineReminderStore();
    store.Deadlines.Add(new DeadlineReminderItem
    {
        SourceType = DeadlineReminderItem.SourceTypeAssignment,
        SourceId = 10,
        Title = "Final report",
        CourseName = "Network Programming",
        DueAt = now.AddHours(20)
    });
    int eventCount = 0;

    using var service = new DeadlineReminderService(
        userId: 42,
        store: store,
        clock: () => now,
        pollInterval: TimeSpan.FromMinutes(1),
        initialDelay: TimeSpan.Zero);
    service.NotificationCreated += (_, _) => eventCount++;

    await service.CheckNowAsync().ConfigureAwait(false);
    await service.CheckNowAsync().ConfigureAwait(false);

    AssertEqual(1, store.Created.Count);
    AssertEqual(1, store.Sent.Count);
    AssertEqual(1, eventCount);
    AssertEqual(WorkflowConstants.NotificationCategory.Assignment, store.Created[0].Category);
    AssertEqual(WorkflowConstants.NotificationType.ActionRequired, store.Created[0].NotificationType);
    AssertEqual(DeadlineReminderItem.SourceTypeAssignment, store.Created[0].SourceType);
    AssertEqual(10, store.Created[0].SourceId);
}

static async Task RunDeadlineReminderServiceSendsOneHourAfterTwentyFourHour()
{
    DateTime now = new DateTime(2026, 6, 6, 10, 0, 0, DateTimeKind.Unspecified);
    var store = new FakeDeadlineReminderStore();
    store.Deadlines.Add(new DeadlineReminderItem
    {
        SourceType = DeadlineReminderItem.SourceTypeExam,
        SourceId = 99,
        Title = "Midterm",
        CourseName = "Network Programming",
        DueAt = now.AddMinutes(45)
    });
    store.AlreadySent.Add(FakeDeadlineReminderStore.Key(7, DeadlineReminderItem.SourceTypeExam, 99, DeadlineReminderItem.ReminderType24H));

    using var service = new DeadlineReminderService(
        userId: 7,
        store: store,
        clock: () => now,
        pollInterval: TimeSpan.FromMinutes(1),
        initialDelay: TimeSpan.Zero);

    await service.CheckNowAsync().ConfigureAwait(false);

    AssertEqual(1, store.Created.Count);
    AssertEqual(1, store.Sent.Count);
    AssertTrue(store.Sent.Contains(FakeDeadlineReminderStore.Key(7, DeadlineReminderItem.SourceTypeExam, 99, DeadlineReminderItem.ReminderType1H)), "1h reminder must use a separate sent key");
    AssertEqual(WorkflowConstants.NotificationCategory.Exam, store.Created[0].Category);
}

static async Task RunDeadlineReminderServiceRetriesAfterCreateFailure()
{
    DateTime now = new DateTime(2026, 6, 6, 10, 0, 0, DateTimeKind.Unspecified);
    var store = new FakeDeadlineReminderStore();
    store.Deadlines.Add(new DeadlineReminderItem
    {
        SourceType = DeadlineReminderItem.SourceTypeAssignment,
        SourceId = 10,
        Title = "Final report",
        CourseName = "Network Programming",
        DueAt = now.AddHours(20)
    });
    store.FailNextCreate = true;

    using var service = new DeadlineReminderService(
        userId: 42,
        store: store,
        clock: () => now,
        pollInterval: TimeSpan.FromMinutes(1),
        initialDelay: TimeSpan.Zero);

    try
    {
        await service.CheckNowAsync().ConfigureAwait(false);
    }
    catch (InvalidOperationException)
    {
    }

    await service.CheckNowAsync().ConfigureAwait(false);

    AssertEqual(1, store.Created.Count);
    AssertEqual(1, store.Sent.Count);
}

static async Task RunDeadlineReminderServiceAvoidsDuplicateConcurrentClaims()
{
    DateTime now = new DateTime(2026, 6, 6, 10, 0, 0, DateTimeKind.Unspecified);
    var store = new FakeDeadlineReminderStore
    {
        CoordinateConcurrentClaims = true
    };
    store.Deadlines.Add(new DeadlineReminderItem
    {
        SourceType = DeadlineReminderItem.SourceTypeAssignment,
        SourceId = 10,
        Title = "Final report",
        CourseName = "Network Programming",
        DueAt = now.AddHours(20)
    });

    using var first = new DeadlineReminderService(
        userId: 42,
        store: store,
        clock: () => now,
        pollInterval: TimeSpan.FromMinutes(1),
        initialDelay: TimeSpan.Zero);
    using var second = new DeadlineReminderService(
        userId: 42,
        store: store,
        clock: () => now,
        pollInterval: TimeSpan.FromMinutes(1),
        initialDelay: TimeSpan.Zero);

    await Task.WhenAll(first.CheckNowAsync(), second.CheckNowAsync()).ConfigureAwait(false);

    AssertEqual(1, store.Created.Count);
    AssertEqual(1, store.Sent.Distinct().Count());
}

static void RunDeadlineReminderSourceTests()
{
    string repoRoot = RepoRoot();
    string servicePath = Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Backend", "Services", "DeadlineReminderService.cs");
    string dashboardPath = Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "StudentDashboard.cs");
    string notificationPagePath = Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Student", "UC_Notification.cs");
    string dbPath = Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Backend", "Data", "CourseGuardDbContext.cs");
    string notificationRepoPath = Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Backend", "Data", "NotificationRepository.cs");

    AssertTrue(File.Exists(servicePath), "deadline reminder service must exist");
    string service = File.ReadAllText(servicePath);
    string dashboard = File.ReadAllText(dashboardPath);
    string notificationPage = File.ReadAllText(notificationPagePath);
    string db = File.ReadAllText(dbPath);
    string notificationRepo = File.ReadAllText(notificationRepoPath);

    AssertFalse(service.Contains("CourseGuard.Frontend.Helpers"), "backend reminder service must not depend on frontend helpers");
    AssertFalse(service.Contains("InsertNotification"), "service must not use legacy notification helpers");
    AssertTrue(service.Contains("SemaphoreSlim"), "service must guard overlapping timer callbacks");
    AssertTrue(service.Contains("CreateDeadlineReminderNotification"), "service must delegate reminder notification creation to the atomic store operation");
    AssertFalse(service.Contains("TryClaimReminder"), "service must not claim reminders separately from creating notifications");
    AssertTrue(service.Contains("NotificationCreated"), "service must notify dashboard after creating a notification");
    AssertTrue(notificationRepo.Contains(": INotificationWriter"), "NotificationRepository must implement notification writer abstraction");
    AssertTrue(db.Contains("GetUpcomingDeadlines"), "db context must expose deadline query");
    AssertTrue(db.Contains("deadline_reminders_sent"), "db context must create/use deadline reminder tracking table");
    AssertTrue(db.Contains("WITH claimed"), "db context must insert the reminder marker and notification in one SQL operation");
    AssertTrue(db.Contains("BeginTransaction"), "db context must wrap deadline reminder notification creation in a transaction");
    AssertTrue(db.Contains("s.id IS NULL"), "assignment reminders must skip submitted assignments");
    AssertTrue(db.Contains("exam_questions"), "exam reminders must require questions");
    AssertTrue(dashboard.Contains("DeadlineReminderService"), "student dashboard must own the deadline reminder service");
    AssertTrue(dashboard.Contains("RefreshVisibleNotificationPage"), "dashboard must refresh the notification page explicitly");
    AssertTrue(notificationPage.Contains("RefreshAsync"), "notification user control must expose refresh for dashboard events");
}

static void RunNativeClassroomAttendanceSourceTests()
{
    string path = Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "StudentNativeClassroomForm.cs");
    string source = File.ReadAllText(path);
    AssertTrue(source.Contains("CourseGuardDbContext"), "student native classroom must depend on db context for attendance logging");
    AssertTrue(source.Contains("_attendanceLogId"), "student native classroom must retain the attendance log id");
    AssertTrue(source.Contains("LogAttendanceInAsync"), "student native classroom must log attendance in after joining");
    AssertTrue(source.Contains("LogAttendanceOutAsync"), "student native classroom must log attendance out on close");
    int joinIndex = source.IndexOf("JoinRoomAsync", StringComparison.Ordinal);
    int attendanceInIndex = source.IndexOf("LogAttendanceInAsync", StringComparison.Ordinal);
    AssertTrue(joinIndex >= 0, "student native classroom must join the room before attendance logging");
    AssertTrue(attendanceInIndex >= 0, "student native classroom must log attendance in after joining");
    AssertTrue(joinIndex < attendanceInIndex, "attendance in should be recorded only after the classroom join succeeds");
}

static void RunNativeClassroomLiveStatusRefreshTests()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

            VerifyStudentNativeClassroomLiveStatusRefresh();
            VerifyTeacherNativeClassroomLiveStatusRefresh();
            VerifyTeacherNativeClassroomScreenShareOffWithoutActivePresenterPreservesStage();
            VerifyTeacherNativeClassroomDisconnectResetsOnlyActivePresenter();
            VerifyTeacherNativeClassroomSafeDisconnectCleanupFromBackgroundThread();
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
}

static void VerifyStudentNativeClassroomLiveStatusRefresh()
{
    var form = (StudentNativeClassroomForm)RuntimeHelpers.GetUninitializedObject(typeof(StudentNativeClassroomForm));
    MethodInfo setLiveStatus = typeof(StudentNativeClassroomForm).GetMethod("SetLiveStatus", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("StudentNativeClassroomForm must expose SetLiveStatus for transient live state");
    MethodInfo update = typeof(StudentNativeClassroomForm).GetMethod("UpdateClassroomPresentation", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Cannot find student UpdateClassroomPresentation");

    SetFieldValue(form, "_statusLabel", new Label());
    SetFieldValue(form, "_mediaStateLabel", new Label());
    SetFieldValue(form, "_btnCamera", new Button());
    SetFieldValue(form, "_btnMic", new Button());
    SetFieldValue(form, "_btnShareScreen", new Button());
    SetFieldValue(form, "_isConnectedToClassroom", true);
    SetFieldValue(form, "_isCameraOn", true);
    SetFieldValue(form, "_isMicOn", false);

    setLiveStatus.Invoke(form, new object[] { "Đang xem video giáo viên - 10:15:00" });
    update.Invoke(form, new object[] { false });

    Label status = GetFieldValue<Label>(form, "_statusLabel");
    Label media = GetFieldValue<Label>(form, "_mediaStateLabel");
    AssertEqual("Đang xem video giáo viên - 10:15:00", status.Text);
    AssertTrue(media.Text.Length > 0, "student presenter refresh should still update media detail while live status is active");

    SetFieldValue(form, "_isConnectedToClassroom", false);
    update.Invoke(form, new object[] { true });
    AssertEqual("Mất kết nối", status.Text);
}

static void VerifyTeacherNativeClassroomLiveStatusRefresh()
{
    var form = (TeacherNativeClassroomForm)RuntimeHelpers.GetUninitializedObject(typeof(TeacherNativeClassroomForm));
    MethodInfo setLiveStatus = typeof(TeacherNativeClassroomForm).GetMethod("SetLiveStatus", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("TeacherNativeClassroomForm must expose SetLiveStatus for transient live state");
    MethodInfo update = typeof(TeacherNativeClassroomForm).GetMethod("UpdateClassroomPresentation", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Cannot find teacher UpdateClassroomPresentation");

    SetFieldValue(form, "_server", new TcpClassroomServer());
    SetFieldValue(form, "_statusLabel", new Label());
    SetFieldValue(form, "_participantStateLabel", new Label());
    SetFieldValue(form, "_btnCamera", new Button());
    SetFieldValue(form, "_btnMic", new Button());
    SetFieldValue(form, "_btnShareScreen", new Button());
    SetFieldValue(form, "_isServerReady", true);
    SetFieldValue(form, "_isCameraOn", true);
    SetFieldValue(form, "_isSharingScreen", true);

    setLiveStatus.Invoke(form, new object[] { "Đang xem Học viên A trình bày: Desktop - 10:16:00" });
    update.Invoke(form, new object[] { false });

    Label status = GetFieldValue<Label>(form, "_statusLabel");
    Label participants = GetFieldValue<Label>(form, "_participantStateLabel");
    AssertEqual("Đang xem Học viên A trình bày: Desktop - 10:16:00", status.Text);
    AssertTrue(participants.Text.Length > 0, "teacher presenter refresh should still update participant detail while live status is active");

    SetFieldValue(form, "_isServerReady", false);
    update.Invoke(form, new object[] { true });
    AssertEqual("Mất kết nối", status.Text);
}

static void VerifyTeacherNativeClassroomScreenShareOffWithoutActivePresenterPreservesStage()
{
    using var form = new TeacherNativeClassroomForm(654);
    _ = form.Handle;
    Application.DoEvents();

    MethodInfo setLiveStatus = typeof(TeacherNativeClassroomForm).GetMethod("SetLiveStatus", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("TeacherNativeClassroomForm must expose SetLiveStatus for transient live state");
    MethodInfo handleIncomingSignal = typeof(TeacherNativeClassroomForm).GetMethod("HandleIncomingSignal", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("TeacherNativeClassroomForm must handle incoming classroom signals");

    var preview = GetFieldValue<PictureBox>(form, "_teacherPreview");
    var placeholder = preview.Tag as Label
        ?? throw new InvalidOperationException("teacher preview placeholder must be attached to the preview tag");
    SetFieldValue(form, "_activeStudentScreenShareSenderId", 0);

    using var teacherStageFrame = new Bitmap(2, 2);
    preview.Image = teacherStageFrame;
    placeholder.Visible = false;
    setLiveStatus.Invoke(form, new object[] { "Đang dạy trực tuyến" });

    handleIncomingSignal.Invoke(form, new object[]
    {
        new ClassroomSignalModel
        {
            Type = ClassroomMessageType.ScreenShareOff,
            SenderRole = "STUDENT",
            SenderId = 17,
            SenderName = "Học viên B"
        }
    });

    AssertTrue(ReferenceEquals(preview.Image, teacherStageFrame),
        "student ScreenShareOff without an active student presenter must preserve the current teacher stage image");
    AssertEqual("Đang dạy trực tuyến", GetFieldValue<Label>(form, "_statusLabel").Text);
    AssertFalse(placeholder.Visible,
        "student ScreenShareOff without an active presenter must not restore the stage placeholder");
    AssertEqual(0, GetFieldValue<int>(form, "_activeStudentScreenShareSenderId"));
}

static void VerifyTeacherNativeClassroomDisconnectResetsOnlyActivePresenter()
{
    var form = (TeacherNativeClassroomForm)RuntimeHelpers.GetUninitializedObject(typeof(TeacherNativeClassroomForm));
    MethodInfo setLiveStatus = typeof(TeacherNativeClassroomForm).GetMethod("SetLiveStatus", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("TeacherNativeClassroomForm must expose SetLiveStatus for transient live state");
    MethodInfo update = typeof(TeacherNativeClassroomForm).GetMethod("UpdateClassroomPresentation", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Cannot find teacher UpdateClassroomPresentation");
    MethodInfo resetDisconnectedPresenter = typeof(TeacherNativeClassroomForm).GetMethod("ResetStageIfDisconnectedStudentWasActivePresenter", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("TeacherNativeClassroomForm must clear the active presenter state when that student disconnects");

    var preview = new PictureBox();
    var placeholder = new Label { Visible = false };
    preview.Tag = placeholder;

    SetFieldValue(form, "_server", new TcpClassroomServer());
    SetFieldValue(form, "_teacherPreview", preview);
    SetFieldValue(form, "_statusLabel", new Label());
    SetFieldValue(form, "_participantStateLabel", new Label());
    SetFieldValue(form, "_btnCamera", new Button());
    SetFieldValue(form, "_btnMic", new Button());
    SetFieldValue(form, "_btnShareScreen", new Button());
    SetFieldValue(form, "_isServerReady", true);
    SetFieldValue(form, "_isMicOn", true);

    using var preservedFrame = new Bitmap(2, 2);
    preview.Image = preservedFrame;
    SetFieldValue(form, "_activeStudentScreenShareSenderId", 42);
    setLiveStatus.Invoke(form, new object[] { "Đang xem Học viên A trình bày: Desktop - 10:16:00" });

    bool resetInactive = (bool)resetDisconnectedPresenter.Invoke(form, new object[] { 7, "Học viên B" })!;
    update.Invoke(form, new object[] { resetInactive });
    AssertFalse(resetInactive, "disconnecting a different student must not clear the active presenter");
    AssertEqual("Đang xem Học viên A trình bày: Desktop - 10:16:00", GetFieldValue<Label>(form, "_statusLabel").Text);
    AssertTrue(ReferenceEquals(preview.Image, preservedFrame), "inactive student disconnect must not replace the current stage frame");
    AssertEqual(42, GetFieldValue<int>(form, "_activeStudentScreenShareSenderId"));

    using var staleFrame = new Bitmap(3, 3);
    preview.Image = staleFrame;
    placeholder.Visible = false;
    SetFieldValue(form, "_activeStudentScreenShareSenderId", 42);
    setLiveStatus.Invoke(form, new object[] { "Đang xem Học viên A trình bày: Desktop - 10:16:30" });

    bool resetActive = (bool)resetDisconnectedPresenter.Invoke(form, new object[] { 42, "Học viên A" })!;
    update.Invoke(form, new object[] { resetActive });
    Label status = GetFieldValue<Label>(form, "_statusLabel");
    AssertTrue(resetActive, "disconnecting the active presenter must clear the live stage state");
    AssertEqual("Đang dạy trực tuyến", status.Text);
    AssertEqual(0, GetFieldValue<int>(form, "_activeStudentScreenShareSenderId"));
    AssertEqual(null, preview.Image);
    AssertTrue(placeholder.Visible, "disconnecting the active presenter must restore the stage placeholder");
    AssertTrue(placeholder.Text.Contains("Học viên A", StringComparison.Ordinal), "placeholder should identify the disconnected presenter");
    AssertImageDisposed(staleFrame, "disconnect cleanup must dispose the stale presenter frame");
}

static void VerifyTeacherNativeClassroomSafeDisconnectCleanupFromBackgroundThread()
{
    using var form = new TeacherNativeClassroomForm(321);
    _ = form.Handle;
    Application.DoEvents();

    MethodInfo setLiveStatus = typeof(TeacherNativeClassroomForm).GetMethod("SetLiveStatus", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("TeacherNativeClassroomForm must expose SetLiveStatus for transient live state");
    MethodInfo safeResetDisconnectedPresenter = typeof(TeacherNativeClassroomForm).GetMethod("SafeResetStageIfDisconnectedStudentWasActivePresenter", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("TeacherNativeClassroomForm must expose a safe disconnect cleanup wrapper");

    var preview = GetFieldValue<PictureBox>(form, "_teacherPreview");
    var placeholder = preview.Tag as Label
        ?? throw new InvalidOperationException("teacher preview placeholder must be attached to the preview tag");
    var status = GetFieldValue<Label>(form, "_statusLabel");

    SetFieldValue(form, "_isServerReady", true);
    SetFieldValue(form, "_isMicOn", true);
    string expectedBaselineStatus = ClassroomUxPresenter.Present(
        isTeacher: true,
        isConnected: true,
        isCameraOn: false,
        isMicOn: true,
        isSharingScreen: false,
        participantCount: 0).StatusText;

    using var staleFrame = new Bitmap(4, 4);
    preview.Image = staleFrame;
    placeholder.Visible = false;
    SetFieldValue(form, "_activeStudentScreenShareSenderId", 42);
    setLiveStatus.Invoke(form, new object[] { "Äang xem Há»c viÃªn A trÃ¬nh bÃ y: Desktop - 10:16:30" });

    Exception? backgroundFailure = null;
    var disconnectThread = new Thread(() =>
    {
        try
        {
            safeResetDisconnectedPresenter.Invoke(form, new object[] { 42, "Há»c viÃªn A" });
        }
        catch (Exception ex)
        {
            backgroundFailure = ex;
        }
    });

    disconnectThread.Start();
    disconnectThread.Join();
    if (backgroundFailure != null)
        throw new InvalidOperationException(backgroundFailure.Message, backgroundFailure);

    Application.DoEvents();

    AssertEqual(expectedBaselineStatus, status.Text);
    AssertEqual(0, GetFieldValue<int>(form, "_activeStudentScreenShareSenderId"));
    AssertEqual(null, preview.Image);
    AssertTrue(placeholder.Text.Contains("Há»c viÃªn A", StringComparison.Ordinal), "background disconnect cleanup placeholder should identify the disconnected presenter");
    AssertImageDisposed(staleFrame, "background disconnect cleanup must dispose the stale presenter frame");

    using var preservedFrame = new Bitmap(4, 4);
    preview.Image = preservedFrame;
    placeholder.Visible = false;
    SetFieldValue(form, "_activeStudentScreenShareSenderId", 7);
    setLiveStatus.Invoke(form, new object[] { "Äang xem Há»c viÃªn B trÃ¬nh bÃ y: Desktop - 10:18:00" });

    backgroundFailure = null;
    disconnectThread = new Thread(() =>
    {
        try
        {
            safeResetDisconnectedPresenter.Invoke(form, new object[] { 99, "Há»c viÃªn Z" });
        }
        catch (Exception ex)
        {
            backgroundFailure = ex;
        }
    });

    disconnectThread.Start();
    disconnectThread.Join();
    if (backgroundFailure != null)
        throw new InvalidOperationException(backgroundFailure.Message, backgroundFailure);

    Application.DoEvents();

    AssertEqual("Äang xem Há»c viÃªn B trÃ¬nh bÃ y: Desktop - 10:18:00", status.Text);
    AssertTrue(ReferenceEquals(preview.Image, preservedFrame), "inactive student disconnect must preserve the current stage frame");
    AssertEqual(7, GetFieldValue<int>(form, "_activeStudentScreenShareSenderId"));
}

static void RunTeacherAttendanceSummarySourceTests()
{
    string repoPath = Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Data", "TeacherRepository.cs");
    string controllerPath = Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Controllers", "TeacherController.cs");
    string modelPath = Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Models", "AttendanceLogModel.cs");
    string repo = File.ReadAllText(repoPath);
    string controller = File.ReadAllText(controllerPath);
    string model = File.ReadAllText(modelPath);
    AssertTrue(model.Contains("StudentName"), "attendance model must expose student name for grid display");
    AssertTrue(repo.Contains("GetAttendanceSummary"), "teacher repository must query attendance summary");
    AssertTrue(repo.Contains("attendance_logs"), "attendance summary must read from attendance_logs");
    AssertTrue(repo.Contains("c.teacher_id = @teacher_id"), "attendance summary must be scoped to the teacher's courses");
    AssertTrue(controller.Contains("GetAttendanceSummary"), "teacher controller must expose attendance summary");
}

static void RunTeacherAttendanceUiSourceTests()
{
    string pagePath = Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherStudents.cs");
    string dialogPath = Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherAttendanceDialog.cs");
    string page = File.ReadAllText(pagePath);
    string dialog = File.ReadAllText(dialogPath);
    AssertTrue(File.Exists(dialogPath), "teacher attendance dialog must exist");
    AssertTrue(page.Contains("TeacherAttendanceDialog"), "teacher students page must open attendance dialog");
    AssertTrue(page.Contains("AddHeaderAction"), "attendance action should be added to the existing grid page header");
    AssertTrue(dialog.Contains("_isBindingSessions"), "attendance dialog must guard initial session binding from duplicate attendance loads");
    AssertTrue(dialog.Contains("if (_isBindingSessions)"), "attendance dialog session changed handler must skip initial binding");
}

static void RunAttendanceCleanupAndSchemaSourceTests()
{
    string classroomPath = Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "StudentNativeClassroomForm.cs");
    string repoPath = Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Data", "TeacherRepository.cs");
    string classroom = File.ReadAllText(classroomPath);
    string repo = File.ReadAllText(repoPath);

    AssertTrue(classroom.Contains("_isClosing"), "native classroom close handler must guard re-entrant close");
    AssertTrue(classroom.Contains("_cleanupComplete"), "native classroom must track when async close cleanup is complete");
    AssertTrue(classroom.Contains("_attendanceInTask"), "native classroom must retain in-flight attendance-in task during close");
    AssertTrue(classroom.Contains("await _attendanceInTask"), "native classroom close cleanup must await in-flight attendance-in");
    AssertTrue(classroom.Contains("if (_isClosing)"), "native classroom OnShown path must guard close state");
    AssertTrue(classroom.Contains("e.Cancel = true"), "native classroom close handler must cancel the first close while async cleanup runs");
    AssertTrue(classroom.Contains("if (_cleanupComplete)"), "native classroom final programmatic close must be allowed after cleanup");
    AssertTrue(classroom.Contains("_cleanupComplete = true"), "native classroom must mark cleanup complete before final programmatic close");
    int closeHandlerIndex = classroom.IndexOf("private async void StudentNativeClassroomForm_FormClosing", StringComparison.Ordinal);
    int cleanupGuardIndex = classroom.IndexOf("if (_cleanupComplete)", closeHandlerIndex, StringComparison.Ordinal);
    int cancelIndex = classroom.IndexOf("e.Cancel = true", closeHandlerIndex, StringComparison.Ordinal);
    int inProgressGuardIndex = classroom.IndexOf("if (_isClosing)", closeHandlerIndex, StringComparison.Ordinal);
    AssertTrue(cleanupGuardIndex >= 0 && cancelIndex >= 0 && cleanupGuardIndex < cancelIndex, "native classroom must allow completed cleanup close before canceling active cleanup closes");
    AssertTrue(cancelIndex >= 0 && inProgressGuardIndex > cancelIndex, "native classroom re-entrant close must be canceled before returning while cleanup is in progress");
    AssertTrue(classroom.Contains("CloseClassroomAsync"), "native classroom close cleanup should be moved to an async helper");
    AssertTrue(classroom.Contains("await LogAttendanceOutIfPossibleAsync()"), "native classroom close cleanup must await attendance out");
    AssertTrue(repo.Contains("CREATE TABLE IF NOT EXISTS attendance_logs"), "teacher repository schema must create attendance_logs on clean databases");
    AssertTrue(repo.Contains("idx_attendance_logs_session_student"), "teacher repository schema must create session/student attendance index");
    AssertTrue(repo.Contains("idx_attendance_logs_open_session_student"), "teacher repository schema must create open attendance index");
}

static void RunChatUnreadSourceTests()
{
    string dbPath = Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Data", "CourseGuardDbContext.cs");
    string controllerPath = Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Backend", "Controllers", "ChatController.cs");
    string db = File.ReadAllText(dbPath);
    string controller = File.ReadAllText(controllerPath);
    AssertTrue(db.Contains("GetUnreadChatCount"), "db context must expose unread chat count");
    AssertTrue(db.Contains("MarkAllChatRead"), "db context must expose mark all read");
    AssertTrue(db.Contains("MarkCourseChatRead"), "db context must expose course-scoped mark read");
    AssertTrue(db.Contains("CHAT_READS"), "unread logic must use CHAT_READS");
    AssertTrue(db.Contains("LAST_READ_MESSAGE_ID"), "unread logic must compare against last read message id");
    AssertTrue(db.Contains("m.SENDER_ID <> @user_id"), "unread count must exclude messages sent by the current user");
    AssertTrue(db.Contains("COALESCE(m.IS_DELETED, FALSE) = FALSE"), "unread count must exclude deleted messages");
    AssertTrue(db.Contains("c.TEACHER_ID = @user_id"), "unread visibility must include teacher-owned courses");
    AssertTrue(db.Contains("FROM ENROLLMENTS e"), "unread visibility must include enrolled student courses");
    AssertTrue(db.Contains("UPPER(COALESCE(e.STATUS, 'ACTIVE')) IN ('ACTIVE', 'APPROVED')"), "unread visibility must require active or approved enrollment");
    AssertTrue(db.Contains("LEFT JOIN CHAT_READS cr"), "unread count must join chat read watermarks");
    AssertTrue(db.Contains("cr.LAST_READ_MESSAGE_ID IS NULL OR m.ID > cr.LAST_READ_MESSAGE_ID"), "unread count must compare message id to last read watermark");
    AssertTrue(db.Contains("ON CONFLICT (USER_ID, COURSE_ID) DO UPDATE"), "mark read must upsert one read watermark per course");
    AssertTrue(db.Contains("WHERE m.COURSE_ID = @course_id"), "course-scoped mark read must only advance one selected course");
    AssertTrue(db.Contains("WHERE CHAT_READS.LAST_READ_MESSAGE_ID IS NULL"), "mark read must guard null existing watermarks");
    AssertTrue(db.Contains("EXCLUDED.LAST_READ_MESSAGE_ID > CHAT_READS.LAST_READ_MESSAGE_ID"), "mark read must not move the watermark backwards");
    AssertTrue(controller.Contains("GetUnreadCount"), "chat controller must expose unread count");
    AssertTrue(controller.Contains("MarkAllRead"), "chat controller must expose mark all read");
    AssertTrue(controller.Contains("MarkCourseRead"), "chat controller must expose course-scoped mark read");
}

static void RunSidebarChatBadgeSourceTests()
{
    string sidebarPath = Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Theme", "SidebarPanel.cs");
    string badgePath = Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Theme", "CountBadgePainter.cs");
    string sidebar = File.ReadAllText(sidebarPath);
    string badge = File.ReadAllText(badgePath);
    AssertTrue(File.Exists(badgePath), "count badge painter helper must exist");
    AssertTrue(sidebar.Contains("ChatUnreadCount"), "sidebar must expose chat unread count");
    AssertTrue(sidebar.Contains("IsChatNavItem(iconKey)"), "sidebar badge must target the stable nav icon key");
    AssertTrue(sidebar.Contains("\"message\""), "sidebar badge must target the message nav icon key");
    AssertFalse(sidebar.Contains("Tin nhÃ¡ÂºÂ¯n"), "sidebar badge should not depend on mojibake label fallbacks");
    AssertTrue(sidebar.Contains("CountBadgePainter.Draw"), "sidebar must draw the badge through the helper");
    AssertTrue(sidebar.Contains("_chatUnreadCount > 99 ? 26 : badgeSize"), "sidebar must reserve pill width for capped counts");
    AssertTrue(badge.Contains("99+"), "count badge painter must cap large counts");
    AssertTrue(badge.Contains("text.Length >= 3"), "count badge painter must widen three-character labels");
    AssertTrue(badge.Contains("AddArc"), "count badge painter must draw pill badges for wider labels");
}

static void RunStudentDashboardChatBadgeSourceTests()
{
    string path = Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "StudentDashboard.cs");
    string source = File.ReadAllText(path);
    AssertTrue(source.Contains("ChatController"), "student dashboard must use chat controller for unread count");
    AssertTrue(source.Contains("GetUnreadCount"), "student dashboard must load unread count");
    AssertTrue(source.Contains("SetChatUnreadCount"), "student dashboard must update course-first sidebar chat badge");
    AssertTrue(source.Contains("MessagesMarkedRead"), "student dashboard must reload unread count when the workspace marks messages read");
    AssertTrue(source.Contains("LoadChatUnreadCountAsync().FireAndForgetSafe(this)"), "student dashboard must load chat unread count without blocking construction");
    AssertTrue(source.Contains("Task.Run"), "student dashboard must move chat unread database work off the UI thread");
    AssertTrue(source.Contains("SetChatUnreadCountSafe"), "student dashboard must marshal unread badge UI updates safely");
    AssertTrue(source.Contains("BeginInvoke"), "student dashboard must use BeginInvoke for cross-thread unread badge updates");
    AssertTrue(source.Contains("ReloadChatUnreadCountSafe"), "student dashboard must safely reload total unread from workspace read events");
    AssertTrue(source.Contains("private const string CourseWorkspacePage"), "student dashboard should use one course workspace key for unread guards");
    AssertTrue(source.Contains("_chatUnreadLoadVersion"), "student dashboard must version unread badge loads");
    AssertTrue(source.Contains("int loadVersion = _chatUnreadLoadVersion"), "student dashboard must capture unread badge load version before background work");
    AssertTrue(source.Contains("_chatUnreadLoadVersion++"), "student dashboard must invalidate pending unread badge loads when reloading chat unread");
    AssertTrue(source.Contains("SetChatUnreadCountSafe(unreadCount, loadVersion)"), "student dashboard must pass unread load version into UI update");
    AssertTrue(source.Contains("loadVersion != _chatUnreadLoadVersion"), "student dashboard must suppress stale unread badge updates");
    AssertTrue(source.Contains("chatPanel.MessagesMarkedRead += (_, _) => ReloadChatUnreadCountSafe();"),
        "student dashboard must wire workspace read events to a total unread badge reload");
    int reloadMethodIndex = source.IndexOf("private void ReloadChatUnreadCountSafe()", StringComparison.Ordinal);
    int loadCallIndex = source.IndexOf("LoadChatUnreadCountAsync().FireAndForgetSafe(this)", reloadMethodIndex, StringComparison.Ordinal);
    AssertTrue(reloadMethodIndex >= 0 && loadCallIndex > reloadMethodIndex,
        "workspace read reload helper must reload total unread count");
    AssertFalse(source.Contains("chatPanel.MessagesMarkedRead += (_, _) => ClearChatUnreadCountSafe();"),
        "workspace read events must not blindly clear the total unread badge");
    AssertFalse(source.Contains("chatPanel.MessagesMarkedRead += (_, _) => _sidebar.SetChatUnreadCount(0);"),
        "workspace read events must not set total unread badge to zero directly");
}

static void RunStudentChatPageReadWatermarkSourceTests()
{
    string path = Path.Combine(RepoRoot(), "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Shared", "CourseWorkspace", "CourseChatPanel.cs");
    string source = File.ReadAllText(path);
    AssertTrue(source.Contains("MarkDisplayedMessagesReadAsync"), "course chat panel must own a read watermark advancement helper");
    AssertTrue(source.Contains("int courseId = _selectedCourseId"), "course chat panel must capture selected course before background mark-read");
    AssertTrue(source.Contains("_selectedCourseId != courseId"), "course chat panel must ignore stale refreshes after selected course changes");
    AssertTrue(source.Contains("_chatController.MarkCourseRead(userId, courseId)"), "course chat panel must advance only the selected course read watermark through ChatController");
    AssertFalse(source.Contains("MarkAllRead"), "course chat panel must not mark every course read during selected-course refresh");
    AssertTrue(source.Contains("MarkDisplayedMessagesReadAsync(courseId).FireAndForgetSafe(this)"), "course chat panel must advance read watermark in the background after refresh");
    int emptyStateIndex = source.IndexOf("if (messages.Count == 0)", StringComparison.Ordinal);
    int markReadIndex = source.IndexOf("MarkDisplayedMessagesReadAsync(courseId).FireAndForgetSafe(this)", StringComparison.Ordinal);
    AssertTrue(emptyStateIndex >= 0 && markReadIndex > emptyStateIndex, "course chat panel must mark read only after successful message rendering");
}

static void RunDashboardCardTests()
{
    using var studentGrid = new DataGridView();
    using var teacherGrid = new DataGridView();

    Control studentCard = InvokeChromeDataCard(
        "CourseGuard.Frontend.UserControls.Student.StudentTabChrome",
        "Danh sach",
        studentGrid);
    Control teacherCard = InvokeChromeDataCard(
        "CourseGuard.Frontend.UserControls.Teacher.TeacherTabChrome",
        "Danh sach",
        teacherGrid);

    AssertGridIsWrappedInRoundedBody(studentCard, studentGrid, "student table data card");
    AssertGridIsWrappedInRoundedBody(teacherCard, teacherGrid, "teacher table data card");

    studentCard.Dispose();
    teacherCard.Dispose();
}

static void RunDashboardHeaderSubtitleLayoutTests()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            using Control studentHeader = InvokeChromeHeader(
                "CourseGuard.Frontend.UserControls.Student.StudentTabChrome",
                "Tổng quan cá nhân",
                "Theo dõi khóa học đang tham gia, bài kiểm tra, thông báo và hoạt động gần đây.");
            using Control teacherHeader = InvokeChromeHeader(
                "CourseGuard.Frontend.UserControls.Teacher.TeacherTabChrome",
                "Lịch dạy",
                "Quản lý lịch học và mở lớp trực tuyến");

            AssertHeaderSubtitleHasDescenderRoom(studentHeader, "student header");
            AssertHeaderSubtitleHasDescenderRoom(teacherHeader, "teacher header");
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
}

static void RunRoundedChromeClippingGuardTests()
{
    string repoRoot = RepoRoot();
    string metaTheme = File.ReadAllText(Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Frontend", "Theme", "MetaTheme.cs"));
    string dropdownStyler = File.ReadAllText(Path.Combine(repoRoot, "CourseGuard", "CourseGuard", "Frontend", "Theme", "StudentDropdownStyler.cs"));

    AssertTrue(
        metaTheme.Contains("new Rectangle(-1, -1, Width + 2, Height + 2), CornerRadius + 1", StringComparison.Ordinal),
        "themed message dialog region should overscan rounded corners so the outer border is not clipped");
    AssertTrue(
        metaTheme.Contains("Margin = new Padding(CornerRadius, 0, CornerRadius, 0)", StringComparison.Ordinal),
        "themed message dialog tone strip should stay clear of rounded top corners");
    AssertTrue(
        dropdownStyler.Contains("new RectangleF(1f, 1f, combo.Width - 2f, combo.Height - 2f)", StringComparison.Ordinal),
        "styled combobox chrome should draw its border fully inside the window DC to preserve the bottom edge");
    AssertTrue(
        dropdownStyler.Contains("combo.Height - 2", StringComparison.Ordinal)
            && dropdownStyler.Contains("g.DrawLine(pen, 10, bottomY, combo.Width - 11, bottomY)", StringComparison.Ordinal),
        "styled combobox chrome should explicitly redraw the bottom border inside the control");
}

static void RunStudentGridPageBaseTests()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            using var page = new TestStudentGridPage();
            TableLayoutPanel root = page.Controls.OfType<TableLayoutPanel>().Single();

            AssertEqual(0, page.CreateTableCallCount);
            AssertEqual(1, root.ColumnCount);
            AssertEqual(2, root.RowCount);
            AssertTrue(page.Controls.Contains(root), "base page should create the root layout");
            AssertTrue(GetAllControls(page).Contains(page.ExposedGrid), "root layout should contain the grid");
            AssertTrue(GetAllControls(page).Contains(page.ExposedRefreshButton), "header should contain the refresh button");
            AssertTrue(page.ExposedSearchBox != null, "base page should create requested search input");
            AssertTrue(page.ExposedCourseFilter != null, "base page should create requested course filter");
            AssertTrue(page.ExposedSearchButton != null, "base page should create requested search button");
            AssertTrue(GetAllControls(page).Contains(page.ExposedSearchBox!), "header should contain the search input");
            AssertTrue(GetAllControls(page).Contains(page.ExposedCourseFilter!), "header should contain the course filter");
            AssertTrue(GetAllControls(page).Contains(page.ExposedSearchButton!), "header should contain the search button");

            AssertTrue(page.ExposedGrid.ReadOnly, "grid should be styled readonly");
            AssertEqual(BorderStyle.None, page.ExposedGrid.BorderStyle);
            AssertEqual(DataGridViewSelectionMode.FullRowSelect, page.ExposedGrid.SelectionMode);
            AssertFalse(page.ExposedGrid.EnableHeadersVisualStyles, "grid should use theme header styles");

            page.LoadForTestAsync().GetAwaiter().GetResult();

            AssertEqual(1, page.CreateTableCallCount);
            AssertTrue(page.ExposedBindingSource.DataSource is DataTable, "load should bind a DataTable");
            AssertFalse(page.ExposedGrid.Visible, "empty table should hide the grid");
            AssertTrue(page.ExposedGridBody.Controls.Contains(page.ExposedEmptyStateLabel), "table body should contain empty label");
            AssertTrue(page.ExposedEmptyStateLabel.Visible, "empty table should show empty state label");
            AssertEqual("No rows for this test.", page.ExposedEmptyStateLabel.Text);

            page.RebindCachedRows();

            AssertTrue(page.ExposedBindingSource.DataSource is DataTable, "successful empty load should allow local rebind");
            AssertFalse(page.ExposedGrid.Visible, "empty rebind should keep the grid hidden");
            AssertEqual("No rows for this test.", page.ExposedEmptyStateLabel.Text);
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
}

static void RunStudentGridPageHeaderActionTests()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            using var page = new TestStudentGridPage();
            TableLayoutPanel root = page.Controls.OfType<TableLayoutPanel>().Single();
            Control header = root.GetControlFromPosition(0, 0)
                ?? throw new InvalidOperationException("base page should create a header");
            TableLayoutPanel headerLayout = header.Controls.OfType<TableLayoutPanel>().Single();
            Control titleStack = headerLayout.GetControlFromPosition(0, 0)
                ?? throw new InvalidOperationException("header should contain a title stack");
            Control actionStrip = headerLayout.GetControlFromPosition(1, 0)
                ?? throw new InvalidOperationException("header should contain an action strip");

            AssertFalse(titleStack.Controls.Contains(page.ExposedExtraAction), "derived action must not be added to title stack");
            AssertTrue(actionStrip.Controls.Contains(page.ExposedExtraAction), "derived action should be added to header action strip");
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
}

static void RunStudentGridPageRefreshFailureTests()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            using var page = new TestStudentGridPage();
            page.SetNextRows("Cached row");
            page.LoadForTestAsync().GetAwaiter().GetResult();

            DataTable boundBeforeFailure = page.ExposedBindingSource.DataSource as DataTable
                ?? throw new InvalidOperationException("successful load should bind a DataTable");
            AssertEqual(1, boundBeforeFailure.Rows.Count);
            AssertTrue(page.ExposedGrid.Visible, "successful load should show the grid");

            page.FailNextLoad();
            RunWithAutoClosingThemedDialog(() => page.LoadForTestAsync());

            AssertTrue(ReferenceEquals(boundBeforeFailure, page.ExposedBindingSource.DataSource), "failed refresh should preserve prior binding");
            AssertTrue(page.ExposedGrid.Visible, "failed refresh should not hide previously bound rows");
            AssertEqual(1, page.ExposedGrid.Rows.Count);

            page.RebindCachedRows();

            AssertTrue(page.ExposedGrid.Visible, "cache rebind should keep rows visible");
            AssertEqual(1, page.ExposedGrid.Rows.Count);
            AssertEqual("Cached row", page.ExposedGrid.Rows[0].Cells["Name"].Value?.ToString());
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
}

static void RunStudentGridPageInitialFailureRebindTests()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            using var page = new TestStudentGridPage();
            page.FailNextLoad();
            RunWithAutoClosingThemedDialog(() => page.LoadForTestAsync());

            page.RebindCachedRows();

            AssertEqual<object?>(null, page.ExposedBindingSource.DataSource);
            AssertFalse(page.ExposedGrid.Visible, "search rebind after initial failure should keep the grid hidden");
            AssertTrue(page.ExposedEmptyStateLabel.Visible, "search rebind after initial failure should preserve error state");
            AssertEqual("Không thể tải dữ liệu.", page.ExposedEmptyStateLabel.Text);
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
}

static void RunStudentGridPageInitialFailureTests()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            using var page = new TestStudentGridPage();
            page.FailNextLoad();
            RunWithAutoClosingThemedDialog(() => page.LoadForTestAsync());

            AssertEqual<object?>(null, page.ExposedBindingSource.DataSource);
            AssertFalse(page.ExposedGrid.Visible, "initial failure should hide the grid");
            AssertTrue(page.ExposedEmptyStateLabel.Visible, "initial failure should show the error empty state");
            AssertEqual("Không thể tải dữ liệu.", page.ExposedEmptyStateLabel.Text);
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
}

static void RunTeacherGridPageStaleLoadVersioningTests()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            using var page = new TestTeacherGridPage();
            page.BeginControlledLoads();

            TaskCompletionSource<DataTable> staleSuccess = page.EnqueuePendingLoad();
            TaskCompletionSource<DataTable> currentSuccess = page.EnqueuePendingLoad();

            Task firstLoad = page.LoadForTestAsync();
            Task secondLoad = page.LoadForTestAsync();

            currentSuccess.SetResult(TestTeacherGridPage.CreateTable("new"));
            WaitForTaskWithMessagePump(secondLoad);

            staleSuccess.SetResult(TestTeacherGridPage.CreateTable("old"));
            WaitForTaskWithMessagePump(Task.WhenAll(firstLoad, secondLoad));

            DataTable afterStaleSuccess = page.ExposedBindingSource.DataSource as DataTable
                ?? throw new InvalidOperationException("teacher grid page should bind a DataTable after successful refresh");
            AssertEqual(1, afterStaleSuccess.Rows.Count);
            AssertEqual("new", afterStaleSuccess.Rows[0]["Name"]?.ToString());
            AssertEqual("new", page.ExposedGrid.Rows[0].Cells["Name"].Value?.ToString());

            TaskCompletionSource<DataTable> staleFailure = page.EnqueuePendingLoad();
            TaskCompletionSource<DataTable> currentAfterFailure = page.EnqueuePendingLoad();

            Task thirdLoad = page.LoadForTestAsync();
            Task fourthLoad = page.LoadForTestAsync();

            currentAfterFailure.SetResult(TestTeacherGridPage.CreateTable("latest"));
            WaitForTaskWithMessagePump(fourthLoad);

            RunWithAutoClosingThemedDialog(() =>
            {
                staleFailure.SetException(new InvalidOperationException("stale failure"));
                WaitForTaskWithMessagePump(Task.WhenAll(thirdLoad, fourthLoad));
                return Task.CompletedTask;
            });

            DataTable afterStaleFailure = page.ExposedBindingSource.DataSource as DataTable
                ?? throw new InvalidOperationException("teacher grid page should preserve latest bound table after stale failure");
            AssertEqual(1, afterStaleFailure.Rows.Count);
            AssertEqual("latest", afterStaleFailure.Rows[0]["Name"]?.ToString());
            AssertEqual("latest", page.ExposedGrid.Rows[0].Cells["Name"].Value?.ToString());
            AssertTrue(page.ExposedGrid.Visible, "stale failure should not hide rows from the newer successful load");
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
}

static void RunTeacherScheduleFailureCleanupTests()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

            var page = (UC_TeacherSchedule)RuntimeHelpers.GetUninitializedObject(typeof(UC_TeacherSchedule));
            SetFieldValue(page, "_sessions", new List<TeacherScheduleItemModel>
            {
                new() { Id = 10, CourseId = 1, CourseName = "Lap trinh mang", Title = "TCP", StartTime = DateTime.Now }
            });
            SetFieldValue(page, "_filteredSessions", new List<TeacherScheduleItemModel>
            {
                new() { Id = 10, CourseId = 1, CourseName = "Lap trinh mang", Title = "TCP", StartTime = DateTime.Now }
            });
            SetFieldValue(page, "_filterCache", new Dictionary<int, (List<TeacherScheduleItemModel>, Dictionary<DateTime, List<TeacherScheduleItemModel>>, DateTime, DateTime, string)>());
            SetFieldValue(page, "_selectedCalendarSessionId", 10);

            var dataCard = new RoundedPanel { Width = 480, Height = 320 };
            var grid = new DataGridView();
            grid.Columns.Add("Id", "Id");
            grid.Rows.Add(10);
            grid.CurrentCell = grid.Rows[0].Cells[0];
            var calendarView = new TableLayoutPanel();
            calendarView.Controls.Add(new Label { Text = "stale" });
            var emptyState = new Label { Visible = false };
            var openButton = new Button { Text = "Mở lớp", Enabled = true };
            var editButton = new Button { Enabled = true };
            var deleteButton = new Button { Enabled = true };
            var summaryTitle = new Label { Text = "stale title" };
            var summaryDetail = new Label { Text = "stale detail" };
            var summaryAction = new Label { Text = "stale action" };

            SetFieldValue(page, "_dataCard", dataCard);
            SetFieldValue(page, "_grid", grid);
            SetFieldValue(page, "_calendarView", calendarView);
            SetFieldValue(page, "_emptyStateLabel", emptyState);
            SetFieldValue(page, "_btnOpenClass", openButton);
            SetFieldValue(page, "_btnEdit", editButton);
            SetFieldValue(page, "_btnDelete", deleteButton);
            SetFieldValue(page, "_selectedSessionTitle", summaryTitle);
            SetFieldValue(page, "_selectedSessionDetail", summaryDetail);
            SetFieldValue(page, "_selectedSessionAction", summaryAction);

            MethodInfo clear = typeof(UC_TeacherSchedule).GetMethod("ClearScheduleStateAfterLoadFailure", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Cannot find ClearScheduleStateAfterLoadFailure");
            clear.Invoke(page, new object[] { "Lỗi tải lịch dạy: boom" });

            AssertEqual(0, GetFieldValue<List<TeacherScheduleItemModel>>(page, "_sessions").Count);
            AssertEqual(0, GetFieldValue<List<TeacherScheduleItemModel>>(page, "_filteredSessions").Count);
            AssertEqual(0, ((System.Collections.IDictionary)typeof(UC_TeacherSchedule).GetField("_filterCache", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!).Count);
            AssertTrue(typeof(UC_TeacherSchedule).GetField("_selectedCalendarSessionId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page) == null,
                "failure cleanup must clear selected calendar session id");
            AssertTrue(grid.CurrentCell == null, "failure cleanup must clear current grid cell");
            AssertEqual(0, calendarView.Controls.Count);
            AssertFalse(grid.Visible, "failure cleanup must hide stale grid");
            AssertFalse(calendarView.Visible, "failure cleanup must hide stale calendar");
            AssertTrue(emptyState.Visible, "failure cleanup must show empty/error state");
            AssertEqual("Lỗi tải lịch dạy: boom", emptyState.Text);
            AssertEqual("Dạy online", openButton.Text);
            AssertFalse(openButton.Enabled, "failure cleanup must disable open class");
            AssertFalse(editButton.Enabled, "failure cleanup must disable edit");
            AssertFalse(deleteButton.Enabled, "failure cleanup must disable delete");
            AssertEqual("Chưa chọn lịch dạy", summaryTitle.Text);
            AssertTrue(summaryDetail.Text.Contains("Chọn một buổi dạy"), "failure cleanup must reset summary detail");
            AssertEqual("Hành động: Dạy online", summaryAction.Text);
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
}

static void RunStudentScheduleFailureCleanupTests()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

            var page = (UC_Schedule)RuntimeHelpers.GetUninitializedObject(typeof(UC_Schedule));
            SetFieldValue(page, "_sessions", new List<StudentScheduleItemModel>
            {
                new() { SessionId = 21, CourseName = "Lap trinh mang", Title = "TCP", TeacherName = "GV A", StartTime = DateTime.Now, MeetingLink = "tcp" }
            });
            SetFieldValue(page, "_filterCache", new Dictionary<int, (List<StudentScheduleItemModel>, Dictionary<DateTime, List<StudentScheduleItemModel>>, DateTime, DateTime, string)>());
            SetFieldValue(page, "_selectedCalendarSessionId", 21);

            var scheduleBody = new RoundedPanel { Width = 480, Height = 320 };
            var grid = new DataGridView();
            grid.Columns.Add("SessionId", "SessionId");
            grid.Rows.Add(21);
            grid.CurrentCell = grid.Rows[0].Cells[0];
            var calendarView = new TableLayoutPanel();
            calendarView.Controls.Add(new Label { Text = "stale" });
            var emptyState = new Label { Visible = false };
            var joinButton = new Button { Text = "Vào lớp", Enabled = true };
            var quickNoteButton = new Button { Enabled = true };
            var summaryTitle = new Label { Text = "stale title" };
            var summaryDetail = new Label { Text = "stale detail" };
            var summaryAction = new Label { Text = "stale action" };

            SetFieldValue(page, "_scheduleBody", scheduleBody);
            SetFieldValue(page, "dgvSchedule", grid);
            SetFieldValue(page, "_calendarView", calendarView);
            SetFieldValue(page, "_emptyStateLabel", emptyState);
            SetFieldValue(page, "btnJoinOnline", joinButton);
            SetFieldValue(page, "_btnQuickNote", quickNoteButton);
            SetFieldValue(page, "_selectedSessionTitle", summaryTitle);
            SetFieldValue(page, "_selectedSessionDetail", summaryDetail);
            SetFieldValue(page, "_selectedSessionAction", summaryAction);

            MethodInfo clear = typeof(UC_Schedule).GetMethod("ClearScheduleStateAfterLoadFailure", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Cannot find ClearScheduleStateAfterLoadFailure");
            clear.Invoke(page, new object[] { "Lỗi tải lịch học: boom" });

            AssertEqual(0, GetFieldValue<List<StudentScheduleItemModel>>(page, "_sessions").Count);
            AssertEqual(0, ((System.Collections.IDictionary)typeof(UC_Schedule).GetField("_filterCache", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!).Count);
            AssertTrue(typeof(UC_Schedule).GetField("_selectedCalendarSessionId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page) == null,
                "failure cleanup must clear selected calendar session id");
            AssertTrue(grid.CurrentCell == null, "failure cleanup must clear current grid cell");
            AssertEqual(0, calendarView.Controls.Count);
            AssertFalse(grid.Visible, "failure cleanup must hide stale grid");
            AssertFalse(calendarView.Visible, "failure cleanup must hide stale calendar");
            AssertTrue(emptyState.Visible, "failure cleanup must show empty/error state");
            AssertEqual("Lỗi tải lịch học: boom", emptyState.Text);
            AssertEqual("Chưa chọn buổi học", summaryTitle.Text);
            AssertTrue(summaryDetail.Text.Contains("Chọn một buổi học"), "failure cleanup must reset summary detail");
            AssertEqual("Hành động: Tham gia online", summaryAction.Text);
            AssertEqual("Tham gia online", joinButton.Text);
            AssertFalse(joinButton.Enabled, "failure cleanup must disable join");
            AssertFalse(quickNoteButton.Enabled, "failure cleanup must disable quick note");
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
}

static void RunTeacherSimpleItemDialogValidationTests()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

            var dialog = (TeacherSimpleItemDialog)RuntimeHelpers.GetUninitializedObject(typeof(TeacherSimpleItemDialog));
            MethodInfo validate = typeof(TeacherSimpleItemDialog).GetMethod("ValidateBeforeSave", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Cannot find ValidateBeforeSave");
            MethodInfo hookInputs = typeof(TeacherSimpleItemDialog).GetMethod("HookValidationInputs", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Cannot find HookValidationInputs");

            TeacherCourseModel course = new() { Id = 12, Name = "Lập trình mạng" };
            ComboBox courseCombo = new();
            courseCombo.DisplayMember = nameof(TeacherCourseModel.Name);
            courseCombo.Items.Add(course);
            courseCombo.SelectedIndex = 0;
            TextBox title = new() { Text = string.Empty };
            TextBox details = new();
            DateTimePicker date = new() { Value = new DateTime(2026, 6, 15, 0, 0, 0) };
            DateTimePicker start = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Value = new DateTime(2026, 6, 15, 9, 0, 0) };
            DateTimePicker end = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Value = new DateTime(2026, 6, 15, 8, 0, 0) };
            ComboBox status = new();
            Label validationSummary = new() { Visible = false };

            SetFieldValue(dialog, "CourseCombo", courseCombo);
            SetFieldValue(dialog, "TitleTextBox", title);
            SetFieldValue(dialog, "DetailsTextBox", details);
            SetFieldValue(dialog, "DatePicker", date);
            SetFieldValue(dialog, "StartTimePicker", start);
            SetFieldValue(dialog, "EndTimePicker", end);
            SetFieldValue(dialog, "StatusCombo", status);
            SetFieldValue(dialog, "_validationSummary", validationSummary);
            SetFieldValue(dialog, "_enableTimeRange", true);

            hookInputs.Invoke(dialog, Array.Empty<object>());

            bool initialValid = (bool)validate.Invoke(dialog, Array.Empty<object>())!;
            AssertFalse(initialValid, "empty title must fail validation");
            AssertTrue(validationSummary.Visible, "invalid save must show inline validation summary");
            AssertEqual("Vui lòng nhập tiêu đề.", validationSummary.Text);

            title.Text = "Buổi 1";
            AssertTrue(validationSummary.Visible, "fixing title while time is still invalid should keep summary visible");
            AssertEqual("Thời gian kết thúc phải sau thời gian bắt đầu.", validationSummary.Text);

            end.Value = start.Value.AddHours(2);
            AssertFalse(validationSummary.Visible, "fixing time range should clear visible validation without another save");
            AssertEqual(string.Empty, validationSummary.Text);

            bool validAfterFix = (bool)validate.Invoke(dialog, Array.Empty<object>())!;
            AssertTrue(validAfterFix, "dialog should validate successfully after title and time are fixed");
            AssertFalse(validationSummary.Visible, "successful validation must hide validation summary");
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
}

static void RunModalDialogAdminInputStyleSourceTests()
{
    string root = RepoRoot();
    string themePath = Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Theme", "DialogInputStyler.cs");
    AssertTrue(File.Exists(themePath), "shared dialog input styler must exist in the theme layer");

    string stylerSource = File.ReadAllText(themePath);
    string baseSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "ThemedDialogBase.cs"));
    string simpleDialogSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherSimpleItemDialog.cs"));
    string examDialogSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherExamDialog.cs"));
    string quickNoteSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "QuickNoteDialog.cs"));
    string lessonDetailSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Student", "StudentLessonDetailDialog.cs"));
    string pollSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "CreatePollDialog.cs"));

    AssertTrue(stylerSource.Contains("public static class DialogInputStyler"), "dialog input styling must be centralized");
    AssertTrue(stylerSource.Contains("TextBoxBase") && stylerSource.Contains("DateTimePicker")
        && stylerSource.Contains("NumericUpDown") && stylerSource.Contains("CheckedListBox"),
        "dialog styler must cover textbox, date/time, numeric and checked-list inputs");
    AssertTrue(stylerSource.Contains("StudentDropdownStyler.StyleComboBox"),
        "dialog styler must reuse the admin/student dropdown chrome");
    AssertTrue(stylerSource.Contains("AppColors.BgInput") && stylerSource.Contains("AppColors.BorderStrong"),
        "dialog styler must use the shared admin theme input tokens");
    AssertTrue(stylerSource.Contains("BorderStyle.None"),
        "dialog text and numeric inputs must avoid old fixed single borders");

    AssertTrue(baseSource.Contains("DialogInputStyler.ApplyAdminStyle(ContentPanel)"),
        "ThemedDialogBase must apply shared input styling to teacher/student/admin modal content");
    AssertTrue(quickNoteSource.Contains("DialogInputStyler.ApplyAdminStyle(this)"),
        "QuickNoteDialog is a direct Form and must opt in to shared input styling");
    AssertTrue(lessonDetailSource.Contains("DialogInputStyler.ApplyAdminStyle(this)"),
        "StudentLessonDetailDialog is a direct Form and must opt in to shared input styling");
    AssertTrue(pollSource.Contains("DialogInputStyler"),
        "CreatePollDialog must opt in to shared input styling for dynamically added options");

    AssertFalse(simpleDialogSource.Contains("BorderStyle.FixedSingle"),
        "TeacherSimpleItemDialog must not use old fixed single input borders");
    AssertFalse(examDialogSource.Contains("BorderStyle.FixedSingle"),
        "TeacherExamDialog must not use old fixed single input borders");
}

static void RunCourseScopedDialogLayoutSourceTests()
{
    string root = RepoRoot();
    string stylerSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Theme", "DialogInputStyler.cs"));
    string courseDialogSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherCourseDialog.cs"));
    string simpleDialogSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherSimpleItemDialog.cs"));
    string assignmentDialogSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherAssignmentDialog.cs"));
    string lessonDialogSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherLessonDialog.cs"));
    string materialDialogSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherMaterialUploadDialog.cs"));
    string examDialogSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherExamDialog.cs"));
    string examQuestionsSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "Forms", "Teacher", "TeacherExamQuestionsDialog.cs"));
    string scheduleSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherSchedule.cs"));
    string assignmentsSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherAssignments.cs"));
    string lessonsSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherLessons.cs"));
    string materialsSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherMaterials.cs"));
    string examsSource = File.ReadAllText(Path.Combine(root, "CourseGuard", "CourseGuard", "Frontend", "UserControls", "Teacher", "UC_TeacherExams.cs"));

    AssertTrue(stylerSource.Contains("DialogNumericInputShell"),
        "dialog numeric inputs must use a custom admin-style spinner shell instead of the native NumericUpDown chrome");
    AssertTrue(stylerSource.Contains("control is NumericUpDown numeric")
        && stylerSource.Contains("new DialogNumericInputShell(numeric"),
        "DialogInputStyler must route NumericUpDown controls through the admin-style numeric shell");
    AssertTrue(courseDialogSource.Contains("DefaultInputRowHeight = 52"),
        "shared teacher dialog grids must reserve enough vertical space for styled admin inputs");

    AssertTrue(simpleDialogSource.Contains("bool lockCourseSelection = false")
        && simpleDialogSource.Contains("CreateCourseScopeDisplay")
        && simpleDialogSource.Contains("CourseCombo.Visible = !_lockCourseSelection"),
        "schedule/simple item dialogs must replace the course combo with read-only course context when scoped");
    AssertTrue(assignmentDialogSource.Contains("bool lockCourseSelection = false")
        && assignmentDialogSource.Contains("CreateCourseScopeDisplay")
        && assignmentDialogSource.Contains("_course.Visible = !_lockCourseSelection"),
        "assignment dialogs must not allow changing course when opened from a course workspace");
    AssertTrue(lessonDialogSource.Contains("bool lockCourseSelection = false")
        && lessonDialogSource.Contains("CreateCourseScopeDisplay")
        && lessonDialogSource.Contains("_course.Visible = !_lockCourseSelection"),
        "lesson dialogs must not allow changing course when opened from a course workspace");
    AssertTrue(materialDialogSource.Contains("bool lockCourseSelection = false")
        && materialDialogSource.Contains("CreateCourseScopeDisplay")
        && materialDialogSource.Contains("_course.Visible = !_lockCourseSelection"),
        "material upload dialogs must not allow changing course when opened from a course workspace");
    AssertTrue(examDialogSource.Contains("bool lockCourseSelection = false")
        && examDialogSource.Contains("CreateCourseScopeDisplay")
        && examDialogSource.Contains("_course.Visible = !_lockCourseSelection"),
        "exam dialogs must not allow changing course when opened from a course workspace");

    AssertTrue(scheduleSource.Contains("lockCourseSelection: _initialCourseId.HasValue"),
        "teacher schedule must lock the course field for course workspace dialogs");
    AssertTrue(assignmentsSource.Contains("lockCourseSelection: _initialCourseId.HasValue"),
        "teacher assignment page must lock the course field for course workspace dialogs");
    AssertTrue(lessonsSource.Contains("lockCourseSelection: _initialCourseId.HasValue"),
        "teacher lesson page must lock the course field for course workspace dialogs");
    AssertTrue(materialsSource.Contains("lockCourseSelection: _initialCourseId.HasValue"),
        "teacher material page must lock the course field for course workspace dialogs");
    AssertTrue(examsSource.Contains("lockCourseSelection: _initialCourseId.HasValue"),
        "teacher exam page must lock the course field for course workspace dialogs");

    AssertTrue(examQuestionsSource.Contains("WrapContents = true"),
        "exam question dialog action bars must wrap instead of clipping buttons");
    AssertTrue(examQuestionsSource.Contains("QuestionActionRowHeight = 96")
        && examQuestionsSource.Contains("EditorInputRowHeight = 52"),
        "exam question dialog must reserve enough height for wrapped buttons and styled inputs");
}

static void RunStudentScheduleCalendarLabelReuseTests()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

            var page = (UC_Schedule)RuntimeHelpers.GetUninitializedObject(typeof(UC_Schedule));
            MethodInfo createCell = typeof(UC_Schedule).GetMethod("CreateCalendarDayCell", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Cannot find CreateCalendarDayCell");
            MethodInfo configureCell = typeof(UC_Schedule).GetMethod("ConfigureCalendarDayCell", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Cannot find ConfigureCalendarDayCell");

            RoundedPanel cell = (RoundedPanel)(createCell.Invoke(null, Array.Empty<object>()) ?? throw new InvalidOperationException("CreateCalendarDayCell returned null"));
            DateTime today = new DateTime(2026, 6, 15);
            StudentScheduleItemModel session = new()
            {
                SessionId = 44,
                CourseId = 5,
                CourseName = "Lập trình mạng",
                Title = "TCP",
                StartTime = today.AddHours(9)
            };

            configureCell.Invoke(page, new object[] { cell, today, new List<StudentScheduleItemModel> { session }, today });
            Label assignedLabel = AssertControl<Label>(cell.Controls["lblSession0"]!, "calendar cell must expose lblSession0");
            AssertEqual($"{session.StartTime:HH:mm} - TCP", assignedLabel.Text);
            AssertEqual(Cursors.Hand, assignedLabel.Cursor);
            AssertEqual(session.SessionId, assignedLabel.Tag);

            configureCell.Invoke(page, new object[] { cell, today, new List<StudentScheduleItemModel>(), today });

            for (int i = 0; i < 3; i++)
            {
                Label sessionLabel = AssertControl<Label>(cell.Controls[$"lblSession{i}"]!, $"calendar cell must expose lblSession{i}");
                AssertFalse(sessionLabel.Visible, $"unused pooled label {i} should be hidden");
                AssertEqual(string.Empty, sessionLabel.Text);
                AssertTrue(sessionLabel.Tag == null, $"unused pooled label {i} must clear Tag");
                AssertEqual(Cursors.Default, sessionLabel.Cursor);
            }
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure != null)
        throw new InvalidOperationException(failure.Message, failure);
}

static void WaitForTaskWithMessagePump(Task task, int timeoutMilliseconds = 5000)
{
    DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
    while (!task.IsCompleted)
    {
        if (DateTime.UtcNow >= deadline)
            throw new TimeoutException("Timed out while waiting for WinForms task completion.");

        Application.DoEvents();
        Thread.Sleep(10);
    }

    task.GetAwaiter().GetResult();
}

static ChatMessageModel NewChatMessage(int id, int courseId, string content)
{
    return new ChatMessageModel
    {
        Id = id,
        CourseId = courseId,
        SenderId = 7,
        SenderName = "Test Sender",
        SenderRole = "Student",
        Content = content,
        MessageType = "TEXT",
        SentAt = new DateTime(2026, 7, 1, 8, 0, 0).AddMinutes(id)
    };
}

static CourseToolDefinition NewCourseTool(
    string id,
    string label,
    string description,
    Action? onPreview = null,
    Action? onOpen = null,
    string badgeText = "")
{
    return new CourseToolDefinition(
        id,
        label,
        id,
        description,
        _ =>
        {
            onPreview?.Invoke();
            return new Label { Text = description, AutoSize = true };
        },
        _ => onOpen?.Invoke(),
        badgeText);
}

static void RunMessageDialogButtonTests()
{
    using Form yesNoCancel = CreateThemedDialog(
        "Ban co chac muon dang xuat?",
        "Dang xuat",
        MessageBoxButtons.YesNoCancel,
        MessageBoxIcon.Question);
    AssertEqual(255, yesNoCancel.BackColor.A);
    AssertDialogResults(
        yesNoCancel,
        new[] { DialogResult.Yes, DialogResult.No, DialogResult.Cancel },
        "logout confirmation should expose yes/no/cancel actions");

    using Form okCancel = CreateThemedDialog(
        "Thao tac nay can xac nhan.",
        "Xac nhan",
        MessageBoxButtons.OKCancel,
        MessageBoxIcon.Warning);
    AssertDialogResults(
        okCancel,
        new[] { DialogResult.OK, DialogResult.Cancel },
        "ok/cancel dialog should expose both actions");
}

static string FormatVietnamTime(DateTime value)
{
    Type formatterType = typeof(AppColors).Assembly.GetType("CourseGuard.Frontend.Theme.SystemTimeFormatter")
        ?? throw new InvalidOperationException("Cannot find SystemTimeFormatter");
    MethodInfo method = formatterType.GetMethod("FormatVietnamTime", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Cannot find FormatVietnamTime");
    return (string)(method.Invoke(null, new object[] { value })
        ?? throw new InvalidOperationException("FormatVietnamTime returned null"));
}

static Form CreateThemedDialog(string content, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
{
    Type dialogType = typeof(StudentExamScoringService).Assembly.GetType("CourseGuard.Frontend.Theme.MetaTheme+ThemedMessageDialog")
        ?? throw new InvalidOperationException("Cannot find MetaTheme.ThemedMessageDialog");
    object? dialog = TryCreateDialog(dialogType, new object?[] { content, title, buttons, icon, null, null, null })
        ?? TryCreateDialog(dialogType, new object?[] { content, title, buttons, icon });
    return (Form)(dialog
        ?? throw new InvalidOperationException("Cannot construct themed dialog"));
}

static object? TryCreateDialog(Type dialogType, object?[] args)
{
    try
    {
        return Activator.CreateInstance(dialogType, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, args, null);
    }
    catch (MissingMethodException)
    {
        return null;
    }
}

static void RunWithAutoClosingThemedDialog(Func<Task> action)
{
    using var timer = new System.Windows.Forms.Timer { Interval = 20 };
    timer.Tick += (_, _) =>
    {
        Form? dialog = Application.OpenForms
            .Cast<Form>()
            .FirstOrDefault(form => form.GetType().Name.Contains("ThemedMessageDialog", StringComparison.Ordinal));
        if (dialog == null)
            return;

        dialog.DialogResult = DialogResult.OK;
        dialog.Close();
    };

    timer.Start();
    try
    {
        action().GetAwaiter().GetResult();
    }
    finally
    {
        timer.Stop();
    }
}

static void AssertDialogResults(Form dialog, DialogResult[] expected, string scenario)
{
    DialogResult[] actual = GetAllControls(dialog)
        .OfType<Button>()
        .Where(button => button.DialogResult != DialogResult.None)
        .Select(button => button.DialogResult)
        .OrderBy(result => result.ToString())
        .ToArray();
    DialogResult[] sortedExpected = expected.OrderBy(result => result.ToString()).ToArray();

    if (!actual.SequenceEqual(sortedExpected))
    {
        string actualText = string.Join(", ", actual);
        string expectedText = string.Join(", ", sortedExpected);
        throw new InvalidOperationException($"{scenario}: expected [{expectedText}], got [{actualText}]");
    }
}

static IEnumerable<Control> GetAllControls(Control root)
{
    foreach (Control child in root.Controls)
    {
        yield return child;
        foreach (Control grandChild in GetAllControls(child))
            yield return grandChild;
    }
}

static bool IsSidebarNavigationButton(Button button)
{
    if (button.Tag is CourseSelectorItem)
        return false;

    if (button.Tag is string tag && string.Equals(tag, "course-selector-button", StringComparison.Ordinal))
        return false;

    return true;
}

static Control InvokeChromeDataCard(string typeName, string title, Control content)
{
    Type chromeType = typeof(StudentExamScoringService).Assembly.GetType(typeName)
        ?? throw new InvalidOperationException($"Cannot find {typeName}");
    MethodInfo method = chromeType.GetMethod("CreateDataCard", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Cannot find CreateDataCard on {typeName}");
    return (Control)(method.Invoke(null, new object[] { title, content })
        ?? throw new InvalidOperationException("CreateDataCard returned null"));
}

static Control InvokeChromeHeader(string typeName, string title, string subtitle)
{
    Type chromeType = typeof(StudentExamScoringService).Assembly.GetType(typeName)
        ?? throw new InvalidOperationException($"Cannot find {typeName}");
    MethodInfo method = chromeType.GetMethod(
            "CreateHeader",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(string), typeof(string), typeof(Control[]) },
            null)
        ?? throw new InvalidOperationException($"Cannot find CreateHeader(string, string, Control[]) on {typeName}");
    return (Control)(method.Invoke(null, new object[] { title, subtitle, Array.Empty<Control>() })
        ?? throw new InvalidOperationException("CreateHeader returned null"));
}

static void AssertGridIsWrappedInRoundedBody(Control card, DataGridView grid, string scenario)
{
    TableLayoutPanel layout = card.Controls.OfType<TableLayoutPanel>().Single();
    Control body = layout.GetControlFromPosition(0, 1)
        ?? throw new InvalidOperationException($"{scenario}: missing content body");
    if (body is not RoundedPanel)
        throw new InvalidOperationException($"{scenario}: expected DataGridView content to be hosted inside RoundedPanel");
    if (!body.Controls.Contains(grid))
        throw new InvalidOperationException($"{scenario}: RoundedPanel does not contain original grid");
}

static void AssertHeaderSubtitleHasDescenderRoom(Control header, string scenario)
{
    TableLayoutPanel layout = header.Controls.OfType<TableLayoutPanel>().Single();
    FlowLayoutPanel titleStack = layout.GetControlFromPosition(0, 0) as FlowLayoutPanel
        ?? throw new InvalidOperationException($"{scenario}: missing title stack");
    Label[] labels = titleStack.Controls.OfType<Label>().ToArray();
    if (labels.Length < 2)
        throw new InvalidOperationException($"{scenario}: missing subtitle label");

    Label subtitle = labels[1];
    AssertFalse(subtitle.AutoSize, $"{scenario}: subtitle should use fixed height instead of clipping-prone AutoSize");
    AssertFalse(subtitle.UseCompatibleTextRendering, $"{scenario}: subtitle should use native TextRenderer to avoid clipping descenders");
    AssertTrue(subtitle.Height >= 28, $"{scenario}: subtitle should reserve enough height for Vietnamese descenders");
    AssertTrue(subtitle.Padding.Bottom >= 4, $"{scenario}: subtitle should keep bottom padding for glyph descenders");

    int requiredHeight = header.Padding.Top + header.Padding.Bottom + labels.Sum(label => label.Height + label.Margin.Vertical);
    AssertTrue(header.MinimumSize.Height >= requiredHeight, $"{scenario}: header card should be tall enough for title and subtitle without clipping");
    AssertTrue(header.Height >= header.MinimumSize.Height, $"{scenario}: header card should use its minimum height instead of the default panel height");
    AssertTrue(header.Height <= 104, $"{scenario}: header card should stay compact and not push dashboard content down");
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected {expected}, got {actual}");
}

static T GetPropertyValue<T>(object instance, string propertyName)
{
    PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Cannot find property {propertyName}");
    object? value = property.GetValue(instance);
    return value is T typed
        ? typed
        : throw new InvalidOperationException($"Property {propertyName} was not a {typeof(T).Name}");
}

static T GetFieldValue<T>(object instance, string fieldName)
{
    FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Cannot find field {fieldName}");
    object? value = field.GetValue(instance);
    return value is T typed
        ? typed
        : throw new InvalidOperationException($"Field {fieldName} was not a {typeof(T).Name}");
}

static void SetFieldValue(object instance, string fieldName, object? value)
{
    FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Cannot find field {fieldName}");
    field.SetValue(instance, value);
}

static void ResetAppEnvironmentDotEnvLoadedForTests()
{
    FieldInfo field = typeof(AppEnvironment).GetField("_dotEnvLoaded", BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Cannot find AppEnvironment dot env loaded flag");
    field.SetValue(null, false);
}

static void SetStudentProfileTestFields(
    UC_Profile page,
    TextBox fullName,
    TextBox email,
    TextBox phone,
    TextBox birthDate,
    ErrorProvider errors)
{
    SetFieldValue(page, "txtFullName", fullName);
    SetFieldValue(page, "txtEmail", email);
    SetFieldValue(page, "txtPhone", phone);
    SetFieldValue(page, "txtBirthDate", birthDate);
    SetFieldValue(page, "txtAddress", new TextBox());
    SetFieldValue(page, "txtMajor", new TextBox());
    SetFieldValue(page, "txtBio", new TextBox());
    ComboBox gender = new();
    gender.Items.Add("Nam");
    gender.SelectedIndex = 0;
    SetFieldValue(page, "cboGender", gender);

    SetFieldValue(page, "lblValName", new Label());
    SetFieldValue(page, "lblValStudentCode", new Label());
    SetFieldValue(page, "lblEditStudentCode", new Label());
    SetFieldValue(page, "lblValEmail", new Label());
    SetFieldValue(page, "lblValPhone", new Label());
    SetFieldValue(page, "lblValAddress", new Label());
    SetFieldValue(page, "lblValMajor", new Label());
    SetFieldValue(page, "lblValGender", new Label());
    SetFieldValue(page, "lblValBirthDate", new Label());
    SetFieldValue(page, "lblValBio", new Label());
    SetFieldValue(page, "_lblHeaderName", new Label());
    SetFieldValue(page, "_studentCode", "HS00000");
    SetFieldValue(page, "_validationErrors", errors);
}

static bool InvokeProfileSaveWithDialogClose(Control owner, MethodInfo saveMethod)
{
    bool? result = null;
    RunWithAutoClosingThemedDialog(() =>
    {
        result = (bool)saveMethod.Invoke(owner, Array.Empty<object>())!;
        return Task.CompletedTask;
    });

    return result ?? throw new InvalidOperationException("Profile save did not return a result");
}

static void AssertTrue(bool value, string message)
{
    if (!value)
        throw new InvalidOperationException(message);
}

static void AssertFalse(bool value, string message)
{
    if (value)
        throw new InvalidOperationException(message);
}

static void AssertProperty(Type type, string name)
{
    AssertTrue(type.GetProperty(name) != null, $"{type.Name} must expose {name}");
}

static void AssertProfileUsesValidatedSave(Type pageType, string pageName)
{
    const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    MethodInfo validatedSave = pageType.GetMethod("SaveProfileIfValid", privateInstance)
        ?? throw new InvalidOperationException($"{pageName} profile page must expose SaveProfileIfValid");
    MethodInfo validateInputs = pageType.GetMethod("ValidateProfileInputs", privateInstance)
        ?? throw new InvalidOperationException($"{pageName} profile page must expose ValidateProfileInputs");
    MethodInfo saveProfile = pageType.GetMethod("SaveProfile", privateInstance)
        ?? throw new InvalidOperationException($"{pageName} profile page must expose SaveProfile");
    MethodInfo changeAvatar = pageType.GetMethod("ChangeAvatar", privateInstance)
        ?? throw new InvalidOperationException($"{pageName} profile page must expose ChangeAvatar");

    AssertTrue(
        MethodCalls(validatedSave, validateInputs),
        $"{pageName} validated save path must run profile validation first");
    AssertTrue(
        MethodCalls(validatedSave, saveProfile),
        $"{pageName} validated save path must save only after validation");
    AssertTrue(
        MethodCalls(changeAvatar, validatedSave),
        $"{pageName} avatar changes must use the validated save path");
}

static void AssertScopedTeacherContentPageLocksCourse(string source, string pageName)
{
    string selectedCourseId = ExtractPropertySource(source, "private int? SelectedCourseId");
    string loadCourseFilter = ExtractMethodSource(source, "private void LoadCourseFilter");
    string selectCourseFilter = ExtractMethodSource(source, "private void SelectCourseFilter");

    AssertTrue(selectedCourseId.Contains("_initialCourseId ??"),
        $"{pageName} scoped mode must make _initialCourseId the authoritative selected course id");
    AssertTrue(loadCourseFilter.Contains("_courseFilter.Enabled = !_initialCourseId.HasValue"),
        $"{pageName} scoped mode must disable the mutable course combo");
    AssertTrue(loadCourseFilter.Contains("GetCoursesForCurrentScope"),
        $"{pageName} scoped mode must populate the combo from courses filtered to the initial course");
    AssertTrue(selectCourseFilter.Contains("_initialCourseId.Value"),
        $"{pageName} SelectCourseFilter must ignore attempts to switch away from the initial course");
    AssertTrue(source.Contains("GetCoursesForCurrentScope"),
        $"{pageName} dialogs must receive course lists filtered to the initial course in scoped mode");
}

static bool MethodCalls(MethodInfo caller, MethodInfo callee)
{
    byte[]? il = caller.GetMethodBody()?.GetILAsByteArray();
    if (il == null)
        return false;

    Dictionary<ushort, EmitOpCode> opCodes = GetOpCodeMap();
    int offset = 0;
    while (offset < il.Length)
    {
        ushort opCodeValue = il[offset++];
        if (opCodeValue == 0xFE)
            opCodeValue = (ushort)(0xFE00 | il[offset++]);

        if (!opCodes.TryGetValue(opCodeValue, out EmitOpCode opCode))
            return false;

        int operandStart = offset;
        if (opCode.OperandType == EmitOperandType.InlineMethod)
        {
            int token = BitConverter.ToInt32(il, operandStart);
            try
            {
                MethodBase? resolved = caller.Module.ResolveMethod(
                    token,
                    caller.DeclaringType?.GetGenericArguments(),
                    caller.GetGenericArguments());
                if (resolved != null && resolved.Module == callee.Module && resolved.MetadataToken == callee.MetadataToken)
                    return true;
            }
            catch (ArgumentException)
            {
                // Keep scanning if this runtime cannot resolve a metadata token.
            }
        }

        offset += GetOperandSize(opCode.OperandType, il, operandStart);
    }

    return false;
}

static Dictionary<ushort, EmitOpCode> GetOpCodeMap()
{
    Dictionary<ushort, EmitOpCode> opCodes = new();
    foreach (FieldInfo field in typeof(EmitOpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
    {
        if (field.GetValue(null) is EmitOpCode opCode)
            opCodes[(ushort)opCode.Value] = opCode;
    }

    return opCodes;
}

static int GetOperandSize(EmitOperandType operandType, byte[] il, int operandStart)
{
    return operandType switch
    {
        EmitOperandType.InlineNone => 0,
        EmitOperandType.ShortInlineBrTarget or EmitOperandType.ShortInlineI or EmitOperandType.ShortInlineVar => 1,
        EmitOperandType.InlineVar => 2,
        EmitOperandType.InlineI or EmitOperandType.InlineBrTarget or EmitOperandType.InlineField or EmitOperandType.InlineMethod
            or EmitOperandType.InlineSig or EmitOperandType.InlineString or EmitOperandType.InlineTok or EmitOperandType.InlineType
            or EmitOperandType.ShortInlineR => 4,
        EmitOperandType.InlineI8 or EmitOperandType.InlineR => 8,
        EmitOperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, operandStart)),
        _ => 0
    };
}

static TControl AssertControl<TControl>(Control control, string message)
    where TControl : Control
{
    AssertTrue(control is TControl, message);
    return (TControl)control;
}

static TControl SingleChild<TControl>(Control parent)
    where TControl : Control
{
    TControl[] matches = parent.Controls.OfType<TControl>().ToArray();
    AssertEqual(1, matches.Length);
    return matches[0];
}

static void RaiseFocusChanged(Control control, bool focused)
{
    string methodName = focused ? "OnGotFocus" : "OnLostFocus";
    MethodInfo method = typeof(Control).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(nameof(Control), methodName);
    method.Invoke(control, new object[] { EventArgs.Empty });
}

static void AssertImageDisposed(Image image, string message)
{
    try
    {
        _ = image.Width;
    }
    catch (ObjectDisposedException)
    {
        return;
    }
    catch (ArgumentException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static class TestRunner
{
    public static bool Offline { get; set; }
    private static int _passed;
    private static int _failed;
    private static int _skipped;

    private static bool ShouldSkip(string name)
    {
        if (!Offline || name is not (
            "app environment required values load .env on demand" or
            "ai services expose essay grading virtual tutor and flashcard generation" or
            "gamification and schedule conflict service calculates badges and overlaps"))
            return false;

        _skipped++;
        Console.WriteLine($"SKIP {name}: requires external configuration or services (--offline)");
        return true;
    }

    public static void PrintSummary() =>
        Console.WriteLine($"RESULT: {_passed} passed, {_failed} failed, {_skipped} skipped.");

    public static void Run(string name, Action test)
    {
        if (ShouldSkip(name)) return;
        try
        {
            test();
            _passed++;
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception ex)
        {
            _failed++;
            Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    public static void Run(string name, Func<Task> test)
    {
        if (ShouldSkip(name)) return;
        try
        {
            test().GetAwaiter().GetResult();
            _passed++;
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception ex)
        {
            _failed++;
            Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }
}

sealed class RecordingSynchronizationContext : SynchronizationContext
{
    private readonly Queue<(SendOrPostCallback Callback, object? State)> _callbacks = new();
    private readonly ManualResetEventSlim _posted = new();

    public override void Post(SendOrPostCallback d, object? state)
    {
        lock (_callbacks)
        {
            _callbacks.Enqueue((d, state));
        }

        _posted.Set();
    }

    public bool WaitForPost(TimeSpan timeout)
    {
        return _posted.Wait(timeout);
    }

    public void ExecuteAll()
    {
        while (true)
        {
            SendOrPostCallback callback;
            object? state;
            lock (_callbacks)
            {
                if (_callbacks.Count == 0)
                    return;

                (callback, state) = _callbacks.Dequeue();
            }

            callback(state);
        }
    }
}

sealed class FakeDeadlineReminderStore : IDeadlineReminderStore
{
    private readonly object _claimLock = new();
    public List<DeadlineReminderItem> Deadlines { get; } = new();
    public HashSet<string> AlreadySent { get; } = new();
    public List<string> Sent { get; } = new();
    public List<CreatedNotification> Created { get; } = new();
    public bool FailNextCreate { get; set; }
    public bool CoordinateConcurrentClaims { get; set; }
    private readonly CountdownEvent _concurrentClaims = new(2);
    private readonly ManualResetEventSlim _releaseConcurrentClaims = new();

    public void EnsureDeadlineReminderSchema()
    {
    }

    public List<DeadlineReminderItem> GetUpcomingDeadlines(int userId, DateTime from, DateTime to)
    {
        return Deadlines
            .Where(item => item.DueAt >= from && item.DueAt <= to)
            .ToList();
    }

    public int CreateDeadlineReminderNotification(
        int userId,
        DeadlineReminderItem item,
        string remindType,
        string title,
        string content)
    {
        if (CoordinateConcurrentClaims)
        {
            _concurrentClaims.Signal();
            if (!_concurrentClaims.Wait(TimeSpan.FromSeconds(3)))
                throw new InvalidOperationException("concurrent reminder claims did not align");
            _releaseConcurrentClaims.Set();
            if (!_releaseConcurrentClaims.Wait(TimeSpan.FromSeconds(3)))
                throw new InvalidOperationException("concurrent reminder claims were not released");
        }

        string key = Key(userId, item.SourceType, item.SourceId, remindType);
        lock (_claimLock)
        {
            if (AlreadySent.Contains(key) || Sent.Contains(key))
                return 0;

            if (FailNextCreate)
            {
                FailNextCreate = false;
                throw new InvalidOperationException("notification create failed");
            }

            Sent.Add(key);
            Created.Add(new CreatedNotification(
                userId,
                title,
                content,
                item.NotificationCategory,
                WorkflowConstants.NotificationType.ActionRequired,
                item.SourceType,
                item.SourceId));
            return Created.Count;
        }
    }

    public static string Key(int userId, string sourceType, int sourceId, string remindType)
    {
        return $"{userId}:{sourceType}:{sourceId}:{remindType}";
    }
}

sealed record CreatedNotification(
    int UserId,
    string Title,
    string Content,
    string Category,
    string NotificationType,
    string? SourceType,
    int? SourceId);

sealed class FakeClassroomSignalService : IClassroomSignalService
{
    private readonly object _broadcastLock = new();
    private int _startCount;
    private readonly ManualResetEventSlim _broadcastCountChanged = new();

    public int StartCount => Volatile.Read(ref _startCount);
    public List<int> BroadcastSessionIds { get; } = new();
    public List<int> ClosedBroadcastSessionIds { get; } = new();
    public bool ThrowOnStartListening { get; set; }
    public Action? OnStartListening { get; set; }

    public void StartListening()
    {
        Interlocked.Increment(ref _startCount);
        OnStartListening?.Invoke();
        if (ThrowOnStartListening)
            throw new InvalidOperationException("Start failed");
    }

    public Task BroadcastClassOpened(int sessionId)
    {
        lock (_broadcastLock)
        {
            BroadcastSessionIds.Add(sessionId);
            _broadcastCountChanged.Set();
        }

        return Task.CompletedTask;
    }

    public Task BroadcastClassClosed(int sessionId)
    {
        lock (_broadcastLock)
        {
            ClosedBroadcastSessionIds.Add(sessionId);
            _broadcastCountChanged.Set();
        }

        return Task.CompletedTask;
    }

    public bool WaitForBroadcastCount(int expectedCount, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            lock (_broadcastLock)
            {
                if (BroadcastSessionIds.Count >= expectedCount)
                    return true;
            }

            TimeSpan remaining = deadline - DateTime.UtcNow;
            _broadcastCountChanged.Wait(remaining > TimeSpan.FromMilliseconds(50) ? TimeSpan.FromMilliseconds(50) : remaining);
            _broadcastCountChanged.Reset();
        }

        lock (_broadcastLock)
            return BroadcastSessionIds.Count >= expectedCount;
    }
}

sealed class TestProfilePage : ProfilePageBase
{
    public Control BuildInput(
        string labelText,
        TextBox textBox,
        bool password = false,
        bool blendWithCard = true,
        int inputWidth = 280,
        bool clearInputTag = true)
    {
        return CreateInputGroup(labelText, textBox, password, blendWithCard, inputWidth, clearInputTag);
    }

    public Control BuildMultiline(
        string labelText,
        TextBox textBox,
        bool blendWithCard = true,
        bool clearTextBoxMargin = false,
        bool clearInputTag = true)
    {
        return CreateMultilineInputGroup(labelText, textBox, blendWithCard, clearTextBoxMargin, clearInputTag);
    }

    public Control BuildCombo(string labelText, ComboBox comboBox, bool blendWithCard = true)
    {
        return CreateComboGroup(labelText, comboBox, blendWithCard);
    }
}

sealed class TestStudentGridPage : StudentGridPageBase
{
    private readonly List<string> _cachedRows = new();
    private readonly List<string> _nextRows = new();
    private bool _failNextLoad;

    public TestStudentGridPage()
        : base(
            "Test page",
            "Builds common grid chrome.",
            "Rows",
            "No rows for this test.",
            hintText: "Only test rows are shown.",
            showSearch: true,
            searchPlaceholder: "Search rows",
            showCourseFilter: true,
            showSearchButton: true)
    {
        AddHeaderAction(ExposedExtraAction);
    }

    public int CreateTableCallCount { get; private set; }
    public Button ExposedExtraAction { get; } = new() { Text = "Extra action" };
    public DataGridView ExposedGrid => Grid;
    public BindingSource ExposedBindingSource => BindingSource;
    public Button ExposedRefreshButton => RefreshButton;
    public TextBox? ExposedSearchBox => SearchBox;
    public ComboBox? ExposedCourseFilter => CourseFilter;
    public Button? ExposedSearchButton => SearchButton;
    public RoundedPanel ExposedGridBody => GridBody;
    public Label ExposedEmptyStateLabel => EmptyStateLabel;

    public Task LoadForTestAsync() => LoadDataAsync();

    public void SetNextRows(params string[] rows)
    {
        _nextRows.Clear();
        _nextRows.AddRange(rows);
    }

    public void FailNextLoad()
    {
        _failNextLoad = true;
    }

    public void RebindCachedRows()
    {
        SetGridTable(CreateTable(_cachedRows));
    }

    protected override Task<DataTable> CreateTableAsync()
    {
        CreateTableCallCount++;
        if (_failNextLoad)
        {
            _failNextLoad = false;
            throw new InvalidOperationException("Simulated refresh failure");
        }

        _cachedRows.Clear();
        _cachedRows.AddRange(_nextRows);
        return Task.FromResult(CreateTable(_cachedRows));
    }

    private static DataTable CreateTable(IEnumerable<string> rows)
    {
        DataTable table = new();
        table.Columns.Add("ID", typeof(int));
        table.Columns.Add("Name", typeof(string));
        int id = 1;
        foreach (string row in rows)
            table.Rows.Add(id++, row);
        return table;
    }
}

sealed class TestTeacherGridPage : TeacherGridPageBase
{
    private readonly object _pendingLoadLock = new();
    private readonly Queue<TaskCompletionSource<DataTable>> _pendingLoads = new();
    private bool _useControlledLoads;

    public TestTeacherGridPage()
        : base(
            1,
            (TeacherController)RuntimeHelpers.GetUninitializedObject(typeof(TeacherController)),
            "Teacher test page",
            "Builds common teacher grid chrome.",
            "Rows")
    {
    }

    public DataGridView ExposedGrid => Grid;
    public BindingSource ExposedBindingSource => BindingSource;

    public Task LoadForTestAsync() => LoadDataAsync();

    public void BeginControlledLoads()
    {
        _useControlledLoads = true;
    }

    public TaskCompletionSource<DataTable> EnqueuePendingLoad()
    {
        TaskCompletionSource<DataTable> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_pendingLoadLock)
            _pendingLoads.Enqueue(pending);
        return pending;
    }

    protected override Task<DataTable> CreateTableAsync()
    {
        if (_useControlledLoads)
        {
            lock (_pendingLoadLock)
            {
                if (_pendingLoads.Count > 0)
                    return _pendingLoads.Dequeue().Task;
            }
        }

        return Task.FromResult(CreateTable());
    }

    public static DataTable CreateTable(params string[] rows)
    {
        DataTable table = new();
        table.Columns.Add("ID", typeof(int));
        table.Columns.Add("Name", typeof(string));
        int id = 1;
        foreach (string row in rows)
            table.Rows.Add(id++, row);
        return table;
    }
}
