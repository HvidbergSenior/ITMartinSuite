using FluentAssertions;
using ITMartinLaes.Application;
using ITMartinLaes.Domain;
using Moq;

namespace ITMartinLaes.Tests;

public class SummaryPlanTests
{
    [Test]
    public void A_288_page_book_is_15_parts_of_20_pages_and_one_summary_call()
    {
        var parts = SummaryPlan.Parts(288);
        parts.Should().HaveCount(15);
        parts[0].Should().Be((1, 20));
        parts[^1].Should().Be((281, 288));
    }

    [Test]
    public void A_very_long_book_gets_bigger_parts_never_more_calls_than_the_cap()
    {
        var parts = SummaryPlan.Parts(2000);
        parts.Count.Should().BeLessThan(SummaryPlan.MaxCallsPerBook);
        parts[^1].To.Should().Be(2000);
    }

    [TestCase("Business Model Generation (1).pdf", "Business Model Generation")]
    [TestCase("min_bog.pdf", "min bog")]
    [TestCase(".pdf", "Bog")]
    public void The_title_comes_from_the_file_name(string file, string title) => Book.TitleFromFileName(file).Should().Be(title);

    [Test]
    public void The_reader_never_goes_past_the_last_page()
    {
        var b = new Book { Pages = 10 };
        b.TurnTo(99);
        b.LastPage.Should().Be(10);
        b.TurnTo(-3);
        b.LastPage.Should().Be(1);
    }
}

public class SummariesTests
{
    private sealed class MemoryBooks : IBookRepository
    {
        public readonly Dictionary<Guid, Book> All = [];
        public Task AddAsync(Book book, CancellationToken ct = default) { All[book.Id] = book; return Task.CompletedTask; }
        public Task<Book?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(All.GetValueOrDefault(id));
        public Task<List<Book>> AllAsync(CancellationToken ct = default) => Task.FromResult(All.Values.ToList());
        public Task UpdateAsync(Book book, CancellationToken ct = default) { All[book.Id] = book; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { All.Remove(id); return Task.CompletedTask; }
        public Task<Book?> NextToSummariseAsync(CancellationToken ct = default) =>
            Task.FromResult(All.Values.FirstOrDefault(b => b.State is SummaryState.Venter or SummaryState.Læser));
    }

    [Test]
    public async Task One_call_per_part_then_one_summary_call_and_the_book_is_ready()
    {
        var books = new MemoryBooks();
        var book = new Book { Title = "Test", FileName = "a.pdf", Pages = 45 };
        await books.AddAsync(book);
        var files = new Mock<IBookFiles>();
        files.Setup(f => f.PathOf("a.pdf")).Returns("/data/a.pdf");
        var text = new Mock<IPdfText>();
        text.Setup(t => t.Pages(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>())).Returns(["tekst"]);
        var ai = new Mock<IBookSummarizer>();
        ai.SetupGet(a => a.Enabled).Returns(true);
        ai.SetupGet(a => a.MaxCallsPerDay).Returns(100);
        ai.Setup(a => a.ReadPartAsync("Test", It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, int from, int to, IReadOnlyList<string> _, CancellationToken _) =>
                new PartNote(from, to, "noter", from == 1 ? [new Chapter("Kapitel 1", 3, "")] : []));
        ai.Setup(a => a.SummariseAsync("Test", 45, It.IsAny<IReadOnlyList<PartNote>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("Resumé", ["Pointe"], [new Chapter("Kapitel 1", 3, "Om noget"), new Chapter("Uden for bogen", 99, "")]));
        var summaries = new Summaries(books, files.Object, text.Object, ai.Object);

        for (var i = 0; i < 10; i++) await summaries.WorkAsync();

        var done = books.All[book.Id];
        done.State.Should().Be(SummaryState.Klar);
        done.Calls.Should().Be(4);   // 3 parts of 20/20/5 pages + the summary
        done.Summary.Should().Be("Resumé");
        done.Chapters.Should().ContainSingle().Which.Page.Should().Be(3);   // page 99 is not in a 45-page book
        ai.Verify(a => a.ReadPartAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        ai.Verify(a => a.SummariseAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<PartNote>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Nothing_is_called_when_the_day_cap_is_reached()
    {
        var books = new MemoryBooks();
        await books.AddAsync(new Book { Title = "Test", FileName = "a.pdf", Pages = 45 });
        var ai = new Mock<IBookSummarizer>();
        ai.SetupGet(a => a.Enabled).Returns(true);
        ai.SetupGet(a => a.MaxCallsPerDay).Returns(5);
        ai.Setup(a => a.CallsTodayAsync(It.IsAny<CancellationToken>())).ReturnsAsync(5);
        var summaries = new Summaries(books, Mock.Of<IBookFiles>(), Mock.Of<IPdfText>(), ai.Object);

        (await summaries.WorkAsync()).Should().BeFalse();
        ai.Verify(a => a.ReadPartAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
