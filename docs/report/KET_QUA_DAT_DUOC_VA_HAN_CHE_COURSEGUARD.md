# Kết quả đạt được, chức năng đã hoàn thành và hạn chế hiện tại của CourseGuard

Tài liệu này tổng hợp kết quả triển khai hệ thống CourseGuard dựa trên runbook, output kiểm thử dự kiến của hệ thống, các màn hình/chức năng đã phân tích trong source code và các tài liệu báo cáo trong `docs/report`.

## 1. Cơ sở đánh giá

Việc đánh giá kết quả hệ thống dựa trên các nguồn sau:

- Runbook vận hành tại `docs/README.md`.
- Danh sách chức năng được tổng hợp từ source code.
- Phân tích kiến trúc project hiện tại.
- Luồng kiểm thử tối thiểu sau khi chạy ứng dụng.
- Các output hệ thống có thể quan sát được qua:
  - Screenshot giao diện.
  - Terminal output khi restore/build/run.
  - Metrics trên dashboard.
  - Kết quả nghiệp vụ như đăng nhập, duyệt tài khoản, thống kê, báo cáo.

## 2. Kết quả đạt được

### 2.1. Kết quả về mặt hệ thống

CourseGuard đã xây dựng được một ứng dụng desktop phục vụ quản lý khóa học và thi trực tuyến trên nền tảng Windows Forms. Hệ thống có kiến trúc monolithic phù hợp với phạm vi đồ án, trong đó giao diện, xử lý nghiệp vụ và truy cập dữ liệu được tổ chức trong cùng một solution nhưng vẫn chia theo các module rõ ràng.

Các kết quả chính đã đạt được:

| Nhóm kết quả | Mô tả |
| --- | --- |
| Ứng dụng chạy được trên Windows | Project có thể restore, build và run bằng .NET SDK theo runbook. |
| Kết nối được database cloud | Ứng dụng kết nối đến Supabase PostgreSQL thông qua `Npgsql`. |
| Có phân quyền người dùng | Hệ thống điều hướng giao diện theo vai trò Admin, Teacher, Student. |
| Có dashboard quản trị | Admin Dashboard hiển thị KPI, yêu cầu người dùng và hoạt động gần đây. |
| Có quản lý khóa học | Hỗ trợ tạo, cập nhật, duyệt, ghi danh và theo dõi khóa học. |
| Có chức năng thi trực tuyến | Hỗ trợ quản lý bài thi, câu hỏi, phiên thi, bài làm và điểm số. |
| Có thống kê/báo cáo | Hỗ trợ lọc dữ liệu, xem metrics và xuất báo cáo ở một số màn hình. |
| Có hỗ trợ email | Luồng quên mật khẩu có gửi email thông qua SMTP/MailKit. |
| Có giám sát phiên thi | Source code có service stream màn hình sinh viên qua TCP trong phiên thi. |

### 2.2. Kết quả quan sát qua terminal output

Theo runbook, hệ thống có thể được chạy qua terminal bằng các lệnh:

```powershell
dotnet restore "CourseGuard/CourseGuard/CourseGuard.csproj"
dotnet build "CourseGuard/CourseGuard/CourseGuard.csproj"
dotnet run --project "CourseGuard/CourseGuard/CourseGuard.csproj"
```

Output mong đợi:

| Lệnh | Kết quả mong đợi |
| --- | --- |
| `dotnet restore` | Tải và khôi phục thành công các package NuGet. |
| `dotnet build` | Build project thành công, không phát sinh lỗi biên dịch. |
| `dotnet run` | Khởi động ứng dụng WinForms và hiển thị màn hình đăng nhập. |

Khi quá trình build/run thành công, đây là bằng chứng cho thấy source code, dependency và cấu hình .NET cơ bản đã đáp ứng yêu cầu chạy local.

### 2.3. Kết quả quan sát qua screenshot giao diện

Các screenshot giao diện có thể được dùng để minh chứng những kết quả sau:

