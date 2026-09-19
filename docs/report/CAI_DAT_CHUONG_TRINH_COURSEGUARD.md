# Cài đặt chương trình CourseGuard

Tài liệu này mô tả yêu cầu môi trường, công nghệ sử dụng, quy trình cài đặt và cách chạy hệ thống CourseGuard. Nội dung được tổng hợp từ runbook, file project `.csproj`, cấu trúc source code và các tài liệu phân tích trong `docs/report`.

## 1. Yêu cầu môi trường

CourseGuard là ứng dụng desktop chạy trên Windows, vì vậy môi trường cài đặt cần đáp ứng các yêu cầu sau:

| Nhóm yêu cầu | Nội dung |
| --- | --- |
| Hệ điều hành | Windows 10/11. |
| Runtime/SDK | .NET SDK 10.x, phù hợp với `TargetFramework: net10.0-windows`. |
| Giao diện | Máy cần hỗ trợ chạy ứng dụng Windows Forms. |
| Internet | Cần kết nối Internet ổn định để truy cập Supabase PostgreSQL cloud. |
| Database | Supabase PostgreSQL đã được tạo schema và dữ liệu cần thiết. |
| Quyền hệ thống | Người dùng có quyền chạy ứng dụng desktop và cho phép app kết nối outbound đến database/email service. |
| Công cụ phát triển | Visual Studio/Rider hoặc terminal có lệnh `dotnet`. |

## 2. Công nghệ sử dụng

### 2.1. Nền tảng chính

| Thành phần | Công nghệ |
| --- | --- |
| Ngôn ngữ lập trình | C# |
| Framework | .NET `net10.0-windows` |
| Giao diện | Windows Forms |
| Kiến trúc triển khai | Desktop monolith kết nối trực tiếp database cloud |
| Database | PostgreSQL/Supabase |
| Kết nối database | Npgsql |

### 2.2. Thư viện/package chính

Các dependency được khai báo trong `CourseGuard/CourseGuard/CourseGuard.csproj`:

| Package | Phiên bản | Vai trò |
| --- | ---: | --- |
| `DotNetEnv` | `3.2.0` | Đọc cấu hình từ file `.env` hoặc biến môi trường. |
| `Google.Apis.Gmail.v1` | `1.73.0.4029` | Hỗ trợ tích hợp Gmail API nếu cần. |
| `MailKit` | `4.16.0` | Gửi email qua SMTP, dùng trong luồng quên mật khẩu. |
| `Npgsql` | `10.0.2` | Kết nối và truy vấn PostgreSQL/Supabase. |
| `CsvHelper` | `33.0.1` | Hỗ trợ xử lý/xuất dữ liệu CSV. |
| `ScottPlot.WinForms` | `5.1.58` | Hiển thị biểu đồ/thống kê trên giao diện WinForms. |

### 2.3. Docker, requirements và setup script

Tại thời điểm phân tích source code hiện tại:

- Project **không có Dockerfile hoặc docker-compose** cho app chính.
- Project **không dùng `requirements.txt`** vì đây không phải project Python.
- Dependency được quản lý bằng **NuGet** thông qua file `.csproj`.
- Quy trình setup chính dùng các lệnh **`dotnet restore`**, **`dotnet build`**, **`dotnet run`**.

Do CourseGuard là ứng dụng WinForms chạy trên Windows, Docker không phải phương án chạy chính. Nếu cần container hóa trong tương lai, nên tách backend/API riêng trước, còn client WinForms vẫn chạy trên máy người dùng.

## 3. Chuẩn bị trước khi cài đặt

### 3.1. Kiểm tra .NET SDK

Mở PowerShell hoặc terminal và chạy:

```powershell
dotnet --version
```

Nếu máy chưa có .NET SDK 10.x, cần cài đặt SDK tương ứng trước khi restore/build project.

### 3.2. Kiểm tra database

Database của hệ thống nằm trên Supabase PostgreSQL. Schema tham chiếu nằm tại:

```text
CourseGuard/CourseGuard/Backend/Database/Scripts/SupabaseSchema.sql
```

Các nhóm bảng chính gồm:

- `USERS`, `ROLES`, `PERMISSIONS`, `ROLE_PERMISSIONS`
- `COURSES`, `ENROLLMENTS`, `MATERIALS`, `MESSAGES`
- `EXAMS`, `QUESTIONS`, `EXAM_ATTEMPTS`, `ANSWERS`, `VIOLATIONS`
- `AUDIT_LOGS`, `NOTIFICATIONS`, `DEVICES`

### 3.3. Kiểm tra cấu hình môi trường

Project có sử dụng file `.env` và biến môi trường cho một số cấu hình. Một số cấu hình email có thể override bằng biến môi trường:

```text
SMTP_HOST
SMTP_PORT
SMTP_USER
SMTP_PASS
SMTP_FROM_EMAIL
SMTP_FROM_NAME
```

Nếu không cấu hình SMTP riêng, project hiện có default SMTP trong `Backend/Services/SmtpEmailService.cs` để phục vụ chạy local/demo.

## 4. Quy trình cài đặt

### Bước 1: Lấy source code

Clone hoặc pull source code về máy local. Sau đó mở terminal tại thư mục gốc repository.

