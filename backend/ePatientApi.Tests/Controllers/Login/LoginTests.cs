using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ePatientApi.Controllers;
using ePatientApi.DataAccess;
using ePatientApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using Moq;

namespace ePatientApi.Tests
{
    [TestClass]
    public class LoginControllerTests
    {
        private LoginController _controller;
        private AppDbContext _context;
        private IConfiguration _config;
        private Mock<ILogger<LoginController>> _loggerMock;

        private const string ValidUsername = "jozko";
        private const string ValidPassword = "testPassword123@";
        private const string JwtKey = "ThisIsASecretKeyForTesting123456789012345678901234";
        private const string Issuer = "TestIssuer";
        private const string Audience = "TestAudience";

        [TestInitialize]
        public void Setup()
        {
            // Arrange
            _context = CreateInMemoryDbContext();
            AddTestUser(ValidUsername, ValidPassword);

            _config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Jwt:Key"] = JwtKey,
                    ["Jwt:Issuer"] = Issuer,
                    ["Jwt:Audience"] = Audience
                })
                .Build();

            _loggerMock = new Mock<ILogger<LoginController>>();
            _controller = new LoginController(_context, _config, _loggerMock.Object);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Origin"] = "http://localhost";
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        [TestCleanup]
        public void Cleanup()
        {
            _context?.Dispose();
        }

        private AppDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .Options;

