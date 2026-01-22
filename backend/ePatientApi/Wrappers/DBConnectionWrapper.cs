using Npgsql;
using System.Data;
using ePatientApi.Interfaces;
using System.Diagnostics.CodeAnalysis;

namespace ePatientApi.Wrappers
{
    /// <summary>
    /// Wrapper for managing PostgreSQL database connections and commands.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class DbConnectionWrapper : IDbConnectionWrapper
    {
        private readonly NpgsqlConnection myConnection;

        public DbConnectionWrapper(string connectionString)
        {
            myConnection = new NpgsqlConnection(connectionString);
        }

        public async Task OpenAsync(CancellationToken token)
        {
            if (myConnection.State != ConnectionState.Open)
            {
                await myConnection.OpenAsync(token);
            }
        }

        public async Task<object?> ExecuteScalarAsync(string commandText, CancellationToken token)
        {
            using var command = myConnection.CreateCommand();
            command.CommandText = commandText;
            var result = await command.ExecuteScalarAsync(token);
            return result ?? DBNull.Value;
        }

        public IDbCommand CreateCommand()
        {
            return myConnection.CreateCommand();
        }

        public IDbConnection GetConnection()
        {
            return myConnection;
        }

        public void Dispose()
        {
            myConnection?.Dispose();
        }
    }
}