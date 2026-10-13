using ITMartinClub.Domain;

namespace ITMartinClub.Application;

public sealed record CalendarView(
    List<CalendarEvent> Upcoming,
    List<CalendarEvent> Past,
    Dictionary<Guid, List<EventRsvp>> Rsvps,
    Dictionary<Guid, EventPrep> Preps,
    Dictionary<Guid, EventTimeSuggestion> Suggestions,
    Dictionary<Guid, List<EventTimeVote>> Votes);

// Kalender: events, who comes (RSVP), a short "look at this before" prep, and members suggesting another time that the
// others vote on - an admin applies or dismisses the suggestion.
public sealed class TeamCalendar(ITeamItems<CalendarEvent> events, ICalendarStore store, IPushNotifier push, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<CalendarView> LoadAsync(Guid groupId, int pastCount = 5)
    {
        var now = Now;
        var all = (await events.ListAsync(groupId)).OrderBy(e => e.EventDate).ToList();
        var upcoming = all.Where(e => e.EventDate >= now).ToList();
        var past = all.Where(e => e.EventDate < now).OrderByDescending(e => e.EventDate).Take(pastCount).ToList();

        var eventIds = upcoming.Select(e => e.Id).ToList();
        var rsvps = (await store.RsvpsAsync(eventIds)).GroupBy(r => r.EventId).ToDictionary(g => g.Key, g => g.ToList());
        var preps = new Dictionary<Guid, EventPrep>();
        foreach (var p in await store.PrepsAsync(eventIds)) preps[p.EventId] = p;
        var suggestions = (await store.OpenSuggestionsAsync(eventIds, now))
            .GroupBy(s => s.EventId).ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CreatedAt).First());
        var votes = (await store.VotesAsync(suggestions.Values.Select(s => s.Id).ToList()))
            .GroupBy(v => v.SuggestionId).ToDictionary(g => g.Key, g => g.ToList());
        return new CalendarView(upcoming, past, rsvps, preps, suggestions, votes);
    }

    /// <summary>New or changed event + push to the team (except the one who did it; Admin: everyone).</summary>
    public async Task SaveEventAsync(Guid groupId, Guid? editingId, string title, DateTime dateUtc, string location, string description,
        string actingName, string? exceptMember)
    {
        if (string.IsNullOrWhiteSpace(title)) return;
        title = title.Trim();
        var loc = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        var desc = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        var dateStr = DanishTime.Local(dateUtc).ToString("d. MMM");

        if (editingId is { } id)
        {
            var ev = await events.GetAsync(groupId, id);
            if (ev is null) return;
            ev.Title = title; ev.EventDate = dateUtc; ev.Location = loc; ev.Description = desc;
            await events.UpdateAsync(ev);
            _ = push.ToTeamAsync(groupId, exceptMember, "📅 Begivenhed opdateret", $"{title} – {dateStr}");
        }
        else
        {
            await events.AddAsync(new CalendarEvent { GroupId = groupId, Title = title, EventDate = dateUtc, Location = loc, Description = desc, CreatedByName = actingName });
            _ = push.ToTeamAsync(groupId, exceptMember, "📅 Ny begivenhed", $"{title} – {dateStr}");
        }
    }

    public Task DeleteEventAsync(Guid groupId, Guid id) => events.DeleteAsync(groupId, id);

    public async Task SuggestTimeAsync(Guid groupId, Guid eventId, DateTime suggestedUtc, string byName)
    {
        if (await events.GetAsync(groupId, eventId) is null) return;
        await store.AddSuggestionAsync(new EventTimeSuggestion { EventId = eventId, SuggestedDate = suggestedUtc, SuggestedByName = byName });
        _ = push.ToTeamAsync(groupId, byName, "⏰ Ny tid foreslået", $"{byName} foreslår en anden tid - stem inden 2 timer");
    }

    public Task VoteAsync(Guid suggestionId, string memberName, string status) => store.VoteAsync(suggestionId, memberName, status);

    public async Task ApplySuggestionAsync(Guid groupId, Guid eventId, EventTimeSuggestion suggestion)
    {
        var ev = await events.GetAsync(groupId, eventId);
        if (ev is null) return;
        ev.EventDate = suggestion.SuggestedDate;
        await events.UpdateAsync(ev);
        await store.ResolveSuggestionAsync(suggestion.Id);
        _ = push.ToTeamAsync(groupId, null, "📅 Tid ændret", $"{ev.Title} er flyttet til {DanishTime.Local(suggestion.SuggestedDate):d. MMM HH:mm}");
    }

    public Task DismissSuggestionAsync(EventTimeSuggestion suggestion) => store.ResolveSuggestionAsync(suggestion.Id);

    public async Task SavePrepAsync(Guid groupId, CalendarEvent ev, string focus, string checklist, string byName)
    {
        if (string.IsNullOrWhiteSpace(focus)) return;
        await store.AddPrepAsync(new EventPrep { EventId = ev.Id, Focus = focus.Trim(), Checklist = checklist.Trim(), CreatedByName = byName });
        _ = push.ToTeamAsync(groupId, byName, "📋 Forberedelse klar", $"Til {ev.Title}: {focus.Trim()} – se hvad du skal gøre inden");
    }

    public Task RsvpAsync(Guid eventId, string memberName, string status) => store.SetRsvpAsync(eventId, memberName, status, Now);
}
