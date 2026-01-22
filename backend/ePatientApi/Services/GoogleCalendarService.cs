using System.Net.Http.Headers;
using System.Text.Json;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using Microsoft.AspNetCore.DataProtection;
using ePatientApi.DataAccess;
using ePatientApi.Models;

namespace ePatientApi.Services
{
    /// <summary>
    /// Helper for interacting with Google Calendar API for a patient.
    /// Responsible for ensuring access_token is refreshed when expired.
    /// Tokens stored in DB are protected using IDataProtector with purpose "GoogleTokens".
    /// </summary>
    public class GoogleCalendarService
    {
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context;
        private readonly IDataProtector _protector;
        private readonly ILogger<GoogleCalendarService> _logger;

        public GoogleCalendarService(IConfiguration configuration, AppDbContext context, IDataProtectionProvider provider, ILogger<GoogleCalendarService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _protector = provider.CreateProtector("GoogleTokens");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private async Task EnsureValidAccessTokenAsync(RegisteredPatient patient)
        {
            if (patient == null) throw new ArgumentNullException(nameof(patient));

            if (string.IsNullOrWhiteSpace(patient.GoogleAccessToken) && string.IsNullOrWhiteSpace(patient.GoogleRefreshToken))
            {
                return;
            }

            var now = DateTime.UtcNow;
            if (patient.GoogleTokenExpiry.HasValue && patient.GoogleTokenExpiry.Value > now.AddSeconds(30))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(patient.GoogleRefreshToken))
            {
                _logger.LogWarning("No refresh token available for patient {BirthNumber}; cannot refresh Google access token.", patient.BirthNumber);
                return;
            }

            string refreshToken;
            try
            {
                refreshToken = _protector.Unprotect(patient.GoogleRefreshToken!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unprotect Google refresh token for {BirthNumber}", patient.BirthNumber);
                return;
            }

            var clientId = _configuration["Google:ClientId"] ?? string.Empty;
            var clientSecret = _configuration["Google:ClientSecret"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                _logger.LogWarning("Google client id/secret not configured; cannot refresh token.");
                return;
            }

            using var http = new HttpClient();
            var form = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            };

            var resp = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(form));
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to refresh Google token for {BirthNumber}. Status: {Status}", patient.BirthNumber, resp.StatusCode);
                return;
            }

            using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            if (doc.RootElement.TryGetProperty("access_token", out var at))
            {
                var accessToken = at.GetString();
                var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exIn) ? exIn.GetInt32() : 3600;

                try
                {
                    patient.GoogleAccessToken = _protector.Protect(accessToken ?? string.Empty);
                    patient.GoogleTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);
                    _context.RegisteredPatients.Update(patient);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save refreshed google token for {BirthNumber}", patient.BirthNumber);
                }
            }
        }

        private string? UnprotectAccessToken(RegisteredPatient patient)
        {
            if (string.IsNullOrWhiteSpace(patient.GoogleAccessToken)) return null;
            try
            {
                return _protector.Unprotect(patient.GoogleAccessToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unprotect Google access token for {BirthNumber}", patient.BirthNumber);
                return null;
            }
        }

        public async Task<Google.Apis.Calendar.v3.Data.Event?> CreateAppointmentAsync(RegisteredPatient patient, AppointmentData appointment)
        {
            if (patient == null) throw new ArgumentNullException(nameof(patient));
            if (appointment == null) throw new ArgumentNullException(nameof(appointment));

            await EnsureValidAccessTokenAsync(patient);
            var accessToken = UnprotectAccessToken(patient);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogWarning("No valid access token for patient {BirthNumber}; skipping calendar create.", patient.BirthNumber);
                return null;
            }

            var credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken(accessToken);
            var service = new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "ePatient"
            });

            TimeSpan parsedTime = TimeSpan.Parse(appointment.ReservationTime ?? "00:00");
            var localDate = appointment.ReservationDay.Date.Add(parsedTime);
            var start = localDate;
            var end = start.AddMinutes(30);

            string tzId = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
                ? "Central Europe Standard Time"
                : "Europe/Bratislava";
            var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            var startUnspecified = DateTime.SpecifyKind(start, DateTimeKind.Unspecified);
            var endUnspecified = DateTime.SpecifyKind(end, DateTimeKind.Unspecified);
            var startDto = new DateTimeOffset(startUnspecified, tzInfo.GetUtcOffset(startUnspecified));
            var endDto = new DateTimeOffset(endUnspecified, tzInfo.GetUtcOffset(endUnspecified));

            _logger.LogDebug("GoogleCalendar: creating event start={StartLocal} startDto={StartDto} tz={Tz} offset={Offset}",
                startUnspecified, startDto, tzInfo.Id, tzInfo.GetUtcOffset(startUnspecified));

            var newEvent = new Google.Apis.Calendar.v3.Data.Event
            {
                Summary = "Doctor visit",
                Description = "Scheduled via ePatient",
                Start = new Google.Apis.Calendar.v3.Data.EventDateTime { DateTimeDateTimeOffset = startDto, TimeZone = "Europe/Bratislava" },
                End = new Google.Apis.Calendar.v3.Data.EventDateTime { DateTimeDateTimeOffset = endDto, TimeZone = "Europe/Bratislava" },
                Reminders = new Google.Apis.Calendar.v3.Data.Event.RemindersData { UseDefault = true },
                Attendees = !string.IsNullOrWhiteSpace(patient.Email)
                    ? new List<Google.Apis.Calendar.v3.Data.EventAttendee> { new Google.Apis.Calendar.v3.Data.EventAttendee { Email = patient.Email } }
                    : null
            };

            var created = await service.Events.Insert(newEvent, "primary").ExecuteAsync();
            return created;
        }

        public async Task DeleteAppointmentAsync(RegisteredPatient patient, string eventId)
        {
            if (patient == null) throw new ArgumentNullException(nameof(patient));
            if (string.IsNullOrWhiteSpace(eventId)) return;

            await EnsureValidAccessTokenAsync(patient);
            var accessToken = UnprotectAccessToken(patient);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogWarning("No valid access token for patient {BirthNumber}; skipping calendar delete.", patient.BirthNumber);
                return;
            }

            var credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken(accessToken);
            var service = new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "ePatient"
            });

            try
            {
                await service.Events.Delete("primary", eventId).ExecuteAsync();
            }
            catch (Google.GoogleApiException ex)
            {
                _logger.LogWarning(ex, "Failed to delete event {EventId} for patient {BirthNumber}", eventId, patient.BirthNumber);
            }
        }
    }
}
