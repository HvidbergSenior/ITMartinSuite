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
    // string pattern as Assignment.AssignedToNames - a weekly checklist can
    // recur on more than one day ("every Monday and Thursday: bins out"), not
    // just a single one. Mutually exclusive with IsDaily. Empty/null = not a
    // recurring weekly task (or IsDaily covers it instead). Superseded the
    // single-day RecurrenceDayOfWeek column (2026-09-08, before any real
    // weekly task existed yet) - that column is still physically in the DB,
    // just unmapped and unused.
    public string RecurrenceDaysRaw { get; set; } = string.Empty;

    // Reopens once a new calendar month starts (local time) - like IsDaily
    // but monthly, not tied to a specific day-of-month.
    public bool IsMonthly { get; set; }

    // Reopens 14 days after it was last completed - a rolling window from
    // completion, not aligned to a calendar boundary the way the others are
    // (there's no natural "start of a 14-day period" the way there is for a
    // day/week/month).
    public bool IsBiweekly { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Group Group { get; set; } = null!;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public List<DayOfWeek> RecurrenceDays =>
        RecurrenceDaysRaw.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(Enum.Parse<DayOfWeek>)
            .ToList();

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsWeekly => RecurrenceDaysRaw.Length > 0;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsRecurringCard => IsWeekly || IsMonthly || IsBiweekly;
}
