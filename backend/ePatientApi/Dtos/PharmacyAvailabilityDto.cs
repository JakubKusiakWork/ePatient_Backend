namespace ePatientApi.Dtos
{
    public class PharmacyAvailabilityDto
    {
        public string PharmacyId { get; set; } = string.Empty;
        public string Product { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal? Price { get; set; }
        public object? Details { get; set; }
        public string? Scraper { get; set; }
    }
}
