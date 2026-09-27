using ITMartinMitEl.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ITMartinMitEl.Server.Services;

public sealed record AccountResult(bool Ok, string? Error = null, UserAccount? User = null)
{
    public static AccountResult Fail(string error) => new(false, error);
    public static AccountResult Success(UserAccount user) => new(true, null, user);
}

/// <summary>
/// Accounts, households and the advisor grants between them. Everything anyone can do
/// to an account goes through here, so the rules live in one place.
/// </summary>
public sealed class AccountService(IDbContextFactory<MitElDbContext> factory)
{
    public const int MinPasswordLength = 8;

    public static string Normalise(string? email) => (email ?? "").Trim().ToLowerInvariant();

    public static bool LooksLikeEmail(string email) =>
        email.Length >= 5 && email.Contains('@') && email.IndexOf('.', email.IndexOf('@')) > 0;

    /// <summary>Creates an account and the household that belongs to it.</summary>
    public async Task<AccountResult> RegisterAsync(string email, string password, string displayName, string? householdName = null)
    {
        var check = Validate(email, password);
        if (check is not null) return check;

        email = Normalise(email);
        await using var db = await factory.CreateDbContextAsync();
        if (await db.Users.AnyAsync(u => u.Email == email))
            return AccountResult.Fail("Der findes allerede en bruger med den mail.");

        var household = new Household
        {
            Name = string.IsNullOrWhiteSpace(householdName) ? "Mit hjem" : householdName.Trim(),
        };
        var user = new UserAccount
        {
            Email = email,
            PasswordHash = PasswordHasher.Hash(password),
            Role = UserRoles.Owner,
            HouseholdId = household.Id,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? email : displayName.Trim(),
        };

        db.Households.Add(household);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return AccountResult.Success(user);
    }

    /// <summary>An advisor has no household of their own - only the grants customers give them.</summary>
    public async Task<AccountResult> RegisterAdvisorAsync(string email, string password, string displayName)
    {
        var check = Validate(email, password);
        if (check is not null) return check;

        email = Normalise(email);
        await using var db = await factory.CreateDbContextAsync();
        if (await db.Users.AnyAsync(u => u.Email == email))
            return AccountResult.Fail("Der findes allerede en bruger med den mail.");

        var user = new UserAccount
        {
            Email = email,
            PasswordHash = PasswordHasher.Hash(password),
            Role = UserRoles.Advisor,
            HouseholdId = null,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? email : displayName.Trim(),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return AccountResult.Success(user);
    }

    public async Task<AccountResult> AuthenticateAsync(string email, string password)
    {
        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == Normalise(email));

        // The same answer either way, so the page never reveals whether a mail exists.
        if (user is null || !PasswordHasher.Verify(password ?? "", user.PasswordHash))
            return AccountResult.Fail("Forkert mail eller adgangskode.");

        user.LastLoginUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return AccountResult.Success(user);
    }

    public async Task<AccountResult> ChangePasswordAsync(Guid userId, string current, string replacement)
    {
        if ((replacement ?? "").Length < MinPasswordLength)
            return AccountResult.Fail($"Adgangskoden skal være mindst {MinPasswordLength} tegn.");

        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(userId);
        if (user is null) return AccountResult.Fail("Brugeren findes ikke.");
        if (!PasswordHasher.Verify(current ?? "", user.PasswordHash))
            return AccountResult.Fail("Den nuværende adgangskode passer ikke.");

        user.PasswordHash = PasswordHasher.Hash(replacement!);
        await db.SaveChangesAsync();
        return AccountResult.Success(user);
    }

    /// <summary>
    /// Used from the admin page: there is no mail server in the suite, so a forgotten
    /// password is reset by hand and told to the customer.
    /// </summary>
    public async Task<AccountResult> ResetPasswordAsync(string email, string replacement)
    {
        if ((replacement ?? "").Length < MinPasswordLength)
            return AccountResult.Fail($"Adgangskoden skal være mindst {MinPasswordLength} tegn.");

        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == Normalise(email));
        if (user is null) return AccountResult.Fail("Brugeren findes ikke.");

