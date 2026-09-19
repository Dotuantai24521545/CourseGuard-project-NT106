# Danh sách chức năng hệ thống CourseGuard

Tài liệu này tổng hợp các chức năng chính của hệ thống CourseGuard dựa trên source code hiện tại. Mỗi chức năng được mô tả ngắn gọn kèm input/output ở mức nghiệp vụ, phù hợp để đưa vào báo cáo đồ án.

## 1. Nhóm chức năng xác thực và tài khoản

| STT | Chức năng | Mô tả ngắn | Input | Output |
|---:|---|---|---|---|
| 1 | Đăng nhập | Xác thực tài khoản bằng username và password, sau đó trả về thông tin người dùng nếu hợp lệ. | `username`, `password` | Thông tin `UserModel` nếu đúng; `null` nếu sai hoặc không tồn tại. |
| 2 | Đăng nhập bất đồng bộ | Phiên bản async của chức năng đăng nhập, dùng khi cần tránh treo giao diện. | `username`, `password`, `CancellationToken` | `UserModel` hoặc `null`. |
| 3 | Lấy hồ sơ người dùng | Truy xuất thông tin người dùng theo username. | `username` | Thông tin `UserModel` hoặc `null`. |
| 4 | Ghi nhận thông tin đăng nhập | Lưu thông tin thiết bị và IP khi người dùng đăng nhập. | `userId`, `deviceName`, `ipAddress` | Ghi log thành công hoặc bỏ qua nếu lỗi. |
| 5 | Ghi log hoạt động người dùng | Lưu lại hành động người dùng để phục vụ theo dõi hoạt động hệ thống. | `userId`, `action`, `details`, `ipAddress` | Bản ghi activity log trong database. |
| 6 | Đăng ký tài khoản sinh viên | Tạo yêu cầu đăng ký tài khoản mới với trạng thái chờ duyệt. | `UserModel`, `password` | `true/false`; thông báo lỗi trong `LastErrorMessage` nếu thất bại. |
| 7 | Gửi yêu cầu quên mật khẩu | Người dùng gửi yêu cầu reset mật khẩu bằng username và email. | `username`, `email` | `true/false`; user được chuyển trạng thái `RESET_REQUEST` nếu hợp lệ. |
| 8 | Đổi mật khẩu | Người dùng đổi mật khẩu bằng mật khẩu cũ và mật khẩu mới. | `userId`, `oldPassword`, `newPassword`, `ipAddress` | `true/false`; mật khẩu mới được hash và cập nhật nếu hợp lệ. |
| 9 | Đăng xuất | Xóa phiên đăng nhập hiện tại và ghi log đăng xuất. | `userId`, `username`, `ipAddress` | Session được clear; activity log được ghi nhận. |

## 2. Nhóm chức năng quản trị viên

