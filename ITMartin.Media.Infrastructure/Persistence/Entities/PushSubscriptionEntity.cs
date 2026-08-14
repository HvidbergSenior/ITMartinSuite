namespace ITMartin.Media.Infrastructure.Persistence.Entities;

/// <summary>
/// A browser's Web Push subscription, so FileSorter can notify whoever is
/// running it (single local install, no per-tenant scoping needed) when a
/// background job like a Package1 sort finishes.
/// </summary>
public sealed class PushSubscriptionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Endpoint { get; set; }

    public required string P256DH { get; set; }

    public required string Auth { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
