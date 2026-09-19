# Hướng dẫn Git: Branch & Pull Request 🚀

Tài liệu này hướng dẫn quy trình làm việc chuẩn với Git: Tạo nhánh (branch), gửi code (push), và tạo Pull Request (PR).

## 1. Tạo và chuyển sang Branch mới
Trước khi làm một tính năng mới hoặc sửa lỗi, hãy tạo một branch riêng biệt. Đừng làm trực tiếp trên `main`.

```bash
# Kiểm tra branch hiện tại
git branch

# Tạo và chuyển ngay sang branch mới
# Đặt tên branch rõ ràng (ví dụ: feature/login-ui, fix/button-bug)
git checkout -b feature/ten-tinh-nang
```

## 2. Làm việc và Commit Code
Sau khi sửa code xong, thực hiện lưu thay đổi (commit).

```bash
# Xem các file đã thay đổi
git status

# Thêm tất cả thay đổi vào Staging Area
git add .

# Commit với nội dung mô tả rõ ràng
git commit -m "Thêm tính năng đăng nhập mới"
```

## 3. Đẩy Branch lên Server (Push)
Lần đầu tiên đẩy branch mới lên server, bạn cần thiết lập "upstream" (liên kết với remote).

```bash
# Đẩy code lên server
git push -u origin feature/ten-tinh-nang
```
*Lưu ý: Các lần sau chỉ cần gõ `git push` là đủ.*

## 4. Tạo Pull Request (PR)
Sau khi push thành công, bạn cần tạo PR để merge code vào branch chính (`main`).

1.  Truy cập trang repository trên GitHub/GitLab.
2.  Bạn sẽ thấy thông báo **"Compare & pull request"** hiện ra (nếu vừa push xong). Nhấn vào đó.
3.  Nếu không thấy:
    *   Vào tab **Pull requests**.
    *   Nhấn **New pull request**.
    *   Chọn **base: main** <- **compare: feature/ten-tinh-nang**.
4.  Điền tiêu đề và mô tả những gì đã làm.
5.  Nhấn **Create pull request**.

## 5. Sau khi PR được Merge
Khi code đã được merge vào `main`, bạn nên cập nhật lại code ở máy mình.

```bash
# Quay về branch chính
git checkout main

# Kéo code mới nhất về
git pull origin main

# (Tùy chọn) Xóa branch cũ đã merge cho gọn
git branch -d feature/ten-tinh-nang
```
