namespace ePatientApi.Models
{
    /// <summary>
    /// DTO used to identify an appointment and associated patient.
    /// </summary>
    public class AppointmentIdData
    {
        public int AppointmentId { get; set; }
        public required string PatientBirthNumber { get; set; }
    }
}