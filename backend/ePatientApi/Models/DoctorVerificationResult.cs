using System;

namespace ePatientApi.Models
{
    public class DoctorVerificationResult
    {
        public bool IsVerified { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? FullName { get; set; }
        public string? Specialization { get; set; }
        public string? SourceUrl { get; set; }
    }
}
