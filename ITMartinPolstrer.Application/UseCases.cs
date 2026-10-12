using ITMartinPolstrer.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinPolstrer.Application;

// Every field on the "Nyt møbel" / "Ret" forms. All optional - the phone form must never block on a field.
public sealed record PieceDetails(
    string? Title, string? Category, string? Worker, string? Customer, string? Material,
    string? Techniques, string? InstructedBy, string? Instructions, DateOnly? Deadline);

// Photo-first intake from the phone (plain HTTP, no Blazor circuit - the circuit is exactly what dies while the camera is open).
// The first photo creates a draft piece; the details form fills it in. Nothing can be lost after the first photo lands.
public sealed class Intake(IPieceStore store, IMediaStore media)
{
    public async Task<(Piece Piece, List<MediaFile> Saved)> UploadAsync(Guid? pieceId, IReadOnlyList<(string FileName, Stream Content)> files, CancellationToken ct = default)
    {
        var saved = new List<MediaFile>();
        var piece = await store.SaveGraphAsync(pieceId, NewDraft, async p =>
        {
            var step = p.Steps.OrderBy(s => s.At).First();
            foreach (var (name, content) in files)
            {
                var m = await media.SaveAsync(p.Folder, name, content, ct);
                m.StepId = step.Id;
                step.Media.Add(m);
                saved.Add(m);
            }
        }, ct);
        return (piece, saved);
    }

    /// <returns>The piece's slug (for the redirect to its page).</returns>
    public async Task<string> SaveDetailsAsync(Guid? pieceId, PieceDetails d, CancellationToken ct = default)
    {
        var title = (d.Title ?? "").Trim();
        var piece = await store.SaveGraphAsync(pieceId, () => NewPiece(title), p =>
        {
            // A draft photographed before it had a name lives in "Møbel"; it moves to the title now.
            if (string.IsNullOrWhiteSpace(p.Title) && title.Length > 0 && p.Folder.StartsWith("Møbel"))
            {
                p.Folder = media.RenameFolderForTitle(p.Folder, title, p.Steps.SelectMany(s => s.Media));
                p.Slug = PieceSlug.From(title);
            }
            Apply(p, d);
            return Task.CompletedTask;
        }, ct);
        return piece.Slug;
    }

    internal static void Apply(Piece p, PieceDetails d)
    {
        p.Title = (d.Title ?? "").Trim();
        p.Category = (d.Category ?? "").Trim();
        p.Worker = (d.Worker ?? "").Trim();
        p.Customer = (d.Customer ?? "").Trim();
        p.Material = (d.Material ?? "").Trim();
        p.Techniques = (d.Techniques ?? "").Trim();
        p.InstructedBy = (d.InstructedBy ?? "").Trim();
        p.Instructions = (d.Instructions ?? "").Trim();
        p.Deadline = d.Deadline;
    }

    private Piece NewDraft() => WithFirstStep(new Piece { Title = "", Slug = PieceSlug.From("moebel"), Folder = media.NewFolderFor("") });

    private Piece NewPiece(string title)
    {
        var name = title.Length == 0 ? "moebel" : title;
        return WithFirstStep(new Piece { Slug = PieceSlug.From(name), Folder = media.NewFolderFor(name) });
    }

    private static Piece WithFirstStep(Piece p)
    {
        p.Steps.Add(new Step { PieceId = p.Id, Note = "Modtaget" });
        return p;
    }
}

// One piece's page on the phone: steps with photos, status, edit, delete, AI suggestions, the share page.
public sealed class Journal(IPieceStore store, IMediaStore media, IAiSuggester ai)
{
    public bool AiEnabled => ai.Enabled;

    public Task<Piece?> GetAsync(string slug, CancellationToken ct = default) => store.GetBySlugAsync(slug, ct);

    public async Task AddStepAsync(Piece piece, string note, IReadOnlyList<(string FileName, string TempPath)> files, CancellationToken ct = default)
    {
        var step = new Step { PieceId = piece.Id, Note = note.Trim() };
        foreach (var (name, temp) in files)
        {
            var m = await media.SaveFromTempAsync(piece.Folder, name, temp, ct);
            m.StepId = step.Id;
            step.Media.Add(m);
        }
        await store.AddStepAsync(step, ct);
        if (piece.Status == PieceStatus.Modtaget) await store.UpdatePieceAsync(piece.Id, p => p.Status = PieceStatus.IGang, ct);   // first real step = work has started
    }

    public Task SaveStepNoteAsync(Guid stepId, string note, CancellationToken ct = default) => store.UpdateStepNoteAsync(stepId, note.Trim(), ct);

    public async Task DeleteStepAsync(Step step, CancellationToken ct = default)
    {
        foreach (var m in step.Media) media.Delete(m);
        await store.DeleteStepAsync(step.Id, ct);
    }