        user.PasswordHash = PasswordHasher.Hash(replacement!);
        await db.SaveChangesAsync();
        return AccountResult.Success(user);
    }

    /// <summary>
    /// Deletes the account and, for the last person in a household, the household with it:
    /// settings, meter token, run log, consumption and every advisor grant. While others still
    /// share the household only this login goes. Returns true when the household itself was deleted.
    /// </summary>
    public async Task<bool> DeleteAccountAsync(Guid userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(userId);
        if (user is null) return false;

        if (user.HouseholdId is { } shared && await db.Users.AnyAsync(u => u.Id != userId && u.HouseholdId == shared))
        {
            db.Users.Remove(user);
            await db.SaveChangesAsync();
            return false;
        }

        if (user.HouseholdId is { } householdId)
        {
            var household = await db.Households.FindAsync(householdId);
            if (household is not null) db.Households.Remove(household);

            var grants = await db.AdvisorAccess.Where(a => a.HouseholdId == householdId).ToListAsync();
            db.AdvisorAccess.RemoveRange(grants);
        }
        else
        {
            // An advisor leaving takes their own access with them, never the
            // customers' data.
            var grants = await db.AdvisorAccess.Where(a => a.AdvisorUserId == userId).ToListAsync();
            foreach (var grant in grants) grant.RevokedUtc = DateTime.UtcNow;
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Adds another login to an existing household (a partner, a grown-up child) - same meter,
    /// same machines. No mail server exists, so a temporary password is made here and shown once.
    /// </summary>
    public async Task<(AccountResult Result, string? Password)> AddMemberAsync(Guid householdId, string email, string displayName)
    {
        var password = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(9))
            .Replace('+', 'x').Replace('/', 'y');
        var check = Validate(email, password);
        if (check is not null) return (check, null);

        email = Normalise(email);
        await using var db = await factory.CreateDbContextAsync();
        if (await db.Users.AnyAsync(u => u.Email == email))
            return (AccountResult.Fail("Der findes allerede en bruger med den mail."), null);
        if (await db.Households.FindAsync(householdId) is null)
            return (AccountResult.Fail("Hjemmet findes ikke."), null);

        var user = new UserAccount
        {
            Email = email,
            PasswordHash = PasswordHasher.Hash(password),
            Role = UserRoles.Owner,
            HouseholdId = householdId,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? email : displayName.Trim(),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (AccountResult.Success(user), password);
    }

    public async Task<List<UserAccount>> MembersAsync(Guid householdId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Users.Where(u => u.HouseholdId == householdId).OrderBy(u => u.CreatedAtUtc).ToListAsync();
    }

    /// <summary>Takes another login out of the household. Never the one asking, never the household's data.</summary>
    public async Task<bool> RemoveMemberAsync(Guid householdId, Guid memberId, Guid askingUserId)
    {
        if (memberId == askingUserId) return false;
        await using var db = await factory.CreateDbContextAsync();
        var member = await db.Users.FirstOrDefaultAsync(u => u.Id == memberId && u.HouseholdId == householdId);
        if (member is null) return false;
        db.Users.Remove(member);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<UserAccount?> FindAsync(Guid userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Users.FindAsync(userId);
    }

    public async Task<List<UserAccount>> AllUsersAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Users.OrderBy(u => u.Email).ToListAsync();
    }

    private static AccountResult? Validate(string email, string password)
    {
        if (!LooksLikeEmail(Normalise(email))) return AccountResult.Fail("Skriv en rigtig mailadresse.");
        if ((password ?? "").Length < MinPasswordLength)
            return AccountResult.Fail($"Adgangskoden skal være mindst {MinPasswordLength} tegn.");
        return null;
    }
}
