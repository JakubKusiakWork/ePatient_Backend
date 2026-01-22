using ePatientApi.Interfaces;
using ePatientApi.DataAccess;
using ePatientApi.Dtos;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using ePatientApi.Models;
using System.Text;

namespace ePatientApi.Services
{
    public class PasswordResetService : IPasswordReset
    {
        private readonly  AppDbContext myContext;
        private readonly IEmailSender myEmailSender;
        private readonly IConfiguration myConfiguration;

        public PasswordResetService(AppDbContext context, IEmailSender emailSender, IConfiguration config)
        {
            myContext = context;
            myEmailSender = emailSender;
            myConfiguration = config;
        }

        public async Task RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return;
            }

            var patient = await myContext.RegisteredPatients
                .FirstOrDefaultAsync(p => p.Email == request.Email, cancelToken);

            if (patient == null)
            {
                return;
            }

            var rawToken = GenerateSecureToken(32);
            var hashedToken = ComputeSha256(rawToken);

            var expiresMinutes = myConfiguration.GetValue<int?>("Auth:PasswordReset:ExpiresMinutes") ?? 15;

            var resetToken = new ForgotPassword
            {
                PatientBirthNumber = patient.BirthNumber,
                TokenHash = hashedToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expiresMinutes)
            };

            myContext.ForgotPassword.Add(resetToken);
            await myContext.SaveChangesAsync(cancelToken);

            var frontendBaseUrl = myConfiguration["CORS:localhostURL"] ?? "http://localhost:4200";
            var resetUrl = $"{frontendBaseUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";

            var subject = "Password reset for patients.";
            var body = $@"
            Ahoj {patient.FirstName},

            We have received a request to change the password for your ePatient account.

            Click this link (valid for {expiresMinutes}):
            {resetUrl}

            If you did not request a password reset, you can ignore this emial.";

            await myEmailSender.SendEmailAsync(patient.Email, subject, body);
        }

        public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                throw new ArgumentException("Invalid payload.");
            }

            var rawToken = Uri.UnescapeDataString(request.Token);

            var hashedToken = ComputeSha256(rawToken);

            var resetToken = await myContext.ForgotPassword
                .Include(t => t.Patient)
                .FirstOrDefaultAsync(t =>
                    t.TokenHash == hashedToken &&
                    !t.Used &&
                    t.ExpiresAt > DateTime.UtcNow, cancelToken);

            if (resetToken == null || resetToken.Patient == null)
            {
                throw new InvalidOperationException("Invalid or expired password reset token.");
            }

            resetToken.Patient.HashedPassword = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

            resetToken.Used = true;

            await myContext.SaveChangesAsync(cancelToken);
        }

        private static string GenerateSecureToken(int lengthBytes)
        {
            var bytes = new byte[lengthBytes];
            RandomNumberGenerator.Fill(bytes);

            return Convert.ToBase64String(bytes);
        }

        private static string ComputeSha256(string input)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha.ComputeHash(bytes);

            return Convert.ToBase64String(hash);
        }
    }
}