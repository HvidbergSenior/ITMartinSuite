namespace ITMartinLager.Domain;

// Stikprøve for magazines (user 2026-11-25: "the findings of listings where the numbers are different - that must be a hint as well").
// Every listing names its own issue ("nr. 15"). What the listings say about the number is a sign of how sure our thing is:
// they agree with us = fine; they agree with each other but not with us = ours is probably wrong; they disagree = unsure + the price
// is made of different issues. A listing with the same cover (CoverMatch) counts double, one with another edition's cover is left out.
public enum IssueHintKind { None, Agree, Other, Mixed, NoNumber }

public sealed record IssueHint(IssueHintKind Kind, string Text, string? SuggestedNumber)
{
    /// <summary>The worst first: Mixed, Other, then None and Agree.</summary>
    public int Order => Kind switch { IssueHintKind.Mixed => 0, IssueHintKind.Other => 1, IssueHintKind.NoNumber => 1, IssueHintKind.None => 2, _ => 3 };
}

public static class IssueHints
{
    public static bool Applies(Item i) => FixedPrice.Magazines.Contains(i.Kind) || i.Kind == Kinds.Jumbobog;

    public static IssueHint For(Item i, IEnumerable<PriceFind> finds)
    {
        var votes = new Dictionary<string, int>();
        foreach (var f in finds)
        {
            if (CoverMatch.OtherEdition(f)) continue;
            var numbers = ListingMatch.IssueNumbers(f.Name).Where(n => n != "").Distinct().ToList();
            if (numbers.Count != 1) continue;   // no number, or a range/several: no vote
            var weight = f.Cover is { } c && c >= CoverMatch.SameFrom ? 2 : 1;
            votes[numbers[0]] = votes.GetValueOrDefault(numbers[0]) + weight;
        }
        if (votes.Count == 0) return new IssueHint(IssueHintKind.None, "", null);

        var ours = i.Number.Trim().TrimStart('0');
        var list = string.Join(", ", votes.OrderByDescending(v => v.Value).ThenBy(v => v.Key).Select(v => $"nr. {v.Key}"));
        if (votes.Count == 1)
        {
            var only = votes.Keys.First();
            if (only == ours) return new IssueHint(IssueHintKind.Agree, $"Annoncerne siger nr. {only} – som din.", null);
            return new IssueHint(IssueHintKind.Other,
                ours == "" ? $"Annoncerne siger nr. {only} – din har intet nummer." : $"Annoncerne siger nr. {only} – du har nr. {ours}.", only);
        }
        // 2026-11-25 (first look at real data): a thing with NO number got listings for 14 different issues - they say nothing about it.
        if (ours == "")
            return new IssueHint(IssueHintKind.NoNumber, "Din har intet nummer, og annoncerne nævner mange numre – de siger intet om din. Læs nummeret på forsiden og skriv det her.", null);
        var top = votes.OrderByDescending(v => v.Value).First();
        var total = votes.Values.Sum();
        if (top.Key == ours && top.Value * 3 >= total * 2) return new IssueHint(IssueHintKind.Agree, $"De fleste annoncer siger nr. {ours} (de andre: {list}).", null);
        return new IssueHint(IssueHintKind.Mixed, $"Annoncerne er uenige om nummeret: {list}. Prisen er lavet af flere numre.", null);
    }
}
