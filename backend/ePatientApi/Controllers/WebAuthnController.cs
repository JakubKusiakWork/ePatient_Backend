using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

using Fido2NetLib;
using Fido2NetLib.Objects;

using ePatientApi.DataAccess;
using ePatientApi.Models;

namespace ePatientApi.Controllers
{
    [Route("api/webauthn")]
    [ApiController]
    public class WebAuthnController : ControllerBase
    {
        private readonly Fido2 _fido2;
        private readonly AppDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WebAuthnController> _logger;

        public WebAuthnController(AppDbContext dbContext, IConfiguration configuration, ILogger<WebAuthnController> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var origins = new HashSet<string>();
            var deployUrl = _configuration["CORS:deployURL"];
            var localhostUrl = _configuration["CORS:localhostURL"];
            var testUrl = _configuration["CORS:testURL"];

            if (!string.IsNullOrEmpty(deployUrl))
            {
                origins.Add(deployUrl);
            }

            if (!string.IsNullOrEmpty(localhostUrl))
            {
                origins.Add(localhostUrl);
            }

            if (!string.IsNullOrEmpty(testUrl))
            {
                origins.Add(testUrl);
            }

            _logger.LogInformation("Fido2 Origins: {Origins}", string.Join(", ", origins));

            var serverDomain = _configuration["WebAuthn:ServerDomain"];
            if (string.IsNullOrEmpty(serverDomain))
            {
                _logger.LogError("WebAuthn:ServerDomain is not configured.");
                throw new InvalidOperationException("WebAuthn:ServerDomain is not configured.");
            }

            _logger.LogInformation("Fido2 ServerDomain: {ServerDomain}", serverDomain);

            _fido2 = new Fido2(new Fido2Configuration
            {
                ServerDomain = serverDomain,
                ServerName = "ePatientApi",
                Origins = origins
            });
        }

        private static string NormalizeId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return string.Empty;
            }

