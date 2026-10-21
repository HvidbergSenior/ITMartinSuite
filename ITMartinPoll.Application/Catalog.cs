using ITMartinPoll.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinPoll.Application;

// One aggregate at a time (Poll+options+votes, ImageSession+images+ratings, DatePoll+dates+responses+hosts+chat+images,
// Package+polls+sessions, HelpEvent+slots+tasks+signups+chat, Customer). Reads are detached; Change loads the whole
// aggregate for change and saves it - children added to or removed from its lists are inserted/deleted.
public interface IAggregateStore<T> where T : class
{
    Task<T?> GetAsync(int id, CancellationToken ct = default);
    Task<List<T>> ListAsync(CancellationToken ct = default);
    Task<T> AddAsync(T aggregate, CancellationToken ct = default);
    /// <returns>False when the aggregate no longer exists.</returns>
    Task<bool> ChangeAsync(int id, Func<T, Task> change, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

// Uploaded pictures (/app/data/images, served at /poll-images).
public interface IImageFiles
{
    /// <returns>The stored file name.</returns>
    Task<string> SaveAsync(Stream content, string originalName, string prefix = "", CancellationToken ct = default);
}

// "Something changed on this poll" - the open pages refresh (in-process, the Server's broadcast service).
public interface IChangeSignal
{
    void Changed(int channel);
}

public static class ChangeChannels
{
    public static int DatePoll(int id) => id;
    public static int Help(int id) => 1_000_000 + id;   // help pages use their own range
}

// The front page (admin sees the lists; guests reach every poll only through its own link) and the admin overview.
public sealed class Catalog(IAggregateStore<Poll> polls, IAggregateStore<ImageSession> sessions, IAggregateStore<DatePoll> datePolls,
    IAggregateStore<Package> packages, IAggregateStore<HelpEvent> help)
{
    public sealed record FrontPage(List<Package> Packages, List<Poll> Polls, List<ImageSession> Sessions, List<HelpEvent> Help, List<DatePoll> DatePolls);
    public sealed record Overview(List<Poll> Polls, List<ImageSession> Sessions, List<DatePoll> DatePolls);

    // Nothing is listed for guests (user 2026-10-10: "man skal ikke have adgang til alle stem afstemninger").
    // Items in a package are reached through the package, not on their own.
    public async Task<FrontPage> FrontPageAsync(bool isAdmin, CancellationToken ct = default)
    {
        if (!isAdmin) return new([], [], [], [], []);
        return new(
            (await packages.ListAsync(ct)).Where(p => p.IsActive).OrderByDescending(p => p.CreatedAt)
                .Select(p => { p.Polls = p.Polls.Where(x => x.IsActive).ToList(); p.Sessions = p.Sessions.Where(x => x.IsActive).ToList(); return p; }).ToList(),
            (await polls.ListAsync(ct)).Where(p => p.IsActive && p.PackageId == null).OrderByDescending(p => p.CreatedAt).ToList(),
            (await sessions.ListAsync(ct)).Where(s => s.IsActive && s.PackageId == null).OrderByDescending(s => s.CreatedAt).ToList(),
            (await help.ListAsync(ct)).Where(h => h.IsActive).OrderByDescending(h => h.CreatedAt).ToList(),
            (await datePolls.ListAsync(ct)).Where(d => d.IsActive).OrderByDescending(d => d.CreatedAt).ToList());
    }

    public async Task<Overview> OverviewAsync(CancellationToken ct = default) => new(
        (await polls.ListAsync(ct)).OrderByDescending(p => p.CreatedAt).ToList(),
        (await sessions.ListAsync(ct)).OrderByDescending(s => s.CreatedAt).ToList(),
        (await datePolls.ListAsync(ct)).OrderByDescending(d => d.CreatedAt).ToList());

    public Task ToggleAsync(Poll p, CancellationToken ct = default) => polls.ChangeAsync(p.Id, x => { x.IsActive = !x.IsActive; return Task.CompletedTask; }, ct);
    public Task ToggleAsync(ImageSession s, CancellationToken ct = default) => sessions.ChangeAsync(s.Id, x => { x.IsActive = !x.IsActive; return Task.CompletedTask; }, ct);
    public Task ToggleAsync(DatePoll d, CancellationToken ct = default) => datePolls.ChangeAsync(d.Id, x => { x.IsActive = !x.IsActive; return Task.CompletedTask; }, ct);
    public Task DeleteAsync(Poll p, CancellationToken ct = default) => polls.DeleteAsync(p.Id, ct);
    public Task DeleteAsync(ImageSession s, CancellationToken ct = default) => sessions.DeleteAsync(s.Id, ct);
    public Task DeleteAsync(DatePoll d, CancellationToken ct = default) => datePolls.DeleteAsync(d.Id, ct);
}

// Admin: make or change an ordinary poll.
public sealed class PollEditor(IAggregateStore<Poll> polls, IImageFiles images, TimeProvider clock)
{
    public sealed record Form(string Title, string Body, string? ImageName, DateTime? DeadlineUtc, bool AllowMultiple, bool RequireContact, IReadOnlyList<string> Options);

    public Task<Poll?> GetAsync(int id, CancellationToken ct = default) => polls.GetAsync(id, ct);

    public Task<string> SaveImageAsync(Stream content, string name, CancellationToken ct = default) => images.SaveAsync(content, name, "", ct);

    /// <returns>Error (Danish) or null when saved.</returns>
    public async Task<string?> SaveAsync(int id, Form f, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(f.Title)) return "Titel mangler";
        var options = f.Options.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).ToList();
        if (options.Count < 2) return "Minimum 2 svarmuligheder";
        if (id == 0)
        {
            await polls.AddAsync(new Poll
            {
                Title = f.Title.Trim(), Body = f.Body.Trim(), ImageName = f.ImageName, Deadline = f.DeadlineUtc, IsActive = true,
                AllowMultiple = f.AllowMultiple, RequireContact = f.RequireContact, CreatedAt = clock.GetUtcNow().UtcDateTime,
                Options = options.Select((l, i) => new PollOption { Label = l, SortOrder = i }).ToList(),
            }, ct);
            return null;
        }
        await polls.ChangeAsync(id, p =>
        {
            p.Title = f.Title.Trim(); p.Body = f.Body.Trim(); p.ImageName = f.ImageName; p.Deadline = f.DeadlineUtc;
            p.AllowMultiple = f.AllowMultiple; p.RequireContact = f.RequireContact;
            // Options are only re-made when they really changed - re-making them removes the votes on them.
            if (!p.Options.OrderBy(o => o.SortOrder).Select(o => o.Label).SequenceEqual(options))
            {
                p.Options.Clear();
                p.Options.AddRange(options.Select((l, i) => new PollOption { PollId = id, Label = l, SortOrder = i }));
            }
            return Task.CompletedTask;
        }, ct);
        return null;
    }
}

