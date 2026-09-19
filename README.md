# CourseGuard

Ứng dụng C# / .NET 10 Windows Forms quản lý khóa học và thi trực tuyến, kết nối Supabase PostgreSQL và có relay WebSocket riêng.

- [Hướng dẫn cấu hình, chạy và kiểm thử](docs/README.md)
- [Giới thiệu và kiến trúc để thuyết trình](present.md)
- [Phân công bốn nhánh và tiêu chí nghiệm thu](docs/TEAM_HANDOFF.md)
- [Feature/integration tests và khởi tạo database dev](tests/CourseGuard.FeatureTests/README.md)
- [Kiểm thử chấm điểm và cache](tests/CourseGuard.ExamTests/README.md)

## Bắt đầu

Cần Windows và .NET SDK 10.x. Đọc hướng dẫn trước khi chạy: tạo `.env` từ [file mẫu](CourseGuard/CourseGuard/.env.example), điền cấu hình riêng và dùng database dev đã khởi tạo.

```powershell
dotnet restore CourseGuard/CourseGuard.slnx
dotnet build CourseGuard/CourseGuard.slnx
dotnet run --project CourseGuard/CourseGuard/CourseGuard.csproj
```

Clone repo không kèm database credentials. Đăng nhập UI dùng OTP nên còn cần SMTP/email nhận hợp lệ; tài khoản seed `.local` không nhận thư thật. Không chạy script reset schema trên database đang sử dụng.

Baseline đã kiểm tra build, test offline, database và luồng thi backend; OTP/email, đầy đủ UI, storage/realtime và giám sát hai máy vẫn cần nghiệm thu. Kết quả cụ thể và giới hạn được ghi trong hướng dẫn. Các tài liệu cũ dưới `docs/report` chỉ là tài liệu tham khảo, không thay thế runbook hiện tại.
