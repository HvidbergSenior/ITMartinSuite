using ITMartin.Ai.Interfaces;
using ITMartinLibrary.Application.Interfaces;
using ITMartinLibrary.Domain.Entities;

namespace ITMartinLibrary.Application.Services;

public sealed class BookcaseService : IBookcaseService
{
    private readonly ILibraryShelfRecognitionService _ai;
    private readonly IScannedShelfRepository _repo;

    public BookcaseService(ILibraryShelfRecognitionService ai, IScannedShelfRepository repo)
    {
        _ai = ai;
        _repo = repo;
    }

    public async Task ProcessShelvesAsync(
        IList<(int ShelfNumber, string ImagePath)> shelves,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var scanned = new List<ScannedShelf>();

        for (var i = 0; i < shelves.Count; i++)
        {
            var (shelfNumber, imagePath) = shelves[i];
            progress?.Report($"Analyzing shelf {i + 1} of {shelves.Count}...");

            var result = await _ai.AnalyzeAsync(imagePath, ct);
            if (result is null) continue;

            scanned.Add(new ScannedShelf
            {
                ShelfNumber = shelfNumber,
                ImagePath = imagePath,
                ScannedAt = DateTime.UtcNow,
                Books = result.Items
                    .Where(x => !string.IsNullOrWhiteSpace(x.Title) || !string.IsNullOrWhiteSpace(x.Author))
                    .Select(x => new ShelfBook
                    {
                        Title = x.Title ?? "",
                        Author = x.Author ?? "",
                        BBoxX = x.BBoxX ?? 0,
                        BBoxY = x.BBoxY ?? 0,
                        BBoxW = x.BBoxW ?? 100,
                        BBoxH = x.BBoxH ?? 100,
                    })
                    .ToList()
            });
        }

        await _repo.SaveShelvesAsync(scanned, ct);
    }

    public async Task<IList<ShelfSearchResult>> SearchAsync(string query, CancellationToken ct)
    {
        var shelves = await _repo.GetAllWithBooksAsync(ct);

        return shelves
            .OrderBy(s => s.ShelfNumber)
            .Select(s => new ShelfSearchResult
            {
                ShelfNumber = s.ShelfNumber,
                ImagePath = s.ImagePath,
                MatchingBooks = s.Books
                    .Where(b =>
                        b.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        b.Author.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList()
            })
            .Where(r => r.MatchingBooks.Count > 0)
            .ToList();
    }

    public Task<bool> HasDataAsync(CancellationToken ct) => _repo.HasDataAsync(ct);

    public Task ClearAllAsync(CancellationToken ct) => _repo.ClearAllAsync(ct);
}
