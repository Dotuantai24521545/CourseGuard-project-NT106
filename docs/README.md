# CourseGuard — hướng dẫn chạy và kiểm thử

Tài liệu đồng bộ với baseline dev được kiểm tra ngày 19/09/2026. Đây là ứng dụng Windows Forms quản lý khóa học và thi trực tuyến; trạng thái kiểm thử không đồng nghĩa mọi chức năng đã sẵn sàng production.

## 1. Kiến trúc và cấu trúc repository

- Solution chứa client và relay: `CourseGuard/CourseGuard.slnx`.
- Client: `CourseGuard/CourseGuard/CourseGuard.csproj`, C# / .NET 10 / Windows Forms.
- Điểm khởi chạy: `CourseGuard/CourseGuard/Frontend/Program.cs`.
- Giao diện: `Frontend/Forms`, `Frontend/UserControls`; nghiệp vụ: `Backend/Controllers`, `Backend/Services`; SQL: `Backend/Data`.
- `CourseGuardDbContext` và repositories dùng Npgsql kết nối trực tiếp PostgreSQL trên Supabase. Chúng không phải Entity Framework DbContext.
- `CourseGuard/CourseGuardRelayApi` là ứng dụng ASP.NET Core riêng, có các endpoint WebSocket `/relay`, `/classroom-relay`, `/classroom-status-relay`. Relay không phải API trung gian cho mọi truy vấn database.
- Supabase Storage/Realtime, SMTP và OpenRouter là các tích hợp cần cấu hình riêng.

Tài liệu liên quan: [thuyết trình](../present.md), [phân công nhóm](TEAM_HANDOFF.md), [feature/integration tests](../tests/CourseGuard.FeatureTests/README.md), [exam tests](../tests/CourseGuard.ExamTests/README.md).

## 2. Yêu cầu môi trường

- Windows 10/11 và .NET SDK 10.x; dùng Visual Studio/Rider hỗ trợ target của project hoặc terminal.
- Internet để restore NuGet và truy cập dịch vụ cloud.
- Database Supabase dev có schema tương ứng. Không cần cài PostgreSQL local khi dùng cloud.
- WebView2 Runtime cho phần giao diện WebView2; camera/microphone và cấu hình mạng phù hợp khi kiểm thử lớp học.

## 3. Cấu hình trước khi chạy

Từ thư mục gốc repo, chỉ khi chưa có `.env`:

```powershell
Copy-Item CourseGuard/CourseGuard/.env.example CourseGuard/CourseGuard/.env
```

Điền giá trị thật trong `.env` trên máy. Không ghi đè file cấu hình đã có, không commit secret. Clone repo chỉ tải mã nguồn và SQL, không tải database cloud hoặc mật khẩu của nhóm.

| Biến | Công dụng |
| --- | --- |
| `COURSEGUARD_DB_CONNECTION` | Chuỗi kết nối Npgsql tới database. Code còn hỗ trợ alias `SUPABASE_DB_CONNECTION`, `CONNECTION_STRING`. |
| `SMTP_HOST`, `SMTP_USER`, `SMTP_PASS` | Bắt buộc khi khởi tạo dịch vụ SMTP; không có tài khoản SMTP mặc định trong code. |
| `SMTP_PORT` | Mặc định 587; dịch vụ hiện dùng STARTTLS. |
| `SMTP_FROM_EMAIL`, `SMTP_FROM_NAME` | Mặc định lần lượt là SMTP user và `CourseGuard Admin`. |
| `OTP_EXPIRY_MINUTES` | Luồng đăng nhập async đọc thời hạn OTP; mặc định 5 phút. |
| `SUPABASE_URL`, `SUPABASE_ANON_KEY` | Storage/Realtime và helper Supabase Auth; không thay thế chuỗi kết nối PostgreSQL. |
| `OPENROUTER_API_KEY` | Bắt buộc khi gọi AI. |
| `OPENROUTER_MODEL` | Mặc định `openrouter/free`; có thể đặt model khác được tài khoản hỗ trợ. |
| `RELAY_WS_URL` | Được các dịch vụ classroom đọc; cần rà riêng endpoint của từng thành phần khi triển khai/kiểm thử relay. |

Ví dụ định dạng, không phải thông tin đăng nhập thật:

```env
COURSEGUARD_DB_CONNECTION=Host=YOUR_POOLER_HOST;Port=5432;Database=postgres;Username=postgres.YOUR_PROJECT_REF;Password=YOUR_DB_PASSWORD;SSL Mode=Require;Timeout=15
```

Lấy các tham số từ phần kết nối Session pooler của project Supabase. Mật khẩu database khác mật khẩu tài khoản đăng nhập CourseGuard. Với chuỗi Npgsql, không dùng quy tắc percent-encoding của PostgreSQL URI; ký tự đặc biệt cần được xử lý theo định dạng connection string.

