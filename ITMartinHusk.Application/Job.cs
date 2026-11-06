using ITMartinHusk.Domain;

namespace ITMartinHusk.Application;

/// <summary>What a helper (Claude) sends to add or update a job. Matched on Key; null = keep.</summary>
public sealed record JobInput(
    string? Key, string? Title, string? Employer = null, string? Place = null, string? Url = null, bool? Advertised = null,
    bool? PartTime = null, string? State = null, DateTime? AppliedAt = null, DateTime? Deadline = null, bool? InJoblog = null, string? Note = null);

/// <summary>Mine job: every job seen, applied for or left out, and the a-kasse count.</summary>
public sealed class JobUseCases(IHuskStore store, Func<DateTime> now)
{
    public List<JobLead> All() => store.Get().Jobs.OrderByDescending(j => j.AppliedAt ?? j.SeenAt).ToList();

    public JobCount Count() => JobRules.Count(store.Get().Jobs, now());

    public JobLead? AlreadyApplied(string? employer) => JobRules.AlreadyApplied(store.Get().Jobs, employer);

    public string? Add(string? title, string? employer, string? place, string? url, bool advertised, bool partTime)
    {
        try
        {
            var j = JobLead.Create(title, employer, null, now());
            j.Place = HuskRules.Clean(place, JobLead.MaxTitle);
            j.Url = Url(url);
            (j.Advertised, j.PartTime) = (advertised, partTime);
            store.Update(d => d.Jobs.Add(j));
            return null;
        }
        catch (ArgumentException e) { return e.Message; }
    }

    public void Applied(Guid id, DateTime day) => With(id, j => j.Apply(day, now()));
    public void Skip(Guid id, string? why) => With(id, j => j.Skip(why, now()));
    public void Reopen(Guid id) => With(id, j => j.Reopen(now()));
    public void SetInJoblog(Guid id, bool yes) => With(id, j => { j.InJoblog = yes && j.State == JobState.Applied; j.UpdatedAt = now(); });
    public void Remove(Guid id) => store.Update(d => d.Jobs.RemoveAll(j => j.Id == id));

    public BallSaveResult Save(IEnumerable<JobInput> input)
    {
        int made = 0, changed = 0;
        var errors = new List<string>();
        store.Update(d =>
        {
            foreach (var i in input)
            {
                var key = BallRules.Key(i.Key ?? $"{i.Employer} {i.Title}");
                if (key.Length == 0) { errors.Add("Et job mangler key eller titel."); continue; }
                JobState? state = null;
                if (i.State is not null && (state = JobRules.ParseState(i.State)) is null) { errors.Add($"{key}: ukendt status '{i.State}'."); continue; }
                try
                {
                    var j = d.Jobs.FirstOrDefault(x => x.Key == key);
                    var isNew = j is null;
                    j ??= JobLead.Create(i.Title, i.Employer, key, now());
                    if (i.Title is not null) j.Title = HuskRules.Clean(i.Title, JobLead.MaxTitle);
                    if (i.Employer is not null) j.Employer = HuskRules.Clean(i.Employer, JobLead.MaxTitle);
                    if (i.Place is not null) j.Place = HuskRules.Clean(i.Place, JobLead.MaxTitle);
                    if (i.Url is not null) j.Url = Url(i.Url);
                    if (i.Advertised is { } a) j.Advertised = a;
                    if (i.PartTime is { } p) j.PartTime = p;
                    if (i.Deadline is { } dl) j.Deadline = dl.Date;
                    if (i.Note is not null) j.Note = HuskRules.Clean(i.Note, JobLead.MaxText);
                    switch (state)
                    {
                        case JobState.Applied: j.Apply(i.AppliedAt ?? j.AppliedAt ?? now(), now()); break;
                        case JobState.Skipped: j.Skip(null, now()); break;
                        case JobState.Seen: j.Reopen(now()); break;
                    }
                    if (i.InJoblog is { } jl) j.InJoblog = jl && j.State == JobState.Applied;
                    j.UpdatedAt = now();
                    if (isNew) { d.Jobs.Add(j); made++; } else changed++;
                }
                catch (ArgumentException e) { errors.Add($"{key}: {e.Message}"); }
            }
        });
        return new BallSaveResult(made, changed, errors);
    }

    private void With(Guid id, Action<JobLead> change) => store.Update(d => { if (d.Jobs.FirstOrDefault(j => j.Id == id) is { } j) change(j); });

    /// <summary>Only http(s) links are kept - a link is pressed by the person, so nothing else gets in.</summary>
    private static string Url(string? url)
    {
        var u = (url ?? "").Trim();
        return u.Length <= JobLead.MaxUrl && Uri.TryCreate(u, UriKind.Absolute, out var x) && x.Scheme is "http" or "https" ? u : "";
    }
}
