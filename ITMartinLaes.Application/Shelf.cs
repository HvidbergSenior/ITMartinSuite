using ITMartinLaes.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinLaes.Application;

/// <summary>The bookshelf: put a PDF on it, read it, take it off.</summary>
public sealed class Shelf(IBookRepository books, IBookFiles files, IPdfText text, IPageImages images)
{
    public const long MaxBytes = 300L * 1024 * 1024;

    public Task<List<Book>> AllAsync(CancellationToken ct = default) => books.AllAsync(ct);
    public Task<Book?> GetAsync(Guid id, CancellationToken ct = default) => books.GetAsync(id, ct);

    public async Task<Book> AddAsync(Stream pdf, string fileName, CancellationToken ct = default)
    {
        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) throw new LaesException("Det skal være en PDF-fil.");
        var stored = await files.SaveAsync(pdf, ct);
        var pages = files.PathOf(stored) is { } path ? text.PageCount(path) : 0;
        if (pages == 0)
        {
            files.Delete(stored);
            throw new LaesException("PDF'en kunne ikke åbnes - er filen hel?");
        }
        var book = new Book { Title = Book.TitleFromFileName(fileName), FileName = stored, Pages = pages };
        await books.AddAsync(book, ct);
        return book;
    }

    public async Task TurnToAsync(Guid id, int page, CancellationToken ct = default)
    {
        if (await books.GetAsync(id, ct) is not { } book) return;
        book.TurnTo(page);
        await books.UpdateAsync(book, ct);
    }

    public async Task RenameAsync(Guid id, string title, CancellationToken ct = default)
    {
        title = title.Trim();
        if (title == "" || await books.GetAsync(id, ct) is not { } book) return;
        book.Title = title.Length > Book.MaxTitle ? title[..Book.MaxTitle] : title;
        await books.UpdateAsync(book, ct);
    }

    public async Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        if (await books.GetAsync(id, ct) is not { } book) return;
        await books.DeleteAsync(id, ct);
        files.Delete(book.FileName);
    }

    public async Task SummariseAgainAsync(Guid id, CancellationToken ct = default)
    {
        if (await books.GetAsync(id, ct) is not { } book) return;
        book.SummariseAgain();
        await books.UpdateAsync(book, ct);
    }

    public string? PdfPath(Book book) => files.PathOf(book.FileName);

    /// <summary>The widths a page is made in - a phone, a PC, a big screen - so the pictures are kept and reused.</summary>
    public static readonly int[] Widths = [800, 1200, 1800];

    public static int WidthFor(int wanted) => Widths.FirstOrDefault(w => w >= wanted, Widths[^1]);

    public async Task<string?> PageImageAsync(Guid id, int page, int width, CancellationToken ct = default)
    {
        if (await books.GetAsync(id, ct) is not { } book || page < 1 || page > book.Pages) return null;
        return await images.PageAsync(book.FileName, page, WidthFor(width), ct);
    }
}

/// <summary>The summary, a part at a time (the job calls WorkAsync every minute): one AI call per part of ~20 pages, then
/// one call for the summary. Stops at the book's cap and at the day's cap; a restart goes on from the saved notes.</summary>
public sealed class Summaries(IBookRepository books, IBookFiles files, IPdfText text, IBookSummarizer ai)
{
    /// <summary>Does the next step of the next book. True when something was done.</summary>
    public async Task<bool> WorkAsync(CancellationToken ct = default)
    {
        if (!ai.Enabled || await ai.CallsTodayAsync(ct) >= ai.MaxCallsPerDay) return false;
        if (await books.NextToSummariseAsync(ct) is not { } book) return false;
        try
        {
            if (book.Calls >= SummaryPlan.MaxCallsPerBook)
            {
                book.Failed($"Stoppet ved {SummaryPlan.MaxCallsPerBook} AI-kald for denne bog.");
            }
            else if (files.PathOf(book.FileName) is not { } path)
            {
                book.Failed("PDF-filen findes ikke længere.");
            }
            else if (SummaryPlan.Next(book) is { } part)
            {
                var pages = text.Pages(path, part.From, part.To);
                book.AddNote(await ai.ReadPartAsync(book.Title, part.From, part.To, pages, ct));
            }
            else
            {
                var (summary, points, chapters) = await ai.SummariseAsync(book.Title, book.Pages, book.Notes, ct);
                book.Finish(summary, points, chapters);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            book.Failed("AI'en kunne ikke læse bogen: " + ex.Message);
        }
        await books.UpdateAsync(book, ct);
        return true;
    }

    /// <summary>How far: parts read and parts in all (+1 for the summary itself).</summary>
    public static (int Done, int All) Progress(Book book) =>
        (book.Notes.Count + (book.State == SummaryState.Klar ? 1 : 0), SummaryPlan.Parts(book.Pages).Count + 1);
}

public static class DependencyInjection
{
    public static IServiceCollection AddLaesApplication(this IServiceCollection services)
    {
        services.AddScoped<Shelf>();
        services.AddScoped<Summaries>();
        return services;
    }
}
