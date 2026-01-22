using System.ComponentModel.DataAnnotations.Schema;

namespace ePatientApi.Models
{
    /// <summary>
    /// Represents an examination record for a patient while on-site.
    /// </summary>
    [Table("patientexamination")]
    public class PatientExaminationData
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("patient_id")]
        public int PatientId { get; set; }

        [Column("examination")]
        public string? Examination { get; set; }

        [Column("attendingdoctor")]
        public string? AttendingDoctor { get; set; }

        [Column("room")]
        public string? Room { get; set; }

        public RegisteredPatient? RegisteredPatient { get; set; }
    }
}