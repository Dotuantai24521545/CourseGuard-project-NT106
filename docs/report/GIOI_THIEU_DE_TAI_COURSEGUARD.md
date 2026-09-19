# Giới thiệu đề tài CourseGuard

## 1. Bối cảnh nghiên cứu

Trong những năm gần đây, chuyển đổi số trong giáo dục đã trở thành xu hướng tất yếu tại các trường đại học, trung tâm đào tạo và tổ chức giáo dục. Các hoạt động dạy học truyền thống dần được kết hợp với nền tảng số nhằm hỗ trợ quản lý khóa học, phân phối tài liệu, tổ chức kiểm tra, theo dõi tiến độ học tập và đánh giá kết quả của người học. Đặc biệt sau giai đoạn học trực tuyến phát triển mạnh, nhu cầu xây dựng các hệ thống quản lý học tập có khả năng vận hành ổn định, phân quyền rõ ràng và hỗ trợ thi trực tuyến ngày càng trở nên cấp thiết.

Tuy nhiên, việc triển khai một hệ thống học tập và thi trực tuyến không chỉ dừng lại ở chức năng tạo khóa học hay làm bài kiểm tra. Hệ thống còn phải giải quyết nhiều vấn đề thực tế như quản lý tài khoản theo vai trò, kiểm soát trạng thái người dùng, tổ chức lớp học, quản lý tài liệu học tập, theo dõi lịch học, ghi nhận điểm số, hỗ trợ thông báo và bảo đảm tính minh bạch trong quá trình kiểm tra. Bên cạnh đó, các yêu cầu về bảo mật thông tin, xác thực người dùng và quản lý dữ liệu tập trung cũng là những yếu tố quan trọng trong môi trường giáo dục hiện đại.

Dựa trên bối cảnh đó, đề tài **CourseGuard** được xây dựng như một ứng dụng desktop hỗ trợ quản lý khóa học và thi trực tuyến. Hệ thống sử dụng nền tảng **Windows Forms trên .NET** kết hợp cơ sở dữ liệu **PostgreSQL/Supabase**, hướng đến việc mô phỏng một môi trường quản lý đào tạo có đầy đủ các vai trò chính gồm quản trị viên, giảng viên và sinh viên. Thông qua việc triển khai các chức năng nghiệp vụ cốt lõi, đề tài giúp nhóm nghiên cứu tiếp cận quy trình xây dựng một phần mềm quản lý giáo dục theo hướng thực tế, có phân lớp xử lý, giao diện người dùng và kết nối cơ sở dữ liệu.

## 2. Lý do chọn đề tài

Đề tài CourseGuard được lựa chọn xuất phát từ nhu cầu thực tiễn trong công tác quản lý đào tạo và tổ chức kiểm tra trực tuyến. Trong môi trường học tập hiện nay, số lượng khóa học, sinh viên, giảng viên và tài liệu học tập ngày càng tăng, nếu chỉ quản lý thủ công bằng bảng tính hoặc hồ sơ rời rạc sẽ dễ phát sinh sai sót, mất thời gian tổng hợp và khó theo dõi lịch sử học tập của từng sinh viên. Một hệ thống phần mềm tập trung giúp tự động hóa các thao tác quản lý, giảm tải công việc cho người phụ trách và nâng cao hiệu quả vận hành.

Bên cạnh đó, thi trực tuyến là một nội dung có tính thời sự và giá trị ứng dụng cao. Việc xây dựng chức năng tạo đề thi, quản lý câu hỏi, cho phép sinh viên làm bài, lưu kết quả và hỗ trợ giảng viên theo dõi phiên thi giúp đề tài có tính thực tiễn rõ ràng hơn so với các hệ thống quản lý khóa học thông thường. Đây cũng là cơ hội để vận dụng kiến thức về lập trình giao diện, xử lý nghiệp vụ, truy vấn dữ liệu, phân quyền người dùng và bảo mật cơ bản trong một bài toán tổng hợp.

Ngoài ra, CourseGuard phù hợp với định hướng của một đồ án công nghệ thông tin vì hệ thống có phạm vi vừa đủ để phân tích, thiết kế và hiện thực trong thời gian học phần. Dự án có nhiều nhóm chức năng gắn với các vai trò khác nhau, bao gồm đăng nhập/đăng ký, quản trị tài khoản, quản lý khóa học, quản lý bài học và tài liệu, tổ chức bài thi, xem điểm, thông báo và thống kê dashboard. Những nội dung này tạo điều kiện để nhóm thực hành quy trình phát triển phần mềm từ khảo sát yêu cầu, thiết kế dữ liệu, xây dựng giao diện đến kiểm thử chức năng.

## 3. Mục tiêu hệ thống

