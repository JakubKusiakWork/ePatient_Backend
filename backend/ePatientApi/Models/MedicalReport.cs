using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ePatientApi.Models
{
    /// <summary>
    /// Represents a medical report created during or after an appointment.
    /// </summary>
    [Table("medicalreports")]
    public class MedicalReport
    {
        [Key]
        [Column("report_id")]
        public int ReportId { get; set; }

        [Column("patient_birth_number")]
        public required string PatientBirthNumber { get; set; }

        [Column("appointment_id")]
        public int AppointmentId { get; set; }

        [Column("patient_id")]
        public int? PatientId { get; set; }

        [Column("medication")]
        public required string Medication { get; set; }

        [Column("external_examinations")]
        public required string ExternalExaminations { get; set; }

        [Column("condition")]
        public required string Condition { get; set; }

        [Column("state")]
        public required string State { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [ForeignKey("PatientBirthNumber")]
        public virtual RegisteredPatient? Patient { get; set; }

        [Column("attendingdoctor")]
        public string? AttendingDoctor { get; set; }

        [Column("follow_up_required")]
        public bool FollowUpRequired { get; set; } = false;

        [Column("priority")]
        public string Priority { get; set; } = "Routine";

        public virtual ICollection<Prescription>? Prescriptions { get; set; }
    }
}