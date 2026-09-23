namespace Textile.Api.Models
{
    public class EmailVerificationToken
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public string Token { get; set; }

        public DateTime ExpiryDate { get; set; }

        public bool IsUsed { get; set; }

        public DateTime CreatedDate { get; set; }

        public User User { get; set; }
    }
}