| Màn hình | Nội dung có thể minh chứng |
| --- | --- |
| Login/Register/Forgot Password | Người dùng có thể đăng nhập, đăng ký hoặc gửi yêu cầu quên mật khẩu. |
| Admin Dashboard | KPI, recent activities và pending user requests được hiển thị. |
| User Management | Admin có thể tìm kiếm, lọc, duyệt hoặc reset tài khoản người dùng. |
| Course Management | Danh sách khóa học được load và quản lý theo trạng thái. |
| Teacher Dashboard | Giảng viên có khu vực quản lý khóa học, bài học, bài thi, sinh viên. |
| Student Dashboard | Sinh viên có thể xem khóa học, lịch học, bài thi và kết quả. |
| Reports | Hệ thống có màn hình lọc dữ liệu và xuất báo cáo. |

Những screenshot này giúp báo cáo thể hiện rõ hệ thống không chỉ dừng ở mức thiết kế dữ liệu mà đã có giao diện thao tác thực tế.

### 2.4. Kết quả quan sát qua metrics

Một số metrics được hệ thống hiển thị hoặc có thể tổng hợp từ database:

| Metric | Ý nghĩa |
| --- | --- |
| Tổng số tài khoản | Thể hiện quy mô người dùng trong hệ thống. |
| Số tài khoản đang chờ duyệt | Phục vụ Admin xử lý đăng ký/reset mật khẩu. |
| Số khóa học | Thể hiện dữ liệu đào tạo đang được quản lý. |
| Số hoạt động đăng nhập gần đây | Hỗ trợ theo dõi hoạt động người dùng. |
| Số sinh viên ghi danh | Đánh giá mức độ tham gia khóa học. |
| Số bài thi/kết quả thi | Phản ánh dữ liệu phục vụ nghiệp vụ thi trực tuyến. |

Các metrics này được dùng để trực quan hóa tình trạng hệ thống trên dashboard và phục vụ báo cáo quản trị.

### 2.5. Kết quả quan sát qua prediction/result

Trong phạm vi source code CourseGuard hiện tại, hệ thống tập trung vào quản lý khóa học và thi trực tuyến, không phải một hệ thống AI prediction độc lập. Tuy nhiên, phần “prediction result” có thể hiểu theo hướng kết quả xử lý/nghiệp vụ mà hệ thống trả về sau mỗi luồng thao tác.

Ví dụ:

| Luồng xử lý | Kết quả trả về |
| --- | --- |
| Đăng nhập | Thành công/thất bại, điều hướng theo role. |
| Duyệt đăng ký | Tài khoản chuyển từ `PENDING` sang `ACTIVE`. |
| Quên mật khẩu | Tài khoản chuyển sang `RESET_REQUEST`, sau đó được reset và gửi email. |
| Duyệt khóa học | Khóa học chuyển sang trạng thái `ACTIVE` hoặc `REJECTED`. |
| Ghi danh | Enrollment được tạo/cập nhật theo trạng thái phù hợp. |
| Làm bài thi | Hệ thống tính điểm và lưu kết quả bài làm. |
| Báo cáo | Dữ liệu được lọc/tổng hợp và xuất ra file báo cáo. |

Như vậy, hệ thống đã có các kết quả xử lý rõ ràng ở từng nghiệp vụ, có thể dùng làm output minh chứng trong báo cáo.

## 3. Chức năng đã hoàn thành

### 3.1. Nhóm chức năng xác thực và tài khoản

- Đăng nhập bằng username/password.
- Đăng nhập bất đồng bộ để giảm treo giao diện.
- Ghi nhận thông tin đăng nhập và hoạt động người dùng.
- Đăng ký tài khoản sinh viên ở trạng thái chờ duyệt.
- Gửi yêu cầu quên mật khẩu.
- Admin duyệt reset mật khẩu và gửi email mật khẩu tạm.
- Đổi mật khẩu.
- Đăng xuất và xóa session hiện tại.

