using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ePatientApi.Models
{
    [Table("healthcards")]
    public class HealthCardEntity
    {
        [Key]
        [Column("healthcard_id")]
        public int HealthCardId { get; set; }

        [Column("patient_birth_number")]
        public string PatientBirthNumber { get; set; } = null!;

        [Column("patient_id")]
        public int? PatientId { get; set; }

        [Column("blood_type")]
        public string? BloodType { get; set; }

        [Column("labs")]
        public string? Labs { get; set; }

        [Column("advance_directives")]
        public string? AdvanceDirectives { get; set; }

        [Column("consent_preferences")]
        public string? ConsentPreferences { get; set; }

        [ForeignKey("PatientId")]
        public virtual RegisteredPatient? Patient { get; set; }

        [Column("identity_date_of_birth")]
        public string? IdentityDateOfBirth { get; set; }

        [Column("identity_city")]
        public string? IdentityCity { get; set; }

        [Column("identity_country")]
        public string? IdentityCountry { get; set; }

        [Column("contact_address")]
        public string? ContactAddress { get; set; }

        [Column("emergency_name")]
        public string? EmergencyName { get; set; }

        [Column("emergency_phone")]
        public string? EmergencyPhone { get; set; }
        [Column("identity_first_name")]
        public string? IdentityFirstName { get; set; }

        [Column("identity_last_name")]
        public string? IdentityLastName { get; set; }

        [Column("contact_phone")]
        public string? ContactPhone { get; set; }

        [Column("contact_email")]
        public string? ContactEmail { get; set; }

        [Column("surgeries")]
        public string? Surgeries { get; set; }
    }
}