| STT | Chức năng | Mô tả ngắn | Input | Output |
|---:|---|---|---|---|
| 1 | Tìm kiếm/lọc người dùng | Admin tra cứu danh sách người dùng theo trạng thái và vai trò. | `status`, `role` | Danh sách `UserModel`. |
| 2 | Lấy người dùng theo vai trò | Lọc danh sách tài khoản theo role như Admin, Teacher, Student. | `role` | Danh sách `UserModel`. |
| 3 | Tạo tài khoản bởi Admin | Admin tạo tài khoản mới và gán role/trạng thái. | `UserModel`, `password` | Chuỗi kết quả: `Success`, `Forbidden`, `ValidationError`, `Conflict`, `UnexpectedError`. |
| 4 | Xóa người dùng | Admin xóa tài khoản người dùng nếu được phép và không vi phạm ràng buộc. | `userId` | `true/false`; thông báo lỗi trong `LastErrorMessage` nếu thất bại. |
| 5 | Xem yêu cầu chờ xử lý | Lấy danh sách user có trạng thái `PENDING` hoặc `RESET_REQUEST`. | Không có | Danh sách `UserModel`. |
| 6 | Xem số liệu dashboard Admin | Lấy các thống kê tổng quan phục vụ dashboard quản trị. | Không có | `AdminDashboardMetricsModel`. |
| 7 | Xem hoạt động đăng nhập gần đây | Lấy danh sách hoạt động người dùng gần nhất. | `limit` | Danh sách `RecentUserActivityModel`. |
| 8 | Duyệt yêu cầu người dùng | Admin duyệt đăng ký, duyệt reset mật khẩu hoặc từ chối yêu cầu. | `userId`, `action` (`APPROVE`, `RESET`, `REJECT`) | `true/false`; cập nhật trạng thái user và ghi log. |
| 9 | Duyệt đăng ký tài khoản | Kích hoạt tài khoản đang chờ duyệt. | `userId` | `true/false`; trạng thái user chuyển sang `ACTIVE`. |
| 10 | Đặt lại mật khẩu người dùng | Admin reset mật khẩu cho tài khoản bất kỳ. | `userId`, `newPassword` | `true/false`; mật khẩu được hash và user chuyển sang `ACTIVE`. |
| 11 | Quản lý khóa học cấp Admin | Admin thêm, sửa, xóa khóa học. | `CourseModel` hoặc `courseId` | `Success/Forbidden/Error` hoặc `true/false`. |
| 12 | Duyệt khóa học | Admin duyệt khóa học do giảng viên gửi lên. | `courseId` | `true/false`; khóa học chuyển sang `ACTIVE`, gửi thông báo cho giảng viên. |
| 13 | Từ chối khóa học | Admin từ chối khóa học đang chờ duyệt và ghi lý do. | `courseId`, `reason` | `true/false`; khóa học chuyển sang `REJECTED`, gửi thông báo cho giảng viên. |
| 14 | Ghi danh sinh viên bởi Admin | Admin thêm sinh viên vào khóa học. | `courseId`, `studentId` | `true/false`; enrollment được tạo/cập nhật. |

## 3. Nhóm chức năng khóa học và ghi danh

| STT | Chức năng | Mô tả ngắn | Input | Output |
|---:|---|---|---|---|
| 1 | Lấy danh sách tất cả khóa học | Truy xuất danh sách khóa học trong hệ thống. | Không có | Danh sách `CourseModel`. |
| 2 | Lấy khóa học sinh viên đã ghi danh | Lấy danh sách ID khóa học mà sinh viên đã tham gia. | `studentId` | `HashSet<int>` chứa các `courseId`. |
| 3 | Sinh viên tham gia khóa học | Sinh viên gửi yêu cầu hoặc tham gia khóa học theo luồng ghi danh. | `courseId`, `studentId` | `true/false`; ghi log nếu thành công. |
| 4 | Lấy yêu cầu ghi danh đang chờ | Admin/Giảng viên xem danh sách enrollment đang chờ duyệt. | `courseId` tùy chọn | Danh sách `EnrollmentModel`. |
| 5 | Lọc enrollment theo trạng thái | Xem danh sách ghi danh theo trạng thái cụ thể. | `courseId`, `status` | Danh sách `EnrollmentModel`. |
| 6 | Duyệt ghi danh | Admin/Giảng viên duyệt sinh viên vào khóa học. | `courseId`, `studentId` | `true/false`. |
| 7 | Từ chối ghi danh | Admin/Giảng viên từ chối yêu cầu tham gia khóa học. | `courseId`, `studentId` | `true/false`. |
| 8 | Lấy khóa học có thể đăng ký | Sinh viên xem danh sách khóa học `ACTIVE` chưa đăng ký. | `studentId` | Danh sách `CourseModel`. |
| 9 | Kiểm tra trạng thái ghi danh | Kiểm tra sinh viên đang ở trạng thái nào trong một khóa học. | `courseId`, `studentId` | Chuỗi trạng thái hoặc `null`. |
| 10 | Lấy danh sách ghi danh của sinh viên | Hiển thị các khóa học sinh viên đã gửi yêu cầu/tham gia/rút. | `studentId` | Danh sách `EnrollmentModel`. |
| 11 | Gửi yêu cầu tham gia khóa học | Sinh viên gửi yêu cầu tham gia khóa học đang mở. | `courseId`, `studentId` | Chuỗi thông báo kết quả nghiệp vụ. |
| 12 | Rút/hủy đăng ký khóa học | Sinh viên hủy yêu cầu hoặc rút khỏi khóa học. | `courseId`, `studentId` | Chuỗi thông báo kết quả nghiệp vụ. |
| 13 | Đếm số sinh viên trong khóa học | Lấy số lượng sinh viên đã ghi danh vào khóa học. | `courseId` | Số nguyên số lượng sinh viên. |
| 14 | Lấy lịch học online của sinh viên | Lấy danh sách buổi học/lịch học online của sinh viên. | `studentId` | Danh sách `StudentScheduleItemModel`. |

