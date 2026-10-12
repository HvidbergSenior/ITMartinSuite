using ITMartinHjem.Domain;

namespace ITMartinHjem.Application;

// The Kundekartotek lives in Stem (poll.db Customers); Hjem signs people up and off through Stem's API.
public interface ICustomerGateway
{
    /// <returns>Error for the person (Danish), or null when they are on the list.</returns>
    Task<string?> SignUpAsync(string email, string name, string source, string consentText, string interests, CancellationToken ct = default);
    /// <returns>True when the e-mail was on the list and is now unsubscribed.</returns>
    Task<bool> UnsubscribeAsync(string email, CancellationToken ct = default);
}

// Martin's inbox (the chat in Svar): a new message there pushes to his phone, and his reply is mailed to the person.
public interface IOwnerInbox
{
    Task NewRequestAsync(string name, string email, string text, CancellationToken ct = default);
}

public sealed class Customers(ICustomerGateway kartotek, IOwnerInbox inbox)
{
    public const string Unreachable = "Det kunne ikke gemmes lige nu – prøv igen om lidt, eller skriv til ITMartin@Mensa.dk.";

    /// <summary>"Prøv gratis": customer in the Kundekartotek + a request in Martin's inbox. Needs the ticked consent.</summary>
    public async Task<string?> RequestTrialAsync(string app, string? name, string? email, bool consent, CancellationToken ct = default)
    {
        var e = (email ?? "").Trim().ToLowerInvariant();
        var n = (name ?? "").Trim();
        if (e.Length == 0) return "Skriv din e-mail, så kan Martin sende dig adgang.";
        if (e.Length > TrialRules.MaxEmail || n.Length > TrialRules.MaxName) return "Navn eller e-mail er for lang.";
        if (!consent) return "Sæt flueben, hvis du vil skrives op som kunde – ellers kan du skrive til Martin i chatten.";
        try
        {
            var error = await kartotek.SignUpAsync(e, n, TrialRules.Source(app), TrialRules.ConsentText(app), app, ct);
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
        try { return await kartotek.SignUpAsync(e, n, BogshoppenRules.Source, BogshoppenRules.ConsentText, BogshoppenRules.Interests(collects, note), ct); }
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
