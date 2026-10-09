using FluentAssertions;
using ITMartinSnak.Server;

namespace ITMartinSnak.Tests;

public class StoreTests
{
    private string _dir = "";

    [SetUp]
    public void SetUp() => _dir = Path.Combine(Path.GetTempPath(), "snak-test-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown() { try { Directory.Delete(_dir, true); } catch { } }

    [Test]
    public void Messages_survive_a_restart_and_come_after_the_last_seen_id()
    {
        var s = new Store(_dir, TimeProvider.System);
        s.AddMessage("Martin", "Hej alle").Should().NotBeNull();
        s.AddMessage("Sonja", "  Hej!  ")!.Text.Should().Be("Hej!");

        var again = new Store(_dir, TimeProvider.System);
        again.MessagesAfter(0).Select(m => m.Name).Should().Equal("Martin", "Sonja");
        again.MessagesAfter(1).Should().ContainSingle().Which.Id.Should().Be(2);
    }

    [Test]
    public void A_message_needs_a_name_and_text()
    {
        var s = new Store(_dir, TimeProvider.System);
        s.AddMessage("", "hej").Should().BeNull();
        s.AddMessage("Martin", "   ").Should().BeNull();
        s.AddMessage(new string('x', 50), "hej")!.Name.Should().HaveLength(Store.MaxName);
    }

    [Test]
    public void Taking_a_task_twice_lets_go_of_it_again()
    {
        var s = new Store(_dir, TimeProvider.System);
        var t = s.AddTask("Køb kaffe", "", "lørdag")!;

        s.ToggleTaker(t.Id, "Jan")!.Takers.Should().Equal("Jan");
        s.ToggleTaker(t.Id, "Sonja")!.Takers.Should().Equal("Jan", "Sonja");
        s.ToggleTaker(t.Id, "jan")!.Takers.Should().Equal("Sonja");
        new Store(_dir, TimeProvider.System).Tasks().Single().Takers.Should().Equal("Sonja");
    }

    [Test]
    public void Tasks_can_be_edited_finished_and_deleted()
    {
        var s = new Store(_dir, TimeProvider.System);
        var t = s.AddTask("Rydde op", "", "")!;
        s.AddTask(" ", "", "").Should().BeNull();

        s.EditTask(t.Id, "Rydde op i haven", "Tag handsker med", "søndag")!.When.Should().Be("søndag");
        s.SetDone(t.Id, true)!.Done.Should().BeTrue();
        s.DeleteTask(t.Id).Should().BeTrue();
        s.Tasks().Should().BeEmpty();
        s.DeleteTask(t.Id).Should().BeFalse();
    }
}
