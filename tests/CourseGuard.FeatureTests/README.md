# Feature tests

Run on Windows with .NET SDK 10, from the repository root:

```powershell
dotnet run --project tests/CourseGuard.FeatureTests/CourseGuard.FeatureTests.csproj -- --offline
dotnet run --project tests/CourseGuard.ExamTests/CourseGuard.ExamTests.csproj
```

The offline feature suite runs logic, source-contract, and WinForms control tests. It prevents `.env` loading and supplies a dummy database connection. Three existing integration tests are explicitly skipped: local `.env` configuration, live AI calls, and the database-backed schedule conflict check. The summary reports passed, failed, and skipped counts; failures return a nonzero exit code.

Offline checks do not validate a real database, email delivery, AI availability, or a full end-to-end user session. Running without `--offline` enables the existing external-service tests; use dedicated test configuration for that mode.

The separate exam suite tests scoring and isolated answer-cache persistence without external services.

## Live integration smoke tests

Use `--integration --database-only` to check the database without sending AI requests.

Copy `CourseGuard/CourseGuard/.env.example` to `.env` in the same folder and configure your database and OpenRouter credentials, then run:

```powershell
dotnet run --project tests/CourseGuard.FeatureTests/CourseGuard.FeatureTests.csproj -- --integration
```

This runs seven checks: database configuration, read-only connectivity/schema, schedule/leaderboard query execution, and four actual AI requests with synthetic lesson content. Database tests do not create accounts, seed data, or alter rows. Missing credentials, provider errors and malformed AI replies fail the run instead of returning demo results. API credentials and provider output are not printed.

These are service smoke tests, not an end-to-end login/exam/submission test. The schedule query uses nonexistent test IDs to detect SQL errors without accessing personal data; it does not prove overlap detection against populated fixtures. AI tests can consume provider quota, and model availability can vary. `OPENROUTER_MODEL` overrides the default free router. No `--offline` option is applied in integration mode.

## Initialize a new development database

After configuring `.env`, run once against an empty Supabase development project:

```powershell
dotnet run --project tests/CourseGuard.FeatureTests/CourseGuard.FeatureTests.csproj -- --initialize-empty-database --project-ref=YOUR_PROJECT_REF
```

This requires the matching session-pooler username, refuses a nonempty public schema, omits the destructive reset preamble, applies the base schema and all ordered migrations in one transaction, and enables RLS on the created tables. It creates the repository demo accounts/courses. It is not an updater for existing databases.

To exercise actual answer persistence against that development database:

```powershell
dotnet run --project tests/CourseGuard.FeatureTests/CourseGuard.FeatureTests.csproj -- --dev-exam-smoke --project-ref=YOUR_PROJECT_REF
```

This validates seeded admin/student passwords through the synchronous authentication method, creates a uniquely named temporary course/exam, checks start/save/resume/submit and persisted score, verifies submission protection and attempt limits, then removes the temporary course and its dependent rows. Normal application schema initialization and password-hash upgrades may persist. It does not test WinForms interactions or the asynchronous login/MFA email flow. Demo accounts use `.local` email addresses; real email delivery requires separate SMTP and recipient configuration.
