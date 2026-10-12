using FluentAssertions;
using ITMartinAdhd.Application;
using ITMartinAdhd.Domain.Entities;
using ITMartinAdhd.Infrastructure;
using ITMartinAdhd.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ITMartinAdhd.Tests;

// Runs the real EF store and photo folder on an in-memory SQLite database and a temp folder.
public class ThingsTests
{
    private static readonly DateTime T0 = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);

    private sealed class Clock(DateTime now) : TimeProvider
    {
        public DateTime Now = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private SqliteConnection _conn = null!;
    private AdhdDbContext _db = null!;
    private string _dir = "";
    private Clock _clock = null!;
    private Things _things = null!;

    [SetUp]
    public void SetUp()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        _db = new AdhdDbContext(new DbContextOptionsBuilder<AdhdDbContext>().UseSqlite(_conn).Options);
        _db.Database.Migrate();   // the real migrations - proves the entity still matches the database
        _dir = Path.Combine(Path.GetTempPath(), "findit-test-" + Guid.NewGuid().ToString("N"));
        _clock = new Clock(T0);
        _things = new Things(new EfItemStore(_db), new FilePhotoStore(_dir), _clock);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        _conn.Dispose();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private static MemoryStream Jpeg() => new([0xFF, 0xD8, 0xFF, 0xD9]);

    [Test]
    public void A_thing_needs_a_name_and_a_place()
    {
        StoredItem.Create("Nøgler", " ", null, null, T0).Should().BeNull();
        StoredItem.Create("", "Skål", null, null, T0).Should().BeNull();
        var item = StoredItem.Create("  Nøgler ", "Skål", "  ", null, T0)!;
        item.Name.Should().Be("Nøgler");
        item.Notes.Should().BeNull();
        StoredItem.Create(new string('x', 300), "Skål", null, null, T0)!.Name.Should().HaveLength(StoredItem.MaxName);
    }

    [Test]
    public async Task Search_needs_every_word_somewhere_and_ignores_case()
    {
        await _things.PutDownAsync("Bilnøgler", "Krog i gangen", null, null, null);
        await _things.PutDownAsync("Nøgler til cykel", "Skuffe", "den røde lås", null, null);
        (await _things.SearchAsync("NØGLER")).Should().HaveCount(2);
        (await _things.SearchAsync("nøgler skuffe")).Single().Name.Should().Be("Nøgler til cykel");
        (await _things.SearchAsync("røde")).Should().ContainSingle();
        (await _things.SearchAsync("  ")).Should().BeEmpty();
    }

    [Test]
    public async Task Photos_are_kept_only_for_photo_types_and_go_with_the_thing()
    {
        var with = (await _things.PutDownAsync("Pas", "Skuffe", null, Jpeg(), "IMG_1.JPG"))!;
        var odd = (await _things.PutDownAsync("Kort", "Pung", null, Jpeg(), "virus.exe"))!;
        with.HasPhoto.Should().BeTrue();
        odd.HasPhoto.Should().BeFalse();
        var file = await _things.PhotoFileAsync(with.Id);
        File.Exists(file).Should().BeTrue();
        (await _things.DeleteAsync(with.Id)).Should().BeTrue();
        File.Exists(file).Should().BeFalse();
        (await _things.DeleteAsync(with.Id)).Should().BeFalse();
    }

    [Test]
    public async Task Moving_a_thing_puts_it_first_again()
    {
        var a = (await _things.PutDownAsync("Briller", "Natbord", null, null, null))!;
        _clock.Now = T0.AddMinutes(5);
        await _things.PutDownAsync("Oplader", "Køkken", null, null, null);
        _clock.Now = T0.AddMinutes(10);
        (await _things.MoveAsync(a.Id, "")).Should().BeFalse();
        (await _things.MoveAsync(a.Id, "Sofabord")).Should().BeTrue();
        var recent = await _things.RecentAsync();
        recent[0].Location.Should().Be("Sofabord");
    }

    [Test]
    public async Task Things_untouched_for_30_days_are_cleared_with_their_photo()
    {
        var old = (await _things.PutDownAsync("Gammel", "Loft", null, Jpeg(), "a.jpg"))!;
        var file = await _things.PhotoFileAsync(old.Id);
        _clock.Now = T0.AddDays(20);
        await _things.PutDownAsync("Ny", "Gang", null, null, null);
        _clock.Now = T0.AddDays(31);
        (await _things.ClearStaleAsync()).Should().Be(1);
        (await _things.RecentAsync()).Single().Name.Should().Be("Ny");
        File.Exists(file).Should().BeFalse();
    }

    [Test]
    public void Photo_names_from_old_rows_and_odd_paths()
    {
        var store = new FilePhotoStore(_dir);
        File.WriteAllBytes(Path.Combine(_dir, "12.jpg"), [1]);
        store.PathOf("/photos/12.jpg").Should().NotBeNull();   // rows from the first version
        store.PathOf("../../etc/passwd").Should().BeNull();
        store.PathOf(null).Should().BeNull();
    }
}
