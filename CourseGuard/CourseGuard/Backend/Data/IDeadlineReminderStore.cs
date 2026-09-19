using System;
using System.Collections.Generic;
using CourseGuard.Backend.Models;

namespace CourseGuard.Backend.Data
{
    public interface IDeadlineReminderStore
    {
        void EnsureDeadlineReminderSchema();
        List<DeadlineReminderItem> GetUpcomingDeadlines(int userId, DateTime from, DateTime to);
        int CreateDeadlineReminderNotification(
            int userId,
            DeadlineReminderItem item,
            string remindType,
            string title,
            string content);
    }
}
