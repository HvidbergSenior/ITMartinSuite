using FluentAssertions;
using ITMartinClub.Server.Services;
using NUnit.Framework;

namespace ITMartinClub.Tests.Services;

// Member PINs on the join page (2026-10-08): a stranger who knows a name must not be able to guess the PIN forever.
public class PinRateLimiterTests
{
    [Test]
    public void Five_wrong_guesses_lock_that_member()
    {
        var limiter = new MemberPinRateLimiterService();
        var member = Guid.NewGuid();
        for (var i = 0; i < 4; i++) limiter.RegisterFailure(member);
        limiter.IsLockedOut(member).Should().BeFalse();
        limiter.RegisterFailure(member);
        limiter.IsLockedOut(member).Should().BeTrue();
    }

    [Test]
    public void Other_members_are_not_locked()
    {
        var limiter = new MemberPinRateLimiterService();
        var guessed = Guid.NewGuid();
        for (var i = 0; i < 5; i++) limiter.RegisterFailure(guessed);
        limiter.IsLockedOut(Guid.NewGuid()).Should().BeFalse();
    }

    [Test]
    public void A_correct_PIN_clears_earlier_wrong_guesses()
    {
        var limiter = new MemberPinRateLimiterService();
        var member = Guid.NewGuid();
        for (var i = 0; i < 4; i++) limiter.RegisterFailure(member);
        limiter.RegisterSuccess(member);
        limiter.RegisterFailure(member);
        limiter.IsLockedOut(member).Should().BeFalse();
    }
}
