using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ePatientApi.Interfaces;

namespace ePatientApi.Controllers
{
    [Route("api/logout")]
    [ApiController]
    public class LogoutController : ControllerBase
    {
        private readonly ITokenBlacklistService _tokenBlacklist;
        private readonly IJwtToken _jwtToken;

        /// <summary>
        /// Initializes a new instance of <see cref="LogoutController"/>.
        /// </summary>
        public LogoutController(ITokenBlacklistService tokenBlacklist, IJwtToken jwtToken)
        {
            _tokenBlacklist = tokenBlacklist ?? throw new ArgumentNullException(nameof(tokenBlacklist));
            _jwtToken = jwtToken ?? throw new ArgumentNullException(nameof(jwtToken));
        }

        /// <summary>
        /// Logs out the user by blacklisting their JWT token.
        /// </summary>
        [Authorize]
        [HttpPost]
        public IActionResult Logout()
        {
            var token = _jwtToken.ExtractTokenFromHeader(Request);

            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest("Missing token.");
            }

            var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);
            var expiry = jwt.ValidTo;

            _tokenBlacklist.BlacklistToken(token, expiry);

            return Ok("Logged out successfully.");
        }
    }
}