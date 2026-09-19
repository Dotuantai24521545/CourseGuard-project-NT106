using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CourseGuard.Backend.Data;
using CourseGuard.Backend.Models;
using CourseGuard.Backend.Services;
using CourseGuard.Frontend.Helpers;

namespace CourseGuard.Backend.Controllers
{
    public class ChatController
    {
        private readonly CourseGuardDbContext _dbContext;
        private readonly ChatFileStorageService _fileStorageService;
        public string LastErrorMessage { get; private set; } = string.Empty;

        public ChatController(CourseGuardDbContext dbContext)
        {
            _dbContext = dbContext;
            _fileStorageService = new ChatFileStorageService();
        }

        public List<ChatCourseModel> GetMyCourses(int userId)
        {
            if (userId <= 0)
            {
                return new List<ChatCourseModel>();
            }

            return _dbContext.GetChatCoursesForUser(userId);
        }

        public Task<List<ChatCourseModel>> GetMyCoursesAsync(int userId, CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
            {
                return Task.FromResult(new List<ChatCourseModel>());
            }

            return _dbContext.GetChatCoursesForUserAsync(userId, cancellationToken);
        }

        public int GetUnreadCount(int userId)
        {
            LastErrorMessage = string.Empty;
            return userId <= 0 ? 0 : _dbContext.GetUnreadChatCount(userId);
        }

        public List<ChatCourseModel> GetUnreadCourses(int userId)
        {
            LastErrorMessage = string.Empty;
            return userId <= 0
                ? new List<ChatCourseModel>()
                : _dbContext.GetChatCoursesForUser(userId).Where(course => course.UnreadCount > 0).ToList();
        }

        public void MarkAllRead(int userId)
        {
            LastErrorMessage = string.Empty;
            if (userId > 0)
            {
                _dbContext.MarkAllChatRead(userId);
            }
        }

        public void MarkCourseRead(int userId, int courseId)
        {
            LastErrorMessage = string.Empty;
            if (userId > 0 && courseId > 0)
            {
                _dbContext.MarkCourseChatRead(userId, courseId);
            }
        }

        public List<ChatMessageModel> GetMessages(int userId, int courseId, int limit = 100)
        {
            LastErrorMessage = string.Empty;
            if (!_dbContext.CanAccessCourseChat(userId, courseId))
            {
                LastErrorMessage = "Bạn không có quyền truy cập phòng chat này.";
                return new List<ChatMessageModel>();
            }

            return _dbContext.GetChatMessages(courseId, limit);
        }

        public async Task<List<ChatMessageModel>> GetMessagesAsync(int userId, int courseId, int limit = 100, CancellationToken cancellationToken = default)
        {
            LastErrorMessage = string.Empty;
            if (!_dbContext.CanAccessCourseChat(userId, courseId))
            {
                LastErrorMessage = "Bạn không có quyền truy cập phòng chat này.";
                return new List<ChatMessageModel>();
            }

            return await _dbContext.GetChatMessagesForUserAsync(userId, courseId, limit, cancellationToken);
        }

        public async Task<List<ChatMessageModel>> GetMessagesBeforeAsync(int userId, int courseId, int beforeMessageId, int limit = 20, CancellationToken cancellationToken = default)
        {
            LastErrorMessage = string.Empty;
            if (!_dbContext.CanAccessCourseChat(userId, courseId))
            {
                LastErrorMessage = "Bạn không có quyền truy cập phòng chat này.";
                return new List<ChatMessageModel>();
            }

            return await _dbContext.GetChatMessagesBeforeForUserAsync(userId, courseId, beforeMessageId, limit, cancellationToken);
        }

        public async Task<List<ChatMessageModel>> GetMessagesAfterAsync(int userId, int courseId, int afterMessageId, int limit = 50, CancellationToken cancellationToken = default)
        {
            LastErrorMessage = string.Empty;
            if (!_dbContext.CanAccessCourseChat(userId, courseId))
            {
                LastErrorMessage = "Bạn không có quyền truy cập phòng chat này.";
                return new List<ChatMessageModel>();
            }

            return await _dbContext.GetChatMessagesAfterForUserAsync(userId, courseId, afterMessageId, limit, cancellationToken);
        }

