using ePatientApi.Models;
using ePatientApi.Interfaces;

namespace ePatientApi.Services
{
    /// <summary>
    /// Service for providing homepage data.
    /// </summary>
    public class HomepageDataService : IHomepageData
    {
        public Task<HomepageData> GetHomepageDataAsync()
        {
            var data = new HomepageData
            {
                PatientName = "John Doe",
                Age = 45,
                LastVisitDate = "2025-04-20",
                UpcomingAppointment = "2025-05-10",
                RecentDiagnoses = new List<string> { "Hypertension", "Type 2 Diabetes" }
            };
            return Task.FromResult(data);
        }
    }
}