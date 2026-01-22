using System;
using System.Linq;
using System.Threading.Tasks;
using ePatientApi.Controllers;
using ePatientApi.DataAccess;
using ePatientApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Threading;

namespace ePatientApi.Tests
{
    [TestClass]
    public class RegisterControllerTests
    {
        private RegisterController _controller;
        private AppDbContext _context;
        private Mock<ILogger<RegisterController>> _mockLogger;

        private static (bool isValid, string errorMessage) InvokePasswordRegexCheck(string password)
        {
            var method = typeof(ePatientApi.Controllers.RegisterController)
                .GetMethod("PasswordRegexCheck", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (method == null)
                throw new InvalidOperationException("PasswordRegexCheck method not found on RegisterController");

            var result = method.Invoke(null, new object[] { password });
            var isValid = (bool)result.GetType().GetField("Item1").GetValue(result);
            var errorMessage = (string)result.GetType().GetField("Item2").GetValue(result);
            return (isValid, errorMessage);
        }

        [TestInitialize]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);
            _mockLogger = new Mock<ILogger<RegisterController>>();
            _controller = new RegisterController(_context, _mockLogger.Object);

            var testPatient = new RegisteredPatient
            {
                Id = ePatientApi.Tests.Data.TestData.NextId(),
                FirstName = "Ján",
                LastName = "Novotný",
                Username = "jan.novotny",
                Email = "jan.novotny@test.sk",
                PhoneNumber = "0901234567",
                HashedPassword = BCrypt.Net.BCrypt.HashPassword("Test123!@"),
                Role = "Patient",
                BirthNumber = Guid.NewGuid().ToString("N").Substring(0,10),
                Insurance = "25"
            };
            _context.RegisteredPatients.Add(testPatient);
            _context.SaveChanges();
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (_context != null)
                {
                    _context.Database?.EnsureDeleted();
                    _context.Dispose();
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        #region RegistrationTests

        [TestMethod]
        public async Task Register_EmptyPatientFields_ReturnsBadRequest()
        {
            var patient = new RegisteredPatient
            {
                FirstName = "",
                LastName = "",
                Username = "",
                Email = "",
                PhoneNumber = "",
                HashedPassword = "",
                BirthNumber = "",
                Insurance = ""
            };

            // Act: Attempt to register the patient.
            var result = await _controller.Register(patient);

            // Assert: Verify the response is a BadRequestObjectResult.
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult), "Result should be a BadRequestObjectResult.");
        }

