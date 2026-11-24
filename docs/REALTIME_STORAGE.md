# Chat, tệp và lớp học trực tuyến — runbook thành viên 4

Phạm vi: chat khóa học, tệp/ảnh trong chat, signaling lớp học WebRTC và cấu hình triển khai. Giám sát thi (camera, ảnh vi phạm) thuộc thành viên 3; tài liệu/bài nộp thuộc thành viên 2. Tài liệu này mô tả bản API hiện tại (WinForms → CourseGuard.Server → PostgreSQL), không phải kiến trúc Supabase Storage/Realtime trước đây.

## Quyết định cấu hình

| Hạng mục | Bản hiện tại | Ghi chú |
|---|---|---|
| Chat | Client gọi `GET /api/courses/{id}/messages?after=` mỗi 2 giây | Không có push realtime; không dùng Supabase Realtime. |
| Tệp/ảnh chat | Lưu trong cột `messages.file_content` (bytea) qua server | Không cần bucket Supabase Storage, không có URL công khai. Mọi lượt tải đều qua API có kiểm tra quyền khóa học. |
| Signaling lớp học | WebSocket `/classroom-relay?ticket=` trên chính CourseGuard.Server | Vé một lần, hạn 30 giây, do WinForms xin bằng phiên đăng nhập. |
| Media (camera/mic/màn hình) | WebRTC P2P giữa các client | Server không chuyển media. Khác mạng cần STUN, NAT chặt cần TURN. |
| STUN/TURN | Server đọc `WEBRTC_*` trong `.env`, client lấy qua `GET /api/classroom/ice-servers` | Không cần sửa cấu hình từng máy client khi thêm TURN. |
| `CourseGuardRelayApi` | Không dùng trong chế độ API | Giữ để đối chiếu chế độ cũ (`RELAY_WS_URL`). Không cần host, không phát sinh phí. |

Không có thay đổi schema hoặc migration mới trong phần này. Không dùng Supabase Auth; danh tính lấy từ bảng `users` qua cookie phiên của server.

## Interface upload/download (cho thành viên 2/3)

Mọi request ghi phải có header `X-CourseGuard-Client: desktop` (WinForms đã tự gắn qua `ServerApiClient`). Lỗi kiểm tra dữ liệu trả `400` với JSON `{"error":"<thông báo tiếng Việt>"}`; `ServerApiClient` hiển thị nguyên thông báo này thay vì `HTTP 400`.

| Endpoint | Quyền | Giới hạn |
|---|---|---|
| `POST /api/courses/{course}/files` body `{fileName, caption, bytes}` | Giáo viên khóa học hoặc học viên đã duyệt | ≤ 5 MB; chỉ PDF, DOC/DOCX, XLS/XLSX, PPT/PPTX, PNG, JPG/JPEG, TXT, ZIP, RAR. MIME do server suy ra từ đuôi tệp (bỏ qua `mimeType` client gửi). Từ chối tệp PE (`.exe` bị đổi đuôi), ảnh có nội dung không khớp đuôi, tên chứa đường dẫn được cắt về tên tệp. |
| `POST /api/courses/{course}/images` body `{caption, items:[{fileName, mimeType, bytes}]}` | Như trên | 1–5 ảnh PNG/JPEG, tổng ≤ 5 MB, kiểm tra chữ ký tệp. |
| `GET /api/courses/{course}/files/{message}` | Như trên | Trả `null` nếu tin không tồn tại/đã xóa (client coi là không có nội dung). |
| `POST /api/storage/evidence`, `GET /api/monitor/evidence/{id}` | Thành viên 3 sở hữu | Không đổi trong phần này. |
| Tài liệu/bài nộp (`/api/teacher/*Material*`, `/api/student/submissions`, `/documents`) | Thành viên 2 sở hữu | Không đổi trong phần này. Có thể tái dùng `ChatUploadPolicy` làm mẫu kiểm tra loại/kích thước. |

Kestrel giới hạn body 8 MB, đủ cho tệp 5 MB mã hóa base64 trong JSON. Xóa tin nhắn chưa có API; tệp bị gắn với tin nhắn và không có URL công khai để thu hồi.

## Lớp học: hành vi kết nối

- Mỗi người dùng chỉ có một kết nối signaling trong một lớp. Kết nối mới (reconnect hoặc mở cửa sổ thứ hai) thay kết nối cũ; kết nối cũ nhận mã đóng `4000` và không tự kết nối lại, tránh tín hiệu bị chuyển hai lần.
- Khi một người mất kết nối mà không kịp gửi `participant-leave` (tắt app, rớt mạng), server tự báo `participant-leave` cho những người còn lại nên ô video cũ được gỡ.
- Khi giáo viên đóng lớp (`UpdateSessionStatusAsync` hoặc `POST /api/classroom/{id}/status` với `isOpened=false`), server đóng kết nối của học viên với mã `4001`; trang lớp học báo `room-closed`, dọn camera/mic và trạng thái `left`.
- Client tự kết nối lại tối đa 8 lần (1, 2, 4, 8 rồi 10 giây/lần). Sau khi kết nối lại, client gửi lại `participant-join` để đối phương thương lượng lại nếu đã gỡ. Hết lượt thử thì báo lỗi “Mất kết nối máy chủ lớp học”.
- Học viên không được gửi `teacher-*`, `room-full` hoặc tín hiệu `CLOSE`; server bỏ qua. Danh tính, vai trò và phiên của người gửi luôn do server ghi đè.
- Tối đa 32 kết nối mỗi lớp ở server; giao diện WebRTC giới hạn 5 học viên có video.

