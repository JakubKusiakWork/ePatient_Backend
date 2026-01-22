using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using ePatientApi.Services;
using System.Linq;

namespace ePatientApi.Tests
{
    [TestClass]
    public class HomepageDataServiceTests
    {
        private HomepageDataService _service;

        [TestInitialize]
        public void Setup()
        {
            // Arrange: Initialize the HomepageDataService.
            _service = new HomepageDataService();
        }

        #region DataRetrievalTests

        [TestMethod]
        public async Task GetHomepageDataAsync_ValidRequest_ReturnsExpectedData()
        {
            // Arrange: No specific setup required as service returns mock data.

            // Act: Retrieve homepage data.
            var data = await _service.GetHomepageDataAsync();

            // Assert: Verify the returned data matches expected values.
            Assert.IsNotNull(data, "Homepage data should not be null.");
            Assert.AreEqual("John Doe", data.PatientName, "Patient name should match.");
            Assert.AreEqual(45, data.Age, "Age should match.");
            Assert.AreEqual("2025-04-20", data.LastVisitDate, "Last visit date should match.");
            Assert.AreEqual("2025-05-10", data.UpcomingAppointment, "Upcoming appointment should match.");
            var expectedDiagnoses = new[] { "Hypertension", "Type 2 Diabetes" };
            CollectionAssert.AreEqual(expectedDiagnoses.ToList(), data.RecentDiagnoses, "Recent diagnoses should match.");
        }

        #endregion
    }
}