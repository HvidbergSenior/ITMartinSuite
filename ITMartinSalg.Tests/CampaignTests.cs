using FluentAssertions;
using ITMartinSalg.Domain;

namespace ITMartinSalg.Tests;

// Day offers, the basket and a sale's steps (user 2026-10-20: "books monday -> 50 pct on used books. Also Monday 10 for
// 800 ... Or 31. in every month 10 for 300" and "sold, packed, in storage or something").
public class CampaignTests
{
    private static readonly DateOnly Monday = new(2026, 10, 19), Tuesday = new(2026, 10, 20), The31st = new(2026, 10, 31);

    private static List<Campaign> On() => Campaign.Examples().Select(c => { c.Active = true; return c; }).ToList();
    private static BasketLine Book(int id, int kr) => new(id, Kinds.Bog, kr);
    private static BasketLine Film(int id, int kr) => new(id, Kinds.Dvd, kr);

    [Test]
    public void The_examples_are_off_until_a_person_turns_them_on()
    {
        Campaign.Examples().Should().OnlyContain(c => !c.Active && c.Valid);
        Campaigns.Price([Book(1, 100)], Campaign.Examples(), Monday).Total.Should().Be(100);
    }

    [Test]
    public void Book_monday_halves_books_and_only_books()
    {
        var p = Campaigns.Price([Book(1, 100), Film(2, 60)], On(), Monday);
        p.Lines.Select(l => (l.ItemId, l.Kr)).Should().Equal((1, 50), (2, 60));
        p.Lines[0].Offer.Should().Be("50 % på brugte bøger");
        p.Saved.Should().Be(50);
        Campaigns.Price([Book(1, 100)], On(), Tuesday).Total.Should().Be(100);   // not a Monday
        Campaigns.DayPrice(Kinds.Bog, Usages.Used, 99, On(), Monday).Should().Be(50);
        Campaigns.DayPrice(Kinds.Bog, Usages.New, 99, On(), Monday).Should().BeNull();   // "50 pct on used books" - a new book is not covered
        Campaigns.DayPrice(Kinds.Dvd, Usages.Used, 99, On(), Monday).Should().BeNull();
    }

    [Test]
    public void Ten_for_800_on_monday_takes_the_dearest_ten_and_the_rest_keeps_its_offer()
    {
        var basket = Enumerable.Range(1, 11).Select(n => Film(n, 100 + n)).Append(Book(99, 40)).ToList();
        var p = Campaigns.Price(basket, On(), Monday);

        var bundle = p.Lines.Where(l => l.Offer == "10 for 800 kr").ToList();
        bundle.Should().HaveCount(10);
        bundle.Sum(l => l.Kr).Should().Be(800);
        bundle.Select(l => l.ItemId).Should().NotContain(1);                        // the cheapest film is left out
        p.Lines.Single(l => l.ItemId == 1).Kr.Should().Be(101);
        // The book is in the bundle only if that saved more; here it is not one of the dearest ten, so it gets the 50 %.
        p.Lines.Single(l => l.ItemId == 99).Kr.Should().Be(20);
        p.Lines.Select(l => l.ItemId).Should().Equal(basket.Select(b => b.ItemId));   // the basket's own order
    }

    [Test]
    public void A_bundle_is_only_used_when_it_is_cheaper_than_paying_one_by_one()
    {
        var cheap = Enumerable.Range(1, 10).Select(n => Film(n, 50)).ToList();   // 500 kr one by one
        Campaigns.Price(cheap, On(), Tuesday).Total.Should().Be(500);             // 10 for 700 would cost more
        Campaigns.Price(cheap.Take(9).ToList(), On(), Monday).Total.Should().Be(450);   // nine is not a bundle
    }

    [Test]
    public void On_the_31st_ten_for_300_and_never_in_a_month_without_one()
    {
        var ten = Enumerable.Range(1, 10).Select(n => Film(n, 60)).ToList();
        var p = Campaigns.Price(ten, On(), The31st);   // a Saturday: only the 31st offer
        p.Total.Should().Be(300);
        p.Lines.Should().OnlyContain(l => l.Kr == 30 && l.Offer == "10 for 300 kr");
        Campaigns.Price(ten, On(), new DateOnly(2026, 11, 30)).Total.Should().Be(600);
    }

    [Test]
    public void The_cheapest_bundle_per_thing_wins_when_two_run_the_same_day()
    {
        // The 31st of August 2026 is a Monday: 10 for 300 beats 10 for 800.
        var ten = Enumerable.Range(1, 10).Select(n => Film(n, 100)).ToList();
        Campaigns.Price(ten, On(), new DateOnly(2026, 8, 31)).Total.Should().Be(300);
    }

    [Test]
    public void Text_and_when_read_as_the_buyer_sees_them()
    {
        var c = On();
        (c[0].Text, c[0].When).Should().Be(("50 % på brugte bøger", "Hver mandag"));
        (c[1].Text, c[1].When).Should().Be(("10 for 800 kr", "Hver mandag"));
        c[^1].When.Should().Be("Den 31. i måneden");
    }
}

// New or used (user 2026-10-20: "there should be a used and new field").
public class OfferUsageTests
{
    [Test]
    public void An_offer_for_new_things_only_and_the_text_says_so()
    {
        var c = new Campaign { Kind = CampaignKinds.Percent, ProductType = ProductTypes.Film, Usage = Usages.New, Percent = 20, Active = true };
        c.Text.Should().Be("20 % på nye film");
        c.Fits(Kinds.Dvd, Usages.New).Should().BeTrue();
        c.Fits(Kinds.Dvd, Usages.Used).Should().BeFalse();
        new Campaign { Kind = CampaignKinds.Bundle, Usage = Usages.Used, Count = 10, BundleKr = 300 }.Text.Should().Be("10 for 300 kr (alt brugt)");
    }
}
