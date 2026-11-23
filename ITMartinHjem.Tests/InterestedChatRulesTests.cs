using FluentAssertions;
using ITMartinHjem.Domain;

namespace ITMartinHjem.Tests;

// "Opret dig som interesseret" on the service pages: sign-up + Snak room (2026-11-23).
public class InterestedChatRulesTests
{
    [Test]
    public void FirstMessage_starts_with_the_service_and_has_a_default()
    {
        InterestedChatRules.FirstMessage("Hjælp over skærmen", " Min printer virker ikke ").Should().Be("Hjælp over skærmen: Min printer virker ikke");
        InterestedChatRules.FirstMessage("Hjælp over skærmen", "").Should().Be("Hjælp over skærmen: Jeg vil gerne høre mere.");
    }

    [Test]
    public void FirstMessage_is_never_longer_than_Snak_allows()
    {
        var text = InterestedChatRules.FirstMessage("X", new string('a', SnakStartRules.MaxText + 50));
        text.Length.Should().Be(SnakStartRules.MaxText);
        SnakStartRules.Check("Ole", text).Should().BeNull();
    }

    [Test]
    public void Note_is_the_service_when_nothing_was_written()
    {
        InterestedChatRules.Note("Din egen hjemmeside", null).Should().Be("Din egen hjemmeside");
        InterestedChatRules.Note("Din egen hjemmeside", "til foreningen").Should().Be("Din egen hjemmeside: til foreningen");
    }

    [Test]
    public void SignedUpButNoRoom_tells_the_number()
    {
        InterestedChatRules.SignedUpButNoRoom("12345678", null).Should().StartWith("Du er skrevet op – dit nummer er 12345678.");
        InterestedChatRules.SignedUpButNoRoom("", "Snak svarer ikke").Should().StartWith("Du er skrevet op. ");
    }

    [Test]
    public void Bogshoppen_is_recognised_as_its_own_sign_up()
    {
        InterestedChatRules.IsBogshoppen(" bogshoppen ").Should().BeTrue();
        InterestedChatRules.IsBogshoppen("Hjælp over skærmen").Should().BeFalse();
    }
}
