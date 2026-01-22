using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ePatientApi.Interfaces;

namespace ePatientApi.Builders
{
    /// <summary>
    /// Provides extension methods to configure JWT authentication for the application.
    /// </summary>
    public static class JwtBuilder
    {
        /// <summary>
        /// Adds JWT-based authentication to the service collection using settings from <see cref="IConfiguration"/>.
        /// Expects configuration keys: Jwt:Key, Jwt:Issuer, Jwt:Audience.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="configuration">Application configuration for JWT settings.</param>
        public static void AddCustomJwt(this IServiceCollection services, IConfiguration configuration)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            string? jwtKey = configuration["Jwt:Key"];

            if (string.IsNullOrWhiteSpace(jwtKey))
            {
                throw new ArgumentNullException("Jwt:Key", "JWT key cannot be null or empty.");
            }

            string? jwtIssuer = configuration["Jwt:Issuer"];
            string? jwtAudience = configuration["Jwt:Audience"];

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtIssuer,
                        ValidAudience = jwtAudience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            var jwtSecurityToken = context.SecurityToken as System.IdentityModel.Tokens.Jwt.JwtSecurityToken;
                            var blacklistService = context.HttpContext.RequestServices.GetRequiredService<ITokenBlacklistService>();

                            if (jwtSecurityToken != null && blacklistService.IsTokenBlacklisted(jwtSecurityToken.RawData))
                            {
                                context.Fail("This token has been revoked.");
                            }

                            return Task.CompletedTask;
                        }
                    };
                });
        }
    }
}