Cấu trúc chính cần có:

```text
CourseGuard/CourseGuard/CourseGuard.sln
CourseGuard/CourseGuard/CourseGuard.csproj
docs/README.md
```

### Bước 2: Restore package NuGet

Chạy lệnh:

```powershell
dotnet restore "CourseGuard/CourseGuard/CourseGuard.csproj"
```

Lệnh này tải các package NuGet được khai báo trong `.csproj` như `Npgsql`, `MailKit`, `DotNetEnv`, `CsvHelper`, `ScottPlot.WinForms`.

### Bước 3: Build project

Chạy lệnh:

```powershell
dotnet build "CourseGuard/CourseGuard/CourseGuard.csproj"
```

Nếu build thành công, project đã sẵn sàng để chạy.

### Bước 4: Kiểm tra kết nối database

Trước khi chạy app, cần bảo đảm:

- Máy có Internet.
- Firewall/antivirus không chặn kết nối outbound.
- Connection string trong các file data access còn hợp lệ.

Các file kết nối database chính:

```text
CourseGuard/CourseGuard/Backend/Data/CourseGuardDbContext.cs
CourseGuard/CourseGuard/Backend/Data/DatabaseAction.cs
CourseGuard/CourseGuard/Backend/Data/ScoreRepository.cs
CourseGuard/CourseGuard/Backend/Data/NotificationRepository.cs
```

## 5. Cách chạy hệ thống

### 5.1. Chạy bằng Visual Studio hoặc Rider

1. Mở solution:

```text
CourseGuard/CourseGuard/CourseGuard.sln
```

2. Chọn startup project là `CourseGuard`.
3. Nhấn `F5` hoặc `Ctrl + F5` để chạy.
4. Đăng nhập bằng tài khoản test nếu database đã có dữ liệu:

```text
Username: admin
Password: admin123
```

### 5.2. Chạy bằng terminal

Từ thư mục gốc repository, chạy:

```powershell
dotnet run --project "CourseGuard/CourseGuard/CourseGuard.csproj"
```

Nếu muốn chạy lại từ đầu theo đầy đủ quy trình:

```powershell
dotnet restore "CourseGuard/CourseGuard/CourseGuard.csproj"
dotnet build "CourseGuard/CourseGuard/CourseGuard.csproj"
dotnet run --project "CourseGuard/CourseGuard/CourseGuard.csproj"
```

## 6. Flow kiểm thử sau khi chạy

Sau khi chương trình khởi động thành công, có thể kiểm thử nhanh theo các bước:

| Bước | Thao tác | Kết quả mong đợi |
| ---: | --- | --- |
| 1 | Đăng nhập bằng tài khoản admin | Vào được dashboard admin. |
| 2 | Kiểm tra Admin Dashboard | KPI, user request và recent activities hiển thị dữ liệu. |
| 3 | Mở User Management | Có thể tìm kiếm/lọc người dùng. |
| 4 | Mở Course Management | Danh sách khóa học được load từ database. |
| 5 | Mở Reports | Có thể lọc dữ liệu theo ngày và xuất báo cáo. |
| 6 | Đăng nhập role teacher/student | Dashboard tương ứng hiển thị đúng layout. |
| 7 | Logout | Quay về màn hình đăng nhập. |

## 7. Lỗi thường gặp khi cài đặt/chạy

| Lỗi | Nguyên nhân thường gặp | Cách xử lý |
| --- | --- | --- |
| Không restore được package | Mất Internet hoặc NuGet bị chặn | Kiểm tra mạng, proxy/firewall. |
| Build lỗi `MSB3021/MSB3027` | File `.exe` đang bị lock do app còn chạy | Đóng app CourseGuard rồi build lại. |
| Không kết nối được database | Mất Internet, connection string sai, firewall chặn | Kiểm tra Internet và các file data access. |
| Gửi email reset password thất bại | SMTP config sai hoặc Gmail App Password hết hiệu lực | Kiểm tra `SMTP_USER`, `SMTP_PASS`, default SMTP. |
| App treo khi load dữ liệu | DB chậm hoặc control bị dispose khi load async | Thử thao tác lại, ghi nhận màn hình/stack trace nếu có. |

## 8. Ghi chú triển khai

- Hệ thống hiện phù hợp với môi trường học tập, demo hoặc triển khai nội bộ quy mô nhỏ.
- Ứng dụng WinForms kết nối trực tiếp đến Supabase PostgreSQL, chưa có backend API server tách riêng.
- Không nên dùng connection string hardcoded cho môi trường production.
- Nếu triển khai thực tế, nên chuyển cấu hình nhạy cảm sang environment variables hoặc secret manager.
- Nếu muốn dùng Docker, nên container hóa database/dev service hoặc tách backend API trước; không nên xem Docker là cách chạy chính cho WinForms client.

## 9. Tóm tắt quy trình cài đặt nhanh

```powershell
# 1. Restore dependency
dotnet restore "CourseGuard/CourseGuard/CourseGuard.csproj"

# 2. Build project
dotnet build "CourseGuard/CourseGuard/CourseGuard.csproj"

# 3. Run application
dotnet run --project "CourseGuard/CourseGuard/CourseGuard.csproj"
```
