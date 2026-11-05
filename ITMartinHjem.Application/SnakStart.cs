using System.Collections.Concurrent;
using ITMartinHjem.Domain;

namespace ITMartinHjem.Application;

/// <summary>Asks Snak (snak.itmartin.dk) for a new room with the visitor's first message in it.
/// Null link = Snak said no or could not be reached. Implemented in ITMartinHjem.Infrastructure.</summary>
public interface ISnakRooms
{
    Task<string?> CreateAsync(string title, string name, string firstMessage, CancellationToken ct = default);
}

// "Skriv til mig" -> one Snak room per person (user 2026-11-05). Martin's phone gets the push from Snak itself.
public sealed class SnakStartUseCases(ISnakRooms snak, TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, (DateOnly Day, int Count)> _perAddress = new();

    public sealed record Result(string? Link, string? Error);

    public async Task<Result> StartAsync(string name, string text, string address, CancellationToken ct = default)
    {
        name = (name ?? "").Trim();
        text = (text ?? "").Trim();
        if (SnakStartRules.Check(name, text) is { } bad) return new(null, bad);

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var used = _perAddress.TryGetValue(address, out var u) && u.Day == today ? u.Count : 0;
        if (used >= SnakStartRules.PerDay)
            return new(null, "Du har startet mange snakke i dag. Ring eller skriv en mail, så svarer jeg derfra.");

        var link = await snak.CreateAsync(name, name, text, ct);
        if (link is null) return new(null, "Det virkede ikke lige nu. Prøv igen om lidt – eller ring eller skriv en mail.");
        _perAddress[address] = (today, used + 1);
        return new(link, null);
    }
}
