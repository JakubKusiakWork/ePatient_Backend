namespace ePatientApi.Models
{
    /// <summary>
    /// DTO used when resetting a patient's password.
    /// </summary>
    public class ResetPassword
    {
        public required string PatientId { get; set; }
        public required string ResetToken { get; set; }
        public required string NewPassword { get; set; }
    }
}