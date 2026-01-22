namespace ePatientApi.Models
{
    public class Product
    {
        public int ProductId { get; set; }
        public string ExternalCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public ICollection<AvailabilityCheck>? AvailabilityChecks { get; set; }
    }
}
