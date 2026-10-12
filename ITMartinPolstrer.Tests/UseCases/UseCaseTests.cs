using FluentAssertions;
using ITMartinPolstrer.Application;
using ITMartinPolstrer.Domain;
using ITMartinPolstrer.Infrastructure.External;
using ITMartinPolstrer.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ITMartinPolstrer.Tests.UseCases;

// The use cases against a real (in-memory) SQLite database and a temp media folder - the same wiring as production.
[TestFixture]
public class UseCaseTests
{
    private SqliteConnection _conn = null!;
    private string _root = "";
    private EfPieceStore _store = null!;
    private FileMediaStore _media = null!;
    private Intake _intake = null!;
    private Journal _journal = null!;

    private sealed class Factory(DbContextOptions<PolstrerDbContext> o) : IDbContextFactory<PolstrerDbContext>
    {
        public PolstrerDbContext CreateDbContext() => new(o);
    }

    private sealed class NoAi : IAiSuggester
    {
        public bool Enabled => false;
        public Task<Suggestion> SuggestAsync(Piece piece, SuggestionLevel level, CancellationToken ct = default) => throw new NotSupportedException();
    }

    [SetUp]
    public void SetUp()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<PolstrerDbContext>().UseSqlite(_conn).Options;
        using (var db = new PolstrerDbContext(options)) PolstrerSchema.Ensure(db);
        _root = Path.Combine(Path.GetTempPath(), "polstrer-uc-" + Guid.NewGuid().ToString("N"));
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Polstrer:MediaRoot"] = _root }).Build();
        _media = new FileMediaStore(cfg, NullLogger<FileMediaStore>.Instance);
        _store = new EfPieceStore(new Factory(options));
        _intake = new Intake(_store, _media);
        _journal = new Journal(_store, _media, new NoAi());
    }

    [TearDown]
    public void TearDown()
    {
        _conn.Dispose();
        try { Directory.Delete(_root, true); } catch { }
    }

    private static Stream Jpeg()
    {
        var ms = new MemoryStream();
        using (var img = new Image<Rgba32>(200, 150)) img.SaveAsJpeg(ms);
        ms.Position = 0;
        return ms;
    }

    [Test]
    public async Task First_photo_makes_a_draft_and_the_next_photo_lands_on_the_same_piece()
    {
        var (draft, saved) = await _intake.UploadAsync(null, [("IMG_1.jpg", Jpeg())]);
        saved.Should().ContainSingle();
        draft.Folder.Should().StartWith("Møbel");

        // The second photo goes to an existing step - it must be INSERTED (client-made Guid key), not taken for an existing row.
        var (again, saved2) = await _intake.UploadAsync(draft.Id, [("IMG_2.jpg", Jpeg()), ("IMG_3.jpg", Jpeg())]);
        again.Id.Should().Be(draft.Id);
        saved2.Should().HaveCount(2);

        var loaded = await _store.GetBySlugAsync(draft.Slug);
        loaded!.Steps.Should().ContainSingle().Which.Media.Should().HaveCount(3);
        loaded.Steps[0].Note.Should().Be("Modtaget");
    }

    [Test]
    public async Task Details_name_the_draft_and_move_its_folder_and_photos()
    {
        var (draft, _) = await _intake.UploadAsync(null, [("IMG_1.jpg", Jpeg())]);
        var slug = await _intake.SaveDetailsAsync(draft.Id, new PieceDetails("Lænestol grøn", " Lænestol ", "Lise", "Marianne", "uld", "fjedre", "kunden", "Nyt betræk", new DateOnly(2026, 11, 1)));

        slug.Should().StartWith("laenestol-groen-");
        var p = await _store.GetBySlugAsync(slug);
        p!.Folder.Should().Be("Lænestol grøn");
        p.Category.Should().Be("Lænestol");
        var photo = p.Steps.SelectMany(s => s.Media).Single();
        photo.RelativePath.Should().StartWith("Lænestol grøn/");
        File.Exists(_media.FullPath(photo.RelativePath)).Should().BeTrue();
    }

    [Test]
    public async Task A_new_step_starts_the_work_and_deleting_the_piece_removes_its_folder()
    {
        var slug = await _intake.SaveDetailsAsync(null, new PieceDetails("Stol", "", "", "", "", "", "", "", null));
        var p = (await _journal.GetAsync(slug))!;
        p.Status.Should().Be(PieceStatus.Modtaget);

        var temp = Path.Combine(Path.GetTempPath(), $"polstrer-{Guid.NewGuid():N}.jpg");
        await using (var f = File.Create(temp)) await Jpeg().CopyToAsync(f);
        await _journal.AddStepAsync(p, "Afmonteret betræk", [("IMG_9.jpg", temp)]);

        p = (await _journal.GetAsync(slug))!;
        p.Status.Should().Be(PieceStatus.IGang);
        p.Steps.Should().HaveCount(2);
        File.Exists(temp).Should().BeFalse("the temp upload is moved into place");

        await _journal.DeletePieceAsync(p);
        (await _journal.GetAsync(slug)).Should().BeNull();
        Directory.Exists(Path.Combine(_root, p.Folder)).Should().BeFalse();
    }

    [Test]
    public async Task Ai_suggestion_only_fills_empty_fields()
    {
        var slug = await _intake.SaveDetailsAsync(null, new PieceDetails("Wegner-stol", "", "", "", "læder", "", "", "", null));
        var p = (await _journal.GetAsync(slug))!;
        await _journal.ApplySuggestionAsync(p.Id, new Suggestion("Andet navn", "Stol", "1960erne", "Teak, uld", "", "", ["fjedre", "kantsyning"], [], ""));

        p = (await _journal.GetAsync(slug))!;
        p.Title.Should().Be("Wegner-stol");          // hers
        p.Material.Should().Be("læder");             // hers
        p.Category.Should().Be("Stol");              // was empty
        p.Techniques.Should().Be("fjedre, kantsyning");
    }

    [Test]
    public async Task Board_hides_pieces_delivered_more_than_two_weeks_ago_and_takes_comments()
    {
        var board = new WorkshopBoard(_store, TimeProvider.System);
        var open = await _intake.SaveDetailsAsync(null, new PieceDetails("Sofa", "", "", "", "", "", "", "", new DateOnly(2026, 10, 20)));
        var old = await _intake.SaveDetailsAsync(null, new PieceDetails("Gammel stol", "", "", "", "", "", "", "", null));
        var oldPiece = (await _journal.GetAsync(old))!;
        await _journal.SetStatusAsync(oldPiece.Id, PieceStatus.Afleveret);
        await _conn.CreateCommand().Also(c => c.CommandText = $"UPDATE Steps SET At = '2026-01-01 10:00:00' WHERE PieceId = '{oldPiece.Id.ToString().ToUpperInvariant()}' OR PieceId = '{oldPiece.Id}'").ExecuteNonQueryAsync();

        var shown = await board.PiecesAsync();
        shown.Select(p => p.Title).Should().Contain("Sofa").And.NotContain("Gammel stol");

        var sofa = (await _journal.GetAsync(open))!;
        await board.AddCommentAsync(sofa.Id, " Peter ", " Fjedre bestilt ");
        await board.AddCommentAsync(sofa.Id, "Peter", "   ");   // empty = ignored
        sofa = (await _journal.GetAsync(open))!;
        sofa.Comments.Should().ContainSingle().Which.Text.Should().Be("Fjedre bestilt");
    }
}

internal static class TestExtensions
{
    public static T Also<T>(this T x, Action<T> a) { a(x); return x; }
}
