using FluentAssertions;
using ITMartinLager.Application;
using ITMartinLager.Domain;
using ITMartinLager.Infrastructure.External;
using ITMartinLager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Moq;

namespace ITMartinLager.Tests;

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

public class SaleStepTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Order Paid(string delivery = "Send med posten")
    {
        var item = new Item { Id = 1, Kind = Kinds.Bog, Title = "Mort", Quantity = 2 };
        var o = Order.For([(item, new PricedLine(1, 80, 80, ""))], Now);
        o.Pay(new Dictionary<int, Item> { [1] = item }, new PaymentState(true, false, 129, "a@b.dk", "Anna", Delivery: delivery), Now);
        return o;
    }

    [Test]
    public void Paid_then_packed_then_sent_with_the_parcel_number()
    {
        var o = Paid();
        o.Pack(Now);
        o.Send(" 00370712345678 ", Now);
        (o.Status, o.Tracking, o.PackedAt, o.DoneAt).Should().Be((OrderStatuses.Sent, "00370712345678", Now, Now));
        o.Paid.Should().BeTrue();
    }

    [Test]
    public void Steps_only_go_forwards_and_undo_goes_back_one()
    {
        var o = Paid("Afhentning");
        o.PickUp(Now);
        o.Invoking(x => x.Pack(Now)).Should().Throw<InvalidOperationException>();
        o.Undo();
        o.Status.Should().Be(OrderStatuses.Packed);   // picked up straight away still counts as packed
        o.Undo();
        o.Status.Should().Be(OrderStatuses.Paid);

        var pending = Order.For([(new Item { Id = 2, Title = "x" }, new PricedLine(2, 10, 10, ""))], Now);
        pending.Invoking(x => x.Send("", Now)).Should().Throw<InvalidOperationException>();
        pending.Expire();
        pending.Status.Should().Be(OrderStatuses.Expired);
    }

    [Test]
    public void A_basket_order_names_the_first_thing_and_how_many_more()
    {
        var a = new Item { Id = 1, Title = "Mort" };
        var b = new Item { Id = 2, Title = "Eric" };
        var c = new Item { Id = 3, Title = "Snuff" };
        Order.For([(a, new PricedLine(1, 50, 40, "x")), (b, new PricedLine(2, 50, 40, "x")), (c, new PricedLine(3, 50, 40, "x"))], Now)
            .Should().Match<Order>(o => o.Line == "Mort + 2 andre" && o.Kr == 120 && o.Lines.Count == 3 && o.ItemId == 1);
        Order.For([(a, new PricedLine(1, 50, 50, "")), (b, new PricedLine(2, 50, 50, ""))], Now).Line.Should().Be("Mort + 1 anden");
    }
}

public class BasketTests
{
    private SqliteConnection _conn = null!;
    private TestDbFactory Factory => new(_conn);
    private ItemRepository Items => new(Factory);
    private readonly LagerOptions _opt = new() { ShopPickup = "Aarhus C efter aftale", ShopShippingKr = 49 };

