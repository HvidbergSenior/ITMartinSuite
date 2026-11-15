using FluentAssertions;
using ITMartinLager.Domain;

namespace ITMartinLager.Tests;

// The Vare page shows only the fields that fit the kind (user 2026-11-15: "only relevant stuff") - and never hides one that holds something.
public class ItemFieldsTests
{
    [Test]
    public void A_book_shows_book_fields_and_a_blad_shows_series_and_number()
    {
        var book = new Item { Kind = Kinds.Bog, Title = "Mort" };
        ItemFields.Shows(book, ItemFields.Publisher).Should().BeTrue();
        ItemFields.Shows(book, ItemFields.Platform).Should().BeFalse();
        ItemFields.ArtistLabel(Kinds.Bog).Should().Be("Forfatter");

        var blad = new Item { Kind = Kinds.AndersAnd };
        ItemFields.Shows(blad, ItemFields.Number).Should().BeTrue();
        ItemFields.Shows(blad, ItemFields.Barcode).Should().BeFalse();
    }

    [Test]
    public void A_field_that_already_holds_something_is_always_shown()
    {
        var game = new Item { Kind = Kinds.Konsolspil, Publisher = "Nintendo" };
        ItemFields.Shows(game, ItemFields.Publisher).Should().BeTrue();
        ItemFields.Shows(game, ItemFields.Edition).Should().BeFalse();
    }
}
