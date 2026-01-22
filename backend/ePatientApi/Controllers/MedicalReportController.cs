using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ePatientApi.DataAccess;
using ePatientApi.Models;

namespace ePatientApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MedicalReportController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MedicalReportController(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Creates a new medical report for a patient.
        /// </summary>
        [HttpPost("create")]
        public async Task<IActionResult> CreateMedicalReport([FromBody] MedicalReport report)
        {
            if (report == null || string.IsNullOrWhiteSpace(report.PatientBirthNumber))
            {
                return BadRequest(new { message = "Invalid report payload." });
            }

            var patient = await FindPatientByAnyIdAsync(report.PatientBirthNumber);
            if (patient == null)
            {
                return NotFound(new { message = "Patient not found." });
            }

            if (report.AppointmentId > 0)
            {
                var existingReport = await _context.MedicalReports
                    .FirstOrDefaultAsync(r => r.AppointmentId == report.AppointmentId && r.IsActive);
                
                if (existingReport != null)
                {
                    return BadRequest(new { 
                        message = "A report already exists for this appointment. Use update instead.",
                        reportId = existingReport.ReportId,
                        appointmentId = existingReport.AppointmentId
                    });
                }
            }

            report.PatientBirthNumber = patient.BirthNumber;
            try
            {
                if (patient.Id > 0)
                {
                    report.PatientId = patient.Id;
                }
            }
            catch {}
            if (string.IsNullOrWhiteSpace(report.AttendingDoctor))
            {
                try
                {
                    var latestLogged = await _context.LoggedDoctors.OrderByDescending(ld => ld.Id).FirstOrDefaultAsync();
                    if (latestLogged != null)
                    {
                        report.AttendingDoctor = string.Concat(latestLogged.DoctorFirstName, " ", latestLogged.DoctorLastName).Trim();
                    }
                }
                catch {}
            }
            
            report.CreatedAt = DateTime.UtcNow;
            await _context.MedicalReports.AddAsync(report);
            await _context.SaveChangesAsync();

            // Auto-create prescriptions from medication text
            if (!string.IsNullOrWhiteSpace(report.Medication))
            {
                var medicationLines = report.Medication.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in medicationLines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var medicationName = System.Text.RegularExpressions.Regex.Match(line, @"^([A-Za-z]+)").Value.Trim().ToLower();
                    if (string.IsNullOrWhiteSpace(medicationName)) continue;

                    var existingProduct = await _context.Products.FirstOrDefaultAsync(p => p.Name.ToLower() == medicationName);
                    if (existingProduct == null)
                    {
                        var newProduct = new Product
                        {
                            Name = medicationName,
                            ExternalCode = medicationName
                        };
                        await _context.Products.AddAsync(newProduct);
                        await _context.SaveChangesAsync();
                    }

                    var prescription = new Prescription
                    {
                        ReportId = report.ReportId,
                        MedicationName = medicationName,
                        Dosage = line.Contains("mg") ? System.Text.RegularExpressions.Regex.Match(line, @"\d+\s*mg").Value : null,
                        Instructions = line,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _context.Prescriptions.AddAsync(prescription);
                }
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Report created.", reportId = report.ReportId, patientBirthNumber = report.PatientBirthNumber });
        }

        /// <summary>
        /// Updates an existing medical report by replacing the data.
        /// Allows setting fields to empty strings to clear them.
        /// </summary>
        [HttpPatch("update/{appointmentId}")]
        public async Task<IActionResult> UpdateMedicalReport(int appointmentId, [FromBody] MedicalReportPatch updateReport)
        {
            var existingReport = await _context.MedicalReports
                .FirstOrDefaultAsync(r => r.AppointmentId == appointmentId && r.IsActive);

            if (existingReport == null)
            {
                return NotFound(new { message = "Report not found for this appointment." });
            }

            // Always update fields if they are provided (even if empty)
            // This allows clearing fields by setting them to empty string
            if (updateReport.Medication != null)
            {
                existingReport.Medication = updateReport.Medication;
            }

            if (updateReport.ExternalExaminations != null)
            {
                existingReport.ExternalExaminations = updateReport.ExternalExaminations;
            }

            if (updateReport.Condition != null)
            {
                existingReport.Condition = updateReport.Condition;
            }

            if (!string.IsNullOrWhiteSpace(updateReport.State))
            {
                existingReport.State = updateReport.State;
            }

            if (updateReport.FollowUpRequired.HasValue)
            {
                existingReport.FollowUpRequired = updateReport.FollowUpRequired.Value;
            }

            if (updateReport.Priority != null)
            {
                existingReport.Priority = updateReport.Priority;
            }

            existingReport.CreatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { 
                message = "Report updated successfully.",
                reportId = existingReport.ReportId,
                appointmentId = existingReport.AppointmentId
            });
        }

        /// <summary>
        /// Retrieves the latest medical report for a specific appointment.
        /// </summary>
        [HttpGet("actual/{appointmentId}")]
        public async Task<IActionResult> GetActualDatas(int appointmentId)
        {
            var medicalReport = await _context.MedicalReports
                .Include(r => r.Patient)
                .Include(r => r.Prescriptions)
                .Where(r => r.AppointmentId == appointmentId && r.IsActive)
                .FirstOrDefaultAsync();

            if (medicalReport == null)
            {
                return NotFound(new { message = "Medical report not found for this appointment." });
            }

            var report = new
            {
                firstname = medicalReport.Patient != null ? medicalReport.Patient.FirstName : string.Empty,
                lastName = medicalReport.Patient != null ? medicalReport.Patient.LastName : string.Empty,
                medication = medicalReport.Medication,
                externalExamination = medicalReport.ExternalExaminations,
                condition = medicalReport.Condition,
                createdAt = medicalReport.CreatedAt.ToString("dd.MM.yyyy"),
                followUpRequired = medicalReport.FollowUpRequired,
                priority = medicalReport.Priority,
                prescriptions = medicalReport.Prescriptions?.Select(p => new
                {
                    prescriptionId = p.PrescriptionId,
                    medicationName = p.MedicationName,
                    dosage = p.Dosage,
                    instructions = p.Instructions,
                    quantity = p.Quantity
                }).ToList()
            };

            return Ok(report);
        }

        /// <summary>
        /// Retrieves the medical report history for a specific patient.
        /// For doctors: filtered by their appointments/patients.
        /// For patients: shows all their own reports.
        /// </summary>
        [HttpGet("history/{patientId}")]
        public async Task<IActionResult> GetReportHistory(string patientId)
        {
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return BadRequest(new { message = "Patient identifier required." });
            }

            var userIdentifier = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            
            if (string.IsNullOrWhiteSpace(userIdentifier))
            {
                return Unauthorized(new { message = "Unable to identify the logged-in user." });
            }

            var patient = await FindPatientByAnyIdAsync(patientId);
            if (patient == null)
            {
                return NotFound(new { message = "Patient not found." });
            }

            var birthNumber = patient.BirthNumber;

            var reports = await _context.MedicalReports
                .Include(r => r.Prescriptions)
                .Where(r => r.PatientBirthNumber == birthNumber)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            List<MedicalReport> filteredReports;

            if (userRole != null && userRole.Contains("Clinician", StringComparison.OrdinalIgnoreCase))
            {
                var doctor = await _context.RegisteredDoctors
                    .FirstOrDefaultAsync(d => d.DoctorCode == userIdentifier);
                
                if (doctor == null)
                {
                    return NotFound(new { message = "Doctor not found." });
                }

                var doctorAppointmentIds = await _context.Appointments
                    .Where(a => a.DoctorId == doctor.DoctorId)
                    .Select(a => a.AppointmentId)
                    .ToListAsync();

                var doctorFullName = $"{doctor.DoctorFirstName} {doctor.DoctorLastName}".Trim();
                var verifiedName = doctor.VerifiedFullName;
                
                bool isGP = IsGeneralPractitioner(doctor.VerifiedSpecialization);
                
                bool isPatientAssignedToGP = false;
                if (isGP)
                {
                    var assignedPatient = await _context.RegisteredPatients
                        .FirstOrDefaultAsync(p => p.BirthNumber == birthNumber && p.GpDoctorId == doctor.DoctorId);
                    isPatientAssignedToGP = assignedPatient != null;
                }
                
                List<string> gpDoctorNames = new List<string>();
                if (isGP && isPatientAssignedToGP)
                {
                    var gpDoctors = await _context.RegisteredDoctors
                        .Where(d => d.VerifiedSpecialization != null)
                        .Select(d => new { d.DoctorFirstName, d.DoctorLastName, d.VerifiedFullName, d.VerifiedSpecialization, d.DoctorId })
                        .ToListAsync();
                    
                    foreach (var gp in gpDoctors.Where(d => IsGeneralPractitioner(d.VerifiedSpecialization) && d.DoctorId != doctor.DoctorId))
                    {
                        gpDoctorNames.Add($"{gp.DoctorFirstName} {gp.DoctorLastName}".Trim().ToLower());
                        if (!string.IsNullOrWhiteSpace(gp.VerifiedFullName))
                        {
                            gpDoctorNames.Add(gp.VerifiedFullName.Trim().ToLower());
                        }
                    }
                }

                filteredReports = reports.Where(r => 
                {
                    bool isFromTheirAppointment = r.AppointmentId > 0 && doctorAppointmentIds.Contains(r.AppointmentId);
                    bool isTheirReport = !string.IsNullOrWhiteSpace(r.AttendingDoctor) && 
                                       (r.AttendingDoctor.Contains(doctorFullName, StringComparison.OrdinalIgnoreCase) ||
                                        (!string.IsNullOrWhiteSpace(verifiedName) && r.AttendingDoctor.Contains(verifiedName, StringComparison.OrdinalIgnoreCase)));
                    
                    bool isForTheirGPPatient = isGP && isPatientAssignedToGP;
                    
                    if (!isFromTheirAppointment && !isTheirReport && !isForTheirGPPatient)
                    {
                        return false;
                    }
                    
                    if (isGP && isForTheirGPPatient && !string.IsNullOrWhiteSpace(r.AttendingDoctor))
                    {
                        var attendingDoctorLower = r.AttendingDoctor.Trim().ToLower();
                        if (gpDoctorNames.Any(gpName => attendingDoctorLower.Contains(gpName) || gpName.Contains(attendingDoctorLower)))
                        {
                            return false;
                        }
                    }
                    
                    return true;
                }).ToList();
            }
            else
            {
                var requestingPatient = await _context.RegisteredPatients
                    .FirstOrDefaultAsync(p => p.BirthNumber == userIdentifier || p.Id.ToString() == userIdentifier);
                
                if (requestingPatient == null || requestingPatient.BirthNumber != birthNumber)
                {
                    return Forbid();
                }
                filteredReports = reports;
            }

            var result = new List<object>();
            foreach (var report in filteredReports)
            {
                string? attendingDoctor = report.AttendingDoctor;
                string? specialization = null;

                if (string.IsNullOrWhiteSpace(attendingDoctor))
                {
                    try
                    {
                        var logged = await _context.LoggedDoctors
                            .Where(ld => ld.Created_at <= report.CreatedAt)
                            .OrderByDescending(ld => ld.Created_at)
                            .FirstOrDefaultAsync();
                        
                        if (logged != null)
                        {
                            try
                            {
                                attendingDoctor = string.Concat(logged.DoctorFirstName, " ", logged.DoctorLastName).Trim();
                                
                                var doctor = await _context.RegisteredDoctors
                                    .FirstOrDefaultAsync(d => d.DoctorFirstName == logged.DoctorFirstName && d.DoctorLastName == logged.DoctorLastName);
                                if (doctor != null)
                                {
                                    specialization = doctor.VerifiedSpecialization;
                                }
                            }
                            catch { }
                        }
                    }
                    catch {}
                }
                else
                {
                    try
                    {
                        RegisteredDoctor? doctor = null;
                        
                        var parts = attendingDoctor.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            var firstName = parts[0];
                            var lastName = string.Join(" ", parts.Skip(1));
                            
                            doctor = await _context.RegisteredDoctors
                                .FirstOrDefaultAsync(d => 
                                    d.DoctorFirstName.ToLower() == firstName.ToLower() && 
                                    d.DoctorLastName.ToLower() == lastName.ToLower());
                            
                            if (doctor == null)
                            {
                                doctor = await _context.RegisteredDoctors
                                    .FirstOrDefaultAsync(d => 
                                        d.VerifiedFirstName != null && d.VerifiedLastName != null &&
                                        d.VerifiedFirstName.ToLower() == firstName.ToLower() && 
                                        d.VerifiedLastName.ToLower() == lastName.ToLower());
                            }
                            
                            if (doctor == null)
                            {
                                doctor = await _context.RegisteredDoctors
                                    .FirstOrDefaultAsync(d => 
                                        d.VerifiedFullName != null && 
                                        d.VerifiedFullName.ToLower().Contains(attendingDoctor.ToLower()));
                            }
                            
                            if (doctor == null)
                            {
                                doctor = await _context.RegisteredDoctors
                                    .FirstOrDefaultAsync(d => 
                                        (d.DoctorFirstName + " " + d.DoctorLastName).ToLower().Contains(attendingDoctor.ToLower()));
                            }
                            
                            if (doctor != null)
                            {
                                specialization = doctor.VerifiedSpecialization;
                            }
                        }
                    }
                    catch { }
                }

                result.Add(new {
                    report.ReportId,
                    report.PatientBirthNumber,
                    report.AppointmentId,
                    report.PatientId,
                    report.Medication,
                    report.ExternalExaminations,
                    report.Condition,
                    report.State,
                    createdAt = report.CreatedAt,
                    attendingDoctor = attendingDoctor ?? "Unknown Doctor",
                    specialization = TranslateSpecialization(specialization),
                    followUpRequired = report.FollowUpRequired,
                    priority = report.Priority,
                    prescriptions = report.Prescriptions?.Select(p => new
                    {
                        prescriptionId = p.PrescriptionId,
                        medicationName = p.MedicationName,
                        dosage = p.Dosage,
                        instructions = p.Instructions,
                        quantity = p.Quantity
                    }).ToList()
                });
            }

            return Ok(result);
        }

        /// <summary>
        /// Retrieves all medical reports for the logged-in doctor's patients.
        /// </summary>
        [HttpGet("all")]
        public async Task<IActionResult> GetAllReports()
        {
            var doctorCode = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            
            if (string.IsNullOrWhiteSpace(doctorCode))
            {
                return Unauthorized(new { message = "Unable to identify the logged-in doctor." });
            }

            var doctor = await _context.RegisteredDoctors
                .FirstOrDefaultAsync(d => d.DoctorCode == doctorCode);
            
            if (doctor == null)
            {
                return NotFound(new { message = "Doctor not found." });
            }

            var doctorAppointmentIds = await _context.Appointments
                .Where(a => a.DoctorId == doctor.DoctorId)
                .Select(a => a.AppointmentId)
                .ToListAsync();

            var doctorFullName = $"{doctor.DoctorFirstName} {doctor.DoctorLastName}".Trim();
            var verifiedName = doctor.VerifiedFullName;
            
            bool isGP = IsGeneralPractitioner(doctor.VerifiedSpecialization);
            
            List<string> gpPatientBirthNumbers = new List<string>();
            if (isGP)
            {
                gpPatientBirthNumbers = await _context.RegisteredPatients
                    .Where(p => p.GpDoctorId == doctor.DoctorId)
                    .Select(p => p.BirthNumber)
                    .ToListAsync();
            }
            
            var allDoctorSpecs = await _context.RegisteredDoctors
                .Select(d => new { 
                    FullName = (d.DoctorFirstName + " " + d.DoctorLastName).ToLower(), 
                    d.VerifiedSpecialization 
                })
                .ToListAsync();
            
            var doctorSpecMap = allDoctorSpecs.ToDictionary(d => d.FullName, d => d.VerifiedSpecialization);
            
            List<string> gpDoctorNames = new List<string>();
            
            if (isGP)
            {
                var gpDoctors = await _context.RegisteredDoctors
                    .Where(d => d.VerifiedSpecialization != null)
                    .Select(d => new { d.DoctorFirstName, d.DoctorLastName, d.VerifiedFullName, d.VerifiedSpecialization, d.DoctorId })
                    .ToListAsync();
                
                foreach (var gp in gpDoctors.Where(d => IsGeneralPractitioner(d.VerifiedSpecialization) && d.DoctorId != doctor.DoctorId))
                {
                    gpDoctorNames.Add($"{gp.DoctorFirstName} {gp.DoctorLastName}".Trim().ToLower());
                    if (!string.IsNullOrWhiteSpace(gp.VerifiedFullName))
                    {
                        gpDoctorNames.Add(gp.VerifiedFullName.Trim().ToLower());
                    }
                }
            }
            
            var reports = _context.MedicalReports
                .Include(r => r.Patient)
                .AsEnumerable()
                .Where(r => 
                {
                    bool isFromTheirAppointment = r.AppointmentId > 0 && doctorAppointmentIds.Contains(r.AppointmentId);
                    bool isTheirReport = !string.IsNullOrWhiteSpace(r.AttendingDoctor) && 
                                       (r.AttendingDoctor.Contains(doctorFullName, StringComparison.OrdinalIgnoreCase) ||
                                        (!string.IsNullOrWhiteSpace(verifiedName) && r.AttendingDoctor.Contains(verifiedName, StringComparison.OrdinalIgnoreCase)));
                    
                    bool isForTheirGPPatient = isGP && !string.IsNullOrWhiteSpace(r.PatientBirthNumber) && gpPatientBirthNumbers.Contains(r.PatientBirthNumber);
                    
                    if (!isFromTheirAppointment && !isTheirReport && !isForTheirGPPatient)
                    {
                        return false;
                    }
                    
                    if (isGP && isForTheirGPPatient && !string.IsNullOrWhiteSpace(r.AttendingDoctor))
                    {
                        var attendingDoctorLower = r.AttendingDoctor.Trim().ToLower();
                        if (gpDoctorNames.Any(gpName => attendingDoctorLower.Contains(gpName) || gpName.Contains(attendingDoctorLower)))
                        {
                            return false;
                        }
                    }
                    
                    return true;
                })
                .Select(r => 
                {
                    var doctorName = r.AttendingDoctor;
                    string? specialization = null;
                    
                    if (string.IsNullOrWhiteSpace(doctorName))
                    {
                        var loggedDoctor = _context.LoggedDoctors
                            .Where(ld => ld.Created_at <= r.CreatedAt)
                            .OrderByDescending(ld => ld.Created_at)
                            .FirstOrDefault();
                        
                        if (loggedDoctor != null)
                        {
                            doctorName = string.Concat(loggedDoctor.DoctorFirstName, " ", loggedDoctor.DoctorLastName).Trim();
                        }
                    }
                    
                    if (!string.IsNullOrWhiteSpace(doctorName))
                    {
                        var doctorKey = doctorName.Trim().ToLower();
                        if (doctorSpecMap.TryGetValue(doctorKey, out var spec))
                        {
                            specialization = spec;
                        }
                    }
                    
                    return new
                    {
                        appointmentId = r.AppointmentId,
                        patientName = r.Patient != null
                            ? string.Concat(r.Patient.FirstName, " ", r.Patient.LastName)
                            : "Unknown Patient",
                        patientBirthNumber = r.Patient?.BirthNumber,
                        createdAt = r.CreatedAt.ToString("dd-MM-yyyy"),
                        doctorName = doctorName ?? "Unknown Doctor",
                        specialization = TranslateSpecialization(specialization),
                        followUpRequired = r.FollowUpRequired,
                        priority = r.Priority
                    };
                })
                .OrderByDescending(r => r.createdAt)
                .ToList();

            return Ok(reports);
        }

        /// <summary>
        /// Retrieves the latest 5 medical reports for the logged-in doctor's patients.
        /// </summary>
        [HttpGet("latest")]
        public async Task<IActionResult> GetLatestReports()
        {
            var doctorCode = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            
            if (string.IsNullOrWhiteSpace(doctorCode))
            {
                return Unauthorized(new { message = "Unable to identify the logged-in doctor." });
            }

            var doctor = await _context.RegisteredDoctors
                .FirstOrDefaultAsync(d => d.DoctorCode == doctorCode);
            
            if (doctor == null)
            {
                return NotFound(new { message = "Doctor not found." });
            }

            var doctorAppointmentIds = await _context.Appointments
                .Where(a => a.DoctorId == doctor.DoctorId)
                .Select(a => a.AppointmentId)
                .ToListAsync();

            var doctorFullName = $"{doctor.DoctorFirstName} {doctor.DoctorLastName}".Trim();
            var verifiedName = doctor.VerifiedFullName;
            
            bool isGP = IsGeneralPractitioner(doctor.VerifiedSpecialization);
            
            List<string> gpPatientBirthNumbers = new List<string>();
            if (isGP)
            {
                gpPatientBirthNumbers = await _context.RegisteredPatients
                    .Where(p => p.GpDoctorId == doctor.DoctorId)
                    .Select(p => p.BirthNumber)
                    .ToListAsync();
            }
            
            var allDoctorSpecs2 = await _context.RegisteredDoctors
                .Select(d => new { 
                    FullName = (d.DoctorFirstName + " " + d.DoctorLastName).ToLower(), 
                    d.VerifiedSpecialization 
                })
                .ToListAsync();
            
            var doctorSpecMap2 = allDoctorSpecs2.ToDictionary(d => d.FullName, d => d.VerifiedSpecialization);
            
            List<string> gpDoctorNames = new List<string>();
            
            if (isGP)
            {
                var gpDoctors = await _context.RegisteredDoctors
                    .Where(d => d.VerifiedSpecialization != null)
                    .Select(d => new { d.DoctorFirstName, d.DoctorLastName, d.VerifiedFullName, d.VerifiedSpecialization, d.DoctorId })
                    .ToListAsync();
                
                foreach (var gp in gpDoctors.Where(d => IsGeneralPractitioner(d.VerifiedSpecialization) && d.DoctorId != doctor.DoctorId))
                {
                    gpDoctorNames.Add($"{gp.DoctorFirstName} {gp.DoctorLastName}".Trim().ToLower());
                    if (!string.IsNullOrWhiteSpace(gp.VerifiedFullName))
                    {
                        gpDoctorNames.Add(gp.VerifiedFullName.Trim().ToLower());
                    }
                }
            }
            
            var latestReports = _context.MedicalReports
                .Include(r => r.Patient)
                .OrderByDescending(r => r.CreatedAt)
                .AsEnumerable()
                .Where(r => 
                {
                    bool isFromTheirAppointment = r.AppointmentId > 0 && doctorAppointmentIds.Contains(r.AppointmentId);
                    bool isTheirReport = !string.IsNullOrWhiteSpace(r.AttendingDoctor) && 
                                       (r.AttendingDoctor.Contains(doctorFullName, StringComparison.OrdinalIgnoreCase) ||
                                        (!string.IsNullOrWhiteSpace(verifiedName) && r.AttendingDoctor.Contains(verifiedName, StringComparison.OrdinalIgnoreCase)));
                    
                    bool isForTheirGPPatient = isGP && !string.IsNullOrWhiteSpace(r.PatientBirthNumber) && gpPatientBirthNumbers.Contains(r.PatientBirthNumber);
                    
                    if (!isFromTheirAppointment && !isTheirReport && !isForTheirGPPatient)
                        return false;
                    
                    if (isGP && isForTheirGPPatient && !string.IsNullOrWhiteSpace(r.AttendingDoctor))
                    {
                        var attendingDoctorLower = r.AttendingDoctor.Trim().ToLower();
                        if (gpDoctorNames.Any(gpName => attendingDoctorLower.Contains(gpName) || gpName.Contains(attendingDoctorLower)))
                        {
                            return false;
                        }
                    }
                    
                    return true;
                })
                .Take(5)
                .Select(r =>
                {
                    var doctorName = r.AttendingDoctor;
                    string? specialization = null;
                    
                    if (string.IsNullOrWhiteSpace(doctorName))
                    {
                        var loggedDoctor = _context.LoggedDoctors
                            .Where(ld => ld.Created_at <= r.CreatedAt)
                            .OrderByDescending(ld => ld.Created_at)
                            .FirstOrDefault();
                        
                        if (loggedDoctor != null)
                        {
                            doctorName = string.Concat(loggedDoctor.DoctorFirstName, " ", loggedDoctor.DoctorLastName).Trim();
                        }
                    }
                    
                    if (!string.IsNullOrWhiteSpace(doctorName))
                    {
                        var doctorKey = doctorName.Trim().ToLower();
                        if (doctorSpecMap2.TryGetValue(doctorKey, out var spec))
                        {
                            specialization = spec;
                        }
                    }
                    
                    return new
                    {
                        appointmentId = r.AppointmentId,
                        patientName = r.Patient != null
                            ? string.Concat(r.Patient.FirstName, " ", r.Patient.LastName)
                            : "Unknown Patient",
                        createdAt = r.CreatedAt.ToString("dd-MM-yyyy"),
                        doctorName = doctorName ?? "Unknown Doctor",
                        specialization = TranslateSpecialization(specialization),
                        followUpRequired = r.FollowUpRequired,
                        priority = r.Priority
                    };
                })
                .ToList();

            return Ok(latestReports);
        }

        /// <summary>
        /// Retrieves a medical report by its ID.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetReportById(int id)
        {
            var report = await _context.MedicalReports
                .FirstOrDefaultAsync(r => r.ReportId == id) ?? throw new InvalidOperationException("Report not found.");

            return Ok(report);
        }

        /// <summary>
        /// Translates Slovak specialization names to English equivalents.
        /// </summary>
        private string TranslateSpecialization(string? slovakSpecialization)
        {
            if (string.IsNullOrWhiteSpace(slovakSpecialization))
            {
                return "General Practitioner";
            }

            var lower = slovakSpecialization.ToLower().Trim();
            
            if (lower.Contains("všeobecné lekárstvo") || lower.Contains("vseobecne lekarstvo"))
            {
                return "General Practitioner";
            }
            else if (lower.Contains("ortopédia") || lower.Contains("ortopedia"))
            {
                return "Orthopedics";
            }
            return slovakSpecialization;
        }

        /// <summary>
        /// Checks if a specialization is General Practitioner.
        /// </summary>
        private bool IsGeneralPractitioner(string? specialization)
        {
            if (string.IsNullOrWhiteSpace(specialization))
            {
                return false;
            }

            var lower = specialization.ToLower().Trim();
            return lower.Contains("všeobecné lekárstvo") || 
                   lower.Contains("vseobecne lekarstvo") ||
                   lower.Contains("general practitioner");
        }

        private async Task<RegisteredPatient?> FindPatientByAnyIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            id = id.Trim();

            var byBirth = await _context.RegisteredPatients
                .FirstOrDefaultAsync(p => p.BirthNumber == id);

            if (byBirth != null)
            {
                return byBirth;
            }

            var byUsername = await _context.RegisteredPatients
                .FirstOrDefaultAsync(p => p.Username == id);

            return byUsername;
        }
    }
}