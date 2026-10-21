using ITMartinHjem.Domain;

namespace ITMartinHjem.Application;

/// <summary>Who they said yes to: ITMartin (all free apps) or Bogshoppen.</summary>
public enum CustomerOf { ITMartin, Bogshoppen }

public sealed record CustomerSignUp(string Email, string Name, CustomerOf Company, string App, string Source, string ConsentText, string Interests);

// The Kundekartotek lives in Kunder (2026-10-21, one list for all apps); Hjem signs people up and off through its API.
// The adapter keeps a sign-up in an outbox when Kunder is down, so the person is never turned away for that.
public interface ICustomerGateway
{
    /// <returns>Error for the person (Danish), or null when they are on the list.</returns>
    Task<string?> SignUpAsync(CustomerSignUp signUp, CancellationToken ct = default);
    Task UnsubscribeAsync(string email, CancellationToken ct = default);
    /// <summary>The names of the free apps Martin has switched on in Kunder.</summary>
    Task<IReadOnlyList<string>> FreeAppsAsync(CancellationToken ct = default);
}

// Martin's inbox (the chat in Svar): a new message there pushes to his phone, and his reply is mailed to the person.
public interface IOwnerInbox
{
    Task NewRequestAsync(string name, string email, string text, CancellationToken ct = default);
}

public sealed class Customers(ICustomerGateway kartotek, IOwnerInbox inbox)
{
    public const string Unreachable = "Det kunne ikke gemmes lige nu – prøv igen om lidt, eller skriv til ITMartin@Mensa.dk.";

    /// <summary>The apps a sign-up gives (the ones Martin has switched on in Kunder).</summary>
    public Task<IReadOnlyList<string>> FreeAppsAsync(CancellationToken ct = default) => kartotek.FreeAppsAsync(ct);

    /// <summary>"Prøv gratis": customer in the Kundekartotek + a request in Martin's inbox. Needs the ticked consent and a free app.</summary>
    public async Task<string?> RequestTrialAsync(string app, string? name, string? email, bool consent, CancellationToken ct = default)
    {
        var e = (email ?? "").Trim().ToLowerInvariant();
        var n = (name ?? "").Trim();
        if (e.Length == 0) return "Skriv din e-mail, så kan Martin sende dig adgang.";
        if (e.Length > TrialRules.MaxEmail || n.Length > TrialRules.MaxName) return "Navn eller e-mail er for lang.";
        if (!consent) return "Sæt flueben, hvis du vil skrives op som kunde – ellers kan du skrive til Martin i chatten.";
        if (!TrialRules.IsFree(app, await kartotek.FreeAppsAsync(ct))) return TrialRules.NotFree;
        try
        {
            var error = await kartotek.SignUpAsync(new(e, n, CustomerOf.ITMartin, app, TrialRules.Source(app), TrialRules.ConsentText, app), ct);
            if (error is not null) return error;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return Unreachable; }
        await inbox.NewRequestAsync(n, e, TrialRules.RequestMessage(app, e), ct);
        return null;
    }

    /// <summary>bogshoppen.dk "Sådan følger du med": customer for the live auctions, with what they collect. Needs the ticked consent.</summary>
    public async Task<string?> FollowBogshoppenAsync(string? name, string? email, IEnumerable<string> collects, string? note, bool consent, CancellationToken ct = default)
    {
        var e = (email ?? "").Trim().ToLowerInvariant();
        var n = (name ?? "").Trim();
        if (e.Length == 0) return "Skriv din e-mail, så kan vi sende dig et login til liveauktionerne.";
        if (e.Length > TrialRules.MaxEmail || n.Length > TrialRules.MaxName) return "Navn eller e-mail er for lang.";
        if (!consent) return "Sæt flueben, hvis du vil skrives op som kunde.";
        try
        {
            return await kartotek.SignUpAsync(new(e, n, CustomerOf.Bogshoppen, "bogshoppen.dk", BogshoppenRules.Source, BogshoppenRules.ConsentText,
                BogshoppenRules.Interests(collects, note)), ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return Unreachable; }
    }

    /// <returns>Null = unsubscribed (or was never on the list - same answer, so nobody can test which e-mails exist).</returns>
    public async Task<string?> UnsubscribeAsync(string? email, CancellationToken ct = default)
    {
        var e = (email ?? "").Trim().ToLowerInvariant();
        if (e.Length == 0 || !e.Contains('@')) return "Skriv den e-mail, du skrev dig op med.";
        try { await kartotek.UnsubscribeAsync(e, ct); return null; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return Unreachable; }
    }
}
