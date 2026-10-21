using FluentAssertions;
using ITMartinLager.Domain;

namespace ITMartinLager.Tests;

// "some of the prices very high why?" (user 2026-10-21) -> other formats, collector copies, eBay asking prices and one
// site deciding alone. "our price should be an algorithm based on NOT the unrealistic ones".
public class RealisticPriceTests
{
    private static readonly DateTime Now = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);
    private static Offer O(string name, decimal kr) => new(name, kr, $"https://x/{name.GetHashCode()}/{kr}");

    [TestCase("Red Hot Chili Peppers - Californication LP", true)]
    [TestCase("Californication (Vinyl, 2LP)", true)]
    [TestCase("Red Hot Chili Peppers - Californication 7\" single", true)]
    [TestCase("Californication – signed by Flea", true)]
    [TestCase("Californication Deluxe Box", true)]
    [TestCase("Red Hot Chili Peppers - Californication (CD)", false)]
    [TestCase("Californication von Red Hot Chili Peppers | CD | Zustand gut", false)]
    public void A_CD_is_not_priced_from_records_or_signed_or_box_copies(string listing, bool other) =>
        Pricing.OtherFormat(Kinds.Cd, listing).Should().Be(other);

    [Test]
    public void Other_formats_per_kind()
    {
        Pricing.OtherFormat(Kinds.Vinyl, "U2 The Joshua Tree LP 1987").Should().BeFalse();
        Pricing.OtherFormat(Kinds.Vinyl, "U2 - The Joshua Tree (CD)").Should().BeTrue();
        Pricing.OtherFormat(Kinds.Dvd, "Heat Blu-ray").Should().BeTrue();
        Pricing.OtherFormat(Kinds.Dvd, "Heat 4K UHD").Should().BeTrue();
        Pricing.OtherFormat(Kinds.Dvd, "Heat (DVD, 1995)").Should().BeFalse();
        Pricing.OtherFormat(Kinds.Bog, "Mort LP").Should().BeFalse();   // books are not formats
    }

    [Test]
    public void First_editions_and_graded_copies_count_only_when_ours_is_one()
    {
        var ours = new Item { Kind = Kinds.Tegneserie, Title = "Som i et æventyr", Series = "Birger og Viola", Number = "3" };
        Pricing.CollectorCopy(ours, "Birger og Viola Nr. 3 Som i et æventyr (1. udgave, 1. oplag, Near Mint +)").Should().BeTrue();
        Pricing.CollectorCopy(ours, "Birger og Viola Nr. 3 Som i et æventyr (Fine/Very Fine)").Should().BeTrue();
        Pricing.CollectorCopy(ours, "Projekt X Klassiker nr. 2").Should().BeTrue();
        Pricing.CollectorCopy(ours, "Birger og Viola 3 – Som i et æventyr – tegneserie").Should().BeFalse();
        var first = new Item { Kind = Kinds.Bog, Title = "Hobbitten", Edition = "1. udgave, 1. oplag" };
        Pricing.CollectorCopy(first, "Tolkien: Hobbitten, 1. udgave 1962").Should().BeFalse();
    }

    [Test]
    public void One_vote_per_site_and_the_unrealistic_asks_left_out()
    {
        // One site full of dear listings does not decide it.
        Pricing.Realistic([("Tradera", 20), ("Tradera", 30), ("Kleinanzeigen", 25), ("Finn.no", 40), ("Finn.no", 600), ("Finn.no", 700)])
            .Should().Be(25);
        Pricing.Realistic([("DBA", 50)]).Should().Be(50);
        Pricing.Realistic([]).Should().BeNull();
    }

    [Test]
    public void eBay_is_shown_but_never_counted()
    {
        Pricing.NormalDanishFirst([("eBay", 291), ("eBay", 300), ("Tradera", 40)]).Should().Be(40);
        Pricing.NormalDanishFirst([("eBay", 291)]).Should().BeNull();
    }

    [Test]
    public void A_Danish_price_far_over_the_rest_is_not_trusted_alone()
    {
        // DBA's leftover listings say 170, four other used markets say 40-70.
        Pricing.NormalDanishFirst([("DBA", 150), ("DBA", 170), ("DBA", 190), ("Tradera", 40), ("Tradera", 54), ("Kleinanzeigen", 67), ("Tori.fi", 60)])
            .Should().BeLessThan(100);
        // Cheaper in Denmark: Danish first as always.
        Pricing.NormalDanishFirst([("DBA", 40), ("DBA", 60), ("AbeBooks", 400), ("ZVAB", 450)]).Should().Be(40);
    }

    [Test]
    public void The_Californication_CD_is_priced_from_CDs_only()
    {
        var item = new Item { Kind = Kinds.Cd, Title = "Californication", Artist = "Red Hot Chili Peppers", Confidence = 0.85, PriceHint = 150 };
        Listings L(params Offer[] o) => Pricing.OnlyThisItem(item, ListingParse.Summarise(o.ToList(), "u"));
        var answers = new List<SourceAnswer>
        {
            new("DBA", false, L(O("Red Hot Chili Peppers - Californication LP", 250), O("Californication vinyl", 300), O("RHCP Californication CD", 30), O("Californication, rock", 40))),
            new("eBay", false, L(O("Californication CD Zustand gut", 27), O("Californication CD", 291), O("Californication CD", 556))),
            new("Tradera", false, L(O("Red Hot Chili Peppers - Californication (CD)", 24), O("CD Red Hot Chili Peppers Californication", 27))),
            new("Kleinanzeigen", false, L(O("Californication CD", 30), O("Californication CD", 67))),
        };
        HuntResult.From(1, answers, Now).ApplyTo(item, Now);
        item.PriceHint.Should().BeInRange(20, 40);
    }

    [Test]
    public void Unsure_items_get_no_price_and_an_old_hint_is_not_kept()
    {
        var blank = new Item { Kind = Kinds.Cd, Title = "Lead Data", Confidence = 0.3, PriceHint = 85 };
        var answers = new List<SourceAnswer> { new("Tradera", false, ListingParse.Summarise([O("Lead Data", 87)], "u")) };
        HuntResult.From(1, answers, Now).ApplyTo(blank, Now);
        blank.PriceHint.Should().BeNull();

        var gone = new Item { Kind = Kinds.Bog, Title = "Mort", Confidence = 0.9, PriceHint = 220 };
        HuntResult.From(2, [new SourceAnswer("DBA", false, ListingParse.Summarise([], "u"))], Now).ApplyTo(gone, Now);
        gone.PriceHint.Should().BeNull();
    }
}
