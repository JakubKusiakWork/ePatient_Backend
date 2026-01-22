namespace ePatientApi.Builders
{
    /// <summary>
    /// Utility helpers to validate JWT-related configuration values.
    /// </summary>
    public static class JwtConfiguration
    {
        /// <summary>
        /// Validates that JWT configuration values exist in the provided <see cref="IConfiguration"/>.
        /// Throws <see cref="InvalidOperationException"/> when required values are missing.
        /// </summary>
        /// <param name="configuration">Application configuration to validate.</param>
        public static void ValidateJwtSettings(IConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            string? jwtKey = configuration["Jwt:Key"];
            string? jwtIssuer = configuration["Jwt:Issuer"];
            string? jwtAudience = configuration["Jwt:Audience"];

            if (string.IsNullOrWhiteSpace(jwtKey) || string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
            {
                throw new InvalidOperationException("JWT configuration values (Key, Issuer, Audience) are required.");
            }
        }

        /// <summary>
        /// Backwards-compatible wrapper used by existing callers. Calls <see cref="ValidateJwtSettings(IConfiguration)"/>.
        /// </summary>
        /// <param name="configuration">Application configuration to validate.</param>
        public static void ValidateJWT(IConfiguration configuration)
        {
            ValidateJwtSettings(configuration);
        }
    }
}