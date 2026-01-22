using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

using ePatientApi.DataAccess;
using ePatientApi.Models;
using System.Text.Json;

namespace ePatientApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthcardController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly Interfaces.IHealthcardService _healthcardService;
        private readonly ILogger<HealthcardController> _logger;

        public HealthcardController(AppDbContext context, Interfaces.IHealthcardService healthcardService, ILogger<HealthcardController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _healthcardService = healthcardService ?? throw new ArgumentNullException(nameof(healthcardService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }


        /// <summary>
        /// Saves the provided health card payload. Currently acts as a stub that validates patient existence (by birth number) if present.
        /// </summary>
        [HttpPost("save")]
        public async Task<IActionResult> SaveHealthCard([FromBody] HealthCard card)
        {
            _logger.LogDebug("SaveHealthCard called with Identity.NationalId={NationalId}", card?.IdentityNationalId);
            try
            {
                var payloadJson = JsonSerializer.Serialize(card);
                _logger.LogDebug("Incoming healthcard payload: {Payload}", payloadJson);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to serialize incoming healthcard payload");
            }
            if (card == null)
            {
                _logger.LogWarning("SaveHealthCard called with null payload");
                return BadRequest(new { message = "Health card payload is required." });
            }

            card.IdentityNationalId ??= string.Empty;
            card.IdentityFirstName ??= string.Empty;
            card.IdentityLastName ??= string.Empty;
            card.ContactEmail ??= string.Empty;
            card.ContactPhone ??= string.Empty;

            var providedId = card.IdentityNationalId?.Trim();
            if (string.IsNullOrWhiteSpace(providedId) && User?.Identity?.IsAuthenticated == true)
            {
                providedId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
                if (!string.IsNullOrWhiteSpace(providedId)) card.IdentityNationalId = providedId;
            }
            if (!string.IsNullOrEmpty(providedId))
            {
                var patient = await _context.RegisteredPatients.FirstOrDefaultAsync(p => p.BirthNumber == providedId || p.Username == providedId);
                if (patient == null)
                {
                    return NotFound(new { message = "Patient not found for provided identifier." });
                }

                card.IdentityNationalId = patient.BirthNumber;
                if (string.IsNullOrWhiteSpace(card.IdentityFirstName) && !string.IsNullOrWhiteSpace(patient.FirstName))
                {
                    card.IdentityFirstName = patient.FirstName;
                }

                if (string.IsNullOrWhiteSpace(card.IdentityLastName) && !string.IsNullOrWhiteSpace(patient.LastName))
                {
                    card.IdentityLastName = patient.LastName;
                }

                if (string.IsNullOrWhiteSpace(card.ContactEmail) && !string.IsNullOrWhiteSpace(patient.Email))
                {
                    card.ContactEmail = patient.Email;
                }

                if (string.IsNullOrWhiteSpace(card.ContactPhone) && !string.IsNullOrWhiteSpace(patient.PhoneNumber))
                {
                    card.ContactPhone = patient.PhoneNumber;
                }
            }

            try
            {
                var saved = await _healthcardService.UpsertAsync(card);
                _logger.LogInformation("Health card upserted for patient {PatientId}", saved?.IdentityNationalId);
                return Ok(new { message = "Health card saved.", card = saved });
            }
            catch (KeyNotFoundException knf)
            {
                _logger.LogWarning("Upsert failed: {Message}", knf.Message);
                return NotFound(new { message = knf.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while saving the health card for NationalId={NationalId}", card?.IdentityNationalId);
                return StatusCode(500, new { message = "An error occurred while saving the health card.", detail = ex.Message });
            }
        }

        /// <summary>
        /// Returns the health card for the currently authenticated user (auto-filled from registered user data if available).
        /// </summary>
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMyHealthCard()
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                return Unauthorized(new { message = "Authentication required." });
            }

            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(new { message = "Could not determine authenticated user identifier." });
            }

            try
            {
                var card = await _healthcardService.GetByPatientIdAsync(id);
                return Ok(card);
            }
            catch (KeyNotFoundException knf)
            {
                return NotFound(new { message = knf.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving the health card.", detail = ex.Message });
            }
        }

        /// <summary>
        /// Returns a PDF export of the authenticated user's health card.
        /// </summary>
        [Authorize]
        [HttpGet("me/pdf")]
        public async Task<IActionResult> GetMyHealthCardPdf()
        {
            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrWhiteSpace(id)) return BadRequest(new { message = "Could not determine authenticated user identifier." });

            try
            {
                var pdf = await _healthcardService.GeneratePdfAsync(id);
                return File(pdf, "application/pdf", "healthcard.pdf");
            }
            catch (KeyNotFoundException knf)
            {
                return NotFound(new { message = knf.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate PDF for user {Id}", id);
                return StatusCode(500, new { message = "Failed to generate PDF.", detail = ex.Message });
            }
        }

        /// <summary>
        /// Returns a PNG QR code export of the authenticated user's health card (compact payload).
        /// </summary>
        [Authorize]
        [HttpGet("me/qr")]
        public async Task<IActionResult> GetMyHealthCardQr()
        {
            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrWhiteSpace(id)) return BadRequest(new { message = "Could not determine authenticated user identifier." });

            try
            {
                var png = await _healthcardService.GenerateQrAsync(id);
                return File(png, "image/png", "healthcard-qr.png");
            }
            catch (KeyNotFoundException knf)
            {
                return NotFound(new { message = knf.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate QR for user {Id}", id);
                return StatusCode(500, new { message = "Failed to generate QR.", detail = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves a health card for the supplied patient id. Currently returns a not-implemented placeholder.
        /// </summary>
        [HttpGet("get/{patientId}")]
        public async Task<IActionResult> GetHealthCard(string patientId)
        {
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return BadRequest(new { message = "Patient identifier is required." });
            }

            // Try to find a registered patient by birth number or username
            var patient = await _context.RegisteredPatients.FirstOrDefaultAsync(p => p.BirthNumber == patientId || p.Username == patientId);
            if (patient == null)
            {
                return NotFound(new { message = "Patient not found." });
            }

            try
            {
                var card = await _healthcardService.GetByPatientIdAsync(patient.BirthNumber ?? patient.Username ?? string.Empty);
                return Ok(card);
            }
            catch (KeyNotFoundException knf)
            {
                return NotFound(new { message = knf.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving the health card.", detail = ex.Message });
            }
        }
    }
}
