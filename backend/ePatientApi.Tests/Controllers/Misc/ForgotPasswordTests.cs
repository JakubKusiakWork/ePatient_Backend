using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.EntityFrameworkCore;
using ePatientApi.Controllers;
using ePatientApi.DataAccess;
using ePatientApi.Models;
using ePatientApi.Dtos;
using ePatientApi.Services;
using ePatientApi.Interfaces;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading;
using System.Security.Cryptography;
using System.Text;

namespace ePatientApi.Tests
{
    [TestClass]
    public class ForgotPasswordControllerTests
    {
        private AppDbContext _context;
        private ForgotPasswordController _controller;

        [TestInitialize]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: System.Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);

            var emailSender = new TestEmailSender();

            var inMemorySettings = new Dictionary<string, string>()
            {
                { "Auth:PasswordReset:ExpiresMinutes", "60" },
                { "CORS:localhostURL", "http://localhost:4200" }
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var passwordResetService = new PasswordResetService(_context, emailSender, configuration);
            _controller = new ForgotPasswordController(passwordResetService);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [TestMethod]
        public async Task VerifyEmail_EmailExists_ReturnsOkWithUserId()
        {
            // Arrange
            var existingPatient = new RegisteredPatient 
            { 
                Id = ePatientApi.Tests.Data.TestData.NextId(), 
                Email = "john.doe@test.com", 
                HashedPassword = "oldPasswordHash",
                FirstName = "John",
                LastName = "Doe",
                PhoneNumber = "1234567890",
                Username = "johndoe",
                BirthNumber = Guid.NewGuid().ToString("N").Substring(0,10),
                Insurance = "25",
                Role = "Patient"
            };
            _context.RegisteredPatients.Add(existingPatient);
            await _context.SaveChangesAsync();

            var request = new ForgotPasswordRequest { Email = "john.doe@test.com" };

            // Act
            var result = await _controller.ForgotPassword(request, CancellationToken.None);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult?.Value);

            dynamic responseValue = okResult.Value;
            Assert.AreEqual("If there is an account whit this email address, we have sent you a link to reset your password.", responseValue.GetType().GetProperty("message").GetValue(responseValue, null));

            var tokenEntry = await _context.ForgotPassword.FirstOrDefaultAsync(f => f.PatientBirthNumber == existingPatient.BirthNumber);
            Assert.IsNotNull(tokenEntry);
            Assert.IsFalse(tokenEntry.Used);
        }

        [TestMethod]
        public async Task VerifyEmail_EmailDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            var request = new ForgotPasswordRequest { Email = "nonexistent.email@test.com" };

            // Act
            var result = await _controller.ForgotPassword(request, CancellationToken.None);

            // Assert: service/controller intentionally returns same Ok message even when user missing
            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult?.Value);
            dynamic responseValue = okResult.Value;
            Assert.AreEqual("If there is an account whit this email address, we have sent you a link to reset your password.", responseValue.GetType().GetProperty("message").GetValue(responseValue, null));
        }

        [TestMethod]
        public async Task ResetPassword_UserExists_ReturnsOkAndPasswordIsChanged()
        {
            // Arrange
            var originalPasswordHash = BCrypt.Net.BCrypt.HashPassword("OldSecurePassword123!");
            var patientToReset = new RegisteredPatient 
            { 
                Id = ePatientApi.Tests.Data.TestData.NextId(), 
                Email = "jane.smith@test.com", 
                HashedPassword = originalPasswordHash,
                FirstName = "Jane",
                LastName = "Smith",
                PhoneNumber = "0987654321",
                Username = "janesmith",
                BirthNumber = Guid.NewGuid().ToString("N").Substring(0,10),
                Insurance = "25",
                Role = "Patient"
            };
            _context.RegisteredPatients.Add(patientToReset);
            await _context.SaveChangesAsync();

            var newPassword = "NewSuperSecurePassword456?";
            var rawTokenBytes = new byte[32];
            RandomNumberGenerator.Fill(rawTokenBytes);
            var rawToken = Convert.ToBase64String(rawTokenBytes);

            string ComputeSha256(string input)
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                var bytes = Encoding.UTF8.GetBytes(input);
                var hash = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }

            var hashed = ComputeSha256(rawToken);

            var tokenEntry = new ForgotPassword
            {
                PatientBirthNumber = patientToReset.BirthNumber,
                Patient = patientToReset,
                TokenHash = hashed,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };
            _context.ForgotPassword.Add(tokenEntry);
            await _context.SaveChangesAsync();

            var request = new ResetPasswordRequest { Token = Uri.EscapeDataString(rawToken), NewPassword = newPassword };

            // Act
            var result = await _controller.ResetPassword(request, CancellationToken.None);

            // Assert
            Assert.IsInstanceOfType(result, typeof(OkObjectResult));
            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult?.Value);

            dynamic responseValue = okResult.Value;
            Assert.AreEqual("Your password has been successfully changed. You can log in with your new password.", responseValue.GetType().GetProperty("message").GetValue(responseValue, null));

            var updatedPatient = await _context.RegisteredPatients.FirstOrDefaultAsync(p => p.BirthNumber == patientToReset.BirthNumber);
            Assert.IsNotNull(updatedPatient);
            Assert.AreNotEqual(originalPasswordHash, updatedPatient.HashedPassword);
            Assert.IsTrue(BCrypt.Net.BCrypt.Verify(newPassword, updatedPatient.HashedPassword));
            var usedToken = await _context.ForgotPassword.FirstOrDefaultAsync(t => t.Id == tokenEntry.Id);
            Assert.IsTrue(usedToken.Used);
        }

        [TestMethod]
        public async Task ResetPassword_UserDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            var request = new ResetPasswordRequest { Token = "invalidtoken", NewPassword = "SomeNewPassword" };

            // Act
            var result = await _controller.ResetPassword(request, CancellationToken.None);

            // Assert
            Assert.IsInstanceOfType(result, typeof(NotFoundObjectResult));
            var notFoundResult = result as NotFoundObjectResult;
            Assert.IsNotNull(notFoundResult?.Value);
            dynamic responseValue = notFoundResult.Value;
            Assert.AreEqual("Invalid or expired password reset token.", responseValue.GetType().GetProperty("message").GetValue(responseValue, null));
        }
    }

    public class TestEmailSender : IEmailSender
    {
        public Task SendEmail(string toEmail, int doctorId, CancellationToken cancelToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancelToken = default)
        {
            return Task.CompletedTask;
        }
    }
}