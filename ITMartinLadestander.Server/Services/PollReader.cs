using Microsoft.Data.Sqlite;

namespace ITMartinLadestander.Server.Services;

public sealed record PollOption(string Label, int Votes);
public sealed record PollQuestion(int Id, string Title, List<PollOption> Options);
public sealed record OperatorRating(string Name, int Votes, double Avg, List<string> Comments);
public sealed record PollResult(int Households, List<PollQuestion> Questions, List<OperatorRating> Operators, DateTime ReadAt);

// Reads the Stem poll database (stem.itmartin.dk) read-only. Aggregates only -
// voter names never leave this class. Missing file => null and the page shows
// the numbers from 2026-09-20 instead.
public sealed class PollReader(IConfiguration cfg, ILogger<PollReader> log)
{
    private readonly string _path = cfg["PollDb"] ?? "/poll/poll.db";
    private PollResult? _cache;
    private DateTime _cachedAt;

    public PollResult? Read()
    {
        if (_cache is not null && DateTime.UtcNow - _cachedAt < TimeSpan.FromMinutes(5)) return _cache;
        if (!File.Exists(_path)) return _cache;
        try
        {
            using var db = new SqliteConnection($"Data Source={_path};Mode=ReadOnly");
            db.Open();

            var questions = new List<PollQuestion>();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText =
                    "select p.Id, p.Title, o.Label, count(v.Id) " +
                    "from Polls p join Options o on o.PollId = p.Id " +
                    "left join Votes v on v.OptionId = o.Id " +
                    "group by o.Id order by p.Id, o.Id";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var id = r.GetInt32(0);
                    var q = questions.LastOrDefault(x => x.Id == id);
                    if (q is null) { q = new PollQuestion(id, r.GetString(1), []); questions.Add(q); }
                    q.Options.Add(new PollOption(r.GetString(2), r.GetInt32(3)));
                }
            }

            int households;
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = "select count(distinct VoterName) from Votes";
                households = Convert.ToInt32(cmd.ExecuteScalar());
            }

            var ops = new List<OperatorRating>();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText =
                    "select i.Caption, count(r.Id), coalesce(avg(r.Score), 0) " +
                    "from Sessions s join SessionImages i on i.SessionId = s.Id " +
                    "left join ImageRatings r on r.ImageId = i.Id and r.Score > 0 " +
                    "where s.Title like 'Ladeoperat%' " +
                    "group by i.Id having count(r.Id) > 0 order by 3 desc, 2 desc";
                using var r = cmd.ExecuteReader();
                while (r.Read()) ops.Add(new OperatorRating(r.GetString(0), r.GetInt32(1), Math.Round(r.GetDouble(2), 1), []));
            }
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText =
                    "select i.Caption, r.Comment from Sessions s " +
                    "join SessionImages i on i.SessionId = s.Id join ImageRatings r on r.ImageId = i.Id " +
                    "where s.Title like 'Ladeoperat%' and r.Comment <> ''";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    ops.FirstOrDefault(o => o.Name == r.GetString(0))?.Comments.Add(r.GetString(1).Trim());
            }

            _cache = new PollResult(households, questions, ops, DateTime.UtcNow);
            _cachedAt = DateTime.UtcNow;
            return _cache;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Poll db read failed");
            return _cache;
        }
    }

    // What the db said on 2026-09-20, for when the poll volume is not mounted (dev box).
    public static PollResult Snapshot20260920() => new(11,
    [
        new(5, "Har du en elbil eller hybridbil i dag?", [new("Ja, elbil", 4), new("Ja, hybridbil", 0), new("Nej", 7)]),
        new(2, "Har du tænkt dig at anskaffe en elbil?", [new("Jeg har allerede en elbil", 4), new("Ja, inden for det næste år", 1), new("Ja, om 1-3 år", 3), new("Måske, men ikke lige nu", 2), new("Nej, det har jeg ikke planer om", 1)]),
        new(3, "Hvor mange ladestandere skal der være?", [new("1 ladestander med 2 udtag", 1), new("2 ladestandere med 4 udtag", 4), new("3 ladestandere med 6 udtag", 1), new("4 ladestandere med 8 udtag", 3), new("Ved ikke", 2)]),
        new(6, "Hvor vigtigt er det for dig, at du og dine gæster kan lade ved foreningen?", [new("Meget vigtigt", 1), new("Vigtigt", 5), new("Mindre vigtigt", 3), new("Ikke vigtigt", 2)]),
    ],
    [
        new("Clever", 3, 5.0, []),
        new("Spirii", 1, 5.0, []),
        new("OK", 2, 4.5, ["De virker ærlige og transparente og har løsninger til foreninger, hvor afregningen kan deles op på bruger.", "Altid rimeligt billig, og ofte gode/hurtige ladere på 75 kW."]),
    ], new DateTime(2026, 9, 20));
}
