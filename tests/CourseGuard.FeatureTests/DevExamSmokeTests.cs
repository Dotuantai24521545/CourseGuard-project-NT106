using CourseGuard.Backend.Controllers;
using CourseGuard.Backend.Data;
using Npgsql;

internal static class DevExamSmokeTests
{
    public static async Task RunAsync(string projectRef)
    {
        var db = new CourseGuardDbContext("");
        using var connection = db.CreateConnection();
        var config = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        if (string.IsNullOrWhiteSpace(projectRef) || config.Username != "postgres." + projectRef)
            throw new InvalidOperationException("Explicit matching dev project reference required.");
        await connection.OpenAsync();
        // Validate seeded credentials through the synchronous authentication path; no email is sent.
        var auth = new AuthController(db);
        var student = auth.Login("student", "admin123") ?? throw new InvalidOperationException("Student credentials failed.");
        _ = auth.Login("admin", "admin123") ?? throw new InvalidOperationException("Admin credentials failed.");
        Console.WriteLine("PASS seeded admin/student credential validation (no MFA email).");
        int courseId = 0;
        try
        {
            using var course = new NpgsqlCommand("INSERT INTO courses(name,teacher_id,status) VALUES(@name,(SELECT id FROM users WHERE username='thnghi'),'ACTIVE') RETURNING id", connection);
            course.Parameters.AddWithValue("name", "__smoke_" + Guid.NewGuid().ToString("N"));
            courseId = Convert.ToInt32(await course.ExecuteScalarAsync());
            using var enrollment = new NpgsqlCommand("INSERT INTO enrollments(course_id,student_id,status) VALUES(@course,@student,'ACTIVE')", connection);
            enrollment.Parameters.AddWithValue("course", courseId);
            enrollment.Parameters.AddWithValue("student", student.Id);
            await enrollment.ExecuteNonQueryAsync();
            using var exam = new NpgsqlCommand("INSERT INTO exams(course_id,title,duration_minutes,max_attempts,status) VALUES(@course,'Temporary smoke exam',30,1,'ACTIVE') RETURNING id", connection);
            exam.Parameters.AddWithValue("course", courseId);
            int examId = Convert.ToInt32(await exam.ExecuteScalarAsync());
            using var question = new NpgsqlCommand("INSERT INTO exam_questions(exam_id,question_text,option_a,option_b,option_c,option_d,correct_option,points,display_order) VALUES(@exam,'1 + 1?','2','3','4','5','A',10,1) RETURNING id", connection);
            question.Parameters.AddWithValue("exam", examId);
            int questionId = Convert.ToInt32(await question.ExecuteScalarAsync());
            var session = db.StartOrResumeStudentExam(student.Id, examId) ?? throw new InvalidOperationException("Exam did not start.");
            if (session.Questions.Count != 1) throw new InvalidOperationException("Question did not load.");
            if (!db.SaveStudentExamAnswer(student.Id, session.AttemptId, questionId, "A"))
                throw new InvalidOperationException("Answer did not save.");
            var resumed = db.StartOrResumeStudentExam(student.Id, examId);
            if (resumed?.AttemptId != session.AttemptId || resumed.Questions[0].SelectedOption != "A")
                throw new InvalidOperationException("Resume lost saved answer.");
            var result = db.SubmitStudentExamAttempt(student.Id, session.AttemptId);
            if (!result.Success || result.Score != 10m) throw new InvalidOperationException("Submission score incorrect.");
            using var verify = new NpgsqlCommand("SELECT score FROM exam_attempts WHERE id=@id AND status='SUBMITTED' AND submit_time IS NOT NULL", connection);
            verify.Parameters.AddWithValue("id", session.AttemptId);
            if (Convert.ToDecimal(await verify.ExecuteScalarAsync()) != 10m) throw new InvalidOperationException("Persisted score incorrect.");
            if (db.SaveStudentExamAnswer(student.Id, session.AttemptId, questionId, "B"))
                throw new InvalidOperationException("Submitted answer could still change.");
            if (db.StartOrResumeStudentExam(student.Id, examId) != null)
                throw new InvalidOperationException("Attempt limit not enforced.");
            Console.WriteLine("PASS start, save, resume, submit, persisted score, submitted-answer protection and attempt limit.");
        }
        finally
        {
            if (courseId > 0)
            {
                using var cleanup = new NpgsqlCommand("DELETE FROM courses WHERE id=@course AND name LIKE '__smoke_%'", connection);
                cleanup.Parameters.AddWithValue("course", courseId);
                await cleanup.ExecuteNonQueryAsync();
                Console.WriteLine("Removed temporary smoke course and dependent exam data.");
            }
        }
    }
}
