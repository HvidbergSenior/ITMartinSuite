using System.Text.Json;
using FluentAssertions;
using ITMartinLager.Application;
using ITMartinLager.Domain;
using ITMartinLager.Infrastructure.External;
using ITMartinLager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Moq;

namespace ITMartinLager.Tests;

public class ShopTests
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

    private async Task<Item> Add(string title, decimal? hint, decimal? price = null, int qty = 1, double conf = 0.9, string status = Statuses.Lager)
    {
        var item = new Item { Kind = Kinds.Bog, Title = title, PriceHint = hint, Price = price, Quantity = qty, Confidence = conf, Status = status };
        item.Touch();
        await Items.AddAsync([item], default);
        return item;
    }

    private (ShopUseCases Shop, Mock<IPaymentGateway> Pay) Make()
    {
        var pay = new Mock<IPaymentGateway>();
        pay.SetupGet(p => p.Enabled).Returns(true);
        pay.Setup(p => p.StartAsync(It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("cs_test_1", "https://checkout.stripe.com/c/pay/cs_test_1"));
        return (new ShopUseCases(Items, new PriceRepository(Factory), new OrderRepository(Factory), pay.Object, new CampaignRepository(Factory), new GoldRepository(Factory), _opt, TimeProvider.System), pay);
    }

    [Test]
    public void Buy_now_price_is_the_persons_price_else_a_sure_suggested_price_under_the_limit()
    {
        Shop.BuyNowKr(new Item { Price = 300, PriceHint = 90 }, 200).Should().Be(300);
        Shop.BuyNowKr(new Item { PriceHint = 89.6m, Confidence = 0.9 }, 200).Should().Be(90);
        Shop.BuyNowKr(new Item { PriceHint = 250, Confidence = 0.9 }, 200).Should().BeNull();   // dear: a person decides
        Shop.BuyNowKr(new Item { PriceHint = 50, Confidence = 0.4 }, 200).Should().BeNull();    // the AI was unsure what it is
        Shop.BuyNowKr(new Item(), 200).Should().BeNull();
    }

    [Test]
    public void Only_unsold_items_off_auction_are_listed()
    {
        Shop.Listed(new Item { Title = "Mort", Status = Statuses.Lager }).Should().BeTrue();
        Shop.Listed(new Item { Title = "Mort", Status = Statuses.TilSalg }).Should().BeTrue();
        Shop.Listed(new Item { Title = "Mort", Status = Statuses.PaaAuktion }).Should().BeFalse();
        Shop.Listed(new Item { Title = "Mort", Status = Statuses.Solgt }).Should().BeFalse();
    }

    [Test]
    public void Proof_is_the_cheapest_exact_listing_per_site_used_first()
    {
        var proof = Shop.Proof([
            new PriceFind { Source = "DBA", Kr = 60, Url = "https://dba/1" }, new PriceFind { Source = "DBA", Kr = 40, Url = "https://dba/2" },
            new PriceFind { Source = "Amazon.de", Kr = 30, Url = "https://a/1", IsNew = true },
            new PriceFind { Source = "Nemos", Kr = 10, Url = "https://n/1", Similar = true },
            new PriceFind { Source = "eBay", Kr = 55, Url = "https://e/1" },
        ]);
        proof.Select(p => (p.Source, p.Kr)).Should().Equal(("DBA", 40), ("eBay", 55), ("Amazon.de", 30));
    }

    [Test]
    public async Task Buying_reserves_the_copy_and_paying_takes_it_out_of_stock()
    {
        var item = await Add("Mort", 80);
        var (shop, pay) = Make();

        var url = await shop.BuyAsync(item.Id, "https://bogshoppen.dk/");
        url.Should().StartWith("https://checkout.stripe.com");
        pay.Verify(p => p.StartAsync(It.Is<Order>(o => o.Kr == 80), It.IsAny<string>(),
            It.Is<string>(s => s.StartsWith("https://bogshoppen.dk/varer/koebt?ordre=")), "https://bogshoppen.dk/varer/" + item.Id, 49, "Aarhus C efter aftale", It.IsAny<CancellationToken>()));

        // Reserved: nobody else can start paying for the only copy.
        (await shop.SearchAsync("mort", null)).Single().Available.Should().Be(0);
        await shop.Invoking(s => s.BuyAsync(item.Id, "https://bogshoppen.dk")).Should().ThrowAsync<LagerException>().WithMessage("*betale*");

        // Paid.
        pay.Setup(p => p.StateAsync("cs_test_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentState(true, false, 129, "a@b.dk", "Anna", "+4512345678", "Vej 1, 8000 Aarhus C", "Send med posten"));
        (await shop.SyncAsync()).Should().Be(1);
        var sold = (await Items.GetAsync(item.Id, default))!;
        sold.Status.Should().Be(Statuses.Solgt);
        sold.SoldPrice.Should().Be(80);
        sold.SoldTo.Should().Be("Anna");
        (await shop.SearchAsync("mort", null)).Should().BeEmpty();
        var order = (await shop.RecentOrdersAsync()).Single();
        (order.Status, order.PaidKr, order.Delivery, order.Address).Should().Be((OrderStatuses.Paid, 129, "Send med posten", "Vej 1, 8000 Aarhus C"));
        (await shop.SyncAsync()).Should().Be(0);   // booked once
    }

    [Test]
    public async Task Only_items_chosen_for_the_front_page_are_shown_there_with_their_links()
    {
        var mort = await Add("Mort", 80);
        await Add("Snuff", 90);
        var sold = await Add("Eric", 70, status: Statuses.Solgt);
        await new PriceRepository(Factory).ReplaceAsync(mort.Id, [], [new PriceFind { ItemId = mort.Id, Source = "DBA", Kr = 95, Url = "https://dba.dk/1" }], default);
        var items = new ItemUseCases(Items, new PriceRepository(Factory), Mock.Of<IPhotoStore>());
        await items.FeatureAsync(mort.Id, true);
        await items.FeatureAsync(sold.Id, true);
        var (shop, _) = Make();

        var front = await shop.FeaturedAsync();
        front.Should().ContainSingle();
        front[0].Item.Title.Should().Be("Mort");
        front[0].Proof.Single().Url.Should().Be("https://dba.dk/1");

        await items.FeatureAsync(mort.Id, false);
        (await shop.FeaturedAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task One_of_several_copies_sold_leaves_the_rest_for_sale()
    {
        var item = await Add("Anders And 12", 30, qty: 3);
        var (shop, pay) = Make();
        await shop.BuyAsync(item.Id, "https://bogshoppen.dk");
        (await shop.SearchAsync("anders", null)).Single().Available.Should().Be(2);
        pay.Setup(p => p.StateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PaymentState(true, false, 30));
        await shop.SyncAsync();
        var left = (await Items.GetAsync(item.Id, default))!;
        (left.Quantity, left.Status).Should().Be((2, Statuses.Lager));
    }

    [Test]
    public async Task An_expired_checkout_frees_the_copy()
    {
        var item = await Add("Mort", 80);
        var (shop, pay) = Make();
        await shop.BuyAsync(item.Id, "https://bogshoppen.dk");
        pay.Setup(p => p.StateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PaymentState(false, true));
        (await shop.SyncAsync()).Should().Be(1);
        (await shop.SearchAsync("mort", null)).Single().Available.Should().Be(1);
        (await Items.GetAsync(item.Id, default))!.Status.Should().Be(Statuses.Lager);
    }

    [Test]
    public async Task No_buy_now_without_a_price_and_without_Stripe()
    {
        var dear = await Add("Hobbitten 1. udgave", 900);
        var (shop, _) = Make();
        await shop.Invoking(s => s.BuyAsync(dear.Id, "https://bogshoppen.dk")).Should().ThrowAsync<LagerException>().WithMessage("*fast pris*");

        var off = new ShopUseCases(Items, new PriceRepository(Factory), new OrderRepository(Factory), Mock.Of<IPaymentGateway>(), new CampaignRepository(Factory), new GoldRepository(Factory), _opt, TimeProvider.System);
        off.CanPay.Should().BeFalse();
        var cheap = await Add("Mort", 50);
        await off.Invoking(s => s.BuyAsync(cheap.Id, "https://bogshoppen.dk")).Should().ThrowAsync<LagerException>().WithMessage("*Betaling*");
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

public class BoxMergeTests
{
    private SqliteConnection _conn = null!;
    private TestDbFactory Factory => new(_conn);

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

    private static Found F(string title, string condition) => new(Kinds.Bog, title, "Terry Pratchett", "", "", 1987, "", condition, 1, 0.9, "");

    [Test]
    public async Task A_duplicate_in_the_box_just_adds_one_and_other_boxes_show_as_we_have_more()
    {
        var boxes = new BoxRepository(Factory);
        var items = new ItemRepository(Factory);
        var k1 = await boxes.AddNextAsync("", default);
        var k2 = await boxes.AddNextAsync("", default);
        var reg = new RegistrationUseCases(items);

        await reg.SaveAsync(k1.Id, null, [F("Mort", "God")]);
        (await reg.SaveAsync(k1.Id, null, [F("Mort", "Meget god")])).Should().Be((0, 1));   // read a bit differently = same thing
        await reg.SaveAsync(k2.Id, null, [F("Mort", "God")]);                              // another box = its own row

        var inK1 = (await items.InBoxAsync(k1.Id, default)).Single();
        inK1.Quantity.Should().Be(2);
        var others = await new ItemUseCases(items, new PriceRepository(Factory), Mock.Of<IPhotoStore>()).OthersLikeAsync(inK1);
        others.Single().Box!.Code.Should().Be("K0002");
    }
}
