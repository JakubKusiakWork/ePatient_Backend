namespace ePatientApi.Models
{
    /// <summary>
    /// DTO for doctor login credentials.
    /// </summary>
    public class DoctorLogin
    {
        public string DoctorCode { get; set; } = null!;
        public string DoctorPassword { get; set; } = null!;
    }
}