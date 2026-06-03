namespace ITMartin.Magic.Application.Models;

public sealed class CardCandidateViewModel
{
    public string Name { get; set; } =
        string.Empty;

    public string SetCode { get; set; } =
        string.Empty;

    public string CollectorNumber { get; set; } =
        string.Empty;

    public string ImageUrl { get; set; } =
        string.Empty;

    public decimal? EurPrice { get; set; }

    public decimal? UsdPrice { get; set; }

    public decimal Confidence { get; set; }
}