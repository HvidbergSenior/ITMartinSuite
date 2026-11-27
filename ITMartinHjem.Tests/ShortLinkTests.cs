using FluentAssertions;
using ITMartinHjem.Domain;

namespace ITMartinHjem.Tests;

public class ShortLinkTests
{
    private static readonly AppLink[] Apps =
    [
        new() { Name = "ElPriser", Url = "https://elpriser.itmartin.dk" },
        new() { Name = "Forløbet", Url = "https://forloebet.itmartin.dk" },
        new() { Name = "PC-tjek", Url = "https://pctjek.itmartin.dk" },
        new() { Name = "Ude", Url = "https://example.com/x" },
    ];

    [TestCase("elpriser", "https://elpriser.itmartin.dk")]
    [TestCase("ElPriser", "https://elpriser.itmartin.dk")]
    [TestCase("forløbet", "https://forloebet.itmartin.dk")]
    [TestCase("pc-tjek", "https://pctjek.itmartin.dk")]
    [TestCase("pctjek", "https://pctjek.itmartin.dk")]
    [TestCase("møde", "https://moede.itmartin.dk/gruppe1")]
    [TestCase("moede", "https://moede.itmartin.dk/gruppe1")]
    public void A_plain_word_finds_the_app(string word, string url) => ShortLinks.Target(word, Apps).Should().Be(url);

    [TestCase("www")]
    [TestCase("ukendt")]
    [TestCase("")]
    public void Unknown_words_find_nothing(string word) => ShortLinks.Target(word, Apps).Should().BeNull();

    [Test]
    public void Only_itmartin_subdomains_give_a_word_from_the_address() => ShortLinks.Target("example", Apps).Should().BeNull();
}
