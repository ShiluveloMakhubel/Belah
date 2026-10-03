using Microsoft.AspNetCore.Mvc;
using Belah_Backend.Data;
using Belah_Backend.Models;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;



namespace Belah_Backend.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class UserController : ControllerBase
	{
		private readonly ApplicationDbContext _context;

		public UserController(ApplicationDbContext context)
		{
			_context = context;
		}

		// 🔐 LOGIN
		[HttpPost("login")]
		public IActionResult Login([FromBody] LoginRequest request)
		{
			string hashedPassword = HashPassword(request.Password);

			var user = _context.Users.FirstOrDefault(u =>
				u.Username == request.Username &&
				u.PasswordHash == hashedPassword);

			if (user == null)
			{
				return Unauthorized("Invalid username or password.");
			}

			return Ok(new { message = "Login successful", userId = user.Id });
		}

		// 📝 REGISTER
		[HttpPost("register")]
		public IActionResult Register([FromBody] RegisterRequest request)
		{
			if (_context.Users.Any(u => u.Username == request.Username || u.Email == request.Email))
			{
				return BadRequest("Username or email already exists.");
			}

			var newUser = new User
			{
				Username = request.Username,
				Email = request.Email,
				PasswordHash = HashPassword(request.Password),
				CreatedAt = DateTime.UtcNow
			};

			_context.Users.Add(newUser);
			_context.SaveChanges();

			return Ok(new { message = "User registered successfully", userId = newUser.Id });
		}

		// 🔐 Password hashing method (SHA256 for demo)
		private string HashPassword(string password)
		{
			using (var sha256 = SHA256.Create())
			{
				byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
				return Convert.ToBase64String(bytes);
			}
		}
	}
}
