# Kết luận đồ án CourseGuard

## 1. Đánh giá kết quả đạt được

Đồ án CourseGuard đã xây dựng được một hệ thống hỗ trợ quản lý khóa học và thi trực tuyến trên nền tảng Windows Forms. Hệ thống hướng đến việc hỗ trợ ba nhóm người dùng chính gồm quản trị viên, giảng viên và sinh viên trong các hoạt động quản lý tài khoản, quản lý khóa học, tổ chức bài thi, theo dõi kết quả học tập và giám sát quá trình thi.

Về mặt chức năng, hệ thống đã đáp ứng được các nghiệp vụ cốt lõi của một phần mềm quản lý học tập và thi trực tuyến. Người dùng có thể đăng nhập, đăng ký tài khoản, quên mật khẩu, đổi mật khẩu và đăng xuất. Quản trị viên có thể quản lý người dùng, duyệt đăng ký, xử lý yêu cầu reset mật khẩu, quản lý khóa học và xem các số liệu tổng quan trên dashboard. Giảng viên có thể quản lý khóa học, bài học, tài liệu, bài thi, câu hỏi thi, danh sách sinh viên và kết quả học tập. Sinh viên có thể xem khóa học, gửi yêu cầu tham gia khóa học, theo dõi lịch học, làm bài thi và xem kết quả.

Về mặt dữ liệu, hệ thống đã kết nối được với Supabase PostgreSQL thông qua thư viện `Npgsql`. Các dữ liệu quan trọng như tài khoản, vai trò, khóa học, ghi danh, bài thi, câu hỏi, bài làm, điểm số, thông báo và nhật ký hoạt động được tổ chức trong cơ sở dữ liệu. Điều này giúp hệ thống có khả năng lưu trữ dữ liệu tập trung, phục vụ việc truy xuất, thống kê và hiển thị thông tin trên giao diện.

Về mặt giao diện, CourseGuard đã xây dựng được các màn hình chính cho từng vai trò người dùng. Dashboard quản trị hiển thị các chỉ số tổng quan, yêu cầu người dùng và hoạt động gần đây. Các màn hình quản lý người dùng, khóa học, báo cáo, giảng viên và sinh viên được tổ chức theo hướng trực quan, giúp người dùng thao tác với hệ thống dễ dàng hơn.

Về mặt vận hành, hệ thống có thể được chạy local trên Windows bằng .NET SDK. Quy trình cài đặt và chạy chương trình đã được mô tả trong runbook, bao gồm restore dependency, build project và chạy ứng dụng bằng lệnh `dotnet run` hoặc thông qua Visual Studio/Rider. Điều này cho thấy đồ án không chỉ dừng ở mức phân tích thiết kế mà đã có sản phẩm phần mềm có thể chạy thử, kiểm thử và trình bày.

Ngoài ra, hệ thống còn có một số chức năng hỗ trợ nâng cao như gửi email reset mật khẩu qua SMTP, ghi log hoạt động người dùng, xuất báo cáo/thống kê và hỗ trợ giám sát màn hình sinh viên trong phiên thi thông qua TCP screen monitoring service. Đây là các điểm mở rộng giúp đồ án thể hiện rõ hơn tính thực tế của một hệ thống thi trực tuyến.

Nhìn chung, CourseGuard đã hoàn thành mục tiêu xây dựng một ứng dụng quản lý khóa học và thi trực tuyến ở mức đồ án CNTT. Hệ thống có đầy đủ các module chính, có luồng xử lý dữ liệu rõ ràng và có khả năng đáp ứng các nghiệp vụ cơ bản trong môi trường học tập.

## 2. Hạn chế hiện tại

Mặc dù đã đạt được nhiều kết quả, hệ thống CourseGuard vẫn còn một số hạn chế cần được ghi nhận.

Thứ nhất, kiến trúc hiện tại là desktop monolith. Giao diện WinForms, controller/service nghiệp vụ và tầng truy cập dữ liệu cùng nằm trong một ứng dụng. Cách tổ chức này phù hợp với phạm vi đồ án và dễ triển khai local, nhưng sẽ gặp hạn chế nếu hệ thống cần mở rộng cho nhiều loại client như web, mobile hoặc tích hợp với các hệ thống bên ngoài.

Thứ hai, ứng dụng WinForms hiện kết nối trực tiếp đến Supabase PostgreSQL. Việc client kết nối thẳng đến database giúp đơn giản hóa triển khai trong môi trường học tập, nhưng chưa phù hợp với mô hình production vì khó kiểm soát bảo mật, phân quyền, logging tập trung và kiểm soát truy cập dữ liệu. Trong triển khai thực tế, nên có backend API server trung gian để xử lý nghiệp vụ và bảo vệ database.

Thứ ba, một số thông tin cấu hình nhạy cảm vẫn còn được hardcode hoặc đặt trực tiếp trong mã nguồn ở mức phục vụ demo/dev. Điều này có thể gây rủi ro bảo mật nếu triển khai thực tế. Các thông tin như connection string, tài khoản SMTP hoặc secret nên được chuyển sang biến môi trường, file cấu hình an toàn hoặc secret manager.

Thứ tư, hệ thống vẫn cần được kiểm thử đầy đủ hơn. Hiện tại, việc kiểm thử chủ yếu dựa trên chạy ứng dụng, thao tác thủ công và quan sát kết quả trên giao diện. Để nâng cao độ tin cậy, cần bổ sung unit test, integration test cho controller/service/repository và kiểm thử UI cho các luồng quan trọng như đăng nhập, duyệt tài khoản, làm bài thi, chấm điểm và xuất báo cáo.

