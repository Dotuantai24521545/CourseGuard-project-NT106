using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace CourseGuard.Backend.Services
{
    public class EmailItem
    {
        public string Sender { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Snippet { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
    }

    public class GmailServiceHelper
    {
        private static string[] Scopes = { GmailService.Scope.GmailReadonly };
        private static string ApplicationName = "CourseGuard Teacher Dashboard";

        public async Task<List<EmailItem>> GetLatestEmailsAsync(int maxResults = 10)
        {
            UserCredential credential;

            string credentialsPath = "credentials.json";
            if (!File.Exists(credentialsPath))
            {
                throw new FileNotFoundException($"Cannot find {credentialsPath}. Please download it from Google Cloud Console and place it in the application's base directory.");
            }

            using (var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read))
            {
                string credPath = "token.json";
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true));
            }

            var service = new GmailService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            var emailList = new List<EmailItem>();

            try
            {
                var request = service.Users.Messages.List("me");
                request.LabelIds = "INBOX";
                request.IncludeSpamTrash = false;
                request.MaxResults = maxResults;

                var response = await request.ExecuteAsync();
                if (response.Messages != null && response.Messages.Count > 0)
                {
                    foreach (var messageItem in response.Messages)
                    {
                        var msgRequest = service.Users.Messages.Get("me", messageItem.Id);
                        var msg = await msgRequest.ExecuteAsync();

                        var headers = msg.Payload.Headers;
                        var subject = headers.FirstOrDefault(h => h.Name == "Subject")?.Value ?? "(No Subject)";
                        var sender = headers.FirstOrDefault(h => h.Name == "From")?.Value ?? "(Unknown Sender)";
                        var date = headers.FirstOrDefault(h => h.Name == "Date")?.Value ?? "";

                        // Cleanup sender name if it has format: "Name" <email@example.com>
                        if (sender.Contains("<") && sender.Contains(">"))
                        {
                            var parts = sender.Split('<');
                            sender = parts[0].Trim(' ', '"');
                        }

                        emailList.Add(new EmailItem
                        {
                            Sender = sender,
                            Subject = subject,
                            Snippet = msg.Snippet,
                            Date = date
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching emails: {ex.Message}");
                throw;
            }

            return emailList;
        }
    }
}
