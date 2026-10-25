using ITMartinLaes.Domain;

namespace ITMartinLaes.Application;

public interface IBookRepository
{
    Task AddAsync(Book book, CancellationToken ct = default);
    Task<Book?> GetAsync(Guid id, CancellationToken ct = default);
    /// <summary>Latest read (or added) first.</summary>
    Task<List<Book>> AllAsync(CancellationToken ct = default);
    Task UpdateAsync(Book book, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    /// <summary>The oldest book still waiting for (or in the middle of) its summary.</summary>
    Task<Book?> NextToSummariseAsync(CancellationToken ct = default);
}

/// <summary>The PDF files (the books folder on the server's own disk).</summary>
public interface IBookFiles
{
    Task<string> SaveAsync(Stream pdf, CancellationToken ct = default);
    /// <summary>Full path, or null when it is gone.</summary>
    string? PathOf(string fileName);
    void Delete(string fileName);
}

/// <summary>A page as a picture for the reader (2026-10-25: drawing this kind of print PDF in the browser froze it).
/// Made once per page and width, then kept next to the PDF.</summary>
public interface IPageImages
{
    /// <summary>Full path of the JPEG, or null when the page cannot be made.</summary>
    Task<string?> PageAsync(string fileName, int page, int width, CancellationToken ct = default);
}

/// <summary>The text in a PDF (no AI - the PDF's own text layer).</summary>
public interface IPdfText
{
    /// <summary>Pages, or 0 when it is not a PDF that can be read.</summary>
    int PageCount(string path);
    /// <summary>The text of pages from..to (1-based), one string per page.</summary>
    List<string> Pages(string path, int from, int to);
}

/// <summary>The AI that reads a part and, at the end, writes the summary. Counts its calls; refuses over the daily cap.</summary>
public interface IBookSummarizer
{
    bool Enabled { get; }
    int MaxCallsPerDay { get; }
    Task<int> CallsTodayAsync(CancellationToken ct = default);
    /// <summary>One call: notes on these pages (each page marked with its number) and the chapters that start in them.</summary>
    Task<PartNote> ReadPartAsync(string title, int from, int to, IReadOnlyList<string> pages, CancellationToken ct = default);
    /// <summary>One call: the summary, the main points and the chapters with a few lines each, from the notes.</summary>
    Task<(string Summary, List<string> Points, List<Chapter> Chapters)> SummariseAsync(string title, int pages,
        IReadOnlyList<PartNote> notes, CancellationToken ct = default);
}

public sealed class LaesException(string message) : Exception(message);
