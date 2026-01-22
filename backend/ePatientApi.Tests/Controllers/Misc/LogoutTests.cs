using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using ePatientApi.Controllers;
using ePatientApi.Interfaces;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ePatientApi.Tests
{
    [TestClass]
    public class LogoutControllerTests
    {
        private Mock<ITokenBlacklistService> _mockTokenBlacklistService;
        private Mock<IJwtToken> _mockJwtToken;
        private LogoutController _controller;

        [TestInitialize]
        public void Setup()
        {
            // Arrange: Initialize mocks and controller.
            _mockTokenBlacklistService = new Mock<ITokenBlacklistService>();
            _mockJwtToken = new Mock<IJwtToken>();
            _controller = new LogoutController(_mockTokenBlacklistService.Object, _mockJwtToken.Object);
        }

        #region InvalidLogoutTests

        [TestMethod]
        public void Logout_MissingToken_ReturnsBadRequest()
        {
            // Arrange: Set up a request with no token and mock token extraction to return null.
            var context = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext { HttpContext = context };
            _mockJwtToken.Setup(x => x.ExtractTokenFromHeader(It.IsAny<HttpRequest>())).Returns((string)null);

            // Act: Attempt to log out.
            var result = _controller.Logout();

            // Assert: Verify the response is a BadRequestObjectResult with the expected message.
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult), "Result should be a BadRequestObjectResult.");
            var badRequest = result as BadRequestObjectResult;
            Assert.AreEqual("Missing token.", badRequest.Value, "Error message should match.");
        }

        #endregion

        #region ValidLogoutTests

        [TestMethod]
        public void Logout_ValidToken_ReturnsOk()
        {
            // Arrange: Set up a request with a valid token and mock token parsing.
            var fakeToken = GenerateFakeJwtToken();
            var context = new DefaultHttpContext();
            context.Request.Headers["Authorization"] = $"Bearer {fakeToken}";
            _controller.ControllerContext = new ControllerContext { HttpContext = context };
            _mockJwtToken.Setup(x => x.ExtractTokenFromHeader(It.IsAny<HttpRequest>())).Returns(fakeToken);
            var fakeJwt = new JwtSecurityToken(expires: DateTime.UtcNow.AddMinutes(5));
            _mockJwtToken.Setup(x => x.ParseJwtToken(fakeToken)).Returns(fakeJwt);

            // Act: Attempt to log out.
            var result = _controller.Logout();

            // Assert: Verify the token is blacklisted and the response is an OkObjectResult.
            _mockTokenBlacklistService.Verify(x => x.BlacklistToken(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once());
            Assert.IsInstanceOfType(result, typeof(OkObjectResult), "Result should be an OkObjectResult.");
            var okResult = result as OkObjectResult;
            Assert.AreEqual("Logged out successfully.", okResult.Value, "Success message should match.");
        }

        #endregion

        private string GenerateFakeJwtToken()
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateJwtSecurityToken(
                subject: new ClaimsIdentity(new[] { new Claim("sub", "123") }),
                expires: DateTime.UtcNow.AddMinutes(5)
            );
            return tokenHandler.WriteToken(token);
        }
    }
}