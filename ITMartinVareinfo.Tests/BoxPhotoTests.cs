using FluentAssertions;
using ITMartinVareinfo.Application;
using ITMartinVareinfo.Domain;
using ITMartinVareinfo.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Moq;

namespace ITMartinVareinfo.Tests;

public class BoxPhotoTests
{
    private SqliteConnection _conn = null!;
    private TestDbFactory Factory => new(_conn);
    private readonly LagerBox _k1 = new(1, "K0001"), _k2 = new(2, "K0002");

    [SetUp]
    public void Open()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        using var db = Factory.CreateDbContext();
        VareinfoDbInitializer.Upgrade(db);
    }

    [TearDown]
    public void Close() => _conn.Dispose();

    private static Mock<IPhotoStore> Store()
    {
        var p = new Mock<IPhotoStore>();
        p.Setup(x => x.ShrinkAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ReturnsAsync([1]);
        p.Setup(x => x.TilesAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ReturnsAsync([[2], [3], [4], [5]]);
        p.Setup(x => x.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([9]);
        return p;
    }

    private static Found F(string t) => new(Kinds.Bog, t, "", "", "", null, "", "God", 1, 0.9, "");

    [Test]
    public void Box_numbers_are_what_is_written_on_the_box()
    {
        BoxNumbers.Of("K0007").Should().Be(7);
        BoxNumbers.Of("K0120").Should().Be(120);
        new LagerBox(3, "K0012").Number.Should().Be(12);
    }

    [Test]
    public async Task Photos_are_only_stored_and_read_into_their_box_when_asked()
    {
        var rows = new PhotoRepository(Factory);
        var reader = new Mock<ICoverReader>();
        reader.SetupGet(r => r.Enabled).Returns(true);
        reader.Setup(r => r.ReadPileAsync(It.IsAny<byte[]>(), It.IsAny<IReadOnlyList<byte[]>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([F("Mort"), F("Sourcery")]);
        var lager = new Mock<ILagerGateway>();
        lager.Setup(l => l.SaveInBoxAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<Found>>(), It.IsAny<CancellationToken>())).ReturnsAsync((2, 0));
        var boxes = new BoxPhotoUseCases(rows, Store().Object, reader.Object, lager.Object, TimeProvider.System);

        await boxes.StoreAsync(new MemoryStream([1]), _k1);
        await boxes.StoreAsync(new MemoryStream([1]), _k1);
        await boxes.StoreAsync(new MemoryStream([1]), _k2);

        // Nothing read yet - and nothing read without Læs.
        (await boxes.ReadWantedAsync()).Should().Be(0);
        reader.Verify(r => r.ReadPileAsync(It.IsAny<byte[]>(), It.IsAny<IReadOnlyList<byte[]>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        (await boxes.BoxesAsync()).Should().BeEquivalentTo([new BoxPhotos("K0002", 1, 0, 0, 0), new BoxPhotos("K0001", 2, 0, 0, 0)],
            o => o.WithStrictOrdering());

        (await boxes.WantReadAsync("K0001")).Should().Be(2);
        (await boxes.ReadWantedAsync()).Should().Be(2);

        // One call per photo, with its 4 close-ups, and the things go into box K0001 only.
        reader.Verify(r => r.ReadPileAsync(It.IsAny<byte[]>(), It.Is<IReadOnlyList<byte[]>>(c => c.Count == 4), "", It.IsAny<CancellationToken>()), Times.Exactly(2));
        lager.Verify(l => l.SaveInBoxAsync("K0001", It.IsAny<int>(), It.Is<IReadOnlyList<Found>>(f => f.Count == 2), It.IsAny<CancellationToken>()), Times.Exactly(2));
        lager.Verify(l => l.SaveInBoxAsync("K0002", It.IsAny<int>(), It.IsAny<IReadOnlyList<Found>>(), It.IsAny<CancellationToken>()), Times.Never);
        (await boxes.BoxesAsync()).Should().BeEquivalentTo([new BoxPhotos("K0002", 1, 0, 0, 0), new BoxPhotos("K0001", 0, 0, 2, 4)],
            o => o.WithStrictOrdering());
    }

    [Test]
    public async Task A_photo_stays_waiting_when_Lager_is_down()
    {
        var rows = new PhotoRepository(Factory);
        var reader = new Mock<ICoverReader>();
        reader.SetupGet(r => r.Enabled).Returns(true);
        reader.Setup(r => r.ReadPileAsync(It.IsAny<byte[]>(), It.IsAny<IReadOnlyList<byte[]>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([F("Mort")]);
        var lager = new Mock<ILagerGateway>();
        lager.Setup(l => l.SaveInBoxAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<Found>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new VareinfoException("Lager svarede ikke"));
        var boxes = new BoxPhotoUseCases(rows, Store().Object, reader.Object, lager.Object, TimeProvider.System);
        await boxes.StoreAsync(new MemoryStream([1]), _k1);
        await boxes.WantReadAsync(null);

        await boxes.Invoking(b => b.ReadWantedAsync()).Should().ThrowAsync<VareinfoException>();

        (await boxes.BoxesAsync()).Single().Should().Be(new BoxPhotos("K0001", 1, 1, 0, 0));
    }

    [Test]
    public async Task A_new_box_is_refused_while_the_current_one_has_no_photos()
    {
        var lager = new Mock<ILagerGateway>();
        lager.Setup(l => l.LatestBoxAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_k1);
        lager.Setup(l => l.NewBoxAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_k2);
        var boxes = new BoxPhotoUseCases(new PhotoRepository(Factory), Store().Object, Mock.Of<ICoverReader>(), lager.Object, TimeProvider.System);

        await boxes.Invoking(b => b.NewBoxAsync()).Should().ThrowAsync<VareinfoException>().WithMessage("*Kasse 1*");
        await boxes.StoreAsync(new MemoryStream([1]), _k1);
        (await boxes.NewBoxAsync()).Should().Be(_k2);
    }

    [Test]
    public async Task The_first_box_is_made_when_Lager_has_none()
    {
        var lager = new Mock<ILagerGateway>();
        lager.Setup(l => l.NewBoxAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_k1);
        var boxes = new BoxPhotoUseCases(new PhotoRepository(Factory), Store().Object, Mock.Of<ICoverReader>(), lager.Object, TimeProvider.System);
        (await boxes.CurrentBoxAsync()).Number.Should().Be(1);
    }

    [Test]
    public void An_old_database_gets_the_new_columns_and_old_photos_count_as_read()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE "Photos" ("Id" INTEGER PRIMARY KEY, "Path" TEXT NOT NULL, "BoxId" INTEGER NULL, "Found" INTEGER NOT NULL, "TakenAt" TEXT NOT NULL);
                INSERT INTO "Photos" VALUES (5, 'photos/a.jpg', NULL, 8, '2026-10-12 10:00:00');
                """;
            cmd.ExecuteNonQuery();
        }
        using var db = new TestDbFactory(conn).CreateDbContext();
        VareinfoDbInitializer.Upgrade(db);
        var p = db.Photos.Single();
        p.Waiting.Should().BeFalse();
        p.BoxCode.Should().Be("");
    }
}
