using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ePatientApi.DataAccess;
using ePatientApi.Models;

namespace ePatientApi.Controllers
{
    [ApiController]
    [Route("api/patient/{birthNumber}/gp")]
    public class PatientGpController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PatientGpController> _logger;

        public PatientGpController(AppDbContext context, ILogger<PatientGpController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET api/patient/{birthNumber}/gp
        [HttpGet]
        public async Task<IActionResult> GetGp(string birthNumber)
        {
            try
            {
                var patient = await _context.RegisteredPatients
                    .Include(p => p.GpDoctor)
                    .FirstOrDefaultAsync(p => p.BirthNumber == birthNumber);

                if (patient == null)
                {
                    return NotFound(new { message = "Patient not found." });
                }

                if (patient.GpDoctorId == null || patient.GpDoctor == null)
                {
                    return Ok(new { gp = (object?)null });
                }

                var gp = new
                {
                    id = patient.GpDoctor.DoctorId,
                    doctorCode = patient.GpDoctor.DoctorCode,
                    displayName = string.IsNullOrWhiteSpace(patient.GpDoctor.VerifiedFullName)
                        ? (string.IsNullOrWhiteSpace(patient.GpDoctor.VerifiedFirstName) && string.IsNullOrWhiteSpace(patient.GpDoctor.VerifiedLastName)
                            ? string.Concat(patient.GpDoctor.DoctorFirstName ?? string.Empty, " ", patient.GpDoctor.DoctorLastName ?? string.Empty).Trim()
                            : string.Concat(patient.GpDoctor.VerifiedFirstName ?? string.Empty, " ", patient.GpDoctor.VerifiedLastName ?? string.Empty).Trim())
                        : patient.GpDoctor.VerifiedFullName,
                    specialization = patient.GpDoctor.VerifiedSpecialization,
                    email = patient.GpDoctor.DoctorEmail
                };

                return Ok(new { gp });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get GP for patient {BirthNumber}", birthNumber);
                return StatusCode(500, new { message = "Failed to get patient GP." });
            }
        }

        public class AssignGpDto { public int? doctorId { get; set; } }

        // POST api/patient/{birthNumber}/gp
        [HttpPost]
        public async Task<IActionResult> AssignGp(string birthNumber, [FromBody] AssignGpDto dto)
        {
            if (dto == null || dto.doctorId == null)
            {
                return BadRequest(new { message = "Missing doctorId in request." });
            }

            try
            {
                var patient = await _context.RegisteredPatients.FirstOrDefaultAsync(p => p.BirthNumber == birthNumber);
                if (patient == null)
                {
                    return NotFound(new { message = "Patient not found." });
                }

                var doctor = await _context.RegisteredDoctors.FirstOrDefaultAsync(d => d.DoctorId == dto.doctorId.Value);
                if (doctor == null)
                {
                    return BadRequest(new { message = "Doctor not found." });
                }

                var spec = (doctor.VerifiedSpecialization ?? string.Empty).Trim().ToLowerInvariant();
                if (spec != "všeobecné lekárstvo" && spec != "vseobecné lekárstvo" && spec != "vseobecne lekárstvo")
                {
                    return Conflict(new { message = "Selected doctor is not a general practitioner (všeobecné lekárstvo)." });
                }

                patient.GpDoctorId = doctor.DoctorId;
                await _context.SaveChangesAsync();

                var gpDto = new { id = doctor.DoctorId, displayName = doctor.VerifiedFullName ?? (doctor.DoctorFirstName + " " + doctor.DoctorLastName).Trim(), specialization = doctor.VerifiedSpecialization, email = doctor.DoctorEmail };

                return Ok(new { gp = gpDto });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error assigning GP for {BirthNumber}", birthNumber);
                return StatusCode(500, new { message = "Database error while assigning GP." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to assign GP for {BirthNumber}", birthNumber);
                return StatusCode(500, new { message = "Failed to assign GP." });
            }
        }

        // DELETE api/patient/{birthNumber}/gp
        [HttpDelete]
        public async Task<IActionResult> UnassignGp(string birthNumber)
        {
            try
            {
                var patient = await _context.RegisteredPatients.FirstOrDefaultAsync(p => p.BirthNumber == birthNumber);
                if (patient == null)
                {
                    return NotFound(new { message = "Patient not found." });
                }

                patient.GpDoctorId = null;
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unassign GP for {BirthNumber}", birthNumber);
                return StatusCode(500, new { message = "Failed to unassign GP." });
            }
        }
    }
}
