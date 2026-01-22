using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using ePatientApi.Builders;

namespace ePatientApi.Tests
{
    [TestClass]
    public class JwtBuilderTests
    {
        private Mock<IConfiguration> _mockConfig;
        private const string ValidJwtKey = "ThisIsASecretKeyForTesting123456789012345678901234";
        private const string ValidIssuer = "TestIssuer";
        private const string ValidAudience = "TestAudience";

        [TestInitialize]
        public void Setup()
        {
            // Arrange: Initialize mock configuration with valid values.
            _mockConfig = new Mock<IConfiguration>();
            _mockConfig.Setup(c => c["Jwt:Key"]).Returns(ValidJwtKey);
            _mockConfig.Setup(c => c["Jwt:Issuer"]).Returns(ValidIssuer);
            _mockConfig.Setup(c => c["Jwt:Audience"]).Returns(ValidAudience);
        }

        #region ValidConfigTests

        [TestMethod]
        public void AddCustomJwt_ValidConfig_ConfiguresJwtOptions()
        {
            // Arrange: Initialize a service collection.
            var services = new ServiceCollection();

            // Act: Add JWT authentication with the mock configuration.
            services.AddCustomJwt(_mockConfig.Object);

            // Assert: Verify the JwtBearerOptions are configured correctly.
            var serviceProvider = services.BuildServiceProvider();
            var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
            var jwtBearerOptions = optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);
            Assert.IsNotNull(jwtBearerOptions, "JwtBearerOptions should be configured.");
            var tokenValidationParameters = jwtBearerOptions.TokenValidationParameters;
            Assert.IsNotNull(tokenValidationParameters, "TokenValidationParameters should be configured.");
            Assert.IsTrue(tokenValidationParameters.ValidateIssuer, "ValidateIssuer should be true.");
            Assert.IsTrue(tokenValidationParameters.ValidateAudience, "ValidateAudience should be true.");
            Assert.IsTrue(tokenValidationParameters.ValidateLifetime, "ValidateLifetime should be true.");
            Assert.IsTrue(tokenValidationParameters.ValidateIssuerSigningKey, "ValidateIssuerSigningKey should be true.");
            Assert.AreEqual(ValidIssuer, tokenValidationParameters.ValidIssuer, "ValidIssuer should match.");
            Assert.AreEqual(ValidAudience, tokenValidationParameters.ValidAudience, "ValidAudience should match.");
            var signingKey = tokenValidationParameters.IssuerSigningKey as SymmetricSecurityKey;
            Assert.IsNotNull(signingKey, "IssuerSigningKey should be a SymmetricSecurityKey.");
            Assert.AreEqual(ValidJwtKey, Encoding.UTF8.GetString(signingKey.Key), "IssuerSigningKey should match.");
        }

        [TestMethod]
        public void AddCustomJwt_NullIssuer_ConfiguresJwtOptions()
        {
            // Arrange: Initialize a service collection and mock configuration with null Issuer.
            var services = new ServiceCollection();
            _mockConfig.Setup(c => c["Jwt:Issuer"]).Returns((string)null);

            // Act: Add JWT authentication with the mock configuration.
            services.AddCustomJwt(_mockConfig.Object);

            // Assert: Verify the JwtBearerOptions are configured with null Issuer.
            var serviceProvider = services.BuildServiceProvider();
            var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
            var jwtBearerOptions = optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);
            Assert.IsNotNull(jwtBearerOptions, "JwtBearerOptions should be configured.");
            var tokenValidationParameters = jwtBearerOptions.TokenValidationParameters;
            Assert.IsNotNull(tokenValidationParameters, "TokenValidationParameters should be configured.");
            Assert.IsTrue(tokenValidationParameters.ValidateIssuer, "ValidateIssuer should be true.");
            Assert.IsNull(tokenValidationParameters.ValidIssuer, "ValidIssuer should be null.");
            Assert.AreEqual(ValidAudience, tokenValidationParameters.ValidAudience, "ValidAudience should match.");
        }

        [TestMethod]
        public void AddCustomJwt_NullAudience_ConfiguresJwtOptions()
        {
            // Arrange: Initialize a service collection and mock configuration with null Audience.
            var services = new ServiceCollection();
            _mockConfig.Setup(c => c["Jwt:Audience"]).Returns((string)null);

            // Act: Add JWT authentication with the mock configuration.
            services.AddCustomJwt(_mockConfig.Object);

            // Assert: Verify the JwtBearerOptions are configured with null Audience.
            var serviceProvider = services.BuildServiceProvider();
            var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
            var jwtBearerOptions = optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);
            Assert.IsNotNull(jwtBearerOptions, "JwtBearerOptions should be configured.");
            var tokenValidationParameters = jwtBearerOptions.TokenValidationParameters;
            Assert.IsNotNull(tokenValidationParameters, "TokenValidationParameters should be configured.");
            Assert.IsTrue(tokenValidationParameters.ValidateAudience, "ValidateAudience should be true.");
            Assert.IsNull(tokenValidationParameters.ValidAudience, "ValidAudience should be null.");
            Assert.AreEqual(ValidIssuer, tokenValidationParameters.ValidIssuer, "ValidIssuer should match.");
        }

        #endregion

        #region InvalidConfigTests

        [TestMethod]
        public void AddCustomJwt_NullJwtKey_ThrowsArgumentNullException()
        {
            // Arrange: Initialize a service collection and mock configuration with null Jwt:Key.
            var services = new ServiceCollection();
            _mockConfig.Setup(c => c["Jwt:Key"]).Returns((string)null);

            // Act & Assert: Verify that adding JWT authentication throws ArgumentNullException.
            var exception = Assert.ThrowsException<ArgumentNullException>(() => services.AddCustomJwt(_mockConfig.Object));
            Assert.AreEqual("Jwt:Key", exception.ParamName, "Exception parameter should be 'Jwt:Key'.");
            Assert.IsTrue(exception.Message.Contains("JWT key cannot be null"), "Exception message should indicate missing JWT key.");
        }

        #endregion
    }
}