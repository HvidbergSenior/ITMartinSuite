using FluentAssertions;
using ITMartinElPriser.Domain;

namespace ITMartinElPriser.Tests;

[TestFixture]
public class CustomerSetupTests
{
    private static SetupFacts Empty() => new("Hjemme", "kunde@example.dk", false, "", null, "", "DK1", null, [], 0, 0, null);

    private static SetupFacts Ready() => new("Hjemme", "kunde@example.dk", true, "Skelagervej 222, 8200 Aarhus N",
        new DateOnly(2026, 11, 23), "Konstant", "DK1",
        new ElBill("NRGi", "Time", "Konstant", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), null, 268.8, 483.04, []),
        ["Vaskemaskine (Asko W6884)"], 5, 2, new DateTime(2027, 11, 24, 0, 0, 0, DateTimeKind.Utc));

    [Test]
    public void A_new_customer_has_nothing_done()
    {
        var (done, total) = CustomerSetup.Progress(CustomerSetup.Evaluate(Empty(), new Dictionary<string, DateTime>()));
        done.Should().Be(0);
        total.Should().Be(CustomerSetup.Steps.Count);
    }

    [Test]
    public void Data_ticks_the_automatic_steps_and_the_rest_are_ticked_by_hand()
    {
        var states = CustomerSetup.Evaluate(Ready(), new Dictionary<string, DateTime>());
        states.Where(s => s.Step.IsAuto).Should().OnlyContain(s => s.Done);
        states.Where(s => !s.Step.IsAuto).Should().OnlyContain(s => !s.Done);

        var ticks = CustomerSetup.Steps.Where(s => !s.IsAuto).ToDictionary(s => s.Id, _ => DateTime.UtcNow);
        var (done, total) = CustomerSetup.Progress(CustomerSetup.Evaluate(Ready(), ticks));
        done.Should().Be(total);
    }

    [Test]
    public void Fewer_than_three_devices_is_not_enough()
    {
        var states = CustomerSetup.Evaluate(Ready() with { DeviceCount = 2 }, new Dictionary<string, DateTime>());
        states.Single(s => s.Step.Id == "devices").Done.Should().BeFalse();
    }

    [Test]
    public void Every_step_says_how_and_ids_are_unique()
    {
        CustomerSetup.Steps.Should().OnlyContain(s => s.How.Length > 20 && s.Title.Length > 0);
        CustomerSetup.Steps.Select(s => s.Id).Should().OnlyHaveUniqueItems();
    }

    [Test]
    public void The_handover_lists_only_what_is_really_there_in_danish()
    {
        CustomerSetup.WhatWasSetUp(Empty()).Should().BeEmpty();
        var lines = CustomerSetup.WhatWasSetUp(Ready()).ToDictionary(l => l.Label, l => l.Value);
        lines.Keys.Should().Equal("Elmåler", "Forbrug", "Netselskab", "Elaftale", "Maskiner", "Enheder", "Besked");
        lines["Forbrug"].Should().Contain("23. november 2026");
        lines["Netselskab"].Should().Contain("DK1 (Vestdanmark)");
        lines["Elaftale"].Should().Be("NRGi Time (regning til 31. aug. 2026)");
        lines["Besked"].Should().Be("Slået til på 2 telefoner");
    }
}
