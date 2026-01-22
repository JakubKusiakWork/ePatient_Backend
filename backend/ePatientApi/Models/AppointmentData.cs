using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ePatientApi.Models
{
    /// <summary>
    /// Represents an appointment reservation record.
    /// </summary>
    [Table("appointments")]
    public class AppointmentData
    {
        [Column("appointment_id")]
        public int AppointmentId { get; set; }

        [Column("reservation_time")]
        public string ReservationTime { get; set; } = null!;

        [Column("reservation_day")]
        public DateTime ReservationDay { get; set; }

        [Column("patient_birth_number")]
        public required string PatientBirthNumber { get; set; } = null!;

        [ForeignKey("PatientBirthNumber")]
        [JsonIgnore]
        public virtual RegisteredPatient? Patient { get; set; }
        
        [Column("google_event_id")]
        public string? GoogleEventId { get; set; }

        [Column("doctor_id")]
        public int? DoctorId { get; set; }

        [Column("doctor_name")]
        public string? DoctorName { get; set; }

        [Column("status")]
        public string Status { get; set; } = "scheduled";
    }
}