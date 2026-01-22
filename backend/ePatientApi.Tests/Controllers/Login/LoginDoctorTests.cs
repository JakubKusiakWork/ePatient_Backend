using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ePatientApi.Controllers;
using ePatientApi.DataAccess;
using ePatientApi.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ePatientApi.Tests
{
    [TestClass]
    public class LoginDoctorControllerTests
    {
        private AppDbContext GetInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: System.Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        private IConfiguration GetMockConfiguration()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    {"Jwt:Key", "ThisIsASecretKeyForTestingPurposesOnly123456"},
                    {"Jwt:Issuer", "TestIssuer"},
                    {"Jwt:Audience", "TestAudience"}
                })
                .Build();
            return config;
        }

        [TestMethod]
        public async Task DoctorLogin_ValidCredentials_ReturnsOkWithToken()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var config = GetMockConfiguration();
            var controller = new LoginDoctorController(config, context);

            var doctor = new RegisteredDoctor
            {
                DoctorId = 1,
                DoctorCode = "DOC001",
                DoctorFirstName = "Janko",
                DoctorLastName = "Mrkva",
                DoctorEmail = "janko.mrkva@nemocnica.sk",
                DoctorPhoneNumber = "0905123456",
                DoctorHashedPassword = BCrypt.Net.BCrypt.HashPassword("heslo123"),
                Role = "Clinician"
            };
            context.RegisteredDoctors.Add(doctor);
            await context.SaveChangesAsync();

            var loginData = new DoctorLogin
            {
                DoctorCode = "DOC001",
                DoctorPassword = "heslo123"
            };

            // Act
            var result = await controller.DoctorLogin(loginData);

            // Assert
            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult);
            Assert.AreEqual(200, okResult.StatusCode);
            
            // Use reflection to access anonymous object properties
            var response = okResult.Value;
            var responseType = response.GetType();
            
            var messageProperty = responseType.GetProperty("message");
            var tokenProperty = responseType.GetProperty("token");
            var firstNameProperty = responseType.GetProperty("firstName");
            var roleProperty = responseType.GetProperty("role");
            
            Assert.IsNotNull(messageProperty);
            Assert.AreEqual("Login successful.", messageProperty.GetValue(response));
            
            Assert.IsNotNull(tokenProperty);
            Assert.IsNotNull(tokenProperty.GetValue(response));
            
            Assert.IsNotNull(firstNameProperty);
            Assert.AreEqual("Janko", firstNameProperty.GetValue(response));
            
            Assert.IsNotNull(roleProperty);
            Assert.AreEqual("Clinician", roleProperty.GetValue(response));
        }

        [TestMethod]
        public async Task DoctorLogin_InvalidDoctorCode_ReturnsUnauthorized()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var config = GetMockConfiguration();
            var controller = new LoginDoctorController(config, context);

            var loginData = new DoctorLogin
            {
                DoctorCode = "DOC999", // Neexistujúci kód doktora
                DoctorPassword = "heslo123"
            };

            // Act
            var result = await controller.DoctorLogin(loginData);

            // Assert
            var unauthorizedResult = result as UnauthorizedObjectResult;
            Assert.IsNotNull(unauthorizedResult);
            Assert.AreEqual(401, unauthorizedResult.StatusCode);
            Assert.AreEqual("Doctor not found.", unauthorizedResult.Value);
        }

        [TestMethod]
        public async Task DoctorLogin_WrongPassword_ReturnsUnauthorized()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var config = GetMockConfiguration();
            var controller = new LoginDoctorController(config, context);

            var doctor = new RegisteredDoctor
            {
                DoctorId = 1,
                DoctorCode = "DOC002",
                DoctorFirstName = "Pavel",
                DoctorLastName = "Novák",
                DoctorEmail = "pavel.novak@poliklinika.sk",
                DoctorPhoneNumber = "0911987654",
                DoctorHashedPassword = BCrypt.Net.BCrypt.HashPassword("spravneheslo"),
                Role = "Clinician"
            };
            context.RegisteredDoctors.Add(doctor);
            await context.SaveChangesAsync();

            var loginData = new DoctorLogin
            {
                DoctorCode = "DOC002",
                DoctorPassword = "zleheslo"
            };

            // Act
            var result = await controller.DoctorLogin(loginData);

            // Assert
            var unauthorizedResult = result as UnauthorizedObjectResult;
            Assert.IsNotNull(unauthorizedResult);
            Assert.AreEqual(401, unauthorizedResult.StatusCode);
            Assert.AreEqual("Incorrect password.", unauthorizedResult.Value);
        }

        [TestMethod]
        public async Task DoctorLogin_NullLoginData_ReturnsBadRequest()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var config = GetMockConfiguration();
            var controller = new LoginDoctorController(config, context);

            // Act
            var result = await controller.DoctorLogin(null);

            // Assert
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequestResult);
            Assert.AreEqual(400, badRequestResult.StatusCode);
            Assert.AreEqual("Invalid login data.", badRequestResult.Value);
        }

        [TestMethod]
        public async Task DoctorLogin_EmptyDoctorCode_ReturnsBadRequest()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var config = GetMockConfiguration();
            var controller = new LoginDoctorController(config, context);

            var loginData = new DoctorLogin
            {
                DoctorCode = "",
                DoctorPassword = "heslo123"
            };

            // Act
            var result = await controller.DoctorLogin(loginData);

            // Assert
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequestResult);
            Assert.AreEqual(400, badRequestResult.StatusCode);
            Assert.AreEqual("Invalid login data.", badRequestResult.Value);
        }

        [TestMethod]
        public async Task DoctorLogin_EmptyPassword_ReturnsBadRequest()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var config = GetMockConfiguration();
            var controller = new LoginDoctorController(config, context);

            var loginData = new DoctorLogin
            {
                DoctorCode = "DOC001",
                DoctorPassword = ""
            };

            // Act
            var result = await controller.DoctorLogin(loginData);

            // Assert
            var badRequestResult = result as BadRequestObjectResult;
            Assert.IsNotNull(badRequestResult);
            Assert.AreEqual(400, badRequestResult.StatusCode);
            Assert.AreEqual("Invalid login data.", badRequestResult.Value);
        }

        [TestMethod]
        public async Task DoctorLogin_ValidCredentials_CreatesLoggedDoctorRecord()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var config = GetMockConfiguration();
            var controller = new LoginDoctorController(config, context);

            var doctor = new RegisteredDoctor
            {
                DoctorId = 1,
                DoctorCode = "DOC003",
                DoctorFirstName = "Mária",
                DoctorLastName = "Kvetková",
                DoctorEmail = "maria.kvetkova@ambulancia.sk",
                DoctorPhoneNumber = "0944321987",
                DoctorHashedPassword = BCrypt.Net.BCrypt.HashPassword("mojeheslo"),
                Role = "Clinician"
            };
            context.RegisteredDoctors.Add(doctor);
            await context.SaveChangesAsync();

            var loginData = new DoctorLogin
            {
                DoctorCode = "DOC003",
                DoctorPassword = "mojeheslo"
            };

            // Act
            await controller.DoctorLogin(loginData);

            // Assert
            var loggedDoctor = await context.LoggedDoctors.FirstOrDefaultAsync(ld => ld.DoctorCode == "DOC003");
            Assert.IsNotNull(loggedDoctor);
            Assert.AreEqual("Mária", loggedDoctor.DoctorFirstName);
            Assert.AreEqual("Kvetková", loggedDoctor.DoctorLastName);
            Assert.AreEqual("DOC003", loggedDoctor.DoctorCode);
        }
    }
}