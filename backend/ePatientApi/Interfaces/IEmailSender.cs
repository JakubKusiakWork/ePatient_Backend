namespace ePatientApi.Interfaces
{
    /// <summary>
    /// Interface for sending emails.
    /// </summary>
    public interface IEmailSender
    {
        Task SendEmail(string toEmail, int doctorId, CancellationToken cancelToken = default);
        Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancelToken = default);
    }
}