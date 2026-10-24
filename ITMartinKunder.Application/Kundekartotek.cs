using Microsoft.Extensions.DependencyInjection;

namespace ITMartinKunder.Application;

/// <summary>The customers, with their consents and events. Implemented in ITMartinKunder.Infrastructure.</summary>
public interface ICustomerRepository
{
    Task<Customer?> ByEmailAsync(string email, CancellationToken ct);
    Task<Customer?> ByTokenAsync(string token, CancellationToken ct);
    Task<Customer?> GetAsync(int id, CancellationToken ct);
    Task<Customer?> ByCodeAsync(string code, CancellationToken ct);
    Task<bool> CodeTakenAsync(string code, CancellationToken ct);
    Task<List<Customer>> AllAsync(CancellationToken ct);
    /// <summary>Adds (Id 0) or saves the customer with its consents and events.</summary>
    Task SaveAsync(Customer customer, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

// One Kundekartotek for every app (user 2026-10-21). The apps call the API (through ITMartinKunder.Client);
// Martin uses the list page.
public sealed class Kundekartotek(ICustomerRepository repo, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <returns>Error for the person (Danish), or null when they are on the list.</returns>
    public async Task<string?> SignUpAsync(SignUpRequest r, CancellationToken ct = default) => (await SignUpWithCodeAsync(r, ct)).Error;

    /// <returns>Error for the person, or their kundenummer (the same one every time they sign up).</returns>
    public async Task<(string? Error, string Code)> SignUpWithCodeAsync(SignUpRequest r, CancellationToken ct = default)
    {
        var email = KundeWords.Normalize(r.Email);
        if (!KundeWords.IsRealEmail(email) || email.Length > Customer.MaxEmail) return ("Skriv en rigtig e-mail, fx navn@gmail.com.", "");
        if (r.ConsentText.Trim().Length == 0) return ("Sæt flueben, hvis du vil skrives op som kunde.", "");
        var c = await repo.ByEmailAsync(email, ct) ?? Customer.New(email, r.Name, Now);
        var code = await FreeCodeAsync(ct);
        c.SignUp(r with { Email = email }, Now, () => code);
        await repo.SaveAsync(c, ct);
        return (null, c.Code);
    }

    /// <summary>ElPriser/Stem ask: may this kundenummer use the extras in this app?</summary>
    public async Task<CodeCheckResponse> CheckCodeAsync(string? code, string app, IReadOnlyList<FreeApp> freeApps, CancellationToken ct = default)
    {
        var k = KundeWords.NormalizeCode(code);
        if (k.Length != 8) return new(false, Error: "Dit kundenummer har 8 cifre – du finder det i mailen fra Martin.");
        if (!freeApps.Any(a => a.Available && string.Equals(a.Name, app.Trim(), StringComparison.OrdinalIgnoreCase)))
            return new(false, Error: "Den app er ikke med i de gratis apps lige nu.");
        var c = await repo.ByCodeAsync(k, ct);
        if (c is null) return new(false, Error: "Det kundenummer kender vi ikke. Tjek det, eller skriv dig op gratis på itmartin.dk.");
        if (!c.CodeWorks) return new(false, Error: "Du er afmeldt. Skriv dig op igen på itmartin.dk, så virker dit kundenummer igen.");
        return new(true, c.Name);
    }

    public async Task<string?> BoughtAsync(PurchaseRequest r, CancellationToken ct = default)
    {
        var email = KundeWords.Normalize(r.Email);
        if (!KundeWords.IsRealEmail(email)) return "Køb uden e-mail kommer ikke i kartoteket.";
        var c = await repo.ByEmailAsync(email, ct) ?? Customer.New(email, r.Name, r.AtUtc);
        c.Bought(r);
        await repo.SaveAsync(c, ct);
        return null;
    }

    /// <returns>True when the e-mail was on the list (the person gets the same answer either way).</returns>
    public async Task<bool> UnsubscribeAsync(string? email, CancellationToken ct = default)
    {
        var e = KundeWords.Normalize(email);
        if (e.Length == 0 || await repo.ByEmailAsync(e, ct) is not { } c) return false;
        c.Unsubscribe(Now);
        await repo.SaveAsync(c, ct);
        return true;
    }

    /// <returns>The e-mail that is now unsubscribed, or null when the link is wrong.</returns>
    public async Task<string?> UnsubscribeByLinkAsync(string token, CancellationToken ct = default)
    {
        if (token.Length < 16 || await repo.ByTokenAsync(token, ct) is not { } c) return null;
        c.Unsubscribe(Now);
        await repo.SaveAsync(c, ct);
        return c.Email;
    }

    /// <summary>A customer from an app's old list. Already here = left alone (Kunder's own data is newer).</summary>
    public async Task<string?> ImportAsync(ImportRequest r, CancellationToken ct = default)
    {
        var email = KundeWords.Normalize(r.Email);
        if (!KundeWords.IsRealEmail(email)) return "Ingen rigtig e-mail.";
        if (await repo.ByEmailAsync(email, ct) is not null) return null;
        var c = Customer.New(email, r.Name, r.CreatedAt);
        c.Phone = r.Phone; c.Note = r.Note; c.AddInterests(r.Interests);
        if (r.UnsubscribeToken.Length >= 16 && await repo.ByTokenAsync(r.UnsubscribeToken, ct) is null) c.UnsubscribeToken = r.UnsubscribeToken;
        c.Consents.Add(new Consent
        {
            Company = Companies.All.Contains(r.Company) ? r.Company : Companies.ITMartin, Text = r.ConsentText, Source = r.Source,
            At = r.ConsentAt, WithdrawnAt = r.UnsubscribedAt,
        });
        c.UnsubscribedAt = r.UnsubscribedAt;
        c.Code = await FreeCodeAsync(ct);
        c.Events.Add(CustomerEvent.Of(EventKinds.Imported, r.App, "", r.Source, 0, r.ConsentAt));
        await repo.SaveAsync(c, ct);
        return null;
    }

    // ---- Martin's list ----

    public async Task<List<Customer>> ListAsync(CancellationToken ct = default) =>
        (await repo.AllAsync(ct)).OrderByDescending(c => c.Events.Select(e => e.At).DefaultIfEmpty(c.CreatedAt).Max()).ToList();

    public async Task AdminUnsubscribeAsync(int id, CancellationToken ct = default)
    {
        if (await repo.GetAsync(id, ct) is not { } c) return;
        c.Unsubscribe(Now);
        await repo.SaveAsync(c, ct);
    }

    public async Task SaveDetailsAsync(int id, string name, string email, string phone, string note, CancellationToken ct = default)
    {
        if (await repo.GetAsync(id, ct) is not { } c) throw new KunderException("Kunden findes ikke.");
        var e = KundeWords.Normalize(email);
        if (!KundeWords.IsRealEmail(e)) throw new KunderException("Skriv en rigtig e-mail.");
        if (e != c.Email && await repo.ByEmailAsync(e, ct) is not null) throw new KunderException("Der er allerede en kunde med den e-mail.");
        c.ChangeDetails(name, e, phone, note);
        await repo.SaveAsync(c, ct);
    }

    /// <summary>GDPR erasure: the person is gone from the list, everything about them.</summary>
    public Task DeleteAsync(int id, CancellationToken ct = default) => repo.DeleteAsync(id, ct);

    /// <summary>The nightly clean-up (Retention): afmeldt customers after 30 days, purchases after 5 years.</summary>
    /// <returns>How many were deleted.</returns>
    public async Task<int> CleanUpAsync(CancellationToken ct = default)
    {
        var gone = (await repo.AllAsync(ct)).Where(c => Retention.ShouldDelete(c, Now)).ToList();
        foreach (var c in gone) await repo.DeleteAsync(c.Id, ct);
        return gone.Count;
    }

    /// <summary>"Hent mine data" (GDPR art. 15/20): everything Kunder has about one person, for Martin to send them.</summary>
    public async Task<CustomerExport?> ExportAsync(int id, CancellationToken ct = default) =>
        await repo.GetAsync(id, ct) is not { } c ? null : new CustomerExport(
            c.Email, c.Name, c.Phone, c.Interests, c.Note, c.Code, c.CreatedAt, c.UnsubscribedAt,
            c.Consents.Select(x => new ConsentExport(x.Company, x.Text, x.Source, x.At, x.WithdrawnAt)).ToList(),
            c.Events.OrderBy(e => e.At).Select(e => new EventExport(e.Kind, e.App, e.What, e.Kr, e.At)).ToList(),
            $"Det her er alt, hvad ITMartin og Bogshoppen har gemt om dig i kundekartoteket. Afmeldt slettes efter {Retention.UnsubscribedKeepDays} dage, " +
            $"køb gemmes i {Retention.PurchaseKeepYears} år (bogføringsloven). Se itmartin.dk/vilkaar.");

    /// <summary>Only people with a yes to that company that still stands - for the Bcc line or a mail tool.</summary>
    public async Task<List<Customer>> ActiveAsync(string company, CancellationToken ct = default) =>
        (await repo.AllAsync(ct)).Where(c => c.SaidYesTo(company)).OrderBy(c => c.Name).ToList();

    private async Task<string> FreeCodeAsync(CancellationToken ct)
    {
        string code;
        do code = Customer.NewCode(); while (await repo.CodeTakenAsync(code, ct));
        return code;
    }
}

public interface IFreeAppRepository
{
    Task<List<FreeApp>> AllAsync(CancellationToken ct);
    /// <summary>Adds (Id 0) or saves.</summary>
    Task SaveAsync(FreeApp app, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

// The free apps a sign-up gives access to - Martin decides which (user 2026-10-21).
public sealed class FreeApps(IFreeAppRepository repo)
{
    public async Task<List<FreeApp>> AllAsync(CancellationToken ct = default) =>
        (await repo.AllAsync(ct)).OrderBy(a => a.Sort).ThenBy(a => a.Name).ToList();

    /// <summary>The ones that are on, for the sign-up boxes on the sites.</summary>
    public async Task<List<FreeAppInfo>> AvailableAsync(CancellationToken ct = default) =>
        (await AllAsync(ct)).Where(a => a.Available).Select(a => new FreeAppInfo(a.Name, a.Url, a.Text, a.NeedsCode)).ToList();

    public async Task SaveAsync(FreeApp changed, CancellationToken ct = default)
    {
        var app = changed.Id == 0 ? new FreeApp() : (await repo.AllAsync(ct)).FirstOrDefault(a => a.Id == changed.Id)
                  ?? throw new KunderException("Appen findes ikke.");
        try { app.Change(changed.Name, changed.Url, changed.Text, changed.Available, changed.NeedsCode, changed.Sort); }
        catch (ArgumentException ex) { throw new KunderException(ex.Message); }
        await repo.SaveAsync(app, ct);
    }

    public Task DeleteAsync(int id, CancellationToken ct = default) => repo.DeleteAsync(id, ct);
}

public sealed record CustomerExport(string Email, string Name, string Phone, string Interests, string MartinsNote, string Kundenummer,
    DateTime CreatedAtUtc, DateTime? UnsubscribedAtUtc, List<ConsentExport> Samtykker, List<EventExport> Haendelser, string Forklaring);
public sealed record ConsentExport(string Company, string Text, string Source, DateTime AtUtc, DateTime? WithdrawnAtUtc);
public sealed record EventExport(string Kind, string App, string What, int Kr, DateTime AtUtc);

/// <summary>Sends one plain-text mail. False = not sent (no password set, or the mail server said no).</summary>
public interface IMailSender
{
    Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

// Letters from Martin to the customers (user 2026-10-24), one mail per person with their own kundenummer and afmeld link.
// Nothing goes out until Martin presses send on the page; each letter is sent once per customer.
public sealed class KundeBreve(ICustomerRepository repo, IMailSender mail, TimeProvider clock)
{
    /// <summary>The customers still waiting for this letter, oldest sign-up first.</summary>
    public async Task<List<Customer>> WaitingAsync(string key, CancellationToken ct = default) =>
        (await repo.AllAsync(ct)).Where(c => Letters.Waits(c, key)).OrderBy(c => c.CreatedAt).ToList();

    /// <returns>True when mailed now; false when it could not be sent (they stay waiting) or they no longer wait.</returns>
    public async Task<bool> SendReceivedAsync(int customerId, string kunderSite, CancellationToken ct = default)
    {
        if (await repo.GetAsync(customerId, ct) is not { } c || !Letters.Waits(c, Letters.Received)) return false;
        var (subject, body) = Letters.ReceivedMail(c, kunderSite);
        if (!await mail.SendAsync(c.Email, subject, body, ct)) return false;
        c.Events.Add(Letters.Sent(Letters.Received, subject, clock.GetUtcNow().UtcDateTime));
        await repo.SaveAsync(c, ct);
        return true;
    }
}

public sealed class KunderException(string message) : Exception(message);

public static class DependencyInjection
{
    /// <summary>The use cases. The repository comes from AddKunderInfrastructure.</summary>
    public static IServiceCollection AddKunderApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<Kundekartotek>();
        services.AddScoped<FreeApps>();
        services.AddScoped<KundeBreve>();
        return services;
    }
}
