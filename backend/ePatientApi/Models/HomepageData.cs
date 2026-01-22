namespace ePatientApi.Models
{
    /// <summary>
    /// Aggregated data presented on the homepage/dashboard.
    /// </summary>
    public class HomepageData
    {
        public string PatientName { get; set; } = string.Empty;
        public int Age { get; set; }
        public string LastVisitDate { get; set; } = string.Empty;
        public string UpcomingAppointment { get; set; } = string.Empty;
        public List<string> RecentDiagnoses { get; set; } = new List<string>();
    }
}