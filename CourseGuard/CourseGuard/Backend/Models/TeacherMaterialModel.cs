using System;

namespace CourseGuard.Backend.Models
{
    public class TeacherMaterialModel
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public byte[]? FileContent { get; set; }
        public bool HasStoredContent { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}
