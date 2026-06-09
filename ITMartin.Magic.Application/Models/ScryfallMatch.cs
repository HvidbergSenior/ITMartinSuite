using ITMartin.Magic.Application.Models;

public sealed class ScryfallMatch
{
    public ScryfallCard Card { get; set; } = null!;

    public decimal Score { get; set; }

    public decimal Confidence { get; set; }

    public string ConfidenceLabel { get; set; }
        = "";
}