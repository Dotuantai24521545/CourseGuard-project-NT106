using System.IO;

namespace CourseGuard.Backend.Services
{
    public readonly record struct MaterialFileValidation(bool IsValid, string ErrorMessage);

    public static class MaterialFilePolicy
    {
        public const long MaxFileSizeBytes = 20L * 1024 * 1024;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".zip", ".rar"
        };

        public static MaterialFileValidation Validate(string fileName, long fileSizeBytes)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return new MaterialFileValidation(false, "Tên file không hợp lệ.");

            if (fileSizeBytes <= 0)
                return new MaterialFileValidation(false, "File rỗng, không thể tải lên.");

            if (fileSizeBytes > MaxFileSizeBytes)
                return new MaterialFileValidation(false, "File vượt quá giới hạn 20MB.");

            string extension = Path.GetExtension(fileName);
            if (!AllowedExtensions.Contains(extension))
                return new MaterialFileValidation(false, "Định dạng file chưa được hỗ trợ.");

            return new MaterialFileValidation(true, string.Empty);
        }

        public static string ResolveMimeType(string fileName)
        {
            return Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".txt" => "text/plain",
                ".zip" => "application/zip",
                ".rar" => "application/vnd.rar",
                _ => "application/octet-stream"
            };
        }
    }
}
