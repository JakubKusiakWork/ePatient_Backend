using System.Collections.Generic;

namespace ePatientApi.Models
{
    /// <summary>
    /// DTO representing the Health Card payload exchanged with the frontend.
    /// Kept as a plain DTO (no EF annotations) to avoid immediate DB schema changes.
    /// </summary>
    public class HealthCard
    {
        public string IdentityFirstName { get; set; } = string.Empty;
        public string IdentityLastName { get; set; } = string.Empty;
        public string IdentityDateOfBirth { get; set; } = string.Empty;
        public string IdentityNationalId { get; set; } = string.Empty;
        public string IdentityCity { get; set; } = string.Empty;
        public string IdentityCountry { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string ContactAddress { get; set; } = string.Empty;
        public string EmergencyName { get; set; } = string.Empty;
        public string EmergencyPhone { get; set; } = string.Empty;

        public string BloodType { get; set; } = string.Empty;
        public System.Collections.Generic.List<System.Text.Json.JsonElement> Surgeries { get; set; } = new System.Collections.Generic.List<System.Text.Json.JsonElement>();

        public string? Labs { get; set; }

        public string AdvanceDirectives { get; set; } = string.Empty;
        public string ConsentPreferences { get; set; } = string.Empty;
        public bool ClearSurgeries { get; set; } = false;
    }
}
