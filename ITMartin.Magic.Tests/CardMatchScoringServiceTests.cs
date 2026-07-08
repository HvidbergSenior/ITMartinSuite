using FluentAssertions;
using ITMartin.Ai.Models;
using ITMartin.Magic.Application.Models;
using ITMartin.Magic.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITMartin.Magic.Tests;

public class CardMatchScoringServiceTests
{
    private CardMatchScoringService _sut = null!;

    [SetUp]
    public void Setup()
    {
        _sut = new CardMatchScoringService(NullLogger<CardMatchScoringService>.Instance);
    }

    private static ScryfallCard Card(
        string name = "Holy Armor",
        string collectorNumber = "29",
        string artist = "Melissa A. Benson",
        string manaCost = "{W}",
        string typeLine = "Enchantment — Aura",
        string power = "",
        string toughness = "",
        string borderColor = "black",
        string releasedAt = "1995-04-01") => new()
    {
        Id = Guid.NewGuid().ToString(),
        Name = name,
        CollectorNumber = collectorNumber,
        Artist = artist,
        ManaCost = manaCost,
        TypeLine = typeLine,
        Power = power,
        Toughness = toughness,
        BorderColor = borderColor,
        ReleasedAt = releasedAt
    };

    private static MagicCardAnalysisResult Analysis(
        string identifiedName = "Holy Armor",
        string? collectorNumber = null,
        string? artist = null,
        string? manaCost = null,
        string? cardType = null,
        string? powerToughness = null,
        string? borderColor = null,
        string? copyrightYear = null) => new()
    {
        IdentifiedName = identifiedName,
        CollectorNumber = collectorNumber,
        Artist = artist,
        ManaCost = manaCost,
        CardType = cardType,
        PowerToughness = powerToughness,
        BorderColor = borderColor,
        CopyrightYear = copyrightYear
    };

    [Test]
    public void NameMismatch_ScoresZero()
    {
        var score = _sut.CalculateScore(Card(name: "Holy Armor"), Analysis(identifiedName: "Holy Strength"));
        score.Should().Be(0);
    }

    [Test]
    public void NameMatch_OnlyBaseScore()
    {
        var score = _sut.CalculateScore(Card(), Analysis());
        score.Should().Be(100);
    }

    [Test]
    public void CollectorNumberMatch_AddsStrongBonus()
    {
        var score = _sut.CalculateScore(Card(collectorNumber: "29"), Analysis(collectorNumber: "29"));
        score.Should().Be(100 + 1000);
    }

    [Test]
    public void CollectorNumberMismatch_PenalizesScore()
    {
        var score = _sut.CalculateScore(Card(collectorNumber: "29"), Analysis(collectorNumber: "142"));
        score.Should().Be(100 - 500);
    }

    [Test]
    public void ArtistMatch_AddsBonus()
    {
        var score = _sut.CalculateScore(Card(artist: "John Avon"), Analysis(artist: "John Avon"));
        score.Should().Be(100 + 300);
    }

    [Test]
    public void ArtistMismatch_NoPenaltyJustNoBonus()
    {
        var score = _sut.CalculateScore(Card(artist: "John Avon"), Analysis(artist: "Someone Else"));
        score.Should().Be(100);
    }

    [Test]
    public void ManaCostMatch_AddsBonus()
    {
        var score = _sut.CalculateScore(Card(manaCost: "{2}{W}"), Analysis(manaCost: "{2}{W}"));
        score.Should().Be(100 + 100);
    }

    [Test]
    public void CardTypeContains_AddsBonus()
    {
        var score = _sut.CalculateScore(Card(typeLine: "Creature — Human Wizard"), Analysis(cardType: "Wizard"));
        score.Should().Be(100 + 100);
    }

    [Test]
    public void PowerToughnessMatch_AddsBonus()
    {
        var score = _sut.CalculateScore(Card(power: "2", toughness: "3"), Analysis(powerToughness: "2/3"));
        score.Should().Be(100 + 100);
    }

    [Test]
    public void BorderColorMatch_AddsBonus()
    {
        var score = _sut.CalculateScore(Card(borderColor: "black"), Analysis(borderColor: "black"));
        score.Should().Be(100 + 200);
    }

    [Test]
    public void BorderColorMismatch_PenalizesScore()
    {
        var score = _sut.CalculateScore(Card(borderColor: "white"), Analysis(borderColor: "black"));
        score.Should().Be(100 - 300);
    }

    // ── Regression: the exact real-world bug found testing Holy Armor ──
    // Physical card confirmed 1995 (4th Edition). Old cards without a set
    // symbol are disambiguated almost entirely by copyright year, since
    // border color and mana cost/type are identical across 1990s reprints.

    [Test]
    public void CopyrightYearMatch_4thEdition_ScoresHighestAmongOldReprints()
    {
        var fourthEdition = Card(releasedAt: "1995-04-01");
        var unlimited = Card(releasedAt: "1993-12-01");
        var analysis = Analysis(copyrightYear: "1995", borderColor: "black");

        var scoreFourthEd = _sut.CalculateScore(fourthEdition, analysis);
        var scoreUnlimited = _sut.CalculateScore(unlimited, analysis);

        scoreFourthEd.Should().BeGreaterThan(scoreUnlimited);
        scoreFourthEd.Should().Be(100 + 200 + 150); // name + border + year match
        scoreUnlimited.Should().Be(100 + 200 - 150); // name + border + year mismatch
    }

    [Test]
    public void CopyrightYearMismatch_PenalizesScore()
    {
        var score = _sut.CalculateScore(Card(releasedAt: "1993-12-01"), Analysis(copyrightYear: "1995"));
        score.Should().Be(100 - 150);
    }

    [Test]
    public void NoCopyrightYearExtracted_NoBonusOrPenalty()
    {
        var score = _sut.CalculateScore(Card(releasedAt: "1995-04-01"), Analysis(copyrightYear: null));
        score.Should().Be(100);
    }

    [Test]
    public void AllSignalsMatch_MaximizesScore()
    {
        var card = Card(collectorNumber: "29", artist: "Melissa A. Benson", manaCost: "{W}",
            typeLine: "Enchantment — Aura", borderColor: "black", releasedAt: "1995-04-01");
        var analysis = Analysis(collectorNumber: "29", artist: "Melissa A. Benson", manaCost: "{W}",
            cardType: "Aura", borderColor: "black", copyrightYear: "1995");

        var score = _sut.CalculateScore(card, analysis);

        score.Should().Be(100 + 1000 + 300 + 100 + 100 + 200 + 150);
    }
}