    public async Task DeleteMediaAsync(MediaFile m, CancellationToken ct = default)
    {
        media.Delete(m);
        await store.DeleteMediaAsync(m.Id, ct);
    }

    public Task SetStatusAsync(Guid pieceId, PieceStatus status, CancellationToken ct = default) =>
        store.UpdatePieceAsync(pieceId, p => p.Status = status, ct);

    public Task SavePieceAsync(Piece edited, CancellationToken ct = default) =>
        store.UpdatePieceAsync(edited.Id, p => Intake.Apply(p, Details(edited)), ct);

    public async Task DeletePieceAsync(Piece piece, CancellationToken ct = default)
    {
        media.DeleteFolder(piece.Folder);   // the gallery must not keep an empty card for it
        await store.DeletePieceAsync(piece.Id, ct);
    }

    public Task<List<string>> WorkersAsync(CancellationToken ct = default) => store.WorkersAsync(ct);

    public Task<Suggestion> SuggestAsync(Piece piece, SuggestionLevel level, CancellationToken ct = default) => ai.SuggestAsync(piece, level, ct);

    // Only empty fields are filled - what she wrote herself always wins.
    public Task ApplySuggestionAsync(Guid pieceId, Suggestion s, CancellationToken ct = default) =>
        store.UpdatePieceAsync(pieceId, p =>
        {
            if (string.IsNullOrWhiteSpace(p.Category)) p.Category = s.Type.Trim();
            if (string.IsNullOrWhiteSpace(p.Material)) p.Material = s.Materialer.Trim();
            if (string.IsNullOrWhiteSpace(p.Techniques)) p.Techniques = string.Join(", ", s.Teknikker).Trim();
            if (string.IsNullOrWhiteSpace(p.Title) || p.Title == "Uden navn") p.Title = s.NavnType.Trim();
        }, ct);

    public Task AddSuggestionAsStepAsync(Guid pieceId, Suggestion s, CancellationToken ct = default) =>
        store.AddStepAsync(new Step { PieceId = pieceId, Note = SuggestionNote(s) }, ct);

    internal static string SuggestionNote(Suggestion s)
    {
        var nl = Environment.NewLine;
        return $"AI-forslag ({(s.IsQuick ? "hurtig" : "grundig")}):{nl}"
             + $"{s.NavnType} – {s.PeriodeStil}{nl}Materialer: {s.Materialer}{nl}Konstruktion: {s.Konstruktion}{nl}Stand: {s.Stand}{nl}"
             + "Sådan gør du:" + nl + string.Join(nl, s.SaadanGoerDu.Select((t, i) => $"{i + 1}. {t}"))
             + $"{nl}Usikkert: {s.Usikkerhed}";
    }

    private static PieceDetails Details(Piece p) =>
        new(p.Title, p.Category, p.Worker, p.Customer, p.Material, p.Techniques, p.InstructedBy, p.Instructions, p.Deadline);
}

// Erfaring: every piece that has been through the workshop, newest first (searched in memory by ExperienceSearch).
public sealed class Experience(IPieceStore store)
{
    public async Task<List<Piece>> AllAsync(CancellationToken ct = default) =>
        (await store.AllAsync(ct)).OrderByDescending(p => p.ReceivedAt).ToList();
}

// The workshop board (Polstrer-tavlen, the paid version for a company of polstrere): columns per status + comments.
public sealed class WorkshopBoard(IPieceStore store, TimeProvider clock)
{
    public static readonly TimeSpan DeliveredShown = TimeSpan.FromDays(14);

    // Afleveret only shows the last two weeks - the column is a "just done", not an archive. Soonest deadline first.
    public async Task<List<Piece>> PiecesAsync(CancellationToken ct = default)
    {
        var cutoff = clock.GetUtcNow().UtcDateTime - DeliveredShown;
        return (await store.AllAsync(ct))
            .Where(p => p.Status != PieceStatus.Afleveret || p.Steps.Max(s => (DateTime?)s.At) >= cutoff || p.Comments.Any(c => c.At >= cutoff))
            .OrderBy(p => p.Deadline ?? DateOnly.MaxValue).ThenByDescending(p => p.ReceivedAt)
            .ToList();
    }

    public async Task AddCommentAsync(Guid pieceId, string author, string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        await store.AddCommentAsync(new Comment { PieceId = pieceId, Author = author.Trim(), Text = text.Trim() }, ct);
    }

    public Task DeleteCommentAsync(Guid commentId, CancellationToken ct = default) => store.DeleteCommentAsync(commentId, ct);
}

public static class DependencyInjection
{
    /// <summary>The use cases. Ports come from AddPolstrerInfrastructure.</summary>
    public static IServiceCollection AddPolstrerApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<Intake>();
        services.AddScoped<Journal>();
        services.AddScoped<Experience>();
        services.AddScoped<WorkshopBoard>();
        return services;
    }
}
