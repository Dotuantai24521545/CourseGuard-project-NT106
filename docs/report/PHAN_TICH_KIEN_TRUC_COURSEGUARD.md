# Phân tích kiến trúc project CourseGuard

## 1. Tổng quan kiến trúc hiện tại

CourseGuard là ứng dụng desktop được xây dựng bằng **C# Windows Forms** trên nền tảng **.NET `net10.0-windows`**. Project hiện tại có kiến trúc dạng **desktop monolith**, trong đó giao diện người dùng, xử lý nghiệp vụ và truy cập dữ liệu cùng nằm trong một solution/app, nhưng được chia thư mục tương đối rõ theo vai trò và trách nhiệm.

Về tổng thể, hệ thống có thể nhìn theo các lớp chính:

```text
Người dùng
   |
   v
WinForms UI
   |
   v
Controllers / Services
   |
   v
Data Access / Repositories
   |
   v
PostgreSQL / Supabase
```

Cách tổ chức này phù hợp với quy mô đồ án: dễ triển khai, dễ chạy thử, không cần tách thành nhiều service phức tạp nhưng vẫn thể hiện được các thành phần cơ bản của một hệ thống quản lý khóa học và thi trực tuyến.

> **Xem thêm:** Chi tiết về sơ đồ kết nối mạng, các giao tiếp P2P/Client-Server và giao thức TCP tự thiết kế tại [Phân tích Giao tiếp Hệ thống](file:///c:/Documentss/LtrinhMangCanBan/NT106_QuanLyKhoaHoc_ThiOnline/docs/report/PHAN_TICH_GIAO_TIEP_HE_THONG_COURSEGUARD.md).


## 2. Các module chính

### 2.1. Module Frontend

**Vị trí:** `CourseGuard/CourseGuard/Frontend`

Module Frontend chịu trách nhiệm xây dựng giao diện và tương tác trực tiếp với người dùng. Đây là phần người dùng thao tác nhiều nhất trong hệ thống.

Các nhóm chính:

- `Forms/Login`: giao diện đăng nhập, đăng ký, quên mật khẩu.
- `Forms/Admin`: màn hình điều hướng sau đăng nhập và dashboard quản trị viên.
- `Forms/Teacher`: dashboard giảng viên và các form/dialog phục vụ quản lý khóa học, bài học, tài liệu, bài thi, câu hỏi thi.
- `Forms/Student`: dashboard sinh viên, màn hình lớp học online, làm bài thi và xem lại bài thi.
- `UserControls`: các màn hình con được nhúng trong dashboard theo từng vai trò.
- `Theme`: các thành phần giao diện dùng chung như panel, button, topbar, sidebar, dropdown, skeleton loader.

Vai trò của module Frontend:

- Hiển thị dữ liệu cho người dùng.
- Nhận input từ người dùng.
- Gọi controller hoặc service tương ứng để xử lý nghiệp vụ.
- Điều hướng giao diện theo vai trò Admin, Teacher, Student.
- Cập nhật trạng thái màn hình sau khi dữ liệu thay đổi.

### 2.2. Module Backend Controllers

**Vị trí:** `CourseGuard/CourseGuard/Backend/Controllers`

Module Controllers đóng vai trò trung gian giữa giao diện và tầng dữ liệu. Các controller hiện tại không phải REST API controller, mà là các lớp C# nội bộ được WinForms gọi trực tiếp.

Các controller chính:

- `AuthController`: xử lý đăng nhập, xác thực tài khoản và thông tin người dùng hiện tại.
- `UserController`: xử lý nghiệp vụ quản lý người dùng, duyệt tài khoản, cập nhật trạng thái, xử lý quên mật khẩu.
- `CourseController`: xử lý các nghiệp vụ liên quan đến khóa học, danh sách khóa học, đăng ký học.
- `TeacherController`: xử lý nghiệp vụ dành cho giảng viên như khóa học, bài học, tài liệu, bài thi, câu hỏi, sinh viên, điểm số.
- `DashboardController`: cung cấp dữ liệu thống kê cho dashboard.
- `ChatController`: xử lý dữ liệu tin nhắn và lớp học/chat liên quan.

Vai trò của module Controllers:

- Kiểm tra điều kiện đầu vào cơ bản.
- Điều phối gọi `DbContext`, repository hoặc service.
- Chuyển dữ liệu từ tầng truy cập dữ liệu sang model phù hợp cho UI.
- Giữ cho form không phải xử lý trực tiếp quá nhiều logic nghiệp vụ.

### 2.3. Module Backend Data

**Vị trí:** `CourseGuard/CourseGuard/Backend/Data`

Module Data là nơi truy cập cơ sở dữ liệu chính của hệ thống. Project hiện tại dùng `Npgsql` để kết nối và truy vấn PostgreSQL/Supabase.

Các thành phần chính:

- `CourseGuardDbContext`: cổng truy cập dữ liệu lõi, chứa nhiều truy vấn cho user, course, enrollment, device log và các nghiệp vụ chính.
- `TeacherRepository`: gom các truy vấn liên quan đến nghiệp vụ giảng viên như khóa học, bài học, tài liệu, bài thi, sinh viên và điểm.
- `ScoreRepository`: xử lý dữ liệu điểm số, kết quả học tập.
- `NotificationRepository`: xử lý dữ liệu thông báo.
- `DatabaseAction`: hỗ trợ chuẩn hóa thao tác hoặc kết quả khi làm việc với cơ sở dữ liệu.

Vai trò của module Data:

- Mở kết nối đến database.
- Thực thi truy vấn SQL.
- Ánh xạ dữ liệu từ database sang model C#.
- Cập nhật, thêm, xóa và truy xuất dữ liệu nghiệp vụ.

### 2.4. Module Backend Models

**Vị trí:** `CourseGuard/CourseGuard/Backend/Models`

Module Models chứa các lớp dữ liệu dùng để truyền thông tin giữa UI, controller và data access. Đây là các DTO/model phục vụ cho từng nghiệp vụ cụ thể.

Một số nhóm model tiêu biểu:

- Model người dùng: `UserModel`, `StudentProfileModel`, `TeacherProfileModel`, `AccountSummaryModel`.
- Model khóa học: `CourseModel`, `CourseListItemModel`, `TeacherCourseModel`, `EnrollmentModel`.
- Model bài học/tài liệu: `TeacherLessonModel`, `TeacherMaterialModel`.
- Model bài thi: `TeacherExamModel`, `TeacherExamQuestionModel`, `StudentExamTakingModel`, `StudentExamReviewModel`, `StudentExamListItemModel`.
- Model điểm và kết quả: `StudentScoreModel`, `TeacherScoreModel`, `StudentResultListItemModel`.
- Model dashboard/thống kê: `AdminDashboardMetricsModel`, `TeacherDashboardSummaryModel`, `LoginFrequencyModel`.
- Model thông báo/chat: `NotificationModel`, `ChatMessageModel`, `ChatCourseModel`.
- `WorkflowConstants`: định nghĩa các hằng số trạng thái/nghiệp vụ dùng chung.

Vai trò của module Models:

- Chuẩn hóa cấu trúc dữ liệu trong app.
- Giảm phụ thuộc trực tiếp giữa UI và database schema.
- Giúp controller và repository trao đổi dữ liệu rõ ràng hơn.

### 2.5. Module Backend Security

**Vị trí:** `CourseGuard/CourseGuard/Backend/Security`

Module Security xử lý các vấn đề bảo mật cơ bản của hệ thống.

Các thành phần chính:

- `PasswordHasher`: băm và kiểm tra mật khẩu.
- `UserSessionContext`: lưu thông tin phiên đăng nhập hiện tại.
- `BloomFilter` và `UserIdentityBloomIndex`: hỗ trợ kiểm tra nhanh danh tính/tài khoản trong một số luồng xử lý.

Vai trò của module Security:

- Hỗ trợ xác thực người dùng.
- Quản lý thông tin user đang đăng nhập.
- Cung cấp một số cơ chế tối ưu hoặc kiểm tra dữ liệu liên quan đến tài khoản.

### 2.6. Module Backend Services

**Vị trí:** `CourseGuard/CourseGuard/Backend/Services`

Module Services chứa các xử lý hỗ trợ hoặc tích hợp ngoài, tách khỏi controller/data access để dễ tái sử dụng hơn.

Các service chính:

- `GmailServiceHelper`, `SmtpEmailService`: hỗ trợ gửi email, ví dụ trong luồng quên mật khẩu hoặc thông báo tài khoản.
- `SupabaseAuthService`: hỗ trợ xử lý liên quan đến Supabase/auth.
- `ExamScoringService`: hỗ trợ chấm điểm bài thi.
- `StudentExamAvailabilityService`: kiểm tra điều kiện sinh viên có thể làm bài thi.
- `ChatFileStorageService`: xử lý lưu trữ file liên quan đến chat.
- `MaterialFilePolicy`: kiểm tra/quy định file tài liệu học tập.
- `Monitoring`: các lớp hỗ trợ stream màn hình trong phiên thi, ví dụ `TcpScreenMonitorService`, `StudentScreenStreamClient`, `ScreenStreamProtocol`.

Vai trò của module Services:

- Đóng gói các nghiệp vụ hỗ trợ có thể dùng lại.
- Tách logic đặc thù như email, chấm điểm, kiểm tra điều kiện thi, lưu file khỏi form/controller.
- Hỗ trợ các chức năng nâng cao nhưng vẫn giữ project ở mức đơn giản, phù hợp đồ án.

### 2.7. Module Backend Config và Database Scripts

**Vị trí:**

- `CourseGuard/CourseGuard/Backend/Config`
- `CourseGuard/CourseGuard/Backend/Database`

Module Config chịu trách nhiệm đọc cấu hình môi trường, đặc biệt là file `.env` và các biến môi trường cần thiết để kết nối dịch vụ ngoài hoặc database.

Module Database chứa các script/schema tham chiếu phục vụ tạo và đồng bộ cấu trúc cơ sở dữ liệu.

Vai trò:

- Tách cấu hình khỏi mã nguồn ở mức cơ bản.
- Hỗ trợ triển khai trên máy khác bằng cách thay đổi biến môi trường.
- Lưu trữ schema để tiện tái tạo database khi cần.

## 3. Vai trò kỹ thuật trong xây dựng Admin Dashboard (UI & Backend-for-UI)

Để xây dựng một trang quản trị (Admin Dashboard) hoạt động ổn định và bảo mật trong hệ thống CourseGuard, các thành phần kỹ thuật được phân chia rõ ràng thành hai phần: Giao diện người dùng (UI) và Tầng xử lý hỗ trợ giao diện (Backend-for-UI).

### 3.1. Thành phần giao diện (UI - WinForms Presentation Layer)
Tầng UI chịu trách nhiệm trực quan hóa thông tin và tiếp nhận tương tác từ quản trị viên.

*   **Các Form và UserControl lõi**:
    *   `AdminDashboard` (Form): Khung chứa chính (Main Container) thiết kế theo mô hình bố cục chia vùng, quản lý thanh điều hướng (Sidebar), tiêu đề (Topbar), và một `Panel` trung tâm để nhúng động các UserControl con.
    *   `UC_UserManage` (UserControl): Giao diện quản lý tài khoản, cung cấp bộ lọc trạng thái, ô tìm kiếm và bảng dữ liệu (`DataGridView`) để hiển thị danh sách người dùng.
    *   `UC_CourseManage` (UserControl): Giao diện quản lý các khóa học trong hệ thống, tích hợp các nút điều khiển duyệt/từ chối khóa học đang ở trạng thái `PENDING`.
    *   `UC_AdminReports` (UserControl): Giao diện thống kê và vẽ biểu đồ giám sát hệ thống.
*   **Vai trò kỹ thuật chính**:
    *   **Quản lý trạng thái giao diện (UI State Management)**: Chuyển đổi linh hoạt giữa các màn hình chức năng (UserControls) bằng phương pháp nạp động vào vùng hiển thị chính mà không cần mở các Form popup mới, giúp tối ưu hóa hiệu năng và trải nghiệm người dùng desktop.
    *   **Tiếp nhận và chuẩn hóa dữ liệu đầu vào (Input Handling)**: Ràng buộc biểu mẫu nhập liệu (Form validation), kiểm tra định dạng email, độ dài mật khẩu, bắt lỗi bỏ trống trường dữ liệu trước khi gửi yêu cầu.
    *   **Trực quan hóa dữ liệu thống kê (Data Visualization)**: Tích hợp thư viện `ScottPlot.WinForms` để vẽ biểu đồ trực quan hóa dữ liệu thống kê (biểu đồ tròn thể hiện cơ cấu tài khoản người dùng, biểu đồ đường thể hiện tần suất đăng nhập theo thời gian).
    *   **Gọi điều hướng Controller**: UI đóng vai trò là Client tiêu thụ dịch vụ, không truy vấn trực tiếp cơ sở dữ liệu mà gọi các hàm bất đồng bộ (async/await) của Controller để đảm bảo giao diện không bị treo (non-blocking UI).

### 3.2. Thành phần xử lý nghiệp vụ & dữ liệu (Backend-for-UI)
Tầng này chịu trách nhiệm thực thi các logic nghiệp vụ quản trị, truy xuất cơ sở dữ liệu và chuyển đổi dữ liệu thành các dạng cấu trúc mà UI có thể hiển thị trực tiếp.

*   **Các lớp nghiệp vụ và dữ liệu lõi**:
    *   `UserController`: Cung cấp API nội bộ cho UI để quản lý người dùng, duyệt tài khoản, hoặc khôi phục mật khẩu.
    *   `CourseController`: Cung cấp API xử lý duyệt khóa học, đăng ký khóa học cấp Admin.
    *   `DashboardController`: Xử lý tính toán thống kê và trả về các số liệu tổng hợp.
    *   `CourseGuardDbContext`: Cổng thực thi SQL qua `Npgsql` để tương tác trực tiếp với Supabase.
*   **Vai trò kỹ thuật chính**:
    *   **Điều khiển và Thực thi logic nghiệp vụ (Business Logic & Orchestration)**: 
        *   Khi Admin phê duyệt yêu cầu khôi phục mật khẩu, `UserController` xử lý băm mật khẩu tạm thời bằng `PasswordHasher`, cập nhật trạng thái người dùng trong DB, đồng thời kích hoạt `SmtpEmailService` để gửi mật khẩu mới cho sinh viên/giảng viên qua SMTP.
        *   Khi Admin duyệt khóa học, `CourseController` cập nhật trạng thái khóa học thành `ACTIVE`, đồng thời tự động ghi nhận bản ghi thông báo trong bảng `NOTIFICATIONS` để gửi cho giảng viên sở hữu khóa học.
    *   **Tối ưu hóa và truy xuất cơ sở dữ liệu (Data Access Optimization)**:
        *   Thực hiện các truy vấn SQL tổng hợp nâng cao bằng các hàm tích hợp (`COUNT`, `SUM`, `GROUP BY`, `ORDER BY`) để tính toán số liệu thống kê nhanh chóng thay vì tải toàn bộ bảng dữ liệu về máy khách xử lý.
        *   Sử dụng cơ chế Connection Pooling của thư viện `Npgsql` để quản lý hiệu quả các kết nối TCP tới Supabase, tránh nghẽn đường truyền.
    *   **Đóng gói dữ liệu thông qua DTO/Model (Data Packaging & Security)**:
        *   Sử dụng các Model chuyên biệt như `AdminDashboardMetricsModel` (chứa các thuộc tính đếm tổng số sinh viên, giảng viên, khóa học đang hoạt động) và `RecentUserActivityModel` (dữ liệu log lịch sử) để trả kết quả đã tinh gọn cho UI.
        *   Bảo vệ dữ liệu nhạy cảm bằng cách loại bỏ thông tin hash mật khẩu khỏi các đối tượng truyền tải lên UI của quản trị viên.

## 4. Vai trò từng nhóm người dùng trong hệ thống

### 4.1. Quản trị viên

Quản trị viên có trách nhiệm quản lý tổng quan hệ thống. Các chức năng chính gồm xem dashboard, quản lý tài khoản, duyệt/cập nhật trạng thái người dùng và hỗ trợ xử lý yêu cầu tài khoản.

### 4.2. Giảng viên

Giảng viên là người tạo và quản lý nội dung giảng dạy. Các chức năng chính gồm quản lý khóa học, bài học, bài tập, tài liệu, sinh viên tham gia, bài thi, câu hỏi thi, phiên thi và kết quả học tập.

### 4.3. Sinh viên

Sinh viên là người tham gia học tập và thi. Các chức năng chính gồm xem khóa học/lịch học/thông báo, tham gia lớp học, làm bài thi trực tuyến và xem điểm/kết quả.

## 5. Luồng xử lý dữ liệu

### 5.1. Luồng khởi động ứng dụng

```text
Program.cs
   -> Load cấu hình môi trường
   -> Khởi tạo WinForms
   -> Kiểm tra/tạo tài khoản seed nếu cần
   -> Mở RedirectForm
   -> Điều hướng sang màn hình phù hợp sau đăng nhập
```

`Program.cs` là entry point của ứng dụng. Khi app chạy, hệ thống nạp cấu hình môi trường, chuẩn bị dữ liệu mặc định nếu cần và mở `RedirectForm` để bắt đầu luồng đăng nhập.

### 5.2. Luồng đăng nhập và phân quyền

```text
LoginPage
   -> AuthController.Login()
   -> CourseGuardDbContext.GetUserByUsername()
   -> PasswordHasher kiểm tra mật khẩu
   -> Kiểm tra trạng thái tài khoản
   -> Lưu user/session hiện tại
   -> RedirectForm mở dashboard theo role
```

Sau khi người dùng nhập tài khoản và mật khẩu, UI gọi `AuthController`. Controller lấy thông tin người dùng từ database, kiểm tra mật khẩu và trạng thái tài khoản. Nếu hợp lệ, hệ thống điều hướng đến dashboard tương ứng với vai trò Admin, Teacher hoặc Student.

### 5.3. Luồng quản lý khóa học

```text
TeacherDashboard / StudentDashboard / AdminDashboard
   -> CourseController hoặc TeacherController
   -> CourseGuardDbContext / TeacherRepository
   -> PostgreSQL/Supabase
   -> Model kết quả
   -> Cập nhật lại UI
```

Với giảng viên, luồng này dùng để tạo/cập nhật khóa học, gửi duyệt khóa học, quản lý bài học và tài liệu. Với sinh viên, luồng này dùng để xem danh sách khóa học, đăng ký/tham gia khóa học hoặc theo dõi nội dung học tập.

### 5.4. Luồng thi trực tuyến

```text
TeacherDashboard
   -> TeacherController
   -> TeacherRepository
   -> Tạo bài thi/câu hỏi/trạng thái bài thi

StudentDashboard / DoExamForm
   -> CourseController hoặc service liên quan
   -> StudentExamAvailabilityService
   -> Lấy câu hỏi từ database
   -> Sinh viên làm bài
   -> ExamScoringService chấm điểm
   -> Lưu kết quả vào database
```

Giảng viên tạo bài thi và câu hỏi, sau đó sinh viên làm bài thông qua giao diện `DoExamForm`. Khi hoàn thành, hệ thống ghi nhận câu trả lời, tính điểm và lưu kết quả để sinh viên/giảng viên có thể xem lại.

### 5.5. Luồng thông báo, email và hỗ trợ tài khoản

```text
UI quản trị hoặc login
   -> UserController / NotificationRepository
   -> SmtpEmailService hoặc GmailServiceHelper
   -> Database / Email provider
   -> Trả kết quả về UI
```

Luồng này phục vụ các chức năng như thông báo, duyệt tài khoản, yêu cầu khôi phục mật khẩu hoặc gửi email hỗ trợ người dùng.

## 6. Mô hình triển khai

Project hiện tại phù hợp với mô hình triển khai đơn giản như sau:

```text
Máy người dùng Windows
   - Chạy ứng dụng CourseGuard WinForms
   - Đọc cấu hình từ .env / environment variables
   - Kết nối Internet hoặc mạng nội bộ
        |
        v
PostgreSQL / Supabase
   - Lưu user, course, enrollment, exam, score, notification
        |
        v
Dịch vụ ngoài nếu có
   - Email SMTP/Gmail
   - Supabase Auth/Storage tùy cấu hình
```

Đặc điểm mô hình triển khai:

- Ứng dụng chạy trực tiếp trên máy Windows của người dùng.
- Không có backend web server riêng tách biệt.
- App desktop kết nối trực tiếp đến database/service thông qua cấu hình môi trường.
- Phù hợp với demo đồ án, môi trường lab hoặc triển khai nội bộ quy mô nhỏ.
- Khi triển khai thực tế quy mô lớn, có thể cân nhắc tách API server riêng để tăng bảo mật và dễ quản lý truy cập database.

## 7. Dependency giữa các thành phần

### 7.1. Dependency nội bộ

```text
Frontend Forms/UserControls
   -> Backend Controllers
   -> Backend Data / Repositories
   -> Backend Models
   -> Backend Security / Services
```

Mối quan hệ chính:

- Frontend phụ thuộc vào Controllers để lấy và cập nhật dữ liệu.
- Controllers phụ thuộc vào Data/Repositories để truy vấn database.
- Data/Repositories phụ thuộc vào Models để ánh xạ dữ liệu.
- Controllers và Data có thể dùng Security để kiểm tra mật khẩu, session hoặc trạng thái.
- Controllers hoặc Forms có thể dùng Services cho các xử lý phụ như gửi email, chấm điểm, kiểm tra điều kiện thi.
- Theme/UserControls phụ thuộc vào WinForms và thư viện vẽ giao diện, ít liên quan đến database.

### 7.2. Dependency thư viện ngoài

Một số dependency đáng chú ý trong project:

- `Npgsql`: kết nối PostgreSQL/Supabase.
- `DotNetEnv`: đọc cấu hình từ file `.env`.
- `MailKit`: gửi email qua SMTP.
- `Google.Apis.Gmail.v1`: tích hợp Gmail API.
- `CsvHelper`: hỗ trợ xử lý dữ liệu CSV.
- `ScottPlot.WinForms`: hiển thị biểu đồ/thống kê trong giao diện WinForms.

### 7.3. Nhận xét về mức độ phụ thuộc

Kiến trúc hiện tại có mức độ tách lớp vừa phải, phù hợp với quy mô đồ án. Project chưa tách thành nhiều layer/package độc lập hoặc áp dụng quá nhiều pattern phức tạp. Điều này giúp việc đọc code, chạy demo và phát triển chức năng nhanh hơn.

Tuy nhiên, do app desktop kết nối trực tiếp đến database, một số logic truy cập dữ liệu còn tập trung nhiều trong `CourseGuardDbContext` và repository. Đây là lựa chọn chấp nhận được trong phạm vi đồ án, nhưng nếu phát triển thành sản phẩm thực tế thì nên cân nhắc tách backend API riêng, chuẩn hóa service/repository và kiểm soát quyền truy cập dữ liệu chặt chẽ hơn.

## 8. Kết luận

Kiến trúc CourseGuard hiện tại được tổ chức theo hướng đơn giản, dễ hiểu và bám sát yêu cầu của một đồ án quản lý khóa học kết hợp thi trực tuyến. Hệ thống gồm các module giao diện, controller, model, truy cập dữ liệu, bảo mật, dịch vụ hỗ trợ và cấu hình. Dữ liệu được xử lý theo luồng từ WinForms UI đến controller, repository/database rồi trả về model để hiển thị lại cho người dùng.

Mô hình này không quá phức tạp, phù hợp với mục tiêu học tập và demo chức năng. Đồng thời, cấu trúc project vẫn đủ rõ ràng để mở rộng thêm các chức năng như nâng cấp bảo mật, bổ sung API trung gian, cải thiện kiểm thử hoặc hoàn thiện các module quản lý học tập trong tương lai.
