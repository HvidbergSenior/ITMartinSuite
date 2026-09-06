using FluentAssertions;
using ITMartinClub.Server.Data.Entities;

namespace ITMartinClub.Tests.Services;

// Pure logic on the Assignment entity itself - no database needed. Covers
// the assignee list, which is stored as a flat semicolon-delimited string
// rather than a join table (see the field's own comment).
[TestFixture]
public class AssignmentTests
{
    [Test]
    public void Assignees_is_empty_when_AssignedToNames_is_blank()
    {
        var a = new Assignment { AssignedToNames = "" };
        a.Assignees.Should().BeEmpty();
    }

    [Test]
    public void Assignees_parses_semicolon_separated_names()
    {
        var a = new Assignment { AssignedToNames = "Rico;Martin Hvidberg" };
        a.Assignees.Should().Equal("Rico", "Martin Hvidberg");
    }

    [Test]
    public void AssigneeLabel_joins_names_with_comma_space()
    {
        var a = new Assignment { AssignedToNames = "Rico;Martin Hvidberg" };
        a.AssigneeLabel.Should().Be("Rico, Martin Hvidberg");
    }

    [Test]
    public void IsAssignedTo_is_case_insensitive()
    {
        var a = new Assignment { AssignedToNames = "Rico" };
        a.IsAssignedTo("rico").Should().BeTrue();
        a.IsAssignedTo("RICO").Should().BeTrue();
        a.IsAssignedTo("Martin").Should().BeFalse();
    }

    [Test]
    public void AddAssignee_on_an_unclaimed_task_sets_the_name_with_no_separator()
    {
        var a = new Assignment { AssignedToNames = "" };
        a.AddAssignee("Rico");
        a.AssignedToNames.Should().Be("Rico");
    }

    [Test]
    public void AddAssignee_appends_a_second_assignee_with_a_semicolon()
    {
        var a = new Assignment { AssignedToNames = "Rico" };
        a.AddAssignee("Martin Hvidberg");
        a.AssignedToNames.Should().Be("Rico;Martin Hvidberg");
    }

    [Test]
    public void AddAssignee_does_not_duplicate_an_already_assigned_name()
    {
        var a = new Assignment { AssignedToNames = "Rico" };
        a.AddAssignee("Rico");
        a.AssignedToNames.Should().Be("Rico", "joining twice must not double-add or corrupt the list");
    }

    [Test]
    public void AddAssignee_dedup_check_is_case_insensitive()
    {
        var a = new Assignment { AssignedToNames = "Rico" };
        a.AddAssignee("rico");
        a.AssignedToNames.Should().Be("Rico", "the same person under different casing is still already assigned");
    }
}