Thứ năm, một số chức năng nâng cao vẫn cần hoàn thiện thêm. Theo runbook, một số màn như Device Monitoring, Audit Logs hoặc Settings có thể vẫn còn ở mức placeholder nếu chưa được triển khai đầy đủ. Chức năng giám sát phiên thi đã có service hỗ trợ stream màn hình nhưng cần kiểm thử thực tế trên nhiều máy và nhiều tình huống mạng khác nhau để đánh giá độ ổn định.

Thứ sáu, hệ thống hiện phụ thuộc vào kết nối Internet vì database đặt trên Supabase cloud. Khi mất mạng hoặc kết nối đến Supabase không ổn định, các chức năng truy xuất dữ liệu, đăng nhập, dashboard và xử lý nghiệp vụ có thể bị ảnh hưởng. Hệ thống chưa có cơ chế offline mode hoặc cache dữ liệu cục bộ.

## 3. Hướng phát triển trong tương lai

Trong tương lai, CourseGuard có thể được phát triển theo nhiều hướng để nâng cao khả năng mở rộng, bảo mật và tính ứng dụng thực tế.

Trước hết, nên tách phần backend thành một API server riêng, ví dụ sử dụng ASP.NET Core Web API. Khi đó, ứng dụng WinForms sẽ chỉ đóng vai trò client, gửi request đến backend thay vì kết nối trực tiếp database. Backend API sẽ chịu trách nhiệm xác thực, phân quyền, xử lý nghiệp vụ, truy cập database và ghi log. Cách làm này giúp hệ thống dễ mở rộng sang web app, mobile app hoặc tích hợp với các hệ thống học tập khác.

Tiếp theo, cần chuẩn hóa cơ chế cấu hình và bảo mật. Các thông tin nhạy cảm như connection string, SMTP password, API key hoặc secret cần được đưa ra khỏi source code. Hệ thống nên sử dụng environment variables, `.env` theo từng môi trường hoặc secret manager khi triển khai thực tế. Đồng thời, cần rà soát lại phân quyền để đảm bảo mỗi vai trò chỉ được truy cập đúng các chức năng được phép.

Một hướng phát triển quan trọng khác là hoàn thiện chức năng thi trực tuyến và giám sát phiên thi. Hệ thống có thể bổ sung cảnh báo gian lận, ghi nhận vi phạm, theo dõi trạng thái sinh viên realtime, lưu ảnh/chứng cứ giám sát và thống kê lịch sử vi phạm. Chức năng stream màn hình có thể được cải thiện về hiệu năng, độ trễ, khả năng reconnect và bảo mật truyền dữ liệu.

Hệ thống cũng có thể mở rộng dashboard và báo cáo. Các báo cáo có thể bổ sung biểu đồ trực quan hơn, bộ lọc nâng cao, thống kê theo khóa học, giảng viên, sinh viên, bài thi hoặc khoảng thời gian. Chức năng export có thể chuẩn hóa sang các định dạng như PDF, Excel hoặc CSV để phục vụ công tác quản lý và lưu trữ.

Về trải nghiệm người dùng, có thể tiếp tục cải thiện giao diện theo hướng hiện đại, đồng bộ và dễ sử dụng hơn. Các màn hình nên có thông báo trạng thái rõ ràng, loading indicator, validate input tốt hơn và xử lý lỗi thân thiện hơn. Điều này đặc biệt quan trọng với các thao tác như đăng ký, gửi yêu cầu tham gia khóa học, làm bài thi hoặc xuất báo cáo.

Về chất lượng phần mềm, cần bổ sung hệ thống test tự động và logging chuẩn hóa. Unit test nên được áp dụng cho các hàm xử lý nghiệp vụ, service và repository. Integration test nên kiểm tra các luồng chính với database test. Logging cần đủ chi tiết để hỗ trợ debug khi hệ thống phát sinh lỗi trong quá trình sử dụng.

Ngoài ra, CourseGuard có thể được mở rộng thêm các chức năng như thông báo realtime, chat realtime, quản lý tài liệu nâng cao, nộp bài tập, chấm điểm tự động, phân tích kết quả học tập và tích hợp lịch học. Nếu phát triển theo hướng sản phẩm hoàn chỉnh, hệ thống có thể bổ sung phiên bản web để người dùng truy cập thuận tiện hơn mà không cần cài ứng dụng desktop.

## 4. Kết luận chung

Tổng kết lại, đồ án CourseGuard đã xây dựng được một hệ thống quản lý khóa học và thi trực tuyến có đầy đủ các nhóm chức năng cơ bản cho quản trị viên, giảng viên và sinh viên. Hệ thống đã thể hiện được luồng xử lý từ giao diện người dùng, controller/service nghiệp vụ, tầng truy cập dữ liệu đến cơ sở dữ liệu Supabase PostgreSQL.

Các chức năng như xác thực tài khoản, quản lý người dùng, quản lý khóa học, ghi danh, quản lý bài thi, làm bài, chấm điểm, dashboard, báo cáo, email và giám sát phiên thi đã tạo nên nền tảng tương đối hoàn chỉnh cho một hệ thống phục vụ môi trường học tập trực tuyến.

Mặc dù vẫn còn một số hạn chế về kiến trúc, bảo mật cấu hình, kiểm thử tự động và khả năng mở rộng, CourseGuard đã đáp ứng tốt mục tiêu của một đồ án CNTT. Những hạn chế này cũng là cơ sở để định hướng phát triển trong các phiên bản tiếp theo, đặc biệt là tách backend API, chuẩn hóa bảo mật, hoàn thiện giám sát thi trực tuyến và nâng cao trải nghiệm người dùng.

Với những kết quả đã đạt được, CourseGuard có thể được xem là nền tảng ban đầu khả thi cho việc xây dựng một hệ thống quản lý khóa học và thi trực tuyến có tính ứng dụng thực tế cao hơn trong tương lai.