## Chạy và triển khai

Phát triển: làm theo README gốc (`scripts/Start-CourseGuard.ps1`). Signaling dùng chung cổng với API (`5080` local). Không cần chạy thêm relay.

Thêm TURN cho lớp khác mạng (trong `CourseGuard/CourseGuard.Server/.env`):

```env
WEBRTC_STUN_URLS=stun:stun.l.google.com:19302
WEBRTC_TURN_URLS=turn:turn.example.edu:3478?transport=udp,turns:turn.example.edu:5349
WEBRTC_TURN_USERNAME=...
WEBRTC_TURN_CREDENTIAL=...
```

TURN thiếu username/credential bị bỏ qua để không làm hỏng `RTCPeerConnection`. Nhóm chưa có TURN; nếu cần, tự host coturn trên máy có IP công khai hoặc dùng gói miễn phí có giới hạn. Không tự đăng ký dịch vụ trả phí. Thông tin TURN được gửi tới client đã đăng nhập (trình duyệt cần nó để kết nối), vì vậy dùng tài khoản TURN riêng cho demo.

Máy chủ cho client ở máy khác: cần HTTPS/WSS (client từ chối `http://` ngoài localhost), mở cổng HTTPS của server trên firewall. Với TURN: mở `3478/udp`, `3478/tcp`, `5349/tcp` và dải relay UDP của coturn. Media P2P không đi qua cổng server.

Giới hạn của triển khai hiện tại: vé signaling, danh sách kết nối và OTP nằm trong bộ nhớ một tiến trình. Restart server ngắt mọi lớp (client tự kết nối lại); chạy nhiều instance cần sticky session và kho dùng chung.

## Kiểm thử

Tự động (không cần database):

```powershell
dotnet run --project tests/CourseGuard.ServerSecurityTests
```

Bao gồm kiểm tra tệp/ảnh chat (loại, kích thước, tên tệp, tệp thực thi đổi đuôi), danh sách kết nối lớp học (thay kết nối cũ, không gửi trùng, báo rời phòng đúng lúc, giới hạn phòng, đồng thời), quyền tín hiệu của học viên và cấu hình ICE.

Thủ công trên hai máy — chạy cùng một máy không chứng minh NAT/TURN. Ghi kết quả, máy/mạng và log (đã che dữ liệu nhạy cảm) vào PR:

| # | Kịch bản | Kỳ vọng |
|---|---|---|
| C1 | Giáo viên và học viên gửi tin xen kẽ nhanh | Hai bên thấy đủ, đúng thứ tự, không trùng |
| C2 | Học viên chuyển khóa học khi đang tải | Tin của khóa cũ không hiện trong khóa mới |
| C3 | Gửi PDF 4 MB; gửi tệp 6 MB; gửi `.exe` đổi tên `.pdf` | Tải xuống đúng nội dung; hai trường hợp sau báo lỗi rõ ràng |
| C4 | Gửi nhóm 3 ảnh, tạo/bình chọn/đóng poll | Hiển thị đúng ở máy còn lại trong ~2 giây |
| C5 | Rút mạng máy học viên 10 giây rồi cắm lại | Chat tiếp tục, không nhân đôi tin, unread đúng |
| L1 | Giáo viên mở lớp, học viên vào; bật/tắt camera, mic, chia sẻ màn hình | Hai bên thấy/nghe; trạng thái nút khớp |
| L2 | Rút mạng học viên < 30 giây | Học viên tự vào lại, video phục hồi |
| L3 | Tắt hẳn app học viên (Task Manager) | Ô video học viên biến mất ở máy giáo viên |
| L4 | Học viên mở lớp ở cửa sổ thứ hai | Cửa sổ cũ báo đã vào ở cửa sổ khác, không giật qua lại |
| L5 | Giáo viên đóng lớp | Học viên bị ngắt ngay, camera/mic tắt |
| L6 | Hai máy ở hai mạng khác nhau (ví dụ Wi-Fi + 4G) | Có media khi có TURN; ghi rõ nếu chỉ STUN không kết nối được |

Trạng thái: các bài tự động ở trên đã đạt. Các kịch bản C1–C5, L1–L6 chưa được chạy trên hai máy thật trong lượt thay đổi này.
