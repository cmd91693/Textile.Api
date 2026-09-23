using System.Net;
using System.Net.Mail;
using Textile.Api.Models;

namespace Textile.Api.Services
{
    public class EmailService
    {
        private readonly EmailSettings _settings;

        public EmailService(EmailSettings settings)
        {
            _settings = settings;
        }

        public async Task SendOtpEmail(
            string toEmail,
            string otp)
        {
            using var message = new MailMessage();

            message.From = new MailAddress(
                _settings.FromEmail,
                _settings.FromName);

            message.To.Add(toEmail);

            message.Subject = "Textile - Email Verification OTP";

            message.Body = $"""
                Hello,

                Your email verification OTP is:

                {otp}

                This OTP is valid for 5 minutes.

                If you did not create this account,
                please ignore this email.

                Regards,
                Textile
                """;

            message.IsBodyHtml = false;

            using var smtp = new SmtpClient(
                _settings.Host,
                _settings.Port);

            smtp.EnableSsl = true;

            smtp.Credentials = new NetworkCredential(
                _settings.UserName,
                _settings.Password);

            await smtp.SendMailAsync(message);
        }
    }
}