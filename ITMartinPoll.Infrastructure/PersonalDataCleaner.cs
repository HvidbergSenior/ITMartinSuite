using ITMartinPoll.Application;
using ITMartinPoll.Domain;
using ITMartinPoll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ITMartinPoll.Infrastructure;

// PrivacyRules on poll.db: per closed + quiet poll, names -> "Deltager n", e-mail/phone/voter token emptied, chat deleted.
// Running it again changes nothing (pseudonyms stay, empty stays empty).
internal sealed class PersonalDataCleaner(IDbContextFactory<PollDb> dbf) : IPersonalDataCleaner
{
    public async Task<int> ForgetOldAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var cleaned = 0;
        static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);

        foreach (var p in await db.Polls.Include(x => x.Votes).ToListAsync(ct))
        {
            var last = p.Votes.Select(v => Utc(v.VotedAt)).Append(Utc(p.CreatedAt)).Max();
            if (!PrivacyRules.ShouldForget(p.IsActive, p.Deadline, last, nowUtc)) continue;
            var ordered = p.Votes.OrderBy(v => v.VotedAt).ToList();
            var map = PrivacyRules.Pseudonyms(ordered.Select(v => v.VoterName));
            if (map.Count == 0 && ordered.All(v => v.Email == "")) continue;
            foreach (var v in ordered) { v.VoterName = map.GetValueOrDefault(v.VoterName, v.VoterName); v.Email = ""; }
            cleaned++;
        }

        foreach (var s in await db.Sessions.ToListAsync(ct))
        {
            var ratings = await db.ImageRatings.Where(r => db.SessionImages.Any(i => i.Id == r.ImageId && i.SessionId == s.Id)).OrderBy(r => r.RatedAt).ToListAsync(ct);
            var last = ratings.Select(r => Utc(r.RatedAt)).Append(Utc(s.CreatedAt)).Max();
            if (!PrivacyRules.ShouldForget(s.IsActive, s.Deadline, last, nowUtc)) continue;
            var map = PrivacyRules.Pseudonyms(ratings.Select(r => r.VoterName));
            if (map.Count == 0 && ratings.All(r => r.VoterToken == "")) continue;
            foreach (var r in ratings) { r.VoterName = map.GetValueOrDefault(r.VoterName, r.VoterName); r.VoterToken = ""; }
            cleaned++;
        }

        foreach (var d in await db.DatePolls.ToListAsync(ct))
        {
            var responses = await db.DatePollResponses.Where(r => db.DatePollDates.Any(x => x.Id == r.DateId && x.DatePollId == d.Id)).OrderBy(r => r.RespondedAt).ToListAsync(ct);
            var hosts = await db.DatePollHosts.Where(h => h.DatePollId == d.Id).OrderBy(h => h.OfferedAt).ToListAsync(ct);
            var chat = await db.DatePollChat.Where(c => c.DatePollId == d.Id).ToListAsync(ct);
            var last = responses.Select(r => Utc(r.RespondedAt)).Concat(hosts.Select(h => Utc(h.OfferedAt))).Concat(chat.Select(c => Utc(c.SentAt)))
                .Append(Utc(d.CreatedAt)).Max();
            if (!PrivacyRules.ShouldForget(d.IsActive, d.Deadline, last, nowUtc)) continue;
            var map = PrivacyRules.Pseudonyms(responses.Select(r => r.VoterName).Concat(hosts.Select(h => h.VoterName)));
            if (map.Count == 0 && chat.Count == 0) continue;
            foreach (var r in responses) r.VoterName = map.GetValueOrDefault(r.VoterName, r.VoterName);
            foreach (var h in hosts) h.VoterName = map.GetValueOrDefault(h.VoterName, h.VoterName);
            db.DatePollChat.RemoveRange(chat);
            cleaned++;
        }

        foreach (var e in await db.HelpEvents.Include(x => x.Signups).Include(x => x.Chat).ToListAsync(ct))
        {
            var last = e.Signups.Select(s => Utc(s.SignedUpAt)).Concat(e.Chat.Select(c => Utc(c.SentAt))).Append(Utc(e.CreatedAt)).Max();
            if (!PrivacyRules.ShouldForget(e.IsActive, e.Deadline, last, nowUtc)) continue;
            var signups = e.Signups.OrderBy(s => s.SignedUpAt).ToList();
            var map = PrivacyRules.Pseudonyms(signups.Select(s => s.Name));
            if (map.Count == 0 && signups.All(s => s.Phone == "") && e.Chat.Count == 0) continue;
            foreach (var s in signups) { s.Name = map.GetValueOrDefault(s.Name, s.Name); s.Phone = ""; }
            db.HelpChat.RemoveRange(e.Chat);
            cleaned++;
        }

        await db.SaveChangesAsync(ct);
        return cleaned;
    }
}

// Once a night, first run 5 minutes after start.
internal sealed class PrivacyJob(IServiceProvider services, ILogger<PrivacyJob> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMinutes(5), ct);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        do
        {
            try
            {
                using var scope = services.CreateScope();
                var n = await scope.ServiceProvider.GetRequiredService<Privacy>().CleanUpAsync(ct);
                log.LogInformation("Privacy clean-up: personal data taken out of {Count} polls (closed + quiet > {Months} months)", n, PrivacyRules.KeepPersonalMonths);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested) { log.LogWarning(ex, "Privacy clean-up failed"); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
