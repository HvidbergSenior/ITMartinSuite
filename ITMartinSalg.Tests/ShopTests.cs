using System.Text.Json;
using FluentAssertions;
using ITMartinSalg.Application;
using ITMartinSalg.Domain;
using ITMartinSalg.Infrastructure.External;
using ITMartinSalg.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ITMartinSalg.Tests;

// The repositories open a context per call - in tests they all share one in-memory SQLite connection.
public sealed class TestDbFactory(SqliteConnection conn) : IDbContextFactory<SalgDb>
{
    public SalgDb CreateDbContext() => new(new DbContextOptionsBuilder<SalgDb>().UseSqlite(conn).Options);
}

/// <summary>Lager in memory: things for sale, and the sales it booked (each Ref once, like the real one).</summary>
public sealed class FakeStock : IStockGateway
{
    public readonly List<StockItem> Items = [];
    public readonly List<StockSale> Booked = [];
    public readonly HashSet<int> Featured = [];
    public bool Down { get; set; }
    private int _next = 1;

    public string PublicBase => "https://lager.test";

    public StockItem Add(string kind, string title, int? price = null, int? hint = null, int qty = 1, double conf = 0.9, string usage = Usages.Used)
    {
        var i = new StockItem(_next++, kind, title, title, "", "God", null, qty, usage, price, hint, conf, false, "");
        Items.Add(i);
        return i;
    }

    private void Check() { if (Down) throw new SalgException("Lageret svarer ikke lige nu. Prøv igen om lidt."); }

    public Task<StockPage> ListedAsync(IReadOnlyCollection<string>? kinds, string? usage, string text, string sort, int skip, int take, CancellationToken ct)
    {
        Check();
        var q = Items.Where(i => (kinds is null || kinds.Contains(i.Kind)) && (usage is null || i.Usage == usage)
            && text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).All(w => i.Title.ToLowerInvariant().Contains(w)));
        q = sort switch
        {
            ShopSorts.Cheapest => q.OrderBy(i => i.PriceKr ?? i.HintKr),
            ShopSorts.Dearest => q.OrderByDescending(i => i.PriceKr ?? i.HintKr),
            ShopSorts.Title => q.OrderBy(i => i.Title),
            _ => q.OrderByDescending(i => i.Id),
        };
        var all = q.ToList();
        return Task.FromResult(new StockPage(all.Skip(skip).Take(take).ToList(), all.Count));
    }

    public Task<Dictionary<string, int>> PerKindAsync(CancellationToken ct) =>
        Task.FromResult(Items.GroupBy(i => i.Kind).ToDictionary(g => g.Key, g => g.Count()));

    public Task<StockItemPage?> ItemAsync(int id, CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(i => i.Id == id) is { } i ? new StockItemPage(i, [new ShopProof("DBA", 95, "95 kr", "https://dba.dk/1", false)]) : null);

    public Task<List<StockItem>> ByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        Check();
        return Task.FromResult(Items.Where(i => ids.Contains(i.Id)).ToList());
    }

    public Task<List<StockItemPage>> FeaturedAsync(int max, CancellationToken ct) =>
        Task.FromResult(Items.Where(i => Featured.Contains(i.Id)).Select(i => new StockItemPage(i, [])).ToList());

    public Task<byte[]?> PhotoAsync(int id, CancellationToken ct) => Task.FromResult<byte[]?>(null);

    // Like Lager: one copy per line leaves the stock; the last one leaves the shop.
    public Task BookAsync(StockSale sale, CancellationToken ct)
    {
        Check();
        if (Booked.Any(b => b.Ref == sale.Ref)) return Task.CompletedTask;
        Booked.Add(sale);
        foreach (var l in sale.Lines)
            if (Items.FindIndex(i => i.Id == l.ItemId) is var n and >= 0)
            {
                if (Items[n].Quantity <= 1) Items.RemoveAt(n);
                else Items[n] = Items[n] with { Quantity = Items[n].Quantity - 1 };
            }
        return Task.CompletedTask;
    }
}

public class ShopTests
{
    private SqliteConnection _conn = null!;
    private TestDbFactory Factory => new(_conn);
    private FakeStock _stock = null!;
    private readonly Mock<ICustomerRecord> _buyers = new();
    private readonly SalgOptions _opt = new() { ShopPickup = "Aarhus C efter aftale", ShopShippingKr = 49 };

