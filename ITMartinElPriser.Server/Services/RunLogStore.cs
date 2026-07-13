using System.Text.Json;

namespace ITMartinElPriser.Server.Services;

public sealed class ApplianceRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Device { get; set; } = "";
    public double EstKwh { get; set; }
    public DateTime StartedAtDk { get; set; }
    public double PriceKrPerKwhAtStart { get; set; }
    public double EstCostKr { get; set; }
}

// Every "I started the washing machine" tap is logged here so the app can answer
// two questions people actually ask: how often do we run this, and what did it cost.
public sealed class RunLogStore
{
    private readonly string _path;
    private readonly object _lock = new();

    public RunLogStore(IConfiguration config)
    {
        var dataDir = config["DataDir"] ?? "/data";
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "runlog.json");
    }

    public ApplianceRun Add(string device, double estKwh, double priceKrPerKwhAtStart)
    {
        var run = new ApplianceRun
        {
            Device = device,
            EstKwh = estKwh,
            StartedAtDk = DateTime.Now,
            PriceKrPerKwhAtStart = priceKrPerKwhAtStart,
            EstCostKr = Math.Round(estKwh * priceKrPerKwhAtStart, 2),
        };

        lock (_lock)
        {
            var all = LoadAll();
            all.Add(run);
            all = all.OrderByDescending(r => r.StartedAtDk).Take(500).ToList();
            File.WriteAllText(_path, JsonSerializer.Serialize(all));
        }

        return run;
    }

    public List<ApplianceRun> GetRecent(int count = 30)
    {
        lock (_lock)
        {
            return LoadAll().OrderByDescending(r => r.StartedAtDk).Take(count).ToList();
        }
    }

    public (int RunsThisWeek, double CostThisWeekKr, int RunsThisMonth, double CostThisMonthKr) GetTotals()
    {
        lock (_lock)
        {
            var all = LoadAll();
            var weekStart = DateTime.Now.Date.AddDays(-7);
            var monthStart = DateTime.Now.Date.AddDays(-30);

            var week = all.Where(r => r.StartedAtDk >= weekStart).ToList();
            var month = all.Where(r => r.StartedAtDk >= monthStart).ToList();

            return (week.Count, Math.Round(week.Sum(r => r.EstCostKr), 2),
                    month.Count, Math.Round(month.Sum(r => r.EstCostKr), 2));
        }
    }

    private List<ApplianceRun> LoadAll()
    {
        if (!File.Exists(_path)) return [];

        try
        {
            return JsonSerializer.Deserialize<List<ApplianceRun>>(File.ReadAllText(_path)) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
