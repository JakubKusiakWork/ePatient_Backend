using System.Data;

namespace ePatientApi.Interfaces
{
    /// <summary>
    /// Wrapper interface for database connection operations to support testability and resource management.
    /// </summary>
    public interface IDbConnectionWrapper : IDisposable
    {
        Task OpenAsync(CancellationToken cancellationToken);
        Task<object?> ExecuteScalarAsync(string commandText, CancellationToken cancellationToken);
        IDbCommand CreateCommand();
        IDbConnection GetConnection();
    }
}