using ITMartinElPriser.Core;
using NUnit.Framework;

namespace ITMartinElPriser.Tests;

public class ApplianceProgramTests
{
    private static Appliance Washer() => new()
    {
        Name = "Vaskemaskine",
        DurationHours = 2,
        Programs =
        [
            new() { Name = "40 °C", KwhPerRun = 0.7, DurationHours = 2, RunsPerWeek = 3 },
            new() { Name = "60 °C", KwhPerRun = 1.2, DurationHours = 2.5, RunsPerWeek = 1 },
        ],
    };

    [Test]
    public void Sync_makes_flat_fields_the_weekly_mix()
    {
        var a = Washer();
        a.SyncFromPrograms();
        Assert.That(a.RunsPerWeek, Is.EqualTo(4));
        Assert.That(a.KwhPerRun * a.RunsPerWeek, Is.EqualTo(0.7 * 3 + 1.2).Within(0.01));
        Assert.That(a.DurationHours, Is.EqualTo(2));
    }

    [Test]
    public void WithProgram_prices_one_program_and_keeps_the_id()
    {
        var a = Washer();
        var p = a.WithProgram(1);
        Assert.That(p.Id, Is.EqualTo(a.Id));
        Assert.That(p.KwhPerRun, Is.EqualTo(1.2));
        Assert.That(p.DurationHours, Is.EqualTo(2), "run time belongs to the machine");
        Assert.That(a.WithProgram(9), Is.SameAs(a));
    }

    [Test]
    public void Catalog_presets_match_by_name_prefix_only()
    {
        Assert.That(DeviceCatalog.ProgramsFor("Vaskemaskine Asko W6884")?.Id, Is.EqualTo("vaskemaskine"));
        Assert.That(DeviceCatalog.ProgramsFor("Opvaskemaskine")?.Id, Is.EqualTo("opvaskemaskine"));
        Assert.That(DeviceCatalog.ProgramsFor("Tørretumbler"), Is.Null);
    }
}
