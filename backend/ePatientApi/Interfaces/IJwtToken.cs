using System.IdentityModel.Tokens.Jwt;

namespace ePatientApi.Interfaces
{
    /// <summary>
    /// Helper methods for working with JWTs extracted from HTTP requests.
    /// </summary>
    public interface IJwtToken
    {
        string? ExtractTokenFromHeader(HttpRequest request);
        JwtSecurityToken? ParseJwtToken(string token);
    }
}