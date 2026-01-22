namespace ePatientApi.Models
{
    public class Pharmacy
    {
        public int PharmacyId { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? BaseUrl { get; set; }
        public ICollection<AvailabilityCheck>? AvailabilityChecks { get; set; }
    }
}
