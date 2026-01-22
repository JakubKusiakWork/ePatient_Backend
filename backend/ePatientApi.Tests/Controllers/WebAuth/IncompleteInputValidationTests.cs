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
using ePatientApi.Models;

namespace ePatientApi.Tests.Controllers.WebAuthn
{
    [TestClass]
    public class WebAuthnControllerInputValidationTests
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

        [TestMethod]
        public async Task Register_NullRequest_ReturnsBadRequest()
        {
            var result = await _controller.Register(null);
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequestResult?.Value);
        }

        [TestMethod]
        public async Task Register_EmptyUserId_ReturnsBadRequest()
        {
            var request = new WebAuthnRegisterRequest { UserId = "", UserType = "patient" };
            var result = await _controller.Register(request);
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequestResult?.Value);
        }

        [TestMethod]
        public async Task Register_NullUserId_ReturnsBadRequest()
        {
            var request = new WebAuthnRegisterRequest { UserId = null, UserType = "patient" };
            var result = await _controller.Register(request);
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task Register_EmptyUserType_ReturnsBadRequest()
        {
            var request = new WebAuthnRegisterRequest { UserId = "123", UserType = "" };
            var result = await _controller.Register(request);
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task Register_NullUserType_ReturnsBadRequest()
        {
            var request = new WebAuthnRegisterRequest { UserId = "123", UserType = null };
            var result = await _controller.Register(request);
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task Register_InvalidUserType_ReturnsBadRequest()
        {
            var request = new WebAuthnRegisterRequest { UserId = "123", UserType = "admin" };
            var result = await _controller.Register(request);
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
        public async Task Register_UserTypeCaseInsensitive_AcceptsValidTypes(string userType)
        {
            var request = new WebAuthnRegisterRequest { UserId = "123", UserType = userType };

            if (userType.ToLower() == "doctor")
            {
                var testDoctor = new RegisteredDoctor 
                { 
                    DoctorId = 123,
                    DoctorCode = "456",
                    DoctorFirstName = "Janko",
                    DoctorLastName = "Hrasko",
                    DoctorEmail = "janko.hrasko@gmail.com",
                    DoctorHashedPassword = "hashed_password",
                    DoctorPhoneNumber = "123-456-7890",
                    Role = "Doctor"
                };
                
                _dbContext.RegisteredDoctors.Add(testDoctor);
                await _dbContext.SaveChangesAsync();
            }

            var result = await _controller.Register(request);

            if (userType.ToLower() == "doctor")
            {
                Assert.IsInstanceOfType(result, typeof(OkObjectResult));
            }
            else
            {
                Assert.IsInstanceOfType(result, typeof(OkObjectResult));
            }
        }

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
    }
}