        public bool SendMessage(int userId, int courseId, string content)
        {
            LastErrorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(content))
            {
                LastErrorMessage = "Nội dung tin nhắn không được để trống.";
                return false;
            }

            if (!_dbContext.CanAccessCourseChat(userId, courseId))
            {
                LastErrorMessage = "Bạn không có quyền gửi tin nhắn trong phòng chat này.";
                return false;
            }

            int messageId = _dbContext.SendChatMessage(courseId, userId, content.Trim());
            if (messageId <= 0)
            {
                LastErrorMessage = "Gửi tin nhắn thất bại.";
                return false;
            }

            _dbContext.LogUserActivity(userId, "CHAT_USE", $"Gửi tin nhắn chat trong khóa học ID={courseId}", string.Empty);
            return true;
        }

        public async Task<ChatMessageModel?> SendMessageAsync(int userId, int courseId, string content, CancellationToken cancellationToken = default)
        {
            LastErrorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(content))
            {
                LastErrorMessage = "Nội dung tin nhắn không được để trống.";
                return null;
            }

            if (!_dbContext.CanAccessCourseChat(userId, courseId))
            {
                LastErrorMessage = "Bạn không có quyền gửi tin nhắn trong phòng chat này.";
                return null;
            }

            ChatMessageModel? createdMessage = await _dbContext.SendChatMessageAsync(courseId, userId, content.Trim(), cancellationToken);
            if (createdMessage == null || createdMessage.Id <= 0)
            {
                LastErrorMessage = "Gửi tin nhắn thất bại.";
                return null;
            }

            try
            {
                await Task.Run(() => _dbContext.LogUserActivity(userId, "CHAT_USE", $"Gửi tin nhắn chat trong khóa học ID={courseId}", string.Empty), cancellationToken);
            }
            catch
            {
                // Logging failure must not roll back or delay the already-created chat message.
            }