`AppEnvironment` tìm `.env` từ thư mục executable đi ngược lên; project copy file này vào output khi build. Biến môi trường đã tồn tại được ưu tiên, kể cả biến rỗng. Parser hiện đọc từng dòng `KEY=value`, không xử lý đầy đủ cú pháp dotenv: tránh `export`, dấu nháy bao quanh cả giá trị và comment cuối dòng. Sau khi sửa `.env`, build/chạy lại; `--no-build` có thể dùng bản cấu hình đã copy trước đó.

Tạo database không tự tạo bucket/policy Storage, cấu hình Realtime, tài khoản SMTP hoặc relay. Không dùng service-role key để thay thế anon key trong client nhằm bỏ qua lỗi quyền.

## 4. Chuẩn bị database

### Database dev đã được nhóm khởi tạo

Nhận cấu hình riêng từ trưởng nhóm và dùng lại schema đang có. Không chạy lại lệnh khởi tạo hoặc script reset.

### Database dev mới, public schema trống

Sau khi cấu hình `.env`, chạy từ gốc repo và thay `YOUR_PROJECT_REF`:

```powershell
dotnet run --project tests/CourseGuard.FeatureTests/CourseGuard.FeatureTests.csproj -- --initialize-empty-database --project-ref=YOUR_PROJECT_REF
```

Lệnh kiểm tra username khớp project, từ chối schema public không trống, bỏ phần reset của `SupabaseSchema.sql`, tạo bảng/dữ liệu demo và áp dụng migration theo thứ tự trong một transaction. RLS được bật trên các bảng tạo ra; điều đó chưa cung cấp policy truy cập đầy đủ cho Storage/Realtime.

**Không chạy toàn bộ `SupabaseSchema.sql` trực tiếp trên database đang dùng:** phần đầu có `DROP TABLE ... CASCADE`. Initializer không phải công cụ nâng cấp database đã có dữ liệu; migration mới phải được review và áp dụng riêng.

Bảng chính sau migration: `users`, `roles`, `courses`, `enrollments`, `teacher_lessons`, `teacher_assignments`, `materials`, `exams`, `exam_questions`, `exam_attempts`, `exam_attempt_answers`, `messages`, `chat_reads`, `polls`, `online_sessions`, `attendance_logs`, `notifications`, `devices`, `violations`, `audit_logs`. Code còn có các hàm tạo/bổ sung schema khi thực thi; danh sách này không thay thế SQL và migrations.

## 5. Build và chạy

```powershell
dotnet restore CourseGuard/CourseGuard.slnx
dotnet build CourseGuard/CourseGuard.slnx
dotnet run --project CourseGuard/CourseGuard/CourseGuard.csproj
```

Trong IDE, mở solution và chọn client `CourseGuard` làm startup project. Relay chạy riêng khi cần:

```powershell
dotnet run --project CourseGuard/CourseGuardRelayApi/CourseGuardRelayApi.csproj
```

Relay đọc `PORT`, mặc định 8080. Lệnh này không triển khai server cloud và không tự cập nhật mọi URL phía client. Kiểm thử nhiều máy/khác mạng cần endpoint truy cập được và kiểm tra riêng camera, mic, firewall, reconnect.

## 6. Tài khoản demo và đăng nhập

Schema seed có `admin` (admin), `student` (student), `thnghi` cùng các tài khoản giáo viên mẫu; mật khẩu seed là `admin123`. Chỉ sử dụng cho database dev/demo.

Luồng `AuthController.LoginAsync` kiểm tra tài khoản rồi tạo OTP, lưu OTP và đưa email vào hàng đợi trước khi yêu cầu MFA. `CompleteMfaLogin` hoàn tất sau xác minh. Việc gọi `Login` đồng bộ thành công trong smoke test không chứng minh luồng MFA giao diện đã hoạt động.

Email seed có đuôi `.local`, không dùng để nhận OTP thật. Cần cấu hình SMTP và email nhận test hợp lệ trước khi nghiệm thu đăng nhập giao diện. Hàng đợi email hiện chưa xử lý đầy đủ trạng thái gửi thất bại; thiếu cấu hình cũng có thể làm khởi tạo dịch vụ lỗi. Công việc này thuộc nhánh auth/admin trong bản phân công.

Luồng quên mật khẩu có yêu cầu admin xử lý, mật khẩu tạm và kiểm tra hết hạn/bắt đổi mật khẩu. Cần kiểm thử thực tế việc gửi mail, duyệt yêu cầu và đăng nhập lại; không coi là đã đạt chỉ dựa vào test mật khẩu.

## 7. Kiểm thử và bằng chứng hiện có

```powershell
# Logic, source contracts và WinForms control tests; không gọi AI/database thật
dotnet run --project tests/CourseGuard.FeatureTests/CourseGuard.FeatureTests.csproj -- --offline

# Chấm điểm và cache, không cần database
dotnet run --project tests/CourseGuard.ExamTests/CourseGuard.ExamTests.csproj

# Cấu hình, schema, thực thi truy vấn database
dotnet run --project tests/CourseGuard.FeatureTests/CourseGuard.FeatureTests.csproj -- --integration --database-only

# Database + bốn yêu cầu AI với nội dung bài học mẫu
dotnet run --project tests/CourseGuard.FeatureTests/CourseGuard.FeatureTests.csproj -- --integration

# Có ghi/xóa fixture: chỉ chạy trên database dev đã xác nhận
dotnet run --project tests/CourseGuard.FeatureTests/CourseGuard.FeatureTests.csproj -- --dev-exam-smoke --project-ref=YOUR_PROJECT_REF
```

