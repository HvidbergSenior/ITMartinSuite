using ITMartinElPriser.Application;
using ITMartinElPriser.Domain;
using ITMartinMitEl.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ITMartinMitEl.Server.Services;

/// <summary>The reset flow's account port on MinElpris's own database (mitel.db). The rules live in
/// PasswordResetUseCases / PasswordResetTicket; this only reads and writes.</summary>
public sealed class ResetAccounts(IDbContextFactory<MitElDbContext> factory) : IResetAccounts
{
    public async Task<string?> FindNameAsync(string email)
    {
        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == AccountService.Normalise(email));
        return user is null ? null : user.DisplayName ?? "";
    }

    public async Task<DateTime?> LastTicketUtcAsync(string email)
    {
        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == AccountService.Normalise(email));
        if (user is null) return null;
        var times = await db.PasswordResets.AsNoTracking().Where(r => r.UserId == user.Id).Select(r => r.CreatedAtUtc).ToListAsync();
        return times.Count == 0 ? null : times.Max();
    }

    public async Task SaveTicketAsync(string email, string tokenHash, DateTime createdUtc, DateTime expiresUtc)
    {
        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == AccountService.Normalise(email));
        if (user is null) return;
        // Old, finished tickets are not kept.
        var old = await db.PasswordResets.Where(r => r.UserId == user.Id).ToListAsync();
        db.PasswordResets.RemoveRange(old.Where(r => !PasswordResetTicket.Usable(r.ExpiresAtUtc, r.UsedAtUtc, createdUtc)));
        db.PasswordResets.Add(new PasswordReset { UserId = user.Id, TokenHash = tokenHash, CreatedAtUtc = createdUtc, ExpiresAtUtc = expiresUtc });
        await db.SaveChangesAsync();
    }

    public async Task<bool> TicketUsableAsync(string tokenHash, DateTime nowUtc)
    {
        await using var db = await factory.CreateDbContextAsync();
        var t = await db.PasswordResets.AsNoTracking().FirstOrDefaultAsync(r => r.TokenHash == tokenHash);
        return t is not null && PasswordResetTicket.Usable(t.ExpiresAtUtc, t.UsedAtUtc, nowUtc);
    }

    public async Task<bool> UseTicketAsync(string tokenHash, string newPassword, DateTime nowUtc)
    {
        await using var db = await factory.CreateDbContextAsync();
        var t = await db.PasswordResets.FirstOrDefaultAsync(r => r.TokenHash == tokenHash);
        if (t is null || !PasswordResetTicket.Usable(t.ExpiresAtUtc, t.UsedAtUtc, nowUtc)) return false;
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == t.UserId);
        if (user is null) return false;

        user.PasswordHash = PasswordHasher.Hash(newPassword);
        t.UsedAtUtc = nowUtc;
        db.PasswordResets.RemoveRange(await db.PasswordResets.Where(r => r.UserId == user.Id && r.Id != t.Id).ToListAsync());
        await db.SaveChangesAsync();
        return true;
    }
}
