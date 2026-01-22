using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ePatientApi.Models
{
    /// <summary>
    /// Represents a logged-in user session.
    /// </summary>
    [Table("loggeduser")]
    public class LoggedUser
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Column("Username")]
        public required string Username { get; set; }

        [Column("FirstName")]
        public required string FirstName { get; set; }

        [Column("LastName")]
        public required string LastName { get; set; }

        [Column("role")]
        public required string Role { get; set; }
    }
}