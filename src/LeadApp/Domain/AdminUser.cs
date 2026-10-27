namespace LeadApp.Domain;

public sealed class AdminUser
{
    /// <summary>Lower-cased e-mail; also the login user name and the document id.</summary>
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public int FailedAttempts { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime? LastLoginUtc { get; set; }

    public AdminUser Clone() => (AdminUser)MemberwiseClone();
}
