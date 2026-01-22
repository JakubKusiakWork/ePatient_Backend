using Microsoft.AspNetCore.Mvc;
using ePatientApi.DataAccess;
using ePatientApi.Dtos;
using ePatientApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ePatientApi.Controllers.Internal
{
    [ApiController]
    [Route("internal/[controller]")]
    public class AvailabilityController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AvailabilityController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Post(PharmacyAvailabilityDto dto)
        {
            if (string.IsNullOrEmpty(dto.PharmacyId) || string.IsNullOrEmpty(dto.Product))
                return BadRequest("pharmacyId and product are required");

            var pharmacy = await _context.Pharmacies.FirstOrDefaultAsync(p => p.ExternalId == dto.PharmacyId);
            if (pharmacy == null)
            {
                pharmacy = new Pharmacy { ExternalId = dto.PharmacyId, Name = dto.PharmacyId };
                _context.Pharmacies.Add(pharmacy);
                await _context.SaveChangesAsync();
            }

            var product = await _context.Products.FirstOrDefaultAsync(p => p.Name == dto.Product || p.ExternalCode == dto.Product);
            if (product == null)
            {
                product = new Product { ExternalCode = dto.Product, Name = dto.Product };
                _context.Products.Add(product);
                await _context.SaveChangesAsync();
            }

            var check = new AvailabilityCheck
            {
                PharmacyId = pharmacy.PharmacyId,
                ProductId = product.ProductId,
                Timestamp = dto.Timestamp == default ? DateTime.UtcNow : dto.Timestamp,
                Status = dto.Status,
                Price = dto.Price,
                DetailsJson = dto.Details == null ? null : JsonSerializer.Serialize(dto.Details),
                ScraperVersion = dto.Scraper
            };

            _context.AvailabilityChecks.Add(check);
            await _context.SaveChangesAsync();

            return Ok(new { ok = true, id = check.AvailabilityCheckId });
        }
    }
}
