using ITMartinElPriser.Infrastructure;
using ITMartinElPriser.Domain;

namespace ITMartinMitEl.Server.Services;

// "I just started the dishwasher" -> a priced, permanent entry.
public sealed class RunLogService(ElectricityPriceService prices, HouseholdStore store)
{
    public async Task<RunEntry?> LogAsync(Guid applianceId, DateTime startedAt, int? program = null, CancellationToken ct = default, TestRecipe? test = null)
    {
        var data = store.Get();
        var a = data.Appliances.FirstOrDefault(x => x.Id == applianceId);
        if (a is null) return null;
        // A picked program prices the run with its own kWh and length, and
        // the history shows it ("Vaskemaskine 40 °C").
        var name = a.Name;
        if (program is int pi && pi >= 0 && pi < a.Programs.Count)
        {
            name = $"{a.Name} {a.Programs[pi].Name}";
            a = a.WithProgram(pi);
        }

        // Priced with what the meter measured, when it has. A test runs for its recipe's time (a 30-minute oven test).
        a = RunMeasure.Priced(a);
        if (test is not null)
        {
            var t = a.Clone();
            t.KwhPerRun = a.DurationHours > 0 ? a.KwhPerRun / a.DurationHours * test.Hours : a.KwhPerRun;
            t.DurationHours = test.Hours;
            a = t;
            name += " (test)";
        }

        var day = DateOnly.FromDateTime(startedAt);
        var raw = await prices.GetDayAsync(day, data.Settings.PriceArea, ct);
        // A run that crosses midnight needs the next day too.
        if (startedAt.AddHours(a.DurationHours).Date > startedAt.Date)
            raw = raw.Concat(await prices.GetDayAsync(day.AddDays(1), data.Settings.PriceArea, ct)).ToList();

        var cost = PriceModel.CostOfRun(raw, data.Settings, a, startedAt);
        if (cost is null) return null;
        var (cheapest, dearest) = PriceModel.DayRange(raw, data.Settings, a, day);

        var entry = new RunEntry
        {
            ApplianceId = a.Id,
            ApplianceName = name,
            Icon = a.Icon,
            StartedAt = cost.Start,
            DurationHours = a.DurationHours,
            KwhPerRun = a.KwhPerRun,
            CostKr = cost.CostKr,
            AvgKrPerKwh = cost.AvgKrPerKwh,
            CheapestThatDayKr = cheapest?.CostKr,
            DearestThatDayKr = dearest?.CostKr,
            AllIn = data.Settings.ShowAllIn,
            IsTest = test is not null,
        };
        store.Update(d => d.Runs.Add(entry));
        return entry;
    }

    public void Delete(Guid id) => store.Update(d => d.Runs.RemoveAll(r => r.Id == id));
}
