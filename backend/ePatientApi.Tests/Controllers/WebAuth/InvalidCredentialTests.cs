using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using ePatientApi.Controllers;
using ePatientApi.DataAccess;
using ePatientApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System;
using System.Threading;
using Fido2NetLib;
using Fido2NetLib.Objects;
using System.Linq;

namespace ePatientApi.Tests.Controllers.WebAuthn
{
    [TestClass]
    public class WebAuthnControllerAuthenticateCompletePatientTests
    {
        private AppDbContext _dbContext;
        private Mock<IConfiguration> _configMock;
        private Mock<ILogger<WebAuthnController>> _loggerMock;
        private DefaultHttpContext _httpContext;
        private WebAuthnController _controller;

        [TestInitialize]
        public void Setup()
        {
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _dbContext = new AppDbContext(dbOptions);

            _configMock = new Mock<IConfiguration>();
            _configMock.Setup(x => x["CORS:deployURL"]).Returns("https://test.com");
            _configMock.Setup(x => x["WebAuthn:ServerDomain"]).Returns("test.com");
            _configMock.Setup(x => x["Jwt:Key"]).Returns("ThisIsASecretKeyForJwtTokenGeneration1234567890");
            _configMock.Setup(x => x["Jwt:Issuer"]).Returns("ePatientApi");
            _configMock.Setup(x => x["Jwt:Audience"]).Returns("ePatientApi");

            _loggerMock = new Mock<ILogger<WebAuthnController>>();

            _httpContext = new DefaultHttpContext();
            _httpContext.Session = new TestSession();

            _controller = new WebAuthnController(_dbContext, _configMock.Object, _loggerMock.Object)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = _httpContext
                }
            };
        }

        [TestCleanup]
        public void Cleanup()
        {
            _dbContext.Dispose();
        }

        [TestMethod]
        public async Task AuthenticateComplete_InvalidCredential_ReturnsBadRequest()
        {
            // Arrange
            var userId = "patient123";
            var userType = "patient";
            var credentialId = new byte[] { 1, 2, 3 };
            var options = AssertionOptions.Create(
                new Fido2Configuration 
                { 
                    ServerDomain = "test.com", 
                    ServerName = "test", 
                    Origins = new HashSet<string> { "https://test.com" } 
                },
                new byte[] { 1, 2, 3, 4 },
                new List<PublicKeyCredentialDescriptor> 
                { 
                    new PublicKeyCredentialDescriptor { Id = credentialId, Type = PublicKeyCredentialType.PublicKey } 
                },
                UserVerificationRequirement.Required,
                new AuthenticationExtensionsClientInputs());
            _httpContext.Session.SetString("webauthn-options", options.ToJson());
            _httpContext.Session.SetString("webauthn-userid", userId);
            _httpContext.Session.SetString("webauthn-usertype", userType);

            var testPatient = new RegisteredPatient
            {
                Username = userId,
                FirstName = "Jana",
                LastName = "Nováková",
                Email = "jana.novakova@test.sk",
                HashedPassword = "hashed_password",
                PhoneNumber = "0901234567",
                BirthNumber = "9255169876",
                Insurance = "25",
                Role = "Patient"
            };
            _dbContext.RegisteredPatients.Add(testPatient);
            await _dbContext.SaveChangesAsync();

            var response = new AuthenticatorAssertionRawResponse
            {
                Id = credentialId,
                RawId = credentialId,
                Type = PublicKeyCredentialType.PublicKey,
                Response = new AuthenticatorAssertionRawResponse.AssertionResponse
                {
                    AuthenticatorData = new byte[37],
                    Signature = new byte[] { 7, 8, 9 },
                    ClientDataJson = Encoding.UTF8.GetBytes("{\"type\":\"webauthn.get\",\"challenge\":\"AQIDBA==\",\"origin\":\"https://test.com\"}")
                }
            };

            // Act
            var result = await _controller.AuthenticateComplete(response);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult), "Expected BadRequestObjectResult for invalid credential");
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequestResult, "BadRequestObjectResult is null");
            Assert.AreEqual(400, badRequestResult.StatusCode, "Expected status code 400");
            Assert.IsNotNull(badRequestResult.Value, "Response value is null");
            Assert.IsTrue(badRequestResult.Value.ToString().Contains("credential", StringComparison.OrdinalIgnoreCase), 
                "Expected error message to mention credential not found");
        }

        #region Test Helper Classes

        public class TestSession : ISession
        {
            private readonly Dictionary<string, byte[]> _sessionStorage = new();
            public string Id => Guid.NewGuid().ToString();
            public bool IsAvailable => true;
            public IEnumerable<string> Keys => _sessionStorage.Keys;
            public void Clear() => _sessionStorage.Clear();
            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public void Remove(string key) => _sessionStorage.Remove(key);
            public void Set(string key, byte[] value) => _sessionStorage[key] = value;
            public bool TryGetValue(string key, out byte[] value) => _sessionStorage.TryGetValue(key, out value);
            public void SetString(string key, string value) => Set(key, Encoding.UTF8.GetBytes(value));
            public string GetString(string key) => _sessionStorage.TryGetValue(key, out var data) ? Encoding.UTF8.GetString(data) : null;
        }

        #endregion
    }
}