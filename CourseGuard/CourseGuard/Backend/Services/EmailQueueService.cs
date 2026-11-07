using System;
using System.Threading.Tasks;

namespace CourseGuard.Backend.Services
{
    public static class EmailQueueService
    {
        public static async Task<(bool Success, string ErrorMessage)> QueueEmail(string toEmail, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                return (false, "Email người nhận không hợp lệ.");
            }

            var dichvumail = new SmtpEmailService();
            
            return await dichvumail.SendEmailAsync(toEmail, subject, body);
        }
    }
}