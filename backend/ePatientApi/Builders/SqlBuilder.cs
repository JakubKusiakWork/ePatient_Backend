namespace ePatientApi.Builders
{
    /// <summary>
    /// Helper methods to obtain SQL/database configuration values.
    /// </summary>
    public static class SqlBuilder
    {
        /// <summary>
        /// Retrieves the database connection string from configuration.
        /// </summary>
        /// <param name="configuration">Application configuration.</param>
        /// <returns>The connection string from the configuration key "DatabaseBeby:ConnectionString".</returns>
        public static string GetConnectionString(IConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }
            var connectionString = configuration["DatabaseBeby:ConnectionString"];

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = configuration.GetConnectionString("DefaultConnection")
                                   ?? configuration["ConnectionStrings:DefaultConnection"];
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = Environment.GetEnvironmentVariable("EPATIENT_CONNECTION");
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Database connection string not found. Provide one of: 'DatabaseBeby:ConnectionString', a 'ConnectionStrings:DefaultConnection' entry, or set the 'EPATIENT_CONNECTION' environment variable.");
            }

            return connectionString;
        }
    }
}