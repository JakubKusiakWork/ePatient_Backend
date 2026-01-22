namespace ePatientApi.Interfaces
{
    /// <summary>
    /// Service contract for managing blacklisted JWT tokens.
    /// </summary>
    public interface ITokenBlacklistService
    {
        void BlacklistToken(string token, DateTime expiry);
        bool IsTokenBlacklisted(string token);
    }
}