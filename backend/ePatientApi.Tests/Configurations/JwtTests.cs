using ePatientApi.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace ePatientApi.Tests
{
    /// <summary>
    /// Unit tests for JWT configuration validation.
    /// </summary>
    [TestClass]
    public class JwtConfigurationTests
    {
        private IConfiguration _configuration;

        [TestInitialize]
        public void Setup()
        {
            // Arrange: Load configuration from AppSettings.json.
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("AppSettings.json", optional: false, reloadOnChange: true);

            _configuration = configBuilder.Build();
        }

        #region ValidationFailureTests

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ValidateJwt_MissingKey_ThrowsInvalidOperationException()
        {
            // Arrange: Create a configuration missing the Jwt:Key.
            var settings = new Dictionary<string, string>
            {
                { "Jwt:Issuer", "TestIssuer" },
                { "Jwt:Audience", "TestAudience" }
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            // Act: Validate the configuration.
            JwtConfiguration.ValidateJWT(config);

            // Assert: Expects InvalidOperationException (handled by ExpectedException).
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ValidateJwt_MissingIssuer_ThrowsInvalidOperationException()
        {
            // Arrange: Create a configuration missing the Jwt:Issuer.
            var settings = new Dictionary<string, string>
            {
                { "Jwt:Key", "TestKey" },
                { "Jwt:Audience", "TestAudience" }
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            // Act: Validate the configuration.
            JwtConfiguration.ValidateJWT(config);

            // Assert: Expects InvalidOperationException (handled by ExpectedException).
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ValidateJwt_MissingAudience_ThrowsInvalidOperationException()
        {
            // Arrange: Create a configuration missing the Jwt:Audience.
            var settings = new Dictionary<string, string>
            {
                { "Jwt:Key", "TestKey" },
                { "Jwt:Issuer", "TestIssuer" }
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            // Act: Validate the configuration.
            JwtConfiguration.ValidateJWT(config);

            // Assert: Expects InvalidOperationException (handled by ExpectedException).
        }

        #endregion

        #region ValidationSuccessTests

        [TestMethod]
        public void ValidateJwt_CompleteConfig_PassesValidation()
        {
            // Arrange: Use a complete configuration from AppSettings.json.
            var config = _configuration;

            // Act: Validate the configuration.
            JwtConfiguration.ValidateJWT(config);

            // Assert: No exception indicates successful validation.
        }

        #endregion
    }
}