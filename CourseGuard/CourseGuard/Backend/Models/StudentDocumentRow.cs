namespace CourseGuard.Backend.Models;
        public sealed class StudentDocumentRow
        {
            public int Id { get; init; }
            public string CourseName { get; init; } = string.Empty;
            public string FileName { get; init; } = string.Empty;
            public string FilePath { get; init; } = string.Empty;
            public string FileType { get; init; } = string.Empty;
            public string UploadedBy { get; init; } = string.Empty;
            public DateTime UploadedAt { get; init; }
            public string ContentType { get; init; } = string.Empty;
            public long FileSize { get; init; }
            public bool HasStoredContent { get; init; }
        }