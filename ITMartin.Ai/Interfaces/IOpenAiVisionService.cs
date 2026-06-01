namespace ITMartin.Ai.Interfaces;

public interface IOpenAiVisionService
{
    Task<string> AnalyzeImageAsync(
        string imagePath,
        CancellationToken cancellationToken = default);
}