### 3.2. Nhóm chức năng quản trị viên

- Xem dashboard quản trị.
- Xem KPI và thống kê tổng quan.
- Xem hoạt động người dùng gần đây.
- Tìm kiếm/lọc người dùng theo trạng thái và vai trò.
- Duyệt/từ chối đăng ký tài khoản.
- Duyệt yêu cầu reset mật khẩu.
- Tạo, cập nhật hoặc xóa người dùng theo quyền.
- Quản lý khóa học ở cấp Admin.
- Duyệt hoặc từ chối khóa học do giảng viên gửi.
- Duyệt/từ chối ghi danh sinh viên.
- Xem và xuất báo cáo/thống kê.

### 3.3. Nhóm chức năng giảng viên

- Xem dashboard tổng quan của giảng viên.
- Quản lý hồ sơ giảng viên.
- Tạo, cập nhật, xóa khóa học.
- Gửi khóa học lên Admin để duyệt.
- Quản lý bài học.
- Quản lý bài tập.
- Quản lý tài liệu học tập.
- Quản lý sinh viên trong khóa học.
- Duyệt/từ chối yêu cầu ghi danh.
- Quản lý bài thi.
- Quản lý câu hỏi thi.
- Xem và cập nhật điểm/kết quả học tập.
- Theo dõi phiên thi đang hoạt động.

### 3.4. Nhóm chức năng sinh viên

- Đăng ký, đăng nhập, đổi mật khẩu và quên mật khẩu.
- Xem danh sách khóa học có thể tham gia.
- Gửi yêu cầu tham gia khóa học.
- Xem trạng thái ghi danh.
- Xem lịch học/lịch online.
- Tham gia lớp học hoặc hoạt động trong khóa học.
- Làm bài thi trực tuyến.
- Xem kết quả học tập/bài thi.
- Sử dụng chat/lớp học theo khóa học.

### 3.5. Nhóm chức năng thi trực tuyến

- Tạo và quản lý bài thi.
- Tạo và quản lý câu hỏi thi.
- Kiểm tra trạng thái bài thi.
- Kiểm tra điều kiện làm bài của sinh viên.
- Sinh viên làm bài thi.
- Chấm điểm bài thi thông qua service.
- Lưu kết quả bài làm và điểm số.
- Theo dõi phiên thi đang hoạt động.
- Hỗ trợ stream màn hình sinh viên trong phiên thi.

### 3.6. Nhóm chức năng báo cáo và thống kê

- Thống kê số lượng tài khoản.
- Thống kê hoạt động đăng nhập.
- Thống kê danh sách khóa học.
- Hiển thị dashboard theo vai trò.
- Lọc báo cáo theo ngày.
- Xuất dữ liệu báo cáo ở một số định dạng, ví dụ CSV/Excel/PDF tùy màn hình triển khai.

### 3.7. Nhóm chức năng thông báo, email và chat

- Tạo thông báo khi khóa học được duyệt/từ chối.
- Tạo thông báo khi có yêu cầu ghi danh.
- Gửi email reset mật khẩu.
- Ghi log sự kiện hệ thống.
- Lấy danh sách phòng chat theo khóa học.
- Gửi và xem tin nhắn trong lớp học.
- Gửi file trong chat có kiểm tra định dạng/dung lượng.

## 4. Hạn chế hiện tại

### 4.1. Hạn chế về kiến trúc

| Hạn chế | Mô tả |
| --- | --- |
| Chưa tách backend API | Project là WinForms desktop monolith, UI gọi trực tiếp controller/service nội bộ. |
| Client kết nối trực tiếp database | Ứng dụng WinForms kết nối thẳng đến Supabase PostgreSQL, chưa có API server trung gian. |
| Khó mở rộng theo mô hình nhiều client | Nếu sau này có web/mobile app, cần tách backend service riêng. |
| Chưa có Docker cho app chính | Do đặc thù WinForms, Docker không phải phương án chạy chính hiện tại. |

