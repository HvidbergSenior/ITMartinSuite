using FluentAssertions;
using ITMartinNotatskriver.Domain;
using NUnit.Framework;

namespace ITMartinNotatskriver.Tests;

public class UsagePlanTests
{
    [Test]
    public void Free_takes_three_images_and_no_more()
    {
        UsagePlan.Free.WhyNot(AttachmentKind.Image, 0).Should().BeNull();
        UsagePlan.Free.WhyNot(AttachmentKind.Image, 2).Should().BeNull();
        UsagePlan.Free.WhyNot(AttachmentKind.Image, 3).Should().Contain("højst 3 billeder");
    }

    [TestCase(AttachmentKind.Pdf)]
    [TestCase(AttachmentKind.Text)]
    public void Free_refuses_files(AttachmentKind kind) =>
        UsagePlan.Free.WhyNot(kind, 0).Should().Contain("kun billeder og skærmbilleder");

    [Test]
    public void Free_has_no_dictation_extended_has() =>
        (UsagePlan.Free.Dictation, UsagePlan.Extended.Dictation).Should().Be((false, true));

    [Test]
    public void Extended_takes_files_up_to_ten()
    {
        UsagePlan.Extended.WhyNot(AttachmentKind.Pdf, 9).Should().BeNull();
        UsagePlan.Extended.WhyNot(AttachmentKind.Text, 10).Should().Contain("Højst 10");
    }
}

public class AttachmentKindsTests
{
    [TestCase("skærm.png", "image/png", AttachmentKind.Image)]
    [TestCase("IMG_1.HEIC", "", AttachmentKind.Image)]
    [TestCase("rapport.pdf", "application/octet-stream", AttachmentKind.Pdf)]
    [TestCase("brev.docx", "", AttachmentKind.Text)]
    [TestCase("noter.txt", "text/plain", AttachmentKind.Text)]
    public void Detects_the_kind(string name, string type, AttachmentKind expected) =>
        AttachmentKinds.Detect(name, type).Should().Be(expected);

    [Test]
    public void Unknown_files_are_refused() => AttachmentKinds.Detect("program.exe", "application/x-msdownload").Should().BeNull();
}

public class NoteDraftTests
{
    private static Attachment Img(string n = "a.jpg") => Attachment.Image(n, [1, 2, 3]);

    [Test]
    public void Empty_draft_can_not_be_written() => new NoteDraft(UsagePlan.Free).CanWrite.Should().BeFalse();

    [Test]
    public void Notes_alone_can_be_written() => new NoteDraft(UsagePlan.Free) { Notes = "møde med Bo" }.CanWrite.Should().BeTrue();

    [Test]
    public void Pictures_must_be_confirmed_before_writing()
    {
        var d = new NoteDraft(UsagePlan.Free);
        d.Add(Img());
        d.CanWrite.Should().BeFalse();
        d.ConfirmVisuals(true);
        d.CanWrite.Should().BeTrue();
    }

    [Test]
    public void A_new_picture_needs_a_new_confirmation()
    {
        var d = new NoteDraft(UsagePlan.Free);
        d.Add(Img());
        d.ConfirmVisuals(true);
        d.Add(Img("b.jpg"));
        d.VisualsConfirmed.Should().BeFalse();
    }

    [Test]
    public void The_plan_is_enforced_by_the_draft_itself()
    {
        var d = new NoteDraft(UsagePlan.Free);
        d.Add(Img("1")); d.Add(Img("2")); d.Add(Img("3"));
        d.IsFull.Should().BeTrue();
        var add = () => d.Add(Img("4"));
        add.Should().Throw<NotatskriverException>().WithMessage("*højst 3*");
        var pdf = () => new NoteDraft(UsagePlan.Free).Add(Attachment.Pdf("x.pdf", [1]));
        pdf.Should().Throw<NotatskriverException>();
    }

    [Test]
    public void All_text_includes_text_files_but_not_pictures()
    {
        var d = new NoteDraft(UsagePlan.Extended) { Notes = "egne noter" };
        d.Add(Attachment.FromText("f.txt", "fra filen"));
        d.Add(Img());
        d.AllText.Should().Contain("fra filen").And.Contain("egne noter");
    }

    [Test]
    public void Clear_starts_over()
    {
        var d = new NoteDraft(UsagePlan.Free) { Notes = "x", Extra = "y" };
        d.Add(Img());
        d.Clear();
        (d.Notes, d.Extra, d.Attachments.Count, d.CanWrite).Should().Be(("", "", 0, false));
    }
}

public class PersonalDataTests
{
    [TestCase("ring til 12 34 56 78", "Telefonnummer")]
    [TestCase("cpr 010190-1234", "CPR-nummer")]
    [TestCase("skriv til bo@eksempel.dk", "E-mail")]
    [TestCase("bor på Skovvej 12", "Adresse")]
    public void Finds_personal_details(string text, string what) =>
        PersonalData.Find(text).Select(f => f.What).Should().Contain(what);

    [Test]
    public void Two_health_words_mean_patient_notes()
    {
        var f = PersonalData.Find("Patienten har smerter og får panodil");
        PersonalData.LooksLikePatientNotes(f).Should().BeTrue();
    }

    [Test]
    public void One_health_word_is_not_patient_notes() =>
        PersonalData.LooksLikePatientNotes(PersonalData.Find("Jeg skal til lægen på fredag")).Should().BeFalse();

    [Test]
    public void Ordinary_meeting_notes_are_clean() =>
        PersonalData.Find("Møde om budget 2027 – ny printer ok, max 3000").Should().BeEmpty();
}

public class WrittenDocumentTests
{
    [TestCase("BLOKERET", true)]
    [TestCase(" BLOKERET. ", true)]
    [TestCase("Mødereferat\nBLOKERET ved døren", false)]
    public void Recognises_the_AI_block(string answer, bool blocked) => WrittenDocument.IsBlocked(answer).Should().Be(blocked);

    [Test]
    public void Short_lines_after_a_blank_line_are_headings()
    {
        string[] lines = ["Mødereferat", "", "Beslutninger", "Vi køber en printer.", "- Lis tjekker tilbud"];
        WrittenDocument.IsHeading(lines, 0).Should().BeTrue();
        WrittenDocument.IsHeading(lines, 2).Should().BeTrue();
        WrittenDocument.IsHeading(lines, 3).Should().BeFalse("it follows text and ends with a full stop");
        WrittenDocument.IsHeading(lines, 4).Should().BeFalse("list items are never headings");
    }

    [Test]
    public void Every_document_type_has_an_instruction() =>
        DocumentType.All.Should().OnlyContain(t => t.Key.Length > 0 && t.Instruction.Length > 20);
}
