using System.ComponentModel.DataAnnotations;

namespace ePatientApi.Models
{
    public class DoctorRegistrationDto
    {
        [Required]
        public string DoctorCode { get; set; } = null!;
        [Required]
        public string DoctorPassword { get; set; } = null!;
        [Required]
        public string DoctorFirstName { get; set; } = null!;
        [Required]
        public string DoctorLastName { get; set; } = null!;
        public string? DoctorEmail { get; set; }
        public string? DoctorPhoneNumber { get; set; }
        public int SpecializationId { get; set; }
        public string? Role { get; set; }
        public bool SkipVerification { get; set; } = false;
        public bool IsVerified { get; set; } = false;
        public string? VerifiedFirstName { get; set; }
        public string? VerifiedLastName { get; set; }
        public string? VerifiedFullName { get; set; }
        public string? VerifiedSpecialization { get; set; }
        public string? VerifiedSourceUrl { get; set; }
    }
}
