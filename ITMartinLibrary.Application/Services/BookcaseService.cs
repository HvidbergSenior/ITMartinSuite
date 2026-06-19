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

    public async Task<ScanResult> ProcessShelvesAsync(
        IList<(int ShelfNumber, string ImagePath)> shelves,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var existingTitles = await _repo.GetExistingTitlesAsync(ct);
        var newBooks = 0;
        var skippedBooks = 0;
        var shelvesToSave = new List<ScannedShelf>();

        for (var i = 0; i < shelves.Count; i++)
        {
            var (shelfNumber, imagePath) = shelves[i];
            progress?.Report($"Analyzing shelf {i + 1} of {shelves.Count}...");

            var result = await _ai.AnalyzeAsync(imagePath, ct);

            var candidates = result?.Items
                .Where(x => !string.IsNullOrWhiteSpace(x.Title) || !string.IsNullOrWhiteSpace(x.Author))
                .ToList() ?? [];

            var booksForShelf = new List<ShelfBook>();

            foreach (var item in candidates)
            {
                var key = (item.Title ?? "").Trim().ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(key) && existingTitles.Contains(key))
                {
                    skippedBooks++;
                    continue;
                }

                booksForShelf.Add(new ShelfBook
                {
                    Title = item.Title ?? "",
                    Author = item.Author ?? "",
                    BBoxX = item.BBoxX ?? 0,
                    BBoxY = item.BBoxY ?? 0,
                    BBoxW = item.BBoxW ?? 100,
                    BBoxH = item.BBoxH ?? 100,
                });

                if (!string.IsNullOrWhiteSpace(key))
                    existingTitles.Add(key);

                newBooks++;
            }

            progress?.Report($"Shelf {i + 1} of {shelves.Count}: {booksForShelf.Count} new, {candidates.Count - booksForShelf.Count} already in collection");

            if (booksForShelf.Count > 0)
            {
                shelvesToSave.Add(new ScannedShelf
                {
                    ShelfNumber = shelfNumber,
                    ImagePath = imagePath,
                    ScannedAt = DateTime.UtcNow,
                    Books = booksForShelf
                });
            }
        }

        if (shelvesToSave.Count > 0)
            await _repo.AddShelvesAsync(shelvesToSave, ct);

        return new ScanResult(newBooks, skippedBooks);
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

    public Task<int> GetTotalBookCountAsync(CancellationToken ct) =>
        _repo.GetTotalBookCountAsync(ct);

    public Task ClearAllAsync(CancellationToken ct) => _repo.ClearAllAsync(ct);
}
