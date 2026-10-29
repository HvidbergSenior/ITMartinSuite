using ITMartinElPriser.Domain;

namespace ITMartinElPriser.Application;

/// <summary>The household's logged runs and its machines, changed together (a measured run calibrates its machine).</summary>
public interface IRunBook
{
    IReadOnlyList<RunEntry> Runs { get; }
    IReadOnlyList<Appliance> Appliances { get; }
    /// <summary>Changes and saves in one go.</summary>
    void Update(Action<List<RunEntry>, List<Appliance>> change);
}

/// <summary>Measures every logged run the meter now covers, and calibrates the machines from them (RunMeasure). Cheap - it
/// only reads the stored meter data - so the pages run it whenever they open.</summary>
public sealed class RunMeasuringUseCases(IRunBook book, IMeterReadings meter)
{
    /// <returns>How many runs got measured now.</returns>
    public int MeasurePending()
    {
        if (meter.LatestDay is not { } latest) return 0;
        var todo = book.Runs.Where(r => r.MeasuredKwh is null && r.MeasureNote.Length == 0
                                        && DateOnly.FromDateTime(r.StartedAt.AddHours(r.DurationHours)) <= latest).ToList();
        if (todo.Count == 0) return 0;
        var results = new Dictionary<Guid, MeasureResult>();
        foreach (var r in todo)
        {
            var from = DateOnly.FromDateTime(r.StartedAt);
            var quarters = meter.Quarters(from, DateOnly.FromDateTime(r.StartedAt.AddHours(r.DurationHours)));
            if (quarters.Count == 0)
            {
                // An hourly-only meter (or a gap): say so once instead of trying every time.
                results[r.Id] = new MeasureResult(0, true, "Måleren har ingen kvarterstal for den dag.");
                continue;
            }
            if (RunMeasure.Measure(quarters, r.StartedAt, r.DurationHours, r.KwhPerRun, r.IsTest) is { } m) results[r.Id] = m;
        }
        if (results.Count == 0) return 0;
        book.Update((runs, machines) =>
        {
            foreach (var r in runs)
                if (results.TryGetValue(r.Id, out var m))
                {
                    r.MeasuredKwh = m.Kwh > 0 ? m.Kwh : null;
                    r.MeasuredUncertain = m.Uncertain;
                    r.MeasureNote = m.Note;
                }
            foreach (var a in machines) RunMeasure.Calibrate(a, runs);
        });
        return results.Count;
    }

    public TestRecipe Recipe(Appliance a) => RunMeasure.Recipe(a);

    /// <summary>The newest test of this machine that the meter has not measured yet.</summary>
    public RunEntry? PendingTest(Guid applianceId) =>
        book.Runs.Where(r => r.ApplianceId == applianceId && r.IsTest && r.MeasuredKwh is null && r.MeasureNote.Length == 0).MaxBy(r => r.StartedAt);
}
