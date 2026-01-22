using Microsoft.EntityFrameworkCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using QRCoder;
using System.IO;
using System.Text;
using ePatientApi.DataAccess;
using ePatientApi.Interfaces;
using ePatientApi.Models;

namespace ePatientApi.Services
{
    /// <summary>
    /// Service for managing health card data.
    /// </summary>
    public class HealthcardService : IHealthcardService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<HealthcardService> _logger;
        private readonly ePatientApi.Interfaces.IVersioningService? _versioningService;
        private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor? _httpContextAccessor;

        public HealthcardService(AppDbContext context, ILogger<HealthcardService> logger, ePatientApi.Interfaces.IVersioningService? versioningService = null, Microsoft.AspNetCore.Http.IHttpContextAccessor? httpContextAccessor = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _versioningService = versioningService;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<HealthCard> GetByPatientIdAsync(string patientId)
        {
            var patient = await _context.RegisteredPatients.FirstOrDefaultAsync(p => p.BirthNumber == patientId || p.Username == patientId);
            if (patient == null) throw new KeyNotFoundException("Patient not found.");

            var hc = await _context.HealthCards
                .FirstOrDefaultAsync(h => h.PatientBirthNumber == patient.BirthNumber);

            if (hc == null)
            {
                return new HealthCard
                {
                    IdentityFirstName = patient.FirstName,
                    IdentityLastName = patient.LastName,
                    IdentityNationalId = patient.BirthNumber
                };
            }

            var surgeries = new List<System.Text.Json.JsonElement>();
            try
            {
                if (!string.IsNullOrWhiteSpace(hc.Surgeries))
                    surgeries = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(hc.Surgeries) ?? new List<System.Text.Json.JsonElement>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize surgeries json column for patient {Birth}", patient.BirthNumber);
            }

            var dto = new HealthCard
            {
                IdentityFirstName = hc.IdentityFirstName ?? patient.FirstName,
                IdentityLastName = hc.IdentityLastName ?? patient.LastName,
                IdentityNationalId = patient.BirthNumber,
                IdentityDateOfBirth = hc.IdentityDateOfBirth ?? string.Empty,
                IdentityCity = hc.IdentityCity ?? string.Empty,
                IdentityCountry = hc.IdentityCountry ?? string.Empty,
                ContactPhone = hc.ContactPhone ?? patient.PhoneNumber ?? string.Empty,
                ContactEmail = hc.ContactEmail ?? patient.Email ?? string.Empty,
                ContactAddress = hc.ContactAddress ?? string.Empty,
                EmergencyName = hc.EmergencyName ?? string.Empty,
                EmergencyPhone = hc.EmergencyPhone ?? string.Empty,
                BloodType = hc.BloodType ?? string.Empty,
                Labs = hc.Labs,
                AdvanceDirectives = hc.AdvanceDirectives ?? string.Empty,
                ConsentPreferences = hc.ConsentPreferences ?? string.Empty,

                Surgeries = surgeries
            };
            return dto;
        }

        public async Task<HealthCard> UpsertAsync(HealthCard card)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            try
            {
                var incoming = System.Text.Json.JsonSerializer.Serialize(card);
                _logger.LogDebug("UpsertAsync received card payload: {Card}", incoming);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to serialize incoming card payload");
            }
            var birth = card.IdentityNationalId?.Trim();
            if (string.IsNullOrWhiteSpace(birth)) throw new ArgumentException("Birth number is required to upsert health card.");

            _logger.LogDebug("UpsertAsync starting for birth={Birth}", birth);
            var patient = await _context.RegisteredPatients.FirstOrDefaultAsync(p => p.BirthNumber == birth);
            if (patient == null) throw new KeyNotFoundException("Patient not found.");

            _logger.LogDebug("Found patient for birth={Birth} -> Id={Id}", birth, patient.Id);

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var existing = await _context.HealthCards
                    .FirstOrDefaultAsync(h => h.PatientBirthNumber == birth);

                if (existing == null)
                {
                    _logger.LogDebug("No existing HealthCard found for birth={Birth}, creating new entity", birth);
                    existing = new HealthCardEntity { PatientBirthNumber = birth, PatientId = patient.Id };
                    _context.HealthCards.Add(existing);
                    await _context.SaveChangesAsync();
                    _logger.LogDebug("Created HealthCard entity with id={HealthCardId} for patient id={PatientId}", existing.HealthCardId, patient.Id);
                }

                if (existing.PatientId == null)
                {
                    existing.PatientId = patient.Id;
                    _logger.LogDebug("Set existing.HealthCard.PatientId={PatientId} for HealthCard id={HealthCardId}", patient.Id, existing.HealthCardId);
                }
                existing.BloodType = card.BloodType;
                existing.Labs = card.Labs;
                existing.AdvanceDirectives = card.AdvanceDirectives;
                existing.ConsentPreferences = card.ConsentPreferences;

                if (!string.IsNullOrWhiteSpace(card.IdentityFirstName)) existing.IdentityFirstName = card.IdentityFirstName;
                if (!string.IsNullOrWhiteSpace(card.IdentityLastName)) existing.IdentityLastName = card.IdentityLastName;
                if (!string.IsNullOrWhiteSpace(card.IdentityDateOfBirth)) existing.IdentityDateOfBirth = card.IdentityDateOfBirth;
                if (!string.IsNullOrWhiteSpace(card.IdentityCity)) existing.IdentityCity = card.IdentityCity;
                if (!string.IsNullOrWhiteSpace(card.IdentityCountry)) existing.IdentityCountry = card.IdentityCountry;

                if (!string.IsNullOrWhiteSpace(card.ContactPhone)) existing.ContactPhone = card.ContactPhone;
                if (!string.IsNullOrWhiteSpace(card.ContactEmail)) existing.ContactEmail = card.ContactEmail;
                if (!string.IsNullOrWhiteSpace(card.ContactAddress)) existing.ContactAddress = card.ContactAddress;
                if (!string.IsNullOrWhiteSpace(card.EmergencyName)) existing.EmergencyName = card.EmergencyName;
                if (!string.IsNullOrWhiteSpace(card.EmergencyPhone)) existing.EmergencyPhone = card.EmergencyPhone;

                try
                {
                    existing.Surgeries = card.Surgeries != null ? System.Text.Json.JsonSerializer.Serialize(card.Surgeries) : existing.Surgeries;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to serialize surgeries list for patient {Birth}", birth);
                }

                    try
                    {
                        if (card.ClearSurgeries)
                        {
                            existing.Surgeries = System.Text.Json.JsonSerializer.Serialize(new List<object>());
                        }
                        else if (card.Surgeries != null)
                        {
                            existing.Surgeries = System.Text.Json.JsonSerializer.Serialize(card.Surgeries);
                        }
                    }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to serialize healthcard lists to JSON for patient {Birth}", birth);
                }
                    try
                    {
                        var updated = false;
                        if (!string.IsNullOrWhiteSpace(card.IdentityFirstName) && card.IdentityFirstName != patient.FirstName)
                        {
                            _logger.LogDebug("Updating patient.FirstName from '{Old}' to '{New}'", patient.FirstName, card.IdentityFirstName);
                            patient.FirstName = card.IdentityFirstName;
                            updated = true;
                        }
                        if (!string.IsNullOrWhiteSpace(card.IdentityLastName) && card.IdentityLastName != patient.LastName)
                        {
                            _logger.LogDebug("Updating patient.LastName from '{Old}' to '{New}'", patient.LastName, card.IdentityLastName);
                            patient.LastName = card.IdentityLastName;
                            updated = true;
                        }

                        if (!string.IsNullOrWhiteSpace(card.ContactEmail) && card.ContactEmail != patient.Email)
                        {
                            _logger.LogDebug("Updating patient.Email from '{Old}' to '{New}'", patient.Email, card.ContactEmail);
                            patient.Email = card.ContactEmail;
                            updated = true;
                        }
                        if (!string.IsNullOrWhiteSpace(card.ContactPhone) && card.ContactPhone != patient.PhoneNumber)
                        {
                            _logger.LogDebug("Updating patient.PhoneNumber from '{Old}' to '{New}'", patient.PhoneNumber, card.ContactPhone);
                            patient.PhoneNumber = card.ContactPhone;
                            updated = true;
                        }

                        if (updated)
                        {
                            _logger.LogDebug("Marking RegisteredPatient id={Id} as modified and saving", patient.Id);
                            _context.RegisteredPatients.Update(patient);
                            _context.Entry(patient).State = EntityState.Modified;
                        }
                    }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to apply identity/contact updates to RegisteredPatient for birth={Birth}", birth);
                }

                _logger.LogDebug("Saving changes for HealthCard id={HealthCardId} (patient birth={Birth})", existing.HealthCardId, birth);
                await _context.SaveChangesAsync();

                try
                {
                    var editor = _httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "system";
                    if (_versioningService != null)
                    {
                        await _versioningService.CreateVersionAsync(existing, editor);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create healthcard version for id={Id}", existing.HealthCardId);
                }

                await tx.CommitAsync();
                _logger.LogDebug("UpsertAsync completed for HealthCard id={HealthCardId}", existing.HealthCardId);
                var savedDtoTemp = await GetByPatientIdAsync(birth);
                HealthCard savedDto = savedDtoTemp ?? new HealthCard();
                if (savedDto != null)
                {
                    if (!string.IsNullOrWhiteSpace(card.IdentityFirstName)) savedDto.IdentityFirstName = card.IdentityFirstName;
                    if (!string.IsNullOrWhiteSpace(card.IdentityLastName)) savedDto.IdentityLastName = card.IdentityLastName;
                    if (!string.IsNullOrWhiteSpace(card.IdentityDateOfBirth)) savedDto.IdentityDateOfBirth = card.IdentityDateOfBirth;
                    if (!string.IsNullOrWhiteSpace(card.IdentityNationalId)) savedDto.IdentityNationalId = card.IdentityNationalId;
                    if (!string.IsNullOrWhiteSpace(card.IdentityCity)) savedDto.IdentityCity = card.IdentityCity;
                    if (!string.IsNullOrWhiteSpace(card.IdentityCountry)) savedDto.IdentityCountry = card.IdentityCountry;
                    if (!string.IsNullOrWhiteSpace(card.ContactPhone)) savedDto.ContactPhone = card.ContactPhone;
                    if (!string.IsNullOrWhiteSpace(card.ContactEmail)) savedDto.ContactEmail = card.ContactEmail;
                    if (!string.IsNullOrWhiteSpace(card.ContactAddress)) savedDto.ContactAddress = card.ContactAddress;
                    if (!string.IsNullOrWhiteSpace(card.EmergencyName)) savedDto.EmergencyName = card.EmergencyName;
                    if (!string.IsNullOrWhiteSpace(card.EmergencyPhone)) savedDto.EmergencyPhone = card.EmergencyPhone;
                }

                try
                {
                    var refreshedPatient = await _context.RegisteredPatients.FirstOrDefaultAsync(p => p.BirthNumber == birth);
                    if (refreshedPatient != null)
                    {
                        _logger.LogDebug("Persisted patient after save: Id={Id}, FirstName={First}, LastName={Last}, Email={Email}, Phone={Phone}", refreshedPatient.Id, refreshedPatient.FirstName, refreshedPatient.LastName, refreshedPatient.Email, refreshedPatient.PhoneNumber);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to reload patient after save for birth={Birth}", birth);
                }

                return savedDto!;
            }
            catch
            {
                await tx.RollbackAsync();
                _logger.LogError(new Exception("UpsertAsync failed for birth=" + birth), "UpsertAsync failed for birth={Birth}", birth);
                throw;
            }
        }

        public async Task<byte[]> GeneratePdfAsync(string patientId)
        {
            if (string.IsNullOrWhiteSpace(patientId)) throw new ArgumentException("patientId is required");
            var dto = await GetByPatientIdAsync(patientId);
            using var ms = new MemoryStream();
            var doc = new PdfDocument();
            var page = doc.AddPage();
            page.Size = PdfSharpCore.PageSize.A4;
            var gfx = XGraphics.FromPdfPage(page);
            
            var fontTitle = new XFont("Arial", 24, XFontStyle.Bold);
            var fontSection = new XFont("Arial", 14, XFontStyle.Bold);
            var fontLabel = new XFont("Arial", 10, XFontStyle.Bold);
            var fontValue = new XFont("Arial", 10, XFontStyle.Regular);

            var primaryColor = XBrushes.Black;
            var secondaryColor = new XSolidBrush(XColor.FromArgb(100, 100, 100));
            var accentColor = new XSolidBrush(XColor.FromArgb(0, 116, 116));

            double y = 50;
            double leftMargin = 50;
            double rightMargin = page.Width - 50;
            double contentWidth = rightMargin - leftMargin;
            
            gfx.DrawString("Health Card", fontTitle, primaryColor, new XRect(leftMargin, y, contentWidth, 40), XStringFormats.TopLeft);
            y += 50;

            void DrawSection(string title)
            {
                y += 15;
                gfx.DrawString(title, fontSection, accentColor, new XRect(leftMargin, y, contentWidth, 25), XStringFormats.TopLeft);
                y += 25;
                gfx.DrawLine(new XPen(secondaryColor, 0.5), leftMargin, y, rightMargin, y);
                y += 10;
            }

            void DrawField(string label, string? value, bool isLeft = true)
            {
                double xPos = isLeft ? leftMargin : leftMargin + (contentWidth / 2) + 10;
                double fieldWidth = (contentWidth / 2) - 10;
                
                gfx.DrawString(label, fontLabel, secondaryColor, new XRect(xPos, y, fieldWidth, 15), XStringFormats.TopLeft);
                gfx.DrawString(value ?? "—", fontValue, primaryColor, new XRect(xPos, y + 15, fieldWidth, 15), XStringFormats.TopLeft);
            }

            void DrawFullField(string label, string? value)
            {
                gfx.DrawString(label, fontLabel, secondaryColor, new XRect(leftMargin, y, contentWidth, 15), XStringFormats.TopLeft);
                y += 15;
                gfx.DrawString(value ?? "—", fontValue, primaryColor, new XRect(leftMargin, y, contentWidth, 15), XStringFormats.TopLeft);
                y += 25;
            }

            DrawSection("Patient Identity");
            
            var fullName = !string.IsNullOrWhiteSpace(dto.IdentityFirstName) && !string.IsNullOrWhiteSpace(dto.IdentityLastName) 
                ? $"{dto.IdentityFirstName} {dto.IdentityLastName}" 
                : (dto.IdentityFirstName ?? dto.IdentityLastName ?? "");
            
            DrawField("Name", fullName, true);
            DrawField("Date of Birth", dto.IdentityDateOfBirth, false);
            y += 35;
            
            DrawField("Birth Number", dto.IdentityNationalId, true);
            DrawField("City", dto.IdentityCity, false);
            y += 35;
            
            DrawFullField("Country", dto.IdentityCountry);

            DrawSection("Contact Information");
            
            DrawField("Phone", dto.ContactPhone, true);
            DrawField("Email", dto.ContactEmail, false);
            y += 35;
            
            DrawFullField("Address", dto.ContactAddress);
            
            DrawField("Emergency Contact", dto.EmergencyName, true);
            DrawField("Emergency Phone", dto.EmergencyPhone, false);
            y += 35;

            DrawSection("Medical Basics");
            DrawFullField("Blood Type", dto.BloodType);

            if (!string.IsNullOrWhiteSpace(dto.Labs))
            {
                DrawSection("Lab / Clinical Summaries");
                var labsLines = dto.Labs!.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in labsLines)
                {
                    if (y > page.Height - 100)
                    {
                        page = doc.AddPage();
                        gfx = XGraphics.FromPdfPage(page);
                        y = 50;
                    }
                    gfx.DrawString(line, fontValue, primaryColor, new XRect(leftMargin, y, contentWidth, 18), XStringFormats.TopLeft);
                    y += 18;
                }
                y += 10;
            }

            if (!string.IsNullOrWhiteSpace(dto.AdvanceDirectives) || !string.IsNullOrWhiteSpace(dto.ConsentPreferences))
            {
                DrawSection("Legal / Directives");
                
                if (!string.IsNullOrWhiteSpace(dto.AdvanceDirectives))
                {
                    gfx.DrawString("Advance Directives", fontLabel, secondaryColor, new XRect(leftMargin, y, contentWidth, 15), XStringFormats.TopLeft);
                    y += 15;
                    var advLines = dto.AdvanceDirectives!.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in advLines)
                    {
                        if (y > page.Height - 100)
                        {
                            page = doc.AddPage();
                            gfx = XGraphics.FromPdfPage(page);
                            y = 50;
                        }
                        gfx.DrawString(line, fontValue, primaryColor, new XRect(leftMargin, y, contentWidth, 18), XStringFormats.TopLeft);
                        y += 18;
                    }
                    y += 10;
                }
                
                if (!string.IsNullOrWhiteSpace(dto.ConsentPreferences))
                {
                    gfx.DrawString("Consent Preferences", fontLabel, secondaryColor, new XRect(leftMargin, y, contentWidth, 15), XStringFormats.TopLeft);
                    y += 15;
                    var consentLines = dto.ConsentPreferences!.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in consentLines)
                    {
                        if (y > page.Height - 100)
                        {
                            page = doc.AddPage();
                            gfx = XGraphics.FromPdfPage(page);
                            y = 50;
                        }
                        gfx.DrawString(line, fontValue, primaryColor, new XRect(leftMargin, y, contentWidth, 18), XStringFormats.TopLeft);
                        y += 18;
                    }
                }
            }

            // Footer
            y = page.Height - 40;
            gfx.DrawLine(new XPen(secondaryColor, 0.5), leftMargin, y, rightMargin, y);
            y += 10;
            gfx.DrawString($"Generated on {DateTime.Now:dd.MM.yyyy HH:mm}", new XFont("Arial", 8, XFontStyle.Italic), 
                secondaryColor, new XRect(leftMargin, y, contentWidth, 15), XStringFormats.TopLeft);

            doc.Save(ms);
            return ms.ToArray();
        }

        public async Task<byte[]> GenerateQrAsync(string patientId)
        {
            if (string.IsNullOrWhiteSpace(patientId)) throw new ArgumentException("patientId is required");
            var dto = await GetByPatientIdAsync(patientId);
            var payloadObj = new
            {
                n = dto.IdentityFirstName,
                l = dto.IdentityLastName,
                b = dto.IdentityNationalId,
                d = dto.IdentityDateOfBirth,
                ph = dto.ContactPhone,
                e = dto.ContactEmail
            };
            var payload = System.Text.Json.JsonSerializer.Serialize(payloadObj);

            using var qrGen = new QRCodeGenerator();
            var data = qrGen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(data);
            var bytes = qrCode.GetGraphic(20);
            return bytes;
        }
    }
}