## 4. Nhóm chức năng giảng viên

| STT | Chức năng | Mô tả ngắn | Input | Output |
|---:|---|---|---|---|
| 1 | Xem tổng quan dashboard giảng viên | Lấy số liệu tổng hợp cho màn hình giảng viên. | `teacherId` | `TeacherDashboardSummaryModel`. |
| 2 | Xem hồ sơ giảng viên | Lấy thông tin cá nhân/hồ sơ giảng viên. | `teacherId` | `TeacherProfileModel` hoặc `null`. |
| 3 | Cập nhật hồ sơ giảng viên | Thêm mới hoặc cập nhật hồ sơ giảng viên. | `teacherId`, `TeacherProfileModel` | `true/false`. |
| 4 | Xem khóa học của giảng viên | Lấy danh sách khóa học thuộc giảng viên. | `teacherId` | Danh sách `TeacherCourseModel`. |
| 5 | Tạo khóa học | Giảng viên tạo khóa học mới. | `teacherId`, `TeacherCourseModel` | ID khóa học mới hoặc `0` nếu thất bại. |
| 6 | Cập nhật khóa học | Giảng viên cập nhật thông tin khóa học của mình. | `teacherId`, `TeacherCourseModel` | `true/false`. |
| 7 | Gửi khóa học chờ duyệt | Giảng viên gửi khóa học lên Admin để duyệt. | `teacherId`, `courseId` | `true/false`. |
| 8 | Xóa khóa học | Giảng viên xóa khóa học thuộc quyền quản lý. | `teacherId`, `courseId` | `true/false`. |
| 9 | Xem danh sách bài học | Lấy các bài học của giảng viên/khóa học. | `teacherId` | Danh sách `TeacherLessonModel`. |
| 10 | Tạo bài học | Thêm bài học mới cho khóa học. | `teacherId`, `TeacherLessonModel` | ID bài học mới hoặc `0`. |
| 11 | Cập nhật bài học | Sửa nội dung bài học. | `teacherId`, `TeacherLessonModel` | `true/false`. |
| 12 | Xóa bài học | Xóa bài học khỏi hệ thống. | `teacherId`, `lessonId` | `true/false`. |
| 13 | Xem bài tập | Lấy danh sách bài tập do giảng viên quản lý. | `teacherId` | Danh sách `TeacherAssignmentModel`. |
| 14 | Tạo bài tập | Tạo bài tập mới. | `teacherId`, `TeacherAssignmentModel` | ID bài tập mới hoặc `0`. |
| 15 | Cập nhật bài tập | Sửa thông tin bài tập. | `teacherId`, `TeacherAssignmentModel` | `true/false`. |
| 16 | Xóa bài tập | Xóa bài tập khỏi khóa học. | `teacherId`, `assignmentId` | `true/false`. |
| 17 | Xem sinh viên chờ duyệt | Xem danh sách sinh viên gửi yêu cầu tham gia khóa học. | `teacherId` | Danh sách `TeacherStudentModel`. |
| 18 | Xem sinh viên đã ghi danh | Lấy danh sách sinh viên trong các khóa học của giảng viên. | `teacherId`, `courseId` tùy chọn | Danh sách `TeacherStudentModel`. |
| 19 | Duyệt sinh viên vào khóa học | Giảng viên duyệt yêu cầu ghi danh. | `teacherId`, `courseId`, `studentId` | `true/false`. |
| 20 | Từ chối sinh viên vào khóa học | Giảng viên từ chối yêu cầu ghi danh. | `teacherId`, `courseId`, `studentId` | `true/false`. |
| 21 | Xem lịch dạy | Lấy lịch dạy của giảng viên. | `teacherId` | Danh sách `TeacherScheduleItemModel`. |
| 22 | Xem nhiệm vụ giảng dạy | Lấy danh sách công việc/nhiệm vụ giảng dạy. | `teacherId` | Danh sách `TeacherTeachingTaskModel`. |
| 23 | Tạo lịch dạy | Thêm lịch học/lịch dạy mới. | `teacherId`, `TeacherScheduleItemModel` | ID lịch mới hoặc `0`. |
| 24 | Cập nhật lịch dạy | Sửa thông tin lịch dạy. | `teacherId`, `TeacherScheduleItemModel` | `true/false`. |
| 25 | Xóa lịch dạy | Xóa lịch học/lịch dạy. | `teacherId`, `scheduleId` | `true/false`. |

