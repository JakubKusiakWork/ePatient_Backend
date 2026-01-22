using ePatientApi.Models;

namespace ePatientApi.Interfaces
{
    /// <summary>
    /// Contract for services that provide homepage summary data.
    /// </summary>
    public interface IHomepageData
    {
        Task<HomepageData> GetHomepageDataAsync();
    }
}