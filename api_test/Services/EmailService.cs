using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace api_test.Services
{
    public class EmailService
    {
        private readonly string _fromEmail;
        private readonly string _password;

        public EmailService(IConfiguration configuration)
        {
            _fromEmail = configuration["AppSettings:Email"] ?? throw new Exception("AppSettings:Email is missing!");
            _password = configuration["AppSettings:EmailPassword"] ?? throw new Exception("AppSettings:EmailPassword is missing!");
        }

        public async Task SendOtpAsync(string email, string otp)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("API Test App OTP", _fromEmail));
            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = "Verify your email";
            message.Body = new TextPart("plain") { Text = $"Your OTP code: {otp}" };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync("smtp.gmail.com", 587, false);
            await smtp.AuthenticateAsync(_fromEmail, _password);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }
    }
}