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
    public void A_box_whose_placering_starts_with_Test_is_a_test_box_and_its_things_are_only_for_show()
    {
        Box.IsTestName("Test").Should().BeTrue();
        Box.IsTestName("  test – eksempler").Should().BeTrue();
        Box.IsTestName("Garagen, reol 3").Should().BeFalse();
        Box.IsTestName("Kontest").Should().BeFalse();
        var item = new Item { Id = 1, Title = "Eksempel", Box = new Box { Code = "K0001", Place = "Test" } };
        Shop.View(item, []).ForShow.Should().BeTrue();
        item.Box = new Box { Code = "K0002", Place = "Garagen" };
        Shop.View(item, []).ForShow.Should().BeFalse();
    }

    [Test]
    public void Only_unsold_items_off_auction_are_listed()
    {
        Shop.Listed(new Item { Confidence = 1, Title = "Mort", PriceHint = 50, Status = Statuses.Lager }).Should().BeTrue();
        Shop.Listed(new Item { Confidence = 1, Title = "Mort", PriceHint = 50, Status = Statuses.TilSalg }).Should().BeTrue();
        Shop.Listed(new Item { Confidence = 1, Title = "Mort", PriceHint = 50, Status = Statuses.PaaAuktion }).Should().BeFalse();
        Shop.Listed(new Item { Confidence = 1, Title = "Mort", PriceHint = 50, Status = Statuses.Solgt }).Should().BeFalse();
        Shop.Listed(new Item { Confidence = 1, Title = "Mort", Status = Statuses.Lager }).Should().BeTrue("no price yet = the Ingen pris endnu category");
    }

    [Test]
    public void Anders_And_and_Basserne_are_3_kr_a_piece_unless_rare()
    {
        var now = DateTime.UtcNow;
        ShopDeals.PerPiece.Name.Should().Be("Anders And og Basserne: 3 kr stk.");
        Shop.DealFor(new Item { Kind = Kinds.AndersAnd, Title = "Anders And", Number = "12", Year = 1985, Confidence = 1, PriceHint = 18, PricesAt = now }, [])
            .Should().Be(ShopDeals.PerPiece.Name, "its own price does not matter");
        Shop.DealFor(new Item { Kind = Kinds.AndersAnd, Title = "Anders And", Confidence = 1 }, []).Should().Be(ShopDeals.PerPiece.Name, "not searched yet either");
        Shop.DealFor(new Item { Kind = Kinds.Tegneserie, Title = "Rødøje", Series = "Basserne", Number = "612", Confidence = 1, PriceHint = 25, PricesAt = now }, [])
            .Should().Be(ShopDeals.PerPiece.Name);
        Shop.DealFor(new Item { Kind = Kinds.Tegneserie, Title = "Asterix og Kleopatra", Series = "Asterix", Confidence = 1, PriceHint = 25, PricesAt = now }, [])
            .Should().Be("", "other comics keep their own price");
        Shop.DealFor(new Item { Kind = Kinds.AndersAnd, Title = "Anders And", Confidence = 1, PriceHint = 150, PricesAt = now }, [])
            .Should().Be("", "rare (bronze and up) keeps its own price and links");
    }

    [Test]
    public void A_novel_is_not_priced_from_a_comic_album_of_the_same_name()
    {
        Pricing.OtherFormat(Kinds.Bog, "Garfield Farvealbum Nr. 9 Blå mandag").Should().BeTrue();
        Pricing.OtherFormat(Kinds.Bog, "Blå mandag - Nicci French").Should().BeFalse();
    }

    [Test]
    public void Deals_skip_gold_and_unsure_things_and_wait_for_the_price_search()
    {
        var now = DateTime.UtcNow;
        Shop.DealFor(new Item { Kind = Kinds.Bog, Title = "Mort", Confidence = 1, PricesAt = now }, [], ShopDeals.All).Should().Be(ShopDeals.Books.Name);
        Shop.DealFor(new Item { Kind = Kinds.Dvd, Title = "Heat", Confidence = 1, PricesAt = now }, [], ShopDeals.All).Should().Be(ShopDeals.Dvds.Name);
        Shop.DealFor(new Item { Kind = Kinds.Dvd, Title = "Heat", Confidence = 1 }, [], ShopDeals.All).Should().Be("", "not searched yet");
        Shop.DealFor(new Item { Kind = Kinds.Bog, Title = "Mort", Confidence = 0.3, PricesAt = now }, [], ShopDeals.All).Should().Be("", "unsure - a person looks");
        Shop.DealFor(new Item { Kind = Kinds.Cd, Title = "Abbey Road", Confidence = 1, PricesAt = now }, [], ShopDeals.All).Should().Be(ShopDeals.Cds.Name);
        Shop.DealFor(new Item { Kind = Kinds.Cd, Title = "Abbey Road", Confidence = 1, PricesAt = now }, []).Should().Be("", "no deals in use");
        Shop.DealFor(new Item { Kind = Kinds.AndersAnd, Title = "Anders And", Confidence = 0.3 }, []).Should().Be(ShopDeals.PerPiece.Name, "every blad is 3 kr");
        Shop.DealFor(new Item { Kind = Kinds.AndersAnd, Title = "Anders And", Confidence = 0.3, PriceHint = 40 }, [], ShopDeals.All).Should().Be(ShopDeals.Blade.Name, "unsure blad = the Blade bin");
        Shop.Listed(new Item { Kind = Kinds.AndersAnd, Title = "Anders And", Confidence = 0.3, Status = Statuses.Lager, Deal = ShopDeals.Blade.Name }).Should().BeTrue();
        Shop.Listed(new Item { Kind = Kinds.Bog, Title = "Mort", Confidence = 0.3, Status = Statuses.Lager }).Should().BeTrue("shown under Ikke tjekket endnu");
        Shop.Unsure(new Item { Confidence = 0.3 }).Should().BeTrue();
        Shop.DealFor(new Item { Kind = Kinds.Bog, Title = "Mort", Confidence = 1, PriceHint = 20, PricesAt = now }, [], ShopDeals.All).Should().Be(ShopDeals.Books.Name, "20 kr alone would undercut 2 for 50");
        Shop.DealFor(new Item { Kind = Kinds.Bog, Title = "Mort", Confidence = 1, PriceHint = 25, PricesAt = now }, [], ShopDeals.All).Should().Be(ShopDeals.Books.Name);
        Shop.DealFor(new Item { Kind = Kinds.AndersAnd, Title = "Anders And", Confidence = 1, PriceHint = 18, PricesAt = now }, [], ShopDeals.All).Should().Be("", "18 kr is more than 4 for 50");
        Shop.DealFor(new Item { Kind = Kinds.Cd, Title = "Abbey Road", Confidence = 1, PriceHint = 7, PricesAt = now }, [], ShopDeals.All).Should().Be(ShopDeals.Cds.Name, "7 kr alone would undercut 4 for 50");
        Shop.DealFor(new Item { Kind = Kinds.Bog, Title = "Mort", Confidence = 1, PriceHint = 40, PricesAt = now }, [], ShopDeals.All).Should().Be("", "40 kr is more than 2 for 50 - own price");
        Shop.DealFor(new Item { Kind = Kinds.AndersAnd, Title = "Anders And", Confidence = 1, PricesAt = now }, [], ShopDeals.All).Should().Be(ShopDeals.Blade.Name);
        Shop.DealFor(new Item { Kind = Kinds.Bog, Title = "Mort", Confidence = 1, PriceHint = 150, PricesAt = now }, [], ShopDeals.All).Should().Be("", "bronze and up are sold one by one");
    }

    [Test]
    public void Reference_is_the_cheapest_used_exact_listing_never_ebay_else_a_similar_one()
    {
        Shop.Reference([
            new PriceFind { Source = "eBay", Kr = 5, Url = "https://e/1" },
            new PriceFind { Source = "DBA", Kr = 60, Url = "https://dba/1" }, new PriceFind { Source = "Nemos", Kr = 40, Url = "https://n/1" },
            new PriceFind { Source = "Faraos", Kr = 20, Url = "https://f/1", IsNew = true },
        ])!.Url.Should().Be("https://n/1");
        Shop.Reference([new PriceFind { Source = "DBA", Kr = 30, Url = "https://dba/2", Similar = true }])!.Url.Should().Be("https://dba/2");
        Shop.Reference([new PriceFind { Source = "eBay", Kr = 30, Url = "https://e/2" }]).Should().BeNull();
        Shop.Reference([new PriceFind { Source = "GulogGratis", Kr = 1, Url = "https://g/1" }, new PriceFind { Source = "Faraos", Kr = 22, Url = "https://f/2" },
            new PriceFind { Source = "DBA", Kr = 35, Url = "https://dba/3" }], 30)!.Url.Should().Be("https://dba/3", "never cheaper than our own price");
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

        var page = await stock.ListedAsync(null, null, null, ShopSorts.Title, 0, 10, null);
        page.Total.Should().Be(2);
        page.Items.Select(i => (i.Title, i.PriceKr, i.HintKr)).Should().Equal(("Heat", 75, 60), ("Mort", null, 90));
        (await stock.ListedAsync([Kinds.Dvd], null, null, null, 0, 10, null)).Items.Single().Title.Should().Be("Heat");
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
        (await stock.ListedAsync(null, null, "heat", null, 0, 10, null)).Items.Should().BeEmpty();
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
    public async Task A_duplicate_in_the_box_is_the_same_copy_and_other_boxes_show_as_we_have_more()
    {
        var boxes = new BoxRepository(Factory);
        var items = new ItemRepository(Factory);
        var k1 = await boxes.AddNextAsync("", default);
        var k2 = await boxes.AddNextAsync("", default);
        var reg = new RegistrationUseCases(items);

        await reg.SaveAsync(k1.Id, null, [F("Mort", "God")]);
        (await reg.SaveAsync(k1.Id, null, [F("Mort", "Meget god")])).Should().Be((0, 1));   // read a bit differently in another photo = same copy
        await reg.SaveAsync(k2.Id, null, [F("Mort", "God")]);                              // another box = its own row

        var inK1 = (await items.InBoxAsync(k1.Id, default)).Single();
        inK1.Quantity.Should().Be(1);   // user 2026-10-24: another photo of the same box does not count it again
        var others = await new ItemUseCases(items, new PriceRepository(Factory), Mock.Of<IPhotoStore>()).OthersLikeAsync(inK1);
        others.Single().Box!.Code.Should().Be("K0002");
    }
}
