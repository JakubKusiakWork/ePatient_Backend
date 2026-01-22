using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ePatientApi.Models
{
    /// <summary>
    /// Snapshot of appointment details used for historical records.
    /// </summary>
    [Table("appointmentsdetails")]
    public class AppointmentSnapshot
    {
        [Key]
        [Column("snapshot_id")]
        public int SnapshotId { get; set; }

        [Column("appointment_id")]
        public int AppointmentId { get; set; }

        [Column("patient_id")]
        public int PatientId { get; set; }

        [Column("birth_number")]
        public required string BirthNumber { get; set; }

        [Column("first_name")]
        public required string FirstName { get; set; }

        [Column("last_name")]
        public required string LastName { get; set; }

        [Column("insurance")]
        public required string Insurance { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}