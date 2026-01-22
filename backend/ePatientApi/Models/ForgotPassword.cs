namespace ePatientApi.Models
{
    public class ForgotPassword
    {
        public int Id { get; set; }
        public string PatientBirthNumber { get; set; } = null!;
        public RegisteredPatient Patient { get; set; } = null!;
        public string TokenHash { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
        public bool Used { get; set; } = false;
        public DateTime CreatedAt {get; set; } = DateTime.UtcNow;
    }
}