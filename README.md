# CourseGuard

Ứng dụng C#/.NET 10 Windows Forms quản lý khóa học, bài tập, thi và lớp học trực tuyến. Bản hiện tại dùng **WinForms → ASP.NET Core Server → PostgreSQL**.

Client giữ giao diện hiện có và gọi API bằng phiên đăng nhập có OTP. Database, SMTP và khóa AI đặt ở server. Signaling lớp học và camera giám sát đi qua server có xác thực; âm thanh/video WebRTC trao đổi theo cơ chế WebRTC.

## Chạy trên một máy

Cần Windows, .NET SDK 10, WebView2 Runtime và database dev của nhóm đã khởi tạo.

1. Tạo `CourseGuard/CourseGuard.Server/.env` từ file mẫu cạnh nó, điền kết nối database và SMTP. Thêm `OPENROUTER_API_KEY` nếu dùng AI. Không gửi file này cho người chỉ dùng app.
2. Tại gốc repo, chạy:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Start-CourseGuard.ps1
```

Script build lần đầu, chạy server nền tại `http://127.0.0.1:5080`, rồi mở WinForms. Gói chạy nằm ở `.local/server` và `.local/client`. Cấu hình client chỉ có `COURSEGUARD_API_URL`. Chạy lại script để mở thêm client.

Sau khi thay code, đóng tiến trình của bản đang build rồi tạo lại gói:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Start-CourseGuard.ps1 -BuildOnly
```

## Chạy client trên máy khác

Triển khai server Windows với HTTPS, rồi chạy:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Start-CourseGuard.ps1 -ClientOnly -ApiUrl https://YOUR_SERVER
```

Không đưa mật khẩu database, SMTP hoặc khóa AI vào client. OTP gửi tới email trong bản ghi tài khoản; SMTP trong cấu hình server là bên gửi thư.

- [Cấu hình, kiến trúc và kiểm thử](docs/README.md)
- [Sơ đồ kiến trúc](docs/architecture.md)
- [Kết quả chuyển đổi](docs/SERVER_MIGRATION.md)
- [Phân công tính năng](docs/TEAM_HANDOFF.md)

Các nhánh cải tiến tính năng của thành viên vẫn cần review và tích hợp riêng. Chuyển sang API không đồng nghĩa mọi tính năng đã được nghiệm thu trên nhiều máy/khác mạng.
