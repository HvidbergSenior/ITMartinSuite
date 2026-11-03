using FluentAssertions;
using ITMartinLager.Domain;
using NUnit.Framework;

namespace ITMartinLager.Tests;

// User 2026-11-03: iteration 1 = the loppemarked stock in Malenes kælder, iteration 2 = the next 5000.
public class IterationTests
{
    [Test]
    public void New_boxes_go_to_iteration_2_with_a_goal_of_5000()
    {
        Iterations.Current.Number.Should().Be(2);
        Iterations.Current.Goal.Should().Be(5000);
        Iterations.Of(1)!.Place.Should().Be("Malenes kælder");
        Iterations.Of(9).Should().BeNull();
    }
}
