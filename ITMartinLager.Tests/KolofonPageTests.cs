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
    }

    [Test]
    public void Front_and_back_pages_add_up()
    {
        var book = new Item { Id = 4, Kind = Kinds.Bog, Title = "Ansigterne", Artist = "Tove Ditlevsen" };
        var now = DateTime.UtcNow;
        Kolofon.AddPage(book, new KolofonReading("", "1. udgave", null, "Gyldendal", ""), "photos/kolofon/4-1.jpg", now);
        Kolofon.AddPage(book, new KolofonReading("", "1. oplag 1968", 1968, "", ""), "photos/kolofon/4-2.jpg", now);
        book.Edition.Should().Be("1. udgave; 1. oplag 1968");
        book.Year.Should().Be(1968);
        book.Publisher.Should().Be("Gyldendal");
        book.KolofonPhoto.Should().Be("photos/kolofon/4-1.jpg");
        book.KolofonAt.Should().NotBeNull();
        Kolofon.AddPage(book, new KolofonReading("", "1. udgave", null, "", ""), "x.jpg", now);
        book.Edition.Should().Be("1. udgave; 1. oplag 1968", "the same words twice are not added again");
    }
}
