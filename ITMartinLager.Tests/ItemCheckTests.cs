using FluentAssertions;
using ITMartinLager.Application;
using ITMartinLager.Domain;
using ITMartinLager.Infrastructure.Persistence;
using ITMartinVarer.Contracts;
using Microsoft.Data.Sqlite;
using Moq;

namespace ITMartinLager.Tests;

// "AI on Gem" (user 2026-11-19: "it is uncertain and i change text or write something. ai need to find the correct one").
public class ItemCheckTests
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

    [Test]
    public void Only_what_the_AI_knows_and_differs_is_a_change_and_an_empty_answer_never_wipes()
    {
        var item = new Item { Kind = Kinds.Tegneserie, Title = "Kozmik: The Comic", Series = "Kozmik" };
        var ai = new Identified(Kinds.Tegneserie, "Den store kamp", "", "kozmik", "12", 1986, "");
        ItemCorrection.Changes(item, ai).Select(c => c.Label).Should().Equal("Titel", "Nummer", "År");   // "kozmik" = same, case aside

        ItemCorrection.Apply(item, ai);
        (item.Title, item.Series, item.Number, item.Year, item.Artist).Should().Be(("Den store kamp", "kozmik", "12", (int?)1986, ""));
    }

    [Test]
    public void What_the_person_typed_is_the_AIs_starting_point()
    {
        var item = new Item { Kind = Kinds.Cd, Title = "Sammen & hver for sig", Artist = "" };
        ItemCorrection.Typed(item).Should().Be("Type: CD\nTitel: Sammen & hver for sig");
    }

    [Test]
    public async Task Gem_asks_the_AI_once_with_the_own_photo_and_the_typed_text_and_nothing_changes_until_Brug_forslaget()
    {
        var item = new Item { Kind = Kinds.Tegneserie, Title = "Kozmik", Confidence = 0.4, ItemPhoto = "photos/items/1.jpg" };
        await Items.AddAsync([item], default);
        var photos = new Mock<IPhotoStore>();
        photos.Setup(p => p.ReadAsync("photos/items/1.jpg", It.IsAny<CancellationToken>())).ReturnsAsync([1, 2]);
        var reader = new Mock<ICoverReader>();
        reader.Setup(r => r.Enabled).Returns(true);
        var ai = new Identified(Kinds.Tegneserie, "Kozmik", "", "Kozmik", "3", 1987, "");
        reader.Setup(r => r.IdentifyAsync(It.IsAny<byte[]>(), false, It.Is<string>(t => t.Contains("Titel: Kozmik")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ai);
        var check = new ItemCheckUseCases(Items, photos.Object, reader.Object);

        var sug = await check.SuggestAsync(item.Id);
        sug.Changes.Select(c => c.Label).Should().Equal("Serie / blad", "Nummer", "År");
        reader.Verify(r => r.IdentifyAsync(It.IsAny<byte[]>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        (await Items.GetAsync(item.Id, default))!.Number.Should().Be("", "only Brug forslaget changes the thing");

        var saved = await check.ApplyAsync(item.Id, ai, new ItemUseCases(Items, new Mock<IPriceRepository>().Object, photos.Object));
        saved!.Number.Should().Be("3");
        (await Items.GetAsync(item.Id, default))!.Year.Should().Be(1987);
    }

    [Test]
    public async Task Without_its_own_photo_the_whole_pile_is_sent_and_the_typed_text_says_which_one()
    {
        var item = new Item { Kind = Kinds.Tegneserie, Title = "Kozmik", Confidence = 0.4, PhotoId = 7 };
        await Items.AddAsync([item], default);
        var vareinfo = new Mock<IVareinfoGateway>();
        vareinfo.Setup(v => v.PhotoAsync(7, null, It.IsAny<CancellationToken>())).ReturnsAsync([9]);
        var reader = new Mock<ICoverReader>();
        reader.Setup(r => r.Enabled).Returns(true);
        var check = new ItemCheckUseCases(Items, new Mock<IPhotoStore>().Object, reader.Object, vareinfo.Object);

        await check.SuggestAsync(item.Id);
        reader.Verify(r => r.IdentifyAsync(It.IsAny<byte[]>(), true, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task No_key_no_call()
    {
        var reader = new Mock<ICoverReader>();
        var sug = await new ItemCheckUseCases(Items, new Mock<IPhotoStore>().Object, reader.Object).SuggestAsync(1);
        sug.Message.Should().Contain("Claude-nøglen mangler");
        reader.Verify(r => r.IdentifyAsync(It.IsAny<byte[]>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
