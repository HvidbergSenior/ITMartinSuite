using FluentAssertions;
using ITMartinUpload.Server.Services;

namespace ITMartinUpload.Tests;

public class UploadStoreTests
{
    private string _root = "";
    private UploadStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "upload-tests", Guid.NewGuid().ToString("N"));
        _store = new UploadStore(new UploadOptions { Root = _root });
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [TestCase("annette-kent", true)]
    [TestCase("annette_kent2", true)]
    [TestCase("", false)]
    [TestCase("../etc", false)]
    [TestCase("annette kent", false)]
    [TestCase("annette/kent", false)]
    public void IsValidSlug_accepts_only_safe_names(string slug, bool expected) =>
        UploadStore.IsValidSlug(slug).Should().Be(expected);

    [Test]
    public void IsValidSlug_rejects_overlong_names() =>
        UploadStore.IsValidSlug(new string('a', 51)).Should().BeFalse();

    [TestCase("ferie.jpg", "ferie.jpg")]
    [TestCase(@"C:\Users\Annette\ferie.jpg", "ferie.jpg")]
    [TestCase("/home/kent/ferie.jpg", "ferie.jpg")]
    [TestCase("../../ferie.jpg", "ferie.jpg")]
    public void SafeFileName_strips_any_path(string sent, string expected) =>
        UploadStore.SafeFileName(sent).Should().Be(expected);

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("..")]
    [TestCase(null)]
    public void SafeFileName_returns_null_when_nothing_usable_is_left(string? sent) =>
        UploadStore.SafeFileName(sent).Should().BeNull();

    [Test]
    public void UniquePath_keeps_the_name_when_the_folder_is_empty() =>
        UploadStore.UniquePath("/f", "ferie.jpg", _ => false)
            .Should().Be(Path.Combine("/f", "ferie.jpg"));

    [Test]
    public void UniquePath_never_overwrites_what_the_customer_already_sent()
    {
        var taken = new HashSet<string>
        {
            Path.Combine("/f", "ferie.jpg"),
            Path.Combine("/f", "ferie_1.jpg"),
        };

        UploadStore.UniquePath("/f", "ferie.jpg", taken.Contains)
            .Should().Be(Path.Combine("/f", "ferie_2.jpg"));
    }

    [Test]
    public async Task SaveAsync_writes_the_file_into_the_customer_folder()
    {
        await SaveAsync("annette-kent", "ferie.jpg", "hej");

        var written = Path.Combine(_root, "annette-kent", "ferie.jpg");
        File.Exists(written).Should().BeTrue();
        (await File.ReadAllTextAsync(written)).Should().Be("hej");
    }

    [Test]
    public async Task SaveAsync_lowercases_the_folder_so_one_customer_has_one_folder()
    {
        await SaveAsync("Annette-Kent", "a.jpg", "1");
        await SaveAsync("annette-kent", "b.jpg", "2");

        Directory.GetDirectories(_root).Should().ContainSingle()
            .Which.Should().EndWith("annette-kent");
    }

    [Test]
    public async Task SaveAsync_keeps_both_files_when_the_same_name_arrives_twice()
    {
        await SaveAsync("annette-kent", "ferie.jpg", "first");
        await SaveAsync("annette-kent", "ferie.jpg", "second");

        var folder = Path.Combine(_root, "annette-kent");
        Directory.GetFiles(folder).Should().HaveCount(2);
        (await File.ReadAllTextAsync(Path.Combine(folder, "ferie.jpg"))).Should().Be("first");
        (await File.ReadAllTextAsync(Path.Combine(folder, "ferie_1.jpg"))).Should().Be("second");
    }

    [Test]
    public async Task List_returns_newest_first_with_sizes()
    {
        await SaveAsync("annette-kent", "old.jpg", "12345");
        await SaveAsync("annette-kent", "new.jpg", "1");
        File.SetLastWriteTimeUtc(Path.Combine(_root, "annette-kent", "old.jpg"),
            DateTime.UtcNow.AddDays(-2));

        var files = _store.List("annette-kent");

        files.Select(f => f.Name).Should().Equal("new.jpg", "old.jpg");
        files.Single(f => f.Name == "old.jpg").Size.Should().Be(5);
    }

    [Test]
    public void List_is_empty_for_a_customer_who_has_not_sent_anything_yet() =>
        _store.List("nobody").Should().BeEmpty();

    private async Task SaveAsync(string slug, string name, string content)
    {
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        await _store.SaveAsync(slug, UploadStore.SafeFileName(name)!, ms);
    }
}
