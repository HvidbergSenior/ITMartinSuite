using FluentAssertions;
using ITMartinNotatskriver.Application;
using ITMartinNotatskriver.Domain;
using NUnit.Framework;

namespace ITMartinNotatskriver.Tests;

/// <summary>The use cases with fake adapters - no Claude calls, no files.</summary>
public class WriteDocumentTests
{
    private sealed class FakeWriter(string answer = "Færdigt dokument", bool truncated = false) : IDocumentWriter
    {
        public List<WriteRequest> Calls { get; } = [];
        public Exception? Throw { get; init; }

        public Task<WriterAnswer> WriteAsync(WriteRequest request, CancellationToken ct)
        {
            Calls.Add(request);
            if (Throw is not null) throw Throw;
            return Task.FromResult(new WriterAnswer(answer, truncated));
        }
    }

    private sealed class FakeBudget(bool ok = true) : IAiBudget
    {
        public int Taken { get; private set; }
        public bool TryTake(string what) { if (ok) Taken++; return ok; }
    }

    private sealed class BracketHider : INameHiderFactory
    {
        public INameHider Create() => new H();

        private sealed class H : INameHider
        {
            public string Hide(string text) => text.Replace("12 34 56 78", "[TELEFON1]");
            public string Restore(string text) => text.Replace("[TELEFON1]", "12 34 56 78");
        }
    }

    private static (WriteDocument Use, FakeWriter Writer, FakeBudget Budget) Make(FakeWriter? w = null, FakeBudget? b = null)
    {
        w ??= new FakeWriter();
        b ??= new FakeBudget();
        return (new WriteDocument(w, b, new BracketHider()), w, b);
    }

    private static NoteDraft Draft(string notes) => new(UsagePlan.Free) { Notes = notes };

    [Test]
    public async Task Writes_ordinary_notes()
    {
        var (use, writer, budget) = Make();
        var r = await use.ExecuteAsync(Draft("møde om printer"), hideNames: false, default);
        r.Should().BeOfType<WriteOutcome.Written>().Which.Text.Should().Be("Færdigt dokument");
        writer.Calls.Should().ContainSingle();
        budget.Taken.Should().Be(1);
    }

    [Test]
    public async Task Patient_notes_never_reach_the_AI_not_even_with_names_hidden()
    {
        foreach (var hide in new[] { false, true })
        {
            var (use, writer, budget) = Make();
            var r = await use.ExecuteAsync(Draft("Patienten har smerter, fik panodil"), hide, default);
            r.Should().BeOfType<WriteOutcome.Stopped>().Which.Hard.Should().BeTrue();
            writer.Calls.Should().BeEmpty();
            budget.Taken.Should().Be(0, "no AI call was spent");
        }
    }

    [Test]
    public async Task Personal_details_in_a_text_file_also_stop_it()
    {
        var (use, writer, _) = Make();
        var d = new NoteDraft(UsagePlan.Extended) { Notes = "se filen" };
        d.Add(Attachment.FromText("f.txt", "ring 12 34 56 78"));
        var r = await use.ExecuteAsync(d, false, default);
        r.Should().BeOfType<WriteOutcome.Stopped>().Which.Hard.Should().BeFalse();
        writer.Calls.Should().BeEmpty();
    }

    [Test]
    public async Task Hidden_names_are_replaced_before_sending_and_put_back_after()
    {
        var (use, writer, _) = Make(new FakeWriter("Ring til [TELEFON1] i morgen"));
        var r = await use.ExecuteAsync(Draft("husk at ringe 12 34 56 78"), hideNames: true, default);
        writer.Calls.Single().Notes.Should().NotContain("12 34 56 78").And.Contain("[TELEFON1]");
        r.Should().BeOfType<WriteOutcome.Written>().Which.Text.Should().Be("Ring til 12 34 56 78 i morgen");
    }

    [Test]
    public async Task The_AIs_own_block_is_honoured()
    {
        var (use, _, _) = Make(new FakeWriter("BLOKERET"));
        var r = await use.ExecuteAsync(Draft("se billedet"), false, default);
        r.Should().BeOfType<WriteOutcome.Stopped>().Which.Message.Should().Be(WriteDocument.BlockedByAiMessage);
    }

    [Test]
    public async Task No_AI_call_when_the_daily_budget_is_spent()
    {
        var (use, writer, _) = Make(b: new FakeBudget(ok: false));
        var r = await use.ExecuteAsync(Draft("noter"), false, default);
        r.Should().BeOfType<WriteOutcome.Failed>().Which.Message.Should().Be(WriteDocument.QuotaMessage);
        writer.Calls.Should().BeEmpty();
    }

    [Test]
    public async Task Unconfirmed_pictures_are_never_sent()
    {
        var (use, writer, _) = Make();
        var d = Draft("");
        d.Add(Attachment.Image("s.png", [1]));
        (await use.ExecuteAsync(d, false, default)).Should().BeOfType<WriteOutcome.Failed>();
        writer.Calls.Should().BeEmpty();
    }

    [Test]
    public async Task A_too_long_document_says_so()
    {
        var (use, _, _) = Make(new FakeWriter("Lang tekst", truncated: true));
        var r = await use.ExecuteAsync(Draft("noter"), false, default);
        r.Should().BeOfType<WriteOutcome.Written>().Which.Text.Should().EndWith(WrittenDocument.TruncatedNote);
    }

    [Test]
    public async Task Service_problems_become_a_Danish_message()
    {
        var (use, _, _) = Make(new FakeWriter { Throw = new NotatskriverException("Der er travlt lige nu.") });
        var r = await use.ExecuteAsync(Draft("noter"), false, default);
        r.Should().BeOfType<WriteOutcome.Failed>().Which.Message.Should().Be("Der er travlt lige nu.");
    }
}

public class AddAttachmentTests
{
    private sealed class CountingReader : IAttachmentReader
    {
        public int Reads { get; private set; }

        public Task<Attachment> ReadAsync(string fileName, string contentType, Stream content, CancellationToken ct)
        {
            Reads++;
            return Task.FromResult(Attachment.Image(fileName, [1]));
        }
    }

    private static Stream Empty() => new MemoryStream([1]);

    [Test]
    public async Task A_screenshot_is_added_in_the_free_version()
    {
        var reader = new CountingReader();
        var d = new NoteDraft(UsagePlan.Free);
        (await new AddAttachment(reader).ExecuteAsync(d, "skærm.png", "image/png", 10, Empty, default)).Should().BeNull();
        d.Attachments.Should().ContainSingle();
    }

    [Test]
    public async Task A_pdf_in_the_free_version_is_refused_without_being_opened()
    {
        var reader = new CountingReader();
        var why = await new AddAttachment(reader).ExecuteAsync(new NoteDraft(UsagePlan.Free), "x.pdf", "application/pdf", 10, Empty, default);
        why.Should().Contain("kun billeder");
        reader.Reads.Should().Be(0);
    }

    [Test]
    public async Task Too_big_files_are_refused_without_being_opened()
    {
        var reader = new CountingReader();
        var why = await new AddAttachment(reader).ExecuteAsync(new NoteDraft(UsagePlan.Free), "big.jpg", "image/jpeg", AddAttachment.MaxBytes + 1, Empty, default);
        why.Should().Contain("for stor");
        reader.Reads.Should().Be(0);
    }

    [Test]
    public async Task Unknown_files_are_refused() =>
        (await new AddAttachment(new CountingReader()).ExecuteAsync(new NoteDraft(UsagePlan.Extended), "a.exe", "", 10, Empty, default))
        .Should().Contain("filtype");
}
