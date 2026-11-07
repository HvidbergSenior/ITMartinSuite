using FluentAssertions;
using ITMartinLager.Domain;

namespace ITMartinLager.Tests;

// Genres in the shops (user 2026-11-07: "like danktop, indie, pop. and on the other shops as well" + "and alphabetically").
public class GenreTests
{
    private const string M = ProductTypes.Musik, F = ProductTypes.Film, B = ProductTypes.Boeger;

    [TestCase(new[] { "pop rock", "pop" }, "Rock")]
    [TestCase(new[] { "indie pop" }, "Indie")]
    [TestCase(new[] { "synthpop", "new wave" }, "Pop")]
    [TestCase(new[] { "heavy metal", "hard rock" }, "Metal")]
    [TestCase(new[] { "schlager" }, "Dansktop")]
    [TestCase(new[] { "rock", "dansktop" }, "Dansktop")]
    [TestCase(new[] { "folk", GenreRules.DanishArtist }, "Visesang")]
    [TestCase(new[] { "pop", GenreRules.DanishArtist }, "Pop")]
    [TestCase(new[] { "rock", "soundtrack" }, "Soundtrack")]
    [TestCase(new[] { "children's music" }, "Børnemusik")]
    [TestCase(new[] { "improvised music" }, "")]
    [TestCase(new string[0], "")]
    public void Music(string[] tags, string genre) => GenreRules.Pick(M, tags).Should().Be(genre);

    [TestCase(new[] { "animation", "family", "comedy" }, "", "Børnefilm")]
    [TestCase(new[] { "animation", "science fiction" }, "", "Animation")]
    [TestCase(new[] { "drama", "crime" }, "", "Drama")]
    [TestCase(new[] { "comedy" }, "Anders Matthesen - Stand-up 2005", "Stand-up")]
    [TestCase(new[] { "horror", "thriller" }, "", "Gys")]
    [TestCase(new[] { "documentary", "music" }, "", "Dokumentar")]
    public void Film(string[] tags, string title, string genre) => GenreRules.Pick(F, tags, title).Should().Be(genre);

    [TestCase(new[] { "Fiction", "Detective and mystery stories" }, "Krimi")]
    [TestCase(new[] { "Juvenile fiction", "Fantasy" }, "Børnebøger")]
    [TestCase(new[] { "Danish fiction" }, "Romaner")]
    [TestCase(new[] { "History", "Denmark" }, "Fagbøger")]
    [TestCase(new[] { "Biography", "Musicians" }, "Biografi")]
    [TestCase(new string[0], "")]
    public void Books(string[] tags, string genre) => GenreRules.Pick(B, tags).Should().Be(genre);

    [Test]
    public void Every_rule_lands_in_the_shop_list()
    {
        foreach (var (cat, tags) in new[] { (M, new[] { "jazz" }), (F, new[] { "war" }), (B, new[] { "romance" }) })
            Genres.For(cat).Should().Contain(GenreRules.Pick(cat, tags));
    }

    [Test]
    public void Shop_genres_are_alphabetical_in_Danish()
    {
        ShopFamilies.Musik.Genres.Should().StartWith(["Blues", "Børnemusik", "Country", "Dansktop"]);
        ShopFamilies.Musik.Genres.Should().HaveCount(Genres.Musik.Length);
        ShopFamilies.Tegneserier.Genres.Should().BeEmpty();
    }

    [Test]
    public void Genre_from_a_link_is_matched_to_the_list()
    {
        Genres.Normalise("dansktop").Should().Be("Dansktop");
        Genres.Normalise("Nonsens").Should().BeNull();
        Genres.Normalise("Pop", [ProductTypes.Film]).Should().BeNull();
    }
}
