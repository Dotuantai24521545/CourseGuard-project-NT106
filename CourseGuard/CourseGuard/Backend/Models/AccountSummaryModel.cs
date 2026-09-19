namespace CourseGuard.Backend.Models
{
    public class AccountSummaryModel
    {
        public int TotalAccounts { get; set; }
        public int ActiveAccounts { get; set; }
        public int PendingAccounts { get; set; }
        public int TeacherAccounts { get; set; }
        public int StudentAccounts { get; set; }
    }
}
