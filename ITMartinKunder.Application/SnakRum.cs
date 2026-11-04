namespace ITMartinKunder.Application;

/// <summary>Asks Snak (snak.itmartin.dk) for a new room. Null link = Snak said no or could not be reached (Error says why, in Danish).
/// Implemented in ITMartinKunder.Infrastructure.</summary>
public interface ISnakRooms
{
    Task<(string? Link, string? Error)> CreateAsync(string title, string customerRef, CancellationToken ct);
}

// Snak step 2 (user 2026-11-04: "the free one should be created ... maybe even on creation of customer"): Martin presses
// "Lav snak-rum" on a customer, Snak makes the room, the link sits on the card - Martin sends it, all talk goes there.
public sealed class SnakRum(ICustomerRepository repo, ISnakRooms snak, TimeProvider clock)
{
    /// <returns>Error for Martin (Danish), or null when the customer now has a room link.</returns>
    public async Task<string?> MakeAsync(int customerId, CancellationToken ct = default)
    {
        if (await repo.GetAsync(customerId, ct) is not { } c) return "Kunden findes ikke.";
        var (link, error) = await snak.CreateAsync(c.Name == "" ? c.Email : c.Name, $"kunde-{c.Id}", ct);
        if (link is null) return error ?? "Snak lavede ikke rummet. Prøv igen om lidt.";
        c.GotSnakRoom(link, clock.GetUtcNow().UtcDateTime);
        await repo.SaveAsync(c, ct);
        return null;
    }
}
