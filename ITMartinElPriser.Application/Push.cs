using ITMartinElPriser.Domain;

namespace ITMartinElPriser.Application;

public sealed record PushSignup(string Endpoint, string P256dh, string Auth, string? Name, bool NotifyCheapest, bool NotifyExpensive, string? Code = null);

/// <summary>Besked: a phone asks for a message an hour before the cheapest (and dearest) two hours, and can test it.</summary>
public sealed class PushUseCases(ISubscriberStore store, IPushSender sender, GetPriceSnapshot snapshot, ICustomerCodes codes)
{
    public string PublicKey => sender.PublicKey;

    public Task<(bool Ok, string Error)> CheckCodeAsync(string? code, CancellationToken ct = default) => codes.CheckAsync(code ?? "", ct);

    /// <summary>Beskeder need the kundenummer (user 2026-10-21). A phone that already gets beskeder may change its choices
    /// without it (it signed up before the rule, or already showed its number).</summary>
    /// <returns>The phone's id, or an error for the person.</returns>
    public async Task<(Guid? Id, string? Error)> SubscribeAsync(PushSignup req, HouseholdSettings settings, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Endpoint)) return (null, "Telefonen gav ingen besked-adresse – prøv igen.");
        if (Status(req.Endpoint) is null)
        {
            var (ok, error) = await codes.CheckAsync(req.Code ?? "", ct);
            if (!ok) return (null, error);
        }
        return (Subscribe(req, settings), null);
    }

    /// <summary>Adds or updates the phone, with the price settings it has right now (from its cookie).</summary>
    internal Guid? Subscribe(PushSignup req, HouseholdSettings settings)
    {
        if (string.IsNullOrWhiteSpace(req.Endpoint)) return null;
        PushSubscriber? sub = null;
        store.Update(d =>
        {
            sub = d.Subscribers.FirstOrDefault(s => s.Endpoint == req.Endpoint);
            if (sub is null)
            {
                sub = new PushSubscriber { Endpoint = req.Endpoint };
                d.Subscribers.Add(sub);
            }
            sub.P256dh = req.P256dh;
            sub.Auth = req.Auth;
            sub.Name = string.IsNullOrWhiteSpace(req.Name) ? sub.Name : req.Name.Trim();
            sub.NotifyCheapest = req.NotifyCheapest;
            sub.NotifyExpensive = req.NotifyExpensive;
            sub.Settings = settings;
            if (!string.IsNullOrWhiteSpace(req.Code)) sub.CustomerCode = new string(req.Code.Where(char.IsDigit).ToArray());
        });
        return sub!.Id;
    }

    public void Unsubscribe(string endpoint) => store.Update(d => d.Subscribers.RemoveAll(s => s.Endpoint == endpoint));

    public PushSubscriber? Status(string endpoint) => store.Get().Subscribers.FirstOrDefault(x => x.Endpoint == endpoint);

    /// <summary>"Send mig en test" - proves the whole chain on this phone right now. Null = the phone is not signed up;
    /// false = it is gone.</summary>
    public async Task<bool?> TestAsync(string endpoint, CancellationToken ct = default)
    {
        if (Status(endpoint) is not { } sub) return null;
        var snap = await snapshot.ExecuteAsync(sub.Settings, null, null, [], ct);
        var body = snap.NowKrPerKwh is { } p ? $"Lige nu koster strømmen {p:0.00} kr/kWh. Beskeder virker ✓" : "Beskeder virker ✓";
        return await sender.SendAsync(sub, new PushMessage("ElPriser", body), ct);
    }
}