## 5. Nhóm chức năng bài thi và kết quả

| STT | Chức năng | Mô tả ngắn | Input | Output |
|---:|---|---|---|---|
| 1 | Xem danh sách bài thi | Giảng viên xem các bài thi đã tạo. | `teacherId` | Danh sách `TeacherExamModel`. |
| 2 | Tạo bài thi | Giảng viên tạo bài thi mới cho khóa học. | `teacherId`, `TeacherExamModel` | ID bài thi mới hoặc `0`. |
| 3 | Cập nhật bài thi | Sửa thông tin bài thi. | `teacherId`, `TeacherExamModel` | `true/false`. |
| 4 | Kiểm tra điều kiện kích hoạt bài thi | Kiểm tra bài thi có thể chuyển sang trạng thái hoạt động hay chưa. | `teacherId`, `examId` | `true/false`. |
| 5 | Xóa bài thi | Xóa bài thi thuộc quyền quản lý của giảng viên. | `teacherId`, `examId` | `true/false`. |
| 6 | Xem câu hỏi bài thi | Lấy danh sách câu hỏi của một bài thi. | `teacherId`, `examId` | Danh sách `TeacherExamQuestionModel`. |
| 7 | Lấy trạng thái bài thi | Kiểm tra trạng thái hiện tại của bài thi. | `teacherId`, `examId` | Chuỗi trạng thái, mặc định `DRAFT` nếu không hợp lệ. |
| 8 | Tạo câu hỏi thi | Thêm câu hỏi vào bài thi. | `teacherId`, `TeacherExamQuestionModel` | ID câu hỏi mới hoặc `0`. |
| 9 | Cập nhật câu hỏi thi | Sửa nội dung/cấu hình câu hỏi. | `teacherId`, `TeacherExamQuestionModel` | `true/false`. |
| 10 | Xóa câu hỏi thi | Xóa câu hỏi khỏi bài thi. | `teacherId`, `examId`, `questionId` | `true/false`. |
| 11 | Xem kết quả học tập | Giảng viên xem điểm/kết quả của sinh viên, có thể lọc theo khóa học hoặc bài thi. | `teacherId`, `courseId` tùy chọn, `examId` tùy chọn | Danh sách `TeacherScoreModel`. |
| 12 | Cập nhật điểm | Giảng viên cập nhật điểm/kết quả cho sinh viên. | `teacherId`, `TeacherScoreModel` | `true/false`. |
| 13 | Xem phiên thi đang hoạt động | Giảng viên theo dõi các phiên thi đang diễn ra. | `teacherId` | Danh sách `TeacherActiveExamSessionModel`. |
| 14 | Chấm điểm bài thi | Service hỗ trợ tính điểm bài thi dựa trên câu trả lời. | Dữ liệu bài làm/câu trả lời | Điểm số/kết quả bài thi. |
| 15 | Kiểm tra quyền làm bài thi | Kiểm tra sinh viên có đủ điều kiện làm bài thi hay không. | `studentId`, `examId` hoặc dữ liệu tương ứng | Kết quả hợp lệ/không hợp lệ để cho phép vào thi. |

## 6. Nhóm chức năng tài liệu học tập

