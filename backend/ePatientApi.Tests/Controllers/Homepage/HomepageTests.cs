using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using ePatientApi.Controllers;
using ePatientApi.Models;
using ePatientApi.Interfaces;

namespace ePatientApi.Tests
{
    [TestClass]
    public class HomepageControllerTests
    {
        private Mock<IHomepageData> _dataService;
        private Mock<IJwtToken> _tokenService;
        private HomepageController _controller;
        private HomepageData _testData;
        private const string ValidToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...";

        [TestInitialize]
        public void Setup()
        {
            // Arrange: Initialize mocks and test data.
            _dataService = new Mock<IHomepageData>();
            _tokenService = new Mock<IJwtToken>();
            _testData = new HomepageData
            {
                PatientName = "John Doe",
                Age = 30,
                LastVisitDate = "2025-04-01",
                UpcomingAppointment = "2025-05-10",
                RecentDiagnoses = new List<string> { "Flu", "Hypertension" }
            };
            _dataService.Setup(s => s.GetHomepageDataAsync()).ReturnsAsync(_testData);

            var httpContext = new DefaultHttpContext();
            _controller = new HomepageController(_dataService.Object, _tokenService.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };
        }

        #region ValidTokenTests

        [TestMethod]
        public async Task GetHomepageData_ValidToken_ReturnsOk()
        {
            // Arrange: Mock token extraction and parsing with valid claims.
            var claims = new[] { new Claim("unique_name", "testuser") };
            var jwt = new JwtSecurityToken(claims: claims);
            _tokenService.Setup(t => t.ExtractTokenFromHeader(It.IsAny<HttpRequest>())).Returns(ValidToken);
            _tokenService.Setup(t => t.ParseJwtToken(ValidToken)).Returns(jwt);

            // Act: Retrieve homepage data.
            var result = await _controller.GetHomepageData();

            // Assert: Verify the response is an OkObjectResult with correct data.
            Assert.IsInstanceOfType(result.Result, typeof(OkObjectResult), "Result should be an OkObjectResult.");
            var okResult = result.Result as OkObjectResult;
            Assert.AreEqual(200, okResult.StatusCode, "Status code should be 200.");
            var returnedData = okResult.Value as HomepageData;
            Assert.IsNotNull(returnedData, "Returned data should not be null.");
            Assert.AreEqual(_testData.PatientName, returnedData.PatientName, "Patient name should match.");
            Assert.AreEqual(_testData.Age, returnedData.Age, "Age should match.");
            _dataService.Verify(s => s.GetHomepageDataAsync(), Times.Once(), "Data service should be called once.");
        }

        [TestMethod]
        public async Task ExportToPdf_ValidToken_ReturnsPdfFile()
        {
            // Arrange: Mock token extraction and parsing with valid patient ID.
            var jwt = new JwtSecurityToken(claims: new[] { new Claim("sub", "patient123") });
            _tokenService.Setup(t => t.ExtractTokenFromHeader(It.IsAny<HttpRequest>())).Returns(ValidToken);
            _tokenService.Setup(t => t.ParseJwtToken(ValidToken)).Returns(jwt);

            // Act: Export data to PDF.
            var result = await _controller.ExportToPdf();

            // Assert: Verify the response is a FileContentResult with PDF content.
            Assert.IsInstanceOfType(result, typeof(FileContentResult), "Result should be a FileContentResult.");
            var fileResult = result as FileContentResult;
            Assert.AreEqual("application/pdf", fileResult.ContentType, "Content type should be PDF.");
            Assert.AreEqual("PatientReport.pdf", fileResult.FileDownloadName, "File name should match.");
            Assert.IsTrue(fileResult.FileContents.Length > 0, "File should contain content.");
            _dataService.Verify(s => s.GetHomepageDataAsync(), Times.Once(), "Data service should be called once.");
        }

        #endregion

        #region InvalidTokenTests

        [TestMethod]
        public async Task GetHomepageData_MissingToken_ReturnsBadRequest()
        {
            // Arrange: Mock token extraction to return null.
            _tokenService.Setup(t => t.ExtractTokenFromHeader(It.IsAny<HttpRequest>())).Returns((string)null);

            // Act: Attempt to retrieve homepage data.
            var result = await _controller.GetHomepageData();

            // Assert: Verify the response is a BadRequestObjectResult.
            Assert.IsInstanceOfType(result.Result, typeof(BadRequestObjectResult), "Result should be a BadRequestObjectResult.");
            var badRequest = result.Result as BadRequestObjectResult;
            Assert.AreEqual(400, badRequest.StatusCode, "Status code should be 400.");
            Assert.AreEqual("Missing token.", badRequest.Value, "Error message should match.");
            _dataService.Verify(s => s.GetHomepageDataAsync(), Times.Never(), "Data service should not be called.");
        }

        [TestMethod]
        public async Task GetHomepageData_InvalidToken_ReturnsBadRequest()
        {
            // Arrange: Mock token extraction and parsing to return an invalid token.
            _tokenService.Setup(t => t.ExtractTokenFromHeader(It.IsAny<HttpRequest>())).Returns("invalid-token");
            _tokenService.Setup(t => t.ParseJwtToken("invalid-token")).Returns((JwtSecurityToken)null);

            // Act: Attempt to retrieve homepage data.
            var result = await _controller.GetHomepageData();

            // Assert: Verify the response is a BadRequestObjectResult.
            Assert.IsInstanceOfType(result.Result, typeof(BadRequestObjectResult), "Result should be a BadRequestObjectResult.");
            var badRequest = result.Result as BadRequestObjectResult;
            Assert.AreEqual(400, badRequest.StatusCode, "Status code should be 400.");
            Assert.AreEqual("Invalid token.", badRequest.Value, "Error message should match.");
            _dataService.Verify(s => s.GetHomepageDataAsync(), Times.Never(), "Data service should not be called.");
        }

        [TestMethod]
        public async Task ExportToPdf_MissingToken_ReturnsBadRequest()
        {
            // Arrange: Mock token extraction to return null.
            _tokenService.Setup(t => t.ExtractTokenFromHeader(It.IsAny<HttpRequest>())).Returns((string)null);

            // Act: Attempt to export data to PDF.
            var result = await _controller.ExportToPdf();

            // Assert: Verify the response is a BadRequestObjectResult.
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult), "Result should be a BadRequestObjectResult.");
            var badRequest = result as BadRequestObjectResult;
            Assert.AreEqual(400, badRequest.StatusCode, "Status code should be 400.");
            Assert.AreEqual("Missing token.", badRequest.Value, "Error message should match.");
            _dataService.Verify(s => s.GetHomepageDataAsync(), Times.Never(), "Data service should not be called.");
        }

        #endregion
    }
}