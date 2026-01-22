using System.Text.Json;

namespace ePatientApi.Models
{
    public class AvailabilityCheck
    {
        public int AvailabilityCheckId { get; set; }
        public int PharmacyId { get; set; }
        public Pharmacy? Pharmacy { get; set; }
        public int ProductId { get; set; }
        public Product? Product { get; set; }
        public DateTime Timestamp { get; set; }
        public string Status { get; set; } = "error";
        public decimal? Price { get; set; }
        public string? DetailsJson { get; set; }
        public string? ScraperVersion { get; set; }
    }
}
