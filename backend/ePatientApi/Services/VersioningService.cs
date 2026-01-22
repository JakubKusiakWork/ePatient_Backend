using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ePatientApi.DataAccess;
using ePatientApi.Interfaces;
using ePatientApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ePatientApi.Services
{
    public class VersioningService : IVersioningService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<VersioningService> _logger;

        public VersioningService(AppDbContext context, ILogger<VersioningService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HealthCardVersion> CreateVersionAsync(HealthCardEntity currentEntity, string? modifiedBy = null)
        {
            if (currentEntity == null) throw new ArgumentNullException(nameof(currentEntity));

            RegisteredPatient? patient = null;
            try
            {
                if (currentEntity.PatientId != null)
                {
                    patient = await _context.RegisteredPatients.FirstOrDefaultAsync(p => p.Id == currentEntity.PatientId);
                }
                else if (!string.IsNullOrWhiteSpace(currentEntity.PatientBirthNumber))
                {
                    patient = await _context.RegisteredPatients.FirstOrDefaultAsync(p => p.BirthNumber == currentEntity.PatientBirthNumber);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load patient while creating version for healthcard {Id}", currentEntity.HealthCardId);
            }

            var snapshotObj = new
            {
                HealthCard = new
                {
                    currentEntity.HealthCardId,
                    currentEntity.PatientBirthNumber,
                    currentEntity.PatientId,
                    currentEntity.BloodType,
                    currentEntity.Labs,
                    currentEntity.AdvanceDirectives,
                    currentEntity.ConsentPreferences,
                    currentEntity.IdentityDateOfBirth,
                    currentEntity.IdentityCity,
                    currentEntity.IdentityCountry,
                    currentEntity.ContactAddress,
                    currentEntity.EmergencyName,
                    currentEntity.EmergencyPhone,
                    currentEntity.IdentityFirstName,
                    currentEntity.IdentityLastName,
                    currentEntity.ContactPhone,
                    currentEntity.ContactEmail,
                    currentEntity.Surgeries
                },
                Patient = patient == null ? null : new
                {
                    patient.Id,
                    patient.BirthNumber,
                    patient.FirstName,
                    patient.LastName,
                    patient.Email,
                    patient.PhoneNumber,
                    patient.Role,
                    patient.Insurance
                }
            };

            var snapshotJson = JsonSerializer.Serialize(snapshotObj);
            var last = await _context.HealthCardVersions
                .Where(v => v.HealthCardId == currentEntity.HealthCardId)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync();
            var next = (last?.VersionNumber ?? 0) + 1;
            string? changeSummary = null;
            if (last != null)
            {
                try
                {
                    var diffs = await CompareJsonStringsAsync(last.DataSnapshot, snapshotJson);
                    changeSummary = string.Join("; ", diffs.Take(10));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to compute change summary for healthcard {Id}", currentEntity.HealthCardId);
                }
            }
            var version = new HealthCardVersion
            {
                Id = Guid.NewGuid(),
                HealthCardId = currentEntity.HealthCardId,
                VersionNumber = next,
                DataSnapshot = snapshotJson,
                ModifiedBy = modifiedBy,
                ModifiedAt = DateTime.UtcNow,
                ChangeSummary = changeSummary
            };
            _context.HealthCardVersions.Add(version);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created healthcard version {Version} for healthcard {Id} by {User}", version.VersionNumber, version.HealthCardId, modifiedBy);
            return version;
        }

        public async Task<IEnumerable<HealthCardVersion>> GetVersionsAsync(int healthCardId)
        {
            return await _context.HealthCardVersions
                .Where(v => v.HealthCardId == healthCardId)
                .OrderByDescending(v => v.VersionNumber)
                .ToListAsync();
        }

        public async Task<HealthCardVersion?> GetVersionByIdAsync(Guid versionId)
        {
            return await _context.HealthCardVersions.FirstOrDefaultAsync(v => v.Id == versionId);
        }

        public async Task<IEnumerable<string>> CompareVersionsAsync(Guid versionAId, Guid versionBId)
        {
            var a = await GetVersionByIdAsync(versionAId);
            var b = await GetVersionByIdAsync(versionBId);
            if (a == null || b == null) return Array.Empty<string>();
            var diffs = await CompareJsonStringsAsync(a.DataSnapshot, b.DataSnapshot);
            return diffs;
        }

        private Task<List<string>> CompareJsonStringsAsync(string aJson, string bJson)
        {
            var result = new List<string>();
            try
            {
                using var aDoc = JsonDocument.Parse(aJson);
                using var bDoc = JsonDocument.Parse(bJson);
                CompareElements(string.Empty, aDoc.RootElement, bDoc.RootElement, result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "JSON compare failed");
            }
            return Task.FromResult(result);
        }

        private void CompareElements(string path, JsonElement a, JsonElement b, List<string> diffs)
        {
            if (a.ValueKind != b.ValueKind)
            {
                diffs.Add($"{path}: kind {a.ValueKind} -> {b.ValueKind}");
                return;
            }

            switch (a.ValueKind)
            {
                case JsonValueKind.Object:
                    var aProps = a.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
                    var bProps = b.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
                    var keys = new HashSet<string>(aProps.Keys.Concat(bProps.Keys));
                    foreach (var k in keys)
                    {
                        var childPath = string.IsNullOrEmpty(path) ? k : path + "." + k;
                        if (!aProps.ContainsKey(k)) { diffs.Add($"{childPath}: added in B"); continue; }
                        if (!bProps.ContainsKey(k)) { diffs.Add($"{childPath}: removed in B"); continue; }
                        CompareElements(childPath, aProps[k], bProps[k], diffs);
                    }
                    break;
                case JsonValueKind.Array:
                    var aArr = a.EnumerateArray().ToArray();
                    var bArr = b.EnumerateArray().ToArray();
                    var len = Math.Max(aArr.Length, bArr.Length);
                    for (int i = 0; i < len; i++)
                    {
                        var childPath = path + $"[{i}]";
                        if (i >= aArr.Length) { diffs.Add($"{childPath}: added in B"); continue; }
                        if (i >= bArr.Length) { diffs.Add($"{childPath}: removed in B"); continue; }
                        CompareElements(childPath, aArr[i], bArr[i], diffs);
                    }
                    break;
                default:
                    var aStr = a.ToString();
                    var bStr = b.ToString();
                    if (aStr != bStr) diffs.Add($"{path}: '{aStr}' -> '{bStr}'");
                    break;
            }
        }

        public async Task RestoreVersionAsync(Guid versionId, string? restoredBy = null)
        {
            var version = await _context.HealthCardVersions.FirstOrDefaultAsync(v => v.Id == versionId);
            if (version == null) throw new KeyNotFoundException("Version not found.");

            var current = await _context.HealthCards.FirstOrDefaultAsync(h => h.HealthCardId == version.HealthCardId);
            if (current == null) throw new KeyNotFoundException("HealthCard entity not found for version.");

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                await CreateVersionAsync(current, restoredBy + "-pre-restore");

                using var doc = JsonDocument.Parse(version.DataSnapshot);
                var root = doc.RootElement;
                if (root.TryGetProperty("HealthCard", out var hc))
                {
                    current.BloodType = hc.GetPropertyOrNullString("BloodType") ?? current.BloodType;
                    current.Labs = hc.GetPropertyOrNullString("Labs") ?? current.Labs;
                    current.AdvanceDirectives = hc.GetPropertyOrNullString("AdvanceDirectives") ?? current.AdvanceDirectives;
                    current.ConsentPreferences = hc.GetPropertyOrNullString("ConsentPreferences") ?? current.ConsentPreferences;
                    current.IdentityDateOfBirth = hc.GetPropertyOrNullString("IdentityDateOfBirth") ?? current.IdentityDateOfBirth;
                    current.IdentityCity = hc.GetPropertyOrNullString("IdentityCity") ?? current.IdentityCity;
                    current.IdentityCountry = hc.GetPropertyOrNullString("IdentityCountry") ?? current.IdentityCountry;
                    current.ContactAddress = hc.GetPropertyOrNullString("ContactAddress") ?? current.ContactAddress;
                    current.EmergencyName = hc.GetPropertyOrNullString("EmergencyName") ?? current.EmergencyName;
                    current.EmergencyPhone = hc.GetPropertyOrNullString("EmergencyPhone") ?? current.EmergencyPhone;
                    current.IdentityFirstName = hc.GetPropertyOrNullString("IdentityFirstName") ?? current.IdentityFirstName;
                    current.IdentityLastName = hc.GetPropertyOrNullString("IdentityLastName") ?? current.IdentityLastName;
                    current.ContactPhone = hc.GetPropertyOrNullString("ContactPhone") ?? current.ContactPhone;
                    current.ContactEmail = hc.GetPropertyOrNullString("ContactEmail") ?? current.ContactEmail;
                    current.Surgeries = hc.GetPropertyOrNullString("Surgeries") ?? current.Surgeries;
                }

                if (root.TryGetProperty("Patient", out var p) && p.ValueKind != JsonValueKind.Null)
                {
                    var patientBirth = p.GetPropertyOrNullString("BirthNumber");
                    RegisteredPatient? patient = null;
                    if (!string.IsNullOrWhiteSpace(patientBirth))
                    {
                        patient = await _context.RegisteredPatients.FirstOrDefaultAsync(rp => rp.BirthNumber == patientBirth);
                    }
                    else if (current.PatientId != null)
                    {
                        patient = await _context.RegisteredPatients.FirstOrDefaultAsync(rp => rp.Id == current.PatientId);
                    }

                    if (patient != null)
                    {
                        var updated = false;
                        var first = p.GetPropertyOrNullString("FirstName");
                        var last = p.GetPropertyOrNullString("LastName");
                        var email = p.GetPropertyOrNullString("Email");
                        var phone = p.GetPropertyOrNullString("PhoneNumber");
                        if (!string.IsNullOrWhiteSpace(first) && patient.FirstName != first) { patient.FirstName = first; updated = true; }
                        if (!string.IsNullOrWhiteSpace(last) && patient.LastName != last) { patient.LastName = last; updated = true; }
                        if (!string.IsNullOrWhiteSpace(email) && patient.Email != email) { patient.Email = email; updated = true; }
                        if (!string.IsNullOrWhiteSpace(phone) && patient.PhoneNumber != phone) { patient.PhoneNumber = phone; updated = true; }
                        if (updated)
                        {
                            _context.RegisteredPatients.Update(patient);
                            _context.Entry(patient).State = EntityState.Modified;
                        }
                    }
                }

                _context.HealthCards.Update(current);
                _context.Entry(current).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                await CreateVersionAsync(current, restoredBy + "-restored");
                await tx.CommitAsync();
                _logger.LogInformation("Restored healthcard {Id} to version {V} by {User}", current.HealthCardId, version.VersionNumber, restoredBy);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
    }

    internal static class JsonExtensions
    {
        public static string? GetPropertyOrNullString(this JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var prop)) return null;
            if (prop.ValueKind == JsonValueKind.Null) return null;
            return prop.ToString();
        }
    }
}
