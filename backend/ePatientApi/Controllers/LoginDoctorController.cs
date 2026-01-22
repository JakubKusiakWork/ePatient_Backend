using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;

using ePatientApi.DataAccess;
using ePatientApi.Models;

namespace ePatientApi.Controllers
{
    [Route("api/doctorLogin")]
    [ApiController]
    public class LoginDoctorController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly AppDbContext _context;

        /// <summary>
        /// Initializes a new instance of <see cref="LoginDoctorController"/>.
        /// </summary>
        public LoginDoctorController(IConfiguration configuration, AppDbContext context)
        {
            _config = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Authenticates a doctor and generates a JWT token.
        /// </summary>
        /// <param name="doctorData">Doctor login credentials.</param>
        [HttpPost]
        public async Task<IActionResult> DoctorLogin([FromBody] DoctorLogin doctorData)
        {
            if (doctorData == null || string.IsNullOrWhiteSpace(doctorData.DoctorCode) || string.IsNullOrWhiteSpace(doctorData.DoctorPassword))
            {
                return BadRequest("Invalid login data.");
            }

            var doctor = await _context.RegisteredDoctors
                .FirstOrDefaultAsync(d => d.DoctorCode == doctorData.DoctorCode);

            if (doctor == null)
            {
                return Unauthorized("Doctor not found.");
            }

            var isPasswordValid = BCrypt.Net.BCrypt.Verify(doctorData.DoctorPassword, doctor.DoctorHashedPassword);
            if (!isPasswordValid)
            {
                return Unauthorized("Incorrect password.");
            }

            var token = GenerateJwtToken(doctor);
            _context.LoggedDoctors.Add(new LoggedDoctor
            {
                DoctorCode = doctor.DoctorCode,
                DoctorFirstName = doctor.DoctorFirstName,
                DoctorLastName = doctor.DoctorLastName,
                Created_at = DateTime.UtcNow,
                HashedPassword = doctor.DoctorHashedPassword
            });

            await _context.SaveChangesAsync();
            return Ok(new
            {
                message = "Login successful.",
                token,
                firstName = doctor.DoctorFirstName,
                lastName = doctor.DoctorLastName,
                code = doctor.DoctorCode,
                role = doctor.Role
            });
        }

        /// <summary>
        /// Generates a JWT token for the authenticated doctor.
        /// </summary>
        private string GenerateJwtToken(RegisteredDoctor doctor)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, doctor.DoctorCode),
                new Claim(ClaimTypes.NameIdentifier, doctor.DoctorCode),
                new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(doctor.DoctorFirstName) && string.IsNullOrWhiteSpace(doctor.DoctorLastName)
                    ? doctor.DoctorCode
                    : (doctor.DoctorFirstName + " " + doctor.DoctorLastName).Trim()),
                new Claim("firstName", doctor.DoctorFirstName ?? string.Empty),
                new Claim("lastName", doctor.DoctorLastName ?? string.Empty),
                new Claim("verifiedFullName", doctor.VerifiedFullName ?? string.Empty),
                new Claim(ClaimTypes.Role, doctor.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}