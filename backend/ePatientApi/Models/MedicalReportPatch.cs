namespace ePatientApi.Models
{
    /// <summary>
    /// DTO for patching/updating parts of a medical report.
    /// </summary>
    public class MedicalReportPatch
    {
        public string? Medication { get; set; }
        public string? ExternalExaminations { get; set; }
        public string? Condition { get; set; }
        public string? State { get; set; }
        public bool? FollowUpRequired { get; set; }
        public string? Priority { get; set; }
    }
}