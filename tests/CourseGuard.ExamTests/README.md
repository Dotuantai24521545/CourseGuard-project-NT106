# Offline exam regression tests

Run on Windows with .NET SDK 10:

```powershell
dotnet run --project tests/CourseGuard.ExamTests/CourseGuard.ExamTests.csproj
```

This executable links the production scoring and answer-cache source files and verifies key source contracts in the exam and monitoring UI. It needs no database, `.env`, SMTP, or external packages. Any failure returns a nonzero exit code. Temporary cache data lives under the test output directory and is removed after execution; real student caches are untouched.

## Coverage (19/19 tests)

1. Blank and null answers cannot earn points, even with a blank answer key.
2. Case and whitespace normalization, wrong answers, missing answers, and unknown question IDs.
3. Student and teacher scores agree at decimal rounding boundaries (rounded to 2 decimal places, away from zero, after summing decimal points).
4. Concurrent saves preserve all questions and updating a question does not duplicate it.
5. A Windows file lock that prevents replacement leaves the previous snapshot readable and removes temporary files.
6. Transient exclusive file locks (e.g. antivirus/indexer) retry cleanly without losing or dropping cached answers.
7. Clearing an attempt does not affect another attempt.
8. Changing or re-selecting an answer while offline sync (`SyncPendingAnswersAsync`) is in flight preserves the newer cached selection via atomic per-answer `RemoveSyncedAnswer` checks (`QuestionId`, `SelectedOption`, `Version`, monotonic `AnsweredAt`).
9. Resuming an exam session restores the latest offline cached answers into session questions (`ApplyCachedAnswersToSession`) without regressing newer online-saved selections.
10. `DoExamForm` routes internal dialogs through `ShowExamDialog` / `ShouldIgnoreDeactivateViolation` (preventing false `SCREEN_SWITCH` violations), flushes offline cache on submit (`TryFlushOfflineAnswersForSubmit`), and cleans up hooks/timers/streams in `FormClosing` and `Dispose`.
11. `MonitorAllForm`, `MonitorPopupForm`, and `AiQuestionGeneratorDialog` guard bitmap disposal, WebSocket relay monitoring (`RELAY_WS_URL` + authenticated `ServerApiClient` compatibility), DB violation fallback when image upload fails, and edited/draft AI question imports (`_grid.EndEdit()`).
12. Stale cached answer is not replayed after a newer online save (`ExamAnswerWriter`).
13. Stale cache from a previous session is dropped when a newer answer is saved online.
14. Changing to option B while sync of cached A is in flight leaves B on the server.
15. Rapid selections superseded while waiting on the write gate are skipped so the latest selection wins.
16. Offline save of an older version never overwrites a newer cached version (`RemoveAnswersUpToVersion`).
17. `FlushAsync` waits for in-flight saves before reporting pending state.
18. `LocalAnswerCache.SaveOnlineAnswer` + `SyncPendingAnswersAsync` coordination prevents cached A from overwriting online B both when B is saved before sync and when B is saved while sync of A is in flight.
19. `ScreenStreamPacketAssembler` / `ScreenStreamProtocol.ReceiveBinaryMessageAsync` reassembles split 30-byte headers, split JPEG payloads, and multiple consecutive messages until `EndOfMessage`, enforces the `MaxFrameBytes + HeaderLength` (`5MB + 30` bytes) limit, and handles `Close` and `CancellationToken` cancellation cleanly.

## `LocalAnswerCache` Architecture & Known Limitations

- **Storage format & encryption**: Answers for each attempt are serialized to JSON and encrypted with AES using a SHA-256 key derived from `attemptId` and a static IV (first 16 bytes of the key). This existing format is intentionally preserved so existing student cache files remain readable without a breaking migration; it is obfuscation/integrity-at-rest for local resilience, not defense against a user with local admin access to the machine.
- **Atomic file writes**: `SaveAll` writes and flushes to a `.tmp` file in the same directory before calling `File.Move(..., overwrite: true)`. If replacement fails (e.g., file lock), the previous snapshot remains intact.
- **In-process locking only**: `CacheLock` serializes read-modify-write operations across threads within a single application process. It does **not** coordinate concurrent writes across multiple running instances of `CourseGuard.exe` for the same `attemptId`.
- **Best-effort persistence**: Cache I/O errors return `false` (or empty list on corrupt/unreadable files) so UI flows never crash; if both DB and local disk fail, the selection is not marked saved and `DoExamForm` notifies the student.
- **Scope**: Automated tests here do not replace live database integration tests or two-machine UI/network verification.

