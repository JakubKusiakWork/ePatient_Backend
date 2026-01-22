using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Linq;
using ePatientApi.Builders;

namespace ePatientApi.DataAccess
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables();

            var config = builder.Build();
            var keyVaultName = config["KeyVault:VaultName"];
            var keyVaultUri = config["KeyVault:VaultUri"];
            if (!string.IsNullOrEmpty(keyVaultName) || !string.IsNullOrEmpty(keyVaultUri))
            {
                try
                {
                    var tenantId = config["KeyVault:TenantId"];
                    var clientId = config["KeyVault:ClientId"];
                    var clientSecret = config["KeyVault:ClientSecret"];

                    if (!string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret))
                    {
                        var vaultUri = keyVaultUri;
                        if (string.IsNullOrEmpty(vaultUri) && !string.IsNullOrEmpty(keyVaultName))
                            vaultUri = $"https://{keyVaultName}.vault.azure.net/";

                        if (!string.IsNullOrEmpty(vaultUri))
                        {
                            try
                            {
                                var credential = new Azure.Identity.ClientSecretCredential(tenantId, clientId, clientSecret);
                                var kvBuilder = new ConfigurationBuilder()
                                    .AddConfiguration(config)
                                    .AddAzureKeyVault(new Uri(vaultUri), credential, new Azure.Extensions.AspNetCore.Configuration.Secrets.KeyVaultSecretManager());

                                config = kvBuilder.Build();
                            }
                            catch
                            {
                            }
                        }
                    }
                }
                catch
                {
                }
            }
        
            string? connectionString = null;
            try
            {
                connectionString = SqlBuilder.GetConnectionString(config);
            }
            catch
            {
                var candidates = new[] {
                    Environment.GetEnvironmentVariable("EPATIENT_CONNECTION"),
                    Environment.GetEnvironmentVariable("DatabaseBeby__ConnectionString"),
                    Environment.GetEnvironmentVariable("DatabaseBeby:ConnectionString"),
                    Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"),
                    Environment.GetEnvironmentVariable("ConnectionStrings:DefaultConnection"),
                    Environment.GetEnvironmentVariable("DefaultConnection")
                };

                connectionString = candidates.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("No database connection string found. Set EPATIENT_CONNECTION or DatabaseBeby__ConnectionString or add ConnectionStrings:DefaultConnection in appsettings.json.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
