using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ePatientApi.Services;
using System.IdentityModel.Tokens.Jwt;

namespace ePatientApi.Tests
{
    [TestClass]
    public class JwtTokenServiceTests
    {
        private JwtTokenService _service;

        [TestInitialize]
        public void Setup()
        {
            // Arrange: Initialize the JwtTokenService.
            _service = new JwtTokenService();
        }

        #region InvalidTokenTests

        [TestMethod]
        public void ExtractTokenFromHeader_EmptyHeader_ReturnsNull()
        {
            // Arrange: Create a request with no Authorization header.
            var request = new DefaultHttpContext().Request;

            // Act: Extract the token from the header.
            var token = _service.ExtractTokenFromHeader(request);

            // Assert: Verify the token is null.
            Assert.IsNull(token, "Token should be null for empty header.");
        }

        [TestMethod]
        public void ExtractTokenFromHeader_NonBearerHeader_ReturnsNull()
        {
            // Arrange: Create a request with a non-Bearer Authorization header.
            var request = new DefaultHttpContext();
            request.Request.Headers["Authorization"] = "Basic itsBasicAuthHeader";

            // Act: Extract the token from the header.
            var token = _service.ExtractTokenFromHeader(request.Request);

            // Assert: Verify the token is null.
            Assert.IsNull(token, "Token should be null for non-Bearer header.");
        }

        [TestMethod]
        public void ParseJwtToken_InvalidFormat_ReturnsNull()
        {
            // Arrange: Provide an invalid token string.
            var invalidToken = "invalidTokenFormat";

            // Act: Parse the token.
            var parsedToken = _service.ParseJwtToken(invalidToken);

            // Assert: Verify the parsed token is null.
            Assert.IsNull(parsedToken, "Parsed token should be null for invalid format.");
        }

        #endregion

        #region ValidTokenTests

        [TestMethod]
        public void ExtractTokenFromHeader_BearerHeader_ReturnsToken()
        {
            // Arrange: Create a request with a valid Bearer token.
            var request = new DefaultHttpContext();
            var expectedToken = "validToken";
            request.Request.Headers["Authorization"] = $"Bearer {expectedToken}";

            // Act: Extract the token from the header.
            var token = _service.ExtractTokenFromHeader(request.Request);

            // Assert: Verify the extracted token matches the expected value.
            Assert.AreEqual(expectedToken, token, "Extracted token should match the provided token.");
        }

        [TestMethod]
        public void ParseJwtToken_ValidToken_ReturnsParsedToken()
        {
            // Arrange: Create a valid JWT token.
            var handler = new JwtSecurityTokenHandler();
            var token = handler.CreateJwtSecurityToken(
                issuer: "MSTestIssuer",
                audience: "MSTestAudience"
            );
            var jwtToken = handler.WriteToken(token);

            // Act: Parse the token.
            var parsedToken = _service.ParseJwtToken(jwtToken);

            // Assert: Verify the parsed token is valid and contains expected issuer.
            Assert.IsNotNull(parsedToken, "Parsed token should not be null.");
            Assert.AreEqual("MSTestIssuer", parsedToken.Issuer, "Issuer should match the configured value.");
        }

        #endregion
    }
}