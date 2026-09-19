using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace CourseGuard.Backend.Services
{
    public static class EmailQueueService
    {
        private static readonly ConcurrentQueue<EmailQueueItem> _queue = new();
        private static readonly SemaphoreSlim _signal = new(0);
        private static readonly SmtpEmailService _emailService = new();
        private static readonly CancellationTokenSource _cts = new();

        static EmailQueueService()
        {
            // Khởi chạy tiến trình gửi email ngầm chạy suốt vòng đời ứng dụng
            Task.Run(ProcessQueueAsync);
        }

        /// <summary>
        /// Thêm email vào hàng đợi gửi ngầm. Phản hồi lập tức dưới 1ms.
        /// </summary>
        public static void QueueEmail(string toEmail, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                return;
            }

            _queue.Enqueue(new EmailQueueItem
            {
                ToEmail = toEmail,
                Subject = subject,
                Body = body
            });

            _signal.Release();
        }

        private static async Task ProcessQueueAsync()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await _signal.WaitAsync(_cts.Token);
                    if (_queue.TryDequeue(out var item))
                    {
                        // Thực hiện gửi email qua SmtpEmailService bất đồng bộ ngầm
                        await _emailService.SendEmailAsync(item.ToEmail, item.Subject, item.Body);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Tránh crash luồng nền khi có lỗi SMTP/kết nối mạng
                    Console.WriteLine($"[EmailQueueService] Lỗi khi gửi email ngầm: {ex.Message}");
                }
            }
        }

        public static void Shutdown()
        {
            _cts.Cancel();
            _signal.Release();
        }
    }

    public class EmailQueueItem
    {
        public string ToEmail { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
    }
}
