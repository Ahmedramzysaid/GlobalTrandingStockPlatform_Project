using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GSTP.AuthService.Models;

[Table("users_auth")]
public class UserAuth
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("email")]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Column("password_hash")]
    [MaxLength(512)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [Column("salt")]
    [MaxLength(128)]
    public string Salt { get; set; } = string.Empty;

    [Column("mfa_enabled")]
    public bool MfaEnabled { get; set; } = false;

    [Column("mfa_secret")]
    [MaxLength(128)]
    public string? MfaSecret { get; set; }

    [Column("email_verified")]
    public bool EmailVerified { get; set; } = false;

    [Column("is_locked")]
    public bool IsLocked { get; set; } = false;

    [Column("failed_attempts")]
    public int FailedAttempts { get; set; } = 0;

    [Column("lockout_until")]
    public DateTime? LockoutUntil { get; set; }

    [Required]
    [Column("role")]
    [MaxLength(50)]
    public string Role { get; set; } = "User";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

[Table("refresh_tokens")]
public class RefreshToken
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required]
    [Column("token")]
    [MaxLength(512)]
    public string Token { get; set; } = string.Empty;

    [Column("device_info")]
    [MaxLength(255)]
    public string? DeviceInfo { get; set; }

    [Column("ip_address")]
    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("revoked")]
    public bool Revoked { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("UserId")]
    public UserAuth User { get; set; } = null!;
}
