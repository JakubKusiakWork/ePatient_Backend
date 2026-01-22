using ePatientApi.Dtos;
using ePatientApi.Services;
using ePatientApi.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ePatientApi.Controllers
{
    
    [ApiController]
    [Route("api/auth")]
    public class ForgotPasswordController : ControllerBase
    {
        private readonly IPasswordReset myPasswordReset;

        public ForgotPasswordController(IPasswordReset passwordReset)
        {
            myPasswordReset = passwordReset;
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancelToken)
        {
           await myPasswordReset.RequestPasswordResetAsync(request, cancelToken);

           return Ok(new
            {
                message = "If there is an account whit this email address, we have sent you a link to reset your password."
            }); 
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancelToken)
        {
            try
            {
                await myPasswordReset.ResetPasswordAsync(request, cancelToken);

                return Ok(new
                {
                    message = "Your password has been successfully changed. You can log in with your new password."
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new
                {
                    message = ex.Message
                });
            }
        }
    }
}