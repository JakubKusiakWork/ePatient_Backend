using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using ePatientApi.DataAccess;
using ePatientApi.Models;

namespace ePatientApi.Controllers
{
    [ApiController]
    [Route("api/patientLogin")]
    public class LoginController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<LoginController> _logger;

        public LoginController(AppDbContext context, IConfiguration config, ILogger<LoginController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _configuration = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private static string NormalizeForCompare(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Replace(".", string.Empty).Replace("/", string.Empty).Trim().ToLowerInvariant();
        }

        private async Task<RegisteredPatient?> FindRegisteredPatientByAnyIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            var normalized = NormalizeForCompare(id);

            var byBirth = await _context.RegisteredPatients
                .FirstOrDefaultAsync(p => p.BirthNumber != null &&
                    (p.BirthNumber.Replace("/", string.Empty).ToLower() == normalized || p.BirthNumber.ToLower() == id.ToLower()));

            if (byBirth != null)
            {
                return byBirth;
            }

            var byUsername = await _context.RegisteredPatients
                .FirstOrDefaultAsync(p => p.Username != null && p.Username.Replace(".", string.Empty).ToLower() == normalized);

            if (byUsername != null)
            {
                return byUsername;
            }

            var byFullName = await _context.RegisteredPatients
                .FirstOrDefaultAsync(p => (p.FirstName + " " + p.LastName).ToLower() == normalized);

            return byFullName;
        }

        /// <summary>
        /// Authenticates user and generates a JWT token.
        /// </summary>
        /// <param name="loginUserData">User credentials.</param>
        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginUser loginUserData)
        {
            try
            {
                _logger.LogInformation("Received login request for UserName: {UserName}", loginUserData?.UserName);

                if (loginUserData == null || string.IsNullOrWhiteSpace(loginUserData.UserName) || string.IsNullOrWhiteSpace(loginUserData.Password))
                {
                    _logger.LogWarning("Invalid login data: Missing username or password.");
                    return BadRequest(new { error = "Invalid login data. Username and password are required." });
                }

                var user = await FindRegisteredPatientByAnyIdAsync(loginUserData.UserName);
                if (user == null)
                {
                    _logger.LogWarning("Login failed: User not found for UserName: {UserName}", loginUserData.UserName);
                    return Unauthorized(new { error = "Invalid username or password." });
                }

                if (!BCrypt.Net.BCrypt.Verify(loginUserData.Password, user.HashedPassword))
                {
                    _logger.LogWarning("Login failed: Invalid password for UserName: {UserName}", loginUserData.UserName);
                    return Unauthorized(new { error = "Invalid username or password." });
                }

                var token = GenerateJwtToken(user);

                var loggedUser = new LoggedUser
                {
                    Username = user.Username,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Role = user.Role
                };

                try
                {
                    _context.LoggedUsers.Add(loggedUser);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("User logged in successfully: {UserName}", user.Username);
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Failed to save login event for UserName: {UserName}", user.Username);
                    return StatusCode(500, new { error = "Failed to log login event. Please try again." });
                }

                return Ok(new
                {
                    message = "Login successful.",
                    token,
                    username = user.Username,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    role = user.Role,
                    birthNumber = user.BirthNumber
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during login for UserName: {UserName}", loginUserData?.UserName);
                return StatusCode(500, new { error = "An unexpected error occurred during login." });
            }
        }
        
        [HttpGet("check-db")]
        public IActionResult CheckDb() => Ok(new { isAwake = true, message = "patientLogin endpoint available" });

        /// <summary>
        /// Generates a JWT token for the authenticated user.
        /// </summary>
        private string GenerateJwtToken(RegisteredPatient user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.BirthNumber ?? user.Username ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Sub, user.BirthNumber ?? user.Username ?? string.Empty),
                new Claim(ClaimTypes.Name, user.Username ?? string.Empty),
            };

            var jwtKey = _configuration["Jwt:Key"] ?? string.Empty;
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}