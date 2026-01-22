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
    public class WebAuthnControllerSessionManagementTests
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
        public async Task Register_ValidRequest_StoresDataInSession()
        {
            var request = new WebAuthnRegisterRequest { UserId = "patient123", UserType = "patient" };
            var result = await _controller.Register(request);

            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
            Assert.IsNotNull(_httpContext.Session.GetString("webauthn-options"));
            Assert.AreEqual("patient123", _httpContext.Session.GetString("webauthn-userid"));
            Assert.AreEqual("patient", _httpContext.Session.GetString("webauthn-usertype"));
        }

        [TestMethod]
        public async Task Register_DoctorRequest_StoresNormalizedUserTypeInSession()
        {
            var request = new WebAuthnRegisterRequest { UserId = "123", UserType = "DOCTOR" };
            var testDoctor = new RegisteredDoctor 
            { 
                DoctorId = 123,
                DoctorCode = "456",
                DoctorFirstName = "John",
                DoctorLastName = "Doe",
                DoctorEmail = "john.doe@example.com",
                DoctorHashedPassword = "hashed_password",
                DoctorPhoneNumber = "123-456-7890",
                Role = "Doctor"
            };

            _dbContext.RegisteredDoctors.Add(testDoctor);
            await _dbContext.SaveChangesAsync();

            var result = await _controller.Register(request);

            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
            Assert.AreEqual("doctor", _httpContext.Session.GetString("webauthn-usertype"));
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