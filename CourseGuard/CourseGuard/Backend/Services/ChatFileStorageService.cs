using System;
using System.IO;

namespace CourseGuard.Backend.Services
{
    public class ChatFileStorageService
    {
        private readonly string _storageRoot;

        public ChatFileStorageService(string? storageRoot = null)
        {
            _storageRoot = string.IsNullOrWhiteSpace(storageRoot)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "storage", "chat-files")
                : storageRoot;
        }

        public (string SavedPath, string SavedFileName, long FileSize) SaveChatFile(int courseId, string sourceFilePath)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException("Khong tim thay file de tai len.", sourceFilePath);
            }

            string courseFolder = Path.Combine(_storageRoot, $"course-{courseId}");
            Directory.CreateDirectory(courseFolder);

            string originalName = Path.GetFileName(sourceFilePath);
            string fileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}_{originalName}";
            string destination = Path.Combine(courseFolder, fileName);
            File.Copy(sourceFilePath, destination, true);

            var fileInfo = new FileInfo(destination);
            return (destination, originalName, fileInfo.Length);
        }

        public (string SavedPath, string SavedFileName, long FileSize) SaveChatImage(int courseId, int userId, string sourceFilePath, string originalFileName)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException("Khong tim thay anh de tai len.", sourceFilePath);
            }

            string safeOriginalName = string.IsNullOrWhiteSpace(originalFileName)
                ? Path.GetFileName(sourceFilePath)
                : Path.GetFileName(originalFileName);
            string extension = Path.GetExtension(sourceFilePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = Path.GetExtension(safeOriginalName);
            }

            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".jpg";
            }

            string imageFolder = Path.Combine(_storageRoot, "chat_images", $"course-{courseId}", $"user-{userId}");
            Directory.CreateDirectory(imageFolder);

            string fileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            string destination = Path.Combine(imageFolder, fileName);
            File.Copy(sourceFilePath, destination, true);

            var fileInfo = new FileInfo(destination);
            return (destination, safeOriginalName, fileInfo.Length);
        }
    }
}
