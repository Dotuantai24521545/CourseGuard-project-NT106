# Chạy CourseGuard client–server

Hướng dẫn từng bước cho thành viên lấy code, chạy local, build lại và gửi PR nằm ngay trong [README gốc](../README.md). Mỗi bạn chạy cả server và client trên máy mình; database dev có thể dùng chung. Server chung qua Internet là phương án triển khai sau, không phải điều kiện để phát triển code.

## Thành phần

- `CourseGuard/CourseGuard`: WinForms, bộ gọi HTTP API, cache đáp án và xử lý camera/WebRTC ở máy người dùng.
- `CourseGuard/CourseGuard.Server`: ASP.NET Core, đăng nhập OTP, phiên đăng nhập, kiểm tra quyền, nghiệp vụ và truy vấn PostgreSQL.
- `CourseGuard/CourseGuardRelayApi`: relay phiên bản cũ, không cần chạy trong chế độ API mới.

Server tái sử dụng các lớp nghiệp vụ/dữ liệu đang có bằng liên kết mã nguồn. Vì còn helper ảnh và export dùng thư viện Windows, server hiện target `net10.0-windows`. Client còn chứa một số mã dữ liệu cũ phục vụ tương thích và kiểm thử; ở chế độ API, mở kết nối database trực tiếp và đọc bí mật server bị chặn. Gói client mới chỉ có địa chỉ API.

## Cấu hình và chạy

Server dùng `.env` cạnh project `CourseGuard.Server`. Biến bắt buộc: `COURSEGUARD_DB_CONNECTION`, `SMTP_HOST`, `SMTP_USER`, `SMTP_PASS`. SMTP mặc định 587/STARTTLS; Gmail cần mật khẩu ứng dụng. `SMTP_FROM_EMAIL` và `SMTP_FROM_NAME` là người gửi. AI dùng `OPENROUTER_API_KEY`, tùy chọn `OPENROUTER_MODEL`.

Client dùng `COURSEGUARD_API_URL`, ví dụ local `http://127.0.0.1:5080`. Kết nối ngoài localhost phải dùng HTTPS. Không dùng lại file client cũ chứa kết nối database.

Chạy `scripts/Start-CourseGuard.ps1` tại gốc repo. `-BuildOnly` tạo lại gói; `-ServerOnly` chạy server; `-ClientOnly -ApiUrl ...` kết nối server đã triển khai. Script chỉ build nếu chưa có gói hoặc dùng BuildOnly; khi thay code cần build lại. File chạy và log ở `.local`, thư mục này được Git bỏ qua.

Khi build lại: đóng app, chạy `scripts/Stop-CourseGuardServer.ps1`, sau đó Start với `-BuildOnly`, rồi Start bình thường. Script Stop kiểm tra cả PID và đường dẫn executable để không dừng nhầm server của repo khác.

Email nhận OTP nằm trong database; thay email admin trên database chung không yêu cầu cả nhóm thay cấu hình client. Không commit mật khẩu/token.

## Quyền truy cập

Server kiểm tra mật khẩu, tài khoản và thiết bị trước khi gửi OTP. OTP có hạn 5 phút, tối đa 5 lần thử, dùng một lần. Cookie phiên là HttpOnly; tài khoản và phiên bản mật khẩu được kiểm tra lại khi gọi API. Tài khoản dùng mật khẩu tạm chỉ được xem hồ sơ, đổi mật khẩu hoặc đăng xuất cho tới khi đổi xong.

API lấy ID từ phiên, yêu cầu role cho endpoint quản trị/giảng viên và kiểm tra quyền khóa học/lượt thi trong lớp dữ liệu. Khóa học/đề thi mới luôn ở trạng thái nháp, không tin trạng thái kích hoạt do client gửi. Không có endpoint thực thi SQL hoặc gọi phương thức tùy ý.

## Chat, tệp và lớp học

Chat lấy tin mới qua API mỗi 2 giây. Tệp chat/bài tập được tải lên server và lưu trong PostgreSQL. Ảnh nhóm giới hạn tổng 5 MB. API tải tệp kiểm tra quyền khóa học. Bằng chứng giám sát ở bảng `server_evidence`; URL vi phạm là đường API có xác thực. Đây là lưu trữ qua server trong database, chưa phải triển khai Supabase Storage private riêng.

WebView2 lấy vé signaling hạn 30 giây, dùng một lần, qua phiên WinForms. Server kiểm tra lớp học và tự gán danh tính người gửi; tín hiệu chỉ chuyển trong cùng lớp. Kết nối native dùng cookie. Trạng thái mở/đóng lớp được đọc qua API mỗi 3 giây. Camera giám sát dùng WebSocket xác thực và chỉ chuyển khung hình cho giảng viên phụ trách.

WebRTC khác mạng còn phụ thuộc STUN/TURN, firewall và quyền camera/mic. Chưa nghiệm thu camera/mic hai máy khác mạng trong lượt chuyển đổi này. STUN/TURN cấu hình ở server bằng `WEBRTC_*`; giới hạn tệp chat, hành vi reconnect/đóng lớp và kịch bản kiểm thử hai máy: xem [REALTIME_STORAGE.md](REALTIME_STORAGE.md).

## Kiểm thử

```powershell
dotnet run --project tests/CourseGuard.FeatureTests -- --offline
dotnet run --project tests/CourseGuard.ExamTests
dotnet run --project tests/CourseGuard.ApiClientTests
dotnet run --project tests/CourseGuard.ServerSecurityTests
```

Kết quả và giới hạn ghi ở [SERVER_MIGRATION.md](SERVER_MIGRATION.md). Kiểm thử đầu-cuối qua API dùng database/SMTP thật và dữ liệu tạm; không thay thế nghiệm thu tất cả thao tác giao diện. Các script kiểm thử phiên thật nằm dưới `tests/CourseGuard.ApiClientTests`; cookie kiểm thử phải giữ riêng trong `.local`.

## Triển khai

Production cần Windows/.NET 10, HTTPS, bí mật riêng ở server và nơi lưu khóa ASP.NET Data Protection bền vững. Script đi kèm chỉ chạy local; không tự triển khai cloud, cấp TURN hay mở firewall. Không chạy script reset database trên dữ liệu đang dùng.

Tài liệu kiến trúc trước đây nằm ở [LEGACY_DIRECT_DATABASE.md](LEGACY_DIRECT_DATABASE.md), chỉ dùng đối chiếu lịch sử. Các PR tính năng của thành viên chưa merge vẫn cần review và thích nghi với lớp gọi API mới.
