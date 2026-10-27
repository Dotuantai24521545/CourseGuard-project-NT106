using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CourseGuard.Backend.Config;

namespace CourseGuard.Backend.Services
{
    public class AiGeneratedQuestion
    {
        public string Content { get; set; } = string.Empty;
        public string Level { get; set; } = "EASY"; // EASY, MEDIUM, HARD
        public string OptionA { get; set; } = string.Empty;
        public string OptionB { get; set; } = string.Empty;
        public string OptionC { get; set; } = string.Empty;
        public string OptionD { get; set; } = string.Empty;
        public string CorrectOption { get; set; } = "A";
        public string Explanation { get; set; } = string.Empty;
    }

    public class AiEssayGradeResult
    {
        public decimal SuggestedScore { get; set; }
        public string Feedback { get; set; } = string.Empty;
        public string Strengths { get; set; } = string.Empty;
        public string Improvements { get; set; } = string.Empty;
    }

    public class AiFlashcard
    {
        public string Term { get; set; } = string.Empty;
        public string Definition { get; set; } = string.Empty;
    }

    public class AiQuestionGeneratorService
    {
        private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromSeconds(45) };
        private readonly HttpClient _httpClient;
        private const string OpenRouterUrl = "https://openrouter.ai/api/v1/chat/completions";

        public AiQuestionGeneratorService() : this(SharedClient) { }

        internal AiQuestionGeneratorService(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        private async Task<string> CallOpenRouterAsync(string systemPrompt, string userPrompt)
        {
            string apiKey = AppEnvironment.GetRequired("OPENROUTER_API_KEY");
            string model = AppEnvironment.GetOptional("OPENROUTER_MODEL") ?? "openrouter/free";
            var payload = new
            {
                model,
                messages = new[]
                {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = userPrompt }
        },
                temperature = 0.3
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, OpenRouterUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Add("HTTP-Referer", "https://courseguard.edu.vn");
            request.Headers.Add("X-Title", "CourseGuard AI Engine");
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            string text;
            try
            {
                using var response = await _httpClient.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
                    throw new Exception("Tài khoản đã hết Quota sử dụng AI. Vui lòng kiểm tra lại API Key.");
                if (!response.IsSuccessStatusCode)
                    throw new Exception($"OpenRouter trả về lỗi HTTP {(int)response.StatusCode}. Vui lòng thử lại.");

                string responseBody = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseBody);
                text = doc.RootElement.GetProperty("choices")[0].GetProperty("message")
                    .GetProperty("content").GetString()?.Trim() ?? string.Empty;
            }
            catch (TaskCanceledException)
            {
                throw new Exception("Kết nối đến AI bị quá hạn (Timeout). Vui lòng kiểm tra mạng và thử lại.");
            }

            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("OpenRouter không trả về nội dung. Vui lòng thử lại.");

            if (text.StartsWith("```", StringComparison.Ordinal))
            {
                int firstNewLine = text.IndexOf('\n');
                if (firstNewLine >= 0) text = text[(firstNewLine + 1)..];
                if (text.EndsWith("```", StringComparison.Ordinal)) text = text[..^3];
            }
            return text.Trim();
        }

        public async Task<List<AiGeneratedQuestion>> GenerateQuestionsAsync(string topicContent, int easyCount, int mediumCount, int hardCount)
        {
            if (string.IsNullOrWhiteSpace(topicContent)) return new List<AiGeneratedQuestion>();

            string systemPrompt = "Bạn là chuyên gia giáo dục biên soạn câu hỏi trắc nghiệm tiếng Việt. Trả về định dạng JSON thuần túy danh sách các câu hỏi trong đó mỗi câu có content, level (EASY/MEDIUM/HARD), optionA, optionB, optionC, optionD, correctOption (A/B/C/D), explanation.";
            string userPrompt = "Nội dung: " + topicContent + "\nKhởi tạo: " + easyCount + " EASY, " + mediumCount + " MEDIUM, " + hardCount + " HARD.";

            string rawJson = await CallOpenRouterAsync(systemPrompt, userPrompt);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            try
            {
                var questions = JsonSerializer.Deserialize<List<AiGeneratedQuestion>>(rawJson, options);
                if (questions == null || questions.Count == 0 || questions.Exists(q => q == null
                    || string.IsNullOrWhiteSpace(q.Content) || string.IsNullOrWhiteSpace(q.OptionA)
                    || string.IsNullOrWhiteSpace(q.OptionB) || string.IsNullOrWhiteSpace(q.OptionC)
                    || string.IsNullOrWhiteSpace(q.OptionD) || q.CorrectOption is not ("A" or "B" or "C" or "D")))
                    throw new InvalidOperationException("AI trả về dữ liệu thiếu thông tin. Vui lòng thử lại.");
                return questions;
            }
            catch (JsonException)
            {
                throw new Exception("AI trả về sai định dạng dữ liệu (không phải JSON). Vui lòng thử tạo lại.");
            }
        }