| STT | Chức năng | Mô tả ngắn | Input | Output |
|---:|---|---|---|---|
| 1 | Xem tài liệu | Giảng viên xem danh sách tài liệu theo khóa học hoặc toàn bộ tài liệu của mình. | `teacherId`, `courseId` tùy chọn | Danh sách `TeacherMaterialModel`. |
| 2 | Tạo tài liệu | Thêm tài liệu học tập cho khóa học. | `teacherId`, `TeacherMaterialModel` | ID tài liệu mới hoặc `0`. |
| 3 | Cập nhật tài liệu | Sửa thông tin tài liệu. | `teacherId`, `TeacherMaterialModel` | `true/false`. |
| 4 | Xóa tài liệu | Xóa tài liệu khỏi hệ thống. | `teacherId`, `materialId` | `true/false`. |
| 5 | Kiểm tra file tài liệu | Kiểm tra quy định file trước khi lưu/tải lên. | Đường dẫn file, metadata file | Hợp lệ/không hợp lệ kèm thông báo lỗi. |

## 7. Nhóm chức năng chat/lớp học

| STT | Chức năng | Mô tả ngắn | Input | Output |
|---:|---|---|---|---|
| 1 | Lấy danh sách khóa học có chat | Lấy các khóa học mà người dùng có quyền truy cập phòng chat. | `userId` | Danh sách `ChatCourseModel`. |
| 2 | Lấy tin nhắn | Lấy các tin nhắn trong phòng chat khóa học nếu người dùng có quyền truy cập. | `userId`, `courseId`, `limit` | Danh sách `ChatMessageModel`. |
| 3 | Gửi tin nhắn văn bản | Người dùng gửi tin nhắn vào phòng chat của khóa học. | `userId`, `courseId`, `content` | `true/false`; ghi log nếu gửi thành công. |
| 4 | Gửi file trong chat | Người dùng gửi file vào phòng chat, hệ thống kiểm tra quyền, định dạng và dung lượng file. | `userId`, `courseId`, `sourceFilePath`, `caption` | `true/false`; file được lưu và message được tạo nếu hợp lệ. |
| 5 | Kiểm tra file chat | Kiểm tra file có tồn tại, không rỗng, không quá 20MB và thuộc định dạng hỗ trợ. | `sourceFilePath` | Hợp lệ/không hợp lệ kèm lỗi. |

## 8. Nhóm chức năng dashboard và thống kê

| STT | Chức năng | Mô tả ngắn | Input | Output |
|---:|---|---|---|---|
| 1 | Thống kê tần suất đăng nhập | Lấy số liệu đăng nhập theo số ngày gần nhất. | `days` | Danh sách `LoginFrequencyModel`. |
| 2 | Thống kê danh sách khóa học | Lấy dữ liệu thống kê/tổng hợp danh sách khóa học. | `limit` | Danh sách `CourseListItemModel`. |
| 3 | Thống kê tài khoản | Lấy số liệu tổng hợp về tài khoản trong hệ thống. | Không có | `AccountSummaryModel`. |
| 4 | Dashboard Admin | Lấy các số liệu tổng quan phục vụ giao diện quản trị. | Không có | `AdminDashboardMetricsModel`. |
| 5 | Dashboard Teacher | Lấy các số liệu tổng quan phục vụ giao diện giảng viên. | `teacherId` | `TeacherDashboardSummaryModel`. |

## 9. Nhóm chức năng thông báo và email

| STT | Chức năng | Mô tả ngắn | Input | Output |
|---:|---|---|---|---|
| 1 | Tạo thông báo khi duyệt/từ chối khóa học | Gửi thông báo cho giảng viên khi Admin xử lý khóa học. | `teacherId`, tiêu đề, nội dung, loại thông báo, `courseId` | Bản ghi thông báo trong database. |
| 2 | Tạo thông báo yêu cầu ghi danh | Gửi thông báo cho giảng viên khi sinh viên yêu cầu tham gia khóa học. | `teacherId`, `studentId`, `courseId` | Bản ghi thông báo trong database. |
| 3 | Gửi email reset mật khẩu | Gửi mật khẩu tạm thời đến email người dùng khi Admin duyệt reset. | Email người nhận, subject, nội dung | `true/false`; lỗi trả về qua `errorMessage`. |
| 4 | Ghi log sự kiện hệ thống | Lưu lại các hành động như đăng ký, duyệt user, reset mật khẩu, chat, ghi danh. | `userId`, `action`, `details`, `ipAddress` | Bản ghi log trong database. |

