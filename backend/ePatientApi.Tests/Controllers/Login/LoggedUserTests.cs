using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ePatientApi.Models;

namespace ePatientApi.Tests
{
    [TestClass]
    public class LoggedUserTests
    {
        #region ModelTests

        [TestMethod]
        public void LoggedUser_SetDetails_ReturnsCorrectValues()
        {
            // Arrange: Initialize a LoggedUser with test data.
            var user = new LoggedUser
            {
                Id = 42,
                Username = "janko.mrkvicka",
                FirstName = "Janko",
                LastName = "Mrkvicka",
                Role = "Patient"
            };

            // Act: Compute the full name.
            var fullName = $"{user.FirstName} {user.LastName}";

            // Assert: Verify all properties are set correctly.
            Assert.AreEqual(42, user.Id, "User ID should match the set value.");
            Assert.AreEqual("janko.mrkvicka", user.Username, "Username should match the set value.");
            Assert.AreEqual("Janko", user.FirstName, "First name should match the set value.");
            Assert.AreEqual("Mrkvicka", user.LastName, "Last name should match the set value.");
            Assert.AreEqual("Janko Mrkvicka", fullName, "Full name should combine first and last names.");
        }

        #endregion
    }
}