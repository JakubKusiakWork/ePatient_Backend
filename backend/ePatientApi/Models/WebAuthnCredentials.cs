using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ePatientApi.Models
{
    /// <summary>
    /// Credentials stored for WebAuthn authentication for a user.
    /// </summary>
    [Table("webauthncredentials")]
    public class WebAuthnCredentials
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        public string UserId { get; set; } = null!;

        [Column("user_type")]
        public string UserType { get; set; } = null!;

        [Column("credential_id")]
        public string CredentialId { get; set; } = null!;

        [Column("public_key")]
        public string PublicKey { get; set; } = null!;

        [Column("user_handle")]
        public string UserHandle { get; set; } = null!;

        [Column("signature_counter")]
        public uint SignatureCounter { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("authenticator_type")]
        public required string AuthenticatorType { get; set; }
    }
}