        //  1. AI Chấm bài Tự luận (AI Essay Grading)
        public async Task<AiEssayGradeResult> GradeEssayAsync(string questionText, string sampleAnswer, string studentSubmission, decimal maxPoints)
        {
            if (maxPoints <= 0) throw new ArgumentOutOfRangeException(nameof(maxPoints));
            string systemPrompt = "Bạn là trợ lý chấm thi tự luận chuyên nghiệp. Hãy so sánh bài làm của sinh viên với đáp án mẫu và chấm điểm chính xác. Trả về JSON thuần túy có thuộc tính suggestedScore (số), feedback (chuỗi), strengths (chuỗi), improvements (chuỗi).";
            string userPrompt = "Đề bài: " + questionText + "\nĐáp án mẫu: " + sampleAnswer + "\nBài nộp sinh viên: " + studentSubmission + "\nĐiểm tối đa: " + maxPoints;

            string rawJson = await CallOpenRouterAsync(systemPrompt, userPrompt);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            try
            {
                var result = JsonSerializer.Deserialize<AiEssayGradeResult>(rawJson, options);
                if (result == null || result.SuggestedScore < 0 || result.SuggestedScore > maxPoints
                    || string.IsNullOrWhiteSpace(result.Feedback))
                    throw new InvalidOperationException("AI trả về kết quả chấm điểm thiếu thông tin. Vui lòng thử lại.");
                return result;
            }
            catch (JsonException)
            {
                throw new Exception("AI trả về sai định dạng dữ liệu (không phải JSON). Vui lòng thử tạo lại.");
            }
        }

        // 2. Trợ lý Học tập 24/7 (Virtual Course Tutor)
        public async Task<string> AskVirtualTutorAsync(string courseName, string lectureContext, string studentQuestion)
        {
            string systemPrompt = "Bạn là Trợ lý Học tập AI 24/7 môn " + courseName + ". Hãy giải đáp thắc mắc của sinh viên một cách dễ hiểu, tận tâm và bám sát kiến thức giáo trình.";
            string userPrompt = "Tài liệu tham khảo môn học:\n" + lectureContext + "\n\nCâu hỏi sinh viên: " + studentQuestion;

            string answer = await CallOpenRouterAsync(systemPrompt, userPrompt);
            return answer;
        }

        // 3. Tự động Tạo Thẻ Ôn tập (AI Flashcards Generator)
        public async Task<List<AiFlashcard>> GenerateFlashcardsAsync(string lectureContent, int count = 5)
        {
            string systemPrompt = "Bạn là chuyên gia tổng hợp kiến thức. Hãy trích xuất các thuật ngữ và định nghĩa quan trọng nhất từ bài giảng thành bộ thẻ ghi nhớ (Flashcards). Trả về JSON thuần túy danh sách các thẻ có thuộc tính term và definition.";
            string userPrompt = "Tài liệu bài giảng:\n" + lectureContent + "\n\nHãy tạo " + count + " thẻ flashcard.";

            string rawJson = await CallOpenRouterAsync(systemPrompt, userPrompt);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            try
            {
                var cards = JsonSerializer.Deserialize<List<AiFlashcard>>(rawJson, options);
                if (cards == null || cards.Count == 0 || cards.Exists(c => c == null
                    || string.IsNullOrWhiteSpace(c.Term) || string.IsNullOrWhiteSpace(c.Definition)))
                    throw new InvalidOperationException("AI trả về flashcard thiếu thông tin. Vui lòng thử lại.");
                return cards;
            }
            catch (JsonException)
            {
                throw new Exception("AI trả về sai định dạng dữ liệu (không phải JSON). Vui lòng thử tạo lại.");
            }
        }
    }
}
