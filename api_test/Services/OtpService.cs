namespace api_test.Services
{
    public class OtpService
    {
        public string GenerateOtp()
        {
            return new Random().Next(1000, 9999).ToString();
        }
    }
}
