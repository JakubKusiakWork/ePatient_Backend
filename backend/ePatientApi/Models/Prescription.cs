using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ePatientApi.Models
{
    [Table("prescriptions")]
    public class Prescription
    {
        [Key]
        [Column("prescription_id")]
        public int PrescriptionId { get; set; }

        [Column("report_id")]
        public int ReportId { get; set; }

        [Column("medication_name")]
        public required string MedicationName { get; set; }

        [Column("dosage")]
        public string? Dosage { get; set; }

        [Column("instructions")]
        public string? Instructions { get; set; }

        [Column("quantity")]
        public string? Quantity { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("ReportId")]
        public virtual MedicalReport? MedicalReport { get; set; }
    }
}
