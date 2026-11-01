using FluentAssertions;
using ITMartinPolstrer.Application;

namespace ITMartinPolstrer.Tests.UseCases;

[TestFixture]
public class PinGuardTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Test]
    public void Right_pin_logs_in()
        => new PinGuard(new Clock()).Check("1234", "1234", "a").Should().Be(PinResult.Ok);

    [Test]
    public void Fifth_wrong_try_locks_and_even_the_right_pin_waits()
    {
        var g = new PinGuard(new Clock());
        for (var i = 0; i < 4; i++) g.Check("x", "1234", "a").Should().Be(PinResult.Wrong);
        g.Check("x", "1234", "a").Should().Be(PinResult.Locked);
        g.Check("1234", "1234", "a").Should().Be(PinResult.Locked);
    }

    [Test]
    public void Lock_is_per_address_and_ends_after_15_minutes()
    {
        var clock = new Clock();
        var g = new PinGuard(clock);
        for (var i = 0; i < 5; i++) g.Check("x", "1234", "a");
        g.Check("1234", "1234", "b").Should().Be(PinResult.Ok);
        clock.Now = clock.Now.AddMinutes(15);
        g.Check("1234", "1234", "a").Should().Be(PinResult.Ok);
    }

    [Test]
    public void Right_pin_clears_earlier_wrong_tries()
    {
        var g = new PinGuard(new Clock());
        for (var i = 0; i < 4; i++) g.Check("x", "1234", "a");
        g.Check("1234", "1234", "a").Should().Be(PinResult.Ok);
        g.Check("x", "1234", "a").Should().Be(PinResult.Wrong);
    }

    [Test]
    public void Empty_pin_setting_never_lets_anyone_in()
        => new PinGuard(new Clock()).Check("", "", "a").Should().Be(PinResult.Wrong);
}
