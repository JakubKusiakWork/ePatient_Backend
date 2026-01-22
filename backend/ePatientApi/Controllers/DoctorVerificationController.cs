using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ePatientApi.Services;

namespace ePatientApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DoctorVerificationController : ControllerBase
    {
        private readonly DoctorVerificationService _verificationService;

        public DoctorVerificationController(DoctorVerificationService verificationService)
        {
            _verificationService = verificationService;
        }

        [HttpGet("verify")]
        public async Task<IActionResult> Verify([FromQuery] string firstName, [FromQuery] string lastName, [FromQuery] int specializationId)
        {
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                return BadRequest(new { error = "Missing firstName or lastName" });
            }

            var result = await _verificationService.VerifyDoctorAsync(firstName, lastName, specializationId);
            return Ok(result);
        }
    }
}
