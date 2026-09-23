using Microsoft.EntityFrameworkCore;
using Textile.Api.Models;

namespace Textile.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
        public DbSet<EmailVerificationToken> EmailVerificationTokens { get; set; }
        public DbSet<UserOtp> UserOtps { get; set; }
        public DbSet<Product> Products { get; set; }
    }
}