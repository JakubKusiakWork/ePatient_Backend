using Microsoft.AspNetCore.Mvc;
using ePatientApi.Services;
using ePatientApi.Interfaces;

namespace ePatientApi.Controllers
{
    [ApiController]
    [Route("api/doctor-email")]
    public class DoctorEmailController : ControllerBase
    {
        private readonly DoctorEmailService _doctorEmail;

        public DoctorEmailController(DoctorEmailService doctorEmail)
        {
            _doctorEmail = doctorEmail;
        }

        [HttpPost("send-code")]
        public async Task<IActionResult> SendCode([FromBody] DoctorEmailRequest request, CancellationToken cancelToken)
        {
            if (string.IsNullOrEmpty(request.Email))
            {
                return BadRequest("Email is required.");
            }

            string res = await _doctorEmail.GenerateAndSendCode(request.Email, cancelToken);

            return res switch
            {
                "NOT_FOUND" => NotFound("Email not found."),
                "EXISTS" => Conflict("Email already verified."),
                "SUCCESS" => Ok("Verification code sent."),
                "ALREADY_SENT" => Conflict("Registration code already sent and is still valid."),
                _ => StatusCode(500, "Internal server error.")
            };
        }

        [HttpPost("set-password")]
        public async Task<IActionResult> SetPassword([FromBody] SetPasswordRequest request, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest("Email is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Password is required.");
            }

            var res = await _doctorEmail.setPassword(request.Email, request.Code, request.Password, cancelToken);

            return res switch
            {
                "NOT_FOUND" => NotFound("Email not found."),
                "EXISTS" => BadRequest("No verification code found. Request a new one."),
                "INVALID_CODE" => BadRequest("Invalid verification code."),
                "EXPIRED_CODE" => BadRequest("Registration code has expired. Request a new one."),
                "WEAK_PASSWORD" => BadRequest("Password must be at least 8 characters."),
                "ALREADY_SET" => Conflict("Password already set."),
                "SUCCESS" => Ok("Password set successfully."),
                _ => StatusCode(500, "Internal server error.")
            };
        }

        [HttpPost("verify-code")]
        public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeRequest request, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest("Email is required.");
            }

            if (request.Code == null)
            {
                return BadRequest("Invalid registration code.");
            }

            var res = await _doctorEmail.verifyCode(request.Email, request.Code, cancelToken);

            return res switch

            {
                "NOT_FOUND" => NotFound("Email not found."),
                "ALREADY_VERIFIED" => Conflict("Email already verified."),
                "INVALID_CODE" => BadRequest("Invalid registration code."),
                "EXPIRED_CODE" => BadRequest("Registration code has expired. Request a new one."),
                "SUCCESS" => Ok("Email successfully verified."),
                _ => StatusCode(500, "Internal server errorr.")
            };
        }
    }

    public record DoctorEmailRequest(string Email);
    public record SetPasswordRequest(string Email, string Code, string Password);
    public record VerifyCodeRequest(string Email, string Code);
}