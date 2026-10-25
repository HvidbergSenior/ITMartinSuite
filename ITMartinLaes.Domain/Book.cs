using System.Text.Json;

namespace ITMartinLaes.Domain;

// Kolibri Læs (user 2026-10-25: "an app that can read these [PDFs] and make them book like ... Maybe make resume of it").
// A book is one PDF the user put on the shelf: read like a book (pages side by side, it remembers where you stopped)
// and summarised in Danish by the AI - a short summary, the main points and a few lines per chapter.

public enum SummaryState { Venter, Læser, Klar, Fejl }

/// <summary>A chapter as the summary has it: where it starts and what it says in a few lines.</summary>
public sealed record Chapter(string Title, int Page, string Text);

/// <summary>What the AI wrote about one part of the book (one call): short notes, and the chapters that start in it.</summary>
public sealed record PartNote(int From, int To, string Notes, List<Chapter> Chapters);

public sealed class Book
{
    public const int MaxTitle = 200;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string FileName { get; set; } = "";       // the PDF in the books folder
    public int Pages { get; set; }
    public int LastPage { get; set; } = 1;          // where the reader stopped
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }

    public SummaryState State { get; set; } = SummaryState.Venter;
    public string Summary { get; set; } = "";
    public string PointsJson { get; set; } = "[]";
    public string ChaptersJson { get; set; } = "[]";
    public string NotesJson { get; set; } = "[]";   // the parts read so far, so a restart goes on where it stopped
    public int Calls { get; set; }                  // AI calls spent on this book
    public string Error { get; set; } = "";

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public List<string> Points => JsonSerializer.Deserialize<List<string>>(PointsJson, Json) ?? [];
    public List<Chapter> Chapters => JsonSerializer.Deserialize<List<Chapter>>(ChaptersJson, Json) ?? [];
    public List<PartNote> Notes => JsonSerializer.Deserialize<List<PartNote>>(NotesJson, Json) ?? [];

    public void TurnTo(int page)
    {
        LastPage = Math.Clamp(page, 1, Math.Max(1, Pages));
        ReadAt = DateTime.UtcNow;
    }

    public void AddNote(PartNote note)
    {
        var notes = Notes;
        notes.RemoveAll(n => n.From == note.From);
        notes.Add(note);
        NotesJson = JsonSerializer.Serialize(notes.OrderBy(n => n.From));
        State = SummaryState.Læser;
        Calls++;
    }

    public void Finish(string summary, IEnumerable<string> points, IEnumerable<Chapter> chapters)
    {
        Summary = summary.Trim();
        PointsJson = JsonSerializer.Serialize(points.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
        ChaptersJson = JsonSerializer.Serialize(chapters.Where(c => c.Page >= 1 && c.Page <= Math.Max(1, Pages)).OrderBy(c => c.Page));
        State = SummaryState.Klar;
        Error = "";
        Calls++;
    }

    public void Failed(string why)
    {
        State = SummaryState.Fejl;
        Error = why.Length > 300 ? why[..300] : why;
    }

    /// <summary>"Lav resuméet igen": the notes are kept only when asked to read again from scratch.</summary>
    public void SummariseAgain()
    {
        NotesJson = "[]";
        State = SummaryState.Venter;
        Error = "";
    }

    /// <summary>A title from the file name: "Business Model Generation (1).pdf" -> "Business Model Generation".</summary>
    public static string TitleFromFileName(string fileName)
    {
        var t = Path.GetFileNameWithoutExtension(fileName ?? "");
        t = System.Text.RegularExpressions.Regex.Replace(t, @"\s*\(\d+\)$", "");
        t = t.Replace('_', ' ').Trim();
        if (t == "") t = "Bog";
        return t.Length > MaxTitle ? t[..MaxTitle] : t;
    }
}

/// <summary>How a book is read by the AI (CLAUDE.md cost rules): many pages per call, never a call per page, and a hard
/// cap per book. A long book gets bigger parts, not more calls. The last call writes the summary from the notes.</summary>
public static class SummaryPlan
{
    public const int PagesPerCall = 20;
    /// <summary>Calls for one book, the last one included - a 288-page book is 15 + 1.</summary>
    public const int MaxCallsPerBook = 30;
    /// <summary>The text of one part is cut here (~40,000 tokens) - a picture-heavy or odd PDF cannot blow up one call.</summary>
    public const int MaxCharsPerCall = 160_000;

    public static List<(int From, int To)> Parts(int pages)
    {
        if (pages < 1) return [];
        var size = Math.Max(PagesPerCall, (int)Math.Ceiling(pages / (double)(MaxCallsPerBook - 1)));
        var parts = new List<(int, int)>();
        for (var from = 1; from <= pages; from += size) parts.Add((from, Math.Min(pages, from + size - 1)));
        return parts;
    }

    /// <summary>The next part not read yet, or null when every part has its note.</summary>
    public static (int From, int To)? Next(Book book)
    {
        var done = book.Notes.Select(n => n.From).ToHashSet();
        foreach (var p in Parts(book.Pages)) if (!done.Contains(p.From)) return p;
        return null;
    }

    public static string Cut(string text) => text.Length > MaxCharsPerCall ? text[..MaxCharsPerCall] : text;
}
