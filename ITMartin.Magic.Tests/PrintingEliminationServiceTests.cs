using FluentAssertions;
using ITMartin.Ai.Models;
using ITMartin.Magic.Application.Models;
using ITMartin.Magic.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITMartin.Magic.Tests;

public class PrintingEliminationServiceTests
{
    private PrintingEliminationService _sut = null!;

    [SetUp]
    public void Setup()
    {
        _sut = new PrintingEliminationService(NullLogger<PrintingEliminationService>.Instance);
    }

    private static ScryfallCard Card(string collectorNumber, string artist, string set = "4ed") => new()
    {
        Id = Guid.NewGuid().ToString(),
        Name = "Holy Armor",
        Set = set,
        CollectorNumber = collectorNumber,
        Artist = artist
    };

    // ── Regression: EliminateByCollectorNumber previously filtered by
    // artist instead of collector number (copy-paste bug). These printings
    // share the same artist but have different collector numbers — the
    // fix must narrow to the one whose collector number actually matches.

    [Test]
    public async Task EliminatesByCollectorNumber_WhenSameArtistDifferentNumbers()
    {
        var cards = new List<ScryfallCard>
        {
            Card(collectorNumber: "29", artist: "Melissa A. Benson", set: "4ed"),
            Card(collectorNumber: "142", artist: "Melissa A. Benson", set: "3ed"),
        };
        var analysis = new MagicCardAnalysisResult { CollectorNumber = "29" };

        var result = await _sut.EliminateAsync(cards, analysis, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Set.Should().Be("4ed");
    }

    [Test]
    public async Task CollectorNumberNoMatch_FallsBackToFullList()
    {
        var cards = new List<ScryfallCard>
        {
            Card(collectorNumber: "29", artist: "A"),
            Card(collectorNumber: "142", artist: "B"),
        };
        var analysis = new MagicCardAnalysisResult { CollectorNumber = "999" };

        var result = await _sut.EliminateAsync(cards, analysis, CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Test]
    public async Task NoCollectorNumberExtracted_SkipsThatFilterEntirely()
    {
        var cards = new List<ScryfallCard>
        {
            Card(collectorNumber: "29", artist: "A"),
            Card(collectorNumber: "142", artist: "B"),
        };
        var analysis = new MagicCardAnalysisResult { CollectorNumber = null };

        var result = await _sut.EliminateAsync(cards, analysis, CancellationToken.None);

        result.Should().HaveCount(2);
    }

    // ── Artist elimination normalizes "Illus." prefixes that OCR
    // commonly captures from the artist credit line on the card ──

    [Test]
    public async Task EliminatesByArtist_IgnoringIllusPrefix()
    {
        var cards = new List<ScryfallCard>
        {
            Card(collectorNumber: "1", artist: "John Avon"),
            Card(collectorNumber: "2", artist: "Someone Else"),
        };
        var analysis = new MagicCardAnalysisResult { Artist = "Illus. John Avon" };

        var result = await _sut.EliminateAsync(cards, analysis, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Artist.Should().Be("John Avon");
    }

    [Test]
    public async Task ArtistNoMatch_FallsBackToFullList()
    {
        var cards = new List<ScryfallCard>
        {
            Card(collectorNumber: "1", artist: "John Avon"),
            Card(collectorNumber: "2", artist: "Someone Else"),
        };
        var analysis = new MagicCardAnalysisResult { Artist = "Totally Unknown Artist" };

        var result = await _sut.EliminateAsync(cards, analysis, CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Test]
    public async Task BothFiltersApply_NarrowsProgressively()
    {
        var cards = new List<ScryfallCard>
        {
            Card(collectorNumber: "29", artist: "Melissa A. Benson", set: "4ed"),
            Card(collectorNumber: "29", artist: "Different Artist", set: "sum"),
            Card(collectorNumber: "142", artist: "Melissa A. Benson", set: "3ed"),
        };
        var analysis = new MagicCardAnalysisResult
        {
            CollectorNumber = "29",
            Artist = "Melissa A. Benson"
        };

        var result = await _sut.EliminateAsync(cards, analysis, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Set.Should().Be("4ed");
    }
}
