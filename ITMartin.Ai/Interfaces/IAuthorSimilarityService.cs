namespace ITMartin.Ai.Interfaces;

public interface IAuthorSimilarityService
{
    Task<List<AuthorSuggestion>> GetSimilarAuthorsAsync(
        IEnumerable<string> authorsInLibrary,
        CancellationToken cancellationToken = default);
}

public sealed class AuthorSuggestion
{
    public string AuthorName { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Genre { get; set; } = "";
}
