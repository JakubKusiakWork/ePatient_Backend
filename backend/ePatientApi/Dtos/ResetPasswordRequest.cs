namespace ePatientApi.Dtos
{
    public class ResetPasswordRequest
    {
        public string Token { get; set; } = null!;
        public string NewPassword {get; set; } = null!;
    }
}