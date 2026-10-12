using FluentAssertions;
using ITMartinLager.Application;
using ITMartinLager.Domain;
using ITMartinLager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Moq;

namespace ITMartinLager.Tests;

public class UseCaseTests
{
    private SqliteConnection _conn = null!;
    private TestDbFactory Factory => new(_conn);
    private ItemRepository Items => new(Factory);
    private PriceRepository Prices => new(Factory);

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

    private static IPriceSource Site(string name, bool isNew, Listings? answer)
    {
        var s = new Mock<IPriceSource>();
        s.SetupGet(x => x.Name).Returns(name);
        s.SetupGet(x => x.IsNew).Returns(isNew);
        s.SetupGet(x => x.Country).Returns("");
        s.Setup(x => x.Fits(It.IsAny<string>())).Returns(true);
        s.Setup(x => x.LookupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(answer);
        return s.Object;
    }

    private static Listings Offers(params decimal[] kr) => ListingParse.Summarise(kr.Select(p => new Offer("Mort Terry Pratchett", p, $"https://x/{p}")).ToList(), "u");

    private async Task<Item> Book()
    {
        var item = new Item { Kind = Kinds.Bog, Title = "Mort", Artist = "Terry Pratchett", BoxId = null };
        item.Touch();
        await Items.AddAsync([item], default);
        return item;
    }

    [Test]
    public async Task A_hunt_keeps_every_site_answer_the_proof_and_the_normal_used_price()
    {
        var item = await Book();
        var hunt = new PriceHuntUseCases(Items, Prices,
            [Site("DBA", false, Offers(40, 50, 60)), Site("eBay", false, null), Site("Amazon.de", true, Offers(120))], TimeProvider.System);

        (await hunt.HuntAsync(item.Id)).Should().Be(2);
        var (finds, checks) = await Prices.ForItemAsync(item.Id, default);
        checks.Select(c => (c.Source, c.Ok)).Should().BeEquivalentTo(new[] { ("DBA", true), ("eBay", false), ("Amazon.de", true) });
        finds.Should().HaveCount(4);
        var saved = await Items.GetAsync(item.Id, default);
        saved!.PriceHint.Should().Be(45);          // 10 % under the used normal price 50; the new price is not a used price
        saved.PricesAt.Should().NotBeNull();
        hunt.SourceList.Should().Be("Brugt: DBA og eBay. Ny: Amazon.de.");
    }

    [Test]
    public async Task Waiting_kolofons_are_read_ten_per_ai_call()
    {
        var books = new List<Item>();
        for (var n = 0; n < 12; n++)
        {
            var b = new Item { Kind = Kinds.Bog, Title = $"Bog {n}", KolofonPhoto = $"photos/kolofon/{n}.jpg" };
            b.Touch();
            books.Add(b);
        }
        await Items.AddAsync(books, default);
        var photos = new Mock<IPhotoStore>();
        photos.Setup(p => p.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([1, 2, 3]);
        var reader = new Mock<ICoverReader>();
        reader.SetupGet(r => r.Enabled).Returns(true);
        reader.Setup(r => r.ReadKolofonsAsync(It.IsAny<IReadOnlyList<byte[]>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<byte[]> j, CancellationToken _) =>
                Enumerable.Range(0, j.Count).ToDictionary(i => i, _ => new KolofonReading("9780552131063", "1. udgave", 1987, "Gyldendal", null)));
        var kolofon = new KolofonUseCases(Items, photos.Object, reader.Object, new LagerOptions(), TimeProvider.System);

        (await kolofon.ReadWaitingAsync()).Should().Be(10);
        (await kolofon.ReadWaitingAsync()).Should().Be(2);
        (await kolofon.ReadWaitingAsync()).Should().Be(0);
        reader.Verify(r => r.ReadKolofonsAsync(It.Is<IReadOnlyList<byte[]>>(j => j.Count <= Kolofon.PerCall), It.IsAny<CancellationToken>()), Times.Exactly(2));
        (await kolofon.WaitingAsync()).Should().Be(0);
        (await Items.GetAsync(books[0].Id, default))!.Edition.Should().Be("1. udgave");
    }

    [Test]
    public async Task A_book_found_dear_or_with_a_first_edition_cover_goes_on_the_kolofon_list()
    {
        var dear = await Book();
        var first = new Item { Kind = Kinds.Bog, Title = "Ternet Ninja", EditionHint = EditionHints.First };
        var cheap = new Item { Kind = Kinds.Bog, Title = "Billig" };
        first.Touch(); cheap.Touch();
        await Items.AddAsync([first, cheap], default);
        await Prices.ReplaceAsync(dear.Id, [], [new PriceFind { ItemId = dear.Id, Source = "AbeBooks", Kr = 400, Url = "u" }], default);
        await Prices.ReplaceAsync(cheap.Id, [], [new PriceFind { ItemId = cheap.Id, Source = "DBA", Kr = 20, Url = "u" }], default);

        var wanted = await Items.KolofonWantedAsync(150, 200, default);
        wanted.Select(w => (w.Item.Title, w.TopKr)).Should().Equal(("Ternet Ninja", 0), ("Mort", 400));
    }
}

// The layer rules (CLAUDE.md, user: "i started ITMartin suite as ddd and clean"): Domain knows nothing outside itself,
// Application only Domain, and no page talks to the database.
public class LayerTests
{
    private static string[] Refs(Type t) => t.Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

    [Test]
    public void Domain_has_no_outside_dependencies() =>
        Refs(typeof(Item)).Where(n => n.StartsWith("ITMartin") || n.StartsWith("Microsoft.EntityFramework") || n.StartsWith("Anthropic"))
            .Should().BeEmpty();

    [Test]
    public void Application_depends_only_on_Domain() =>
        Refs(typeof(ItemUseCases)).Where(n => n.StartsWith("ITMartin") || n.StartsWith("Microsoft.EntityFramework") || n.StartsWith("Anthropic"))
            .Should().BeEquivalentTo(["ITMartinLager.Domain"]);

    [Test]
    public void No_page_uses_the_database_or_infrastructure()
    {
        var pages = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "ITMartinLager.Server", "Pages");
        foreach (var file in Directory.GetFiles(pages, "*.razor"))
        {
            var text = File.ReadAllText(file);
            text.Should().NotContain("DbContext", file).And.NotContain("LagerDb", file).And.NotContain("Infrastructure", file);
        }
    }
}
