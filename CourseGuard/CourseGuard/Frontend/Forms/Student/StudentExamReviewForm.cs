using System.Globalization;
using System.Text;
using System.Windows.Forms;
using CourseGuard.Backend.Models;
using CourseGuard.Frontend.Theme;

namespace CourseGuard.Frontend.Forms.Student
{
    public class StudentExamReviewForm : Form
    {
        public StudentExamReviewForm(StudentExamReviewModel review)
        {
            Text = "Xem lại bài";
            Width = 820;
            Height = 620;
            StartPosition = FormStartPosition.CenterParent;

            var text = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = AppFonts.Body,
                Text = BuildReviewText(review)
            };

            Controls.Add(text);
            AppColors.ApplyTheme(this);
        }

        private static string BuildReviewText(StudentExamReviewModel review)
        {
            var sb = new StringBuilder();
            sb.AppendLine(review.ExamTitle);
            sb.AppendLine(review.CourseName);
            sb.AppendLine($"Điểm: {review.Score.ToString("0.0", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"Trạng thái: {review.StatusText}");
            sb.AppendLine();

            if (review.Questions.Count == 0)
            {
                sb.AppendLine("Bài thi này chưa có dữ liệu câu hỏi để xem lại.");
                return sb.ToString();
            }

            foreach (var q in review.Questions)
            {
                sb.AppendLine($"Câu {q.DisplayOrder} ({q.Points.ToString("0.##", CultureInfo.InvariantCulture)} điểm)");
                sb.AppendLine(q.QuestionText);
                sb.AppendLine($"A. {q.OptionA}");
                sb.AppendLine($"B. {q.OptionB}");
                sb.AppendLine($"C. {q.OptionC}");
                sb.AppendLine($"D. {q.OptionD}");
                sb.AppendLine($"Bạn chọn: {(string.IsNullOrWhiteSpace(q.SelectedOption) ? "Chưa chọn" : q.SelectedOption)}");
                sb.AppendLine($"Đáp án đúng: {q.CorrectOption}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
