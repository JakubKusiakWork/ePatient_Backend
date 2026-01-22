using ePatientApi.DataAccess;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using ePatientApi.Interfaces;
using System.Reflection.Metadata;

namespace ePatientApi.Services
{
    /// <summary>
    /// Service for handling doctor email verification and code generation.
    /// </summary>
    public class DoctorEmailService
    {
        private readonly AppDbContext _context;
        private readonly IEmailSender _emailSender;

        public DoctorEmailService(AppDbContext context, IEmailSender emailSender)
        {
            _context = context;
            _emailSender = emailSender;
        }

        public async Task<string> GenerateAndSendCode(string email, CancellationToken cancelToken = default)
        {
            var clinician = await _context.DoctorEmails
                .SingleOrDefaultAsync(doctor => doctor.Email == email, cancelToken);
            if (clinician == null)
            {
                var maxId = await _context.DoctorEmails.MaxAsync(d => (int?)d.DoctorId, cancelToken) ?? 0;
                clinician = new Models.DoctorEmail
                {
                    DoctorId = maxId + 1,
                    Email = email,
                    State = false,
                    RegistrationCode = null,
                    HashedPassword = null,
                    GeneratedAt = null,
                    ExpiresAt = null
                };

                _context.DoctorEmails.Add(clinician);
                await _context.SaveChangesAsync(cancelToken);
            }

            if (clinician.State)
            {
                return "EXISTS";
            }

            if (clinician.ExpiresAt.HasValue && clinician.ExpiresAt > DateTime.UtcNow)
            {
                return "ALREADY_SENT";
            }

            int registrationCode = RandomNumberGenerator.GetInt32(10_000, 100_000);

            clinician.RegistrationCode = registrationCode.ToString();
            clinician.GeneratedAt = DateTime.UtcNow;
            clinician.ExpiresAt = DateTime.UtcNow.AddMinutes(3);

            await _context.SaveChangesAsync(cancelToken);
            await _emailSender.SendEmail(email, registrationCode, cancelToken);

            return "SUCCESS";
        }

        public async Task<string> setPassword(string email, string code, string password, CancellationToken cancelToken = default)
        {
            var doctor = await _context.DoctorEmails.SingleOrDefaultAsync(doctor => doctor.Email == email, cancelToken);

            if (doctor == null)
            {
                return "NOT_FOUND";
            }

            if (doctor.HashedPassword != null)
            {
                return "EXISTS";
            }

            if (doctor.RegistrationCode != code)
            {
                return "INVALID_CODE";
            }

            if (doctor.RegistrationCode == null)
            {
                return "NO_CODE";
            }

            if (doctor.ExpiresAt == null || DateTime.UtcNow > doctor.ExpiresAt)
            {
                return "EXPIRED_CODE";
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < 0)
            {
                return "WEAK_PASSWORD";
            }

            var hash = BCrypt.Net.BCrypt.HashPassword(password);
            var hashCode = BCrypt.Net.BCrypt.HashPassword(code);
            doctor.HashedPassword = hash;
            doctor.RegistrationCode = hashCode;
            doctor.GeneratedAt = null;
            doctor.ExpiresAt = null;

            await _context.SaveChangesAsync(cancelToken);

            return "SUCCESS";
        }

        public async Task<string> verifyCode(string email, string code, CancellationToken cancelToken = default)
        {
            
            try
            {
                var clinician = await _context.DoctorEmails
                    .SingleOrDefaultAsync(cl => cl.Email == email, cancelToken);

                if (clinician == null)
                {
                    return "NOT_FOUND";
                }

                if (clinician.State)
                {
                    return "ALREADY_VERIFIED";
                }

                if (!clinician.ExpiresAt.HasValue || clinician.ExpiresAt <= DateTime.UtcNow)
                {
                    return "EXPIRED_CODE";
                }

                if (clinician.RegistrationCode != code)
                {
                    return "INVALID_CODE";
                }

                clinician.State = true;
                await _context.SaveChangesAsync(cancelToken);

                // Console.WriteLine($"Email: {email}, Code: {code}");

                return "SUCCESS";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VerifyCode ERROR] {ex.GetType().Name}: {ex.Message}");
                return "INTERNAL_ERROR";
            }
        }
    }
}