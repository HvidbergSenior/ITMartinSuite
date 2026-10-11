using FluentAssertions;
using ITMartinLager.Server.Data;
using ITMartinLager.Server.Services;
using NUnit.Framework;

namespace ITMartinLager.Tests;

public class KolofonTests
{
    [Test]
    public void A_read_kolofon_sets_edition_publisher_year_and_a_new_isbn_makes_the_hunter_search_again()
    {
        var item = new Item { Kind = Kinds.Bog, Title = "Mort", Year = 1990, PricesAt = DateTime.UtcNow };
        Kolofon.Apply(item, new PileReader.KolofonRow
        {
            Isbn = "ISBN 0-575-04171-4", Edition = " First edition ", Year = 1987, Publisher = "Victor Gollancz",
        });
        item.Barcode.Should().Be("9780575041714");
        item.Edition.Should().Be("First edition");
        item.Publisher.Should().Be("Victor Gollancz");
        item.Year.Should().Be(1987);
        item.PricesAt.Should().BeNull();
        item.KolofonAt.Should().NotBeNull();
        item.Search.Should().Contain("first edition");
    }

    [Test]
    public void Unreadable_kolofon_keeps_what_the_book_had()
    {
        var at = DateTime.UtcNow;
        var item = new Item { Kind = Kinds.Bog, Title = "Mort", Year = 1990, Barcode = "9780552131063", PricesAt = at };
        Kolofon.Apply(item, new PileReader.KolofonRow { Note = "Ikke en kolofon" });
        item.Barcode.Should().Be("9780552131063");
        item.Year.Should().Be(1990);
        item.PricesAt.Should().Be(at);
        item.Note.Should().Contain("Ikke en kolofon");
    }
}
