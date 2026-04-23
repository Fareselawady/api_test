namespace api_test.Models
{
    public class VerifyOtpDto
    {
        public string PendingToken { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
    }
}
