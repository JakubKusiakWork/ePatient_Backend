namespace ePatientApi.Interfaces
{
    public interface IDoveraService
    {
        Task<bool> CheckAsync(string birthNumber, DateTime date, CancellationToken cancelToken);
    }
}