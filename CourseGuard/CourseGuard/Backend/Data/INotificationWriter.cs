namespace CourseGuard.Backend.Data
{
    public interface INotificationWriter
    {
        int Create(
            int userId,
            string title,
            string content,
            string category,
            string notificationType,
            string? sourceType = null,
            int? sourceId = null);
    }
}
