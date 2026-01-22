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
using Microsoft.EntityFrameworkCore;
using System.Text;
using System;
using System.Threading;
using Fido2NetLib;
using Fido2NetLib.Objects;

namespace ePatientApi.Tests.Controllers.WebAuthn
{
    [TestClass]
    public class WebAuthnControllerAuthenticateCompleteInputValidationTests
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
            _configMock.Setup(x => x["WebAuthn:ServerDomain"]).Returns("localhost");
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
        public async Task AuthenticateComplete_NullResponse_ReturnsBadRequest()
        {
            var result = await _controller.AuthenticateComplete(null);
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequestResult?.Value);
        }

        [TestMethod]
        public async Task AuthenticateComplete_MissingSessionOptions_ReturnsBadRequest()
        {
            _httpContext.Session.SetString("webauthn-userid", "123");
            _httpContext.Session.SetString("webauthn-usertype", "patient");

            var response = new AuthenticatorAssertionRawResponse();
            var result = await _controller.AuthenticateComplete(response);

            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequestResult?.Value);
        }

        [TestMethod]
        public async Task AuthenticateComplete_MissingSessionUserId_ReturnsBadRequest()
        {
            var options = AssertionOptions.Create(
                new Fido2Configuration { ServerDomain = "localhost", ServerName = "test", Origins = new HashSet<string> { "https://test.com" } },
                new byte[] { 1, 2, 3, 4 }, // Challenge
                new List<PublicKeyCredentialDescriptor>(),
                UserVerificationRequirement.Required,
                new AuthenticationExtensionsClientInputs());
            _httpContext.Session.SetString("webauthn-options", options.ToJson());
            _httpContext.Session.SetString("webauthn-usertype", "patient");

            var response = new AuthenticatorAssertionRawResponse();
            var result = await _controller.AuthenticateComplete(response);

            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequestResult?.Value);
        }

        [TestMethod]
        public async Task AuthenticateComplete_MissingSessionUserType_ReturnsBadRequest()
        {
            var options = AssertionOptions.Create(
                new Fido2Configuration { ServerDomain = "localhost", ServerName = "test", Origins = new HashSet<string> { "https://test.com" } },
                new byte[] { 1, 2, 3, 4 },
                new List<PublicKeyCredentialDescriptor>(),
                UserVerificationRequirement.Required,
                new AuthenticationExtensionsClientInputs());
            _httpContext.Session.SetString("webauthn-options", options.ToJson());
            _httpContext.Session.SetString("webauthn-userid", "123");

            var response = new AuthenticatorAssertionRawResponse();
            var result = await _controller.AuthenticateComplete(response);

            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequestResult?.Value);
        }

        [TestMethod]
        public async Task AuthenticateComplete_CredentialNotFound_ReturnsBadRequest()
        {
            var userId = "123";
            var userType = "patient";
            var options = AssertionOptions.Create(
                new Fido2Configuration { ServerDomain = "localhost", ServerName = "test", Origins = new HashSet<string> { "https://test.com" } },
                new byte[] { 1, 2, 3, 4 },
                new List<PublicKeyCredentialDescriptor>(),
                UserVerificationRequirement.Required,
                new AuthenticationExtensionsClientInputs());
            _httpContext.Session.SetString("webauthn-options", options.ToJson());
            _httpContext.Session.SetString("webauthn-userid", userId);
            _httpContext.Session.SetString("webauthn-usertype", userType);

            var response = new AuthenticatorAssertionRawResponse
            {
                Id = new byte[] { 1, 2, 3 },
                RawId = new byte[] { 1, 2, 3 },
                Type = PublicKeyCredentialType.PublicKey,
                Response = new AuthenticatorAssertionRawResponse.AssertionResponse
                {
                    AuthenticatorData = new byte[] { 4, 5, 6 },
                    Signature = new byte[] { 7, 8, 9 },
                    ClientDataJson = Encoding.UTF8.GetBytes("{\"type\":\"webauthn.get\",\"challenge\":\"AQIDBA==\",\"origin\":\"https://test.com\"}")
                }
            };

            var result = await _controller.AuthenticateComplete(response);

            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequestResult?.Value);
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