using ITMartinSalg.Domain;
using ITMartinVarer.Contracts;

namespace ITMartinSalg.Application;

public interface ILoanRepository
{
    Task AddAsync(ConsoleLoan loan, CancellationToken ct);
    Task<ConsoleLoan?> GetAsync(int id, CancellationToken ct);
    Task SaveAsync(ConsoleLoan loan, CancellationToken ct);
    Task<List<ConsoleLoan>> OfAsync(string customer, CancellationToken ct);
    /// <summary>The open ones (not Afleveret/Aflyst) and the last 30 days' closed ones, Friday first.</summary>
    Task<List<ConsoleLoan>> RecentAsync(DateTime since, CancellationToken ct);
}

/// <summary>"Lån en konsol" (2026-11-21): the customer asks on bogshoppen.dk, Martin confirms, hands out and takes back on the
/// Udlån page. How many consoles he has is his to say - he confirms only what he can lend.</summary>
public sealed class LoanUseCases(ILoanRepository loans, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<ShopOfferAnswer> AskAsync(ShopLoanPlace place, CancellationToken ct = default)
    {
        try
        {
            var loan = ConsoleLoan.Ask(place.Customer, place.Console, place.Friday, place.WithTenDvds, Now);
            if ((await loans.OfAsync(loan.Customer, ct)).Any(l => l.Friday == loan.Friday && l.Status is not (ConsoleLoan.Back or ConsoleLoan.Cancelled)))
                return new(false, "Du har allerede bestilt et lån den weekend.");
            await loans.AddAsync(loan, ct);
            return new(true, null, loan.PriceKr);
        }
        catch (InvalidOperationException e) { return new(false, e.Message); }
    }

    public async Task<List<ShopLoanView>> MineAsync(string? customer, CancellationToken ct = default) =>
        Kundenummer.Normalise(customer) is { } code
            ? (await loans.OfAsync(code, ct)).Select(l => new ShopLoanView(l.Id, l.Console, l.Friday, l.PriceKr, l.Status)).ToList()
            : [];

    public Task<List<ConsoleLoan>> OverviewAsync(CancellationToken ct = default) => loans.RecentAsync(Now.Date.AddDays(-30), ct);

    /// <summary>Martin's buttons: bekraeft / udlever / tilbage / aflys. Null = done, else the reason.</summary>
    public async Task<string?> MoveAsync(int id, string action, CancellationToken ct = default)
    {
        if (await loans.GetAsync(id, ct) is not { } l) return "Lånet findes ikke.";
        try
        {
            switch (action)
            {
                case "bekraeft": l.Confirm(Now); break;
                case "udlever": l.HandOut(Now); break;
                case "tilbage": l.Return(Now); break;
                case "aflys": l.Cancel(Now); break;
                default: return "Ukendt knap.";
            }
            await loans.SaveAsync(l, ct);
            return null;
        }
        catch (InvalidOperationException e) { return e.Message; }
    }

    /// <summary>The customer calls it off before it is handed out.</summary>
    public async Task<ShopOfferAnswer> CancelByCustomerAsync(int id, string? customer, CancellationToken ct = default)
    {
        if (Kundenummer.Normalise(customer) is not { } code || await loans.GetAsync(id, ct) is not { } l || l.Customer != code) return new(false, "Lånet findes ikke.");
        try { l.Cancel(Now); await loans.SaveAsync(l, ct); return new(true); }
        catch (InvalidOperationException e) { return new(false, e.Message); }
    }
}
