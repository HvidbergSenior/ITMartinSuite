using FluentAssertions;
using ITMartinSalg.Domain;

namespace ITMartinSalg.Tests;

// Byd + Mit bundt (2026-11-05).
public class OfferTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void A_bid_is_the_hidden_most_a_customer_would_pay()
    {
        var b = Bid.Place("4881 0359", 12, 150, Now);
        b.Customer.Should().Be("48810359");
        b.MaxKr.Should().Be(150);
    }

    [TestCase(0)]
    [TestCase(100_001)]
    public void A_bid_outside_1_to_100000_kr_is_refused(int kr) =>
        FluentActions.Invoking(() => Bid.Place("48810359", 12, kr, Now)).Should().Throw<InvalidOperationException>();

    [Test]
    public void A_bundle_keeps_the_chosen_order_without_doubles_and_may_ask_for_a_price()
    {
        var b = BundleOffer.Make("48810359", [40, 12, 40, 77], null, Now);
        b.Ids.Should().Equal(40, 12, 77);
        b.AsksForPrice.Should().BeTrue();
        b.Status.Should().Be(BundleOffer.New);
    }

    [Test]
    public void A_bundle_is_at_least_two_things() =>
        FluentActions.Invoking(() => BundleOffer.Make("48810359", [12], 50, Now)).Should().Throw<InvalidOperationException>().WithMessage("*mindst 2*");

    [Test]
    public void A_bundle_needs_a_real_number() =>
        FluentActions.Invoking(() => BundleOffer.Make("123", [1, 2], 50, Now)).Should().Throw<InvalidOperationException>();

    [Test]
    public void Answered_is_remembered()
    {
        var b = BundleOffer.Make("48810359", [1, 2], 80, Now);
        b.MarkAnswered();
        b.Status.Should().Be(BundleOffer.Answered);
    }
}
