using System;

namespace CourseGuard.Backend.Security
{
    public static class OtpRecoveryService
    {
        public static int GenerateOtp(int studentId, int attemptId)
        {
            // Simple offline-consistent hash algorithm based on studentId, attemptId, and the current date (so it rotates daily)
            int day = DateTime.Today.DayOfYear;
            int year = DateTime.Today.Year;
            
            long seed = (long)studentId * 7919 + (long)attemptId * 5653 + (long)day * 997 + (long)year * 103;
            
            // Ensure a 6-digit positive code
            int otp = (int)(Math.Abs(seed) % 900000) + 100000;
            return otp;
        }

        public static bool VerifyOtp(int studentId, int attemptId, string inputOtp)
        {
            if (int.TryParse(inputOtp, out int code))
            {
                return code == GenerateOtp(studentId, attemptId);
            }
            return false;
        }
    }
}
