using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ePatientApi.Models
{
    [Table("healthcard_versions")]
    public class HealthCardVersion
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("healthcard_id")]
        public int HealthCardId { get; set; }

        [Column("version_number")]
        public int VersionNumber { get; set; }

        [Column("data_snapshot", TypeName = "jsonb")]
        public string DataSnapshot { get; set; } = string.Empty;

        [Column("modified_by")]
        public string? ModifiedBy { get; set; }

        [Column("modified_at")]
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

        [Column("change_summary")]
        public string? ChangeSummary { get; set; }
    }
}
