using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Npgsql;
using ePatientApi.DataAccess;
using ePatientApi.Models;

namespace ePatientApi.Controllers
{
    [Route("api/register")]
    [ApiController]
    public class RegisterController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<RegisterController> _logger;
        private readonly ePatientApi.Services.DoctorVerificationService? _doctorVerificationService;

        public RegisterController(AppDbContext context, ILogger<RegisterController> logger, ePatientApi.Services.DoctorVerificationService? doctorVerificationService = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _doctorVerificationService = doctorVerificationService;
        }

        /// <summary>
        /// Registers a new patient in the system.
        /// </summary>
        /// <param name="patient">Patient registration data.</param>
        [HttpPost]
        public async Task<IActionResult> Register([FromBody] RegisteredPatient patient)
        {
            try
            {
                _logger.LogInformation("Register called for username: {Username}", patient?.Username);

                if (patient != null)
                {
                    patient.Username = (patient.Username ?? string.Empty).Trim();
                    patient.Email = (patient.Email ?? string.Empty).Trim();
                    patient.BirthNumber = (patient.BirthNumber ?? string.Empty).Trim();
                    patient.FirstName = (patient.FirstName ?? string.Empty).Trim();
                    patient.LastName = (patient.LastName ?? string.Empty).Trim();
                    patient.PhoneNumber = (patient.PhoneNumber ?? string.Empty).Trim();
                    patient.Insurance = (patient.Insurance ?? string.Empty).Trim();
                }

                if (patient == null || IsEmptyInput(patient))
                {
                    _logger.LogWarning("Invalid registration data: Missing required fields.");
                    return BadRequest(new
                    {
                        error = new
                        {
                            code = "INVALID_DATA",
                            message = "All fields must be filled."
                        }
                    });
                }

                var (isPasswordValid, passwordError) = PasswordRegexCheck(patient.HashedPassword);
                if (!isPasswordValid)
                {
                    _logger.LogWarning("Password validation failed: {Error}", passwordError);
                    return BadRequest(new
                    {
                        error = new
                        {
                            code = "INVALID_PASSWORD",
                            message = passwordError
                        }
                    });
                }

                var existingByBirth = await _context.RegisteredPatients
                    .FirstOrDefaultAsync(r => r.BirthNumber == patient.BirthNumber);
                if (existingByBirth != null)
                {
                    _logger.LogWarning("Birth number already exists: {BirthNumber}", patient.BirthNumber);
                    return Conflict(new
                    {
                        error = new
                        {
                            code = "DUPLICATE_BIRTHNUMBER",
                            message = "The birth number is already registered."
                        }
                    });
                }

                var existingByEmail = await _context.RegisteredPatients
                    .FirstOrDefaultAsync(r => r.Email == patient.Email);
                if (existingByEmail != null)
                {
                    _logger.LogWarning("Email already exists: {Email}", patient.Email);
                    return Conflict(new
                    {
                        error = new
                        {
                            code = "DUPLICATE_EMAIL",
                            message = "The email address is already registered."
                        }
                    });
                }

                patient.HashedPassword = BCrypt.Net.BCrypt.HashPassword(patient.HashedPassword);

                _context.RegisteredPatients.Add(patient);
                await _context.SaveChangesAsync();

                var loggedUser = new LoggedUser
                {
                    Username = patient.Username ?? string.Empty,
                    FirstName = patient.FirstName ?? string.Empty,
                    LastName = patient.LastName ?? string.Empty,
                    Role = patient.Role ?? string.Empty
                };
                _context.LoggedUsers.Add(loggedUser);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User registered successfully: {Username}", patient.Username);

                return Ok(new
                {
                    message = "User registered successfully.",
                    receivedData = new
                    {
                        patient.FirstName,
                        patient.LastName,
                        patient.Username,
                        patient.Email,
                        patient.PhoneNumber,
                        patient.Role,
                        patient.BirthNumber,
                        patient.Insurance
                    }
                });
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx)
            {
                _logger.LogError(ex, "Database error during registration.");
                return HandleDatabaseException(pgEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during registration.");
                return StatusCode(500, new
                {
                    error = new
                    {
                        code = "SERVER_ERROR",
                        message = "An unexpected error occurred during registration.",
                        detail = ex.Message
                    }
                });
            }
        }

        /// <summary>
        /// Registers a new doctor. This endpoint performs LEKOM verification and stores verification metadata
        /// alongside the registered doctor record.
        /// </summary>
        [HttpPost("doctor")]
        public async Task<IActionResult> RegisterDoctor([FromBody] DoctorRegistrationDto doctor)
        {
            try
            {
                _logger.LogInformation("Doctor register called for code: {DoctorCode}", doctor?.DoctorCode);

                if (doctor != null)
                {
                    doctor.DoctorCode = (doctor.DoctorCode ?? string.Empty).Trim();
                    doctor.DoctorEmail = (doctor.DoctorEmail ?? string.Empty).Trim();
                    doctor.DoctorFirstName = (doctor.DoctorFirstName ?? string.Empty).Trim();
                    doctor.DoctorLastName = (doctor.DoctorLastName ?? string.Empty).Trim();
                    doctor.DoctorPhoneNumber = (doctor.DoctorPhoneNumber ?? string.Empty).Trim();
                }

                if (doctor == null || string.IsNullOrWhiteSpace(doctor.DoctorCode) || string.IsNullOrWhiteSpace(doctor.DoctorPassword))
                {
                    return BadRequest(new { error = "Missing required doctor registration fields." });
                }

                var existingByCode = await _context.RegisteredDoctors.FirstOrDefaultAsync(d => d.DoctorCode == doctor.DoctorCode);
                if (existingByCode != null)
                {
                    return Conflict(new { error = new { code = "DUPLICATE_DOCTOR_CODE", message = "Doctor code already exists." } });
                }

                var existingByEmail = await _context.RegisteredDoctors.FirstOrDefaultAsync(d => d.DoctorEmail == doctor.DoctorEmail);
                if (existingByEmail != null)
                {
                    return Conflict(new { error = new { code = "DUPLICATE_EMAIL", message = "Email already registered." } });
                }

                ePatientApi.Models.DoctorVerificationResult? verification = null;
                if (doctor.SkipVerification)
                {
                    verification = new ePatientApi.Models.DoctorVerificationResult
                    {
                        IsVerified = doctor.IsVerified,
                        FirstName = doctor.VerifiedFirstName,
                        LastName = doctor.VerifiedLastName,
                        FullName = doctor.VerifiedFullName,
                        Specialization = doctor.VerifiedSpecialization,
                        SourceUrl = doctor.VerifiedSourceUrl
                    };
                }
                else
                {
                    verification = _doctorVerificationService != null
                        ? await _doctorVerificationService.VerifyDoctorAsync(doctor.DoctorFirstName, doctor.DoctorLastName, doctor.SpecializationId)
                        : null;
                }

                var dbDoctor = new RegisteredDoctor
                {
                    DoctorFirstName = doctor.DoctorFirstName,
                    DoctorLastName = doctor.DoctorLastName,
                    DoctorCode = doctor.DoctorCode,
                    DoctorEmail = doctor.DoctorEmail ?? string.Empty,
                    DoctorPhoneNumber = doctor.DoctorPhoneNumber ?? string.Empty,
                    DoctorHashedPassword = BCrypt.Net.BCrypt.HashPassword(doctor.DoctorPassword),
                    Role = doctor.Role ?? "Clinician",
                    IsVerified = verification?.IsVerified ?? false,
                    VerifiedFirstName = verification?.FirstName,
                    VerifiedLastName = verification?.LastName,
                    VerifiedFullName = verification?.FullName,
                    VerifiedSpecialization = verification?.Specialization,
                    VerifiedSourceUrl = verification?.SourceUrl
                };

                _context.RegisteredDoctors.Add(dbDoctor);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Doctor registered successfully: {DoctorCode}", dbDoctor.DoctorCode);

                return Ok(new { message = "Doctor registered successfully.", code = dbDoctor.DoctorCode, isVerified = dbDoctor.IsVerified });
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx)
            {
                _logger.LogError(ex, "Database error during doctor registration.");
                return HandleDatabaseException(pgEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during doctor registration.");
                return StatusCode(500, new { error = new { code = "SERVER_ERROR", message = "An unexpected error occurred during doctor registration.", detail = ex.Message } });
            }
        }

        /// <summary>
        /// Checks if any required fields are empty.
        /// </summary>
        private static bool IsEmptyInput(RegisteredPatient patient)
        {
            return string.IsNullOrWhiteSpace(patient.FirstName) ||
                string.IsNullOrWhiteSpace(patient.LastName) ||
                string.IsNullOrWhiteSpace(patient.Email) ||
                string.IsNullOrWhiteSpace(patient.PhoneNumber) ||
                string.IsNullOrWhiteSpace(patient.HashedPassword) ||
                string.IsNullOrWhiteSpace(patient.Username) ||
                string.IsNullOrWhiteSpace(patient.Role) ||
                string.IsNullOrWhiteSpace(patient.BirthNumber) ||
                string.IsNullOrWhiteSpace(patient.Insurance);
        }

        /// <summary>
        /// Validates the password against specific rules.
        /// </summary>
        private static (bool isValid, string errorMessage) PasswordRegexCheck(string password)
        {
            var errorMessages = new List<string>();

            if (string.IsNullOrEmpty(password))
            {
                return (false, "Password cannot be empty.");
            }

            if (password.Length < 8)
            {
                errorMessages.Add("Password must contain at least 8 characters.");
            }

            if (!Regex.IsMatch(password, "[A-Z]"))
            {
                errorMessages.Add("Password must contain at least one uppercase letter.");
            }

            if (!Regex.IsMatch(password, "[a-z]"))
            {
                errorMessages.Add("Password must contain at least one lowercase letter.");
            }

            if (!Regex.IsMatch(password, "\\d"))
            {
                errorMessages.Add("Password must contain at least one digit.");
            }

            if (!Regex.IsMatch(password, "[!@#$%^&*(),.?\"{}|<>]"))
            {
                errorMessages.Add("Password must contain at least one special character.");
            }

            return errorMessages.Any()
                ? (false, string.Join("\n", errorMessages))
                : (true, "Password is valid.");
        }

        /// <summary>
        /// Handles database exceptions and returns appropriate responses.
        /// </summary>
        private IActionResult HandleDatabaseException(PostgresException pgEx)
        {
            if (pgEx.SqlState == "23505")
            {
                if (pgEx.MessageText.Contains("IX_registeredpatient_username"))
                {
                    return Conflict(new
                    {
                        error = new
                        {
                            code = "DUPLICATE_USERNAME",
                            message = "The username is already taken."
                        }
                    });
                }

                if (pgEx.MessageText.Contains("IX_registeredpatient_email"))
                {
                    return Conflict(new
                    {
                        error = new
                        {
                            code = "DUPLICATE_EMAIL",
                            message = "The email address is already registered."
                        }
                    });
                }

                if (pgEx.MessageText.Contains("IX_registeredpatient_birthnumber") || pgEx.MessageText.Contains("IX_registeredpatient_birth_number"))
                {
                    return Conflict(new
                    {
                        error = new
                        {
                            code = "DUPLICATE_BIRTHNUMBER",
                            message = "The birth number is already registered."
                        }
                    });
                }
            }

            return StatusCode(500, new
            {
                error = new
                {
                    code = "SERVER_ERROR",
                    message = "A database error occurred.",
                    detail = pgEx.MessageText
                }
            });
        }
    }
}