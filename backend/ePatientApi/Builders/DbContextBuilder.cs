using Microsoft.EntityFrameworkCore;
using ePatientApi.DataAccess;

namespace ePatientApi.Builders
{
    /// <summary>
    /// Provides extension methods to register the application's <see cref="AppDbContext"/>.
    /// </summary>
    public static class DbContextBuilder
    {
        /// <summary>
        /// Adds the application's <see cref="AppDbContext"/> to the service collection using a Postgres connection string.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="connectionString">The Postgres connection string.</param>
        public static void AddAppDbContext(this IServiceCollection services, string connectionString)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string must be provided.", nameof(connectionString));
            }

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null
                    );
                });
            });
        }
    }
}