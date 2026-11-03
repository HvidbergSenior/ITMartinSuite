using FluentAssertions;
using ITMartinLager.Domain;
using ITMartinVarer.Contracts;
using NUnit.Framework;

namespace ITMartinLager.Tests;

// Helpers only take pictures (user 2026-11-03): a kolofon page read with the box goes on its book, several pages add up.
public class KolofonPageTests
{
    private static readonly Item Ansigterne = new() { Id = 1, Kind = Kinds.Bog, Title = "Ansigterne", Artist = "Tove Ditlevsen" };
    private static readonly Item Elsk = new() { Id = 2, Kind = Kinds.Bog, Title = "Elsk det som er", Artist = "Byron Katie", Barcode = "9788770000000" };
    private static readonly Item Cd = new() { Id = 3, Kind = Kinds.Cd, Title = "Ansigterne", Artist = "Nogen" };

    [Test]
    public void A_page_finds_its_book_by_title_and_author_or_isbn()
    {
        Kolofon.MatchPage([Ansigterne, Elsk, Cd], "Ansigterne", "Tove Ditlevsen", "").Should().Be(Ansigterne);
        Kolofon.MatchPage([Ansigterne, Elsk], "", "", "9788770000000").Should().Be(Elsk);
        Kolofon.MatchPage([Ansigterne, Elsk], "Ansigterne", "Klaus Rifbjerg", "").Should().BeNull("another author");
        Kolofon.MatchPage([Ansigterne, Elsk], "Noget helt andet", "", "").Should().BeNull();
        // Rico 2026-11-03: "© Tove Ditlevsen, 1968" - no title on the page: the one book by that author in the box.
        Kolofon.MatchPage([Ansigterne, Elsk, Cd], "", "Tove Ditlevsen", "").Should().Be(Ansigterne);
        var other = new Item { Id = 9, Kind = Kinds.Bog, Title = "Vilhelms værelse", Artist = "Tove Ditlevsen" };
        Kolofon.MatchPage([Ansigterne, other], "", "Tove Ditlevsen", "").Should().BeNull("two books by her - a person decides");
    }

    [Test]
    public void Front_and_back_pages_add_up()
    {
        var book = new Item { Id = 4, Kind = Kinds.Bog, Title = "Ansigterne", Artist = "Tove Ditlevsen" };
        var now = DateTime.UtcNow;
        Kolofon.AddPage(book, new KolofonReading("", "1. udgave", null, "Gyldendal", ""), "photos/kolofon/4-1.jpg", now);
        Kolofon.AddPage(book, new KolofonReading("", "1. oplag 1968", 1968, "Det Hoffensbergske Etablissement", ""), "photos/kolofon/4-2.jpg", now);
        book.Edition.Should().Be("1. udgave; 1. oplag 1968");
        book.Year.Should().Be(1968);
        book.Publisher.Should().Be("Gyldendal");
        book.KolofonPhoto.Should().Be("photos/kolofon/4-1.jpg");
        book.KolofonAt.Should().NotBeNull();
        Kolofon.AddPage(book, new KolofonReading("", "1. udgave", null, "", ""), "x.jpg", now);
        book.Edition.Should().Be("1. udgave; 1. oplag 1968", "the same words twice are not added again");
    }
    [Test]
    public void A_kolofon_without_a_later_oplag_makes_a_likely_first_edition_and_a_new_search()
    {
        var book = new Item { Id = 6, Kind = Kinds.Bog, Title = "Ansigterne", Artist = "Tove Ditlevsen", PricesAt = DateTime.UtcNow };
        Kolofon.AddPage(book, new KolofonReading("", "Ingen udgave/oplag nævnt (© 1968)", 1968, "", ""), "k.jpg", DateTime.UtcNow);
        Kolofon.LikelyFirst(book).Should().BeTrue();
        book.PricesAt.Should().BeNull("the edition changed - search again");
        Pricing.CollectorCopy(book, "Ansigterne (1. oplag 1968)").Should().BeFalse("ours is likely a 1st too, so first-edition listings count");
        var later = new Item { Kind = Kinds.Bog, Title = "Ansigterne", Year = 1972, Edition = "Ingen udgave/oplag nævnt (© 1968)" };
        Kolofon.LikelyFirst(later).Should().BeFalse("a 1972 copy of a 1968 book is a later printing");
        Pricing.CollectorCopy(new Item { Kind = Kinds.Bog, Title = "Ansigterne" }, "Ansigterne (1. oplag 1968)").Should().BeTrue();
    }

    [Test]
    public void A_short_title_with_the_same_author_is_the_same_book()
    {
        var mitLiv = new Item { Id = 5, Kind = Kinds.Bog, Title = "Mit liv", Artist = "Jørn Jønke", Year = 1985 };
        mitLiv.LooksLike(new Found(Kinds.Bog, "Mit liv – Tiderne Skifter", "Jønke", "", "", null, "", "God", 1, 0.9, "")).Should().BeTrue();
        mitLiv.LooksLike(new Found(Kinds.Bog, "Mit liv – Tiderne Skifter", "Karen Blixen", "", "", null, "", "God", 1, 0.9, "")).Should().BeFalse();
        mitLiv.LooksLike(new Found(Kinds.Bog, "Mit liv", "", "", "", null, "", "God", 1, 0.9, "")).Should().BeTrue("the same title");
    }
}
