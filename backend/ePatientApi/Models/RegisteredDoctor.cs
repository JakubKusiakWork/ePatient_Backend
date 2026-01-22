using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ePatientApi.Models
{
    /// <summary>
    /// Registered doctor profile persisted in the database.
    /// </summary>
    [Table("registereddoctor")]
    public class RegisteredDoctor
    {
        [Key]
        [Column("id")]
        public int DoctorId { get; set; }

        [Column("firstname")]
        public string DoctorFirstName { get; set; } = null!;

        [Column("lastname")]
        public string DoctorLastName { get; set; } = null!;

        [Column("doctorcode")]
        public string DoctorCode { get; set; } = null!;

        [Column("doctor_email")]
        public string DoctorEmail { get; set; } = null!;

        [Column("phone_number")]
        public string DoctorPhoneNumber { get; set; } = null!;

        [Column("doctor_password")]
        public string DoctorHashedPassword { get; set; } = null!;

        [Column("role")]
        public string Role { get; set; } = "Clinician";

        [Column("is_verified")]
        public bool IsVerified { get; set; } = false;

        [Column("verified_firstname")]
        public string? VerifiedFirstName { get; set; }

        [Column("verified_lastname")]
        public string? VerifiedLastName { get; set; }

        [Column("verified_fullname")]
        public string? VerifiedFullName { get; set; }

        [Column("verified_specialization")]
        public string? VerifiedSpecialization { get; set; }

        [Column("verified_source_url")]
        public string? VerifiedSourceUrl { get; set; }
    }
}