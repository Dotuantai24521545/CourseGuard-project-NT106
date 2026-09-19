namespace CourseGuard.Backend.Models
{
    public sealed class LoginResultModel
    {
        public UserModel? User { get; init; }
        public string ErrorCode { get; init; } = string.Empty;
        public bool MustChangePassword { get; init; }
        public bool IsMfaRequired { get; init; }

        public bool Succeeded => User != null && string.IsNullOrWhiteSpace(ErrorCode) && !IsMfaRequired;

        public static LoginResultModel Failed()
        {
            return new LoginResultModel();
        }

        public static LoginResultModel Error(string errorCode)
        {
            return new LoginResultModel { ErrorCode = errorCode ?? string.Empty };
        }

        public static LoginResultModel Success(UserModel user, bool mustChangePassword = false)
        {
            return new LoginResultModel
            {
                User = user,
                MustChangePassword = mustChangePassword
            };
        }

        public static LoginResultModel MfaRequired(UserModel user)
        {
            return new LoginResultModel
            {
                User = user,
                IsMfaRequired = true
            };
        }

        public static LoginResultModel Evaluate(UserModel user)
        {
            if (user.TempPasswordExpiresAt.HasValue)
            {
                if (user.TempPasswordExpiresAt.Value < System.DateTime.Now)
                {
                    return Error(LoginErrorCodes.TempPasswordExpired);
                }

                return Success(user, mustChangePassword: true);
            }

            return Success(user);
        }
    }

    public static class LoginErrorCodes
    {
        public const string TempPasswordExpired = "TEMP_PASSWORD_EXPIRED";
        public const string AccountLocked = "ACCOUNT_LOCKED";
        public const string InvalidMfa = "INVALID_MFA";
        public const string DeviceBlocked = "DEVICE_BLOCKED";
    }
}