Kết quả baseline kiểm tra ngày 19/09/2026, không phải cam kết cho mọi môi trường:

| Kiểm tra | Kết quả đã ghi nhận | Giới hạn |
| --- | --- | --- |
| Build solution | 0 error, 0 warning | Một số warning đã được cấu hình hạ mức/ẩn trong project. |
| Feature offline | 249 đạt, 0 lỗi, 3 bỏ qua | Ba ca liên quan `.env`, AI và database; gồm cả test source contract, không phải 249 luồng UI đầu-cuối. |
| Exam tests | 6/6 đạt | Scoring và cache cô lập, không chứng minh mất mạng end-to-end. |
| Database integration | 3/3 đạt | Kiểm tra cấu hình, schema và truy vấn với ID không tồn tại; không chứng minh toàn bộ nghiệp vụ. |
| AI thật | 4/4 đạt ở lần chạy trước | Tutor, flashcard, chấm tự luận, sinh câu hỏi; còn phụ thuộc quota/model/mạng. |
| Exam smoke trên cloud dev | Đạt | Xác thực đồng bộ, mở/lưu/resume/nộp, điểm lưu 10/10, chặn sửa sau nộp và giới hạn lượt; fixture đã dọn. Không gửi MFA email. |

## 8. Thay đổi đã thực hiện và giới hạn còn lại

- Không tính điểm cho đáp án rỗng; student/teacher thống nhất làm tròn decimal hai chữ số, midpoint away from zero.
- Cache ghi file tạm, flush rồi thay file đích, khóa read-modify-write trong một process. Giữ định dạng mã hóa cũ và best-effort error handling; chưa bảo đảm phục hồi mọi trường hợp mất mạng/tắt máy/nhiều process.
- AI đọc key từ cấu hình, dùng model cấu hình được, báo lỗi HTTP/nội dung sai thay vì chấm điểm mẫu; gọi thật đã kiểm tra, UI còn cần nghiệm thu.
- Sửa truy vấn lịch thi về `open_time`/`close_time`; bảng xếp hạng dùng `exam_attempts`, giới hạn course và lượt đã nộp, không trả dữ liệu demo khi DB lỗi.
- Sửa cảnh báo truy cập cột xem ảnh và dọn biến/đoạn code chống gian lận đã comment; không có nghĩa mọi chức năng anti-cheat đã bật.
- Sửa migration ảnh vi phạm dùng tên PostgreSQL lowercase đúng với schema.

Còn phải nghiệm thu: OTP/email, đầy đủ UI và phân quyền, workflow khóa học/bài tập, upload/download và policy project mới, chat/lớp học/giám sát hai máy, mất mạng giữa lúc thi và triển khai relay. Xem [phân công bốn nhánh](TEAM_HANDOFF.md).

## 9. Xử lý lỗi thường gặp

| Triệu chứng | Kiểm tra |
| --- | --- |
| Missing required environment variable | Đúng vị trí `.env`, đã thay placeholder, không có biến môi trường rỗng ghi đè; build lại để copy cấu hình. |
| Kết nối DB thất bại | Host/port/user/password đúng project; username session pooler có project ref; network/SSL. Không sửa password trong source. |
| relation does not exist | Database chưa được khởi tạo hoặc thiếu migration; không dùng reset để sửa database có dữ liệu. |
| Không nhận OTP | SMTP cấu hình thật, email user không phải `.local`, kiểm tra lỗi gửi; hàng đợi chưa bảo đảm retry/hiển thị lỗi đầy đủ. |
| Upload/Realtime lỗi | `SUPABASE_URL`, key, bucket/policy/publication và quyền của client; kết nối PostgreSQL thành công không xác nhận các phần này. |
| AI HTTP lỗi/timeout/JSON sai | Key, model, quota và mạng; không dùng kết quả giả để che lỗi. |
| MSB3021/MSB3027 | Đóng ứng dụng đang giữ file executable rồi build lại. |
| CS0006 sau đổi môi trường build | Restore/build trong cùng môi trường; đường dẫn NuGet cache có thể khác giữa user hoặc sandbox. |
| Disposed control hoặc UI treo | Ghi thao tác và stack trace đã che secret; tạo issue cho đúng nhánh, không coi mọi race condition đã được xử lý. |

## 10. Làm việc nhóm

Clone repo nhóm, tạo branch từ `main`, đọc [TEAM_HANDOFF.md](TEAM_HANDOFF.md) và mở PR nhỏ. Review migration trước khi chạy trên DB chung; không commit `.env`, log nhạy cảm hoặc output build. Client hiện kết nối DB trực tiếp, nên phân phối database credentials cho người dùng cuối là giới hạn kiến trúc cần giải quyết trước production. Không đẩy nhánh local lưu lịch sử upstream cũ lên repo nhóm.
