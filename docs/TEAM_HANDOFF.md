# Phân công hoàn thiện CourseGuard — 4 thành viên + trưởng nhóm

## Baseline và giới hạn đã xác minh

- Build thành công, 0 warning/error ở lần kiểm tra gần nhất.
- 249 feature test offline đạt; 3 ca phụ thuộc cấu hình/dịch vụ được bỏ qua có báo cáo.
- 6 kiểm thử chấm điểm/cache đã đạt; 4 yêu cầu AI thật đã đạt.
- Supabase dev mới đã được khởi tạo bằng schema và 16 migration; 3 ca database đạt.
- Luồng backend mở bài, lưu, resume, nộp, đọc điểm, chặn sửa sau nộp và giới hạn lượt thi đã đạt với fixture riêng và được dọn.
- Xác thực mật khẩu admin/student đã kiểm tra bằng hàm đồng bộ. Chưa xác nhận đăng nhập giao diện + OTP/email, đầy đủ các màn hình, upload/storage hoặc realtime trên hai máy.

Đây là phạm vi hoàn thiện bản dev/demo để nhóm nghiệm thu, không phải xác nhận sẵn sàng production. Các nhiệm vụ dưới đây gồm kiểm thử chức năng đã có và sửa lỗi tìm được; không mặc định tất cả chức năng đang hỏng.

## Trước khi chia branch — trưởng nhóm

1. Review diff hiện có, kiểm tra file đưa vào commit; không dùng `git add .` khi chưa xem danh sách. Không commit `.env`, log, bin/obj hoặc ảnh chứa thông tin đăng nhập.
2. API key AI từng nằm trong source: thay key và rà secrets trong lịch sử trước khi công khai/chia sẻ repository. Chỉ xóa key khỏi file hiện tại không xóa lịch sử Git.
3. Xác nhận repo đích thuộc nhóm. Remote hiện tại tên `upstream` trỏ repo gốc, không mặc định push vào đó. Thêm `origin` cho repo nhóm khi có URL và quyền.
4. Commit/push một baseline chung sau kiểm tra; cả bốn người tạo branch từ cùng baseline đó.
5. Chia sẻ cấu hình dev riêng, cung cấp SMTP và tài khoản nhận email test thật. Tài khoản seed có email `.local`, không nhận OTP Internet.
6. Mọi người dùng database dev riêng là tốt nhất. Nếu chung database: đặt tiền tố riêng cho fixture, chỉ xóa fixture mình tạo; cấm chạy reset schema. Chỉ trưởng nhóm áp dụng migration vào database chung.

## Thành viên 1 — tài khoản, OTP, quản trị

Branch: `feat/auth-admin`

Sở hữu: `Backend/Controllers/AuthController.cs`, `UserController.cs`, `SettingsController.cs`; dịch vụ SMTP/email queue; OTP/session/password; `Frontend/Forms/Login`, `Forms/Admin`, `UserControls/Admin`. Thay đổi `Frontend/Program.cs` phải phối hợp trưởng nhóm.

Công việc:

- Cấu hình SMTP qua môi trường, cập nhật `.env.example` bằng tên biến và placeholder; không nhúng secret.
- Chạy đăng nhập với email test thật cho admin/teacher/student. Kiểm tra OTP đúng/sai/hết hạn/dùng lại/gửi lại, giới hạn thử và khóa tài khoản.
- Xử lý lỗi cấu hình SMTP và lỗi gửi: hiện `EmailQueueService` tạo SMTP trong static initialization, đồng thời chưa sử dụng kết quả gửi thất bại. UI phải báo đúng, có đường thử lại và không xác nhận gửi thành công giả.
- Kiểm tra đăng ký, admin duyệt/từ chối, quên mật khẩu, mật khẩu tạm, bắt đổi mật khẩu, logout và phân quyền truy cập dữ liệu/hành động.
- Kiểm thử dashboard, quản lý user/course/device, báo cáo, audit/settings; sửa lỗi truy vấn, lọc, trạng thái rỗng và lỗi tải được phát hiện. Phần course approval thuộc người 1; CRUD nội dung course thuộc người 2.

Nghiệm thu: video/log đã che dữ liệu nhạy cảm của đăng nhập OTP thật theo ba vai trò; reset và đổi mật khẩu chạy; SMTP lỗi không làm ứng dụng crash; hành động trái quyền bị chặn ở backend. Không chỉ ẩn nút trên UI. Test mới có cả thất bại và thành công.

## Thành viên 2 — khóa học, nội dung học, bài tập, AI

Branch: `feat/learning-workflows`

