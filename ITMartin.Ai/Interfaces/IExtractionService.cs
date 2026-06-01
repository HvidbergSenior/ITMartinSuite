namespace ITMartin.Ai.Interfaces;

public interface IExtractionService
{
    Task<T> ExtractAsync<T>(
        string text,
        CancellationToken cancellationToken = default)
        where T : class;
}