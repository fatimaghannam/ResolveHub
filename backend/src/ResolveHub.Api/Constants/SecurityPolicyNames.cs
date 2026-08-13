namespace ResolveHub.Api.Constants;

public static class SecurityPolicyNames //This class stores the names of security policies used in the backend 
{
    public const string FrontendCors = "FrontendCors"; //controls which frontend is allowed to communicate with the backend 
    public const string LoginRateLimit = "LoginRateLimit"; //Limits how many login requests can be made in certain time
    public const string ForgotPasswordRateLimit =
        "ForgotPasswordRateLimit"; //limits repeated forgot-password requests
    public const string ResetPasswordRateLimit =
        "ResetPasswordRateLimit"; //limits repeated password-reset requests 
    public const string AiRateLimit = "AiRateLimit";
}
