using System.Net.Http.Headers;
using System.Text.Json;
using System.Net;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using ePatientApi.DataAccess;
using ePatientApi.Models;
using ePatientApi.Interfaces;

namespace ePatientApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GoogleController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context;
        private readonly IDataProtector _protector;
        private readonly IJwtToken _jwtToken;
        private readonly ILogger<GoogleController> _logger;

        public GoogleController(IConfiguration configuration, AppDbContext context, IDataProtectionProvider provider, IJwtToken jwtToken, ILogger<GoogleController> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _protector = provider.CreateProtector("GoogleTokens");
            _jwtToken = jwtToken ?? throw new ArgumentNullException(nameof(jwtToken));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("auth")]
        public IActionResult Auth()
        {
            var rawJwt = _jwtToken.ExtractTokenFromHeader(Request);
            if (string.IsNullOrWhiteSpace(rawJwt))
            {
                return Unauthorized(new { message = "Authorization required to connect Google Calendar." });
            }

            var parsed = _jwtToken.ParseJwtToken(rawJwt);
            var sub = parsed?.Claims?.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub || c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(sub))
            {
                return Unauthorized(new { message = "Unable to identify user from token." });
            }

            var state = _protector.Protect(sub);
            var clientId = _configuration["Google:ClientId"] ?? string.Empty;
            var redirect = _configuration["Google:RedirectUri"] ?? string.Empty;
            var scope = WebUtility.UrlEncode("https://www.googleapis.com/auth/calendar.events");
            var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?client_id={WebUtility.UrlEncode(clientId)}&response_type=code&scope={scope}&redirect_uri={WebUtility.UrlEncode(redirect)}&access_type=offline&prompt=consent&state={WebUtility.UrlEncode(state)}";

            return Redirect(authUrl);
        }

        [HttpGet("url")]
        public IActionResult GetAuthUrl()
        {
            var rawJwt = _jwtToken.ExtractTokenFromHeader(Request);
            if (string.IsNullOrWhiteSpace(rawJwt))
            {
                return Unauthorized(new { message = "Authorization required to get Google OAuth URL." });
            }

            var parsed = _jwtToken.ParseJwtToken(rawJwt);
            var sub = parsed?.Claims?.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub || c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(sub))
            {
                return Unauthorized(new { message = "Unable to identify user from token." });
            }

            var state = _protector.Protect(sub);
            var clientId = _configuration["Google:ClientId"] ?? string.Empty;
            var redirect = _configuration["Google:RedirectUri"] ?? string.Empty;
            var scope = WebUtility.UrlEncode("https://www.googleapis.com/auth/calendar.events");
            var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?client_id={WebUtility.UrlEncode(clientId)}&response_type=code&scope={scope}&redirect_uri={WebUtility.UrlEncode(redirect)}&access_type=offline&prompt=consent&state={WebUtility.UrlEncode(state)}";

            return Ok(new { url = authUrl });
        }

        [HttpGet("callback")]
        public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            {
                return BadRequest(new { message = "Missing code or state." });
            }

            string birthNumber;
            try
            {
                birthNumber = _protector.Unprotect(state);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Invalid state in Google callback");
                return BadRequest(new { message = "Invalid state." });
            }

            var clientId = _configuration["Google:ClientId"] ?? string.Empty;
            var clientSecret = _configuration["Google:ClientSecret"] ?? string.Empty;
            var redirect = _configuration["Google:RedirectUri"] ?? string.Empty;

            using var http = new HttpClient();
            var form = new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirect,
                ["grant_type"] = "authorization_code"
            };

            var resp = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(form));
            if (!resp.IsSuccessStatusCode)
            {
                var txt = await resp.Content.ReadAsStringAsync();
                _logger.LogWarning("Google token endpoint returned {Status}: {Body}", resp.StatusCode, txt);
                return StatusCode((int)resp.StatusCode, new { message = "Failed to exchange code for tokens." });
            }

            using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            var accessToken = doc.RootElement.TryGetProperty("access_token", out var at) ? at.GetString() : null;
            var refreshToken = doc.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exIn) ? exIn.GetInt32() : 3600;

            var patient = await _context.RegisteredPatients.FirstOrDefaultAsync(p => p.BirthNumber == birthNumber);
            if (patient == null)
            {
                patient = await _context.RegisteredPatients.FirstOrDefaultAsync(p => p.Username == birthNumber);
            }

            if (patient == null)
            {
                return NotFound(new { message = "User not found to attach google tokens." });
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(accessToken))
                    patient.GoogleAccessToken = _protector.Protect(accessToken);

                if (!string.IsNullOrWhiteSpace(refreshToken))
                    patient.GoogleRefreshToken = _protector.Protect(refreshToken);

                patient.GoogleTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);
                _context.RegisteredPatients.Update(patient);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save google tokens for {BirthNumber}", patient.BirthNumber);
                return StatusCode(500, new { message = "Internal error saving tokens." });
            }

            var html = "<html><body><h2>Google Calendar connected</h2><p>You can now close this window and return to the ePatient application.</p></body></html>";
            return Content(html, "text/html");
        }

        [HttpGet("revoke")]
        public async Task<IActionResult> Revoke([FromQuery] string? birthNumber)
        {
            if (string.IsNullOrWhiteSpace(birthNumber))
            {
                var rawJwt = _jwtToken.ExtractTokenFromHeader(Request);
                var parsed = rawJwt != null ? _jwtToken.ParseJwtToken(rawJwt) : null;
                birthNumber = parsed?.Claims?.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub || c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            }

            if (string.IsNullOrWhiteSpace(birthNumber)) return BadRequest(new { message = "birthNumber required" });

            var patient = await _context.RegisteredPatients.FirstOrDefaultAsync(p => p.BirthNumber == birthNumber);
            if (patient == null) return NotFound(new { message = "patient not found" });

            try
            {
                if (!string.IsNullOrWhiteSpace(patient.GoogleAccessToken))
                {
                    var access = _protector.Unprotect(patient.GoogleAccessToken);
                    using var http = new HttpClient();
                    var resp = await http.PostAsync($"https://oauth2.googleapis.com/revoke?token={WebUtility.UrlEncode(access)}", null!);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to call revoke endpoint for {BirthNumber}", patient.BirthNumber);
            }

            patient.GoogleAccessToken = null;
            patient.GoogleRefreshToken = null;
            patient.GoogleTokenExpiry = null;
            _context.RegisteredPatients.Update(patient);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Google calendar disconnected." });
        }
    }
}
