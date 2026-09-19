using System;

namespace CourseGuard.Backend.Models
{
    public class LoginFrequencyModel
    {
        public DateTime LoginDate { get; set; }
        public int LoginCount { get; set; }
    }
}
