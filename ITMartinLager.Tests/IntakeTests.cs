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

    private static PileReading Pile(params Found[] f) => new([.. f]);
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
            .ReturnsAsync(Pile(F("Mort"), F("Sourcery")));
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

        // One call per photo with its 4 close-ups; the same two books in two photos of one box = the same copies (user 2026-10-24).
        reader.Verify(r => r.ReadPileAsync(It.IsAny<byte[]>(), It.Is<IReadOnlyList<byte[]>>(c => c.Count == 4), "", It.IsAny<CancellationToken>()), Times.Exactly(2));
        var box7 = await Boxes.ByCodeAsync("K0007", default);
        var inBox = await Items.InBoxAsync(box7!.Id, default);
        inBox.Select(i => (i.Title, i.Quantity, i.PhotoId)).Should().BeEquivalentTo(new[] { ("Mort", 1, (int?)11), ("Sourcery", 1, (int?)11) });
        (await Items.InBoxAsync((await Boxes.ByCodeAsync("K0042", default))!.Id, default)).Should().BeEmpty();
        (await intake.BoxesAsync()).Last().Should().Be(new BoxIntake("K0007", 0, 0, 2, 4, 0, 0, "Anna, Rico"));
    }

    [Test]
    public async Task Scans_are_looked_up_on_Laes_and_unknown_codes_are_only_counted()
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
        // The unknown code is not an item (it would count a photographed DVD twice) - Kasser shows it as 1 unknown.
        items.Should().ContainSingle().Which.Should().Match<Item>(i => i.Title == "Mort" && i.Quantity == 2 && i.Barcode == "9780552131063");
        (await intake.BoxesAsync()).Single().Unknown.Should().Be(1);
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
        ClaudeCoverReader.Clean(new ClaudeCoverReader.RawItem { Kind = "frimærke", Title = "x" }).Kind.Should().Be(Kinds.Andet);
        ClaudeCoverReader.Clean(new ClaudeCoverReader.RawItem { Kind = "vinyl", Title = "x" }).Kind.Should().Be(Kinds.Vinyl);
        ClaudeCoverReader.Clean(new ClaudeCoverReader.RawItem { Title = "x", Usage = "Ny" }).Usage.Should().Be(Usages.New);
        ClaudeCoverReader.Clean(new ClaudeCoverReader.RawItem { Title = "x" }).Usage.Should().Be(Usages.Used);
    }

    // Day sessions (user 2026-11-10: "take pictures of my products -> and on Bogshoppen"): read at once, no Læs, and the reader is
    // told what the pile mostly is from the button. The things are ordinary stock (they go on bogshoppen.dk by themselves).
    [Test]
    public async Task A_day_session_is_read_at_once_with_its_button_as_the_hint()
    {
        var batch = new CaptureBatch([new CapturedPhoto(31, "K5012", "Rico", 0, At), new CapturedPhoto(32, "K5013", "Rico", 0, At)], [],
            [new CapturedBox("K5012", "Blade og album – 10. november", Kind: "Blade og album", ReadNow: true),
             new CapturedBox("K5013", "Blandet – 10. november", Kind: "Blandet", ReadNow: true)]);
        var reader = new Mock<ICoverReader>();
        reader.SetupGet(r => r.Enabled).Returns(true);
        reader.Setup(r => r.ReadPileAsync(It.IsAny<byte[]>(), It.IsAny<IReadOnlyList<byte[]>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pile(F("Jonah Hex 49")));
        var intake = Intake(Vareinfo(batch).Object, reader.Object);

        await intake.FetchAsync();
        (await intake.ReadWantedAsync()).Should().Be(2, "no Læs needed for a day session");
        reader.Verify(r => r.ReadPileAsync(It.IsAny<byte[]>(), It.IsAny<IReadOnlyList<byte[]>>(), CaptureKinds.HintFor("Blade og album"), It.IsAny<CancellationToken>()), Times.Once);
        reader.Verify(r => r.ReadPileAsync(It.IsAny<byte[]>(), It.IsAny<IReadOnlyList<byte[]>>(), "", It.IsAny<CancellationToken>()), Times.Once);
        var box = await Boxes.GetOrAddAsync("K5012", default);
        box.Kind.Should().Be("Blade og album");
        box.Fund.Should().BeFalse();
        (await Items.InBoxAsync(box.Id, default)).Should().OnlyContain(i => i.Status != Statuses.Fund);
    }

    // "Tag igen" (2026-11-15: "if the picture is rotated and so on it should be taken again"): nothing is made, the reason is kept.
    [Test]
    public async Task A_rotated_photo_makes_nothing_and_says_take_it_again()
    {
        var batch = new CaptureBatch([new CapturedPhoto(51, "K5030", "Martin", 0, At)], [], [new CapturedBox("K5030", "16. november", Kind: "Blandet", ReadNow: true)]);
        var reader = new Mock<ICoverReader>();
        reader.SetupGet(r => r.Enabled).Returns(true);
        reader.Setup(r => r.ReadPileAsync(It.IsAny<byte[]>(), It.IsAny<IReadOnlyList<byte[]>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PileReading([F("Mort")], "drejet"));
        var intake = Intake(Vareinfo(batch).Object, reader.Object);

        await intake.FetchAsync();
        await intake.ReadWantedAsync();

        (await Items.InBoxAsync((await Boxes.GetOrAddAsync("K5030", default)).Id, default)).Should().BeEmpty();
        (await intake.UnreadableAsync()).Single().Retake.Should().Be("drejet");
    }

    // "for instance agression book says 69 on it" (2026-11-15): the sticker price is read with the photo and kept on the thing.
    [Test]
    public void A_price_sticker_on_the_thing_is_kept_and_nonsense_is_not()
    {
        var f = ClaudeCoverReader.Clean(new ClaudeCoverReader.RawItem { Kind = Kinds.Bog, Title = "Aggression", Condition = "God", Quantity = 1, Confidence = 0.9, StickerKr = 69 });
        f.StickerKr.Should().Be(69);
        Item.From(f, null, null).StickerKr.Should().Be(69);
        ClaudeCoverReader.Clean(new ClaudeCoverReader.RawItem { Kind = Kinds.Bog, Title = "X", StickerKr = 0 }).StickerKr.Should().BeNull();
    }

    // "if picture is too bad and nothing can be read... It should not fill it out" (2026-11-15): a reading below Readings.CannotRead
    // makes nothing; a photo that gave nothing is listed as "kunne ikke læses".
    [Test]
    public async Task What_the_AI_could_not_read_is_not_made_and_the_photo_is_listed()
    {
        var batch = new CaptureBatch([new CapturedPhoto(41, "K5020", "Martin", 0, At), new CapturedPhoto(42, "K5020", "Martin", 0, At)], [],
            [new CapturedBox("K5020", "15. november", Kind: "Blandet", ReadNow: true)]);
        var reader = new Mock<ICoverReader>();
        reader.SetupGet(r => r.Enabled).Returns(true);
        reader.SetupSequence(r => r.ReadPileAsync(It.IsAny<byte[]>(), It.IsAny<IReadOnlyList<byte[]>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pile(F("Mort"), F("Blandede kort i bunke") with { Confidence = 0.3 }, F("Usikker") with { Confidence = 0.4 }))
            .ReturnsAsync(Pile(F("Noget utydeligt") with { Confidence = 0.2 }));
        var intake = Intake(Vareinfo(batch).Object, reader.Object);

        await intake.FetchAsync();
        await intake.ReadWantedAsync();

        var box = await Boxes.GetOrAddAsync("K5020", default);
        (await Items.InBoxAsync(box.Id, default)).Select(i => i.Title).Should().BeEquivalentTo("Mort", "Usikker");   // 0.4 goes to Ret de svære
        (await intake.UnreadableAsync()).Select(s => s.VareinfoId).Should().Equal(42);
    }

    // Tjek in Vareinfo (2026-11-10): Nej tak per thing, Fortryd back on the list, and only the things of that box.
    [Test]
    public async Task Tjek_says_no_per_thing_takes_it_back_and_knows_its_box()
    {
        var batch = new CaptureBatch([new CapturedPhoto(41, "K5020", "Rico", 0, At)], [], [new CapturedBox("K5020", "Tjek: Blandet – 10. november", Fund: true, Kind: "Blandet", ReadNow: true)]);
        var reader = new Mock<ICoverReader>();
        reader.SetupGet(r => r.Enabled).Returns(true);
        reader.Setup(r => r.ReadPileAsync(It.IsAny<byte[]>(), It.IsAny<IReadOnlyList<byte[]>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pile(F("Mort"), F("Eric")));
        var intake = Intake(Vareinfo(batch).Object, reader.Object);
        await intake.FetchAsync();
        await intake.ReadWantedAsync();
        var fund = new FundUseCases(Boxes, Items, new GoldRepository(Factory));
        var trip = (await fund.TripByCodeAsync("K5020"))!.Value;
        trip.Open.Should().HaveCount(2);
        var mort = trip.Open.Single(r => r.Item.Title == "Mort").Item;

        await fund.DeclineAsync(mort.Id);
        (await fund.TripByCodeAsync("K5020"))!.Value.Should().Match<(Box Box, List<FundRow> Open, List<FundRow> Taken, int NotBought)>(t => t.Open.Count == 1 && t.NotBought == 1);
        await fund.BackAsync(mort.Id);
        (await Items.GetAsync(mort.Id, default))!.Status.Should().Be(Statuses.Fund);
        await fund.TakeAsync(mort.Id);
        await fund.BackAsync(mort.Id);
        (await Items.GetAsync(mort.Id, default))!.Status.Should().Be(Statuses.Fund, "Fortryd also after Tog den");

        (await fund.InBoxAsync(mort.Id, "K5020")).Should().BeTrue();
        (await fund.InBoxAsync(mort.Id, "K5021")).Should().BeFalse("Vareinfo may only change the things of the session it shows");
        (await fund.TripByCodeAsync("K9999")).Should().BeNull();

        // The day is over: what was not taken becomes Ikke købt by itself; what was taken stays ours.
        var eric = (await fund.TripByCodeAsync("K5020"))!.Value.Open.Single(r => r.Item.Title == "Eric").Item;
        await fund.TakeAsync(eric.Id);
        var later = new FundUseCases(Boxes, Items, new GoldRepository(Factory), new FixedClock(DateTime.UtcNow.AddDays(1)));
        (await fund.ForgetOldDaysAsync()).Should().Be(0, "still the same day");
        (await later.ForgetOldDaysAsync()).Should().Be(1);
        (await Items.GetAsync(mort.Id, default))!.Status.Should().Be(Statuses.IkkeKoebt);
        (await Items.GetAsync(eric.Id, default))!.Status.Should().Be(Statuses.Lager);
    }

    private sealed class FixedClock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
    }

    // Fund (user 2026-11-03: "The pictures today are NOT products from our storage"): a Fund session's photos are read at
    // once, the things are Fund (not ours, not for sale); Tog den = stock, Færdig = the rest Ikke købt.
    [Test]
    public async Task A_Fund_session_is_read_at_once_and_only_what_is_taken_becomes_stock()
    {
        var batch = new CaptureBatch([new CapturedPhoto(21, "K5003", "MartinAdmin", 0, At)], [], [new CapturedBox("K5003", "Session 3 – Genbrugsplads", Fund: true)]);
        var reader = new Mock<ICoverReader>();
        reader.SetupGet(r => r.Enabled).Returns(true);
        reader.Setup(r => r.ReadPileAsync(It.IsAny<byte[]>(), It.IsAny<IReadOnlyList<byte[]>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pile(F("Mort"), F("Sourcery") with { PriceHint = 120 }, F("Eric")));
        var intake = Intake(Vareinfo(batch).Object, reader.Object);

        await intake.FetchAsync();
        (await intake.ReadWantedAsync()).Should().Be(1, "no Læs needed for a Fund session");
        var box = (await Boxes.ByCodeAsync("K5003", default))!;
        box.Fund.Should().BeTrue();
        var found = await Items.InBoxAsync(box.Id, default);
        found.Should().HaveCount(3).And.OnlyContain(i => i.Status == Statuses.Fund);
        (await Items.UnitsAsync(default)).Should().Be(0, "fund is not our stock");
        (await new ItemUseCases(Items, Mock.Of<IPriceRepository>(), Mock.Of<IPhotoStore>()).InterestingAsync([])).Should().BeEmpty();

        var fund = new FundUseCases(Boxes, Items, new GoldRepository(Factory));
        (await fund.TripsAsync()).Single().Should().Match<FundTrip>(t => t.Open == 3 && t.Taken == 0 && t.Waiting == 3);
        var sourcery = found.Single(i => i.Title == "Sourcery");
        await fund.TakeAsync(sourcery.Id);
        await fund.TakeAsync(sourcery.Id);   // a double tap changes nothing
        (await Items.GetAsync(sourcery.Id, default))!.Status.Should().Be(Statuses.Lager);
        (await Items.UnitsAsync(default)).Should().Be(1);

        (await fund.FinishAsync(box.Id)).Should().Be(2);
        var trip = (await fund.TripAsync(box.Id))!.Value;
        (trip.Open.Count, trip.Taken.Single().Item.Title, trip.NotBought).Should().Be((0, "Sourcery", 2));
        (await Items.NextToHuntAsync(Pricing.HuntedKinds, DateTime.UtcNow, 10, default)).Should().Equal([sourcery.Id], "what was not bought is never priced again");

        (await fund.ReopenAsync(box.Id)).Should().Be(2);
        await fund.UndoAsync(sourcery.Id);
        (await fund.TripAsync(box.Id))!.Value.Open.Should().HaveCount(3);
    }

    [Test]
    public void Fund_list_is_interesting_and_dearest_first_and_waiting_last()
    {
        var now = DateTime.UtcNow;
        Item I(string t, decimal? kr, bool priced = true) => new() { Kind = Kinds.Bog, Title = t, PriceHint = kr, PricesAt = priced ? now : null };
        FundRules.Rows([I("Billig", 15), I("Venter", null, priced: false), I("Dyr", 250), I("Mellem", 60)], [])
            .Select(r => r.Item.Title).Should().Equal("Dyr", "Mellem", "Billig", "Venter");
    }

    [Test]
    public async Task A_kolofon_page_taken_before_the_cover_waits_and_lands_on_the_book()
    {
        // Live 2026-11-03 (TestMartin): the kolofon of "Sallies historier" came before its cover, and a back cover became a thing.
        var batch = new CaptureBatch([new CapturedPhoto(21, "K9999", "TestMartin", 0, At), new CapturedPhoto(22, "K9999", "TestMartin", 0, At),
            new CapturedPhoto(23, "K9999", "TestMartin", 0, At)], []);
        var reader = new Mock<ICoverReader>();
        reader.SetupGet(r => r.Enabled).Returns(true);
        var kolofon = F("Sallies historier") with { Artist = "Kim Fupz Aakeson", Kolofon = true, Edition = "1. oplag, 2. udgave", Barcode = "9788762601819", Year = 2007, Publisher = "Carlsen" };
        var back = new Found(Kinds.Bog, "", "", "", "", null, "", "God", 1, 0.5, "", Barcode: "9788762601819", Kolofon: true);
        reader.SetupSequence(r => r.ReadPileAsync(It.IsAny<byte[]>(), It.IsAny<IReadOnlyList<byte[]>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pile(kolofon))
            .ReturnsAsync(Pile(F("Sallies historier") with { Artist = "Kim Fupz Aakeson" }))
            .ReturnsAsync(Pile(back));
        var intake = new IntakeUseCases(Vareinfo(batch).Object, new IntakeRepository(Factory), Boxes, new RegistrationUseCases(Items), reader.Object,
            Mock.Of<IBarcodeLookup>(), TimeProvider.System, items: Items);
        await intake.FetchAsync();
        await intake.WantReadAsync("K9999");
        await intake.ReadWantedAsync();

        var box = await Boxes.ByCodeAsync("K9999", default);
        var inBox = await Items.InBoxAsync(box!.Id, default);
        inBox.Should().ContainSingle("the kolofon page and the back cover are not things of their own");
        var book = inBox[0];
        (book.Title, book.Edition, book.Barcode, book.Publisher, book.Year).Should().Be(("Sallies historier", "1. oplag, 2. udgave", "9788762601819", "Carlsen", 2007));
        book.KolofonAt.Should().NotBeNull();
        (await new IntakeRepository(Factory).WaitingPagesAsync(box.Id, default)).Should().BeEmpty("the back cover matched by ISBN");
    }
}
