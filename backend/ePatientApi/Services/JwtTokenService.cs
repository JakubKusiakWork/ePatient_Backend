using System.IdentityModel.Tokens.Jwt;
using ePatientApi.Interfaces;

namespace ePatientApi.Services
{
    /// <summary>
    /// Service for handling JWT token extraction and parsing.  
    /// </summary>
    public class JwtTokenService : IJwtToken
    {
        public string? ExtractTokenFromHeader(HttpRequest request)
        {
            var token = request.Headers["Authorization"].ToString();

            if (string.IsNullOrWhiteSpace(token) || !token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return token.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim();
        }

        public JwtSecurityToken? ParseJwtToken(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            return handler.CanReadToken(token)
                ? handler.ReadJwtToken(token)
                : null;
        }
    }
}