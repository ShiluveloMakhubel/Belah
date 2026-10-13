
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Belah_Backend.Data;
using Belah_Backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.RateLimiting;

namespace Belah_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public UserController(
            ApplicationDbContext context,
            IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [Authorize]
        [HttpGet("me")]
        public IActionResult GetCurrentUser()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
            var user = _context.Users.Where(u => u.Id == userId)
                .Select(u => new { u.Id, u.Username, u.Email, u.Role, u.CreatedAt })
                .FirstOrDefault();
            return user == null ? Unauthorized() : Ok(user);
        }

        [HttpPost("register")]
        [EnableRateLimiting("auth")]
        public IActionResult Register([FromBody] RegisterRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(
                    "Username, email and password are required.");
            }

            string username = request.Username.Trim();
            string email = request.Email.Trim().ToLowerInvariant();
            string password = request.Password;

            if (username.Length > 50)
                return BadRequest("Username cannot exceed 50 characters.");

            if (email.Length > 254 ||
                !new System.ComponentModel.DataAnnotations
                    .EmailAddressAttribute().IsValid(email))
            {
                return BadRequest("Please provide a valid email address.");
            }

            if (password.Length < 8 ||
                Encoding.UTF8.GetByteCount(password) > 72)
            {
                return BadRequest(
                    "Password must be at least 8 characters and no more than 72 UTF-8 bytes.");
            }

            bool exists = _context.Users.Any(u =>
                u.Username == username || u.Email == email);

            if (exists)
                return Ok(new { message = "If registration can be completed, you can sign in with the account." });

            var newUser = new User
            {
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = "Customer", // Customers cannot self-assign or promote to other roles
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            _context.SaveChanges();

            return Ok(new
            {
                message = "If registration can be completed, you can sign in with the account."
            });
        }

        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(
                    "Username and password are required.");
            }

            string username = request.Username.Trim();

            var user = _context.Users.FirstOrDefault(u =>
                u.Username == username);

            if (user == null)
                return Unauthorized("Invalid username or password.");

            bool passwordValid;

            // Verify existing BCrypt hashes.
            if (user.PasswordHash.StartsWith(
                    "$2", StringComparison.Ordinal))
            {
                try
                {
                    passwordValid = BCrypt.Net.BCrypt.Verify(
                        request.Password, user.PasswordHash);
                }
                catch (BCrypt.Net.SaltParseException)
                {
                    passwordValid = false;
                }
            }
            else
            {
                // Support legacy SHA-256 hashes during migration.
                string legacyHash;

                using (var sha256 = SHA256.Create())
                {
                    byte[] bytes = sha256.ComputeHash(
                        Encoding.UTF8.GetBytes(request.Password));

                    legacyHash = Convert.ToBase64String(bytes);
                }

                byte[] expected =
                    Encoding.UTF8.GetBytes(user.PasswordHash);
                byte[] actual = Encoding.UTF8.GetBytes(legacyHash);

                passwordValid =
                    expected.Length == actual.Length &&
                    CryptographicOperations.FixedTimeEquals(
                        expected, actual);

                // Upgrade legacy hashes after successful login.
                if (passwordValid &&
                    Encoding.UTF8.GetByteCount(request.Password) <= 72)
                {
                    user.PasswordHash =
                        BCrypt.Net.BCrypt.HashPassword(request.Password);

                    _context.SaveChanges();
                }
            }

            if (!passwordValid)
                return Unauthorized("Invalid username or password.");

            // Create a signed JWT.
            string jwtKey = _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException(
                    "JWT signing key is not configured.");

            string issuer =
                _configuration["Jwt:Issuer"] ?? "BelahApi";

            string audience =
                _configuration["Jwt:Audience"] ?? "BelahWebsite";

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey));

            var credentials = new SigningCredentials(
                key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    user.Id.ToString()),

                new Claim(ClaimTypes.Name, user.Username),

                new Claim(ClaimTypes.Email, user.Email),

                new Claim(ClaimTypes.Role, user.Role)
            };

            DateTime expiresAt = DateTime.UtcNow.AddHours(1);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            string tokenString =
                new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new
            {
                message = "Login successful",
                token = tokenString,
                expiresAt,
                userId = user.Id,
                username = user.Username,
                role = user.Role
            });
        }
    }
}
