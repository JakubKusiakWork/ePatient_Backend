using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ePatientApi.Services;

namespace ePatientApi.Tests
{
    [TestClass]
    public class BlacklistTokenServiceTests
    {
        private BlacklistTokenService _service;

        [TestInitialize]
        public void Setup()
        {
            // Arrange: Initialize the BlacklistTokenService.
            _service = new BlacklistTokenService();
        }

        #region ValidTokenTests

        [TestMethod]
        public void BlacklistToken_ValidToken_AddsToBlacklist()
        {
            // Arrange: Create a valid token with future expiry.
            string token = "testToken";
            var expiry = DateTime.UtcNow.AddMinutes(10);

            // Act: Blacklist the token.
            _service.BlacklistToken(token, expiry);

            // Assert: Verify the token is blacklisted.
            Assert.IsTrue(_service.IsTokenBlacklisted(token), "Token should be blacklisted.");
        }

        #endregion

        #region ExpiryTests

        [TestMethod]
        public void BlacklistToken_ExpiredToken_RemovesFromBlacklist()
        {
            // Arrange: Blacklist a valid token and an expired token.
            string validToken = "testToken";
            string expiredToken = "expiredToken";
            _service.BlacklistToken(validToken, DateTime.UtcNow.AddMinutes(5));
            _service.BlacklistToken(expiredToken, DateTime.UtcNow.AddMinutes(-1));

            // Act: Check if tokens are blacklisted.
            var isExpiredBlacklisted = _service.IsTokenBlacklisted(expiredToken);
            var isValidBlacklisted = _service.IsTokenBlacklisted(validToken);

            // Assert: Verify the expired token is not blacklisted, but the valid one is.
            Assert.IsFalse(isExpiredBlacklisted, "Expired token should not be blacklisted.");
            Assert.IsTrue(isValidBlacklisted, "Valid token should be blacklisted.");
        }

        [TestMethod]
        public void BlacklistToken_AlreadyExpiredToken_DoesNotAddToBlacklist()
        {
            // Arrange: Create a token with a past expiry date.
            string expiredToken = "expiredToken";
            var expiry = DateTime.UtcNow.AddMinutes(-5);

            // Act: Attempt to blacklist the expired token.
            _service.BlacklistToken(expiredToken, expiry);

            // Assert: Verify the token is not blacklisted.
            Assert.IsFalse(_service.IsTokenBlacklisted(expiredToken), "Expired token should not be blacklisted.");
        }

        #endregion
    }
}