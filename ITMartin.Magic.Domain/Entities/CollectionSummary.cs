namespace ITMartin.Magic.Domain.Entities;

public sealed record CollectionSummary
{
    public int TotalCards { get; init; }

    public int UniqueCards { get; init; }

    public decimal TotalEurValue { get; init; }

    public decimal TotalUsdValue { get; init; }
}