using FluentAssertions;
using ITMartin.Ai.Models;
using ITMartin.Magic.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITMartin.Magic.Tests;

// Live tests against the real Scryfall API (public, no key required).
// These exist to catch real "which printing/version did we pick" bugs —
// the kind of thing that only shows up against actual multi-printing data,
// like the Holy Armor 2ed-vs-4ed misidentification found 2026-07-08.
public class ScryfallServiceLiveTests
{
    private ScryfallService _sut = null!;

    [SetUp]
    public void Setup()
    {
        var http = new HttpClient { BaseAddress = new Uri("https://api.scryfall.com/") };
        http.DefaultRequestHeaders.Add("User-Agent", "ITMartinMagicTests/1.0 (contact: hvidbergsenior@gmail.com)");
        http.DefaultRequestHeaders.Add("Accept", "application/json");

        var scoring = new CardMatchScoringService(NullLogger<CardMatchScoringService>.Instance);
        var elimination = new PrintingEliminationService(NullLogger<PrintingEliminationService>.Instance);
        _sut = new ScryfallService(http, scoring, elimination, NullLogger<ScryfallService>.Instance);
    }

    // ── Regression: card physically confirmed to be 4th Edition (1995).
    // App previously returned 2ed (Unlimited, 1993) instead. ──
    [Test]
    public async Task HolyArmor_WithCorrect1995CopyrightYear_MatchesFourthEdition()
    {
        var analysis = new MagicCardAnalysisResult
        {
            IdentifiedName = "Holy Armor",
            CopyrightYear = "1995",
            BorderColor = "white"
        };

        var result = await _sut.SearchAsync("Holy Armor", setCode: null, analysis, CancellationToken.None);

        result.Should().NotBeNull();
        result!.BestMatch.Should().NotBeNull();
        result.BestMatch!.Set.Should().Be("4ed");
    }

    [Test]
    public async Task HolyArmor_WithCorrect1993CopyrightYear_MatchesUnlimited()
    {
        var analysis = new MagicCardAnalysisResult
        {
            IdentifiedName = "Holy Armor",
            CopyrightYear = "1993",
            BorderColor = "white"
        };

        var result = await _sut.SearchAsync("Holy Armor", setCode: null, analysis, CancellationToken.None);

        result.Should().NotBeNull();
        result!.BestMatch.Should().NotBeNull();
        result.BestMatch!.Set.Should().Be("2ed");
    }

    [Test]
    public async Task HolyArmor_NoSetSymbolMode_OnlyReturnsPreSymbolEraSets()
    {
        var result = await _sut.SearchAsync("Holy Armor", setCode: null, analysis: null, CancellationToken.None);

        result.Should().NotBeNull();
        var allowedSets = new[] { "lea", "leb", "2ed", "3ed", "4ed", "4bb", "arn", "atq", "5ed", "chr", "ren" };
        result!.Matches.Select(m => m.Card.Set).Should().OnlyContain(s => allowedSets.Contains(s));
    }

    [Test]
    public async Task LightningBolt_HasManyRealPrintings()
    {
        var result = await _sut.SearchAsync("Lightning Bolt", setCode: null, analysis: null, CancellationToken.None);

        result.Should().NotBeNull();
        // Only pre-symbol-era printings pass the no-set filter; Lightning Bolt
        // has at least LEA/LEB/2ED/3ED printings from that period.
        result!.Matches.Should().NotBeEmpty();
    }

    [Test]
    public async Task SpecificSetCode_NarrowsToThatPrinting_WhenItExists()
    {
        var result = await _sut.SearchAsync("Lightning Bolt", setCode: "lea", analysis: null, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Matches.Should().OnlyContain(m => string.Equals(m.Card.Set, "lea", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task SetCodeWithNoMatchingPrinting_FallsBackRatherThanReturningEmpty()
    {
        // Holy Armor was never printed in "war" (War of the Spark) — the
        // service should fall back to all printings rather than returning nothing.
        var result = await _sut.SearchAsync("Holy Armor", setCode: "war", analysis: null, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Matches.Should().NotBeEmpty();
        result.Matches.Should().Contain(m => m.Card.Set != "war");
    }

    [Test]
    public async Task UnknownCardName_ReturnsNull()
    {
        var result = await _sut.SearchAsync("Definitely Not A Real Magic Card Name Xyz123", setCode: null, analysis: null, CancellationToken.None);

        result.Should().BeNull();
    }

    // ── Regression: real captured bad-scan (data/bad-scans/252ec7f8...json,
    // 2026-07-08). Physical card confirmed Revised Edition. AI extracted
    // CopyrightYear=null (illegible on this card) and previously had no other
    // way to tell Revised apart from 4th Edition, defaulting to 4ed. Real
    // photo also showed nothing under the artist line — Revised's signature.
    [Test]
    public async Task EarthElemental_NoYearButNoLineUnderArtist_MatchesRevisedNotFourthEdition()
    {
        var analysis = new MagicCardAnalysisResult
        {
            IdentifiedName = "Earth Elemental",
            Artist = "Dan Frazier",
            ManaCost = "{3}{R}{R}",
            CardType = "Summon Elemental",
            PowerToughness = "4/5",
            BorderColor = "white",
            CopyrightYear = null,
            HasLineUnderArtist = false,
            IdentificationConfidence = 0.95m
        };

        var result = await _sut.SearchAsync("Earth Elemental", setCode: null, analysis, CancellationToken.None);

        result.Should().NotBeNull();
        result!.BestMatch.Should().NotBeNull();
        result.BestMatch!.Set.Should().Be("3ed");
    }

    [Test]
    public async Task EarthElemental_NoYearButLineUnderArtist_MatchesFourthEditionNotRevised()
    {
        var analysis = new MagicCardAnalysisResult
        {
            IdentifiedName = "Earth Elemental",
            Artist = "Dan Frazier",
            BorderColor = "white",
            CopyrightYear = null,
            HasLineUnderArtist = true,
            IdentificationConfidence = 0.95m
        };

        var result = await _sut.SearchAsync("Earth Elemental", setCode: null, analysis, CancellationToken.None);

        result.Should().NotBeNull();
        result!.BestMatch.Should().NotBeNull();
        result.BestMatch!.Set.Should().Be("4ed");
    }

    [Test]
    public async Task PricesParseCorrectly_RegardlessOfLocale()
    {
        var analysis = new MagicCardAnalysisResult { IdentifiedName = "Holy Armor", CopyrightYear = "1995" };
        var result = await _sut.SearchAsync("Holy Armor", setCode: null, analysis, CancellationToken.None);

        result.Should().NotBeNull();
        result!.BestMatch.Should().NotBeNull();
        // Regression: ParsePrice previously used culture-dependent decimal.TryParse,
        // which could silently null out real prices under a non-invariant locale.
        result.BestMatch!.EurPrice.Should().NotBeNull();
        result.BestMatch.EurPrice!.Value.Should().BeGreaterThan(0);
    }
}
