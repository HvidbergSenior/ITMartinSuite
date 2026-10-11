using FluentAssertions;
using ITMartinLager.Domain;

namespace ITMartinLager.Tests;

public class KolofonTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 18, 0, 0, DateTimeKind.Utc);

    [Test]
    public void A_read_kolofon_sets_edition_publisher_year_and_a_new_isbn_makes_the_hunter_search_again()
    {
        var item = new Item { Kind = Kinds.Bog, Title = "Mort", Year = 1990, PricesAt = Now };
        Kolofon.Apply(item, new KolofonReading("ISBN 0-575-04171-4", " First edition ", 1987, "Victor Gollancz", null), Now);
        item.Barcode.Should().Be("9780575041714");
        item.Edition.Should().Be("First edition");
        item.Publisher.Should().Be("Victor Gollancz");
        item.Year.Should().Be(1987);
        item.PricesAt.Should().BeNull();
        item.KolofonAt.Should().Be(Now);
        item.Search.Should().Contain("first edition");
    }

    [Test]
    public void Unreadable_kolofon_keeps_what_the_book_had()
    {
        var item = new Item { Kind = Kinds.Bog, Title = "Mort", Year = 1990, Barcode = "9780552131063", PricesAt = Now };
        Kolofon.Apply(item, new KolofonReading(null, null, null, null, "Ikke en kolofon"), Now);
        item.Barcode.Should().Be("9780552131063");
        item.Year.Should().Be(1990);
        item.PricesAt.Should().Be(Now);
        item.Note.Should().Contain("Ikke en kolofon");
    }

    [Test]
    public void A_new_photo_waits_to_be_read()
    {
        var item = new Item { Kind = Kinds.Bog, KolofonAt = Now };
        Kolofon.PhotoTaken(item, "photos/kolofon/1.jpg");
        item.KolofonPhoto.Should().Be("photos/kolofon/1.jpg");
        item.KolofonAt.Should().BeNull();
    }
}
