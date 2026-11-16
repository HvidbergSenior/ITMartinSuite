using FluentAssertions;
using ITMartinLager.Domain;
using NUnit.Framework;

namespace ITMartinLager.Tests;

// The price analysis (2026-11-16): classified ads never count, Discogs counts for records, Ny/Som ny = 25 % under Bog & idé
// with its link as proof, and our own sales move the suggestion once a kind has enough of them.
public class PriceAnalysisTests
{
    [Test]
    public void Classified_ads_never_count_and_Discogs_only_for_records()
    {
        Pricing.NormalDanishFirst([("DBA", 300), ("GulogGratis", 400)], Kinds.Cd).Should().BeNull();
        Pricing.NormalDanishFirst([("DBA", 300), ("Discogs", 11)], Kinds.Cd).Should().Be(11);
        Pricing.NormalDanishFirst([("Discogs", 11)], Kinds.Bog).Should().BeNull();
        Pricing.NormalDanishFirst([("GulogGratis", 60), ("Faraos", 13), ("Nemos Bibliotek", 15)], Kinds.Tegneserie).Should().Be(13);
    }

    [Test]
    public void A_book_as_new_is_25_percent_under_the_new_price_never_under_the_used_one()
    {
        var now = DateTime.UtcNow;
        var bogIde = ListingParse.Summarise([new Offer("Aggression", 160, "https://bogide.dk/1")], "u");
        var used = ListingParse.Summarise([new Offer("Aggression", 80, "https://faraos.dk/1")], "u");
        var answers = new List<SourceAnswer> { new("Faraos", false, used), new("Bog & idé", true, bogIde) };

        var asNew = new Item { Kind = Kinds.Bog, Title = "Aggression", Usage = Usages.AsNew, Confidence = 0.9 };
        HuntResult.From(1, answers, now, kind: Kinds.Bog).ApplyTo(asNew, now);
        asNew.PriceHint.Should().Be(120);                       // 160 x 0.75

        var brugt = new Item { Kind = Kinds.Bog, Title = "Aggression", Confidence = 0.9 };
        HuntResult.From(1, answers, now, kind: Kinds.Bog).ApplyTo(brugt, now);
        brugt.PriceHint.Should().Be(60);                        // used: 80 x 0.75 (loppemarked)
        Pricing.AsNewPrice(null).Should().BeNull();
    }

    [Test]
    public void The_proof_for_a_book_as_new_is_the_new_shops_link()
    {
        var finds = new[]
        {
            new PriceFind { Source = "Faraos", Kr = 80, Url = "https://faraos.dk/1" },
            new PriceFind { Source = "Bog & idé", Kr = 160, Url = "https://bogide.dk/1", IsNew = true },
        };
        Shop.Reference(finds, 120, likeNew: true)!.Url.Should().Be("https://bogide.dk/1");
        Shop.Reference(finds, 60)!.Url.Should().Be("https://faraos.dk/1");
    }

    [Test]
    public void Own_sales_move_the_suggestion_only_with_enough_of_them()
    {
        var few = Enumerable.Repeat((Kinds.Cd, 20, 10), SalesCalibration.MinSales - 1).ToList();
        SalesCalibration.Factor(few, Kinds.Cd).Should().Be(1m);
        var many = Enumerable.Repeat((Kinds.Cd, 20, 15), SalesCalibration.MinSales).ToList();
        SalesCalibration.Factor(many, Kinds.Cd).Should().Be(0.75m);
        SalesCalibration.Factor(many, Kinds.Bog).Should().Be(1m);
        SalesCalibration.Apply(40, 0.75m).Should().Be(30);
        SalesCalibration.Factor(Enumerable.Repeat((Kinds.Cd, 10, 100), SalesCalibration.MinSales), Kinds.Cd).Should().Be(SalesCalibration.MaxFactor);
    }

    [Test]
    public void A_sale_keeps_the_suggestion_it_had()
    {
        var item = new Item { Kind = Kinds.Cd, Title = "X", PriceHint = 20 };
        item.SellCopy(15, "", DateTime.UtcNow, "Kontant");
        item.HintAtSale.Should().Be(20);
    }

    // Only proper cut-outs in the shop (2026-11-16, "all the bad pictures I can not have in the shop").
    [TestCase("photos/items/1.jpg", 400, 600, false, "")]
    [TestCase("photos/items/1.jpg", 176, 1220, false, "smal stribe")]
    [TestCase("photos/items/1.jpg", 100, 200, false, "for lille")]
    [TestCase("", 0, 0, false, "hele bunken")]
    [TestCase("photos/items/1.jpg", 400, 600, true, "skjult")]
    public void The_shop_shows_only_proper_cut_outs(string photo, int w, int h, bool hidden, string problem)
    {
        var item = new Item { ItemPhoto = photo, PhotoId = 347, PhotoHidden = hidden, PhotoW = w > 0 ? w : null, PhotoH = h > 0 ? h : null, Title = "X" };
        item.ShopPhotoProblem.Should().Be(problem);
        Shop.View(item, []).HasPhoto.Should().Be(problem == "");
    }

    [Test]
    public void A_photo_not_measured_yet_is_shown() =>
        new Item { ItemPhoto = "photos/items/1.jpg", Title = "X" }.ShopPhotoProblem.Should().BeEmpty();
}
