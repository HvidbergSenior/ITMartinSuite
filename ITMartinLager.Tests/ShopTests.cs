using FluentAssertions;
using ITMartinLager.Application;
using ITMartinLager.Domain;
using ITMartinLager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Moq;

namespace ITMartinLager.Tests;

// What Salg gets from Lager (/api/salg/*, 2026-10-21 split): the things for sale, their proof, and paid sales booked once.
public class StockForSaleTests
{
    private SqliteConnection _conn = null!;
    private TestDbFactory Factory => new(_conn);
    private ItemRepository Items => new(Factory);

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

    private async Task<Item> Add(string title, decimal? hint, decimal? price = null, int qty = 1, string status = Statuses.Lager, string kind = Kinds.Bog)
    {
        var item = new Item { Kind = kind, Title = title, PriceHint = hint, Price = price, Quantity = qty, Confidence = 0.9, Status = status };
        item.Touch();
        await Items.AddAsync([item], default);
        return item;
    }

    private StockForSaleUseCases Make() =>
        new(Items, new PriceRepository(Factory), new GoldRepository(Factory), new SaleBookingRepository(Factory), TimeProvider.System);

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
    public async Task Salg_gets_the_prices_as_whole_kroner_and_only_what_is_for_sale()
    {
        var mort = await Add("Mort", 89.6m);
        await Add("Eric", 70, status: Statuses.Solgt);
        await Add("Heat", 60, price: 75, kind: Kinds.Dvd);
        var stock = Make();

        var page = await stock.ListedAsync(null, null, null, ShopSorts.Title, 0, 10);
        page.Total.Should().Be(2);
        page.Items.Select(i => (i.Title, i.PriceKr, i.HintKr)).Should().Equal(("Heat", 75, 60), ("Mort", null, 90));
        (await stock.ListedAsync([Kinds.Dvd], null, null, null, 0, 10)).Items.Single().Title.Should().Be("Heat");
        (await stock.PerKindAsync()).Should().BeEquivalentTo(new Dictionary<string, int> { [Kinds.Bog] = 1, [Kinds.Dvd] = 1 });
        (await stock.ByIdsAsync([mort.Id, 999])).Single().Id.Should().Be(mort.Id);
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
        var stock = Make();

        var front = await stock.FeaturedAsync(12);
        front.Should().ContainSingle();
        front[0].Item.Title.Should().Be("Mort");
        front[0].Proof.Single().Url.Should().Be("https://dba.dk/1");
        (await stock.ItemAsync(mort.Id))!.Proof.Single().Kr.Should().Be(95);
        (await stock.ItemAsync(sold.Id)).Should().BeNull();

        await items.FeatureAsync(mort.Id, false);
        (await stock.FeaturedAsync(12)).Should().BeEmpty();
    }

    [Test]
    public async Task A_paid_sale_takes_one_copy_per_line_and_the_same_sale_twice_changes_nothing()
    {
        var mort = await Add("Mort", 80, qty: 2);
        var heat = await Add("Heat", 60, kind: Kinds.Dvd);
        var stock = Make();
        var sale = new StockSale("tok1", "Anna", [new(mort.Id, 50, "i butikken for 50 kr (ordre 1)"), new(heat.Id, 60, "i butikken for 60 kr (ordre 1)"), new(999, 10, "x")]);

        (await stock.BookAsync(sale)).Should().BeTrue();
        (await stock.BookAsync(sale)).Should().BeFalse();

        var m = (await Items.GetAsync(mort.Id, default))!;
        (m.Quantity, m.Status).Should().Be((1, Statuses.Lager));
        m.Note.Should().Contain("i butikken for 50 kr (ordre 1)");
        var h = (await Items.GetAsync(heat.Id, default))!;
        (h.Status, h.SoldPrice, h.SoldTo).Should().Be((Statuses.Solgt, 60m, "Anna"));
        (await stock.ListedAsync(null, null, "heat", null, 0, 10)).Items.Should().BeEmpty();
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
