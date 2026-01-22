namespace ePatientApi.Builders
{
    /// <summary>
    /// Provides extension methods to configure Cross-Origin Resource Sharing (CORS) policies for the application.
    /// </summary>
    public static class CorsBuilder
    {
        /// <summary>
        /// Adds a named CORS policy to the service collection. The policy values are read from configuration
        /// keys under the "CORS" section (deployURL, localhostURL, testURL). If no origins are configured,
        /// a localhost origin for Angular development (http://localhost:4200) is used as a fallback.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="configuration">Application configuration used to read CORS origins.</param>
        public static void AddCustomCors(this IServiceCollection services, IConfiguration configuration)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            services.AddCors(options =>
            {
                options.AddPolicy("AllowAngularApp", policy =>
                {
                    var origins = new List<string>(capacity: 4);

                    string? deployUrl = configuration["CORS:deployURL"];
                    string? localhostUrl = configuration["CORS:localhostURL"];
                    string? testUrl = configuration["CORS:testURL"];

                    if (!string.IsNullOrWhiteSpace(deployUrl))
                    {
                        origins.Add(deployUrl);
                    }

                    if (!string.IsNullOrWhiteSpace(localhostUrl))
                    {
                        origins.Add(localhostUrl);
                    }

                    if (!string.IsNullOrWhiteSpace(testUrl))
                    {
                        origins.Add(testUrl);
                    }

                    const string defaultLocalhostAngular = "http://localhost:4200";

                    if (!origins.Contains(defaultLocalhostAngular, StringComparer.OrdinalIgnoreCase))
                    {
                        origins.Add(defaultLocalhostAngular);
                        Console.WriteLine($"CORS: added default origin {defaultLocalhostAngular} for development.");
                    }
                    Console.WriteLine($"CORS Origins configured: {string.Join(", ", origins)}");
                    if (origins.Any())
                    {
                        policy.WithOrigins(origins.ToArray())
                              .AllowAnyMethod()
                              .AllowAnyHeader()
                              .AllowCredentials();
                    }
                    else
                    {
                        policy.WithOrigins(defaultLocalhostAngular)
                              .AllowAnyMethod()
                              .AllowAnyHeader()
                              .AllowCredentials();
                    }
                });
            });
        }
    }
}