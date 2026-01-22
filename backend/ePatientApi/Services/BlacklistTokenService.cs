using System.Collections.Concurrent;
using ePatientApi.Interfaces;

namespace ePatientApi.Services
{
    /// <summary>
    /// In-memory implementation of <see cref="ITokenBlacklistService"/> that holds blacklisted tokens with expiry timestamps.
    /// </summary>
    public class BlacklistTokenService : ITokenBlacklistService
    {
        private readonly ConcurrentDictionary<string, DateTime> _blacklistedTokens = new();
        public void BlacklistToken(string token, DateTime expiry)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException("Token must be provided.", nameof(token));
            }

            _blacklistedTokens[token] = expiry;
        }

        public bool IsTokenBlacklisted(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var now = DateTime.UtcNow;

            foreach (var entry in _blacklistedTokens)
            {
                if (entry.Value < now)
                {
                    _blacklistedTokens.TryRemove(entry.Key, out _);
                }
            }

            return _blacklistedTokens.TryGetValue(token, out var expiry) && expiry > now;
        }
    }
}