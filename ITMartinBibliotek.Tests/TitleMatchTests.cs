using FluentAssertions;
using ITMartinBibliotek.Server.Data.Entities;
using ITMartinBibliotek.Server.Services;

namespace ITMartinBibliotek.Tests;

public class TitleMatchTests
{
    [TestCase("The Matrix (DVD)", "matrix")]
    [TestCase("Blade Runner 2049 [Blu-ray]", "blade runner 2049")]
    [TestCase("Amélie", "amelie")]
    [TestCase("Absolute Music 69 2012", "absolute music 69 2012")]
    public void Normalize_strips_format_noise_and_accents(string input, string expected) =>
        TitleMatch.Normalize(input).Should().Be(expected);

    [Test]
    public void Same_allows_one_year_of_drift_between_disc_and_library()
    {
        TitleMatch.Same("Heat", 1995, "Heat", 1996).Should().BeTrue();
        TitleMatch.Same("Heat", 1995, "Heat", 2000).Should().BeFalse();
        TitleMatch.Same("Heat", null, "Heat", 2000).Should().BeTrue();
        TitleMatch.Same("Heat", 1995, "Heat 2", 1995).Should().BeFalse();
    }

    [Test]
    public void CleanProductName_turns_a_shop_listing_into_a_search_query() =>
        TitleMatch.CleanProductName("Inception (DVD) [2010] Widescreen").Should().Be("Inception 2010");

    [TestCase("DVD Video", MediaFormat.Dvd)]
    [TestCase("Blu-ray Disc", MediaFormat.BluRay)]
    [TestCase("CD, Album", MediaFormat.Cd)]
    [TestCase("Vinyl", null)]
    public void GuessFormat_reads_the_disc_type_out_of_free_text(string text, MediaFormat? expected) =>
        MetadataLookup.GuessFormat(text).Should().Be(expected);
}
