using FluentAssertions;
using ITMartinAuction.Application;
using ITMartinAuction.Domain;
using ITMartinAuction.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ITMartinAuction.Tests;

public class BiddingRuleTests
{
    private static AuctionItem Item(decimal start, params (decimal Amount, bool Pre)[] bids) => new()
    {
        StartingPrice = start,
        Bids = bids.Select(b => new Bid { Amount = b.Amount, IsPreBid = b.Pre, BidderId = Guid.NewGuid() }).ToList(),
    };

    [Test]
    public void The_first_bid_is_the_starting_price_then_one_more_than_the_top() =>
        (Bidding.MinBid(Item(50)), Bidding.MinBid(Item(50, (65, false), (55, false), (200, true)))).Should().Be((50m, 66m));

    [Test]
    public void The_highest_pre_bid_goes_live_and_the_others_drop()
    {
        var item = Item(50, (60, true), (80, true), (70, true));
        var (promoted, stale) = Bidding.PromotePreBid(item);
        promoted!.Amount.Should().Be(80);
        promoted.IsPreBid.Should().BeFalse();
        stale.Select(s => s.Amount).Should().BeEquivalentTo([60m, 70m]);
    }

    [Test]
    public void Hammer_sells_to_the_top_live_bid_and_no_bid_passes()
    {
        var sold = Item(50, (65, false), (55, false));
        Bidding.Close(sold, sold: true)!.Amount.Should().Be(65);
        (sold.Status, sold.WinningBid).Should().Be((AuctionItemStatus.Sold, 65m));

        var none = Item(50, (90, true));
        Bidding.Close(none, sold: true).Should().BeNull();
        none.Status.Should().Be(AuctionItemStatus.Passed);
    }

    [Test]
    public void Buy_now_only_for_a_passed_item()
    {
        var item = Item(50);
        item.Invoking(i => Bidding.BuyNow(i, "Bo", "12345678")).Should().Throw<InvalidOperationException>();
        item.Status = AuctionItemStatus.Passed;
        Bidding.BuyNow(item, "Bo", "12345678");
        item.Status.Should().Be(AuctionItemStatus.Sold);
    }

    [Test]
    public void Live_bids_are_checked_on_the_active_item_only()
    {
        var item = Item(100, (120, false));
        item.Invoking(i => Bidding.Check(AuctionStatus.Live, i, 500)).Should().Throw<InvalidOperationException>().WithMessage("Genstand er ikke aktiv");
        item.Status = AuctionItemStatus.Active;
        item.Invoking(i => Bidding.Check(AuctionStatus.Live, i, 120)).Should().Throw<InvalidOperationException>().WithMessage("Mindste bud er 121 kr");
        Bidding.Check(AuctionStatus.Live, item, 121).Should().BeFalse();
        Bidding.Check(AuctionStatus.PreAuction, Item(100), 100).Should().BeTrue();
    }

    [Test]
    public void Bought_now_is_told_apart_from_sold_to_a_bid()
    {
        var hammered = Item(50, (65, false));
        Bidding.Close(hammered, sold: true);
        hammered.IsBoughtNow.Should().BeFalse();

        var passed = Item(50);
        Bidding.Close(passed, sold: true);
        Bidding.BuyNow(passed, "Bo", "12345678");
        passed.IsBoughtNow.Should().BeTrue();
    }

    [Test]
    public void The_catalogue_is_in_running_order_and_interesting_means_marked_gold_first()
    {
        var session = new AuctionSession { Items =
        [
            new AuctionItem { Name = "a", SortOrder = 0 },
            new AuctionItem { Name = "b", SortOrder = 1, Highlight = "🥉 BRONZE – x" },
            new AuctionItem { Name = "c", SortOrder = 2, Highlight = "🏆 GULD – x" },
        ] };
        session.Catalogue().Select(i => i.Name).Should().Equal("a", "b", "c");
        session.Catalogue(onlyInteresting: true).Select(i => i.Name).Should().Equal("c", "b");
    }