// Image sessions: rate pictures 1-5 (public page) and make/edit the series (admin).
public sealed class ImageRatings(IAggregateStore<ImageSession> sessions, IImageFiles images, TimeProvider clock)
{
    public sealed record ImageForm(int Id, string FileName, string Caption, string Description);
    public sealed record Form(string Title, string Description, string Question, string? CoverName, DateTime? DeadlineUtc,
        IReadOnlyList<ImageForm> Existing, IReadOnlyList<ImageForm> New);

    public Task<ImageSession?> GetAsync(int id, CancellationToken ct = default) => sessions.GetAsync(id, ct);

    public Task RateAsync(int sessionId, int imageId, string token, string voterName, int score, string comment, CancellationToken ct = default) =>
        sessions.ChangeAsync(sessionId, s => { RatingRules.Rate(s, imageId, token, voterName, score, comment, clock.GetUtcNow().UtcDateTime); return Task.CompletedTask; }, ct);

    public Task<string> SaveImageAsync(Stream content, string name, string prefix = "", CancellationToken ct = default) => images.SaveAsync(content, name, prefix, ct);

    /// <returns>Error (Danish) or null when saved.</returns>
    public async Task<string?> SaveAsync(int id, Form f, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(f.Title)) return "Titel mangler";
        if (id == 0 && f.New.Count == 0) return "Upload mindst ét billede";
        if (id == 0)
        {
            await sessions.AddAsync(new ImageSession
            {
                Title = f.Title.Trim(), Description = f.Description.Trim(), Question = f.Question.Trim(),
                CoverImageName = string.IsNullOrEmpty(f.CoverName) ? null : f.CoverName, IsActive = true,
                CreatedAt = clock.GetUtcNow().UtcDateTime, Deadline = f.DeadlineUtc,
                Images = f.New.Select((n, i) => new SessionImage { FileName = n.FileName, SortOrder = i, Caption = n.Caption.Trim(), Description = n.Description.Trim() }).ToList(),
            }, ct);
            return null;
        }
        await sessions.ChangeAsync(id, s =>
        {
            s.Title = f.Title.Trim(); s.Description = f.Description.Trim(); s.Question = f.Question.Trim();
            s.CoverImageName = string.IsNullOrEmpty(f.CoverName) ? null : f.CoverName; s.Deadline = f.DeadlineUtc;
            foreach (var e in f.Existing)
                if (s.Images.FirstOrDefault(i => i.Id == e.Id) is { } img) { img.Caption = e.Caption.Trim(); img.Description = e.Description.Trim(); }
            var next = s.Images.Count;
            s.Images.AddRange(f.New.Select((n, i) => new SessionImage { SessionId = id, FileName = n.FileName, SortOrder = next + i, Caption = n.Caption.Trim(), Description = n.Description.Trim() }));
            return Task.CompletedTask;
        }, ct);
        return null;
    }
}

public static class DependencyInjection
{
    /// <summary>The use cases. Ports come from AddPollInfrastructure (stores, images) and the Server (IChangeSignal).</summary>
    public static IServiceCollection AddPollApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<Voting>();
        services.AddScoped<CustomerList>();
        services.AddScoped<CustomerPolls>();
        services.AddScoped<Catalog>();
        services.AddScoped<PollEditor>();
        services.AddScoped<ImageRatings>();
        services.AddScoped<DatePolls>();
        services.AddScoped<Packages>();
        services.AddScoped<HelpEvents>();
        return services;
    }
}