    [SetUp]
    public void Open()
    {
        _buyers.Reset();
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        using var db = Factory.CreateDbContext();
        SalgDbInitializer.Upgrade(db);
        _stock = new FakeStock();
    }

    [TearDown]
    public void Close() => _conn.Dispose();

    /// <summary>A clock on a given day.</summary>
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private (ShopUseCases Shop, Mock<IPaymentGateway> Pay) Make(DateTimeOffset? day = null)
    {
        var pay = new Mock<IPaymentGateway>();
        pay.SetupGet(p => p.Enabled).Returns(true);
        pay.Setup(p => p.StartAsync(It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("cs_test_1", "https://checkout.stripe.com/c/pay/cs_test_1"));
        return (new ShopUseCases(_stock, new OrderRepository(Factory), pay.Object, new CampaignRepository(Factory), _buyers.Object, _opt,
            day is { } d ? new Clock(d) : TimeProvider.System), pay);
    }

    private static StockItem Item(int? price = null, int? hint = null, double conf = 0.9) =>
        new(1, Kinds.Bog, "x", "x", "", "", null, 1, Usages.Used, price, hint, conf, false, "");

    [Test]
    public void Buy_now_price_is_the_persons_price_else_a_sure_suggested_price_under_the_limit()
    {
        Shop.BuyNowKr(Item(price: 300, hint: 90), 200).Should().Be(300);
        Shop.BuyNowKr(Item(hint: 90), 200).Should().Be(90);
        Shop.BuyNowKr(Item(hint: 250), 200).Should().BeNull();               // dear: a person decides
        Shop.BuyNowKr(Item(hint: 50, conf: 0.4), 200).Should().BeNull();     // the AI was unsure what it is
        Shop.BuyNowKr(Item(), 200).Should().BeNull();
    }

    [Test]
    public async Task A_test_box_item_is_shown_but_never_sold()
    {
        var item = _stock.Add(Kinds.Bog, "Eksempel", price: 100);
        _stock.Items[_stock.Items.IndexOf(item)] = item = item with { ForShow = true };
        Shop.BuyNowKr(item, 200).Should().BeNull();
        var (shop, _) = Make();
        var shown = (await shop.SearchAsync("eksempel", null)).Single();
        (shown.ForShow, shown.BuyNowKr).Should().Be((true, (int?)null));
        await shop.Invoking(s => s.BuyAsync(item.Id, "https://bogshoppen.dk")).Should().ThrowAsync<SalgException>();
    }

    [Test]
    public async Task Buying_reserves_the_copy_and_paying_tells_Lager_once()
    {
        var item = _stock.Add(Kinds.Bog, "Mort", hint: 80);
        var (shop, pay) = Make();

        var url = await shop.BuyAsync(item.Id, "https://bogshoppen.dk/");
        url.Should().StartWith("https://checkout.stripe.com");
        pay.Verify(p => p.StartAsync(It.Is<Order>(o => o.Kr == 80), It.IsAny<string>(),
            It.Is<string>(s => s.StartsWith("https://bogshoppen.dk/varer/koebt?ordre=")), "https://bogshoppen.dk/varer/" + item.Id, 49, "Aarhus C efter aftale", It.IsAny<CancellationToken>()));

        // Reserved: nobody else can start paying for the only copy.
        (await shop.SearchAsync("mort", null)).Single().Available.Should().Be(0);
        await shop.Invoking(s => s.BuyAsync(item.Id, "https://bogshoppen.dk")).Should().ThrowAsync<SalgException>().WithMessage("*betale*");

        // Paid.
        pay.Setup(p => p.StateAsync("cs_test_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentState(true, false, 129, "a@b.dk", "Anna", "+4512345678", "Vej 1, 8000 Aarhus C", "Send med posten"));
        (await shop.SyncAsync()).Should().Be(1);
        var sale = _stock.Booked.Single();
        sale.Buyer.Should().Be("Anna");
        sale.Lines.Single().Should().Match<StockSaleLine>(l => l.ItemId == item.Id && l.Kr == 80 && l.How.StartsWith("i butikken for 80 kr (ordre "));
        (await shop.SearchAsync("mort", null)).Should().BeEmpty();
        var order = (await shop.RecentOrdersAsync()).Single();
        (order.Status, order.PaidKr, order.Delivery, order.Address, order.StockBooked).Should().Be((OrderStatuses.Paid, 129, "Send med posten", "Vej 1, 8000 Aarhus C", true));
        (await shop.SyncAsync()).Should().Be(0);   // booked once
        _stock.Booked.Should().ContainSingle();
        _buyers.Verify(b => b.BoughtAsync(It.Is<Order>(o => o.Email == "a@b.dk"), It.IsAny<CancellationToken>()), Times.Once);   // Anna is a customer in Kunder, once
    }

    [Test]
    public async Task A_payment_while_Lager_is_down_is_kept_and_booked_when_it_is_back()
    {
        var item = _stock.Add(Kinds.Bog, "Mort", hint: 80);
        var (shop, pay) = Make();
        await shop.BuyAsync(item.Id, "https://bogshoppen.dk");
        pay.Setup(p => p.StateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PaymentState(true, false, 80, "a@b.dk"));

        _stock.Down = true;
        (await shop.SyncAsync()).Should().Be(1);   // the payment is saved
        var order = (await shop.RecentOrdersAsync()).Single();
        (order.Status, order.NeedsBooking).Should().Be((OrderStatuses.Paid, true));

        _stock.Down = false;
        (await shop.SyncAsync()).Should().Be(1);
        _stock.Booked.Single().Buyer.Should().Be("a@b.dk");
        (await shop.RecentOrdersAsync()).Single().NeedsBooking.Should().BeFalse();
        (await shop.SyncAsync()).Should().Be(0);
    }

    [Test]
    public async Task One_of_several_copies_sold_leaves_the_rest_for_sale()
    {
        var item = _stock.Add(Kinds.Bog, "Anders And 12", hint: 30, qty: 3);
        var (shop, pay) = Make();
        await shop.BuyAsync(item.Id, "https://bogshoppen.dk");
        (await shop.SearchAsync("anders", null)).Single().Available.Should().Be(2);
        pay.Setup(p => p.StateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PaymentState(true, false, 30));
        await shop.SyncAsync();
        (await shop.SearchAsync("anders", null)).Single().Available.Should().Be(2);
    }

    [Test]
    public async Task An_expired_checkout_frees_the_copy_and_Lager_hears_nothing()
    {
        var item = _stock.Add(Kinds.Bog, "Mort", hint: 80);
        var (shop, pay) = Make();
        await shop.BuyAsync(item.Id, "https://bogshoppen.dk");
        pay.Setup(p => p.StateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PaymentState(false, true));
        (await shop.SyncAsync()).Should().Be(1);
        (await shop.SearchAsync("mort", null)).Single().Available.Should().Be(1);
        _stock.Booked.Should().BeEmpty();
    }

    [Test]
    public async Task No_buy_now_without_a_price_and_without_Stripe()
    {
        var dear = _stock.Add(Kinds.Bog, "Hobbitten 1. udgave", hint: 900);
        var (shop, _) = Make();
        await shop.Invoking(s => s.BuyAsync(dear.Id, "https://bogshoppen.dk")).Should().ThrowAsync<SalgException>().WithMessage("*fast pris*");

        var off = new ShopUseCases(_stock, new OrderRepository(Factory), Mock.Of<IPaymentGateway>(), new CampaignRepository(Factory), _buyers.Object, _opt, TimeProvider.System);
        off.CanPay.Should().BeFalse();
        var cheap = _stock.Add(Kinds.Bog, "Mort", hint: 50);
        await off.Invoking(s => s.BuyAsync(cheap.Id, "https://bogshoppen.dk")).Should().ThrowAsync<SalgException>().WithMessage("*Betaling*");
    }

    [Test]
    public async Task The_front_page_shows_what_Lager_chose_with_its_proof()
    {
        var mort = _stock.Add(Kinds.Bog, "Mort", hint: 80);
        _stock.Add(Kinds.Bog, "Snuff", hint: 90);
        _stock.Featured.Add(mort.Id);
        var (shop, _) = Make();
        (await shop.FeaturedAsync()).Single().Item.Title.Should().Be("Mort");
        (await shop.ItemAsync(mort.Id))!.Value.Proof.Single().Url.Should().Be("https://dba.dk/1");
        (await shop.ItemAsync(999)).Should().BeNull();
    }

    [Test]
    public async Task The_examples_are_in_the_database_switched_off()
    {
        var all = await new CampaignRepository(Factory).AllAsync(default);
        all.Should().HaveCount(Campaign.Examples().Count).And.OnlyContain(c => !c.Active);
        using var db = Factory.CreateDbContext();
        SalgDbInitializer.Upgrade(db);   // a second start adds no more
        (await new CampaignRepository(Factory).AllAsync(default)).Should().HaveCount(all.Count);
    }

    private async Task TurnOnAsync(string name)
    {
        var repo = new CampaignRepository(Factory);
        var c = (await repo.AllAsync(default)).Single(x => x.Name == name);
        c.Active = true;
        await repo.SaveAsync(c, default);
    }

    [Test]
    public async Task A_basket_on_book_monday_is_quoted_bought_and_paid_at_the_day_price()
    {
        await TurnOnAsync("Bogmandag");
        var mort = _stock.Add(Kinds.Bog, "Mort", price: 100, qty: 2);
        var film = _stock.Add(Kinds.Dvd, "Heat", price: 60);
        var (shop, pay) = Make(new DateTimeOffset(2026, 10, 19, 10, 0, 0, TimeSpan.Zero));   // a Monday

        var quote = await shop.QuoteAsync([mort.Id, mort.Id, film.Id, 999]);
        quote.Lines.Select(l => l.Kr).Should().Equal(50, 50, 60);
        (quote.Total, quote.Saved).Should().Be((160, 100));
        quote.Gone.Should().Equal(999);
        quote.Today.Single().Text.Should().Be("50 % på brugte bøger");
        (await shop.MarketAsync(ProductTypes.Boeger, null, null, null, null, 0)).Items.Single().DayKr.Should().Be(50);

        // Something gone = no payment; the buyer sees the basket again first.
        await shop.Invoking(s => s.BuyAsync([mort.Id, 999], "https://bogshoppen.dk")).Should().ThrowAsync<SalgException>().WithMessage("*kurven*");

        await shop.BuyAsync([mort.Id, mort.Id, film.Id], "https://bogshoppen.dk");
        pay.Verify(p => p.StartAsync(It.Is<Order>(o => o.Kr == 160 && o.Lines.Count == 3), It.IsAny<string>(), It.IsAny<string>(),
            "https://bogshoppen.dk/kurv", 49, It.IsAny<string>(), It.IsAny<CancellationToken>()));
        (await shop.QuoteAsync([mort.Id])).Gone.Should().Equal(mort.Id);   // both copies are reserved now

        pay.Setup(p => p.StateAsync("cs_test_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentState(true, false, 209, "a@b.dk", "Anna", Delivery: "Send med posten"));
        (await shop.SyncAsync()).Should().Be(1);
        _stock.Booked.Single().Lines.Select(l => (l.ItemId, l.Kr)).Should().Equal((mort.Id, 50), (mort.Id, 50), (film.Id, 60));
        _stock.Items.Should().BeEmpty();

        var order = (await shop.RecentOrdersAsync()).Single();
        await shop.PackAsync(order.Id);
        await shop.SendAsync(order.Id, "0037");
        (await shop.RecentOrdersAsync()).Single().Should().Match<Order>(o => o.Status == OrderStatuses.Sent && o.Tracking == "0037");
        await shop.Invoking(s => s.PackAsync(order.Id)).Should().ThrowAsync<SalgException>();
    }

    [Test]
    public async Task The_marketplace_counts_per_product_type_and_filters_by_type()
    {
        _stock.Add(Kinds.Bog, "Mort", price: 50);
        _stock.Add(Kinds.Bog, "Eric", price: 40);
        _stock.Add(Kinds.Vinyl, "Abbey Road", price: 200);
        _stock.Add(Kinds.Cd, "Abbey Road", price: 60);
        var (shop, _) = Make();

        var all = await shop.MarketAsync(null, null, null, null, ShopSorts.Dearest, 0);
        all.Types.Should().BeEquivalentTo([new ShopType(ProductTypes.Boeger, 2), new ShopType(ProductTypes.Musik, 2)]);
        all.Items.First().Title.Should().Be("Abbey Road");
        all.Total.Should().Be(4);

        var music = await shop.MarketAsync(ProductTypes.Musik, null, null, "abbey", ShopSorts.Cheapest, 0);
        music.Items.Select(i => (i.Kind, i.BuyNowKr)).Should().Equal((Kinds.Cd, 60), (Kinds.Vinyl, 200));
        music.Items.Should().OnlyContain(i => i.Type == ProductTypes.Musik);
    }

    [Test]
    public void Stripe_session_is_read_with_buyer_and_shipping()
    {
        using var doc = JsonDocument.Parse("""
            {"id":"cs_1","status":"complete","payment_status":"paid","amount_total":12900,
             "customer_details":{"email":"a@b.dk","name":"Anna B","phone":"+4512345678","address":{"line1":"x","city":"y"}},
             "collected_information":{"shipping_details":{"name":"Anna","address":{"line1":"Vej 1","line2":null,"postal_code":"8000","city":"Aarhus C"}}},
             "shipping_cost":{"amount_total":4900}}
            """);
        StripeCheckout.Parse(doc.RootElement).Should().Be(new PaymentState(true, false, 129, "a@b.dk", "Anna", "+4512345678", "Vej 1, 8000 Aarhus C", "Send med posten"));

        using var open = JsonDocument.Parse("""{"id":"cs_2","status":"expired","payment_status":"unpaid","customer_details":null}""");
        StripeCheckout.Parse(open.RootElement).Should().Be(new PaymentState(false, true, 0, "", "", "", "", "Afhentning"));
    }

    [Test]
    public void Stripe_form_has_price_in_oere_both_deliveries_and_expiry()
    {
        var order = new Order { Id = 7, Kr = 85, Line = "Mort · Terry Pratchett", CreatedAt = new DateTime(2026, 10, 15, 10, 0, 0, DateTimeKind.Utc) };
        var f = StripeCheckout.Form(order, "Bog", "https://s", "https://c", 49, "Aarhus").ToDictionary(x => x.Key, x => x.Value);
        f["line_items[0][price_data][unit_amount]"].Should().Be("8500");
        f["line_items[0][price_data][currency]"].Should().Be("dkk");
        f["shipping_options[0][shipping_rate_data][fixed_amount][amount]"].Should().Be("0");
        f["shipping_options[1][shipping_rate_data][fixed_amount][amount]"].Should().Be("4900");
        f["expires_at"].Should().Be(new DateTimeOffset(2026, 10, 15, 10, 30, 0, TimeSpan.Zero).ToUnixTimeSeconds().ToString());
    }
}

public class SaleStepTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private static StockItem S(int id, string title) => new(id, Kinds.Bog, title, title, "", "", null, 2, Usages.Used, 80, null, 1, false, "");

    private static Order Paid(string delivery = "Send med posten")
    {
        var o = Order.For([(S(1, "Mort"), new PricedLine(1, 80, 80, ""))], Now);
        o.Pay(new PaymentState(true, false, 129, "a@b.dk", "Anna", Delivery: delivery), Now);
        return o;
    }

    [Test]
    public void Paid_then_packed_then_sent_with_the_parcel_number()
    {
        var o = Paid();
        o.NeedsBooking.Should().BeTrue();
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

        var pending = Order.For([(S(2, "x"), new PricedLine(2, 10, 10, ""))], Now);
        pending.Invoking(x => x.Send("", Now)).Should().Throw<InvalidOperationException>();
        pending.NeedsBooking.Should().BeFalse();
        pending.Expire();
        pending.Status.Should().Be(OrderStatuses.Expired);
    }

    [Test]
    public void A_basket_order_names_the_first_thing_and_how_many_more_and_the_sale_says_the_offer()
    {
        var o = Order.For([(S(1, "Mort"), new PricedLine(1, 50, 40, "x")), (S(2, "Eric"), new PricedLine(2, 50, 40, "x")), (S(3, "Snuff"), new PricedLine(3, 50, 40, "x"))], Now);
        o.Should().Match<Order>(x => x.Line == "Mort + 2 andre" && x.Kr == 120 && x.Lines.Count == 3 && x.ItemId == 1);
        o.Id = 7;
        o.Sale().Lines[0].How.Should().Be("i butikken for 40 kr (ordre 7, x)");
        Order.For([(S(1, "Mort"), new PricedLine(1, 50, 50, "")), (S(2, "Eric"), new PricedLine(2, 50, 50, ""))], Now).Line.Should().Be("Mort + 1 anden");
    }
}

// The move from Lager (2026-10-21): the orders and offers Lager had are copied once from a copy of lager.db.
public class ImportFromLagerTests
{
    [Test]
    public void Lager_orders_and_offers_come_over_once_and_paid_ones_count_as_booked()
    {
        var file = Path.Combine(Path.GetTempPath(), $"lager-import-{Guid.NewGuid():N}.db");
        try
        {
            using (var old = new SqliteConnection($"Data Source={file};Pooling=False"))
            {
                old.Open();
                using var cmd = old.CreateCommand();
                // The tables as Lager had them on 2026-10-21 (incl. the old Handled column).
                cmd.CommandText = """
                    CREATE TABLE "Orders" ("Id" INTEGER PRIMARY KEY, "Token" TEXT, "ItemId" INTEGER, "Line" TEXT, "Kr" INTEGER, "Status" TEXT,
                        "CheckoutId" TEXT, "CreatedAt" TEXT, "ExpiresAt" TEXT, "PaidAt" TEXT, "PaidKr" INTEGER, "Email" TEXT, "Name" TEXT,
                        "Phone" TEXT, "Address" TEXT, "Delivery" TEXT, "Handled" INTEGER, "Channel" TEXT, "PackedAt" TEXT, "DoneAt" TEXT, "Tracking" TEXT);
                    CREATE TABLE "OrderLines" ("Id" INTEGER PRIMARY KEY, "OrderId" INTEGER, "ItemId" INTEGER, "Line" TEXT, "Kind" TEXT, "Usage" TEXT,
                        "ListKr" INTEGER, "Kr" INTEGER, "Offer" TEXT);
                    CREATE TABLE "Campaigns" ("Id" INTEGER PRIMARY KEY, "Name" TEXT, "Kind" TEXT, "ProductType" TEXT, "Usage" TEXT, "Weekday" INTEGER,
                        "DayOfMonth" INTEGER, "Percent" INTEGER, "Count" INTEGER, "BundleKr" INTEGER, "Active" INTEGER);
                    INSERT INTO "Orders" VALUES (3, 't-paid', 7, 'Mort', 80, 'Sendt', 'cs_1', '2026-10-15 10:00:00', '2026-10-15 10:31:00', '2026-10-15 10:05:00',
                        129, 'a@b.dk', 'Anna', '', 'Vej 1', 'Send med posten', 0, 'Køb nu', '2026-10-16 09:00:00', '2026-10-16 09:00:00', '0037');
                    INSERT INTO "Orders" VALUES (4, 't-wait', 8, 'Eric', 40, 'Afventer betaling', 'cs_2', '2026-10-21 10:00:00', '2026-10-21 10:31:00', NULL,
                        0, '', '', '', '', '', 0, 'Køb nu', NULL, NULL, '');
                    INSERT INTO "OrderLines" VALUES (5, 3, 7, 'Mort', 'Bog', 'Brugt', 80, 80, '');
                    INSERT INTO "OrderLines" VALUES (6, 4, 8, 'Eric', 'Bog', 'Brugt', 40, 40, '');
                    INSERT INTO "Campaigns" VALUES (1, 'Bogmandag', 'Procent', 'Bøger', 'Brugt', 1, NULL, 50, 0, 0, 1);
                    """;
                cmd.ExecuteNonQuery();
            }

            using var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            using (var db = new TestDbFactory(conn).CreateDbContext()) SalgDbInitializer.Upgrade(db, file);
            using (var db = new TestDbFactory(conn).CreateDbContext()) SalgDbInitializer.Upgrade(db, file);   // a second start copies nothing again

            var orders = new OrderRepository(new TestDbFactory(conn));
            var paid = orders.ByTokenAsync("t-paid", default).Result!;
            (paid.Id, paid.Status, paid.Tracking, paid.StockBooked, paid.Lines.Single().ItemId).Should().Be((3, OrderStatuses.Sent, "0037", true, 7));
            var waiting = orders.ByTokenAsync("t-wait", default).Result!;
            (waiting.Status, waiting.StockBooked).Should().Be((OrderStatuses.Pending, false));
            orders.OpenAsync(default).Result.Select(o => o.Token).Should().Equal("t-wait");
            var offers = new CampaignRepository(new TestDbFactory(conn)).AllAsync(default).Result;
            offers.Single().Should().Match<Campaign>(c => c.Name == "Bogmandag" && c.Active);   // no examples on top of Lager's own
        }
        finally { SqliteConnection.ClearAllPools(); File.Delete(file); }
    }
}
