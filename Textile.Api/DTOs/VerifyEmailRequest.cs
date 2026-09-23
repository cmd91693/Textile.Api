namespace Textile.Api.DTOs
{
    public class VerifyEmailRequest
    {
        public string Email { get; set; }

        public string Otp { get; set; }
    }
}