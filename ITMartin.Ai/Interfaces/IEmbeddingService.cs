namespace ITMartin.Ai.Interfaces;

public interface IEmbeddingService
{
    Task<float[]> CreateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);
}