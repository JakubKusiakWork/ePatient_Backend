using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ePatientApi.DataAccess;
using System.Text.Json;

namespace ePatientApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PharmacyController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PharmacyController> _logger;

        public PharmacyController(AppDbContext context, ILogger<PharmacyController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchPharmacies(
            [FromQuery] string product,
            [FromQuery] string location,
            [FromQuery] int maxResults = 5)
        {
            if (string.IsNullOrWhiteSpace(product) || string.IsNullOrWhiteSpace(location))
            {
                return BadRequest(new { error = "Product and location are required" });
            }

            try
            {
                var searchTerm = product.ToLower().Trim();
                var productEntity = await _context.Products
                    .Where(p => p.Name.ToLower().Contains(searchTerm) || 
                               searchTerm.Contains(p.Name.ToLower()))
                    .FirstOrDefaultAsync();

                if (productEntity == null)
                {
                    var allProducts = await _context.Products.Take(100).ToListAsync();
                    var debugInfo = new { 
                        searchedFor = searchTerm,
                        availableProducts = allProducts.Select(p => p.Name).Take(10).ToList(),
                        message = "Product not found. Available products listed above."
                    };
                    return NotFound(debugInfo);
                }

                var recentChecks = await _context.AvailabilityChecks
                    .Include(ac => ac.Pharmacy)
                    .Where(ac => ac.ProductId == productEntity.ProductId)
                    .Where(ac => ac.Timestamp > DateTime.UtcNow.AddHours(-24))
                    .OrderByDescending(ac => ac.Timestamp)
                    .GroupBy(ac => ac.PharmacyId)
                    .Select(g => g.First())
                    .Take(maxResults)
                    .ToListAsync();

                var results = recentChecks.Select(ac => new
                {
                    pharmacyName = ac.Pharmacy?.Name ?? "Unknown Pharmacy",
                    address = ExtractAddress(ac.DetailsJson) ?? location,
                    stockStatus = MapStatus(ac.Status, ac.Price),
                    openingHours = ExtractOpeningHours(ac.DetailsJson),
                    distance = (double?)null
                }).ToList();

                return Ok(results);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to search pharmacies", details = ex.Message });
            }
        }

        [HttpGet("nearest")]
        public async Task<IActionResult> GetNearestPharmacies(
            [FromQuery] string location,
            [FromQuery] string? product = null,
            [FromQuery] int maxResults = 5)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return BadRequest(new { error = "Location is required" });
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(product))
                {
                    return await SearchPharmacies(product, location, maxResults);
                }

                var recentChecks = await _context.AvailabilityChecks
                    .Include(ac => ac.Pharmacy)
                    .Where(ac => ac.Timestamp > DateTime.UtcNow.AddHours(-24))
                    .OrderByDescending(ac => ac.Timestamp)
                    .GroupBy(ac => ac.PharmacyId)
                    .Select(g => g.First())
                    .Take(maxResults)
                    .ToListAsync();

                var results = recentChecks.Select(ac => new
                {
                    pharmacyName = ac.Pharmacy?.Name ?? "Unknown Pharmacy",
                    address = ExtractAddress(ac.DetailsJson) ?? location,
                    stockStatus = "Available",
                    openingHours = ExtractOpeningHours(ac.DetailsJson),
                    distance = (double?)null
                }).ToList();

                return Ok(results);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to get nearest pharmacies", details = ex.Message });
            }
        }

        [HttpGet("debug/products")]
        public async Task<IActionResult> GetProducts()
        {
            var products = await _context.Products
                .OrderByDescending(p => p.ProductId)
                .Take(20)
                .Select(p => new { p.ProductId, p.Name, p.ExternalCode })
                .ToListAsync();
            
            return Ok(products);
        }

        [HttpGet("debug/availability")]
        public async Task<IActionResult> GetRecentAvailability()
        {
            var checks = await _context.AvailabilityChecks
                .Include(ac => ac.Product)
                .Include(ac => ac.Pharmacy)
                .OrderByDescending(ac => ac.Timestamp)
                .Take(20)
                .Select(ac => new
                {
                    ac.AvailabilityCheckId,
                    ProductName = ac.Product!.Name,
                    PharmacyName = ac.Pharmacy!.Name,
                    ac.Status,
                    ac.Price,
                    ac.Timestamp
                })
                .ToListAsync();
            
            return Ok(checks);
        }

        private string MapStatus(string status, decimal? price)
        {
            return status.ToLower() switch
            {
                "ok" => price.HasValue ? $"na sklade - {price:F2}€" : "na sklade",
                "not_found" => "nie je na sklade",
                "captcha" => "overenie potrebné",
                _ => "neznámy stav"
            };
        }

        private string? ExtractAddress(string? detailsJson)
        {
            if (string.IsNullOrWhiteSpace(detailsJson))
                return null;

            try
            {
                var doc = JsonDocument.Parse(detailsJson);
                if (doc.RootElement.TryGetProperty("address", out var addressProp))
                {
                    return addressProp.GetString();
                }
            }
            catch {}
            return null;
        }

        private object? ExtractOpeningHours(string? detailsJson)
        {
            if (string.IsNullOrWhiteSpace(detailsJson))
                return null;

            try
            {
                var doc = JsonDocument.Parse(detailsJson);
                if (doc.RootElement.TryGetProperty("openingHours", out var hoursProp))
                {
                    return new
                    {
                        today = hoursProp.GetProperty("today").GetString(),
                        tomorrow = hoursProp.GetProperty("tomorrow").GetString()
                    };
                }
            }
            catch
            {
            }

            return null;
        }

        [HttpPost("check-medications")]
        public async Task<IActionResult> CheckMedicationsAvailability([FromBody] CheckMedicationsRequest request)
        {
            if (request.Medications == null || !request.Medications.Any())
            {
                return BadRequest(new { error = "Medications list is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Location))
            {
                return BadRequest(new { error = "Location is required" });
            }

            try
            {
                var shouldRefresh = request.RefreshData ?? true;
                
                var results = new List<object>();
                
                if (shouldRefresh)
                {
                    try
                    {
                        using var httpClient = new HttpClient();
                        httpClient.Timeout = TimeSpan.FromSeconds(30);
                        var pharmacyCheckerUrl = Environment.GetEnvironmentVariable("PHARMACY_CHECKER_URL") ?? "http://localhost:5003";
                        
                        foreach (var medication in request.Medications)
                        {
                            try
                            {
                                var scrapeRequest = new { product = medication.ToLowerInvariant(), location = request.Location };
                                var response = await httpClient.PostAsJsonAsync($"{pharmacyCheckerUrl}/api/scrape", scrapeRequest);
                                
                                if (response.IsSuccessStatusCode)
                                {
                                    _logger.LogInformation("Successfully scraped live data for {Medication}", medication);
                                    
                                    await Task.Delay(500);
                                }
                                else
                                {
                                    _logger.LogWarning("PharmacyChecker returned {Status} for {Medication}", response.StatusCode, medication);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to scrape {Medication}", medication);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to connect to PharmacyChecker service");
                    }
                }

                foreach (var medicationName in request.Medications)
                {
                    var searchTerm = medicationName.ToLower().Trim();
                    var productEntity = await _context.Products
                        .Where(p => p.Name.ToLower().Contains(searchTerm) || 
                                   searchTerm.Contains(p.Name.ToLower()))
                        .FirstOrDefaultAsync();

                    if (productEntity == null)
                    {
                        _logger.LogWarning("Product not found in database for search term: {SearchTerm}", searchTerm);
                        results.Add(new
                        {
                            medication = medicationName,
                            available = false,
                            pharmacies = new List<object>(),
                            searchedFor = searchTerm,
                            message = "Product not found in database. Scraping may have failed or product name mismatch."
                        });
                        continue;
                    }
                    
                    _logger.LogInformation("Found product in database: {ProductName} (ID: {ProductId})", productEntity.Name, productEntity.ProductId);

                    var allRecentChecks = await _context.AvailabilityChecks
                        .Include(ac => ac.Pharmacy)
                        .Where(ac => ac.ProductId == productEntity.ProductId)
                        .Where(ac => ac.Timestamp > DateTime.UtcNow.AddMinutes(-2))
                        .OrderByDescending(ac => ac.Timestamp)
                        .ToListAsync();

                    var recentChecks = allRecentChecks
                        .GroupBy(ac => ac.PharmacyId)
                        .Select(g => g.OrderByDescending(ac => ac.Status == "ok" ? 1 : 0).ThenByDescending(ac => ac.Timestamp).First())
                        .Take(5)
                        .ToList();

                    _logger.LogInformation("Found {Count} recent checks for {Product}, {OkCount} with status 'ok'", 
                        recentChecks.Count, medicationName, recentChecks.Count(ac => ac.Status == "ok"));
                    
                    var pharmacies = recentChecks
                        .Where(ac => ac.Status == "ok")
                        .Select(ac => new
                        {
                            pharmacyName = ac.Pharmacy?.Name ?? "Unknown Pharmacy",
                            address = ExtractAddress(ac.DetailsJson) ?? request.Location,
                            stockStatus = MapStatus(ac.Status, ac.Price),
                            price = ac.Price,
                            lastChecked = ac.Timestamp
                        }).ToList();
                    
                    if (pharmacies.Count == 0 && recentChecks.Count > 0)
                    {
                        _logger.LogWarning("Scraping completed but no pharmacies have status 'ok'. Statuses: {Statuses}", 
                            string.Join(", ", recentChecks.Select(r => $"{r.Pharmacy?.Name}: {r.Status}")));
                    }
                    
                    var hasErrors = recentChecks.Any(ac => ac.Status == "error");
                    var debugMessage = hasErrors 
                        ? $"PharmacyChecker encountered errors. Last attempt: {recentChecks.First().Timestamp:yyyy-MM-dd HH:mm}"
                        : null;

                    results.Add(new
                    {
                        medication = medicationName,
                        available = pharmacies.Any(),
                        pharmacies = pharmacies,
                        debug = debugMessage,
                        productFound = productEntity.Name,
                        totalChecks = recentChecks.Count
                    });
                }

                return Ok(results);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to check medications", details = ex.Message });
            }
        }

        [HttpPost("availability")]
        public async Task<IActionResult> SaveAvailability([FromBody] AvailabilityRequest request)
        {
            try
            {
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Name.ToLower() == request.ProductName.ToLower());

                if (product == null)
                {
                    product = new Models.Product
                    {
                        Name = request.ProductName.ToLowerInvariant(),
                        ExternalCode = request.ProductName
                    };
                    _context.Products.Add(product);
                    await _context.SaveChangesAsync();
                }

                var pharmacy = await _context.Pharmacies
                    .FirstOrDefaultAsync(p => p.ExternalId == request.PharmacyExternalId);

                if (pharmacy == null)
                {
                    pharmacy = new Models.Pharmacy
                    {
                        ExternalId = request.PharmacyExternalId,
                        Name = request.PharmacyExternalId
                    };
                    _context.Pharmacies.Add(pharmacy);
                    await _context.SaveChangesAsync();
                }

                var availabilityCheck = new Models.AvailabilityCheck
                {
                    PharmacyId = pharmacy.PharmacyId,
                    ProductId = product.ProductId,
                    Timestamp = DateTime.UtcNow,
                    Status = request.Status,
                    Price = request.Price,
                    DetailsJson = request.Details != null ? JsonSerializer.Serialize(request.Details) : null
                };

                _context.AvailabilityChecks.Add(availabilityCheck);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Availability data saved" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to save availability", details = ex.Message });
            }
        }
    }

    public class AvailabilityRequest
    {
        public required string ProductName { get; set; }
        public required string PharmacyExternalId { get; set; }
        public required string Status { get; set; }
        public decimal? Price { get; set; }
        public Dictionary<string, object>? Details { get; set; }
    }

    public class CheckMedicationsRequest
    {
        public required List<string> Medications { get; set; }
        public required string Location { get; set; }
        public bool? RefreshData { get; set; } = true;
    }
}