## 10. Nhóm chức năng giám sát phiên thi

| STT | Chức năng | Mô tả ngắn | Input | Output |
|---:|---|---|---|---|
| 1 | Nhận stream màn hình sinh viên | Dịch vụ TCP nhận frame màn hình từ máy sinh viên trong phiên thi. | `examId`, `studentId`, `attemptId`, frame data | Sự kiện `FrameReceived` chứa dữ liệu ảnh màn hình. |
| 2 | Gửi stream màn hình sinh viên | Client phía sinh viên gửi frame màn hình đến service giám sát. | Thông tin phiên thi, frame màn hình | Frame được gửi qua TCP đến máy giám sát. |
| 3 | Cập nhật trạng thái giám sát | Thông báo trạng thái kết nối như đang kết nối, đang nhận hình, mất kết nối. | Trạng thái kết nối | Sự kiện `StatusChanged`. |
| 4 | Theo dõi phiên thi đang hoạt động | Giảng viên xem các phiên thi hiện đang diễn ra. | `teacherId` | Danh sách `TeacherActiveExamSessionModel`. |

## 11. Tóm tắt theo vai trò

### Admin

- Quản lý người dùng.
- Duyệt đăng ký và yêu cầu reset mật khẩu.
- Xem dashboard/thống kê hệ thống.
- Quản lý và duyệt khóa học.
- Duyệt/từ chối ghi danh sinh viên.

### Giảng viên

- Quản lý hồ sơ cá nhân.
- Quản lý khóa học, bài học, bài tập, tài liệu.
- Quản lý bài thi và câu hỏi thi.
- Quản lý sinh viên tham gia khóa học.
- Xem/cập nhật điểm và theo dõi phiên thi.
- Sử dụng chat/lớp học theo khóa học.

### Sinh viên

- Đăng ký, đăng nhập, đổi mật khẩu, quên mật khẩu.
- Xem và tham gia khóa học.
- Theo dõi lịch học, thông báo và lớp học online.
- Làm bài thi, xem kết quả.
- Sử dụng chat/lớp học theo khóa học.

## 12. Đặc tả Use Case

### 12.1. Use Case: Upload APK

| Thành phần | Nội dung |
| --- | --- |
| Tên use case | Upload APK |
| Actor | User |
| Mục tiêu | Cho phép người dùng tải lên file APK để hệ thống chuẩn bị phân tích bảo mật. |
| Tiền điều kiện | Người dùng đã đăng nhập vào hệ thống. |
| Hậu điều kiện | File APK được lưu tạm hoặc lưu vào hệ thống để phục vụ quá trình scan malware. |
| Luồng chính | 1. User chọn chức năng upload APK.<br>2. Hệ thống hiển thị giao diện chọn file.<br>3. User chọn file có định dạng `.apk`.<br>4. Hệ thống kiểm tra định dạng và kích thước file.<br>5. Hệ thống upload file lên server.<br>6. Hệ thống thông báo upload thành công. |
| Luồng thay thế | 1. Nếu file không đúng định dạng `.apk`, hệ thống hiển thị thông báo lỗi.<br>2. Nếu file vượt quá dung lượng cho phép, hệ thống từ chối upload.<br>3. Nếu quá trình upload thất bại, hệ thống yêu cầu user thử lại. |
| Input | File APK, thông tin user, thời gian upload. |
| Output | File APK được lưu thành công; mã định danh file hoặc thông báo lỗi. |

### 12.2. Use Case: Scan Malware

