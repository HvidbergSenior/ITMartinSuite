using FluentAssertions;
using ITMartinVareinfo.Application;
using ITMartinVareinfo.Domain;
using ITMartinVareinfo.Infrastructure.External;
using ITMartinVareinfo.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ITMartinVareinfo.Tests;

public sealed class TestDbFactory(SqliteConnection conn) : IDbContextFactory<VareinfoDb>
{
    public VareinfoDb CreateDbContext() => new(new DbContextOptionsBuilder<VareinfoDb>().UseSqlite(conn).Options);
}

public class ReadingTests
{
    private SqliteConnection _conn = null!;
    private TestDbFactory Factory => new(_conn);

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

    [Test]
    public async Task Waiting_kolofons_are_read_ten_per_ai_call_and_sent_to_Lager()
    {
        var shots = new KolofonShotRepository(Factory);
        for (var n = 0; n < 12; n++) await shots.AddAsync(new KolofonShot { ItemId = 100 + n, Title = $"Bog {n}", Path = $"photos/kolofon/{n}.jpg" }, default);
        var photos = new Mock<IPhotoStore>();
        photos.Setup(p => p.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([1, 2, 3]);
        var reader = new Mock<ICoverReader>();
        reader.SetupGet(r => r.Enabled).Returns(true);
        reader.Setup(r => r.ReadKolofonsAsync(It.IsAny<IReadOnlyList<byte[]>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<byte[]> j, CancellationToken _) =>
                Enumerable.Range(0, j.Count).ToDictionary(i => i, _ => new KolofonReading("9780552131063", "1. udgave", 1987, "Gyldendal", null)));
        var lager = new Mock<ILagerGateway>();
        var kolofon = new KolofonUseCases(shots, photos.Object, reader.Object, lager.Object, TimeProvider.System);

        (await kolofon.ReadWaitingAsync()).Should().Be(10);
        (await kolofon.ReadWaitingAsync()).Should().Be(2);
        (await kolofon.ReadWaitingAsync()).Should().Be(0);
        reader.Verify(r => r.ReadKolofonsAsync(It.Is<IReadOnlyList<byte[]>>(j => j.Count <= KolofonShot.PerCall), It.IsAny<CancellationToken>()), Times.Exactly(2));
        lager.Verify(l => l.KolofonReadAsync(It.IsAny<int>(), It.Is<KolofonReading?>(r => r!.Edition == "1. udgave"), It.IsAny<CancellationToken>()), Times.Exactly(12));
        (await kolofon.WaitingAsync()).Should().Be(0);
        (await kolofon.RecentlyReadAsync()).First().Result.Should().Contain("1. udgave");
    }

    [Test]
    public async Task A_kolofon_photo_is_kept_here_and_Lager_is_told()
    {
        var shots = new KolofonShotRepository(Factory);
        var photos = new Mock<IPhotoStore>();
        photos.Setup(p => p.ShrinkAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ReturnsAsync([1]);
        var lager = new Mock<ILagerGateway>();
        var kolofon = new KolofonUseCases(shots, photos.Object, Mock.Of<ICoverReader>(), lager.Object, TimeProvider.System);

        await kolofon.SavePhotoAsync(7, "Mort", new MemoryStream([1, 2]));

        lager.Verify(l => l.KolofonTakenAsync(7, It.IsAny<CancellationToken>()), Times.Once);
        (await kolofon.WaitingAsync()).Should().Be(1);
        (await shots.LatestForAsync(7, default))!.Title.Should().Be("Mort");
    }
}

public class LayerTests
{
    private static string[] Refs(Type t) => t.Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

    [Test]
    public void Domain_has_no_outside_dependencies() =>
        Refs(typeof(Found)).Where(n => n.StartsWith("ITMartin") || n.StartsWith("Microsoft.EntityFramework") || n.StartsWith("Anthropic"))
            .Should().BeEmpty();

    [Test]
    public void Application_depends_only_on_Domain() =>
        Refs(typeof(ReadingUseCases)).Where(n => n.StartsWith("ITMartin") || n.StartsWith("Microsoft.EntityFramework") || n.StartsWith("Anthropic"))
            .Should().BeEquivalentTo(["ITMartinVareinfo.Domain"]);

    [Test]
    public void No_page_uses_the_database_or_infrastructure()
    {
        var pages = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "ITMartinVareinfo.Server", "Pages");
        foreach (var file in Directory.GetFiles(pages, "*.razor"))
        {
            var text = File.ReadAllText(file);
            text.Should().NotContain("DbContext", file).And.NotContain("VareinfoDb", file).And.NotContain("Infrastructure", file);
        }
    }

    [Test]
    public void Vareinfo_never_references_Lager_code() =>
        new[] { typeof(Found), typeof(ReadingUseCases), typeof(LagerLink) }.SelectMany(Refs).Should().NotContain(n => n.StartsWith("ITMartinLager"));
}