Sở hữu: `CourseController`, phần nghiệp vụ tương ứng của `TeacherController`/`TeacherRepository`; các màn course/lesson/material/assignment/student list/schedule/result bài tập; `AiQuestionGeneratorService`, `AiQuestionGeneratorDialog`, export/calendar/gamification. Không sửa phần thi trắc nghiệm hoặc transport upload.

Công việc:

- Đi trọn luồng giáo viên tạo/sửa course → admin duyệt (phối hợp người 1) → học viên đăng ký/được duyệt → thấy đúng nội dung course.
- Kiểm thử bài học, tài liệu, bài tập: tạo/sửa/xóa theo quyền, publish/draft, deadline, nộp lại nếu được phép, giáo viên xem/chấm và học viên xem kết quả. Upload gọi API thống nhất của người 4.
- Test lịch học, trùng lịch, múi giờ, điểm danh/tổng hợp; phần sự kiện join/leave lớp học thuộc người 4. Bảng xếp hạng phải dùng dữ liệu thật đúng course; không trả dữ liệu demo khi truy vấn lỗi.
- Test AI qua UI: sinh câu hỏi → xem/chỉnh sửa → nhập đề phối hợp người 3; timeout, hết quota, JSON sai và câu hỏi thiếu dữ liệu phải báo rõ. Không tự động công bố điểm AI.
- Xác định tutor/flashcard/chấm tự luận đã có đường truy cập UI hay mới có service. Chức năng ngoài phạm vi demo cần ghi rõ hoãn, không gắn nút giả.
- Kiểm thử tìm kiếm, lọc course, export và các màn rỗng/lỗi; không sửa lại toàn bộ thiết kế UI.

Nghiệm thu: hai tài khoản teacher/student đi được luồng học và bài tập, dữ liệu đúng sau mở lại ứng dụng, không truy cập course của người khác; có bằng chứng xử lý lỗi mạng/upload/AI. Mỗi chức năng chưa xong phải được ghi rõ thay vì dùng dữ liệu mẫu che lỗi.

## Thành viên 3 — bài thi, lưu offline, chống gian lận và giám sát

Branch: `feat/exam-reliability`

Sở hữu: `DoExamForm`, `UC_TakeExam`, `StudentExamReviewForm`, màn soạn đề/ngân hàng câu hỏi; `UC_TeacherExams`, `UC_TeacherResults`, `UC_ExamMonitor`, `MonitorPopupForm`, `MonitorAllForm`; scoring, exam availability, `LocalAnswerCache`, keyboard hook/anti-cheat, `Services/Monitoring`.

Công việc:

- Kiểm thử UI soạn đề → gán câu hỏi/điểm → mở thi → student làm/nộp → teacher/student xem đúng điểm và review theo quyền.
- Test biên giờ mở/đóng, hết giờ, hết lượt, câu bỏ trống, sửa đáp án, nộp liên tiếp, câu/attempt không thuộc user; kết quả không bị ghi đè ngoài trạng thái cho phép.
- Tắt mạng khi lưu/nộp, đóng/mở lại app, kết nối lại: đáp án phải được khôi phục và đồng bộ; không báo đã lưu khi chưa lưu. Cache hiện giữ định dạng mã hóa cũ, bắt lỗi kiểu best-effort và chỉ khóa trong một process: đánh giá các giới hạn này, không tuyên bố đã giải quyết hoàn toàn mất dữ liệu.
- Giám sát trên hai máy: ảnh màn hình, ngắt/kết nối lại, cảnh báo/vi phạm, đóng màn hình giám sát; dọn bitmap/socket/timer/hook khi thoát. Ảnh vi phạm lưu qua dịch vụ người 4.
- Chốt với trưởng nhóm chính sách chống gian lận. Những kiểm tra đang tắt không được tự bật gây khóa máy/tự nộp ngoài yêu cầu; hành vi và giới hạn phải có tài liệu.

Nghiệm thu: video hai máy và ma trận mất mạng/resume/hết giờ; điểm không đổi sau nộp; không mất đáp án trong các tình huống được hỗ trợ; không ghi vi phạm sai khi mở dialog hợp lệ. Có regression test cho lỗi đã sửa.

## Thành viên 4 — chat, lớp học trực tuyến, storage và triển khai relay

Branch: `feat/realtime-storage`

Sở hữu: `Services/Realtime`, `Services/Classroom`, `Services/Storage`, chat/file service; màn native classroom, `Frontend/WebRTC`, `UserControls/Classroom`, `UserControls/Shared/Chat`, `CourseChatPanel`, teacher/student chat; `CourseGuardRelayApi`. Transport giám sát thi thuộc người 3.

