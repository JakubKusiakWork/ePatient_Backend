using ePatientApi.Interfaces;
using System.Net;
using System.Net.Mail;

namespace ePatientApi.Services
{
    /// <summary>
    /// Service for sending emails using SMTP.
    /// </summary>
    public sealed class EmailSenderService : IEmailSender, IDisposable
    {
        private readonly SmtpClient _Client;
        private readonly string _fromAddress;
        private readonly ILogger<EmailSenderService> _logger;

        public EmailSenderService(IConfiguration configuration, ILogger<EmailSenderService> logger)
        {
            _logger = logger;

            var host = configuration["Smtp:Host"];
            var port = int.Parse(configuration["Smtp:Port"] ?? "587");
            var username = configuration["Smtp:Email"];
            var password = configuration["Smtp:Password"];
            _fromAddress = configuration["Smtp:Email"]!;

            _logger.LogInformation("Configuring SMTP client. Host={Host}, Port={Port}, Username={UserMasked}", host, port, MaskUsername(username));

            _Client = new SmtpClient(host, port)
            {
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(username, password)
            };
        }

        public async Task SendEmail(string toEmail, int doctorId, CancellationToken cancelToken = default)
        {
            var msg = new MailMessage(_fromAddress, toEmail)
            {
                Subject = "Your verification code for ePatientApp",
                Body = $"Your verification code is: {doctorId}"
            };

            await _Client.SendMailAsync(msg, cancelToken);
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancelToken = default)
        {
            var msg = new MailMessage(_fromAddress, toEmail)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            try
            {
                _logger.LogInformation("Sending email to {ToEmail} with subject {Subject}", toEmail, subject);
                await _Client.SendMailAsync(msg, cancelToken);
                _logger.LogInformation("Email successfully sent to {ToEmail}", toEmail);
            }
            catch (SmtpException smtpEx)
            {
                _logger.LogError(smtpEx, "SMTP error while sending email to {ToEmail}: {Message}", toEmail, smtpEx.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while sending email to {ToEmail}: {Message}", toEmail, ex.Message);
                throw;
            }
        }

        private static string MaskUsername(string? username)
        {
            if (string.IsNullOrEmpty(username)) return "<empty>";
            var at = username.IndexOf('@');
            if (at <= 1) return "***" + (at > 0 ? username.Substring(at) : string.Empty);
            return username.Substring(0, 1) + new string('*', Math.Min(6, Math.Max(0, at - 2))) + username.Substring(at - 1);
        }

        public void Dispose()
        {
            try
            {
                _Client?.Dispose();
                _logger.LogInformation("Disposed SMTP client in EmailSenderService.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while disposing SMTP client");
            }
        }
    }
}