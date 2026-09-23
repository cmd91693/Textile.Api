using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Textile.Api.Data;
using Textile.Api.DTOs;
using Textile.Api.Models;
using Textile.Api.Services;

namespace Textile.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService;

        public AuthController(
            AppDbContext context,
            IConfiguration configuration,
            EmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
        }

        // REGISTER
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            // 1. Check email already exists
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(x => x.Email == request.Email);

            if (existingUser != null)
            {
                return BadRequest("Email already exists.");
            }

            // 2. Create User
            var user = new User
            {
                UserName = request.UserName,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = request.Role,
                IsActive = true,
                IsEmailVerified = false
            };

            _context.Users.Add(user);

            // Save first because we need UserId
            await _context.SaveChangesAsync();

            // 3. Generate 6 digit OTP
            var otp = RandomNumberGenerator
                .GetInt32(100000, 1000000)
                .ToString();

            // 4. Hash OTP
            var otpHash = BCrypt.Net.BCrypt.HashPassword(otp);

            // 5. Create OTP record
            var userOtp = new UserOtp
            {
                UserId = user.Id,
                OtpHash = otpHash,
                Purpose = "EMAIL_VERIFICATION",
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                IsUsed = false,
                AttemptCount = 0,
                CreatedAt = DateTime.UtcNow
            };

            _context.UserOtps.Add(userOtp);

            await _context.SaveChangesAsync();

            // TEMPORARY
            // Later this OTP will be sent through email.
            //return Ok(new
            //{
            //    message = "User registered successfully. OTP generated.",
            //    userId = user.Id,
            //    otp = otp
            //});

            // Send OTP to user's email
            await _emailService.SendOtpEmail(
                user.Email,
                otp);

            return Ok(new
            {
                message = "User registered successfully. OTP has been sent to your email.",
                userId = user.Id
            });
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail(
            VerifyEmailRequest request)
        {
            // 1. Find user
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Email == request.Email);

            if (user == null)
            {
                return BadRequest("Invalid email or OTP.");
            }

            // 2. Already verified?
            if (user.IsEmailVerified)
            {
                return BadRequest("Email is already verified.");
            }

            // 3. Get latest unused verification OTP
            var otpRecord = await _context.UserOtps
                .Where(x =>
                    x.UserId == user.Id &&
                    x.Purpose == "EMAIL_VERIFICATION" &&
                    !x.IsUsed)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            if (otpRecord == null)
            {
                return BadRequest("OTP not found. Please request a new OTP.");
            }

            // 4. Check expiry
            if (otpRecord.ExpiresAt < DateTime.UtcNow)
            {
                return BadRequest("OTP has expired. Please request a new OTP.");
            }

            // 5. Check maximum attempts
            if (otpRecord.AttemptCount >= 3)
            {
                return BadRequest("Maximum OTP attempts exceeded. Please request a new OTP.");
            }

            // 6. Increase attempt count
            otpRecord.AttemptCount++;

            // 7. Verify OTP
            bool otpValid = BCrypt.Net.BCrypt.Verify(
                request.Otp,
                otpRecord.OtpHash);

            if (!otpValid)
            {
                await _context.SaveChangesAsync();

                return BadRequest("Invalid OTP.");
            }

            // 8. Mark email verified
            user.IsEmailVerified = true;

            // 9. Mark OTP as used
            otpRecord.IsUsed = true;

            await _context.SaveChangesAsync();

            return Ok("Email verified successfully.");
        }

        [HttpPost("resend-otp")]
        public async Task<IActionResult> ResendOtp(
            ResendOtpRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Email == request.Email);

            // Don't reveal whether email exists
            if (user == null)
            {
                return Ok(
                    "If the email exists, a new OTP has been sent.");
            }

            if (user.IsEmailVerified)
            {
                return BadRequest("Email is already verified.");
            }

            // Check last OTP sent time
            var lastOtp = await _context.UserOtps
                .Where(x =>
                    x.UserId == user.Id &&
                    x.Purpose == "EMAIL_VERIFICATION")
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            // 60 seconds resend cooldown
            if (lastOtp != null &&
                lastOtp.CreatedAt > DateTime.UtcNow.AddSeconds(-60))
            {
                return BadRequest(
                    "Please wait 60 seconds before requesting a new OTP.");
            }

            // Generate new 6 digit OTP
            var otp = RandomNumberGenerator
                .GetInt32(100000, 1000000)
                .ToString();

            // Hash OTP
            var otpHash = BCrypt.Net.BCrypt.HashPassword(otp);

            var userOtp = new UserOtp
            {
                UserId = user.Id,
                OtpHash = otpHash,
                Purpose = "EMAIL_VERIFICATION",
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                IsUsed = false,
                AttemptCount = 0,
                CreatedAt = DateTime.UtcNow
            };

            _context.UserOtps.Add(userOtp);

            await _context.SaveChangesAsync();

            // TEMPORARY
            // Later OTP will be sent by email.
            //return Ok(new
            //{
            //    message = "New OTP generated.",
            //    otp = otp
            //});

            // Send OTP to user's email
            await _emailService.SendOtpEmail(
                user.Email,
                otp);

            return Ok(new
            {
                message = "A new OTP has been sent to your email."
            });
        }

        // LOGIN
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Email == request.Email);

            if (user == null)
            {
                return Unauthorized("Invalid email or password.");
            }

            if (!user.IsActive)
            {
                return Unauthorized("User is inactive.");
            }

            if (!user.IsEmailVerified)
            {
                return Unauthorized("Please verify your email before login.");
            }

            bool passwordValid = BCrypt.Net.BCrypt.Verify(
                request.Password,
                user.PasswordHash);

            if (!passwordValid)
            {
                return Unauthorized("Invalid email or password.");
            }

            var token = GenerateToken(user);

            var response = new LoginResponse
            {
                Token = token,
                UserName = user.UserName,
                Role = user.Role
            };

            return Ok(response);
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(
            ForgotPasswordRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Email == request.Email);

            // Security reason: user exist karta hai ya nahi,
            // same response dena better hai.
            if (user == null)
            {
                return Ok("If the email exists, a password reset link will be sent.");
            }

            // Generate secure random token
            var randomBytes = new byte[32];

            using (var random = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                random.GetBytes(randomBytes);
            }

            var token = Convert.ToBase64String(randomBytes);

            var resetToken = new PasswordResetToken
            {
                UserId = user.Id,
                Token = token,
                ExpiryDate = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false,
                CreatedDate = DateTime.UtcNow
            };

            _context.PasswordResetTokens.Add(resetToken);

            await _context.SaveChangesAsync();

            // Development purpose only.
            // Production me token email ke through bhejna hai.
            return Ok(new
            {
                message = "Password reset token generated.",
                token = token
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(
            ResetPasswordRequest request)
        {
            var resetToken = await _context.PasswordResetTokens
                .FirstOrDefaultAsync(x =>
                    x.Token == request.Token &&
                    !x.IsUsed &&
                    x.ExpiryDate > DateTime.UtcNow);

            if (resetToken == null)
            {
                return BadRequest("Invalid or expired reset token.");
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Id == resetToken.UserId);

            if (user == null)
            {
                return BadRequest("User not found.");
            }

            // Hash new password
            user.PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

            // Token cannot be reused
            resetToken.IsUsed = true;

            await _context.SaveChangesAsync();

            return Ok("Password reset successfully.");
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(
            ChangePasswordRequest request)
        {
            // JWT se current logged-in user ki ID
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null)
            {
                return Unauthorized();
            }

            int userId = int.Parse(userIdClaim.Value);

            // Database se user find
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null)
            {
                return NotFound("User not found.");
            }

            // Current password verify
            bool currentPasswordValid =
                BCrypt.Net.BCrypt.Verify(
                    request.CurrentPassword,
                    user.PasswordHash);

            if (!currentPasswordValid)
            {
                return BadRequest("Current password is incorrect.");
            }

            // New password hash
            user.PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    request.NewPassword);

            await _context.SaveChangesAsync();

            return Ok("Password changed successfully.");
        }

        [HttpPost("send-verification")]
        public async Task<IActionResult> SendVerification(
            ForgotPasswordRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Email == request.Email);

            if (user == null)
            {
                return Ok("If the email exists, a verification link will be sent.");
            }

            if (user.IsEmailVerified)
            {
                return Ok("Email is already verified.");
            }

            // Generate secure token
            var randomBytes = new byte[32];

            using (var random = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                random.GetBytes(randomBytes);
            }

            var token = Convert.ToBase64String(randomBytes);

            var verificationToken = new EmailVerificationToken
            {
                UserId = user.Id,
                Token = token,
                ExpiryDate = DateTime.UtcNow.AddMinutes(30),
                IsUsed = false,
                CreatedDate = DateTime.UtcNow
            };

            _context.EmailVerificationTokens.Add(verificationToken);

            await _context.SaveChangesAsync();

            // Development/testing only
            return Ok(new
            {
                message = "Verification token generated.",
                token = token
            });
        }

        // GENERATE JWT
        private string GenerateToken(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    user.Id.ToString()),

                new Claim(
                    ClaimTypes.Name,
                    user.UserName),

                new Claim(
                    ClaimTypes.Email,
                    user.Email),

                new Claim(
                    ClaimTypes.Role,
                    user.Role)
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    _configuration["Jwt:Key"]!));

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(
                    Convert.ToDouble(
                        _configuration["Jwt:ExpiryMinutes"])),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }
    }
}