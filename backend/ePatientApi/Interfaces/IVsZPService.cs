using System.Runtime.CompilerServices;

namespace ePatientApi.Interfaces
{
    public interface IVsZPService
    {
        /// <summary>
        /// Calls the VSZP endpoint which return (true) if patient is registered in VSZP system, otherwise it returns (false).
        /// </summar>
        Task<bool> CheckAsync(string birthNumber, DateTime date, CancellationToken cancelToken);
    }
}