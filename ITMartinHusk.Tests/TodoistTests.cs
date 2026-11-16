using FluentAssertions;
using ITMartinHusk.Application;
using ITMartinHusk.Domain;

namespace ITMartinHusk.Tests;

// "Hent fra Todoist" (2026-11-10): a Todoist project CSV becomes balls; the same file twice changes nothing.
public class TodoistTests
{
    private static readonly DateTime Tue = new(2026, 11, 10, 12, 0, 0);

    private const string Csv = "﻿TYPE,CONTENT,DESCRIPTION,PRIORITY,INDENT,AUTHOR,RESPONSIBLE,DATE,DATE_LANG,TIMEZONE\r\n"
        + "section,Hjemme,,,,,,,,\r\n"
        + "task,Ring til tandlægen @telefon,\"Tid til rens, helst om morgenen\",1,1,Jeppe,,2026-11-20,en,Europe/Copenhagen\r\n"
        + "task,Find nummeret,,4,2,Jeppe,,,en,\r\n"
        + "task,Spørg om pris,,4,2,Jeppe,,,en,\r\n"
        + "note,\"Husk kortet, \"\"det gule\"\"\",,,,,,,,\r\n"
        + "section,Arbejde,,,,,,,,\r\n"
        + "task,Ugerapport,,4,1,Jeppe,,every friday,en,\r\n"
        + "task,Køb blomster,,4,1,Jeppe,,tomorrow,en,\r\n"
        + "\r\n";

    private sealed class MemoryStore : IHuskStore
    {
        public HuskData Data = new();
        public HuskData Get() => Data;
        public void Update(Action<HuskData> change) => change(Data);
    }

    [Test]
    public void Tasks_become_balls_with_area_next_step_deadline_and_the_rest_in_the_note()
    {
        var p = TodoistImport.Parse(Csv, "Mit liv.csv", Tue);
        p.Error.Should().BeNull();
        p.Balls.Select(b => b.Title).Should().Equal("Ring til tandlægen", "Ugerapport", "Køb blomster");

        var dentist = p.Balls[0];
        (dentist.Area, dentist.NextStep, dentist.Deadline).Should().Be(("Hjemme", "Find nummeret", new DateTime(2026, 11, 20)));
        dentist.Note.Should().Contain("Tid til rens, helst om morgenen").And.Contain("prioritet 1").And.Contain("telefon")
            .And.Contain("Delopgaver: Spørg om pris").And.Contain("Husk kortet, \"det gule\"");

        p.Balls[1].Area.Should().Be("Arbejde");
        p.Balls[1].Deadline.Should().BeNull();
        p.Balls[1].Note.Should().Contain("Todoist-dato: every friday");   // a repeat is not guessed
        p.Balls[2].Deadline.Should().Be(Tue.Date.AddDays(1));
    }

    [Test]
    public void Not_a_Todoist_file_or_an_empty_one_says_so_in_plain_Danish()
    {
        TodoistImport.Parse("", "x.csv", Tue).Error.Should().Contain("tom");
        TodoistImport.Parse("Navn,Alder\r\nJens,40\r\n", "x.csv", Tue).Error.Should().Contain("Todoist");
        TodoistImport.Parse("TYPE,CONTENT\r\nsection,Kun en sektion\r\n", "x.csv", Tue).Error.Should().Contain("ingen opgaver");
    }

    [Test]
    public void Importing_the_same_file_twice_changes_nothing_and_an_existing_ball_is_never_overwritten()
    {
        var store = new MemoryStore();
        var balls = new BallUseCases(store, () => Tue);
        balls.Add("Køb blomster", "Mit eget", "Til mor", "", null).Should().BeNull();

        var first = balls.ImportTodoist(Csv, "Mit liv.csv");
        (first.Made, first.Already).Should().Be((2, 1));
        store.Data.Balls.Single(b => b.Title == "Køb blomster").NextStep.Should().Be("Til mor");   // untouched

        var again = balls.ImportTodoist(Csv, "Mit liv.csv");
        (again.Made, again.Already).Should().Be((0, 3));
        store.Data.Balls.Should().HaveCount(3);
    }

    [Test]
    public void Plain_dates_are_read_and_a_date_without_a_year_is_the_next_one()
    {
        TodoistImport.ReadDate("20/11/2026", Tue).Should().Be(new DateTime(2026, 11, 20));
        TodoistImport.ReadDate("20 Nov 2026", Tue).Should().Be(new DateTime(2026, 11, 20));
        TodoistImport.ReadDate("1 Nov", Tue).Should().Be(new DateTime(2027, 11, 1));
        TodoistImport.ReadDate("i morgen", Tue).Should().Be(Tue.Date.AddDays(1));
        TodoistImport.ReadDate("every monday", Tue).Should().BeNull();
    }
}
