using System.ComponentModel.DataAnnotations.Schema;

namespace ePatientApi.Models
{
    /// <summary>
    /// Represents an authenticated doctor session record.
    /// </summary>
    [Table("loggeddoctor")]
    public class LoggedDoctor
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("doctorfirstname")]
        public string DoctorFirstName { get; set; } = null!;

        [Column("doctorlastname")]
        public string DoctorLastName { get; set; } = null!;

        [Column("doctorcode")]
        public string DoctorCode { get; set; } = null!;

        [Column("created_at")]
        public DateTime Created_at { get; set; }

        [Column("hashedpassword")]
        public string HashedPassword { get; set; } = null!;
    }
}