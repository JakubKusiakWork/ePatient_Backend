using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ePatientApi.DataAccess;
using ePatientApi.Models;

namespace ePatientApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DoctorsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DoctorsController> _logger;

        public DoctorsController(AppDbContext context, ILogger<DoctorsController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET api/doctors/verified
        [HttpGet("verified")]
        public async Task<IActionResult> GetVerifiedDoctors()
        {
            try
            {
                var docs = await _context.RegisteredDoctors
                    .Where(d => d.IsVerified)
                    .Select(d => new
                    {
                        id = d.DoctorId,
                        doctorCode = d.DoctorCode,
                        email = d.DoctorEmail,
                        displayName = string.IsNullOrWhiteSpace(d.VerifiedFullName)
                            ? (string.IsNullOrWhiteSpace(d.VerifiedFirstName) && string.IsNullOrWhiteSpace(d.VerifiedLastName)
                                ? string.Concat(d.DoctorFirstName ?? string.Empty, " ", d.DoctorLastName ?? string.Empty).Trim()
                                : string.Concat(d.VerifiedFirstName ?? string.Empty, " ", d.VerifiedLastName ?? string.Empty).Trim())
                            : d.VerifiedFullName,
                        specialization = d.VerifiedSpecialization
                    })
                    .ToListAsync();

                return Ok(docs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load verified doctors");
                return StatusCode(500, new { message = "Failed to load doctors." });
            }
        }

        // GET api/doctors
        [HttpGet]
        public async Task<IActionResult> GetAllDoctors()
        {
            try
            {
                var docs = await _context.RegisteredDoctors
                    .Select(d => new
                    {
                        id = d.DoctorId,
                        doctorCode = d.DoctorCode,
                        email = d.DoctorEmail,
                        displayName = string.IsNullOrWhiteSpace(d.VerifiedFullName)
                            ? (string.IsNullOrWhiteSpace(d.VerifiedFirstName) && string.IsNullOrWhiteSpace(d.VerifiedLastName)
                                ? string.Concat(d.DoctorFirstName ?? string.Empty, " ", d.DoctorLastName ?? string.Empty).Trim()
                                : string.Concat(d.VerifiedFirstName ?? string.Empty, " ", d.VerifiedLastName ?? string.Empty).Trim())
                            : d.VerifiedFullName,
                        isVerified = d.IsVerified,
                        specialization = d.VerifiedSpecialization
                    })
                    .ToListAsync();

                return Ok(docs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load doctors");
                return StatusCode(500, new { message = "Failed to load doctors." });
            }
        }
    }
}