    [SetUp]
    public void Open()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        using var db = Factory.CreateDbContext();
        LagerDbInitializer.Upgrade(db);
    }

    [TearDown]
    public void Close() => _conn.Dispose();

    /// <summary>A clock on a given day.</summary>
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private async Task<Item> Add(string kind, string title, decimal price, int qty = 1)
    {
        var item = new Item { Kind = kind, Title = title, Price = price, Quantity = qty, Confidence = 0.9 };
        item.Touch();
        await Items.AddAsync([item], default);
        return item;
    }

    private (ShopUseCases Shop, Mock<IPaymentGateway> Pay) Make(DateTimeOffset day)
    {
        var pay = new Mock<IPaymentGateway>();
        pay.SetupGet(p => p.Enabled).Returns(true);
        pay.Setup(p => p.StartAsync(It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("cs_test_1", "https://checkout.stripe.com/c/pay/cs_test_1"));
        return (new ShopUseCases(Items, new PriceRepository(Factory), new OrderRepository(Factory), pay.Object, new CampaignRepository(Factory),
            new GoldRepository(Factory), _opt, new Clock(day)), pay);
    }

    private async Task TurnOnAsync(string name)
    {
        var repo = new CampaignRepository(Factory);
        var c = (await repo.AllAsync(default)).Single(x => x.Name == name);
        c.Active = true;
        await repo.SaveAsync(c, default);
    }

    [Test]
    public async Task The_examples_are_in_the_database_switched_off()
    {
        var all = await new CampaignRepository(Factory).AllAsync(default);
        all.Should().HaveCount(Campaign.Examples().Count).And.OnlyContain(c => !c.Active);
        using var db = Factory.CreateDbContext();
        LagerDbInitializer.Upgrade(db);   // a second start adds no more
        (await new CampaignRepository(Factory).AllAsync(default)).Should().HaveCount(all.Count);
    }

    [Test]
    public async Task A_basket_on_book_monday_is_quoted_bought_and_paid_at_the_day_price()
    {
        await TurnOnAsync("Bogmandag");
        var mort = await Add(Kinds.Bog, "Mort", 100, qty: 2);
        var film = await Add(Kinds.Dvd, "Heat", 60);
        var (shop, pay) = Make(new DateTimeOffset(2026, 10, 19, 10, 0, 0, TimeSpan.Zero));   // a Monday

        var quote = await shop.QuoteAsync([mort.Id, mort.Id, film.Id, 999]);
        quote.Lines.Select(l => l.Kr).Should().Equal(50, 50, 60);
        (quote.Total, quote.Saved).Should().Be((160, 100));
        quote.Gone.Should().Equal(999);
        quote.Today.Single().Text.Should().Be("50 % på brugte bøger");
        (await shop.MarketAsync(ProductTypes.Boeger, null, null, null, null, 0)).Items.Single().DayKr.Should().Be(50);

        // Something gone = no payment; the buyer sees the basket again first.
        await shop.Invoking(s => s.BuyAsync([mort.Id, 999], "https://bogshoppen.dk")).Should().ThrowAsync<LagerException>().WithMessage("*kurven*");

        await shop.BuyAsync([mort.Id, mort.Id, film.Id], "https://bogshoppen.dk");
        pay.Verify(p => p.StartAsync(It.Is<Order>(o => o.Kr == 160 && o.Lines.Count == 3), It.IsAny<string>(), It.IsAny<string>(),
            "https://bogshoppen.dk/kurv", 49, It.IsAny<string>(), It.IsAny<CancellationToken>()));
        (await shop.QuoteAsync([mort.Id])).Gone.Should().Equal(mort.Id);   // both copies are reserved now

        pay.Setup(p => p.StateAsync("cs_test_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentState(true, false, 209, "a@b.dk", "Anna", Delivery: "Send med posten"));
        (await shop.SyncAsync()).Should().Be(1);
        (await Items.GetAsync(mort.Id, default))!.Should().Match<Item>(i => i.Status == Statuses.Solgt && i.SoldPrice == 50);
        (await Items.GetAsync(film.Id, default))!.Status.Should().Be(Statuses.Solgt);

        var order = (await shop.RecentOrdersAsync()).Single();
        await shop.PackAsync(order.Id);
        await shop.SendAsync(order.Id, "0037");
        (await shop.RecentOrdersAsync()).Single().Should().Match<Order>(o => o.Status == OrderStatuses.Sent && o.Tracking == "0037");
        await shop.Invoking(s => s.PackAsync(order.Id)).Should().ThrowAsync<LagerException>();
    }

    [Test]
    public async Task The_marketplace_counts_per_product_type_and_filters_by_type()
    {
        await Add(Kinds.Bog, "Mort", 50);
        await Add(Kinds.Bog, "Eric", 40);
        await Add(Kinds.Vinyl, "Abbey Road", 200);
        await Add(Kinds.Cd, "Abbey Road", 60);
        var (shop, _) = Make(DateTimeOffset.UtcNow);

        var all = await shop.MarketAsync(null, null, null, null, ShopSorts.Dearest, 0);
        all.Types.Should().BeEquivalentTo([new ShopType(ProductTypes.Boeger, 2), new ShopType(ProductTypes.Musik, 2)]);
        all.Items.First().Title.Should().Be("Abbey Road");
        all.Total.Should().Be(4);

        var music = await shop.MarketAsync(ProductTypes.Musik, null, null, "abbey", ShopSorts.Cheapest, 0);
        music.Items.Select(i => (i.Kind, i.BuyNowKr)).Should().Equal((Kinds.Cd, 60), (Kinds.Vinyl, 200));
        music.Items.Should().OnlyContain(i => i.Type == ProductTypes.Musik);
    }

    [Test]
    public void An_old_database_gets_a_line_per_order_and_sent_or_picked_up_from_the_old_tick()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using (var db0 = new TestDbFactory(conn).CreateDbContext()) LagerDbInitializer.Upgrade(db0);
        using (var cmd = conn.CreateCommand())
        {
            // An order the way they were saved before baskets, with the old "Handled" tick.
            cmd.CommandText = """
                ALTER TABLE "Orders" ADD COLUMN "Handled" INTEGER NOT NULL DEFAULT 0;
                INSERT INTO "Orders" ("Token", "ItemId", "Line", "Kr", "Status", "Channel", "CheckoutId", "CreatedAt", "ExpiresAt", "PaidKr",
                    "Email", "Name", "Phone", "Address", "Delivery", "Tracking", "Handled", "PaidAt")
                VALUES ('t1', 7, 'Mort', 80, 'Betalt', 'Køb nu', '', '2026-10-15 10:00:00', '2026-10-15 10:31:00', 80, '', '', '', '', 'Afhentning', '', 1, '2026-10-15 10:05:00');
                """;
            cmd.ExecuteNonQuery();
        }
        using var db = new TestDbFactory(conn).CreateDbContext();
        LagerDbInitializer.Upgrade(db);

        var o = new OrderRepository(new TestDbFactory(conn)).ByTokenAsync("t1", default).Result!;
        o.Status.Should().Be(OrderStatuses.PickedUp);
        o.Lines.Single().Should().Match<OrderLine>(l => l.ItemId == 7 && l.Kr == 80);
    }
}

// New or used (user 2026-10-20: "there should be a used and new field").
public class UsageTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    private static Offer O(string name, decimal kr) => new(name, kr, "https://x/" + kr);

    [Test]
    public void Anything_unknown_is_used()
    {
        Usages.Normalise("ny").Should().Be(Usages.New);
        Usages.Normalise(null).Should().Be(Usages.Used);
        Usages.Normalise("forseglet").Should().Be(Usages.Used);
        Item.From(new Found(Kinds.Bog, "Mort", "", "", "", null, "", "God", 1, 0.9, "", Usage: "Ny"), null, null).Usage.Should().Be(Usages.New);
    }

    [Test]
    public void A_new_copy_is_priced_under_the_new_shops_and_a_used_one_under_the_used_market()
    {
        var answers = new List<SourceAnswer>
        {
            new("DBA", false, ListingParse.Summarise([O("Mort", 50), O("Mort", 60)], "u")),
            new("Saxo", true, ListingParse.Summarise([O("Mort", 200)], "u")),
        };
        var used = new Item { Kind = Kinds.Bog, Title = "Mort" };
        var fresh = new Item { Kind = Kinds.Bog, Title = "Mort", Usage = Usages.New };
        HuntResult.From(1, answers, Now).ApplyTo(used, Now);
        HuntResult.From(2, answers, Now).ApplyTo(fresh, Now);
        used.PriceHint.Should().BeLessThan(60);
        fresh.PriceHint.Should().Be(180);   // 10 % under the new shop
    }

    [Test]
    public void A_new_and_a_used_copy_of_the_same_book_are_two_rows()
    {
        var used = new Item { Kind = Kinds.Bog, Title = "Mort", Condition = "God" };
        used.SameAs(new Found(Kinds.Bog, "Mort", "", "", "", null, "", "God", 1, 1, "", Usage: Usages.New)).Should().BeFalse();
        used.SameAs(new Found(Kinds.Bog, "Mort", "", "", "", null, "", "God", 1, 1, "")).Should().BeTrue();
    }

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