    [Test]
    public void Marks_order_gold_silver_bronze_maybe()
    {
        new[] { "🔎 MÅSKE – x", "🥉 BRONZE – x", null, "🏆 GULD – x", "🥈 SØLV – x" }.OrderBy(Highlights.Rank)
            .Should().Equal("🏆 GULD – x", "🥈 SØLV – x", "🥉 BRONZE – x", "🔎 MÅSKE – x", null);
        Highlights.Badge("🥈 SØLV – sælges for ca. 260 kr andre steder").Should().Be("🥈 SØLV");
    }
}

public class LagerIntakeTests
{
    private SqliteConnection _conn = null!;
    private AuctionDbContext Db() => new(new DbContextOptionsBuilder<AuctionDbContext>().UseSqlite(_conn).Options);

    [SetUp]
    public async Task Open()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        await using var db = Db();
        await AuctionSchema.UpgradeAsync(db);
    }

    [TearDown]
    public void Close() => _conn.Dispose();

    [Test]
    public async Task An_item_from_Lager_keeps_its_mark_and_everyone_is_told()
    {
        await using var db = Db();
        var store = new AuctionStore(db);
        var hub = new Mock<IAuctionBroadcast>();
        var auctions = new AuctionUseCases(store, hub.Object, Mock.Of<ICountdown>(), Mock.Of<IPhotoStore>(), TimeProvider.System);
        var session = await auctions.CreateAsync("Bogshoppen fredag");
        var lager = new LagerUseCases(store, auctions, Mock.Of<IPhotoStore>());

        var added = await lager.AddAsync(new LagerItemIn(session.Code.ToLowerInvariant(), "Anders And & Co. nr. 17 1973", null, 100, 1, null,
            "🏆 GULD – sælges for ca. 1500 kr andre steder"));

        added.Should().NotBeNull();
        var full = await auctions.GetByCodeAsync(session.Code);
        full!.Items.Single().Highlight.Should().Be("🏆 GULD – sælges for ca. 1500 kr andre steder");
        hub.Verify(h => h.SendAsync(session.Code, "ItemAdded", It.IsAny<object>()), Times.Once);
        (await lager.OpenSessionsAsync()).Single().Items.Should().Be(1);
        (await lager.AddAsync(new LagerItemIn("NOPE00", "x", null, 1, 1, null))).Should().BeNull();
    }

    [Test]
    public async Task A_live_bid_below_the_minimum_is_refused()
    {
        await using var db = Db();
        var store = new AuctionStore(db);
        var auctions = new AuctionUseCases(store, Mock.Of<IAuctionBroadcast>(), Mock.Of<ICountdown>(), Mock.Of<IPhotoStore>(), Clock);
        var s = await auctions.CreateAsync("Test");
        var item = await auctions.AddItemAsync(s.Code, s.AdminToken, "Tintin 5", null, 100, 1, null);
        await auctions.StartLiveAsync(s.Code, s.AdminToken);
        await auctions.ActivateItemAsync(s.Code, s.AdminToken, item.Id);
        await auctions.GetOrCreateBidderAsync(s.Code, "phone-1");

        await auctions.Invoking(a => a.PlaceBidAsync(s.Code, item.Id, "phone-1", 90)).Should().ThrowAsync<InvalidOperationException>().WithMessage("Mindste bud er 100 kr");
        await auctions.PlaceBidAsync(s.Code, item.Id, "phone-1", 100);
        await auctions.CloseItemAsync(s.Code, s.AdminToken, item.Id, sold: true);
        (await lager(store, auctions).StatusAsync([item.Id])).Single().Should().Match<LagerStatusOut>(x => x.Status == "Sold" && x.WinningBid == 100);
    }

    [Test]
    public async Task Bids_and_sessions_get_the_time_from_the_clock_not_the_machine()
    {
        await using var db = Db();
        var store = new AuctionStore(db);
        var auctions = new AuctionUseCases(store, Mock.Of<IAuctionBroadcast>(), Mock.Of<ICountdown>(), Mock.Of<IPhotoStore>(), Clock);
        var s = await auctions.CreateAsync("Test");
        var item = await auctions.AddItemAsync(s.Code, s.AdminToken, "Tintin 5", null, 100, 1, null);
        await auctions.OpenPreAuctionAsync(s.Code, s.AdminToken);
        await auctions.GetOrCreateBidderAsync(s.Code, "phone-1");
        await auctions.PlaceBidAsync(s.Code, item.Id, "phone-1", 120);

        var full = await auctions.GetByCodeAsync(s.Code);
        (full!.CreatedAt, full.ExpiresAt).Should().Be((Then, Then.AddDays(7)));
        full.Items.Single().Bids.Single().PlacedAt.Should().Be(Then);
    }

    [Test]
    public async Task A_pre_bid_replaces_your_old_one_and_never_goes_under_the_starting_price()
    {
        await using var db = Db();
        var store = new AuctionStore(db);
        var auctions = new AuctionUseCases(store, Mock.Of<IAuctionBroadcast>(), Mock.Of<ICountdown>(), Mock.Of<IPhotoStore>(), Clock);
        var s = await auctions.CreateAsync("Test");
        var item = await auctions.AddItemAsync(s.Code, s.AdminToken, "Tintin 5", null, 100, 1, null);
        await auctions.OpenPreAuctionAsync(s.Code, s.AdminToken);
        await auctions.GetOrCreateBidderAsync(s.Code, "phone-1");

        await auctions.Invoking(a => a.PlaceBidAsync(s.Code, item.Id, "phone-1", 50)).Should().ThrowAsync<InvalidOperationException>().WithMessage("Forudbud skal være mindst 100 kr");
        await auctions.PlaceBidAsync(s.Code, item.Id, "phone-1", 120);
        await auctions.PlaceBidAsync(s.Code, item.Id, "phone-1", 150);

        (await auctions.GetByCodeAsync(s.Code))!.Items.Single().Bids.Should().ContainSingle().Which.Amount.Should().Be(150);
    }

    [Test]
    public async Task No_bids_after_the_auction_has_ended()
    {
        await using var db = Db();
        var store = new AuctionStore(db);
        var auctions = new AuctionUseCases(store, Mock.Of<IAuctionBroadcast>(), Mock.Of<ICountdown>(), Mock.Of<IPhotoStore>(), Clock);
        var s = await auctions.CreateAsync("Test");
        var item = await auctions.AddItemAsync(s.Code, s.AdminToken, "Tintin 5", null, 100, 1, null);
        await auctions.StartLiveAsync(s.Code, s.AdminToken);
        await auctions.ActivateItemAsync(s.Code, s.AdminToken, item.Id);
        await auctions.GetOrCreateBidderAsync(s.Code, "phone-1");
        await auctions.EndAuctionAsync(s.Code, s.AdminToken);

        await auctions.Invoking(a => a.PlaceBidAsync(s.Code, item.Id, "phone-1", 500)).Should().ThrowAsync<InvalidOperationException>().WithMessage("Genstand er ikke aktiv");
    }

    private static readonly DateTime Then = new(2026, 10, 31, 10, 0, 0, DateTimeKind.Utc);
    private static readonly TimeProvider Clock = new FixedClock(Then);
    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }

    private static LagerUseCases lager(IAuctionStore store, AuctionUseCases a) => new(store, a, Mock.Of<IPhotoStore>());
}

public class LayerTests
{
    private static string[] Refs(Type t) => t.Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

    [Test]
    public void Domain_has_no_outside_dependencies() =>
        Refs(typeof(AuctionItem)).Where(n => n.StartsWith("ITMartin") || n.StartsWith("Microsoft.EntityFramework") || n.StartsWith("Microsoft.AspNetCore"))
            .Should().BeEmpty();

    [Test]
    public void Application_depends_only_on_Domain() =>
        Refs(typeof(AuctionUseCases)).Where(n => n.StartsWith("ITMartin") || n.StartsWith("Microsoft.EntityFramework") || n.StartsWith("Microsoft.AspNetCore"))
            .Should().BeEquivalentTo(["ITMartinAuction.Domain"]);

    [Test]
    public void No_page_uses_the_database_or_files_directly()
    {
        var pages = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "ITMartinAuction.Server", "Pages");
        foreach (var file in Directory.GetFiles(pages, "*.razor"))
        {
            var text = File.ReadAllText(file);
            text.Should().NotContain("DbContext", file).And.NotContain("Infrastructure", file).And.NotContain("FileStream", file);
        }
    }
}
