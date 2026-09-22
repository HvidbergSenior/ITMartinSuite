using FluentAssertions;
using ITMartinUpload.Server.Services;

namespace ITMartinUpload.Tests;

public class MessageStoreTests
{
    private string _root = "";
    private MessageStore _messages = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "upload-tests", Guid.NewGuid().ToString("N"));
        _messages = new MessageStore(new UploadStore(new UploadOptions { Root = _root }));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Test]
    public void A_thread_starts_empty() =>
        _messages.Read("annette-kent").Should().BeEmpty();

    [Test]
    public void Messages_are_kept_in_the_order_they_were_written()
    {
        _messages.Add("annette-kent", ChatMessage.Customer, "Skal videoer også med?");
        _messages.Add("annette-kent", ChatMessage.Martin, "Ja, send bare det hele.");

        _messages.Read("annette-kent").Select(m => m.Text)
            .Should().Equal("Skal videoer også med?", "Ja, send bare det hele.");
    }

    [Test]
    public void A_message_survives_a_new_store_over_the_same_folder()
    {
        _messages.Add("annette-kent", ChatMessage.Customer, "Hej Martin");

        var reopened = new MessageStore(new UploadStore(new UploadOptions { Root = _root }));
        reopened.Read("annette-kent").Should().ContainSingle();
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Blank_messages_are_not_stored(string text)
    {
        _messages.Add("annette-kent", ChatMessage.Customer, text).Should().BeFalse();
        _messages.Read("annette-kent").Should().BeEmpty();
    }

    [Test]
    public void A_very_long_message_is_cut_rather_than_rejected()
    {
        _messages.Add("annette-kent", ChatMessage.Customer, new string('x', 5000)).Should().BeTrue();

        _messages.Read("annette-kent").Single().Text.Length.Should().Be(2000);
    }

    [Test]
    public void One_customers_thread_is_not_another_customers()
    {
        _messages.Add("annette-kent", ChatMessage.Customer, "Hej");

        _messages.Read("bogshoppen").Should().BeEmpty();
    }

    [Test]
    public void Unanswered_counts_what_the_customer_wrote_after_Martins_last_reply()
    {
        _messages.Add("annette-kent", ChatMessage.Customer, "1");
        _messages.Add("annette-kent", ChatMessage.Martin, "svar");
        _messages.Add("annette-kent", ChatMessage.Customer, "2");
        _messages.Add("annette-kent", ChatMessage.Customer, "3");

        _messages.UnansweredCount("annette-kent").Should().Be(2);
    }

    [Test]
    public void Nothing_is_unanswered_right_after_Martin_replies()
    {
        _messages.Add("annette-kent", ChatMessage.Customer, "1");
        _messages.Add("annette-kent", ChatMessage.Martin, "svar");

        _messages.UnansweredCount("annette-kent").Should().Be(0);
    }

    [Test]
    public void A_bad_slug_is_never_turned_into_a_path()
    {
        _messages.Add("../andre", ChatMessage.Customer, "hej").Should().BeFalse();
        _messages.Read("../andre").Should().BeEmpty();
    }

    [Test]
    public void LastActivity_is_null_until_someone_writes()
    {
        _messages.LastActivityUtc("annette-kent").Should().BeNull();

        _messages.Add("annette-kent", ChatMessage.Customer, "hej");
        _messages.LastActivityUtc("annette-kent").Should().NotBeNull();
    }
}

public class UploadActivityTests
{
    [Test]
    public void The_first_file_of_a_batch_is_announced()
    {
        var activity = new UploadActivity();
        var now = DateTime.UtcNow;

        activity.ShouldAnnounceUpload("annette-kent", now).Should().BeTrue();
    }

    [Test]
    public void The_rest_of_the_batch_is_not()
    {
        var activity = new UploadActivity();
        var now = DateTime.UtcNow;
        activity.ShouldAnnounceUpload("annette-kent", now);

        activity.ShouldAnnounceUpload("annette-kent", now.AddMinutes(5)).Should().BeFalse();
        activity.ShouldAnnounceUpload("annette-kent", now.AddMinutes(29)).Should().BeFalse();
    }

    [Test]
    public void A_later_batch_is_announced_again()
    {
        var activity = new UploadActivity();
        var now = DateTime.UtcNow;
        activity.ShouldAnnounceUpload("annette-kent", now);

        activity.ShouldAnnounceUpload("annette-kent", now.AddMinutes(31)).Should().BeTrue();
    }

    [Test]
    public void Two_customers_do_not_silence_each_other()
    {
        var activity = new UploadActivity();
        var now = DateTime.UtcNow;
        activity.ShouldAnnounceUpload("annette-kent", now);

        activity.ShouldAnnounceUpload("bogshoppen", now).Should().BeTrue();
    }
}
