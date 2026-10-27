using System.Security.Claims;
using LeadApp.Domain;
using LeadApp.Options;
using LeadApp.Services.Storage;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LeadApp.Services.Auth;

public enum LoginOutcome { Success, InvalidCredentials, LockedOut }

public sealed record LoginResult(LoginOutcome Outcome, ClaimsPrincipal? Principal = null, DateTime? LockoutEndUtc = null);

public static class AdminRoles
{
    public const string Admin = "Admin";
    public const string Policy = "Admin";
}

/// <summary>
/// Password-based login for the single admin account. No registration: the account is seeded from
/// configuration by <see cref="AdminSeeder"/>. Passwords use ASP.NET Core Identity's PBKDF2 hasher.
/// </summary>
public sealed class AdminAuthService(
    IAdminUserRepository admins,
    IPasswordHasher<AdminUser> hasher,
    IOptions<AdminSeedOptions> options,
    AppClock clock,
    ILogger<AdminAuthService> logger)
{
    public const int MinPasswordLength = 12;

    // Hashed once so unknown e-mails cost the same time as known ones (no user enumeration via timing).
    private static readonly Lazy<string> DummyHash =
        new(() => new PasswordHasher<AdminUser>().HashPassword(new AdminUser(), Guid.NewGuid().ToString()));

    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var user = await admins.GetAsync(email.Trim(), ct);
        if (user is null)
        {
            hasher.VerifyHashedPassword(new AdminUser(), DummyHash.Value, password);
            return new LoginResult(LoginOutcome.InvalidCredentials);
        }

        var now = clock.UtcNow;
        if (user.LockoutEndUtc > now)
            return new LoginResult(LoginOutcome.LockedOut, LockoutEndUtc: user.LockoutEndUtc);

        var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            user.FailedAttempts++;
            if (user.FailedAttempts >= options.Value.MaxFailedAttempts)
            {
                user.FailedAttempts = 0;
                user.LockoutEndUtc = now.AddMinutes(options.Value.LockoutMinutes);
                logger.LogWarning("Admin {Email} locked out after repeated failed logins", user.Email);
            }
            await admins.SaveAsync(user, ct);
            return user.LockoutEndUtc > now
                ? new LoginResult(LoginOutcome.LockedOut, LockoutEndUtc: user.LockoutEndUtc)
                : new LoginResult(LoginOutcome.InvalidCredentials);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = hasher.HashPassword(user, password);

        user.FailedAttempts = 0;
        user.LockoutEndUtc = null;
        user.LastLoginUtc = now;
        await admins.SaveAsync(user, ct);

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, user.Email),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, AdminRoles.Admin)
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        return new LoginResult(LoginOutcome.Success, new ClaimsPrincipal(identity));
    }

    /// <returns>An error message, or <c>null</c> on success.</returns>
    public async Task<string?> ChangePasswordAsync(string email, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await admins.GetAsync(email, ct);
        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
            return "Trenutna lozinka nije ispravna.";

        if (newPassword.Length < MinPasswordLength)
            return $"Nova lozinka mora imati najmanje {MinPasswordLength} znakova.";

        if (newPassword == currentPassword)
            return "Nova lozinka mora biti različita od trenutne.";

        user.PasswordHash = hasher.HashPassword(user, newPassword);
        await admins.SaveAsync(user, ct);
        logger.LogInformation("Admin {Email} changed password", user.Email);
        return null;
    }
}

/// <summary>Creates the admin account from configuration when none exists yet.</summary>
public sealed class AdminSeeder(
    IServiceProvider services,
    IOptions<AdminSeedOptions> options,
    ILogger<AdminSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var admins = scope.ServiceProvider.GetRequiredService<IAdminUserRepository>();
        if (await admins.AnyAsync(cancellationToken))
            return;

        var (email, password) = (options.Value.Email?.Trim().ToLowerInvariant(), options.Value.InitialPassword);
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            logger.LogWarning("No admin account exists. Set Admin:Email and Admin:InitialPassword (user-secrets) to create one.");
            return;
        }

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AdminUser>>();
        var user = new AdminUser { Email = email };
        user.PasswordHash = hasher.HashPassword(user, password);
        await admins.SaveAsync(user, cancellationToken);
        logger.LogInformation("Seeded admin account {Email}", email);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