### 4.2. Hạn chế về cấu hình và bảo mật

| Hạn chế | Mô tả |
| --- | --- |
| Connection string còn hardcoded | Một số file data access vẫn chứa connection string trực tiếp. |
| Secret chưa được quản lý chuẩn production | Chưa dùng secret manager hoặc cơ chế cấu hình bảo mật tập trung. |
| SMTP có default config cho demo | Phù hợp môi trường học tập/dev nhưng không nên dùng trực tiếp cho production. |
| Phân quyền cần kiểm thử thêm | Cần rà soát kỹ hơn các luồng thao tác nhạy cảm theo từng role. |

### 4.3. Hạn chế về vận hành

| Hạn chế | Mô tả |
| --- | --- |
| Phụ thuộc Internet | Vì database đặt trên Supabase cloud nên mất mạng sẽ ảnh hưởng trực tiếp. |
| Chưa có cơ chế offline | App chưa hỗ trợ cache/local mode khi không kết nối được database. |
| Một số màn còn placeholder | Runbook ghi nhận các màn như `Device Monitoring`, `Audit Logs`, `Settings` còn placeholder nếu chưa có logic đầy đủ. |
| Lỗi file exe bị lock khi build | Nếu app đang chạy, build có thể lỗi `MSB3021/MSB3027`. |

### 4.4. Hạn chế về kiểm thử và chất lượng

| Hạn chế | Mô tả |
| --- | --- |
| Chưa thấy bộ test tự động đầy đủ | Cần bổ sung unit test/integration test cho controller, repository và service. |
| Kiểm thử UI chủ yếu thủ công | WinForms cần test qua thao tác thực tế hoặc công cụ UI automation. |
| Cần kiểm thử tải và dữ liệu lớn | Dashboard, reports, chat và exam cần kiểm thử thêm với nhiều user/dữ liệu lớn. |
| Cần chuẩn hóa logging | Hiện có audit/activity log, nhưng cần chuẩn hóa hơn cho debug production. |

### 4.5. Hạn chế về chức năng

| Hạn chế | Mô tả |
| --- | --- |
| Giám sát phiên thi cần hoàn thiện thêm | Đã có service stream màn hình nhưng cần kiểm thử thực tế nhiều máy. |
| Báo cáo có thể mở rộng | Có thể bổ sung thêm biểu đồ, dashboard nâng cao và export chuẩn hơn. |
| Thông báo realtime chưa rõ ràng | Hiện thông báo chủ yếu dựa trên database, có thể mở rộng realtime. |
| Chưa có backend web API | Các nghiệp vụ chưa expose qua REST/gRPC nên khó tích hợp hệ thống khác. |

## 5. Đánh giá tổng quan

CourseGuard đã hoàn thành được các chức năng cốt lõi của một hệ thống quản lý khóa học và thi trực tuyến ở mức đồ án CNTT. Hệ thống có đầy đủ các nhóm nghiệp vụ chính gồm xác thực tài khoản, phân quyền người dùng, quản lý khóa học, quản lý bài thi, làm bài, chấm điểm, dashboard, báo cáo, email và một số chức năng giám sát phiên thi.

Về mặt triển khai, hệ thống có thể chạy local trên Windows bằng .NET SDK, kết nối đến Supabase PostgreSQL cloud và hiển thị dữ liệu thực trên giao diện. Điều này cho thấy project không chỉ dừng ở mô hình phân tích mà đã có sản phẩm phần mềm có thể vận hành và kiểm thử.

Tuy nhiên, do định hướng là đồ án và kiến trúc hiện tại là WinForms desktop monolith, hệ thống vẫn còn một số hạn chế về bảo mật cấu hình, khả năng mở rộng, test tự động và triển khai production. Trong các phiên bản tiếp theo, nên ưu tiên tách backend API, chuẩn hóa quản lý secret, bổ sung test tự động và hoàn thiện các màn còn placeholder.
