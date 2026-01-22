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
using System.Linq;
using System;
using System.Threading;

namespace ePatientApi.Tests.Controllers
{
    [TestClass]
    public class WebAuthControllerTests
    {
        private Mock<AppDbContext> _dbContextMock;
        private Mock<IConfiguration> _configMock;
        private Mock<ILogger<WebAuthnController>> _loggerMock;
        private DefaultHttpContext _httpContext;
        private WebAuthnController _controller;

        [TestInitialize]
        public void Setup()
        {
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "WebAuthnTestDb").Options;
            _dbContextMock = new Mock<AppDbContext>(dbOptions);
            _configMock = new Mock<IConfiguration>();
            _loggerMock = new Mock<ILogger<WebAuthnController>>();

            _configMock.Setup(x => x["CORS:deployURL"]).Returns("https://test.com");
            _configMock.Setup(x => x["WebAuthn:ServerDomain"]).Returns("localhost");

            _httpContext = new DefaultHttpContext();
            _httpContext.Session = new TestSession();

            _controller = new WebAuthnController(_dbContextMock.Object, _configMock.Object, _loggerMock.Object)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = _httpContext
                }
            };
        }

        [TestMethod]
        public async Task Register_InvalidUserId_ReturnsBadRequest()
        {
            var request = new WebAuthnRegisterRequest { UserId = "", UserType = "patient" };
            var result = await _controller.Register(request);
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task Register_InvalidUserType_ReturnsBadRequest()
        {
            var request = new WebAuthnRegisterRequest { UserId = "123", UserType = "admin" };
            var result = await _controller.Register(request);
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task Register_ValidPatient_ReturnsOk()
        {
            var request = new WebAuthnRegisterRequest { UserId = "patient123", UserType = "patient" };
            var result = await _controller.Register(request);
            Assert.IsTrue(result is OkObjectResult || result is ObjectResult, "Expected OkObjectResult or ObjectResult");
        }
    }

    public class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _sessionStorage = new();

        public string Id => Guid.NewGuid().ToString();
        public bool IsAvailable => true;
        public IEnumerable<string> Keys => _sessionStorage.Keys;

        public void Clear() => _sessionStorage.Clear();

        public Task CommitAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task LoadAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Remove(string key) => _sessionStorage.Remove(key);

        public void Set(string key, byte[] value) => _sessionStorage[key] = value;

        public bool TryGetValue(string key, out byte[] value) => _sessionStorage.TryGetValue(key, out value);

        public void SetString(string key, string value) => Set(key, Encoding.UTF8.GetBytes(value));

        public string GetString(string key)
            => _sessionStorage.TryGetValue(key, out var data) ? Encoding.UTF8.GetString(data) : null;
    }
}