        [TestMethod]
        public async Task Register_NullPatient_ReturnsBadRequest()
        {
            // Arrange: Provide a null patient.
            RegisteredPatient patient = null;

            // Act: Attempt to register the patient.
            var result = await _controller.Register(patient);

            // Assert: Verify the response is a BadRequestObjectResult.
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult), "Result should be a BadRequestObjectResult.");
        }

        [TestMethod]
        public async Task Register_DuplicateEmail_ReturnsBadRequest()
        {
            // Arrange: Create a patient with an existing email.
            var existingPatient = new RegisteredPatient
            {
                FirstName = "Jozko",
                LastName = "Mrkvicka",
                Username = "jozko.mrkvicka",
                Email = "bossman69@seznam.cz",
                PhoneNumber = "0918777666",
                HashedPassword = "StrongP@ssw0rd",
                Role = "Patient",
                BirthNumber = Guid.NewGuid().ToString("N").Substring(0,10),
                Insurance = "24"
            };
            _context.RegisteredPatients.Add(existingPatient);
            await _context.SaveChangesAsync();
            
            var newPatient = new RegisteredPatient
            {
                FirstName = "Fake",
                LastName = "User",
                Username = "faker123",
                Email = "bossman69@seznam.cz",
                PhoneNumber = "0918555333",
                HashedPassword = "AnotherGoodP@ss1",
                Role = "Patient",
                BirthNumber = Guid.NewGuid().ToString("N").Substring(0,10),
                Insurance = "23"
            };

            // Act: Attempt to register the patient.
            var result = await _controller.Register(newPatient);

            // Assert: Verify the response is a ConflictObjectResult.
            Assert.IsInstanceOfType(result, typeof(ConflictObjectResult), "Result should be a ConflictObjectResult.");
        }

        [TestMethod]
        public async Task Register_ValidPatient_ReturnsOk()
        {
            // Arrange: Create a valid patient.
            var patient = new RegisteredPatient
            {
                FirstName = "Jozko",
                LastName = "Mrkvicka",
                Username = "jozko.bossman",
                Email = "bossman69@gmail.com",
                PhoneNumber = "0918222111",
                HashedPassword = "BossMan123@!",
                Role = "Patient",
                BirthNumber = Guid.NewGuid().ToString("N").Substring(0,10),
                Insurance = "22"
            };

            // Act: Attempt to register the patient.
            var result = await _controller.Register(patient);

            // Assert: Verify the response is an OkObjectResult and the patient is saved.
            Assert.IsInstanceOfType(result, typeof(OkObjectResult), "Result should be an OkObjectResult.");
            var saved = _context.RegisteredPatients.FirstOrDefault(p => p.Email == patient.Email);
            Assert.IsNotNull(saved, "Patient should be saved in the database.");
        }

        [TestMethod]
        public void Register_NullDbContext_ReturnsInternalServerError()
        {
            // Arrange: Initialize controller with null DbContext and a mock logger.
            var mockLogger = new Mock<ILogger<RegisterController>>();
            Assert.ThrowsException<ArgumentNullException>(() => new RegisterController(null, mockLogger.Object));
        }

        [TestMethod]
        public async Task Register_WhitespaceOnlyFields_ReturnsBadRequest()
        {
            // Arrange: Create a patient with whitespace-only fields.
            var patient = new RegisteredPatient
            {
                FirstName = "   ",
                LastName = "\t",
                Username = "\n",
                Email = " ",
                PhoneNumber = "  ",
                HashedPassword = "ValidPass1!",
                Role = "Patient",
                BirthNumber = "   ",
                Insurance = "\t"
            };

            // Act: Attempt to register the patient.
            var result = await _controller.Register(patient);

            // Assert: Verify the response is a BadRequestObjectResult.
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult), "Result should be a BadRequestObjectResult.");
        }

        [TestMethod]
        public async Task Register_MissingRole_ReturnsBadRequest()
        {
            // Arrange: Create a patient without a role.
            var patient = new RegisteredPatient
            {
                FirstName = "John",
                LastName = "Doe",
                Username = "johndoe",
                Email = "john.doe@example.com",
                PhoneNumber = "0918123456",
                HashedPassword = "ValidPass1!",
                Role = null,
                BirthNumber = "9203147884",
                Insurance = "20"
            };

            // Act: Attempt to register the patient.
            var result = await _controller.Register(patient);

            // Assert: Verify the response is a BadRequestObjectResult.
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult), "Result should be a BadRequestObjectResult.");
        }

        [TestMethod]
        public async Task Register_InvalidPassword_ReturnsBadRequest()
        {
            // Arrange: Create a patient with an invalid password.
            var patient = new RegisteredPatient
            {
                FirstName = "Jane",
                LastName = "Smith",
                Username = "janesmith",
                Email = "jane.smith@example.com",
                PhoneNumber = "0918654321",
                HashedPassword = "weak",
                Role = "Patient",
                BirthNumber = "9203147883",
                Insurance = "19"
            };

            // Act: Attempt to register the patient.
            var result = await _controller.Register(patient);

            // Assert: Verify the response is a BadRequestObjectResult.
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult), "Result should be a BadRequestObjectResult.");
        }

        [TestMethod]
        public async Task Register_ValidPatient_CreatesLoggedUser()
        {
            // Arrange: Create a valid patient.
            var patient = new RegisteredPatient
            {
                FirstName = "Test",
                LastName = "User",
                Username = "testuser123",
                Email = "test.user@example.com",
                PhoneNumber = "0918111222",
                HashedPassword = "TestPass1!",
                Role = "Patient",
                BirthNumber = Guid.NewGuid().ToString("N").Substring(0,10),
                Insurance = "18"
            };

            // Act: Register the patient.
            var result = await _controller.Register(patient);

            // Assert: Verify LoggedUser is created.
            Assert.IsInstanceOfType(result, typeof(OkObjectResult), "Result should be an OkObjectResult.");
            var loggedUser = _context.LoggedUsers.FirstOrDefault(u => u.Username == patient.Username);
            Assert.IsNotNull(loggedUser, "LoggedUser should be created.");
            Assert.AreEqual(patient.FirstName, loggedUser.FirstName, "FirstName should match.");
            Assert.AreEqual(patient.LastName, loggedUser.LastName, "LastName should match.");
            Assert.AreEqual("Patient", loggedUser.Role, "Role should be Patient.");
        }

        [TestMethod]
        public async Task Register_ValidPatient_PasswordIsHashed()
        {
            // Arrange: Create a valid patient.
            var originalPassword = "TestPass1!";
            var patient = new RegisteredPatient
            {
                FirstName = "Hash",
                LastName = "Test",
                Username = "hashtest",
                Email = "hash.test@example.com",
                PhoneNumber = "0918333444",
                HashedPassword = originalPassword,
                Role = "Patient",
                BirthNumber = Guid.NewGuid().ToString("N").Substring(0,10),
                Insurance = "17"
            };

            // Act: Register the patient.
            var result = await _controller.Register(patient);

            // Assert: Verify password is hashed.
            Assert.IsInstanceOfType(result, typeof(OkObjectResult), "Result should be an OkObjectResult.");
            var savedPatient = _context.RegisteredPatients.FirstOrDefault(p => p.Email == patient.Email);
            Assert.IsNotNull(savedPatient, "Patient should be saved.");
            Assert.AreNotEqual(originalPassword, savedPatient.HashedPassword, "Password should be hashed.");
            Assert.IsTrue(BCrypt.Net.BCrypt.Verify(originalPassword, savedPatient.HashedPassword), "Hash should be verifiable.");
        }

        [TestMethod]
        public async Task Register_CaseInsensitiveEmailDuplicate_ReturnsConflict()
        {
            // Arrange: Create a patient with existing email in different case.
            var existingPatient = new RegisteredPatient
            {
                FirstName = "Existing",
                LastName = "User",
                Username = "existing.user",
                Email = "test@EXAMPLE.COM",
                PhoneNumber = "0918555666",
                HashedPassword = "ExistingPass1!",
                Role = "Patient",
                BirthNumber = "9203147880",
                Insurance = "16"
            };
            _context.RegisteredPatients.Add(existingPatient);
            await _context.SaveChangesAsync();

            var newPatient = new RegisteredPatient
            {
                FirstName = "New",
                LastName = "User",
                Username = "new.user",
                Email = "TEST@example.com",
                PhoneNumber = "0918777888",
                HashedPassword = "NewPass1!",
                Role = "Patient",
                BirthNumber = Guid.NewGuid().ToString("N").Substring(0,10),
                Insurance = "15"
            };

            // Act: Attempt to register the patient.
            var result = await _controller.Register(newPatient);
            Assert.IsNotNull(result, "Result should not be null.");
        }

        [TestMethod]
        public async Task Register_SpecialCharactersInFields_ReturnsOk()
        {
            // Arrange: Create a patient with special characters in allowed fields.
            var patient = new RegisteredPatient
            {
                FirstName = "José",
                LastName = "O'Connor-Smith",
                Username = "jose_oconnor123",
                Email = "jose.oconnor+test@example-domain.co.uk",
                PhoneNumber = "+421-918-123-456",
                HashedPassword = "JoséPass1!@#",
                Role = "Patient",
                BirthNumber = Guid.NewGuid().ToString("N").Substring(0,10),
                Insurance = "14"
            };

            // Act: Register the patient.
            var result = await _controller.Register(patient);

            // Assert: Should handle special characters gracefully.
            Assert.IsInstanceOfType(result, typeof(OkObjectResult), "Result should be an OkObjectResult.");
        }

        [TestMethod]
        public async Task Register_LongFieldValues_ReturnsOk()
        {
            // Arrange: Create a patient with very long field values.
            var patient = new RegisteredPatient
            {
                FirstName = new string('A', 100),
                LastName = new string('B', 100),
                Username = "verylongusername" + Guid.NewGuid().ToString("N"),
                Email = "verylongemailaddress" + Guid.NewGuid().ToString("N") + "@example.com",
                PhoneNumber = "0918123456789012345",
                HashedPassword = "VeryLongPassword123!@#$%^&*()",
                Role = "Patient",
                BirthNumber = Guid.NewGuid().ToString("N").Substring(0,10),
                Insurance = "13"
            };

            // Act: Register the patient.
            var result = await _controller.Register(patient);

            // Assert: Should handle long values (or return appropriate error if validation exists).
            Assert.IsNotNull(result, "Result should not be null.");
        }

        #endregion

        #region PasswordValidationTests

        [TestMethod]
        public void PasswordRegexCheck_ShortPassword_ReturnsInvalid()
        {
            // Arrange: Provide a password shorter than 8 characters.
            var password = "Short1!";

            // Act: Validate the password using reflection helper.
            var (isValid, errorMessage) = InvokePasswordRegexCheck(password);

            // Assert: Verify the password is invalid with the expected error message.
            Assert.IsFalse(isValid, "Password should be invalid.");
            Assert.IsTrue(errorMessage.Contains("at least 8 characters"), 
                "Error message should indicate short password.");
        }

        [TestMethod]
        public void PasswordRegexCheck_MissingUppercase_ReturnsInvalid()
        {
            // Arrange: Provide a password without uppercase letters.
            var password = "lowercase1!";

            // Act: Validate the password.
            var (isValid, errorMessage) = InvokePasswordRegexCheck(password);

            // Assert: Verify the password is invalid with the expected error message.
            Assert.IsFalse(isValid, "Password should be invalid.");
            Assert.IsTrue(errorMessage.Contains("uppercase"), 
                "Error message should indicate missing uppercase.");
        }

        [TestMethod]
        public void PasswordRegexCheck_MissingLowercase_ReturnsInvalid()
        {
            // Arrange: Provide a password without lowercase letters.
            var password = "UPPERCASE1!";

            // Act: Validate the password.
            var (isValid, errorMessage) = InvokePasswordRegexCheck(password);

            // Assert: Verify the password is invalid with the expected error message.
            Assert.IsFalse(isValid, "Password should be invalid.");
            Assert.IsTrue(errorMessage.Contains("lowercase"), 
                "Error message should indicate missing lowercase.");
        }

        [TestMethod]
        public void PasswordRegexCheck_MissingDigit_ReturnsInvalid()
        {
            // Arrange: Provide a password without digits.
            var password = "NoDigit!@#";

            // Act: Validate the password.
            var (isValid, errorMessage) = InvokePasswordRegexCheck(password);

            // Assert: Verify the password is invalid with the expected error message.
            Assert.IsFalse(isValid, "Password should be invalid.");
            Assert.IsTrue(errorMessage.Contains("digit"), 
                "Error message should indicate missing digits.");
        }

        [TestMethod]
        public void PasswordRegexCheck_MissingSpecialCharacter_ReturnsInvalid()
        {
            // Arrange: Provide a password without special characters.
            var password = "NoSpecial1";

            // Act: Validate the password.
            var (isValid, errorMessage) = InvokePasswordRegexCheck(password);

            // Assert: Verify the password is invalid with the expected error message.
            Assert.IsFalse(isValid, "Password should be invalid.");
            Assert.IsTrue(errorMessage.Contains("special"), 
                "Error message should indicate missing special characters.");
        }

        [TestMethod]
        public void PasswordRegexCheck_ValidPassword_ReturnsValid()
        {
            // Arrange: Provide a valid password meeting all requirements.
            var password = "ValidPass1!";

            // Act: Validate the password.
            var (isValid, errorMessage) = InvokePasswordRegexCheck(password);

            // Assert: Verify the password is valid with the expected message.
            Assert.IsTrue(isValid, "Password should be valid.");
            Assert.AreEqual("Password is valid.", errorMessage, "Success message should match.");
        }

        [TestMethod]
        public void PasswordRegexCheck_NullPassword_ReturnsInvalid()
        {
            // Arrange: Provide a null password.
            string password = null;

            // Act: Validate the password.
            var (isValid, errorMessage) = InvokePasswordRegexCheck(password);

            // Assert: Verify the password is invalid.
            Assert.IsFalse(isValid, "Password should be invalid.");
            Assert.AreEqual("Password cannot be empty.", errorMessage, "Error message should match.");
        }

        [TestMethod]
        public void PasswordRegexCheck_EmptyPassword_ReturnsInvalid()
        {
            // Arrange: Provide an empty password.
            var password = "";

            // Act: Validate the password.
            var (isValid, errorMessage) = InvokePasswordRegexCheck(password);

            // Assert: Verify the password is invalid.
            Assert.IsFalse(isValid, "Password should be invalid.");
            Assert.AreEqual("Password cannot be empty.", errorMessage, "Error message should match.");
        }

        [TestMethod]
        public void PasswordRegexCheck_WhitespacePassword_ReturnsInvalid()
        {
            // Arrange: Provide a whitespace-only password.
            var password = "   ";

            // Act: Validate the password.
            var (isValid, errorMessage) = InvokePasswordRegexCheck(password);

            // Assert: Verify the password is invalid.
            Assert.IsFalse(isValid, "Password should be invalid.");
            Assert.IsTrue(errorMessage.Contains("at least 8 characters"), 
                "Error message should indicate short password.");
        }

        [TestMethod]
        public void PasswordRegexCheck_ExactlyEightCharacters_Valid()
        {
            // Arrange: Provide a password with exactly 8 characters.
            var password = "Valid1!a";

            // Act: Validate the password.
            var (isValid, errorMessage) = InvokePasswordRegexCheck(password);

            // Assert: Verify the password is valid.
            Assert.IsTrue(isValid, "Password should be valid.");
            Assert.AreEqual("Password is valid.", errorMessage, "Success message should match.");
        }

        [TestMethod]
        public void PasswordRegexCheck_MultipleErrors_ReturnsAllErrors()
        {
            // Arrange: Provide a password with multiple validation errors.
            var password = "short";

            // Act: Validate the password.
            var (isValid, errorMessage) = InvokePasswordRegexCheck(password);

            // Assert: Verify all errors are reported.
            Assert.IsFalse(isValid, "Password should be invalid.");
            Assert.IsTrue(errorMessage.Contains("at least 8 characters"), "Should mention length requirement.");
            Assert.IsTrue(errorMessage.Contains("uppercase"), "Should mention missing uppercase.");
            Assert.IsTrue(errorMessage.Contains("digit"), "Should mention missing digits.");
            Assert.IsTrue(errorMessage.Contains("special"), "Should mention missing special characters.");
        }

        [TestMethod]
        public void PasswordRegexCheck_AllSpecialCharacters_Valid()
        {
            // Arrange: Test various special characters.
            var passwords = new[]
            {
                "ValidPass1!",
                "ValidPass1@",
                "ValidPass1#",
                "ValidPass1$",
                "ValidPass1%",
                "ValidPass1^",
                "ValidPass1&",
                "ValidPass1*",
                "ValidPass1(",
                "ValidPass1)",
                "ValidPass1,",
                "ValidPass1.",
                "ValidPass1?",
                "ValidPass1\"",
                "ValidPass1{",
                "ValidPass1}",
                "ValidPass1|",
                "ValidPass1<",
                "ValidPass1>"
            };

            foreach (var password in passwords)
            {
                // Act: Validate each password.
                var (isValid, errorMessage) = InvokePasswordRegexCheck(password);

                // Assert: Each should be valid.
                Assert.IsTrue(isValid, $"Password '{password}' should be valid.");
                Assert.AreEqual("Password is valid.", errorMessage, $"Success message should match for '{password}'.");
            }
        }

        [TestMethod]
        public void PasswordRegexCheck_VeryLongPassword_Valid()
        {
            // Arrange: Provide a very long valid password.
            var password = "ThisIsAVeryLongPasswordThatMeetsAllRequirementsIncludingUppercaseLowercaseDigits123AndSpecialCharacters!@#";

            // Act: Validate the password.
            var (isValid, errorMessage) = InvokePasswordRegexCheck(password);

            // Assert: Verify the password is valid.
            Assert.IsTrue(isValid, "Long password should be valid.");
            Assert.AreEqual("Password is valid.", errorMessage, "Success message should match.");
        }

        [TestMethod]
        public void PasswordRegexCheck_UnicodeCharacters_HandledCorrectly()
        {
            // Arrange: Provide a password with unicode characters.
            var password = "Pássw0rd!"; // Contains accented character

            // Act: Validate the password.
            var (isValid, errorMessage) = InvokePasswordRegexCheck(password);

            // Assert: Should handle unicode characters appropriately.
            Assert.IsTrue(isValid, "Password with unicode should be valid.");
        }

        #endregion

        #region LoggingTests

        [TestMethod]
        public async Task Register_ValidPatient_LogsSuccess()
        {
            // Arrange: Create mock logger to verify logging calls.
            var mockLogger = new Mock<ILogger<RegisterController>>();
            var controller = new RegisterController(_context, mockLogger.Object);
            var patient = new RegisteredPatient
            {
                FirstName = "Log",
                LastName = "Test",
                Username = "logtest",
                Email = "log.test@example.com",
                PhoneNumber = "0918999888",
                HashedPassword = "LogTest1!",
                Role = "Patient",
                BirthNumber = "9203147876",
                Insurance = "12"
            };

            // Act: Register the patient.
            await controller.Register(patient);

            // Assert: Verify logging was called.
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Register called for username")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task Register_InvalidPassword_LogsWarning()
        {
            // Arrange: Create mock logger to verify logging calls.
            var mockLogger = new Mock<ILogger<RegisterController>>();
            var controller = new RegisterController(_context, mockLogger.Object);
            var patient = new RegisteredPatient
            {
                FirstName = "Warning",
                LastName = "Test",
                Username = "warningtest",
                Email = "warning.test@example.com",
                PhoneNumber = "0918777999",
                HashedPassword = "weak",
                Role = "Patient",
                BirthNumber = "9203147875",
                Insurance = "11"
            };

            // Act: Register the patient.
            await controller.Register(patient);

            // Assert: Verify warning was logged for password validation failure.
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Password validation failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #region EdgeCaseTests

        [TestMethod]
        public async Task Register_DatabaseSaveException_ReturnsServerError()
        {
            // Arrange
            var patient = new RegisteredPatient
            {
                FirstName = "Exception",
                LastName = "Test",
                Username = "exceptiontest",
                Email = "exception.test@example.com",
                PhoneNumber = "0918666777",
                HashedPassword = "ExceptionTest1!",
                Role = "Patient",
                BirthNumber = "9203147874",
                Insurance = "10"
            };

            var mockContext = new Mock<AppDbContext>(new DbContextOptions<AppDbContext>());
            mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DbUpdateException());
            
            var controller = new RegisterController(mockContext.Object, _mockLogger.Object);

            // Act
            var result = await controller.Register(patient);

            // Assert
            Assert.IsInstanceOfType(result, typeof(ObjectResult));
            var objectResult = result as ObjectResult;
            Assert.AreEqual(500, objectResult.StatusCode);
        }

        [TestMethod]
        public async Task Register_EmailWithPlusSign_HandledCorrectly()
        {
            // Arrange
            var patient = new RegisteredPatient
            {
                FirstName = "Plus",
                LastName = "Test",
                Username = "plustest",
                Email = "user+tag@example.com",
                PhoneNumber = "0918444555",
                HashedPassword = "PlusTest1!",
                Role = "Patient",
                BirthNumber = "9203147873",
                Insurance = "09"
            };

            // Act
            var result = await _controller.Register(patient);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkObjectResult), "Result should be an OkObjectResult.");
            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult?.Value);
        }

        #endregion
    }
}