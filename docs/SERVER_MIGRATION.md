# Kiểm tra chuyển đổi client–server

Ngày kiểm tra: 16/11/2026. Phạm vi là chuyển hạ tầng và giữ các luồng nghiệp vụ hiện có; các cải tiến tính năng của thành viên được xử lý trên PR riêng.

## Đã chuyển

- WinForms gọi HTTP API cho đăng nhập/OTP, đăng ký, khôi phục/đổi mật khẩu, hồ sơ, quản trị người dùng/thiết bị/cài đặt, báo cáo và bảng điểm.
- Các thao tác giảng viên, đăng ký học, bài học/bài tập, làm bài/lưu đáp án/nộp bài và kết quả chạy nghiệp vụ/truy vấn trên server.
- Chat văn bản/tệp/ảnh nhóm/bình chọn, thông báo, nhắc hạn và AI đi qua server. Chat cập nhật định kỳ 2 giây, không tuyên bố đây là push realtime.
- Camera giám sát từng người/nhiều người dùng WebSocket có xác thực; header frame phải khớp người dùng/lượt thi. Ảnh bằng chứng lưu riêng trên server trong PostgreSQL.
- Signaling WebRTC dùng phiên native hoặc vé một lần do WinForms cấp cho trang lớp học nội bộ. Server kiểm tra quyền lớp, sửa lại danh tính người gửi và chuyển đúng phòng. Mở/đóng lớp dùng API có quyền giảng viên; bên nhận đọc trạng thái định kỳ.
- Gói client chỉ có `COURSEGUARD_API_URL`; cấu hình database/SMTP/AI nằm ở gói server. Lớp dữ liệu cũ còn được biên dịch để tái sử dụng/kiểm thử; chế độ API chặn kết nối DB trực tiếp. Cần giữ API mode khi phát hành.

## Kết quả đã chạy

| Kiểm tra | Kết quả |
|---|---|
| Build solution client/server | 0 lỗi, 0 cảnh báo |
| Feature tests offline | 249 đạt, 0 lỗi, 3 bỏ qua |
| HTTP adapter với server giả lập | 12 đạt |
| OTP: sai mã, hết hạn, giới hạn lần thử, replay đồng thời, thay thử thách, phiên bản mật khẩu | 6 đạt |
| Đăng nhập ba vai trò qua database và SMTP thật | OTP xác minh thành công |
| Quyền admin | Giảng viên/sinh viên bị chặn HTTP 403 khi truy cập danh sách người dùng admin |
| API đọc dữ liệu thật | 15 API giảng viên, 11 API sinh viên và 4 loại báo cáo admin thành công |
| Dashboard WinForms bằng phiên thật, không có mật khẩu DB/SMTP ở client | ADMIN, TEACHER, STUDENT đều mở và tải dữ liệu thành công |
| Luồng ghi API với fixture riêng | Tạo/duyệt khóa học, ghi danh/duyệt, chat, upload/download tệp, tạo/vote/đóng poll, nộp bài tập thành công |
| Bài thi API với fixture riêng | Tạo câu hỏi/kích hoạt, bắt đầu bài, lưu đáp án, nộp bài; server chấm đúng 10 điểm |
| Lớp học API | Tạo/mở phiên, vé signaling không dùng lại được, thay sender giả bằng danh tính thật, điểm danh vào/ra thành công |
| Camera protocol | Khung hình giả lập đi qua WebSocket xác thực tới giảng viên phụ trách |

Khóa học thử có tên riêng và được xóa sau mỗi lần chạy. Audit log và thay đổi bảo mật tài khoản không được xóa. Bộ kiểm thử async cũ từng treo do ngữ cảnh WinForms không có message pump; runner đã được sửa để tách ngữ cảnh và chạy lại đầy đủ đạt kết quả trên.

## Giới hạn nghiệm thu

- Chưa nghiệm thu âm thanh/video/chia sẻ màn hình bằng camera/mic thật trên hai máy khác mạng. Kiểm tra signaling và frame giả lập không chứng minh chất lượng media hay cấu hình TURN.
- Chưa click kiểm tra mọi màn hình và mọi tình huống nghiệp vụ; giữ nguyên các giới hạn tính năng chưa hoàn thiện của dự án. Các PR tính năng chưa merge không tự được tích hợp ở lần chuyển hạ tầng này.
- AI adapter đã kiểm tra đi qua server; chưa chạy lại mọi yêu cầu OpenRouter có tính phí/hạn mức trong lượt này.
- Bản server hiện cần Windows do tái sử dụng helper ảnh và export. Chưa triển khai cloud hoặc đóng gói Linux.
- File/ảnh được lưu qua server trong PostgreSQL; không tuyên bố đã triển khai Supabase Storage private độc lập. Gói phát hành không chứa file `.env` server trong thư mục client.
- Các thử thách OTP/vé lớp học ở bộ nhớ một tiến trình. Restart hủy thử thách đang chờ. Triển khai nhiều server cần kho dùng chung cho các dữ liệu này; khóa Data Protection phải được lưu bền vững/chia sẻ phù hợp.

## Chạy lại

Xem `docs/README.md` và script `scripts/Start-CourseGuard.ps1`. Bộ `CourseGuard.ApiClientTests` có chế độ `--live-dashboard <file-cookie>` cho kiểm tra giao diện bằng phiên thật. `Connect-LiveSessions.ps1` nhận mật khẩu/OTP tương tác và lưu cookie ở `.local`; `LiveWorkflow.ps1` chạy fixture API và dọn riêng khóa học thử. Không commit hay chia sẻ các cookie này.
