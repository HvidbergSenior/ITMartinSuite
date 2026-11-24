using ITMartinSnak.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace ITMartinSnak.Tests;

// 2026-11-24: "I get notification on Skejby Snak when writing on the Anton Snak".
public class PushRulesTests
{
    private static readonly PushPhone[] Phones =
    [
        new("skejby-app", "skejby", "Martin", true),
        new("skejby-app", "", "Martin Hvidberg", true),   // the Skejby icon was also set up as every-room
        new("anton-app", "anton", "Martin", true),
        new("anton-phone", "anton", "Anton", false),
        new("anton-ipad", "anton", "Anton", false),
        new("bertil", "skejby", "Bertil", false),
    ];

    [Test]
    public void Martin_writing_in_Anton_pings_none_of_his_phones()
    {
        var to = PushRules.Targets(Phones, "anton", "anton-app", "Martin", true);
        to.Should().Equal(new[] { "anton-phone", "anton-ipad" });
    }

    [Test]
    public void Martins_phone_counts_as_Martin_even_without_the_login_flag()
    {
        var to = PushRules.Targets(Phones, "anton", "anton-app", "Martin", false);
        to.Should().Equal(new[] { "anton-phone", "anton-ipad" });
    }

    [Test]
    public void Anton_writing_pings_Martin_once_per_phone_but_not_Antons_other_phone()
    {
        var to = PushRules.Targets(Phones, "anton", "anton-phone", "Anton", false);
        to.Should().Equal(new[] { "skejby-app", "anton-app" });
    }

    [Test]
    public void Other_rooms_phones_never_get_it()
    {
        var to = PushRules.Targets(Phones, "skejby", "bertil", "Bertil", false);
        to.Should().NotContain("anton-phone");
        to.Should().NotContain("bertil");
    }

    [Test]
    public void A_reminder_without_a_sender_goes_to_everyone_in_the_room()
    {
        var to = PushRules.Targets(Phones, "skejby", null, null, false);
        to.Should().Equal(new[] { "skejby-app", "bertil" });
    }
}
