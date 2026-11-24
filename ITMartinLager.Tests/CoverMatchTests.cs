using FluentAssertions;
using ITMartinLager.Domain;

namespace ITMartinLager.Tests;

// Same cover or another edition (user 2026-11-24: "look at the pictures from front cover").
public class CoverMatchTests
{
    [TestCase(400, 600, true)]    // a book cover
    [TestCase(600, 600, true)]    // a CD
    [TestCase(483, 155, false)]   // a DVD spine cut from a pile
    [TestCase(60, 540, false)]
    [TestCase(null, null, false)]
    public void Only_a_front_cover_is_compared(int? w, int? h, bool cover) => CoverMatch.CoverShaped(w, h).Should().Be(cover);

    [TestCase(5, true)]
    [TestCase(19, true)]
    [TestCase(20, false)]
    [TestCase(-1, false)]   // picture not fetched = unknown, keeps counting
    public void Few_matching_points_is_another_edition(int points, bool other) =>
        CoverMatch.OtherEdition(new PriceFind { Cover = points }).Should().Be(other);

    [Test]
    public void Not_compared_keeps_counting() => CoverMatch.OtherEdition(new PriceFind()).Should().BeFalse();

    [Test]
    public void Another_edition_does_not_set_the_price()
    {
        var item = new Item { Kind = Kinds.Bog, Title = "Goodbye Mr. Chips", Artist = "James Hilton" };
        var finds = new[]
        {
            new PriceFind { Source = "DBA", Name = "Goodbye Mr. Chips", Kr = 30, Cover = 84 },
            new PriceFind { Source = "DBA", Name = "GOOD-BYE, MR. CHIPS, James Hilton", Kr = 195, Cover = 6 },
        };
        FixedPrice.Chosen(finds, item).Should().Equal(30);
    }
}
