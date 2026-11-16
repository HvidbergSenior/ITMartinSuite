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
        s.Setup(x => x.LookupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(answer);
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
            [Site("Accord", false, Offers(40, 50, 60)), Site("eBay", false, null), Site("Amazon.de", true, Offers(120))], TimeProvider.System);

        (await hunt.HuntAsync(item.Id)).Should().Be(2);
        var (finds, checks) = await Prices.ForItemAsync(item.Id, default);
        checks.Select(c => (c.Source, c.Ok)).Should().BeEquivalentTo(new[] { ("Accord", true), ("eBay", false), ("Amazon.de", true) });
        finds.Should().HaveCount(3);   // a book: the Danish listings only - Amazon.de is foreign (user 2026-11-06)
        var saved = await Items.GetAsync(item.Id, default);
        saved!.PriceHint.Should().Be(35);          // 25 % under the used normal price 50 (ordinary, 2026-11-15); the new price is not a used price
        saved.PricesAt.Should().NotBeNull();
        hunt.SourceList.Should().Be("Brugt: Accord og eBay. Ny: Amazon.de.");   // names from the test sources above
    }

    [Test]
    public async Task A_kolofon_photo_takes_the_book_off_the_list_and_ten_are_read_per_ai_call()
    {
        var first = new Item { Kind = Kinds.Bog, Title = "Ternet Ninja", EditionHint = EditionHints.First };
        first.Touch();
        var more = Enumerable.Range(0, 11).Select(n => new Item { Kind = Kinds.Bog, Title = $"Bog {n}", KolofonPhoto = $"photos/kolofon/{n}.jpg" }).ToList();
        await Items.AddAsync([first, .. more], default);
        var store = new Mock<IPhotoStore>();
        store.Setup(s => s.ShrinkAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ReturnsAsync([1]);
        store.Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([1, 2, 3]);
        var reader = new Mock<ICoverReader>();
        reader.SetupGet(r => r.Enabled).Returns(true);
        reader.Setup(r => r.ReadKolofonsAsync(It.IsAny<IReadOnlyList<byte[]>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<byte[]> j, CancellationToken _) =>
                Enumerable.Range(0, j.Count).ToDictionary(i => i, _ => new KolofonReading("9780552131063", "1. udgave", 1987, "Gyldendal", null)));
        var kolofon = new KolofonUseCases(Items, store.Object, reader.Object, new LagerOptions(), TimeProvider.System);

        (await kolofon.SavePhotoAsync(first.Id, new MemoryStream([1]))).Should().StartWith("photos");
        (await kolofon.WaitingAsync()).Should().HaveCount(12);   // "venter på at blive læst"

        (await kolofon.ReadWaitingAsync()).Should().Be(Kolofon.PerCall);
        (await kolofon.ReadWaitingAsync()).Should().Be(2);
        (await kolofon.ReadWaitingAsync()).Should().Be(0);
        reader.Verify(r => r.ReadKolofonsAsync(It.Is<IReadOnlyList<byte[]>>(j => j.Count <= Kolofon.PerCall), It.IsAny<CancellationToken>()), Times.Exactly(2));
        var read = await Items.GetAsync(first.Id, default);
        read!.Edition.Should().Be("1. udgave");
        read.Barcode.Should().Be("9780552131063");
        read.KolofonAt.Should().NotBeNull();
        await kolofon.Invoking(k => k.SavePhotoAsync(999_999, new MemoryStream([1]))).Should().ThrowAsync<LagerException>();
    }

    [Test]
    public async Task A_knitted_thing_goes_into_Strikkeshoppen_with_the_makers_name_and_is_never_price_hunted()
    {
        var store = new Mock<IPhotoStore>();
        store.Setup(s => s.ShrinkAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ReturnsAsync([1]);
        var uses = new ItemUseCases(Items, Prices, store.Object);
        await uses.Invoking(u => u.AddHandmadeAsync("Sweater", "Sweatre", 300, "", null, null)).Should().ThrowAsync<LagerException>();   // whose?
        await uses.Invoking(u => u.AddHandmadeAsync("Sweater", "Bukser", 300, "Inge", null, null)).Should().ThrowAsync<LagerException>(); // not a kind of strik

        var item = await uses.AddHandmadeAsync(" Blå sweater med snoninger ", "sweatre", 349.6m, "Inge", "100 % uld", new MemoryStream([1]));
        var saved = (await Items.GetAsync(item.Id, default))!;
        (saved.Kind, saved.Category, saved.Genre, saved.Seller, saved.Price, saved.Usage).Should()
            .Be((Kinds.Strik, ProductTypes.Strik, "Sweatre", "Inge", 350m, Usages.New));
        saved.ItemPhoto.Should().StartWith("photos");
        Shop.View(saved, []).Seller.Should().Be("Inge");
        ShopFamilies.For(ProductTypes.Strik)!.Slug.Should().Be("strik");
        (await uses.HandmadeAsync()).Should().ContainSingle(i => i.Id == item.Id);
        (await uses.AddHandmadeAsync("Hue", "Huer", null, "Inge", null, null)).Price.Should().BeNull();   // "no prices yet"
        Pricing.HuntedKinds.Should().NotContain(Kinds.Strik);

        // Tingshoppen: the same for an old thing - used, its own kinds, its own shop.
        var vase = await uses.AddForSellerAsync(Kinds.Ting, "Rubinglas-vase", "glas", null, "Marianne", null, null);
        (vase.Category, vase.Genre, vase.Usage, vase.Seller).Should().Be((ProductTypes.Ting, "Glas", Usages.Used, "Marianne"));
        await uses.Invoking(u => u.AddForSellerAsync(Kinds.Ting, "Vase", "Sweatre", null, "Marianne", null, null)).Should().ThrowAsync<LagerException>();
        await uses.Invoking(u => u.AddForSellerAsync(Kinds.Bog, "Bog", "Krimi", null, "Marianne", null, null)).Should().ThrowAsync<LagerException>();
        ShopFamilies.For(ProductTypes.Ting)!.Slug.Should().Be("ting");
        Pricing.HuntedKinds.Should().NotContain(Kinds.Ting);

        // More than one picture: the first is the main one, the rest are -2, -3 ...; the shop gets how many there are.
        store.Setup(s => s.FullPath(It.IsAny<string>())).Returns((string x) => "/data/" + x);
        var sweater = await uses.AddForSellerWithPhotosAsync(Kinds.Strik, "Stribet sweater", "Sweatre", null, "StrikkeMarianne", null,
            [new MemoryStream([1]), new MemoryStream([2]), new MemoryStream([3])]);
        (sweater.ItemPhoto, sweater.MorePhotos).Should().Be((Path.Combine("photos", "items", sweater.Id + ".jpg"), 2));
        Shop.View(sweater, []).Photos.Should().Be(3);
        (await uses.PhotoFileAsync(sweater.Id, 3)).Should().EndWith(sweater.Id + "-3.jpg");
        (await uses.PhotoFileAsync(sweater.Id, 4)).Should().BeNull();
        store.Verify(s => s.SaveAsync(Path.Combine("photos", "items", sweater.Id + "-2.jpg"), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // "Ret de svære" (2026-11-15): only our unsure ones, oldest first; Gem takes one off the list.
    [Test]
    public async Task The_difficult_ones_are_our_unsure_things_and_Gem_takes_one_off()
    {
        var a = new Item { Kind = Kinds.Bog, Title = "Usikker 1", Confidence = 0.3 };
        var b = new Item { Kind = Kinds.Bog, Title = "Usikker 2", Confidence = 0.5 };
        var sure = new Item { Kind = Kinds.Bog, Title = "Sikker", Confidence = 0.9 };
        var sold = new Item { Kind = Kinds.Bog, Title = "Solgt", Confidence = 0.2, Status = Statuses.Solgt };
        await Items.AddAsync([a, b, sure, sold], default);
        var uc = new ItemUseCases(Items, Prices, new Mock<IPhotoStore>().Object);

        (await uc.UnsureAsync()).Should().Equal(a.Id, b.Id);
        (await uc.NextUnsureAsync(a.Id)).Should().Be(b.Id);
        (await uc.NextUnsureAsync(b.Id)).Should().Be(a.Id);   // round again

        await uc.SaveAsync(a);
        (await uc.UnsureAsync()).Should().Equal(b.Id);
        (await uc.NextUnsureAsync(b.Id)).Should().BeNull();
    }

    [Test]
    public async Task Item_photos_download_as_a_zip_named_after_the_items()
    {
        var withPhoto = new Item { Kind = Kinds.AndersAnd, Title = "Anders And & Co.", Number = "3", Year = 1955, ItemPhoto = "photos/items/a.jpg" };
        var fileGone = new Item { Kind = Kinds.Bog, Title = "Mort", ItemPhoto = "photos/items/gone.jpg" };
        var noPhoto = new Item { Kind = Kinds.Bog, Title = "Ternet Ninja" };
        await Items.AddAsync([withPhoto, fileGone, noPhoto], default);
        var store = new Mock<IPhotoStore>();
        store.Setup(s => s.ReadAsync("photos/items/a.jpg", It.IsAny<CancellationToken>())).ReturnsAsync([1, 2, 3]);
        store.Setup(s => s.ReadAsync("photos/items/gone.jpg", It.IsAny<CancellationToken>())).ReturnsAsync((byte[]?)null);
        var export = new ExportUseCases(Items, Mock.Of<IBookCheckRepository>(), store.Object);

        using var zipped = new MemoryStream();
        (await export.PhotosZipAsync(zipped, null)).Should().Be(1);
        zipped.Position = 0;
        using var zip = new System.IO.Compression.ZipArchive(zipped);
        zip.Entries.Select(e => (e.FullName, e.Length)).Should().Equal(($"Uden kasse/{withPhoto.Id} Anders And & Co. nr. 3 1955.jpg", 3L));

        using var none = new MemoryStream();
        (await export.PhotosZipAsync(none, "K0099")).Should().Be(0);   // another box: nothing
    }

    [Test]
    public void Photo_file_names_are_safe_everywhere()
    {
        new Item { Id = 7, Title = "AC/DC: Back in Black?", Year = 1980 }.PhotoFileName().Should().Be("7 AC DC Back in Black 1980.jpg");
        new Item { Id = 8 }.PhotoFileName().Should().Be("8.jpg");
        new Item { Id = 9, Title = new string('x', 120) }.PhotoFileName().Should().Be($"9 {new string('x', 80)}.jpg");
    }
}

// The layer rules (CLAUDE.md, user: "i started ITMartin suite as ddd and clean"): Domain knows nothing outside itself
// except the shared words of the buying/selling apps (ITMartinVarer.Contracts - plain records, no EF/HTTP), Application
// only Domain, and no page talks to the database.
public class LayerTests
{
    private const string SharedWords = "ITMartinVarer.Contracts";

    private static string[] Refs(Type t) => t.Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

    [Test]
    public void Domain_has_no_outside_dependencies() =>
        Refs(typeof(Item)).Where(n => n.StartsWith("ITMartin") || n.StartsWith("Microsoft.EntityFramework") || n.StartsWith("Anthropic"))
            .Should().BeEquivalentTo([SharedWords]);

    [Test]
    public void Application_depends_only_on_Domain() =>
        Refs(typeof(ItemUseCases)).Where(n => n.StartsWith("ITMartin") || n.StartsWith("Microsoft.EntityFramework") || n.StartsWith("Anthropic"))
            .Should().BeEquivalentTo(["ITMartinLager.Domain", SharedWords]);

    [Test]
    public void Shared_words_depend_on_nothing() =>
        Refs(typeof(Found)).Where(n => n.StartsWith("ITMartin") || n.StartsWith("Microsoft"))
            .Should().BeEmpty();

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

// Pages and endpoints reach the outside world only through use cases (2026-10-20): no page injects a port.
public class PagePortTests
{
    [Test]
    public void No_page_injects_a_gateway_repository_or_store()
    {
        var server = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "ITMartinLager.Server");
        foreach (var file in Directory.GetFiles(server, "*.razor", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
            File.ReadAllText(file).Should().NotMatchRegex(@"@inject I[A-Z]\w*(Gateway|Repository|Store|Source|Reader|Lookup)\b", file);
    }
}
