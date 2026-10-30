using FluentAssertions;
using ITMartinHjem.Application;
using ITMartinHjem.Domain;
using ITMartinHjem.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ITMartinHjem.Tests;

// Samarbejde (2026-10-30): /per/tekster + /per/musik - files both can upload, thoughts under each, Martin hears about Per's.
public class CollabTests
{
    private static readonly DateTime Now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    private sealed class RecordingInbox : IOwnerInbox
    {
        public List<string> Texts = [];
        public Task NewRequestAsync(string name, string email, string text, CancellationToken ct = default) { Texts.Add(name + ": " + text); return Task.CompletedTask; }
    }

    private sealed class MemoryDisk : ICollabDisk
    {
        public Dictionary<string, byte[]> Files = [];
        public async Task<string> SaveAsync(string space, Stream input, string fileName, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            await input.CopyToAsync(ms, ct);
            var name = Guid.NewGuid().ToString("N") + Path.GetExtension(fileName);
            Files[space + "/" + name] = ms.ToArray();
            return name;
        }
        public string? Find(string space, string storedName) => Files.ContainsKey(space + "/" + storedName) ? "/x/" + storedName : null;
        public void Delete(string space, string storedName) => Files.Remove(space + "/" + storedName);
    }

    private sealed class TestDbFactory(DbContextOptions<HjemQuestionsDb> o) : IDbContextFactory<HjemQuestionsDb>
    {
        public HjemQuestionsDb CreateDbContext() => new(o);
    }

    private sealed class FixedClock : TimeProvider
    {
        public DateTime At = Now;
        public override DateTimeOffset GetUtcNow() => At;
    }

    private static async Task<(CollabUseCases Collab, RecordingInbox Inbox, MemoryDisk Disk, FixedClock Clock, SqliteConnection Conn)> NewAsync()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var o = new DbContextOptionsBuilder<HjemQuestionsDb>().UseSqlite(conn).Options;
        await using (var db = new HjemQuestionsDb(o)) await HjemQuestionsDb.EnsureTablesAsync(db);
        var inbox = new RecordingInbox();
        var disk = new MemoryDisk();
        var clock = new FixedClock();
        return (new CollabUseCases(new CollabStore(new TestDbFactory(o)), disk, inbox, clock), inbox, disk, clock, conn);
    }

    [Test]
    public void Rooms_take_only_their_own_kind_of_file()
    {
        CollabRules.CheckFile(CollabRules.Texts, "Af alle farerne der lurer.docx", 50_000).Should().BeNull();
        CollabRules.CheckFile(CollabRules.Texts, "sang.mp3", 50_000).Should().Contain("Tekster tager kun");
        CollabRules.CheckFile(CollabRules.Music, "sang.mp3", 95_000_000).Should().Contain("for stor");
        CollabRules.CheckFile(CollabRules.Music, "sang.m4a", 0).Should().Be("Vælg en fil først.");
        CollabRules.CheckFile("billeder", "a.jpg", 10).Should().Be("Det rum findes ikke.");
    }

    [Test]
    public void Only_the_two_room_pages_get_a_board_and_the_code_ignores_case()
    {
        CollabRules.Split("per/tekster").Should().Be(("per", "tekster"));
        CollabRules.Split("/Per/Musik/").Should().Be(("per", "musik"));
        CollabRules.Split("per").Should().BeNull();
        CollabRules.Split("ydelser/cloud").Should().BeNull();

        var per = new CollabSpace("per", "Per", "Guitar26");
        CollabRules.CodeFits(per, " guitar26 ").Should().BeTrue();
        CollabRules.CodeFits(per, "guitar").Should().BeFalse();
        CollabRules.CodeFits(per with { Code = "" }, "").Should().BeFalse();
        new CollabSpaces([per, new CollabSpace("ingen", "Ingen", "")]).Find("ingen").Should().BeNull();
    }

    [Test]
    public void Title_falls_back_to_the_file_name()
    {
        CollabRules.TitleFrom("", "Af alle farerne der lurer.docx").Should().Be("Af alle farerne der lurer");
        CollabRules.TitleFrom("  Revideret  ", "x.docx").Should().Be("Revideret");
    }

    [Test]
    public async Task Per_uploads_and_writes_Martin_is_told_but_not_about_his_own()
    {
        var (collab, inbox, disk, clock, conn) = await NewAsync();
        await using var _ = conn;

        (await collab.UploadAsync("per", "tekster", "", "Per", new MemoryStream([1, 2, 3]), "Af alle farerne der lurer.docx", "application/msword", 3))
            .Should().BeNull();
        inbox.Texts.Should().ContainSingle().Which.Should().Contain("\"Af alle farerne der lurer\" på /per/tekster");

        var file = (await collab.ItemsAsync("per", "tekster")).Single().File;
        clock.At = Now.AddMinutes(5);
        (await collab.WriteAsync(file.Id, "Martin", "Stærk slutning!")).Should().BeNull();
        clock.At = Now.AddMinutes(10);
        (await collab.WriteAsync(file.Id, "Per", "Tak – jeg korter det ned.")).Should().BeNull();
        (await collab.WriteAsync(file.Id, "Per", "   ")).Should().Be("Skriv din tanke først.");
        inbox.Texts.Should().HaveCount(2);   // Per's upload + Per's thought, not Martin's

        var item = (await collab.ItemsAsync("per", "tekster")).Single();
        item.Notes.Select(n => n.By).Should().Equal("Martin", "Per");
        (await collab.ItemsAsync("per", "musik")).Should().BeEmpty();
        (await collab.OpenAsync(file.Id, "anden")).Should().BeNull();   // never across spaces

        await collab.DeleteAsync(file.Id);
        (await collab.ItemsAsync("per", "tekster")).Should().BeEmpty();
        disk.Files.Should().BeEmpty();
    }

    [Test]
    public async Task A_wrong_file_is_refused_before_anything_is_saved()
    {
        var (collab, inbox, disk, _, conn) = await NewAsync();
        await using var _c = conn;
        (await collab.UploadAsync("per", "musik", "", "Per", new MemoryStream([1]), "noter.docx", "", 1)).Should().Contain("Musik tager kun");
        disk.Files.Should().BeEmpty();
        inbox.Texts.Should().BeEmpty();
    }
}
