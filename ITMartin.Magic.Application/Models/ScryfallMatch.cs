namespace ITMartin.Magic.Application.Models;

public class ScryfallMatch
{
    public ScryfallCard Card { get; set; } = null!;

    public int Score { get; set; }

    public string ConfidenceLabel { get; set; } = "";
}