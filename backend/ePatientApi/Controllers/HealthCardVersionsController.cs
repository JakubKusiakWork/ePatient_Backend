using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ePatientApi.DataAccess;
using ePatientApi.Interfaces;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System;

namespace ePatientApi.Controllers
{
    /// <summary>
    /// Controller for managing health card versions.
    /// </summary>
    [ApiController]
    [Route("api/healthcards/{id}/[controller]")]
    public class VersionsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IVersioningService _versioningService;
        private readonly ILogger<VersionsController> _logger;

        public VersionsController(AppDbContext context, IVersioningService versioningService, ILogger<VersionsController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _versioningService = versioningService ?? throw new ArgumentNullException(nameof(versioningService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetVersions(int id)
        {
            var exists = await _context.HealthCards.FindAsync(id);
            if (exists == null) return NotFound(new { message = "HealthCard not found." });

            var versions = await _versioningService.GetVersionsAsync(id);
            return Ok(versions);
        }

        [HttpGet("{versionId}")]
        [Authorize]
        public async Task<IActionResult> GetVersion(int id, Guid versionId)
        {
            var exists = await _context.HealthCards.FindAsync(id);
            if (exists == null) return NotFound(new { message = "HealthCard not found." });

            var version = await _versioningService.GetVersionByIdAsync(versionId);
            if (version == null || version.HealthCardId != id) return NotFound(new { message = "Version not found for given healthcard." });
            return Ok(version);
        }

        [HttpPost("restore/{versionId}")]
        [Authorize]
        public async Task<IActionResult> RestoreVersion(int id, Guid versionId)
        {
            var user = User;
            var isDoctor = user.IsInRole("Doctor") || user.IsInRole("doctor");
            var isAdmin = user.IsInRole("Admin") || user.IsInRole("admin");
            if (!isDoctor && !isAdmin) return Forbid();

            var exists = await _context.HealthCards.FindAsync(id);
            if (exists == null) return NotFound(new { message = "HealthCard not found." });

            var version = await _versioningService.GetVersionByIdAsync(versionId);
            if (version == null || version.HealthCardId != id) return NotFound(new { message = "Version not found for given healthcard." });

            var editor = User?.Identity?.Name ?? "system";
            await _versioningService.RestoreVersionAsync(versionId, editor);
            _logger.LogInformation("User {User} restored healthcard {Id} to version {V}", editor, id, version.VersionNumber);
            return Ok(new { message = "HealthCard restored.", version = version.VersionNumber });
        }

        [HttpGet]
        [Route("/api/healthcards/patient/{birth}/versions")]
        [Authorize]
        public async Task<IActionResult> GetVersionsByBirth(string birth)
        {
            if (string.IsNullOrWhiteSpace(birth)) return BadRequest(new { message = "birth is required" });
            var hc = await _context.HealthCards.FirstOrDefaultAsync(h => h.PatientBirthNumber == birth);
            if (hc == null) return NotFound(new { message = "HealthCard not found for patient." });
            var versions = await _versioningService.GetVersionsAsync(hc.HealthCardId);
            return Ok(versions);
        }

        [HttpPost]
        [Route("/api/healthcards/patient/{birth}/versions/restore/{versionId}")]
        [Authorize]
        public async Task<IActionResult> RestoreVersionByBirth(string birth, Guid versionId)
        {
            if (string.IsNullOrWhiteSpace(birth)) return BadRequest(new { message = "birth is required" });
            var hc = await _context.HealthCards.FirstOrDefaultAsync(h => h.PatientBirthNumber == birth);
            if (hc == null) return NotFound(new { message = "HealthCard not found for patient." });
            var version = await _versioningService.GetVersionByIdAsync(versionId);
            if (version == null || version.HealthCardId != hc.HealthCardId) return NotFound(new { message = "Version not found for given healthcard." });

            var isDoctor = User.IsInRole("Doctor") || User.IsInRole("doctor");
            var isAdmin = User.IsInRole("Admin") || User.IsInRole("admin");
            if (!isDoctor && !isAdmin) return Forbid();

            var editor = User?.Identity?.Name ?? "system";
            await _versioningService.RestoreVersionAsync(versionId, editor);
            _logger.LogInformation("User {User} restored healthcard {Id} to version {V}", editor, hc.HealthCardId, version.VersionNumber);
            return Ok(new { message = "HealthCard restored.", version = version.VersionNumber });
        }
    }
}
