using ePatientApi.Dtos;

namespace ePatientApi.Interfaces
{
    public interface IPasswordReset
    {
        Task RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken cancelToken);
        Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancelToken); 
    }
}