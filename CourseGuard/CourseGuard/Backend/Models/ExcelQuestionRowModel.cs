using System.ComponentModel.DataAnnotations;
using MiniExcelLibs.Attributes;

namespace CourseGuard.Backend.Models
{
    public class ExcelQuestionRowModel
    {
        [Required(ErrorMessage = "Câu hỏi không được để trống.")]
        [ExcelColumn(Name = "Câu hỏi")]
        public string QuestionText { get; set; } = string.Empty;

        [Required(ErrorMessage = "Đáp án A không được để trống.")]
        [ExcelColumn(Name = "Đáp án A")]
        public string OptionA { get; set; } = string.Empty;

        [Required(ErrorMessage = "Đáp án B không được để trống.")]
        [ExcelColumn(Name = "Đáp án B")]
        public string OptionB { get; set; } = string.Empty;

        [Required(ErrorMessage = "Đáp án C không được để trống.")]
        [ExcelColumn(Name = "Đáp án C")]
        public string OptionC { get; set; } = string.Empty;

        [Required(ErrorMessage = "Đáp án D không được để trống.")]
        [ExcelColumn(Name = "Đáp án D")]
        public string OptionD { get; set; } = string.Empty;

        [Required(ErrorMessage = "Đáp án đúng không được để trống.")]
        [RegularExpression("^[A-Da-d]$", ErrorMessage = "Đáp án đúng phải là A, B, C, hoặc D.")]
        [ExcelColumn(Name = "Đáp án đúng")]
        public string CorrectOption { get; set; } = "A";

        [Range(0.1, 100, ErrorMessage = "Điểm phải lớn hơn 0.")]
        [ExcelColumn(Name = "Điểm")]
        public decimal Points { get; set; } = 1m;

        [ExcelColumn(Name = "Độ khó")]
        public string Difficulty { get; set; } = "MEDIUM";

        [ExcelColumn(Name = "Chương")]
        public string? Chapter { get; set; }
    }
}