| Thành phần | Nội dung |
| --- | --- |
| Tên use case | Scan Malware |
| Actor | User, System |
| Mục tiêu | Phân tích file APK nhằm phát hiện mã độc, quyền nguy hiểm hoặc hành vi bất thường. |
| Tiền điều kiện | User đã upload file APK hợp lệ lên hệ thống. |
| Hậu điều kiện | Hệ thống tạo kết quả phân tích malware cho file APK. |
| Luồng chính | 1. User chọn file APK đã upload để tiến hành scan.<br>2. Hệ thống kiểm tra file APK có tồn tại và hợp lệ không.<br>3. Hệ thống giải nén hoặc đọc cấu trúc file APK.<br>4. Hệ thống phân tích manifest, permission, package, signature và mã nguồn/dex nếu có.<br>5. Hệ thống đối chiếu với luật phát hiện malware hoặc model phân tích.<br>6. Hệ thống tính toán mức độ rủi ro.<br>7. Hệ thống lưu kết quả scan vào database. |
| Luồng thay thế | 1. Nếu file APK không tồn tại, hệ thống thông báo lỗi.<br>2. Nếu file APK bị lỗi hoặc không thể phân tích, hệ thống ghi nhận trạng thái scan thất bại.<br>3. Nếu quá trình scan bị gián đoạn, hệ thống cho phép scan lại. |
| Input | File APK, rule phát hiện malware, cấu hình phân tích. |
| Output | Kết quả scan gồm trạng thái an toàn/nguy hiểm, danh sách rủi ro, permission nguy hiểm, điểm đánh giá bảo mật. |

### 12.3. Use Case: View Result

| Thành phần | Nội dung |
| --- | --- |
| Tên use case | View Result |
| Actor | User |
| Mục tiêu | Cho phép người dùng xem kết quả phân tích bảo mật của file APK. |
| Tiền điều kiện | File APK đã được scan và có kết quả phân tích. |
| Hậu điều kiện | User xem được thông tin chi tiết về kết quả scan malware. |
| Luồng chính | 1. User truy cập danh sách file hoặc lịch sử scan.<br>2. User chọn một kết quả scan cần xem.<br>3. Hệ thống truy xuất dữ liệu phân tích từ database.<br>4. Hệ thống hiển thị tổng quan kết quả scan.<br>5. Hệ thống hiển thị chi tiết các rủi ro, quyền nguy hiểm, cảnh báo và mức độ đe dọa. |
| Luồng thay thế | 1. Nếu kết quả scan chưa hoàn tất, hệ thống hiển thị trạng thái đang xử lý.<br>2. Nếu kết quả không tồn tại, hệ thống thông báo không tìm thấy dữ liệu.<br>3. Nếu user không có quyền xem kết quả, hệ thống từ chối truy cập. |
| Input | ID file APK hoặc ID kết quả scan, thông tin user. |
| Output | Giao diện hiển thị kết quả scan malware và chi tiết phân tích. |

### 12.4. Use Case: Generate Report

| Thành phần | Nội dung |
| --- | --- |
| Tên use case | Generate Report |
| Actor | User |
| Mục tiêu | Tạo báo cáo tổng hợp kết quả phân tích malware của file APK. |
| Tiền điều kiện | File APK đã được scan và có kết quả phân tích hợp lệ. |
| Hậu điều kiện | Báo cáo được tạo và có thể xem, tải xuống hoặc lưu trữ. |
| Luồng chính | 1. User mở kết quả scan của một file APK.<br>2. User chọn chức năng tạo báo cáo.<br>3. Hệ thống tổng hợp thông tin file APK, kết quả scan, danh sách cảnh báo và mức độ rủi ro.<br>4. Hệ thống định dạng báo cáo theo mẫu có sẵn.<br>5. Hệ thống tạo file báo cáo.<br>6. User xem hoặc tải báo cáo về máy. |
| Luồng thay thế | 1. Nếu chưa có kết quả scan, hệ thống yêu cầu scan trước khi tạo báo cáo.<br>2. Nếu quá trình tạo báo cáo lỗi, hệ thống hiển thị thông báo thất bại.<br>3. Nếu dữ liệu phân tích không đầy đủ, hệ thống tạo báo cáo với phần cảnh báo thiếu dữ liệu. |
| Input | ID kết quả scan, thông tin file APK, dữ liệu phân tích malware. |
| Output | Báo cáo kết quả phân tích, có thể ở dạng PDF, HTML, Markdown hoặc file tải xuống. |