            return createdMessage;
        }

        public bool SendFileMessage(int userId, int courseId, string sourceFilePath, string caption = "")
        {
            LastErrorMessage = string.Empty;
            if (!_dbContext.CanAccessCourseChat(userId, courseId))
            {
                LastErrorMessage = "Bạn không có quyền gửi file trong phòng chat này.";
                return false;
            }

            if (!ValidateFile(sourceFilePath, out string validationError))
            {
                LastErrorMessage = validationError;
                return false;
            }

            byte[] fileContent = File.ReadAllBytes(sourceFilePath);
            var saved = _fileStorageService.SaveChatFile(courseId, sourceFilePath);
            string mimeType = ResolveMimeType(Path.GetExtension(saved.SavedFileName));
            int messageId = _dbContext.SendChatFileMessage(
                courseId,
                userId,
                caption,
                saved.SavedPath,
                saved.SavedFileName,
                saved.FileSize,
                mimeType,
                fileContent);

            if (messageId <= 0)
            {
                LastErrorMessage = "Gửi file thất bại.";
                return false;
            }

            _dbContext.LogUserActivity(userId, "CHAT_USE", $"Gửi file chat trong khóa học ID={courseId}: {saved.SavedFileName}", string.Empty);
            return true;
        }

        public async Task<bool> SendFileMessageAsync(int userId, int courseId, string sourceFilePath, string caption = "", CancellationToken cancellationToken = default)
        {
            return await SendFileMessageModelAsync(userId, courseId, sourceFilePath, caption, cancellationToken) != null;
        }

        public async Task<ChatMessageModel?> SendFileMessageModelAsync(int userId, int courseId, string sourceFilePath, string caption = "", CancellationToken cancellationToken = default)
        {
            LastErrorMessage = string.Empty;
            if (!_dbContext.CanAccessCourseChat(userId, courseId))
            {
                LastErrorMessage = "Bạn không có quyền gửi file trong phòng chat này.";
                return null;
            }

            if (!ValidateFile(sourceFilePath, out string validationError))
            {
                LastErrorMessage = validationError;
                return null;
            }

            byte[] fileContent = await File.ReadAllBytesAsync(sourceFilePath, cancellationToken);
            var saved = await Task.Run(() => _fileStorageService.SaveChatFile(courseId, sourceFilePath), cancellationToken);
            string mimeType = ResolveMimeType(Path.GetExtension(saved.SavedFileName));
            ChatMessageModel? message = await _dbContext.SendChatFileMessageAsync(
                courseId,
                userId,
                caption,
                saved.SavedPath,
                saved.SavedFileName,
                saved.FileSize,
                mimeType,
                fileContent,
                cancellationToken);

            if (message == null)
            {
                LastErrorMessage = "Gửi file thất bại.";
                return null;
            }

            await Task.Run(() => _dbContext.LogUserActivity(userId, "CHAT_USE", $"Gửi file chat trong khóa học ID={courseId}: {saved.SavedFileName}", string.Empty), cancellationToken);
            return message;
        }

        public async Task<byte[]?> GetFileMessageContentAsync(int userId, int courseId, int messageId, CancellationToken cancellationToken = default)
        {
            LastErrorMessage = string.Empty;
            if (!_dbContext.CanAccessCourseChat(userId, courseId))
            {
                LastErrorMessage = "Bạn không có quyền tải file trong phòng chat này.";
                return null;
            }

            if (messageId <= 0)
            {
                LastErrorMessage = "File chat không hợp lệ.";
                return null;
            }

            byte[]? content = await _dbContext.GetChatFileContentAsync(courseId, messageId, cancellationToken);
            if (content == null || content.Length == 0)
            {
                LastErrorMessage = "Không tìm thấy nội dung file để tải xuống.";
            }

            return content;
        }

        public async Task<ChatMessageModel?> SendImageMessageAsync(
            int userId,
            int courseId,
            string compressedImagePath,
            string originalFileName,
            string caption = "",
            CancellationToken cancellationToken = default)
        {
            LastErrorMessage = string.Empty;
            if (!_dbContext.CanAccessCourseChat(userId, courseId))
            {
                LastErrorMessage = "Bạn không có quyền gửi ảnh trong phòng chat này.";
                return null;
            }

            if (!ChatImageHelper.IsSupportedImage(originalFileName) && !ChatImageHelper.IsSupportedImage(compressedImagePath))
            {
                LastErrorMessage = "Chỉ hỗ trợ gửi ảnh JPG, JPEG hoặc PNG.";
                return null;
            }

            if (string.IsNullOrWhiteSpace(compressedImagePath) || !File.Exists(compressedImagePath))
            {
                LastErrorMessage = "Không tìm thấy ảnh đã xử lý để gửi.";
                return null;
            }

            var saved = await Task.Run(
                () => _fileStorageService.SaveChatImage(courseId, userId, compressedImagePath, originalFileName),
                cancellationToken);
            string mimeType = ChatImageHelper.GetMimeType(saved.SavedPath);
            byte[] fileContent = await File.ReadAllBytesAsync(saved.SavedPath, cancellationToken);

            string publicUrl = saved.SavedPath;
            try
            {
                var storageService = new CourseGuard.Backend.Services.Storage.SupabaseStorageService();
                string storageFileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{Path.GetExtension(saved.SavedPath).ToLowerInvariant()}";
                publicUrl = await storageService.UploadFileAsync(fileContent, storageFileName, "nt106", mimeType);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Supabase upload failed: {ex.Message}. Falling back to database storage only.");
            }

            ChatMessageModel? message = await _dbContext.SendChatFileMessageAsync(
                courseId,
                userId,
                caption,
                publicUrl,
                saved.SavedFileName,
                saved.FileSize,
                mimeType,
                fileContent,
                cancellationToken);

            if (message == null)
            {
                LastErrorMessage = "Gửi ảnh thất bại.";
                return null;
            }

            try
            {
                await Task.Run(() => _dbContext.LogUserActivity(userId, "CHAT_USE", $"Gửi ảnh chat trong khóa học ID={courseId}: {saved.SavedFileName}", string.Empty), cancellationToken);
            }
            catch
            {
                // Logging failure must not block a successful image message.
            }

            return message;
        }

        public async Task<ChatMessageModel?> SendImageGroupMessageAsync(
            int userId,
            int courseId,
            IReadOnlyList<CompressedChatImageResult> compressedImages,
            string caption = "",
            CancellationToken cancellationToken = default)
        {
            LastErrorMessage = string.Empty;
            if (!_dbContext.CanAccessCourseChat(userId, courseId))
            {
                LastErrorMessage = "Bạn không có quyền gửi ảnh trong phòng chat này.";
                return null;
            }

            var images = (compressedImages ?? Array.Empty<CompressedChatImageResult>())
                .Where(image => image != null)
                .ToList();

            if (images.Count == 0)
            {
                LastErrorMessage = "Vui lòng chọn ít nhất một ảnh để gửi.";
                return null;
            }

            if (images.Count > ChatImageHelper.MaxImageCountPerMessage)
            {
                LastErrorMessage = $"Chỉ được gửi tối đa {ChatImageHelper.MaxImageCountPerMessage} ảnh trong một tin nhắn.";
                return null;
            }

            foreach (var image in images)
            {
                if (string.IsNullOrWhiteSpace(image.FilePath) || !File.Exists(image.FilePath))
                {
                    LastErrorMessage = "Không tìm thấy ảnh đã xử lý để gửi.";
                    return null;
                }

                if (!ChatImageHelper.IsSupportedImage(image.OriginalFileName) && !ChatImageHelper.IsSupportedImage(image.FilePath))
                {
                    LastErrorMessage = "Chỉ hỗ trợ gửi ảnh JPG, JPEG hoặc PNG.";
                    return null;
                }
            }

            var saveTasks = images.Select(image => Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var saved = _fileStorageService.SaveChatImage(courseId, userId, image.FilePath, image.OriginalFileName);
                return new ChatImageAttachmentModel
                {
                    Url = saved.SavedPath,
                    Name = saved.SavedFileName,
                    Size = saved.FileSize,
                    Mime = ChatImageHelper.GetMimeType(saved.SavedPath)
                };
            }, cancellationToken)).ToArray();

            ChatImageAttachmentModel[] attachments = await Task.WhenAll(saveTasks);
            string attachmentJson = ChatImageHelper.SerializeImageAttachments(attachments);
            long totalFileSize = attachments.Sum(item => item.Size);
            string fileName = attachments.Length == 1 ? attachments[0].Name : $"{attachments.Length} ảnh";

            ChatMessageModel? message = await _dbContext.SendChatImageGroupMessageAsync(
                courseId,
                userId,
                caption ?? string.Empty,
                attachmentJson,
                fileName,
                totalFileSize,
                ChatImageHelper.ImageGroupMimeType,
                cancellationToken);

            if (message == null)
            {
                LastErrorMessage = "Gửi ảnh thất bại.";
                return null;
            }

            message.ImageAttachments = attachments.ToList();

            try
            {
                await Task.Run(() => _dbContext.LogUserActivity(userId, "CHAT_USE", $"Gửi {attachments.Length} ảnh chat trong khóa học ID={courseId}", string.Empty), cancellationToken);
            }
            catch
            {
                // Logging failure must not block a successful image group message.
            }

            return message;
        }

        public async Task<int> CreatePollAsync(int teacherId, int courseId, string question, IReadOnlyList<string> options, CancellationToken cancellationToken = default)
        {
            LastErrorMessage = string.Empty;
            if (teacherId <= 0 || courseId <= 0)
            {
                LastErrorMessage = "Thông tin người tạo hoặc khóa học không hợp lệ.";
                return 0;
            }

            if (string.IsNullOrWhiteSpace(question))
            {
                LastErrorMessage = "Câu hỏi vote không được để trống.";
                return 0;
            }

            var cleanedOptions = (options ?? Array.Empty<string>())
                .Where(option => !string.IsNullOrWhiteSpace(option))
                .Select(option => option.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToList();

            if (cleanedOptions.Count < 2)
            {
                LastErrorMessage = "Vote cần ít nhất 2 đáp án khác nhau.";
                return 0;
            }

            if (!_dbContext.CanCreateCoursePoll(teacherId, courseId))
            {
                LastErrorMessage = "Chỉ giảng viên phụ trách khóa học mới được tạo vote.";
                return 0;
            }

            int messageId = await _dbContext.CreatePollMessageAsync(teacherId, courseId, question.Trim(), cleanedOptions, cancellationToken);
            if (messageId <= 0)
            {
                LastErrorMessage = "Tạo vote thất bại.";
                return 0;
            }

            await _dbContext.LogUserActivityAsync(teacherId, "CHAT_USE", $"Tạo vote trong khóa học ID={courseId}", string.Empty, cancellationToken);
            return messageId;
        }

        public async Task<bool> VotePollAsync(int userId, int pollId, int optionId, CancellationToken cancellationToken = default)
        {
            LastErrorMessage = string.Empty;
            if (userId <= 0 || pollId <= 0 || optionId <= 0)
            {
                LastErrorMessage = "Lựa chọn vote không hợp lệ.";
                return false;
            }

            bool success = await _dbContext.VotePollAsync(userId, pollId, optionId, cancellationToken);
            if (!success)
            {
                LastErrorMessage = "Không thể ghi nhận vote. Vote có thể đã đóng hoặc bạn không có quyền tham gia.";
                return false;
            }

            try
            {
                string displayName = await _dbContext.GetUserDisplayNameAsync(userId, cancellationToken);
                string caption = string.IsNullOrWhiteSpace(displayName)
                    ? "Một người vừa bình chọn"
                    : $"{displayName} vừa bình chọn";
                await _dbContext.TryCreatePollBumpMessageAsync(userId, pollId, caption, cancellationToken);
            }
            catch
            {
                // Poll bump is a UX side-effect. Never rollback or fail the main vote action
                // if the automated bump is blocked by cooldown or a transient database issue.
            }

            await _dbContext.LogUserActivityAsync(userId, "CHAT_USE", $"Tham gia vote ID={pollId}", string.Empty, cancellationToken);
            return true;
        }

        public async Task<bool> ClosePollAsync(int teacherId, int pollId, CancellationToken cancellationToken = default)
        {
            LastErrorMessage = string.Empty;
            if (teacherId <= 0 || pollId <= 0)
            {
                LastErrorMessage = "Thông tin vote không hợp lệ.";
                return false;
            }

            bool success = await _dbContext.ClosePollAsync(teacherId, pollId, cancellationToken);
            if (!success)
            {
                LastErrorMessage = "Không thể đóng vote. Chỉ giảng viên phụ trách khóa học mới được đóng vote đang mở.";
                return false;
            }

            try
            {
                await _dbContext.TryCreatePollBumpMessageAsync(teacherId, pollId, "Giảng viên đã đóng bình chọn", cancellationToken);
            }
            catch
            {
                // Closing the poll is the primary business action. Bump creation is best-effort
                // and must not make the successful close look like a failure to the teacher.
            }

            await _dbContext.LogUserActivityAsync(teacherId, "CHAT_USE", $"Đóng vote ID={pollId}", string.Empty, cancellationToken);
            return true;
        }

        private static bool ValidateFile(string sourceFilePath, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                error = "Không tìm thấy file để gửi.";
                return false;
            }

            var fileInfo = new FileInfo(sourceFilePath);
            const long maxSize = 20 * 1024 * 1024; // 20MB
            if (fileInfo.Length <= 0)
            {
                error = "File rỗng, không thể gửi.";
                return false;
            }

            if (fileInfo.Length > maxSize)
            {
                error = "File vuot qua gioi han 20MB.";
                return false;
            }

            string ext = Path.GetExtension(fileInfo.Name).ToLowerInvariant();
            string[] allowed = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".png", ".jpg", ".jpeg", ".txt", ".zip", ".rar" };
            if (Array.IndexOf(allowed, ext) < 0)
            {
                error = "Dinh dang file chua duoc ho tro.";
                return false;
            }

            return true;
        }

        private static string ResolveMimeType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".txt" => "text/plain",
                ".zip" => "application/zip",
                ".rar" => "application/vnd.rar",
                _ => "application/octet-stream"
            };
        }
    }
}
