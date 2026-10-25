using FluentAssertions;
using ITMartinLager.Domain;
using ITMartinLager.Infrastructure.External;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITMartinLager.Tests;

// 2026-10-25: a comic's year from the Grand Comics Database - only when one Danish series has the number.
public class ComicCatalogTests
{
    private static CatalogSeries S(string name, string country, int? from, int? to, params string[] d) =>
        new(name, country, from, to, d, d.Select((_, i) => $"https://www.comics.org/api/issue/{i}/").ToList());

    [TestCase("12", "12")]
    [TestCase("1/1949", "1")]
    [TestCase("3 - Forbrydelser på samlebånd", "3")]
    [TestCase("007", "7")]
    [TestCase("[1993]", "")]
    public void The_number_is_read_from_the_start_of_a_descriptor(string d, string n) => ComicCatalog.NumberOf(d).Should().Be(n);

    [Test]
    public void One_Danish_series_with_the_number_gives_the_year()
    {
        var all = new[]
        {
            S("Edderkoppen", "dk", 1967, 1969, "1", "2", "3"),
            S("Edderkoppen", "no", 1968, 1968, "1/1968", "2/1968"),
            S("Edderkoppen", "dk", 1973, 1974, "1/1973", "2/1973"),
            S("Edderkoppen", "dk", 1981, 1990, "77", "78", "79"),
        };
        var hit = ComicCatalog.Pick(all, "Edderkoppen", "78");
        hit.Should().NotBeNull();
        hit!.Value.Series.YearBegan.Should().Be(1981);
        // "2" is in two Danish series (1967 and 1973) - no guess.
        ComicCatalog.Pick(all, "Edderkoppen", "2").Should().BeNull();
    }

    [Test]
    public void A_year_in_the_descriptor_is_used_without_asking_for_the_issue()
    {
        var s = S("Anders And & Co.", "dk", 1949, 1949, "1/1949", "2/1949");
        ComicCatalog.YearWithoutIssue(s, 1).Should().Be(1949);
        ComicCatalog.YearOfKeyDate("1968-03-00").Should().Be(1968);
    }

    [Test, Explicit, Category("Live")]
    public async Task Live_GCD_year_of_a_Danish_comic()
    {
        var gcd = new GcdCatalog(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, NullLogger<GcdCatalog>.Instance);
        var year = await gcd.YearAsync("Edderkoppen", "78", CancellationToken.None);
        TestContext.Out.WriteLine($"Edderkoppen 78 -> {year}");
        var aa = await gcd.YearAsync("Anders And & Co.", "12", CancellationToken.None);
        TestContext.Out.WriteLine($"Anders And & Co. 12 -> {aa?.ToString() ?? "(several years - no guess)"}");
    }
}
