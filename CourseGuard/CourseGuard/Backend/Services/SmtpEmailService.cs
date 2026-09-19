using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using CourseGuard.Backend.Config;
using System.Threading.Tasks;

namespace CourseGuard.Backend.Services
{
    public class SmtpEmailService
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _username;
        private readonly string _password;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public SmtpEmailService()
        {
            _host = AppEnvironment.GetRequired("SMTP_HOST");
            _port = int.TryParse(AppEnvironment.GetOptional("SMTP_PORT"), out int parsedPort) ? parsedPort : 587;
            _username = AppEnvironment.GetRequired("SMTP_USER");
            _password = AppEnvironment.GetRequired("SMTP_PASS");
            _fromEmail = Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL") ?? _username;
            _fromName = Environment.GetEnvironmentVariable("SMTP_FROM_NAME") ?? "CourseGuard Admin";
        }

        public bool SendEmail(string toEmail, string subject, string body, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(toEmail))
            {
                errorMessage = "Email người nhận không hợp lệ.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_username) || string.IsNullOrWhiteSpace(_password))
            {
                errorMessage = "Thiếu cấu hình SMTP_USER/SMTP_PASS.";
                return false;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(System.Text.Encoding.UTF8, _fromName, _fromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            var bodyPart = new TextPart("plain");
            bodyPart.SetText(System.Text.Encoding.UTF8, body);
            message.Body = bodyPart;

            try
            {
                using var client = new SmtpClient();
                client.Connect(_host, _port, SecureSocketOptions.StartTls);
                client.Authenticate(_username, _password);
                client.Send(message);
                client.Disconnect(true);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Gửi SMTP thất bại: {ex.Message}";
                return false;
            }
        }

        public async Task<(bool Success, string ErrorMessage)> SendEmailAsync(string toEmail, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                return (false, "Email người nhận không hợp lệ.");
            }

            if (string.IsNullOrWhiteSpace(_username) || string.IsNullOrWhiteSpace(_password))
            {
                return (false, "Thiếu cấu hình SMTP_USER/SMTP_PASS.");
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(System.Text.Encoding.UTF8, _fromName, _fromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            var bodyPart = new TextPart("plain");
            bodyPart.SetText(System.Text.Encoding.UTF8, body);
            message.Body = bodyPart;

            try
            {
                using var client = new SmtpClient();
                await client.ConnectAsync(_host, _port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_username, _password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Gửi SMTP thất bại: {ex.Message}");
            }
        }
    }
}
