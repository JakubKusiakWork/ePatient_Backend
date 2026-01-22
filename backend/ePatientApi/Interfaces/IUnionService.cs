namespace ePatientApi.Interfaces
{
    public interface IUnionService
    {
        Task<bool> CheckAsync(string birthNumber, DateTime date, CancellationToken cancelToken);
    }
}