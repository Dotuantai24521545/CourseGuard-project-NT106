# Offline exam regression tests

Run on Windows with .NET SDK 10:

```powershell
dotnet run --project tests/CourseGuard.ExamTests/CourseGuard.ExamTests.csproj
```

This executable links the production scoring and answer-cache source files. It needs no database, `.env`, SMTP, or external packages. Any failure returns a nonzero exit code. Temporary cache data lives under the test output directory and is removed after execution; real student caches are untouched.

Coverage:

- Blank and null answers cannot earn points, even with a blank answer key.
- Case and whitespace normalization, wrong answers, missing answers, and unknown question IDs.
- Student and teacher scores agree at decimal rounding boundaries. Scores are rounded to two decimal places, away from zero, after summing decimal points.
- Concurrent saves preserve all questions and updating a question does not duplicate it.
- A Windows file lock that prevents replacement leaves the previous snapshot readable and removes temporary files.
- Clearing an attempt does not affect another attempt.

Cache writes now flush a temporary file before replacing the destination. The existing encryption format is preserved so existing cache files remain readable. Synchronization covers threads in one application process; it does not coordinate multiple running application instances. Existing best-effort error handling remains unchanged. These tests do not replace database integration or WinForms UI tests.