## Anti-Cheat & Proctoring Policy (`docs/TEAM_HANDOFF.md`)

### Active Mechanisms
- **Keyboard hook (`LowLevelKeyboardHook`)**: Blocks `Alt+Tab`, `Alt+Esc`, `Ctrl+Esc`, `LWin`, `RWin`, `Alt+F4`, `Ctrl+C`, `Ctrl+V`, `Ctrl+X`, and `Shift+Insert` during an active exam session, sends a warning frame, and logs `KEY_PRESS`.
- **Focus & close protection (`DoExamForm`)**: Prevents minimizing, logs `EXAM_CLOSE_ATTEMPT` if closing without submitting, and logs `SCREEN_SWITCH` on `Deactivate` **except** when suppressed by `ShouldIgnoreDeactivateViolation()` (internal dialogs via `ShowExamDialog`, intentional close via `_allowCloseWithoutSubmit`, submitted/auto-submitted state, or visible OTP lock panel `_pnlLock`).
- **Violation threshold & auto-submit**: `EvaluateViolationThresholdAsync` queries persisted `violations` count and triggers `SubmitExam(confirm: false)` when `MaxViolations > 0` is reached.
- **Proctoring & connection loss**: `StudentScreenStreamClient` requires an available webcam at start (`IsCameraAvailable()`) and streams frames to `RELAY_WS_URL`. After 30 seconds of lost relay connectivity following an established connection (`ScreenMonitorConnectionLossTracker`), `DoExamForm` logs `CONNECTION_LOST` and locks the screen with OTP recovery (`OtpRecoveryService`).

### Intentionally Disabled Startup Checks
In `DoExamForm.LoadExamSession()`, the following `AntiCheatEngine` startup checks remain commented out per `docs/TEAM_HANDOFF.md` and team lead policy:
- `AntiCheatEngine.HasMultipleScreens()`
- `AntiCheatEngine.IsRunningInVirtualMachine()`
- `AntiCheatEngine.GetRunningBlacklistedApps()`

Do not re-enable these checks without explicit approval from the team lead, as they can falsely lock out multi-monitor, VM, or screen-recording demo setups.

## Two-Machine Verification Checklist

1. **Relay & Storage Pre-check**: Confirm `RELAY_WS_URL` points to the shared `CourseGuardRelayApi` instance and Supabase bucket `exam-violations` exists with upload/read policy (if storage fails, `MonitorPopupForm` still records the DB violation with a warning).
2. **Authoring & AI Import (Machine 1 - Teacher)**: Create `DRAFT` exam -> add questions manually, from bank, random builder, and AI generator (edit text on grid, verify `DRAFT` guard) -> activate exam (`ACTIVE`).
3. **Exam Start & Offline Resume (Machine 2 - Student with webcam)**: Start exam -> disconnect network -> answer/change questions (verify `Đã lưu tạm (Ngoại tuyến)`) -> close and reopen app -> confirm cached answers restore immediately -> reconnect network -> confirm automatic sync and `SubmitExam` succeeds (including timer expiry `00:00`).
4. **Live Proctoring & Anti-Cheat (Machines 1 & 2)**: Open `UC_ExamMonitor` -> `MonitorAllForm` ("Giám sát tất cả") and `MonitorPopupForm` ("Giám sát cá nhân") -> trigger `Alt+Tab` / tab switch -> verify red alert on Teacher side -> click "Ghi nhận vi phạm" and verify violation row + image URL -> open valid dialogs on Student side and verify no false `SCREEN_SWITCH` violation is recorded -> close forms and verify webcam LED, sockets, timers, and keyboard hook release cleanly.
5. **Post-Exam Review**: Teacher closes exam (`CLOSED`) -> Student views review in `StudentExamReviewForm` -> verify scores match `UC_TeacherResults` and cannot be modified outside `SUBMITTED`/`COMPLETED`/`GRADED`.