Công việc:

- Rà toàn bộ URL/key/backend relay và cấu hình Supabase cho project dev mới; chuyển cấu hình cần thiết sang môi trường. Thiết lập bucket/policy đúng nhu cầu và ghi lại migration/setup.
- Chốt interface upload/download với người 2/3 trước khi họ tích hợp. Kiểm tra ảnh chat/avatar/tài liệu/ảnh vi phạm, kích thước/loại file, URL lỗi, xóa tài nguyên và phân quyền. Không giải quyết lỗi quyền bằng cách công khai toàn bộ bucket.
- Test chat hai tài khoản: gửi/nhận, thứ tự, tin trùng, unread/read, chuyển course khi tải chậm, ảnh/file, poll; reconnect không nhân đôi subscription.
- Test lớp học hai máy: giáo viên mở/đóng phòng, học viên join/leave, chat, camera/mic/share screen, điểm danh; xác nhận hoạt động khác mạng nếu đó là phạm vi demo. Chạy cùng máy không chứng minh relay/NAT hoạt động.
- Ghi rõ relay được host ở đâu, cách chạy, cấu hình host/port/firewall và giới hạn; không tự phát sinh dịch vụ trả phí. Không trộn Auth Supabase với bảng users của app mà không thống nhất thiết kế.

Nghiệm thu: hai máy gửi chat/file, vào/ra lớp và reconnect; upload dùng project mới với quyền đúng; không leak subscription/socket khi đóng; có runbook để máy mới chạy được.

## Quy tắc để bốn nhánh không đụng nhau

- `CourseGuardDbContext.cs`, `TeacherController.cs`, `TeacherRepository.cs`, `Program.cs`, `.csproj`, `.env.example` và test runner là file dùng chung: báo trước tên method/phần cần sửa; PR nhỏ; trưởng nhóm điều phối merge. Không cùng format/refactor toàn file.
- Mỗi người tạo file test riêng theo feature; hạn chế chỉnh `tests/CourseGuard.FeatureTests/Program.cs`. Tách PR refactor khỏi sửa bug.
- Migration mới dùng tên timestamp + feature, không sửa migration đã chạy để thay schema database chung; ghi rõ cách nâng cấp và dữ liệu tác động. Không chạy lại initializer trên database đã có bảng.
- Mỗi PR ghi: lỗi cụ thể, cách tái hiện, hành vi sau sửa, lệnh test/kết quả, cấu hình/migration cần thêm và giới hạn còn lại. Không tự merge vào main.
- Người 1 chốt đăng nhập/email, người 4 chốt upload/realtime interface sớm. Người 2/3 có thể dùng backend fixture trong khi chờ hai phần đó.

## Trưởng nhóm — tích hợp, CI và nghiệm thu cuối

- Tạo CI Windows chạy build + test offline + exam tests trên PR; chưa có workflow `.github` trong bản repo được rà. Test mạng/DB/AI chạy riêng với secret cấu hình, không chạy trên mọi PR từ nguồn không tin cậy.
- Merge PR nhỏ đã xanh; cập nhật baseline, yêu cầu các nhánh đồng bộ trước merge. Sau thay đổi interface/migration, chạy lại các feature chịu tác động.
- Chạy trên máy clone mới: cấu hình theo README, restore/build, đăng nhập qua OTP thật, luồng học/bài tập, luồng thi, chat/lớp học hai máy, upload/download, logout và mở lại.
- Không xem tổng số test xanh là đủ: phải có bằng chứng thao tác UI và dữ liệu persisted, cùng các trường hợp lỗi quan trọng.
- Trước phát hành dev/demo: không còn lỗi chặn luồng chính, không có secret được commit, setup tái lập được, migration có thứ tự, known issues ghi rõ, mỗi nhánh được review và toàn bộ kiểm tra chung đạt.

Lệnh kiểm tra chung (chạy từ gốc repo):

```powershell
dotnet build CourseGuard/CourseGuard.slnx
dotnet run --project tests/CourseGuard.FeatureTests/CourseGuard.FeatureTests.csproj -- --offline
dotnet run --project tests/CourseGuard.ExamTests/CourseGuard.ExamTests.csproj
dotnet run --project tests/CourseGuard.FeatureTests/CourseGuard.FeatureTests.csproj -- --integration --database-only
```

Kiểm thử tích hợp đầy đủ, khởi tạo DB trống và fixture bài thi: xem `tests/CourseGuard.FeatureTests/README.md`. Chỉ dùng lệnh có ghi dữ liệu trên database dev đã xác nhận đúng project.
