namespace ITMartin.Magic.Application.Models;

public class MagicCardWorkflowState
{
    public string ImagePath { get; set; } = string.Empty;

    public string? OcrText { get; set; }

    public string? CardName { get; set; }

    public string? SetCode { get; set; }

    public string? CollectorNumber { get; set; }

    public string? ScryfallId { get; set; }

    public decimal? Price { get; set; }
}