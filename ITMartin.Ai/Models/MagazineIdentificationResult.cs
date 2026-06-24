namespace ITMartin.Ai.Models;

public class MagazineIdentificationResult
{
    public string Title { get; set; } = "";
    public string IssueDate { get; set; } = "";
    public int? Year { get; set; }
    public string Publisher { get; set; } = "";
    public string Country { get; set; } = "Other";
    public string Condition { get; set; } = "Good";
    public string ValueRating { get; set; } = "Unknown";
    public string AiReasoning { get; set; } = "";
}
