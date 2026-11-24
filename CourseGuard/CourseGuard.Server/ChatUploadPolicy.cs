/// <summary>
/// Validation rules for files and images uploaded to course chat. The server stores the bytes in
/// PostgreSQL, so every rule here runs before anything is written. Kept free of ASP.NET/DB types so
/// it can be unit tested by linking this file into the test project.
/// </summary>
internal static class ChatUploadPolicy
{
    public const int MaxFileBytes = 5 * 1024 * 1024;
    public const int MaxImageGroupBytes = 5 * 1024 * 1024;
    public const int MaxImagesPerMessage = 5;
    public const int MaxCaptionLength = 4000;
    public const int MaxFileNameLength = 250;

    // Same list the WinForms picker allows (ChatController.ValidateFile). The server never trusts the
    // MIME type sent by the client: it is derived from the extension so a file cannot pose as an image.
    private static readonly Dictionary<string, string> AllowedFileTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".txt"] = "text/plain",
        [".zip"] = "application/zip",
        [".rar"] = "application/vnd.rar",
    };

    public static IReadOnlyCollection<string> AllowedExtensions => AllowedFileTypes.Keys;

    public sealed record FileDecision(bool Allowed, string? Error, string FileName = "", string MimeType = "");

    public static FileDecision CheckFile(string? fileName, string? caption, byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0) return Reject("Tệp rỗng.");
        if (bytes.Length > MaxFileBytes) return Reject("Tệp vượt quá 5 MB.");
        if ((caption ?? "").Length > MaxCaptionLength) return Reject("Nội dung kèm tệp quá dài.");
        string name = SafeFileName(fileName);
        if (name.Length == 0 || name.Length > MaxFileNameLength) return Reject("Tên tệp không hợp lệ.");
        if (!AllowedFileTypes.TryGetValue(Path.GetExtension(name), out var mime))
            return Reject("Định dạng tệp chưa được hỗ trợ.");
        if (LooksLikeWindowsExecutable(bytes)) return Reject("Không cho phép gửi tệp thực thi.");
        // The client renders image/* inline, so image extensions must carry real image bytes.
        if (mime.StartsWith("image/", StringComparison.Ordinal) && DetectImageMime(bytes) != mime)
            return Reject("Nội dung ảnh không khớp định dạng tệp.");
        return new FileDecision(true, null, name, mime);
    }

    public sealed record ImageItem(string? FileName, string? MimeType, byte[]? Bytes);

    public static string? CheckImageGroup(string? caption, IReadOnlyList<ImageItem>? items)
    {
        if (items is null || items.Count is < 1 or > MaxImagesPerMessage) return "Mỗi lần gửi từ 1 đến 5 ảnh.";
        if ((caption ?? "").Length > MaxCaptionLength) return "Nội dung kèm ảnh quá dài.";
        long total = 0;
        foreach (var item in items)
        {
            if (item.Bytes is null || item.Bytes.Length == 0) return "Ảnh rỗng.";
            total += item.Bytes.Length;
            if (item.MimeType is not ("image/jpeg" or "image/png")) return "Chỉ hỗ trợ ảnh PNG hoặc JPEG.";
            if (DetectImageMime(item.Bytes) != item.MimeType) return "Nội dung ảnh không khớp định dạng tệp.";
            if (SafeFileName(item.FileName).Length > MaxFileNameLength) return "Tên ảnh không hợp lệ.";
        }
        return total > MaxImageGroupBytes ? "Tổng dung lượng ảnh vượt quá 5 MB." : null;
    }

    public static string SafeFileName(string? fileName)
    {
        // Path.GetFileName only understands the current OS separators; strip both kinds explicitly.
        string name = (fileName ?? "").Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        name = new string(name.Where(c => !char.IsControl(c) && c is not ('<' or '>' or ':' or '"' or '|' or '?' or '*')).ToArray());
        return name.Trim().TrimEnd('.', ' ');
    }

    public static string? DetectImageMime(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return "image/jpeg";
        return null;
    }

    // A renamed .exe still carries the DOS "MZ" stub pointing at a "PE\0\0" header.
    private static bool LooksLikeWindowsExecutable(byte[] bytes)
    {
        if (bytes.Length < 0x40 || bytes[0] != 'M' || bytes[1] != 'Z') return false;
        int offset = BitConverter.ToInt32(bytes, 0x3C);
        return offset > 0 && offset <= bytes.Length - 4
            && bytes[offset] == 'P' && bytes[offset + 1] == 'E' && bytes[offset + 2] == 0 && bytes[offset + 3] == 0;
    }

    private static FileDecision Reject(string error) => new(false, error);
}
