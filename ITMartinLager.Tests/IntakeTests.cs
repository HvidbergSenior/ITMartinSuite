using FluentAssertions;
using ITMartinLager.Application;
using ITMartinLager.Domain;
using ITMartinLager.Infrastructure.External;
using ITMartinLager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Moq;

namespace ITMartinLager.Tests;

// Vareinfo's photos and scans into Lager's boxes (2026-10-20): fetched as they come, read only on Læs.
public class IntakeTests
{
    private SqliteConnection _conn = null!;
    private TestDbFactory Factory => new(_conn);
    private ItemRepository Items => new(Factory);
    private BoxRepository Boxes => new(Factory);

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

    private static Found F(string t) => new(Kinds.Bog, t, "", "", "", null, "", "God", 1, 0.9, "");
    private static readonly DateTime At = new(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);

    private static Mock<IVareinfoGateway> Vareinfo(CaptureBatch batch)
    {
        var v = new Mock<IVareinfoGateway>();
        v.SetupSequence(x => x.NewAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch).ReturnsAsync(batch).ReturnsAsync(new CaptureBatch([], []));
        v.Setup(x => x.PhotoAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())).ReturnsAsync([9]);
        return v;
    }

    private IntakeUseCases Intake(IVareinfoGateway vareinfo, ICoverReader reader, IBarcodeLookup? lookup = null) =>
        new(vareinfo, new IntakeRepository(Factory), Boxes, new RegistrationUseCases(Items), reader, lookup ?? Mock.Of<IBarcodeLookup>(), TimeProvider.System);

    [Test]
    public async Task Photos_come_in_box_by_box_and_are_read_into_their_box_only_on_Laes()
    {
        var batch = new CaptureBatch(
            [new CapturedPhoto(11, "K0007", "Rico", 4, At), new CapturedPhoto(12, "K0007", "Anna", 4, At), new CapturedPhoto(13, "K0042", "Anna", 0, At)], []);
        var vareinfo = Vareinfo(batch);
        var reader = new Mock<ICoverReader>();
        reader.SetupGet(r => r.Enabled).Returns(true);
        reader.Setup(r => r.ReadPileAsync(It.IsAny<byte[]>(), It.IsAny<IReadOnlyList<byte[]>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([F("Mort"), F("Sourcery")]);
        var intake = Intake(vareinfo.Object, reader.Object);

        (await intake.FetchAsync()).Should().Be(3);
        (await intake.FetchAsync()).Should().Be(3);   // Vareinfo sent the same again (it missed the OK): nothing doubled
        vareinfo.Verify(v => v.ReceivedAsync(It.Is<CaptureReceived>(r => r.PhotoIds.SequenceEqual(new[] { 11, 12, 13 })), It.IsAny<CancellationToken>()), Times.Exactly(2));
        (await Boxes.ByCodeAsync("K0042", default)).Should().NotBeNull();   // the box came with its first photo

        // Nothing read without Læs.
        (await intake.ReadWantedAsync()).Should().Be(0);
        reader.Verify(r => r.ReadPileAsync(It.IsAny<byte[]>(), It.IsAny<IReadOnlyList<byte[]>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        (await intake.BoxesAsync()).Should().BeEquivalentTo(
            [new BoxIntake("K0042", 1, 0, 0, 0, 0, 0, "Anna"), new BoxIntake("K0007", 2, 0, 0, 0, 0, 0, "Anna, Rico")], o => o.WithStrictOrdering());

        (await intake.WantReadAsync("K0007")).Should().Be(2);
        (await intake.ReadWantedAsync()).Should().Be(2);

        // One call per photo with its 4 close-ups; the same two books twice in one box = 2 copies each.
        reader.Verify(r => r.ReadPileAsync(It.IsAny<byte[]>(), It.Is<IReadOnlyList<byte[]>>(c => c.Count == 4), "", It.IsAny<CancellationToken>()), Times.Exactly(2));
        var box7 = await Boxes.ByCodeAsync("K0007", default);
        var inBox = await Items.InBoxAsync(box7!.Id, default);
        inBox.Select(i => (i.Title, i.Quantity, i.PhotoId)).Should().BeEquivalentTo(new[] { ("Mort", 2, (int?)11), ("Sourcery", 2, (int?)11) });
        (await Items.InBoxAsync((await Boxes.ByCodeAsync("K0042", default))!.Id, default)).Should().BeEmpty();
        (await intake.BoxesAsync()).Last().Should().Be(new BoxIntake("K0007", 0, 0, 2, 4, 0, 0, "Anna, Rico"));
    }

    [Test]
    public async Task Scans_are_looked_up_on_Laes_and_unknown_codes_are_kept()
    {
        var vareinfo = Vareinfo(new CaptureBatch([], [new CapturedScan(1, "K0003", "Rico", "9780552131063", 2, At), new CapturedScan(2, "K0003", "Rico", "5051892000000", 1, At)]));
        var lookup = new Mock<IBarcodeLookup>();
        lookup.Setup(l => l.LookupAsync("9780552131063", It.IsAny<CancellationToken>())).ReturnsAsync(F("Mort"));
        var reader = new Mock<ICoverReader>();
        reader.SetupGet(r => r.Enabled).Returns(true);
        var intake = Intake(vareinfo.Object, reader.Object, lookup.Object);

        await intake.FetchAsync();
        await intake.WantReadAsync(null);
        (await intake.ReadWantedAsync()).Should().Be(2);

        var items = await Items.InBoxAsync((await Boxes.ByCodeAsync("K0003", default))!.Id, default);
        items.Should().HaveCount(2);
        items.Single(i => i.Title == "Mort").Should().Match<Item>(i => i.Quantity == 2 && i.Barcode == "9780552131063");
        items.Single(i => i.Title != "Mort").Title.Should().Contain("5051892000000");
        reader.Verify(r => r.ReadPileAsync(It.IsAny<byte[]>(), It.IsAny<IReadOnlyList<byte[]>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task A_photo_stays_waiting_when_the_reader_fails()
    {
        var vareinfo = Vareinfo(new CaptureBatch([new CapturedPhoto(11, "K0001", "Rico", 0, At)], []));
        var reader = new Mock<ICoverReader>();
        reader.SetupGet(r => r.Enabled).Returns(true);
        reader.Setup(r => r.ReadPileAsync(It.IsAny<byte[]>(), It.IsAny<IReadOnlyList<byte[]>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Dagens grænse"));
        var intake = Intake(vareinfo.Object, reader.Object);
        await intake.FetchAsync();
        await intake.WantReadAsync(null);

        await intake.Invoking(i => i.ReadWantedAsync()).Should().ThrowAsync<InvalidOperationException>();

        (await intake.BoxesAsync()).Single().Should().Be(new BoxIntake("K0001", 1, 1, 0, 0, 0, 0, "Rico"));
    }

    [Test]
    public async Task Boxes_made_from_helper_numbers_and_Ny_kasse_never_clash()
    {
        await Boxes.GetOrAddAsync("K0057", default);
        (await Boxes.GetOrAddAsync("k57", default)).Code.Should().Be("K0057");   // the same box, however it is written
        (await Boxes.AddNextAsync("", default)).Code.Should().Be("K0058");       // after the highest number, not the newest box

        var all = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Boxes.GetOrAddAsync("K0100", default)));
        all.Select(b => b.Id).Distinct().Should().ContainSingle();
    }

    [Test]
    public void Ai_answers_are_cleaned_unknown_kinds_conditions_and_silly_years()
    {
        var f = ClaudeCoverReader.Clean(new ClaudeCoverReader.RawItem { Kind = "jumbobog", Title = " Jumbobog 50 ", Condition = "fin", Year = 19, Quantity = 0, Confidence = 3 });
        f.Kind.Should().Be(Kinds.Jumbobog);
        f.Title.Should().Be("Jumbobog 50");
        f.Condition.Should().Be("God");
        f.Year.Should().BeNull();
        f.Quantity.Should().Be(1);
        f.Confidence.Should().Be(1);
        ClaudeCoverReader.Clean(new ClaudeCoverReader.RawItem { Kind = "vinyl", Title = "x" }).Kind.Should().Be(Kinds.Andet);
    }
}
