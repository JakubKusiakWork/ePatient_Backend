using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ePatientApi.Models;

namespace ePatientApi.Interfaces
{
    public interface IVersioningService
    {
        Task<HealthCardVersion> CreateVersionAsync(HealthCardEntity currentEntity, string? modifiedBy = null);
        Task<IEnumerable<HealthCardVersion>> GetVersionsAsync(int healthCardId);
        Task<HealthCardVersion?> GetVersionByIdAsync(Guid versionId);
        Task<IEnumerable<string>> CompareVersionsAsync(Guid versionAId, Guid versionBId);
        Task RestoreVersionAsync(Guid versionId, string? restoredBy = null);
    }
}
