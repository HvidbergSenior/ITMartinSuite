using FluentAssertions;
using ITMartinSalg.Domain;

namespace ITMartinSalg.Tests;

// Haggling (user 2026-11-21: "both buyer and seller can say all these for this price", "found on Faraos -> link, your offer").
public class HaggleTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Bid_counter_counter_agreed()
    {
        var b = BundleOffer.Make("48810359", [12, 40], 100, Now);
        b.ShopsTurn.Should().BeTrue();
        b.ShopCounters(180, Now);
        (b.Status, b.PriceForCustomer).Should().Be((BundleOffer.Counter, 180));
        b.CustomerCounters(150, Now);
        (b.Status, b.Kr).Should().Be((BundleOffer.New, 150));
        b.ShopAccepts(Now);
        (b.Status, b.AgreedKr).Should().Be((BundleOffer.Agreed, 150));
        b.IsDone.Should().BeTrue();
    }

    [Test]
    public void Martin_offers_all_these_for_a_price_and_the_customer_says_yes()
    {
        var b = BundleOffer.FromShopOffer("48810359", [1, 2, 3], 150, Now);
        (b.FromShop, b.Status, b.PriceForCustomer).Should().Be((true, BundleOffer.Counter, 150));
        b.CustomerAccepts(Now);
        b.AgreedKr.Should().Be(150);
    }

    [Test]
    public void One_thing_is_a_bundle_of_one()
    {
        var b = BundleOffer.Single("48810359", 3372, 300, Now);
        (b.Ids.Count, b.Kr, b.Status).Should().Be((1, 300, BundleOffer.New));
    }

    [Test]
    public void The_automatic_answer_can_be_accepted()
    {
        var b = BundleOffer.Make("48810359", [12, 40], null, Now);
        b.AnswerAutomatically(90, 10);
        b.CustomerAccepts(Now);
        b.AgreedKr.Should().Be(90);
    }

    [Test]
    public void No_moves_after_it_is_done_and_no_turn_jumping()
    {
        var b = BundleOffer.Make("48810359", [12, 40], 100, Now);
        FluentActions.Invoking(() => b.CustomerAccepts(Now)).Should().Throw<InvalidOperationException>();   // waiting for Martin
        b.Reject(Now);
        FluentActions.Invoking(() => b.ShopCounters(120, Now)).Should().Throw<InvalidOperationException>();
    }
}