Mục tiêu tổng quát của hệ thống CourseGuard là xây dựng một ứng dụng hỗ trợ quản lý khóa học và thi trực tuyến, cung cấp môi trường làm việc thống nhất cho quản trị viên, giảng viên và sinh viên. Hệ thống hướng đến việc số hóa các thao tác quản lý đào tạo cơ bản, đồng thời hỗ trợ tổ chức đánh giá kết quả học tập thông qua các bài thi trực tuyến.

Các mục tiêu cụ thể gồm:

- Xây dựng chức năng xác thực người dùng, đăng nhập, đăng ký và điều hướng giao diện theo từng vai trò sử dụng.
- Hỗ trợ quản trị viên quản lý tài khoản, kiểm soát trạng thái người dùng, xử lý yêu cầu liên quan đến tài khoản và theo dõi các thông tin tổng quan của hệ thống.
- Hỗ trợ giảng viên quản lý khóa học, bài học, tài liệu học tập, danh sách sinh viên tham gia, bài kiểm tra, câu hỏi thi và kết quả học tập.
- Hỗ trợ sinh viên xem khóa học, tham gia lớp học, theo dõi lịch học, nhận thông báo, làm bài thi trực tuyến và xem lại kết quả.
- Tổ chức dữ liệu tập trung trên cơ sở dữ liệu PostgreSQL/Supabase nhằm bảo đảm khả năng lưu trữ, truy xuất và cập nhật thông tin nhất quán.
- Thiết kế giao diện desktop trực quan, tách biệt theo nhóm người dùng, giúp thao tác thuận tiện và phù hợp với ngữ cảnh sử dụng.
- Ứng dụng các kỹ thuật bảo mật cơ bản như kiểm tra trạng thái tài khoản, băm mật khẩu và quản lý phiên người dùng trong phạm vi của đồ án.

## 4. Phạm vi project

### 4.1. Phạm vi chức năng

Trong phạm vi đồ án, CourseGuard tập trung vào các nhóm chức năng chính sau:

**Đối với quản trị viên:**

- Đăng nhập vào hệ thống với quyền quản trị.
- Xem dashboard tổng quan về người dùng, khóa học và hoạt động hệ thống.
- Quản lý tài khoản người dùng theo vai trò quản trị viên, giảng viên và sinh viên.
- Duyệt hoặc cập nhật trạng thái tài khoản khi cần thiết.
- Hỗ trợ xử lý các yêu cầu liên quan đến tài khoản, bao gồm yêu cầu khôi phục mật khẩu.

**Đối với giảng viên:**

- Quản lý thông tin cá nhân và dashboard giảng viên.
- Tạo, cập nhật, gửi duyệt và quản lý khóa học.
- Quản lý bài học, tài liệu học tập và nội dung liên quan đến khóa học.
- Quản lý danh sách sinh viên đăng ký/tham gia khóa học.
- Tạo và cập nhật bài thi, câu hỏi thi, trạng thái bài thi.
- Theo dõi phiên thi đang hoạt động và xem kết quả học tập của sinh viên.

**Đối với sinh viên:**

- Đăng nhập vào hệ thống với quyền sinh viên.
- Xem dashboard cá nhân, lịch học, thông báo và thông tin khóa học.
- Tham gia hoặc theo dõi các lớp học/khóa học được phân quyền.
- Làm bài thi trực tuyến trên giao diện desktop.
- Xem kết quả, điểm số và thông tin đánh giá sau khi hoàn thành bài thi.

### 4.2. Phạm vi kỹ thuật

- Ứng dụng được xây dựng dưới dạng phần mềm desktop sử dụng **C# Windows Forms**.
- Dự án hướng đến nền tảng **.NET `net10.0-windows`**.
- Cơ sở dữ liệu sử dụng **PostgreSQL/Supabase**, kết nối thông qua thư viện **Npgsql**.
- Hệ thống có tổ chức thư mục theo hướng tách biệt tương đối giữa giao diện, controller, model, truy cập dữ liệu, bảo mật và dịch vụ tích hợp.
- Một số thư viện hỗ trợ được sử dụng cho các nhu cầu như gửi email, đọc cấu hình môi trường, xử lý CSV và hiển thị biểu đồ.

### 4.3. Giới hạn phạm vi

Do giới hạn về thời gian và quy mô đồ án, project chưa đặt mục tiêu xây dựng một nền tảng LMS hoàn chỉnh ở mức triển khai thương mại. Một số nội dung như thanh toán học phí, họp video thời gian thực, tích hợp hệ thống học vụ bên ngoài, phân tích học tập nâng cao, chống gian lận thi trực tuyến ở mức chuyên sâu hoặc triển khai đa nền tảng chưa thuộc phạm vi chính của đề tài.

Hệ thống chủ yếu tập trung vào việc mô phỏng và hiện thực các chức năng cốt lõi của một ứng dụng quản lý khóa học kết hợp thi trực tuyến, qua đó thể hiện năng lực phân tích bài toán, thiết kế cơ sở dữ liệu, xây dựng giao diện, xử lý nghiệp vụ và kết nối dữ liệu trong một đồ án công nghệ thông tin.
