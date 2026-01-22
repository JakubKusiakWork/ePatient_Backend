using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using ePatientApi.Builders;

namespace ePatientApi.Tests
{
    [TestClass]
    public class CorsBuilderTests
    {
        private IConfiguration _configuration;
        private ServiceCollection _services;

        [TestInitialize]
        public void Setup()
        {
            // Arrange: Initialize configuration and service collection.
            _configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("AppSettings.json", optional: false, reloadOnChange: false)
                .Build();
            _services = new ServiceCollection();
        }

        #region ValidConfigTests

        [TestMethod]
        public void AddCustomCors_ValidConfig_ConfiguresPolicy()
        {
            // Arrange: Use the configuration loaded from AppSettings.json.
            var configuration = _configuration;
            var services = _services;

            // Act: Add CORS configuration to the services.
            services.AddCustomCors(configuration);
            var serviceProvider = services.BuildServiceProvider();
            var corsOptions = serviceProvider.GetRequiredService<IOptions<CorsOptions>>();

            // Assert: Verify the CORS policy is configured correctly.
            var policy = corsOptions.Value.GetPolicy("AllowAngularApp");
            Assert.IsNotNull(policy, "CORS policy should be configured.");
            CollectionAssert.Contains(policy.Origins.ToList(), "https://epatient.azurewebsites.net", "Policy should allow Azure website origin.");
            CollectionAssert.Contains(policy.Origins.ToList(), "http://localhost:4200", "Policy should allow localhost origin.");
            Assert.IsTrue(policy.AllowAnyMethod, "Policy should allow any HTTP method.");
            Assert.IsTrue(policy.AllowAnyHeader, "Policy should allow any header.");
            Assert.IsTrue(policy.SupportsCredentials, "Policy should support credentials.");
        }

        #endregion
    }
}