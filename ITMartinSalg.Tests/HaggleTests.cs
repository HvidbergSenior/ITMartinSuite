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

// Lån en konsol (2026-11-21): a weekend from a Friday, no deposit, PS3 free with 10 DVDs.
public class ConsoleLoanTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);   // a Saturday
    private static readonly DateTime Friday = new(2026, 11, 27);

    [Test]
    public void Asked_confirmed_handed_out_back()
    {
        var l = ConsoleLoan.Ask("48810359", "PS4", Friday, false, Now);
        l.PriceKr.Should().Be(100);
        l.Confirm(Now); l.HandOut(Now); l.Return(Now);
        l.Status.Should().Be(ConsoleLoan.Back);
    }

    [Test]
    public void Ps3_is_free_with_ten_dvds_but_ps5_is_not()
    {
        ConsoleLoan.Ask("48810359", "PS3", Friday, true, Now).PriceKr.Should().Be(0);
        ConsoleLoan.Ask("48810359", "PS5", Friday, true, Now).PriceKr.Should().Be(150);
    }

    [Test]
    public void Only_a_coming_friday_and_no_cancel_once_handed_out()
    {
        FluentActions.Invoking(() => ConsoleLoan.Ask("48810359", "PS3", Friday.AddDays(1), false, Now)).Should().Throw<InvalidOperationException>().WithMessage("*fredag*");
        FluentActions.Invoking(() => ConsoleLoan.Ask("48810359", "PS3", new DateTime(2026, 11, 20), false, Now)).Should().Throw<InvalidOperationException>();
        var l = ConsoleLoan.Ask("48810359", "PS3", Friday, false, Now);
        l.HandOut(Now);
        FluentActions.Invoking(() => l.Cancel(Now)).Should().Throw<InvalidOperationException>();
    }
}

// Hvad syntes du? (2026-11-21): few words, optional teaser, first name only, shown only when approved.
public class VerdictTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void A_verdict_keeps_only_the_first_name_and_waits_for_martin()
    {
        var v = ItemVerdict.Write("48810359", 3372, "Martin Hvidberg Nielsen", "  Uhyggelig   fra første side ", "", Now);
        (v.Name, v.Verdict, v.Status).Should().Be(("Martin", "Uhyggelig fra første side", ItemVerdict.New));
        v.Approve(Now);
        FluentActions.Invoking(() => v.Reject(Now)).Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void Empty_or_too_long_is_refused()
    {
        FluentActions.Invoking(() => ItemVerdict.Write("48810359", 1, "Bo", "", "", Now)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => ItemVerdict.Write("48810359", 1, "Bo", "ok", new string('x', 281), Now)).Should().Throw<InvalidOperationException>();
    }
}
