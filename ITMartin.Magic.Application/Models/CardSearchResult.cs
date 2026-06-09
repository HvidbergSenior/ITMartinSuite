namespace ITMartin.Magic.Application.Models;

public sealed class CardSearchResult
{
    public ScryfallCard? BestMatch { get; set; }

    public List<ScryfallMatch> Matches { get; set; }
        = [];
}