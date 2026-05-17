namespace ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;

public sealed class OpenAiProcessingJob
{
    public Guid MediaId { get; set; }

    public string FilePath { get; set; } = string.Empty;

    public string PromptType { get; set; } = string.Empty;
}