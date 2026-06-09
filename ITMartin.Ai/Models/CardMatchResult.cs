public sealed class CardMatchResult
{
    public CardMatchCandidate? BestMatch { get; set; }

    public List<CardMatchCandidate> Candidates { get; set; } = [];
}