using ITMartinFileSorter.Application.Services;
using NUnit.Framework;

namespace ITMartinFileSorter.Tests.Services;

[TestFixture]
public class ProgressServiceTests
{
private ProgressService _sut;

[SetUp]
public void Setup()
{
    _sut = new ProgressService();
}

// =========================
// START
// =========================

[Test]
public void Start_ShouldInitializeProgress()
{
    _sut.Start("Scanning", 10);

    Assert.That(_sut.Info.Stage, Is.EqualTo("Scanning"));
    Assert.That(_sut.Info.TotalWork, Is.EqualTo(10));
    Assert.That(_sut.Info.WorkDone, Is.EqualTo(0));
}

// =========================
// INCREMENT
// =========================

[Test]
public async Task Increment_ShouldIncreaseWorkDone()
{
    _sut.Start("Test", 10);

    _sut.Increment();
    _sut.Increment();

    Assert.That(_sut.Info.WorkDone, Is.EqualTo(2));
}

[Test]
public async Task Increment_ShouldCalculateSpeedAndEta()
{
    _sut.Start("Test", 10);

    _sut.Increment();

    // wait so speed calculation triggers (> 0.5s)
    await Task.Delay(600);

    _sut.Increment();

    Assert.That(_sut.Info.SpeedPerSecond, Is.GreaterThan(0));
    Assert.That(_sut.Info.EstimatedRemaining, Is.Not.Null);
}

// =========================
// STAGE
// =========================

[Test]
public void SetStage_ShouldUpdateStage()
{
    _sut.Start("A", 10);

    _sut.SetStage("B");

    Assert.That(_sut.Info.Stage, Is.EqualTo("B"));
}

// =========================
// COMPLETE
// =========================

[Test]
public void Complete_ShouldFinishProgress()
{
    _sut.Start("Test", 5);

    _sut.Complete();

    Assert.That(_sut.Info.WorkDone, Is.EqualTo(5));
    Assert.That(_sut.Info.Stage, Is.EqualTo("Completed"));
}

// =========================
// EVENTS
// =========================

[Test]
public void Increment_ShouldTriggerOnChange()
{
    _sut.Start("Test", 10);

    bool called = false;

    _sut.OnChange += () => called = true;

    _sut.Increment();

    Assert.That(called, Is.True);
}

[Test]
public void SetStage_ShouldTriggerOnChange()
{
    _sut.Start("Test", 10);

    bool called = false;

    _sut.OnChange += () => called = true;

    _sut.SetStage("New");

    Assert.That(called, Is.True);
}

}
