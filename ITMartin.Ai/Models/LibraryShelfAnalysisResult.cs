namespace ITMartin.Ai.Models;

public sealed record LibraryShelfAnalysisResult
{
    public List<LibraryShelfItem> Items
    {
        get;
        init;
    } = [];
}