using ITMartinSalg.Application;
using ITMartinSalg.Domain;
using Microsoft.EntityFrameworkCore;

namespace ITMartinSalg.Infrastructure.Persistence;

// "Lån en konsol" (2026-11-21). A context per call, like the other repositories.
public sealed class LoanRepository(IDbContextFactory<SalgDb> dbf) : ILoanRepository
{
    public async Task AddAsync(ConsoleLoan loan, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Loans.Add(loan);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ConsoleLoan?> GetAsync(int id, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Loans.FirstOrDefaultAsync(l => l.Id == id, ct);
    }

    public async Task SaveAsync(ConsoleLoan loan, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Loans.Update(loan);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<ConsoleLoan>> OfAsync(string customer, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Loans.AsNoTracking().Where(l => l.Customer == customer).OrderByDescending(l => l.Friday).Take(20).ToListAsync(ct);
    }

    public async Task<List<ConsoleLoan>> RecentAsync(DateTime since, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Loans.AsNoTracking()
            .Where(l => (l.Status != ConsoleLoan.Back && l.Status != ConsoleLoan.Cancelled) || l.Friday >= since)
            .OrderBy(l => l.Friday).Take(200).ToListAsync(ct);
    }
}
