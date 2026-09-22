using System.Security.Cryptography;
using ITMartinMitEl.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ITMartinMitEl.Server.Services;

public sealed record AdvisorCustomer(Guid HouseholdId, string HouseholdName, string OwnerEmail, DateTime GrantedUtc, DateTime ExpiresUtc);

public sealed record GrantView(Guid Id, string Code, string AdvisorName, DateTime CreatedUtc, DateTime ExpiresUtc, DateTime? RedeemedUtc, DateTime? RevokedUtc)
{
    public bool IsWaiting => RedeemedUtc is null && RevokedUtc is null;
}

/// <summary>
/// The bridge between a customer and Martin: the customer makes a code, the advisor
/// redeems it once, and the customer can take it back whenever they like. An advisor
/// never reaches a household any other way.
/// </summary>
public sealed class AdvisorService(IDbContextFactory<MitElDbContext> factory)
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no O/0, I/1: it gets read aloud
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan GrantLifetime = TimeSpan.FromDays(365);

    /// <summary>A fresh code for the customer to hand over. Readable over the phone.</summary>
    public async Task<string> CreateCodeAsync(Guid householdId)
    {
        await using var db = await factory.CreateDbContextAsync();

        string code;
        do { code = NewCode(); }
        while (await db.AdvisorAccess.AnyAsync(a => a.Code == code));

        db.AdvisorAccess.Add(new AdvisorAccess
        {
            HouseholdId = householdId,
            Code = code,
            ExpiresUtc = DateTime.UtcNow.Add(CodeLifetime),
        });
        await db.SaveChangesAsync();
        return code;
    }

    public async Task<(bool Ok, string Message)> RedeemAsync(string code, Guid advisorUserId)
    {
        code = (code ?? "").Trim().ToUpperInvariant().Replace("-", "");
        await using var db = await factory.CreateDbContextAsync();

        var grant = await db.AdvisorAccess.FirstOrDefaultAsync(a => a.Code == code);
        if (grant is null) return (false, "Koden findes ikke.");
        if (grant.RevokedUtc is not null) return (false, "Koden er trukket tilbage af kunden.");
        if (grant.AdvisorUserId is not null) return (false, "Koden er allerede brugt.");
        if (DateTime.UtcNow >= grant.ExpiresUtc) return (false, "Koden er udløbet. Bed kunden lave en ny.");

        grant.AdvisorUserId = advisorUserId;
        grant.RedeemedUtc = DateTime.UtcNow;
        grant.ExpiresUtc = DateTime.UtcNow.Add(GrantLifetime);
        await db.SaveChangesAsync();
        return (true, "Adgang oprettet.");
    }

    /// <summary>The customer taking their data back. Works on a used code and an unused one alike.</summary>
    public async Task<bool> RevokeAsync(Guid householdId, Guid grantId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var grant = await db.AdvisorAccess.FirstOrDefaultAsync(a => a.Id == grantId && a.HouseholdId == householdId);
        if (grant is null) return false;

        grant.RevokedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>The single question every advisor page must ask before showing anything.</summary>
    public async Task<bool> HasAccessAsync(Guid advisorUserId, Guid householdId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var grants = await db.AdvisorAccess
            .Where(a => a.AdvisorUserId == advisorUserId && a.HouseholdId == householdId)
            .ToListAsync();
        return grants.Any(g => g.IsActive(DateTime.UtcNow));
    }

    /// <summary>Synchronous twin of <see cref="HasAccessAsync"/>, for resolving the store during DI.</summary>
    public bool HasAccess(Guid advisorUserId, Guid householdId)
    {
        using var db = factory.CreateDbContext();
        var now = DateTime.UtcNow;
        return db.AdvisorAccess
            .Where(a => a.AdvisorUserId == advisorUserId && a.HouseholdId == householdId)
            .AsEnumerable()
            .Any(g => g.IsActive(now));
    }

    public async Task<List<AdvisorCustomer>> CustomersAsync(Guid advisorUserId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;

        var grants = await db.AdvisorAccess
            .Where(a => a.AdvisorUserId == advisorUserId)
            .ToListAsync();

        var result = new List<AdvisorCustomer>();
        foreach (var grant in grants.Where(g => g.IsActive(now)))
        {
            var household = await db.Households.FindAsync(grant.HouseholdId);
            if (household is null) continue;
            var owner = await db.Users.FirstOrDefaultAsync(u => u.HouseholdId == grant.HouseholdId);

            result.Add(new AdvisorCustomer(
                household.Id, household.Name, owner?.Email ?? "(ukendt)",
                grant.RedeemedUtc ?? grant.CreatedAtUtc, grant.ExpiresUtc));
        }
        return result.OrderBy(c => c.HouseholdName).ToList();
    }

    /// <summary>What the customer sees on their own page: who has access, and codes not yet used.</summary>
    public async Task<List<GrantView>> GrantsForHouseholdAsync(Guid householdId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var grants = await db.AdvisorAccess
            .Where(a => a.HouseholdId == householdId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync();

        var views = new List<GrantView>();
        foreach (var grant in grants)
        {
            var advisor = grant.AdvisorUserId is { } id ? await db.Users.FindAsync(id) : null;
            views.Add(new GrantView(
                grant.Id, grant.Code,
                advisor?.DisplayName ?? advisor?.Email ?? "(ikke brugt endnu)",
                grant.CreatedAtUtc, grant.ExpiresUtc, grant.RedeemedUtc, grant.RevokedUtc));
        }
        return views;
    }

    private static string NewCode()
    {
        var chars = new char[8];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        return new string(chars);
    }

    /// <summary>"ABCD-EFGH" - only for showing; the stored code has no dash.</summary>
    public static string Pretty(string code) =>
        code.Length == 8 ? $"{code[..4]}-{code[4..]}" : code;
}
