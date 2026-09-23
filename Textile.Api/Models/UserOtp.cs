namespace Textile.Api.Models
{
    public class UserOtp
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public string OtpHash { get; set; }

        public string Purpose { get; set; }

        public DateTime ExpiresAt { get; set; }

        public bool IsUsed { get; set; }

        public int AttemptCount { get; set; }

        public DateTime CreatedAt { get; set; }

        public User User { get; set; }
    }
}