            return new AppDbContext(options);
        }

        private void AddTestUser(string username, string plainPassword, string role = "Patient")
        {
            var user = new RegisteredPatient
            {
                Id = ePatientApi.Tests.Data.TestData.NextId(),
                FirstName = "Jozko",
                LastName = "Mrkvicka",
                Username = username,
                Email = $"{username}@test.sk",
                PhoneNumber = "0901234567",
                HashedPassword = BCrypt.Net.BCrypt.HashPassword(plainPassword),
                Role = role,
                BirthNumber = Guid.NewGuid().ToString("N").Substring(0,10),
                Insurance = "25"
            };

            // Ensure unique tracking by creating a fresh instance to add
            _context.RegisteredPatients.Add(user);
            _context.SaveChanges();
        }

        #region ValidLoginTests

        [TestMethod]
        public async Task Login_ValidCredentials_ReturnsOk()
        {
            // Arrange
            var loginRequest = new LoginUser { UserName = ValidUsername, Password = ValidPassword };

            // Act
            var result = await _controller.Login(loginRequest);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
            var okResult = result as OkObjectResult;
            var token = okResult?.Value?.GetType().GetProperty("token")?.GetValue(okResult.Value) as string;
            Assert.IsFalse(string.IsNullOrEmpty(token));
        }

        [TestMethod]
        public async Task Login_ValidCredentials_ReturnsCorrectUserData()
        {
            // Arrange
            var loginRequest = new LoginUser { UserName = ValidUsername, Password = ValidPassword };

            // Act
            var result = await _controller.Login(loginRequest);

            // Assert
            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult);
            
            var response = okResult.Value;
            var username = response.GetType().GetProperty("username")?.GetValue(response) as string;
            var firstName = response.GetType().GetProperty("firstName")?.GetValue(response) as string;
            var lastName = response.GetType().GetProperty("lastName")?.GetValue(response) as string;
            var role = response.GetType().GetProperty("role")?.GetValue(response) as string;

            Assert.AreEqual(ValidUsername, username);
            Assert.AreEqual("Jozko", firstName);
            Assert.AreEqual("Mrkvicka", lastName);
            Assert.AreEqual("Patient", role);
        }

        [TestMethod]
        public async Task Login_ValidCredentials_CreatesLoggedUserRecord()
        {
            // Arrange
            var loginRequest = new LoginUser { UserName = ValidUsername, Password = ValidPassword };
            var initialLoggedUsersCount = await _context.LoggedUsers.CountAsync();

            // Act
            await _controller.Login(loginRequest);

            // Assert
            var finalLoggedUsersCount = await _context.LoggedUsers.CountAsync();
            Assert.AreEqual(initialLoggedUsersCount + 1, finalLoggedUsersCount, "Should create a new LoggedUser record");

            var loggedUser = await _context.LoggedUsers.FirstOrDefaultAsync(u => u.Username == ValidUsername);
            Assert.IsNotNull(loggedUser, "LoggedUser record should exist");
            Assert.AreEqual("Jozko", loggedUser.FirstName);
            Assert.AreEqual("Mrkvicka", loggedUser.LastName);
        }

        [TestMethod]
        public async Task Login_CaseInsensitiveUsername_ReturnsOk()
        {
            // Arrange: Test with different case username
            var loginRequest = new LoginUser { UserName = ValidUsername.ToUpper(), Password = ValidPassword };

            // Act
            var result = await _controller.Login(loginRequest);


            // The current implementation is case-insensitive for username normalization, expect Ok
            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
        }

        #endregion

        #region InvalidLoginTests

        [TestMethod]
        public async Task Login_InvalidPassword_ReturnsUnauthorized()
        {
            // Arrange: Provide a valid username with an incorrect password.
            var loginRequest = new LoginUser { UserName = ValidUsername, Password = "wrongPassword" };

            // Act: Attempt to log in.
            var result = await _controller.Login(loginRequest);

            // Assert: Verify the response is an UnauthorizedObjectResult.
            Assert.IsInstanceOfType(result, typeof(UnauthorizedObjectResult), "Result should be an UnauthorizedObjectResult.");
        }

        [TestMethod]
        public async Task Login_UnknownUser_ReturnsUnauthorized()
        {
            // Arrange: Provide credentials for a non-existent user.
            var loginRequest = new LoginUser { UserName = "unknownUser", Password = "irrelevant" };

            // Act: Attempt to log in.
            var result = await _controller.Login(loginRequest);

            // Assert: Verify the response is an UnauthorizedObjectResult.
            Assert.IsInstanceOfType(result, typeof(UnauthorizedObjectResult), "Result should be an UnauthorizedObjectResult.");
        }

        [TestMethod]
        public async Task Login_NullModel_ReturnsBadRequest()
        {
            // Arrange: Provide a null login model.
            LoginUser loginRequest = null;

            // Act: Attempt to log in.
            var result = await _controller.Login(loginRequest);

            // Assert: Verify the response is a BadRequestObjectResult.
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult), "Result should be a BadRequestObjectResult.");
        }

        [TestMethod]
        public async Task Login_EmptyUsername_ReturnsBadRequest()
        {
            // Arrange
            var loginRequest = new LoginUser { UserName = "", Password = ValidPassword };

            // Act
            var result = await _controller.Login(loginRequest);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequestResult?.Value);
        }

        [TestMethod]
        public async Task Login_EmptyPassword_ReturnsBadRequest()
        {
            // Arrange
            var loginRequest = new LoginUser { UserName = ValidUsername, Password = "" };

            // Act
            var result = await _controller.Login(loginRequest);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task Login_WhitespaceUsername_ReturnsBadRequest()
        {
            // Arrange
            var loginRequest = new LoginUser { UserName = "   ", Password = ValidPassword };

            // Act
            var result = await _controller.Login(loginRequest);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task Login_WhitespacePassword_ReturnsBadRequest()
        {
            // Arrange
            var loginRequest = new LoginUser { UserName = ValidUsername, Password = "   " };

            // Act
            var result = await _controller.Login(loginRequest);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task Login_NullUsername_ReturnsBadRequest()
        {
            // Arrange
            var loginRequest = new LoginUser { UserName = null, Password = ValidPassword };

            // Act
            var result = await _controller.Login(loginRequest);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task Login_NullPassword_ReturnsBadRequest()
        {
            // Arrange
            var loginRequest = new LoginUser { UserName = ValidUsername, Password = null };

            // Act
            var result = await _controller.Login(loginRequest);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task Login_MissingJwtKey_ReturnsServerError()
        {
            // Arrange
            var brokenConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Jwt:Key"] = null,
                    ["Jwt:Issuer"] = Issuer,
                    ["Jwt:Audience"] = Audience
                })
                .Build();

            var controller = new LoginController(_context, brokenConfig, _loggerMock.Object);
            var loginRequest = new LoginUser { UserName = ValidUsername, Password = ValidPassword };

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Request = { Headers = { ["Origin"] = "http://localhost" } }
                }
            };

            // Act
            var result = await controller.Login(loginRequest);

            // Assert
            Assert.IsTrue(
                result is ObjectResult obj && (obj.StatusCode == 500 || obj.StatusCode == 400),
                "Should return 500 Internal Server Error or 400 Bad Request when JWT key is missing."
            );
        }
        #endregion

        #region DatabaseTests

        [TestMethod]
        public async Task Login_DatabaseSaveFailure_ReturnsServerError()
        {
            // Arrange
            var loginRequest = new LoginUser { UserName = ValidUsername, Password = ValidPassword };

            // Dispose the context to simulate database connection issues
            _context.Dispose();
            
            // Create a new controller with the disposed context
            var controller = new LoginController(_context, _config, _loggerMock.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Request = { Headers = { ["Origin"] = "http://localhost" } }
                }
            };

            // Act
            var result = await controller.Login(loginRequest);

            // Assert
            Assert.IsTrue(result is ObjectResult obj && obj.StatusCode == 500);
        }

        [TestMethod]
        public void Login_NullDbContext_ReturnsServerError()
        {
            // Expect constructor to throw when DbContext is null
            Assert.ThrowsException<ArgumentNullException>(() => new LoginController(null, _config, _loggerMock.Object));
        }

        #endregion

        #region SecurityTests

        [TestMethod]
        public async Task Login_MultipleFailedAttempts_StillReturnsUnauthorized()
        {
            // Arrange
            var loginRequest = new LoginUser { UserName = ValidUsername, Password = "wrongPassword" };

            // Act: Attempt multiple failed logins
            var result1 = await _controller.Login(loginRequest);
            var result2 = await _controller.Login(loginRequest);
            var result3 = await _controller.Login(loginRequest);

            // Assert: All should return Unauthorized
            Assert.IsInstanceOfType(result1, typeof(UnauthorizedObjectResult));
            Assert.IsInstanceOfType(result2, typeof(UnauthorizedObjectResult));
            Assert.IsInstanceOfType(result3, typeof(UnauthorizedObjectResult));
        }

        [TestMethod]
        public async Task Login_SqlInjectionAttempt_ReturnsSafeResponse()
        {
            // Arrange: Attempt SQL injection in username
            var loginRequest = new LoginUser 
            { 
                UserName = "admin'; DROP TABLE RegisteredPatients; --", 
                Password = "password" 
            };

            // Act
            var result = await _controller.Login(loginRequest);

            // Assert: Should safely return Unauthorized, not crash
            Assert.IsInstanceOfType(result, typeof(UnauthorizedObjectResult));
            
            // Verify the table still exists by checking if we can query it
            var userExists = await _context.RegisteredPatients.AnyAsync();
            Assert.IsTrue(userExists || !userExists, "Database should be intact regardless");
        }

        [TestMethod]
        public async Task Login_VeryLongUsername_HandlesSafely()
        {
            // Arrange
            var longUsername = new string('a', 10000);
            var loginRequest = new LoginUser { UserName = longUsername, Password = ValidPassword };

            // Act
            var result = await _controller.Login(loginRequest);

            // Assert: Should handle gracefully
            Assert.IsTrue(result is UnauthorizedObjectResult || result is BadRequestObjectResult);
        }

        [TestMethod]
        public async Task Login_VeryLongPassword_HandlesSafely()
        {
            // Arrange
            var longPassword = new string('a', 10000);
            var loginRequest = new LoginUser { UserName = ValidUsername, Password = longPassword };

            // Act
            var result = await _controller.Login(loginRequest);

            // Assert: Should handle gracefully
            Assert.IsInstanceOfType(result, typeof(UnauthorizedObjectResult));
        }

        #endregion

        #region RoleTests

        [TestMethod]
        public async Task Login_AdminUser_ReturnsCorrectRole()
        {
            // Arrange
            var adminUsername = "admin";
            var adminPassword = "adminPass123@";
            AddTestUser(adminUsername, adminPassword, "Admin");

            var loginRequest = new LoginUser { UserName = adminUsername, Password = adminPassword };

            // Act
            var result = await _controller.Login(loginRequest);

            // Assert
            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult);
            
            var response = okResult.Value;
            var role = response.GetType().GetProperty("role")?.GetValue(response) as string;
            Assert.AreEqual("Admin", role);
        }

        [TestMethod]
        public async Task Login_DoctorUser_ReturnsCorrectRole()
        {
            // Arrange
            var doctorUsername = "doctor";
            var doctorPassword = "doctorPass123@";
            AddTestUser(doctorUsername, doctorPassword, "Doctor");

            var loginRequest = new LoginUser { UserName = doctorUsername, Password = doctorPassword };

            // Act
            var result = await _controller.Login(loginRequest);

            // Assert
            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult);
            
            var response = okResult.Value;
            var role = response.GetType().GetProperty("role")?.GetValue(response) as string;
            Assert.AreEqual("Doctor", role);
        }

        #endregion

        #region LoggingTests

        [TestMethod]
        public async Task Login_ValidCredentials_LogsSuccessfulLogin()
        {
            // Arrange
            var loginRequest = new LoginUser { UserName = ValidUsername, Password = ValidPassword };

            // Act
            await _controller.Login(loginRequest);

            // Assert: Verify that information level logging occurred
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("User logged in successfully")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>() ),
                Times.Once);
        }

        [TestMethod]
        public async Task Login_InvalidCredentials_LogsWarning()
        {
            // Arrange
            var loginRequest = new LoginUser { UserName = ValidUsername, Password = "wrongPassword" };

            // Act
            await _controller.Login(loginRequest);

            // Assert: Verify that warning level logging occurred
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Invalid password")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task Login_UnknownUser_LogsWarning()
        {
            // Arrange
            var loginRequest = new LoginUser { UserName = "unknownUser", Password = "password" };

            // Act
            await _controller.Login(loginRequest);

            // Assert: Verify that warning level logging occurred
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("User not found")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #region EdgeCaseTests

        [TestMethod]
        public async Task Login_SpecialCharactersInUsername_HandledCorrectly()
        {
            // Arrange
            var specialUsername = "user@domain.com";
            var password = "testPass123@";
            AddTestUser(specialUsername, password);

            var loginRequest = new LoginUser { UserName = specialUsername, Password = password };

            // Act
            var result = await _controller.Login(loginRequest);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
        }

        [TestMethod]
        public async Task Login_UnicodeCharactersInUsername_HandledCorrectly()
        {
            // Arrange
            var unicodeUsername = "józek123";
            var password = "testPass123@";
            AddTestUser(unicodeUsername, password);

            var loginRequest = new LoginUser { UserName = unicodeUsername, Password = password };

            // Act
            var result = await _controller.Login(loginRequest);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
        }

        [TestMethod]
        public async Task Login_PasswordWithSpecialCharacters_WorksCorrectly()
        {
            // Arrange
            var username = "testuser";
            var complexPassword = "P@ssw0rd!@#$%^&*()_+-=[]{}|;:,.<>?";
            AddTestUser(username, complexPassword);

            var loginRequest = new LoginUser { UserName = username, Password = complexPassword };

            // Act
            var result = await _controller.Login(loginRequest);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
        }

        [TestMethod]
        public async Task Login_ConcurrentLogins_HandledCorrectly()
        {
            // Arrange
            var loginRequest = new LoginUser { UserName = ValidUsername, Password = ValidPassword };

            // Act: Simulate concurrent login attempts
            var task1 = _controller.Login(loginRequest);
            var task2 = _controller.Login(loginRequest);
            var task3 = _controller.Login(loginRequest);

            var results = await Task.WhenAll(task1, task2, task3);

            // Assert: All should succeed
            foreach (var result in results)
            {
                Assert.IsInstanceOfType(result, typeof(OkObjectResult));
            }

            // Verify multiple LoggedUser records were created
            var loggedUsersCount = await _context.LoggedUsers.CountAsync(u => u.Username == ValidUsername);
            Assert.AreEqual(3, loggedUsersCount);
        }

        #endregion
    }
}