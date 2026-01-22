using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ePatientApi.Models
{
    [Table("doctoremail")]
    public class DoctorEmail
    {
        [Column("doctor_email")]
        public string Email { get; set; } = null!;
        [Key]
        [Column("doctor_id")]
        public int DoctorId { get; set; }
        [Column("state")]
        public bool State { get; set; } = false;
        [Column("registration_code")]
        public string? RegistrationCode { get; set; }
        [Column("hashedpassword")]
        public string? HashedPassword { get; set; }
        [Column("generated_at")]
        public DateTime? GeneratedAt { get; set; }
        [Column("expires_at")]
        public DateTime? ExpiresAt { get; set; }
    }
}