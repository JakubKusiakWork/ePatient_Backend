using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ePatientApi.Controllers;
using ePatientApi.Services;
using ePatientApi.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.ComponentModel.DataAnnotations;

#nullable enable

namespace ePatientApi.Tests.Controllers.Misc
{
    [TestClass]
    public class InsuranceTests
    {
        private Mock<IVsZPService> mockVSZP = null!;
        private Mock<IUnionService> mockUnion = null!;
        private Mock<IDoveraService> mockDovera = null!;
        private InsuranceController myController = null!;

        [TestInitialize]
        public void Setup()
        {
            mockVSZP = new Mock<IVsZPService>(MockBehavior.Strict);
            mockUnion = new Mock<IUnionService>(MockBehavior.Strict);
            mockDovera = new Mock<IDoveraService>(MockBehavior.Strict);

            myController = new InsuranceController(
                mockVSZP.Object,
                mockUnion.Object,
                mockDovera.Object
            );
        }
        
        #region BadRequestTests

        [TestMethod]
        public async Task CheckVSZP_NullRequest_BadRequest()
        {
            // Arrange
            InsuranceController.CheckInsuranceRequest? request = null;
            var ct = CancellationToken.None;

            // Act
            var result = await myController.CheckVSZP(request, ct);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task CheckUnion_NullRequest_BadRequest()
        {
            // Arrange
            InsuranceController.CheckInsuranceRequest? request = null;
            var ct = CancellationToken.None;

            // Act
            var result = await myController.CheckUnion(request, ct);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        [TestMethod]
        public async Task CheckDovera_NullRequest_BadRequest()
        {
            // Arrange
            InsuranceController.CheckInsuranceRequest? request = null;
            var ct = CancellationToken.None;

            // Act
            var result = await myController.CheckDovera(request, ct);

            // Assert
            Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        }

        #endregion

        #region ValidTests

        [TestMethod]
        public async Task CheckVSZP_ValidRequest_Ok()
        {
            // Arrange
            var request = new InsuranceController.CheckInsuranceRequest
            (
                birthNumber: "000720/6903",
                date: new DateTime(2026, 1, 7)
            );
            var ct = CancellationToken.None;

            mockVSZP
                .Setup(x => x.CheckAsync(request.birthNumber!, request.date!.Value, ct))
                .ReturnsAsync(true);

            // Act
            var result = await myController.CheckVSZP(request, ct);

            // Assert
            var ok = result as OkObjectResult;
            Assert.IsNotNull(ok);

            Assert.IsInstanceOfType(ok.Value, typeof(InsuranceController.CheckSingleResponse));
            var payload = (InsuranceController.CheckSingleResponse)ok.Value!;
            Assert.AreEqual(1, payload.ok);

            mockVSZP
                .Verify(x => x.CheckAsync(request.birthNumber!, request.date!.Value, ct), Times.Once);  
            mockVSZP
                .VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task CheckUnion_ValidRequest_Ok()
        {
            // Arrange
            var request = new InsuranceController.CheckInsuranceRequest
            (
                birthNumber: "000201/6762",
                date: new DateTime(2026, 1, 7)
            );
            var ct = CancellationToken.None;

            mockUnion
                .Setup(x => x.CheckAsync(request.birthNumber!, request.date!.Value, ct))
                .ReturnsAsync(true);

            // Act
            var result = await myController.CheckUnion(request, ct);

            // Assert
            var ok = result as OkObjectResult;
            Assert.IsNotNull(ok);

            Assert.IsInstanceOfType(ok.Value, typeof(InsuranceController.CheckSingleResponse));
            var payload = (InsuranceController.CheckSingleResponse)ok.Value!;
            Assert.AreEqual(1, payload.ok);

            mockUnion
                .Verify(x => x.CheckAsync(request.birthNumber!, request.date!.Value, ct), Times.Once);
            mockUnion
                .VerifyNoOtherCalls();
        }

        #endregion
    }
}

#nullable restore