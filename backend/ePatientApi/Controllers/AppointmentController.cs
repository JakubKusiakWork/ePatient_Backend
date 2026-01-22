using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ePatientApi.DataAccess;
using ePatientApi.Models;

namespace ePatientApi.Controllers
{
    /// <summary>
    /// Controller responsible for creating and querying patient appointments.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AppointmentController : ControllerBase
    {
    private readonly AppDbContext _context;
    private readonly ILogger<AppointmentController> _logger;
    private readonly ePatientApi.Interfaces.IEmailSender _emailSender;
    private readonly ePatientApi.Services.GoogleCalendarService _googleCalendarService;

        public AppointmentController(AppDbContext context, ILogger<AppointmentController> logger, ePatientApi.Interfaces.IEmailSender emailSender, ePatientApi.Services.GoogleCalendarService googleCalendarService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
            _googleCalendarService = googleCalendarService ?? throw new ArgumentNullException(nameof(googleCalendarService));
        }

        [HttpPost("testEmail")]
        public async Task<IActionResult> SendTestEmail([FromQuery] string toEmail)
        {
            if (string.IsNullOrWhiteSpace(toEmail)) return BadRequest(new { message = "toEmail query parameter is required." });

            try
            {
                var subject = "ePatient test email";
                var body = "This is a test email from ePatient backend to verify SMTP settings.";
                await _emailSender.SendEmailAsync(toEmail, subject, body, CancellationToken.None);
                return Ok(new { message = "Test email sent (check logs for delivery)." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send test email to {ToEmail}", toEmail);
                return StatusCode(500, new { message = "Failed to send test email.", detail = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateAppointment([FromBody] AppointmentData appointmentData)
        {
            if (appointmentData == null)
            {
                return BadRequest(new { message = "Appointment data is required." });
            }

            if (string.IsNullOrWhiteSpace(appointmentData.PatientBirthNumber))
            {
                return BadRequest(new { message = "Patient identifier is required." });
            }

            appointmentData.PatientBirthNumber = appointmentData.PatientBirthNumber.Trim();

            RegisteredPatient? matchedRegisteredPatient = null;
            RegisteredPatient? patientRecord = null;

            patientRecord = await _context.Patients
                .FirstOrDefaultAsync(p => p.BirthNumber == appointmentData.PatientBirthNumber);

            if (patientRecord == null)
            {
                matchedRegisteredPatient = await _context.Set<RegisteredPatient>()
                    .FirstOrDefaultAsync(r => r.BirthNumber == appointmentData.PatientBirthNumber|| r.Username == appointmentData.PatientBirthNumber);

                if (matchedRegisteredPatient != null)
                {
                    appointmentData.PatientBirthNumber = matchedRegisteredPatient.BirthNumber;
                    patientRecord = await _context.Patients
                        .FirstOrDefaultAsync(p => p.BirthNumber == matchedRegisteredPatient.BirthNumber);
                }
            }

            if (patientRecord == null)
            {
                return NotFound(new { message = "Patient not found." });
            }

            var validTimeSlots = GenerateValidTimeSlots();
            var reservationTimeRaw = (appointmentData.ReservationTime ?? string.Empty).Trim();

            if (!TimeSpan.TryParse(reservationTimeRaw, out var parsedTime))
            {
                return BadRequest(new { message = "Invalid reservation time format." });
            }

            var normalizedTime = parsedTime.ToString(@"hh\:mm");

            if (!validTimeSlots.Contains(normalizedTime))
            {
                return BadRequest(new
                {
                    message = "Invalid reservation time. Allowed time slots only.",
                    validSlots = validTimeSlots
                });
            }

            appointmentData.ReservationDay = DateTime.SpecifyKind(appointmentData.ReservationDay.Date, DateTimeKind.Utc);
            appointmentData.ReservationTime = normalizedTime;

            var todayUtc = DateTime.UtcNow.Date;
            var hasUpcoming = await _context.Appointments
                .AnyAsync(a => a.PatientBirthNumber == appointmentData.PatientBirthNumber && a.ReservationDay >= todayUtc && (string.IsNullOrEmpty(a.Status) || a.Status == "scheduled"));
            if (hasUpcoming)
            {
                return Conflict(new { message = "Patient already has an upcoming appointment." });
            }
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var exists = await _context.Appointments
                    .AnyAsync(a => a.ReservationDay == appointmentData.ReservationDay 
                        && a.ReservationTime == appointmentData.ReservationTime
                        && a.DoctorId == appointmentData.DoctorId
                        && (string.IsNullOrEmpty(a.Status) || a.Status == "scheduled"));

                if (exists)
                {
                    return Conflict(new { message = "Selected time slot is already booked." });
                }

                try
                {
                    if (appointmentData.DoctorId != null)
                    {
                        var doc = await _context.RegisteredDoctors.FirstOrDefaultAsync(d => d.DoctorId == appointmentData.DoctorId.Value);
                        if (doc == null)
                        {
                            return BadRequest(new { message = "Selected doctor not found." });
                        }
                        appointmentData.DoctorName = !string.IsNullOrWhiteSpace(doc.VerifiedFullName)
                            ? doc.VerifiedFullName
                            : string.Concat(doc.VerifiedFirstName ?? doc.DoctorFirstName ?? string.Empty, " ", doc.VerifiedLastName ?? doc.DoctorLastName ?? string.Empty).Trim();
                    }
                    else if (string.IsNullOrWhiteSpace(appointmentData.DoctorName))
                    {
                        var verifiedDoctors = await _context.RegisteredDoctors.Where(d => d.IsVerified).ToListAsync();
                        List<RegisteredDoctor> pool = verifiedDoctors;
                        if (pool == null || pool.Count == 0)
                        {
                            var allDoctors = await _context.RegisteredDoctors.ToListAsync();
                            pool = allDoctors ?? new List<RegisteredDoctor>();
                        }

                        if (pool.Count > 0)
                        {
                            var rnd = new Random();
                            var pick = pool[rnd.Next(0, pool.Count)];
                            appointmentData.DoctorId = pick.DoctorId;
                            appointmentData.DoctorName = !string.IsNullOrWhiteSpace(pick.VerifiedFullName)
                                ? pick.VerifiedFullName
                                : string.Concat(pick.VerifiedFirstName ?? pick.DoctorFirstName ?? string.Empty, " ", pick.VerifiedLastName ?? pick.DoctorLastName ?? string.Empty).Trim();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve or assign doctor for appointment");
                }

                await _context.Appointments.AddAsync(appointmentData);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                try
                {
                    var patientEmail = matchedRegisteredPatient?.Email ?? patientRecord.Email;
                    if (!string.IsNullOrWhiteSpace(patientEmail))
                    {
                        var subject = "Your appointment is confirmed";
                        var body = $"Dear {patientRecord.FirstName} {patientRecord.LastName},\n\nYour appointment on {appointmentData.ReservationDay:yyyy-MM-dd} at {appointmentData.ReservationTime} has been successfully booked.\n\nRegards,\nePatient Clinic";
                        _logger.LogInformation("Sending appointment confirmation email to {PatientEmail} for patient {BirthNumber}", patientEmail, appointmentData.PatientBirthNumber);
                        await _emailSender.SendEmailAsync(patientEmail, subject, body, CancellationToken.None);
                        _logger.LogInformation("Finished sending confirmation email to {PatientEmail}", patientEmail);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send appointment confirmation email for patient {PatientBirthNumber}", appointmentData.PatientBirthNumber);
                }

                try
                {
                    var regPatient = await _context.RegisteredPatients.FirstOrDefaultAsync(p => p.BirthNumber == appointmentData.PatientBirthNumber);
                    if (regPatient != null && (!string.IsNullOrWhiteSpace(regPatient.GoogleAccessToken) || !string.IsNullOrWhiteSpace(regPatient.GoogleRefreshToken)))
                    {
                        var ev = await _googleCalendarService.CreateAppointmentAsync(regPatient, appointmentData);
                        if (ev != null)
                        {
                            appointmentData.GoogleEventId = ev.Id;
                            _context.Appointments.Update(appointmentData);
                            await _context.SaveChangesAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create Google calendar event for patient {PatientBirthNumber}", appointmentData.PatientBirthNumber);
                }

                return CreatedAtAction(nameof(GetAppointmentDetails), new { appointmentId = appointmentData.AppointmentId }, new
                {
                    message = "Appointment created successfully.",
                    appointmentId = appointmentData.AppointmentId,
                    patientId = appointmentData.PatientBirthNumber,
                    doctorId = appointmentData.DoctorId,
                    doctorName = appointmentData.DoctorName
                });
            }
            catch (DbUpdateException)
            {
                return Conflict(new { message = "Could not create appointment due to a conflicting booking." });
            }
        }

        [HttpGet("today")]
        public async Task<IActionResult> GetTodayAppointments()
        {
            var currentUtc = DateTime.UtcNow;

            var isWorkingHour = currentUtc.Hour < 16
                                && currentUtc.DayOfWeek >= DayOfWeek.Monday
                                && currentUtc.DayOfWeek <= DayOfWeek.Friday;

            var targetDay = isWorkingHour ? currentUtc.Date : GetNextWorkingDay(currentUtc);
            
            var doctorCode = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            
            var query = _context.Appointments
                .Where(a => a.ReservationDay == targetDay && (string.IsNullOrEmpty(a.Status) || a.Status == "scheduled"));
            
            if (!string.IsNullOrWhiteSpace(doctorCode))
            {
                var doctor = await _context.RegisteredDoctors
                    .FirstOrDefaultAsync(d => d.DoctorCode == doctorCode);
                if (doctor != null)
                {
                    query = query.Where(a => a.DoctorId == doctor.DoctorId);
                }
            }
            
            var appointmentsQuery = query
                .Include(a => a.Patient)
                .AsEnumerable()
                .OrderBy(a => TimeSpan.Parse(a.ReservationTime ?? "00:00"))
                .Select(a => new
                {
                    Time = a.ReservationTime,
                    PatientName = a.Patient != null
                        ? string.Concat(a.Patient.FirstName, " ", a.Patient.LastName)
                        : "Unknown Patient",
                    AppointmentId = a.AppointmentId,
                    DoctorId = a.DoctorId,
                    DoctorName = a.DoctorName
                })
                .ToList();

            return Ok(new { count = appointmentsQuery.Count, appointments = appointmentsQuery });
        }

        [HttpGet("forDay")]
        public async Task<IActionResult> GetAppointmentsForDay([FromQuery] DateTime day)
        {
            var targetDay = DateTime.SpecifyKind(day.Date, DateTimeKind.Utc);

            var doctorCode = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            
            var query = _context.Appointments
                .Where(a => a.ReservationDay == targetDay);
            
            if (!string.IsNullOrWhiteSpace(doctorCode))
            {
                var doctor = await _context.RegisteredDoctors
                    .FirstOrDefaultAsync(d => d.DoctorCode == doctorCode);
                if (doctor != null)
                {
                    query = query.Where(a => a.DoctorId == doctor.DoctorId);
                }
            }
            
            var allAppointments = await query.Include(a => a.Patient).ToListAsync();
            
            var scheduledAppointments = allAppointments
                .Where(a => string.IsNullOrEmpty(a.Status) || a.Status == "scheduled")
                .OrderBy(a => TimeSpan.Parse(a.ReservationTime ?? "00:00"))
                .Select(a => new
                {
                    Time = a.ReservationTime,
                    PatientName = a.Patient != null
                        ? string.Concat(a.Patient.FirstName, " ", a.Patient.LastName)
                        : "Unknown Patient",
                    AppointmentId = a.AppointmentId,
                    DoctorId = a.DoctorId,
                    DoctorName = a.DoctorName
                })
                .ToList();
            
            var completedCount = allAppointments.Count(a => a.Status == "completed");
            var cancelledCount = allAppointments.Count(a => a.Status == "cancelled");
            
            return Ok(new 
            { 
                count = scheduledAppointments.Count, 
                appointments = scheduledAppointments,
                completedCount = completedCount,
                cancelledCount = cancelledCount
            });
        }

        [HttpGet("{appointmentId}")]
        public async Task<IActionResult> GetAppointmentDetails(int appointmentId)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (appointment == null)
            {
                return NotFound(new { message = "Appointment not found." });
            }

            var patientFullName = appointment.Patient != null
                ? string.Concat(appointment.Patient.FirstName, " ", appointment.Patient.LastName)
                : "Unknown Patient";

            return Ok(new
            {
                appointmentId = appointment.AppointmentId,
                patientId = appointment.PatientBirthNumber,
                patientName = patientFullName,
                reservationTime = appointment.ReservationTime,
                reservationDay = appointment.ReservationDay
                ,
                doctorId = appointment.DoctorId,
                doctorName = appointment.DoctorName
            });
        }

        [HttpDelete("{appointmentId}")]
        public async Task<IActionResult> DeleteAppointment(int appointmentId)
        {
            var appointment = await _context.Appointments.FindAsync(appointmentId);
            if (appointment == null)
            {
                return NotFound(new { message = "Appointment not found." });
            }

            appointment.Status = "cancelled";
            await _context.SaveChangesAsync();

            try
            {
                if (!string.IsNullOrWhiteSpace(appointment.GoogleEventId))
                {
                    var patient = await _context.RegisteredPatients.FirstOrDefaultAsync(p => p.BirthNumber == appointment.PatientBirthNumber);
                    if (patient != null)
                    {
                        await _googleCalendarService.DeleteAppointmentAsync(patient, appointment.GoogleEventId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete Google calendar event for appointment {AppointmentId}", appointmentId);
            }

            try
            {
                var patient = await _context.RegisteredPatients.FirstOrDefaultAsync(p => p.BirthNumber == appointment.PatientBirthNumber);
                var patientEmail = patient?.Email ?? appointment.Patient?.Email;
                if (!string.IsNullOrWhiteSpace(patientEmail))
                {
                    var subject = "Your appointment was cancelled";
                    var body = $"Dear {patient?.FirstName ?? "Patient"},\n\nYour appointment on {appointment.ReservationDay:yyyy-MM-dd} at {appointment.ReservationTime} has been cancelled. If this was a mistake, please contact us or rebook via the application.\n\nRegards,\nePatient Clinic";
                    _logger.LogInformation("Sending appointment cancellation email to {PatientEmail} for appointment {AppointmentId}", patientEmail, appointmentId);
                    await _emailSender.SendEmailAsync(patientEmail, subject, body, CancellationToken.None);
                    _logger.LogInformation("Finished sending cancellation email to {PatientEmail}", patientEmail);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send appointment cancellation email for appointment {AppointmentId}", appointmentId);
            }

            return Ok(new { message = "Appointment cancelled successfully." });
        }

        [HttpGet("availableTimes")]
        public IActionResult GetAvailableTimes([FromQuery] DateTime reservationDay, [FromQuery] int? doctorId)
        {
            var startUtc = DateTime.SpecifyKind(reservationDay.Date, DateTimeKind.Utc);
            var endUtc = startUtc.AddDays(1);

            if (startUtc.DayOfWeek == DayOfWeek.Saturday || startUtc.DayOfWeek == DayOfWeek.Sunday)
            {
                return BadRequest(new { message = "Clinic is closed on weekends." });
            }

            var validTimeSlots = GenerateValidTimeSlots();
            
            var appointmentsQuery = _context.Appointments
                .Where(a => a.ReservationDay >= startUtc && a.ReservationDay < endUtc && (string.IsNullOrEmpty(a.Status) || a.Status == "scheduled"));
            
            if (doctorId.HasValue)
            {
                appointmentsQuery = appointmentsQuery.Where(a => a.DoctorId == doctorId.Value);
            }
            
            var bookedTimesSet = appointmentsQuery
                .Select(a => (a.ReservationTime ?? string.Empty).Trim())
                .ToHashSet();
            var availableTimes = validTimeSlots
                .Where(timeSlot => !bookedTimesSet.Contains(timeSlot))
                .ToList();

            return Ok(availableTimes);
        }

        [HttpGet("availableCounts")]
        public IActionResult GetAvailableCounts([FromQuery] int year, [FromQuery] int month, [FromQuery] int? doctorId)
        {
            if (month < 1 || month > 12) return BadRequest(new { message = "Invalid month" });
            if (year < 2000 || year > 3000) return BadRequest(new { message = "Invalid year" });

            var startUtc = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endUtc = startUtc.AddMonths(1);
            var validTimeSlots = GenerateValidTimeSlots();
            var totalSlots = validTimeSlots.Count;
            
            var appointmentsQuery = _context.Appointments
                .Where(a => a.ReservationDay >= startUtc && a.ReservationDay < endUtc);
            
            if (doctorId.HasValue)
            {
                appointmentsQuery = appointmentsQuery.Where(a => a.DoctorId == doctorId.Value);
            }
            
            var bookingsInMonth = appointmentsQuery
                .AsEnumerable()
                .GroupBy(a => a.ReservationDay.Date.ToString("yyyy-MM-dd"))
                .ToDictionary(g => g.Key, g => g.Select(a => (a.ReservationTime ?? string.Empty).Trim()).ToHashSet());

            var result = new Dictionary<string, int>();
            var daysInMonth = DateTime.DaysInMonth(year, month);
            for (int d = 1; d <= daysInMonth; d++)
            {
                var dayDate = new DateTime(year, month, d);
                if (dayDate.DayOfWeek == DayOfWeek.Saturday || dayDate.DayOfWeek == DayOfWeek.Sunday)
                {
                    result[dayDate.ToString("yyyy-MM-dd")] = 0;
                    continue;
                }

                var key = dayDate.ToString("yyyy-MM-dd");
                if (bookingsInMonth.TryGetValue(key, out var bookedTimes))
                {
                    var free = Math.Max(0, totalSlots - bookedTimes.Count);
                    result[dayDate.ToString("yyyy-MM-dd")] = free;
                }
                else
                {
                    result[dayDate.ToString("yyyy-MM-dd")] = totalSlots;
                }
            }

            return Ok(result);
        }

        [HttpGet("byPatient")]
        public async Task<IActionResult> GetAppointmentsForPatient([FromQuery] string birthNumber)
        {
            if (string.IsNullOrWhiteSpace(birthNumber))
            {
                return BadRequest(new { message = "birthNumber query parameter is required." });
            }

            birthNumber = birthNumber.Trim();

            var todayUtc = DateTime.UtcNow.Date;

            var appts = await _context.Appointments
                .Where(a => a.PatientBirthNumber == birthNumber && a.ReservationDay >= todayUtc && (string.IsNullOrEmpty(a.Status) || a.Status == "scheduled"))
                .Include(a => a.Patient)
                .OrderBy(a => a.ReservationDay).ThenBy(a => a.ReservationTime)
                .Select(a => new
                {
                    a.AppointmentId,
                    ReservationDay = a.ReservationDay,
                    ReservationTime = a.ReservationTime,
                    PatientName = a.Patient != null ? string.Concat(a.Patient.FirstName, " ", a.Patient.LastName) : "Unknown Patient",
                    DoctorId = a.DoctorId,
                    DoctorName = a.DoctorName
                })
                .ToListAsync();

            return Ok(appts);
        }

        private static List<string> GenerateValidTimeSlots()
        {
            var sessionDuration = TimeSpan.FromMinutes(30);
            var breakDuration = TimeSpan.FromMinutes(5);
            var clinicStart = new TimeSpan(8, 0, 0);
            var clinicEnd = new TimeSpan(16, 0, 0);
            var lunchStart = new TimeSpan(12, 0, 0);
            var lunchEnd = new TimeSpan(13, 0, 0);

            var timeSlots = new List<string>();

            var currentTime = clinicStart;

            while (currentTime + sessionDuration <= clinicEnd)
            {
                if (currentTime >= lunchStart && currentTime < lunchEnd)
                {
                    currentTime = lunchEnd;
                    continue;
                }

                timeSlots.Add(currentTime.ToString(@"hh\:mm"));
                currentTime = currentTime + sessionDuration + breakDuration;
            }

            return timeSlots;
        }

        private static DateTime GetNextWorkingDay(DateTime now)
        {
            var weekday = now.DayOfWeek;

            if (weekday == DayOfWeek.Friday || weekday == DayOfWeek.Saturday)
            {
                var daysUntilMonday = ((int)DayOfWeek.Monday - (int)weekday + 7) % 7;
                return now.Date.AddDays(daysUntilMonday == 0 ? 7 : daysUntilMonday);
            }

            if (weekday == DayOfWeek.Sunday)
            {
                return now.Date.AddDays(1);
            }

            return now.Date.AddDays(1);
        }
    }
}