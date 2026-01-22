using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ePatientApi.Models
{
    /// <summary>
    /// Registered patient profile stored in the database.
    /// </summary>
    [Table("registeredpatient")]
    public class RegisteredPatient
    {
        [Column("id")]
        public int Id { get; set; }

        [Key]
        [Column("birthnumber")]
        public string BirthNumber { get; set; } = null!;

        [Column("firstname")]
        public string FirstName { get; set; } = null!;

        [Column("lastname")]
        public string LastName { get; set; } = null!;

        [Column("username")]
        public string Username { get; set; } = null!;

        [Column("email")]
        public string Email { get; set; } = null!;

        [Column("phonenumber")]
        public string PhoneNumber { get; set; } = null!;

        [Column("hashedpassword")]
        public string HashedPassword { get; set; } = null!;

        [Column("role")]
        public string Role { get; set; } = null!;

        [Column("insurance")]
        public string Insurance { get; set; } = null!;

        [Column("resettoken")]
        public string? ResetToken { get; set; }

        [Column("resettokenexpiration")]
        public DateTime? ResetTokenExpiration { get; set; }

        [Column("google_accesstoken")]
        public string? GoogleAccessToken { get; set; }

        [Column("google_refreshtoken")]
        public string? GoogleRefreshToken { get; set; }

        [Column("google_tokenexpiry")]
        public DateTime? GoogleTokenExpiry { get; set; }

        [Column("gp_doctor_id")]
        public int? GpDoctorId { get; set; }

        [ForeignKey("GpDoctorId")]
        public RegisteredDoctor? GpDoctor { get; set; }

        public ICollection<AppointmentData> Appointments { get; set; } = new List<AppointmentData>();
    }
}
