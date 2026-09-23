namespace Textile.Api.Models
{
    public class User
    {
        public int Id { get; set; }

        public string UserName { get; set; }

        public string Email { get; set; }

        public string PasswordHash { get; set; }

        public string Role { get; set; }

        public bool IsActive { get; set; }

        public bool IsEmailVerified { get; set; }
    }
}