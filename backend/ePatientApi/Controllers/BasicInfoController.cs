using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using ePatientApi.DataAccess;
using ePatientApi.Models;

namespace ePatientApi.Controllers
{
    /// <summary>
    /// Controller that provides basic patient information endpoints.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class BasicInfoController : ControllerBase
    {
        private readonly AppDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="BasicInfoController"/> class.
        /// </summary>
        /// <param name="context">Application database context.</param>
        public BasicInfoController(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Retrieves patient basic information (birth number, first and last name) by birth number or username.
        /// </summary>
        /// <param name="request">Contains patient identification data.</param>
        /// <returns>Patient's basic information or a NotFound/BadRequest result.</returns>
        [HttpPost("patientInfo")]
        public async Task<IActionResult> GetPatientInfo([FromBody] AppointmentIdData request)
        {
            if (request == null)
            {
                return BadRequest(new { message = "Request body is required." });
            }

            if (string.IsNullOrWhiteSpace(request.PatientBirthNumber))
            {
                return BadRequest(new { message = "Patient identifier is required." });
            }

            var searchedIdentifier = request.PatientBirthNumber.Trim();
            var patientRecord = await _context.Patients
                .FirstOrDefaultAsync(p => p.BirthNumber == searchedIdentifier);

            if (patientRecord != null)
            {
                return Ok(new
                {
                    birthNumber = patientRecord.BirthNumber,
                    firstName = patientRecord.FirstName,
                    lastName = patientRecord.LastName
                });
            }

            var registeredPatient = await _context.Set<RegisteredPatient>()
                .FirstOrDefaultAsync(r => r.BirthNumber == searchedIdentifier || r.Username == searchedIdentifier);

            if (registeredPatient != null)
            {
                return Ok(new
                {
                    birthNumber = registeredPatient.BirthNumber,
                    firstName = registeredPatient.FirstName,
                    lastName = registeredPatient.LastName
                });
            }

            return NotFound(new { message = "Patient not found." });
        }
    }
}