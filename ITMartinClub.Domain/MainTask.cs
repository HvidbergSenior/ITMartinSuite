namespace ITMartinClub.Server.Data.Entities;

public sealed class MainTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? DefinitionOfDone { get; set; }
    public int SortOrder { get; set; }

    // When true, this main task is a recurring daily checklist rather than a
    // one-off backlog: its subtasks auto-reopen once a new day starts, and
    // "done" means all of today's subtasks are complete, not a fixed text.
    public bool IsDaily { get; set; }

    // Comma-separated DayOfWeek names ("Monday,Thursday"), same delimited-
    // string pattern as Assignment.AssignedToNames - lets weekly/biweekly/
    // monthly all recur on more than one day, not just a single one.
    // Meaning depends on which of IsWeekly/IsBiweekly/IsMonthly is set (see
    // each's own doc comment) - empty/null on any of them falls back to that
    // kind's day-agnostic behavior. Superseded the single-day
    // RecurrenceDayOfWeek column (2026-09-08, before any real weekly task
    // existed yet) - that column is still physically in the DB, just
    // unmapped and unused.
    public string RecurrenceDaysRaw { get; set; } = string.Empty;

    // Reopens once a new calendar month starts (local time), UNLESS specific
    // days are set in RecurrenceDaysRaw - then it reopens on the first
    // occurrence of one of those weekdays on/after the 1st (e.g. "first
    // Wednesday of the month"), not day-agnostic month-start.
    public bool IsMonthly { get; set; }

    // With no days set: reopens 14 days after it was last completed - a
    // rolling window from completion. With days set in RecurrenceDaysRaw:
    // reopens on the given weekday(s), but only every OTHER occurrence
    // (globally aligned by calendar week, not per-task) - "every other
    // Wednesday", not just any Wednesday.
    public bool IsBiweekly { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Group Group { get; set; } = null!;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public List<DayOfWeek> RecurrenceDays =>
        RecurrenceDaysRaw.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(Enum.Parse<DayOfWeek>)
            .ToList();

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsWeekly => RecurrenceDaysRaw.Length > 0 && !IsBiweekly && !IsMonthly;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsRecurringCard => IsWeekly || IsMonthly || IsBiweekly;
}
