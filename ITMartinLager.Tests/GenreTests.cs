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
    [TestCase(new[] { "History", "Denmark" }, "Historie")]
    [TestCase(new[] { "Law", "Economics" }, "Fagbøger")]
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
        ShopFamilies.Tegneserier.Genres.Should().Contain(["Disney", "Krimi", "Western"]);
        ShopFamilies.Kort.Genres.Should().BeEmpty();
    }

    [Test]
    public void Genre_from_a_link_is_matched_to_the_list()
    {
        Genres.Normalise("dansktop").Should().Be("Dansktop");
        Genres.Normalise("Nonsens").Should().BeNull();
        Genres.Normalise("Pop", [ProductTypes.Film]).Should().BeNull();
    }

    // 2026-11-07 later: "as many genres for all the shops - krimi, etc?"
    [TestCase(new[] { "punk rock" }, "Punk")]
    [TestCase(new[] { "christmas", "pop" }, "Julemusik")]
    [TestCase(new[] { "reggae" }, "Reggae")]
    [TestCase(new[] { "disco", "pop" }, "Disco")]
    public void More_music(string[] tags, string genre) => GenreRules.Pick(M, tags).Should().Be(genre);

    [TestCase(new[] { "adventure", "drama" }, "Eventyr")]
    [TestCase(new[] { "history", "drama" }, "Historie")]
    [TestCase(new[] { "music", "romance" }, "Musical")]
    public void More_film(string[] tags, string genre) => GenreRules.Pick(F, tags).Should().Be(genre);

    [TestCase(new[] { "Cooking", "Danish" }, "Kogebøger")]
    [TestCase(new[] { "World War, 1939-1945", "Denmark" }, "Krig")]
    [TestCase(new[] { "Spy stories", "Fiction" }, "Thriller")]
    [TestCase(new[] { "Young adult fiction" }, "Ungdomsbøger")]
    [TestCase(new[] { "Poetry" }, "Digte")]
    [TestCase(new[] { "Travel", "Italy" }, "Rejser")]
    public void More_books(string[] tags, string genre) => GenreRules.Pick(B, tags).Should().Be(genre);

    // Comics from the series (the real series in stock 2026-11-07).
    [TestCase(ProductTypes.Serieblade, "Anders And & Co.", "Nr. 12", "Disney")]
    [TestCase(ProductTypes.Jumbo, "Anders And Jumbobog", "Jumbobog 45", "Disney")]
    [TestCase(ProductTypes.Serieblade, "Jonah Hex", "Krigsret!", "Western")]
    [TestCase(ProductTypes.Tegneserier, "Blueberry", "Den forsvundne guldmine", "Western")]
    [TestCase(ProductTypes.Serieblade, "Basserne", "Nr. 3", "Humor")]
    [TestCase(ProductTypes.GamleBlade, "Kong Kylie", "Nr. 7", "Humor")]
    [TestCase(ProductTypes.Superhelte, "Projekt X", "Nr. 2", "Superhelte")]
    [TestCase(ProductTypes.Superhelte, "Unknown hero", "Nr. 1", "Superhelte")]
    [TestCase(ProductTypes.Tegneserier, "Agent 327", "Dossier Cyclops", "Krimi")]
    [TestCase(ProductTypes.Tegneserier, "Kystbane Komix", "1", "Underground")]
    [TestCase(ProductTypes.Tegneserier, "", "Free Comic Book Day 2010", "")]
    public void Comics(string category, string series, string title, string genre) =>
        GenreRules.Pick(category, [], title, series).Should().Be(genre);

    [TestCase(ProductTypes.Computerspil, "FIFA 2005", "Sport")]
    [TestCase(ProductTypes.Computerspil, "Gran Turismo 4", "Racing")]
    [TestCase(ProductTypes.Computerspil, "Galactic Civilizations IV", "Strategi")]
    [TestCase(ProductTypes.Braetspil, "Trivial Pursuit Familie", "Quiz")]
    [TestCase(ProductTypes.Braetspil, "Settlers of Catan", "Strategispil")]
    [TestCase(ProductTypes.Rollespil, "Dungeons & Dragons Player's Handbook", "Dungeons & Dragons")]
    [TestCase(ProductTypes.Rollespil, "Mutant: År Noll", "Andre rollespil")]
    public void Games(string category, string title, string genre) => GenreRules.Pick(category, [], title).Should().Be(genre);

    [Test]
    public void Comics_and_games_are_read_from_their_name()
    {
        GenreRules.FromName(ProductTypes.Serieblade).Should().BeTrue();
        GenreRules.FromName(ProductTypes.Musik).Should().BeFalse();
        Genres.Categories.Should().Contain([ProductTypes.Superhelte, ProductTypes.Braetspil]);
    }
}
