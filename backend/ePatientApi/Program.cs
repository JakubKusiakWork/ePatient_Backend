using Microsoft.EntityFrameworkCore;
using ePatientApi.Builders;
using System.Diagnostics.CodeAnalysis;
using ePatientApi.Services;
using ePatientApi.Wrappers;
using ePatientApi.Interfaces;
using ePatientApi.DataAccess;
using Npgsql;
using Azure.Identity;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.HttpOverrides;

namespace ePatientApi
{
    /// <summary>
    /// Main program class for configuring and running the ePatient API application.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var keyVaultName = builder.Configuration["KeyVault:VaultName"];
            if (!string.IsNullOrEmpty(keyVaultName))
            {
                var keyVaultUriString = builder.Configuration["KeyVault:VaultUri"];

                if (!string.IsNullOrEmpty(keyVaultUriString) &&
                    Uri.TryCreate(keyVaultUriString, UriKind.Absolute, out var keyVaultUri))
                {
                    var tenantId = builder.Configuration["KeyVault:TenantId"];
                    var clientId = builder.Configuration["KeyVault:ClientId"];
                    var clientSecret = builder.Configuration["KeyVault:ClientSecret"];

                    if (!string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret))
                    {
                        builder.Configuration.AddAzureKeyVault(
                            keyVaultUri,
                            new ClientSecretCredential(tenantId, clientId, clientSecret)
                        );
                    }
                    else
                    {
                        Console.WriteLine("Azure Key Vault credentials (TenantId/ClientId/ClientSecret) are not fully configured; skipping Key Vault integration.");
                    }
                }
                else
                {
                    Console.WriteLine("KeyVault:VaultUri is not configured or invalid; skipping Azure Key Vault integration.");
                }
            }

            builder.Logging.AddConsole();
            builder.Logging.SetMinimumLevel(LogLevel.Debug);

            JwtConfiguration.ValidateJWT(builder.Configuration);

            string connectionString;
            try
            {
                connectionString = SqlBuilder.GetConnectionString(builder.Configuration);
                using var connection = new NpgsqlConnection(connectionString);
                connection.OpenAsync().GetAwaiter().GetResult();
                Console.WriteLine("PostgreSQL connection successful!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection failed: {ex.Message}");
                throw;
            }

            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(connectionString);
            });

            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddDataProtection();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.None;
            });

            builder.Services.AddCustomCors(builder.Configuration);
            builder.Services.AddSingleton<ITokenBlacklistService, BlacklistTokenService>();
            builder.Services.AddCustomJwt(builder.Configuration);
            builder.Services.AddScoped<IJwtToken, JwtTokenService>();
            builder.Services.AddScoped<IDbConnectionWrapper>(_ =>
                new DbConnectionWrapper(connectionString));
            builder.Services.AddScoped<IHomepageData, HomepageDataService>();
            builder.Services.AddScoped<IHealthcardService, HealthcardService>();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<IVersioningService, VersioningService>();
            builder.Services.AddSingleton<IEmailSender, EmailSenderService>();
            builder.Services.AddScoped<IVsZPService, VsZPService>();
            builder.Services.AddScoped<IUnionService, UnionService>();
            builder.Services.AddScoped<IDoveraService, DoveraService>();
            builder.Services.AddScoped<GoogleCalendarService>();
            builder.Services.AddScoped<DoctorEmailService>();
            builder.Services.AddScoped<IPasswordReset, PasswordResetService>();
            builder.Services.AddHttpClient<DoctorVerificationService>();
            builder.Services.AddHttpClient("OllamaClient");
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "ePatient API",
                    Version = "v1"
                });

                var swaggerServer = builder.Configuration["Swagger:ServerUrl"];
                if (!string.IsNullOrWhiteSpace(swaggerServer))
                {
                    c.AddServer(new OpenApiServer { Url = swaggerServer });
                }
            });

            var app = builder.Build();

            app.UseSession();

            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/swagger") && 
                    (string.IsNullOrEmpty(context.Request.Path.Value) || !context.Request.Path.Value.Contains("swagger.json")) && 
                    !context.Request.Path.StartsWithSegments("/api/swaggeraccess"))
                {
                    var unlocked = context.Session.GetString("swagger_unlocked");
                    if (unlocked != "true")
                    {
                        context.Response.Redirect("/api/swaggeraccess/swagger-access");
                        return;
                    }
                }
                await next();
            });

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            ForwardedHeadersOptions? fhOptions = app.Services.GetService(typeof(ForwardedHeadersOptions)) as ForwardedHeadersOptions;
            if (fhOptions != null)
            {
                fhOptions.KnownNetworks.Clear();
                fhOptions.KnownProxies.Clear();
            }

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                var swaggerServerUrl = app.Configuration["Swagger:ServerUrl"];
                if (app.Environment.IsProduction() && !string.IsNullOrWhiteSpace(swaggerServerUrl))
                {
                    var baseUrl = swaggerServerUrl.TrimEnd('/');
                    c.SwaggerEndpoint($"{baseUrl}/swagger/v1/swagger.json", "ePatient API V1");
                }
                else
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ePatient API V1");
                }

                c.RoutePrefix = "swagger";
            });

            if (app.Environment.IsProduction())
            {
                app.UseHttpsRedirection();
            }

            app.UseRouting();
            app.UseCors("AllowAngularApp");
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.MapGet("/", (HttpContext context) => $"ePatient API is running successfully on {context.Request.Host}.");
            app.MapGet("/api/register", (HttpContext context) => $"ePatient API register endpoint is available on {context.Request.Host}.");
            app.Run();
        }
    }
}