/// <summary>Every few minutes (NotificationJob): for each group of phones with the same settings, the messages that are due
/// now (PushPlan) - each sent once per phone, kind and window.</summary>
public sealed class NotifyUseCases(IPriceSource prices, ISubscriberStore store, IPushSender sender, IGridTariffSource tariffs)
{
    /// <summary>Returns how many messages were sent.</summary>
    public async Task<int> RunOnceAsync(DateTime nowDk, CancellationToken ct = default)
    {
        var subs = store.Get().Subscribers.ToList();
        var sent = 0;
        foreach (var group in subs.GroupBy(s => PushPlan.SettingsKey(s.Settings)))
        {
            var settings = group.First().Settings;
            await tariffs.AttachAsync(settings);
            var snap = PriceModel.Build(await prices.GetPricesAsync(settings.PriceArea, ct), settings, [PushPlan.Window], nowDk);
            foreach (var (kind, start, wants, message) in PushPlan.DueNow(snap, nowDk))
                foreach (var sub in group.Where(wants))
                {
                    var key = PushPlan.LogKey(sub.Id, kind, start);
                    if (store.Get().SentPushes.Contains(key)) continue;
                    store.Update(d => { d.SentPushes.Add(key); d.Prune(nowDk); });
                    if (await sender.SendAsync(sub, message, ct)) sent++;
                }
        }
        return sent;
    }
}

/// <summary>Tjek regning (the free hook for MinElpris): the visitor's own bill as a PDF, read in memory only - who gets
/// the money, whether the supplier's price matches what it reports to Forsyningstilsynet, and what could be saved.</summary>
public sealed class BillCheckUseCases(ISupplierSource catalog, IPdfText pdf)
{
    public const long MaxBytes = 10 * 1024 * 1024;

    public sealed record Result(ElBill? Bill, BillComparison? Comparison, string Error);

    public async Task<Result> CheckAsync(byte[] file)
    {
        string text;
        try { text = pdf.Read(file); }
        catch (Exception) { return new(null, null, "Filen kunne ikke åbnes som PDF. Prøv at hente regningen igen fra elselskabet."); }
        var bill = ElBillParser.Parse(text);
        if (bill is null || bill.Lines.Count == 0)
            return new(null, null, "Vi kunne ikke læse regningen. Er det en elregning som PDF? Lige nu kan vi læse NRGi’s – send gerne en fra et andet selskab til ITMartin@Mensa.dk.");
        return new(bill, BillComparison.For(bill, await catalog.GetAsync(GridAreaOf(bill.Grid))), "");
    }

    // The grid areas the parser knows by name (elpris.dk's codes).
    private static string? GridAreaOf(string grid) =>
        grid.Contains("Konstant", StringComparison.OrdinalIgnoreCase) ? "151"
        : grid.Contains("Radius", StringComparison.OrdinalIgnoreCase) ? "791"
        : grid.Contains("N1", StringComparison.OrdinalIgnoreCase) ? "131" : null;
}

/// <summary>Regning: an example month for the household - its kWh spread evenly over the last 30 days' real prices,
/// line by line with what each line is.</summary>
public sealed class MonthBillUseCases(IPriceSource prices, IHouseholdPrefs prefs)
{
    public const int Days = 30;

    public Task<HouseholdSettings> SettingsAsync() => prefs.GetAsync();

    public async Task<List<PricePoint>> PricesAsync(HouseholdSettings s, CancellationToken ct = default)
    {
        var to = DateOnly.FromDateTime(DkTime.Now);
        return await prices.GetRangeAsync(to.AddDays(-Days), to, s.PriceArea, ct);
    }

    /// <summary>save = false on the first load (the cookie cannot be written while the page is prerendered).</summary>
    public async Task<Bill> BuildAsync(HouseholdSettings s, List<PricePoint> raw, int kwh, bool save)
    {
        s.ShowAllIn = true;   // the bill only makes sense all-in
        if (save) await prefs.SaveAsync(s);
        var priced = raw.Select(p => PriceBreakdownCalculator.Compute(p, s)).ToList();
        var to = DateOnly.FromDateTime(DkTime.Now);
        var from = to.AddDays(-Days);
        var perHour = Math.Clamp(kwh, 1, 10000) / (double)(Days * 24);
        var kwhByHour = new Dictionary<DateTime, double>();
        for (var t = from.ToDateTime(TimeOnly.MinValue); t < to.ToDateTime(TimeOnly.MinValue); t = t.AddHours(1))
            kwhByHour[t] = perHour;
        return BillCalculator.Build(from, to, kwhByHour, priced, s);
    }
}
