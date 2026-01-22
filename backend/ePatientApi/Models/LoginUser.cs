namespace ePatientApi.Models
{
    /// <summary>
    /// DTO for login requests.
    /// </summary>
    public class LoginUser
    {
        public required string UserName { get; set; }
        public required string Password { get; set; }
    }
}