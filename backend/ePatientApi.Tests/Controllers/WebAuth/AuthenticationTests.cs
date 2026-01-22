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

namespace ePatientApi.Tests.Controllers.WebAuthn
{
    [TestClass]
    public class WebAuthnControllerAuthenticateTests
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

        #region Input Validation Tests

        [TestMethod]
        public async Task Authenticate_NullRequest_ReturnsBadRequest()
        {
            var result = await _controller.Authenticate(null);
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequestResult?.Value);
        }

        [TestMethod]
        public async Task Authenticate_EmptyUserId_ReturnsBadRequest()
        {
            var request = new WebAuthnAuthenticateRequest { UserId = "", UserType = "patient" };
            var result = await _controller.Authenticate(request);
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequestResult?.Value);
        }

        [TestMethod]
        public async Task Authenticate_NullUserId_ReturnsBadRequest()
        {
            var request = new WebAuthnAuthenticateRequest { UserId = null, UserType = "patient" };
            var result = await _controller.Authenticate(request);
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task Authenticate_EmptyUserType_ReturnsBadRequest()
        {
            var request = new WebAuthnAuthenticateRequest { UserId = "123", UserType = "" };
            var result = await _controller.Authenticate(request);
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task Authenticate_NullUserType_ReturnsBadRequest()
        {
            var request = new WebAuthnAuthenticateRequest { UserId = "123", UserType = null };
            var result = await _controller.Authenticate(request);
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task Authenticate_InvalidUserType_ReturnsBadRequest()
        {
            var request = new WebAuthnAuthenticateRequest { UserId = "123", UserType = "admin" };
            var result = await _controller.Authenticate(request);
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequestResult?.Value);
        }

        [TestMethod]
        [DataRow("DOCTOR")]
        [DataRow("Doctor")]
        [DataRow("doctor")]
        [DataRow("PATIENT")]
        [DataRow("Patient")]
        [DataRow("patient")]
        public async Task Authenticate_UserTypeCaseInsensitive_AcceptsValidTypes(string userType)
        {
            var request = new WebAuthnAuthenticateRequest { UserId = "123", UserType = userType };

            if (userType.ToLower() == "doctor")
            {
                var testCredential = new WebAuthnCredentials
                {
                    UserId = "123",
                    UserType = "doctor",
                    CredentialId = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
                    PublicKey = Convert.ToBase64String(new byte[] { 4, 5, 6 }),
                    UserHandle = Convert.ToBase64String(new byte[] { 7, 8, 9 }),
                    SignatureCounter = 0,
                    CreatedAt = DateTime.UtcNow,
                    AuthenticatorType = "platform"
                };
                _dbContext.WebAuthnCredentials.Add(testCredential);
                await _dbContext.SaveChangesAsync();
            }
            else
            {
                var testCredential = new WebAuthnCredentials
                {
                    UserId = "123",
                    UserType = "patient",
                    CredentialId = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
                    PublicKey = Convert.ToBase64String(new byte[] { 4, 5, 6 }),
                    UserHandle = Convert.ToBase64String(new byte[] { 7, 8, 9 }),
                    SignatureCounter = 0,
                    CreatedAt = DateTime.UtcNow,
                    AuthenticatorType = "platform"
                };
                _dbContext.WebAuthnCredentials.Add(testCredential);
                await _dbContext.SaveChangesAsync();
            }

            var result = await _controller.Authenticate(request);

            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
        }

        #endregion

        #region Credential Retrieval Tests

        [TestMethod]
        public async Task Authenticate_NoCredentialsFound_ReturnsBadRequest()
        {
            var request = new WebAuthnAuthenticateRequest { UserId = "999", UserType = "patient" };
            var result = await _controller.Authenticate(request);
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequestResult?.Value);
        }

        [TestMethod]
        public async Task Authenticate_ValidCredentials_ReturnsOkWithAssertionOptions()
        {
            var request = new WebAuthnAuthenticateRequest { UserId = "123", UserType = "patient" };
            var testCredential = new WebAuthnCredentials
            {
                UserId = "123",
                UserType = "patient",
                CredentialId = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
                PublicKey = Convert.ToBase64String(new byte[] { 4, 5, 6 }),
                UserHandle = Convert.ToBase64String(new byte[] { 7, 8, 9 }),
                SignatureCounter = 0,
                CreatedAt = DateTime.UtcNow,
                AuthenticatorType = "platform"
            };
            _dbContext.WebAuthnCredentials.Add(testCredential);
            await _dbContext.SaveChangesAsync();

            var result = await _controller.Authenticate(request);

            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult?.Value);
        }

        #endregion

        #region Session Management Tests

        [TestMethod]
        public async Task Authenticate_ValidRequest_StoresDataInSession()
        {
            var request = new WebAuthnAuthenticateRequest { UserId = "123", UserType = "patient" };
            var testCredential = new WebAuthnCredentials
            {
                UserId = "123",
                UserType = "patient",
                CredentialId = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
                PublicKey = Convert.ToBase64String(new byte[] { 4, 5, 6 }),
                UserHandle = Convert.ToBase64String(new byte[] { 7, 8, 9 }),
                SignatureCounter = 0,
                CreatedAt = DateTime.UtcNow,
                AuthenticatorType = "platform"
            };
            _dbContext.WebAuthnCredentials.Add(testCredential);
            await _dbContext.SaveChangesAsync();

            var result = await _controller.Authenticate(request);

            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
            Assert.IsNotNull(_httpContext.Session.GetString("webauthn-options"));
            Assert.AreEqual("123", _httpContext.Session.GetString("webauthn-userid"));
            Assert.AreEqual("patient", _httpContext.Session.GetString("webauthn-usertype"));
        }

        [TestMethod]
        public async Task Authenticate_DoctorRequest_StoresNormalizedUserTypeInSession()
        {
            var request = new WebAuthnAuthenticateRequest { UserId = "123", UserType = "DOCTOR" };
            var testCredential = new WebAuthnCredentials
            {
                UserId = "123",
                UserType = "doctor",
                CredentialId = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
                PublicKey = Convert.ToBase64String(new byte[] { 4, 5, 6 }),
                UserHandle = Convert.ToBase64String(new byte[] { 7, 8, 9 }),
                SignatureCounter = 0,
                CreatedAt = DateTime.UtcNow,
                AuthenticatorType = "platform"
            };
            _dbContext.WebAuthnCredentials.Add(testCredential);
            await _dbContext.SaveChangesAsync();

            var result = await _controller.Authenticate(request);

            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
            Assert.AreEqual("doctor", _httpContext.Session.GetString("webauthn-usertype"));
        }

        #endregion

        #region Exception Handling Tests

        [TestMethod]
        public async Task Authenticate_DatabaseException_ReturnsInternalServerError()
        {
            var request = new WebAuthnAuthenticateRequest { UserId = "123", UserType = "patient" };
            _dbContext.Dispose();
            var result = await _controller.Authenticate(request);
            Assert.IsInstanceOfType(result, typeof(ObjectResult));
            var objectResult = result as ObjectResult;
            Assert.AreEqual(500, objectResult?.StatusCode);
        }

        #endregion

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