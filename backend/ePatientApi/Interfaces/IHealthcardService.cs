using ePatientApi.Models;

namespace ePatientApi.Interfaces
{
    public interface IHealthcardService
    {
        Task<HealthCard> GetByPatientIdAsync(string patientId);
        Task<HealthCard> UpsertAsync(HealthCard card);
        Task<byte[]> GeneratePdfAsync(string patientId);
        Task<byte[]> GenerateQrAsync(string patientId);
    }
}