            return id.Replace("/", string.Empty).Replace(".", string.Empty).Replace(" ", string.Empty).Trim().ToLowerInvariant();
        }

        private async Task<RegisteredPatient?> FindRegisteredPatientByAnyIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            var raw = id.Trim();
            var norm = NormalizeId(raw);

            var byBirth = await _dbContext.RegisteredPatients
                .FirstOrDefaultAsync(p => p.BirthNumber == raw || p.BirthNumber == norm);

            if (byBirth != null)
            {
                return byBirth;
            }

            var byUsername = await _dbContext.RegisteredPatients
                .FirstOrDefaultAsync(p => p.Username == raw || p.Username == norm);

            if (byUsername != null)
            {
                return byUsername;
            }

            var all = await _dbContext.RegisteredPatients
                .Where(p => p.FirstName != null && p.LastName != null)
                .Select(p => new { p.BirthNumber, p.FirstName, p.LastName, p.Username })
                .ToListAsync();

            var byFullName = all.FirstOrDefault(p => NormalizeId($"{p.FirstName} {p.LastName}") == norm);

            if (byFullName != null)
            {
                return await _dbContext.RegisteredPatients.FirstOrDefaultAsync(p => p.BirthNumber == byFullName.BirthNumber);
            }

            return null;
        }

        /// <summary>
        /// Registers a new WebAuthn credential for a user
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] WebAuthnRegisterRequest request)
        {
            try
            {
                _logger.LogInformation($"Register called with UserId: {request?.UserId}, UserType: {request?.UserType}");

                if (string.IsNullOrEmpty(request?.UserId) || string.IsNullOrEmpty(request?.UserType))
                {
                    _logger.LogWarning("Invalid registration data: UserId or UserType is empty.");
                    return BadRequest(new { error = "Invalid registration data: UserId or UserType is empty." });
                }

                var normalizedUserType = request.UserType.ToLower();
                if (normalizedUserType != "doctor" && normalizedUserType != "patient")
                {
                    _logger.LogWarning($"Invalid UserType: {request.UserType}");
                    return BadRequest(new { error = $"Invalid UserType: {request.UserType}" });
                }

                Fido2User user;
                string canonicalUserId = request.UserId;

                if (normalizedUserType == "doctor")
                {
                    var doctor = await _dbContext.RegisteredDoctors
                        .FirstOrDefaultAsync(d => d.DoctorId.ToString() == request.UserId);

                    if (doctor == null)
                    {
                        _logger.LogWarning($"Doctor not found for UserId: {request.UserId}");
                        return BadRequest(new { error = $"Doctor not found for UserId: {request.UserId}" });
                    }

                    canonicalUserId = doctor.DoctorId.ToString();
                    user = new Fido2User
                    {
                        Id = Encoding.UTF8.GetBytes(canonicalUserId),
                        Name = doctor.DoctorCode.ToString(),
                        DisplayName = $"{doctor.DoctorFirstName} {doctor.DoctorLastName}"
                    };
                }
                else
                {
                    var patient = await FindRegisteredPatientByAnyIdAsync(request.UserId);
                    if (patient != null && !string.IsNullOrEmpty(patient.BirthNumber))
                    {
                        canonicalUserId = patient.BirthNumber;
                    }
                    else
                    {
                        canonicalUserId = request.UserId;
                    }

                    user = new Fido2User
                    {
                        Id = Encoding.UTF8.GetBytes(canonicalUserId),
                        Name = patient?.Username ?? canonicalUserId,
                        DisplayName = patient != null ? $"{patient.FirstName} {patient.LastName}" : canonicalUserId
                    };
                }

                var options = _fido2.RequestNewCredential(
                    user,
                    new List<PublicKeyCredentialDescriptor>(),
                    new AuthenticatorSelection
                    {
                        AuthenticatorAttachment = AuthenticatorAttachment.Platform,
                        UserVerification = UserVerificationRequirement.Required
                    },
                    AttestationConveyancePreference.None,
                    new AuthenticationExtensionsClientInputs()
                );

                HttpContext.Session.SetString("webauthn-options", options.ToJson());
                HttpContext.Session.SetString("webauthn-userid", canonicalUserId);
                HttpContext.Session.SetString("webauthn-usertype", normalizedUserType);

                _logger.LogInformation("WebAuthn options stored in session.");

                return Ok(options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Register endpoint.");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Completes the WebAuthn registration process
        /// </summary>
        [HttpPost("register/complete")]
        public async Task<IActionResult> RegisterComplete([FromBody] AuthenticatorAttestationRawResponse response)
        {
            try
            {
                _logger.LogInformation("RegisterComplete called.");

                // Retrieve session data
                var optionsJson = HttpContext.Session.GetString("webauthn-options");
                var canonicalUserId = HttpContext.Session.GetString("webauthn-userid");
                var userType = HttpContext.Session.GetString("webauthn-usertype");

                if (string.IsNullOrEmpty(optionsJson) || string.IsNullOrEmpty(canonicalUserId) || string.IsNullOrEmpty(userType))
                {
                    _logger.LogWarning("Session data missing.");
                    return BadRequest(new { error = "Session data missing." });
                }

                var options = CredentialCreateOptions.FromJson(optionsJson);

                if (userType != "doctor" && userType != "patient")
                {
                    _logger.LogWarning($"Invalid UserType in session: {userType}");
                    return BadRequest(new { error = $"Invalid UserType in session: {userType}" });
                }

                var result = await _fido2.MakeNewCredentialAsync(
                    response,
                    options,
                    async (args, token) =>
                    {
                        if (userType == "doctor")
                        {
                            return await _dbContext.RegisteredDoctors
                                .AnyAsync(d => d.DoctorId.ToString() == canonicalUserId, token);
                        }
                        return true;
                    }
                );

                if (result.Status != "ok")
                {
                    _logger.LogWarning($"Credential creation failed: {result.ErrorMessage}");
                    return BadRequest(new { error = $"Credential creation failed: {result.ErrorMessage}" });
                }

                if (result.Result == null)
                {
                    _logger.LogWarning("Credential creation returned no result.");
                    return BadRequest(new { error = "Credential creation returned no result." });
                }

                var webAuthnCredential = new WebAuthnCredentials
                {
                    UserId = NormalizeId(canonicalUserId),
                    UserType = userType,
                    CredentialId = Convert.ToBase64String(result.Result.CredentialId),
                    PublicKey = Convert.ToBase64String(result.Result.PublicKey),
                    UserHandle = Convert.ToBase64String(result.Result.User.Id),
                    SignatureCounter = result.Result.Counter,
                    CreatedAt = DateTime.UtcNow,
                    AuthenticatorType = "platform"
                };

                _dbContext.WebAuthnCredentials.Add(webAuthnCredential);
                await _dbContext.SaveChangesAsync();

                HttpContext.Session.Remove("webauthn-options");
                HttpContext.Session.Remove("webauthn-userid");
                HttpContext.Session.Remove("webauthn-usertype");

                _logger.LogInformation($"Credential registered successfully for UserId: {canonicalUserId}, UserType: {userType}");
                return Ok(new { status = "Success" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RegisterComplete endpoint.");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Authenticates a user with WebAuthn
        /// </summary>
        [HttpPost("authenticate")]
        public async Task<IActionResult> Authenticate([FromBody] WebAuthnAuthenticateRequest request)
        {
            try
            {
                _logger.LogInformation($"Authenticate called with UserId: {request?.UserId}, UserType: {request?.UserType}");

                if (string.IsNullOrEmpty(request?.UserId) || string.IsNullOrEmpty(request?.UserType))
                {
                    _logger.LogWarning("Invalid authentication data: UserId or UserType is empty.");
                    return BadRequest(new { error = "Invalid authentication data: UserId or UserType is empty." });
                }

                var normalizedUserType = request.UserType.ToLower();
                if (normalizedUserType != "doctor" && normalizedUserType != "patient")
                {
                    _logger.LogWarning($"Invalid UserType: {request.UserType}");
                    return BadRequest(new { error = $"Invalid UserType: {request.UserType}" });
                }

                var rawId = request.UserId ?? string.Empty;
                var normId = NormalizeId(rawId);

                var credentials = await _dbContext.WebAuthnCredentials
                    .Where(c => c.UserType == normalizedUserType && c.UserId == normId)
                    .Select(c => new PublicKeyCredentialDescriptor
                    {
                        Id = Convert.FromBase64String(c.CredentialId),
                        Type = PublicKeyCredentialType.PublicKey
                    })
                    .ToListAsync();

                string canonicalIdToUse = rawId;

                if (!credentials.Any() && normalizedUserType == "patient")
                {
                    var patient = await FindRegisteredPatientByAnyIdAsync(rawId);
                    if (patient != null)
                    {
                        _logger.LogInformation("Resolved patient for Authenticate: raw='{Raw}', birth='{Birth}', username='{User}'",
                            rawId, patient.BirthNumber ?? "<null>", patient.Username ?? "<null>");

                        var birthNorm = string.IsNullOrEmpty(patient.BirthNumber) ? null : NormalizeId(patient.BirthNumber);
                        var userNorm = string.IsNullOrEmpty(patient.Username) ? null : NormalizeId(patient.Username);

                        credentials = await _dbContext.WebAuthnCredentials
                            .Where(c => c.UserType == normalizedUserType &&
                                        ((birthNorm != null && c.UserId == birthNorm) ||
                                         (userNorm != null && c.UserId == userNorm)))
                            .Select(c => new PublicKeyCredentialDescriptor
                            {
                                Id = Convert.FromBase64String(c.CredentialId),
                                Type = PublicKeyCredentialType.PublicKey
                            })
                            .ToListAsync();

                        if (credentials.Any())
                        {
                            canonicalIdToUse = patient.BirthNumber ?? patient.Username ?? rawId;
                        }
                    }
                }

                if (!credentials.Any())
                {
                    _logger.LogWarning($"No credentials found for UserId: {request.UserId}, UserType: {normalizedUserType}");
                    return BadRequest(new { error = $"No credentials found for UserId: {request.UserId}, UserType: {normalizedUserType}" });
                }

                var options = _fido2.GetAssertionOptions(
                    credentials,
                    UserVerificationRequirement.Required,
                    new AuthenticationExtensionsClientInputs()
                );

                _logger.LogInformation("Storing WebAuthn options in session for UserId: {UserId}, UserType: {UserType}", canonicalIdToUse, normalizedUserType);
                HttpContext.Session.SetString("webauthn-options", options.ToJson());
                HttpContext.Session.SetString("webauthn-userid", canonicalIdToUse);
                HttpContext.Session.SetString("webauthn-usertype", normalizedUserType);

                return Ok(options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Authenticate endpoint.");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Completes the WebAuthn authentication process
        /// </summary>
        [HttpPost("authenticate/complete")]
        public async Task<IActionResult> AuthenticateComplete([FromBody] AuthenticatorAssertionRawResponse response)
        {
            try
            {
                _logger.LogInformation("AuthenticateComplete called.");

                var optionsJson = HttpContext.Session.GetString("webauthn-options");
                var userId = HttpContext.Session.GetString("webauthn-userid");
                var userType = HttpContext.Session.GetString("webauthn-usertype");
                _logger.LogInformation("Session data retrieved: options={Options}, userId={UserId}, userType={UserType}", 
                    optionsJson ?? "null", userId ?? "null", userType ?? "null");

                if (string.IsNullOrEmpty(optionsJson) || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userType))
                {
                    _logger.LogWarning("Session data missing.");
                    return BadRequest(new { error = "Session data missing." });
                }

                var options = AssertionOptions.FromJson(optionsJson);

                var credential = await _dbContext.WebAuthnCredentials
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.UserType == userType && c.CredentialId == Convert.ToBase64String(response.Id));

                if (credential == null)
                {
                    _logger.LogWarning("Credential not found.");
                    return BadRequest(new { error = "Credential not found." });
                }

                var result = await _fido2.MakeAssertionAsync(
                    response,
                    options,
                    Convert.FromBase64String(credential.PublicKey),
                    credential.SignatureCounter,
                    (args, token) => Task.FromResult(true)
                );

                if (result.Status != "ok")
                {
                    _logger.LogWarning($"Authentication failed: {result.ErrorMessage}");
                    return BadRequest(new { error = $"Authentication failed: {result.ErrorMessage}" });
                }

                credential.SignatureCounter = result.Counter;
                await _dbContext.SaveChangesAsync();

                if (userType == "doctor")
                {
                    var doctor = await _dbContext.RegisteredDoctors.FirstAsync(d => d.DoctorId.ToString() == userId);
                    var token = GenerateJwtToken(doctor);

                    var loggedDoctor = new LoggedDoctor
                    {
                        DoctorFirstName = doctor.DoctorFirstName,
                        DoctorLastName = doctor.DoctorLastName,
                        DoctorCode = doctor.DoctorCode,
                        Created_at = DateTime.UtcNow
                    };
                    _dbContext.LoggedDoctors.Add(loggedDoctor);
                    await _dbContext.SaveChangesAsync();

                    HttpContext.Session.Remove("webauthn-options");
                    HttpContext.Session.Remove("webauthn-userid");
                    HttpContext.Session.Remove("webauthn-usertype");

                    _logger.LogInformation($"Authentication successful for UserId: {userId}, UserType: {userType}");
                    return Ok(new
                    {
                        token = token,
                        message = "Authentication successful.",
                        firstName = doctor.DoctorFirstName,
                        lastName = doctor.DoctorLastName,
                        code = doctor.DoctorCode,
                        role = doctor.Role
                    });
                }
                else
                {
                    var patient = await _dbContext.RegisteredPatients
                        .FirstOrDefaultAsync(p => p.BirthNumber == userId || p.Username == userId);

                    if (patient == null)
                    {
                        _logger.LogWarning("Authenticated patient not found in DB.");
                        return BadRequest(new { error = "Authenticated patient not found." });
                    }

                    var token = GenerateJwtToken(patient);

                    var loggedUser = new LoggedUser
                    {
                        Username = patient.Username,
                        FirstName = patient.FirstName,
                        LastName = patient.LastName,
                        Role = patient.Role
                    };
                    _dbContext.LoggedUsers.Add(loggedUser);
                    await _dbContext.SaveChangesAsync();

                    HttpContext.Session.Remove("webauthn-options");
                    HttpContext.Session.Remove("webauthn-userid");
                    HttpContext.Session.Remove("webauthn-usertype");

                    _logger.LogInformation($"Authentication successful for UserId: {userId}, UserType: {userType}");
                    return Ok(new
                    {
                        token = token,
                        message = "Authentication successful.",
                        username = patient.Username,
                        firstName = patient.FirstName,
                        lastName = patient.LastName,
                        role = patient.Role,
                        birthNumber = patient.BirthNumber
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AuthenticateComplete endpoint.");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private string GenerateJwtToken(RegisteredDoctor doctor)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, doctor.DoctorCode.ToString()),
                new Claim("DoctorFirstName", doctor.DoctorFirstName),
                new Claim("DoctorLastName", doctor.DoctorLastName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
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

        private string GenerateJwtToken(RegisteredPatient patient)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, patient.BirthNumber ?? patient.Username),
                new Claim("username", patient.Username ?? string.Empty),
                new Claim("firstname", patient.FirstName),
                new Claim("lastname", patient.LastName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var jwtKey2 = _configuration["Jwt:Key"] ?? string.Empty;
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey2));
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

    public class WebAuthnRegisterRequest
    {
        public string UserId { get; set; } = null!;
        public string UserType { get; set; } = null!;
    }

    public class WebAuthnAuthenticateRequest
    {
        public string UserId { get; set; } = null!;
        public string UserType { get; set; } = null!;
    }
}