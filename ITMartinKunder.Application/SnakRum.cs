namespace ITMartinKunder.Application;

/// <summary>Asks Snak (snak.itmartin.dk) for a new room, and asks it to mail the link. Implemented in ITMartinKunder.Infrastructure.</summary>
public interface ISnakRooms
{
    /// <summary>Null link = Snak said no or could not be reached (Error says why, in Danish).</summary>
    Task<(string? Link, string? Error)> CreateAsync(string title, string customerRef, CancellationToken ct);

    /// <summary>Snak mails the link to the customer and keeps the address for "Martin har skrevet" mails. Null = sent, else why not.</summary>
    Task<string?> InviteAsync(string link, string mail, string name, CancellationToken ct);
}

// Snak step 2 (user 2026-11-04: "the free one should be created ... maybe even on creation of customer"): Martin presses
// "Lav snak-rum" on a customer, Snak makes the room, the link sits on the card.
// 2026-11-10 (user: "Will he receive the message?" / "also for existing customers"): the link goes out by mail at once,
// "Send linket på mail" sends it from here (the mailto button opened Windows Mail, not Gmail), and older customers
// with a room get it with one press of "Send til alle".
public sealed class SnakRum(ICustomerRepository repo, ISnakRooms snak, TimeProvider clock)
{
    /// <returns>Error for Martin (Danish), or null when the customer now has a room link (and got it by mail, if they have a mail).</returns>
    public async Task<string?> MakeAsync(int customerId, CancellationToken ct = default)
    {
        if (await repo.GetAsync(customerId, ct) is not { } c) return "Kunden findes ikke.";
        var (link, error) = await snak.CreateAsync(c.Name == "" ? c.Email : c.Name, $"kunde-{c.Id}", ct);
        if (link is null) return error ?? "Snak lavede ikke rummet. Prøv igen om lidt.";
        c.GotSnakRoom(link, clock.GetUtcNow().UtcDateTime);
        await repo.SaveAsync(c, ct);
        if (!c.WaitsForSnakLink) return null;
        var mailError = await SendLinkAsync(customerId, ct);
        return mailError is null ? null : "Rummet er lavet, men linket kom ikke ud på mail: " + mailError;
    }

    /// <returns>Null when mailed now, else why not (Danish).</returns>
    public async Task<string?> SendLinkAsync(int customerId, CancellationToken ct = default)
    {
        if (await repo.GetAsync(customerId, ct) is not { } c) return "Kunden findes ikke.";
        if (c.SnakLink == "") return "Kunden har ikke et snak-rum endnu.";
        if (c.Email == "") return "Kunden har ingen mailadresse.";
        if (!c.Active) return "Kunden er afmeldt.";
        var error = await snak.InviteAsync(c.SnakLink, c.Email, c.Name, ct);
        if (error is not null) return error;
        c.SentSnakLink(clock.GetUtcNow().UtcDateTime);
        await repo.SaveAsync(c, ct);
        return null;
    }

    /// <summary>Older customers: everyone with a room who has not had the link by mail. Returns (sent, failed).</summary>
    public async Task<(int Sent, int Failed)> SendAllWaitingAsync(CancellationToken ct = default)
    {
        var waiting = (await repo.AllAsync(ct)).Where(c => c.WaitsForSnakLink).Select(c => c.Id).ToList();
        int sent = 0, failed = 0;
        foreach (var id in waiting)
            if (await SendLinkAsync(id, ct) is null) sent++; else failed++;
        return (sent, failed);
    }
}
