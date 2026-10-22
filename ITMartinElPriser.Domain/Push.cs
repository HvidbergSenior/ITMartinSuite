namespace ITMartinElPriser.Domain;

// Web Push for the free app: anyone with the link can ask for a message an hour before the day's cheapest (and, if they
// want, dearest) two hours. The server keeps nothing about the visitor but this.

/// <summary>One phone that asked for messages. It carries its own price settings (copied from the cookie when it
/// subscribed), because the server has no other memory of who this phone is.</summary>
public sealed class PushSubscriber
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";
    public bool NotifyCheapest { get; set; } = true;
    public bool NotifyExpensive { get; set; }
    public HouseholdSettings Settings { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSentAt { get; set; }
    public int Failures { get; set; }
    /// <summary>The kundenummer that switched beskeder on (user 2026-10-21: beskeder need it). "" = signed up before that.</summary>
    public string CustomerCode { get; set; } = "";
    /// <summary>When the phone was asked once to add its kundenummer (user 2026-10-22: the old phones keep beskeder but
    /// should get into the Kundekartotek). Null = not asked yet.</summary>
    public DateTime? AskedForCodeAt { get; set; }
}

public sealed class SubscriberData
{
    public List<PushSubscriber> Subscribers { get; set; } = [];
    /// <summary>"subscriberId|kind|yyyy-MM-dd HH:mm" of messages already sent, so a restart never sends twice.</summary>
    public List<string> SentPushes { get; set; } = [];

    /// <summary>Forgets sent-log entries older than three days (they can never be due again).</summary>
    public void Prune(DateTime nowDk)
    {
        var cutoff = nowDk.AddDays(-3).ToString("yyyy-MM-dd");
        SentPushes.RemoveAll(s => string.CompareOrdinal(s.Split('|').Last(), cutoff) < 0);
    }
}

public sealed record PushMessage(string Title, string Body, string Url = "/");

/// <summary>When a message is due and what it says.</summary>
public static class PushPlan
{
    public static readonly TimeSpan Tick = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan LeadTime = TimeSpan.FromHours(1);

    /// <summary>The generic "one wash": 1 kWh over two hours - the thing people wait to start. Only the window matters.</summary>
    public static readonly Appliance Window = new() { Name = "Vask", Icon = "🧺", KwhPerRun = 1.0, DurationHours = 2 };

    /// <summary>Subscribers with the same key share one price calculation.</summary>
    public static string SettingsKey(HouseholdSettings s) =>
        $"{s.PriceArea}|{s.ShowAllIn}|{s.GridCompanyId}|{s.GridSupplierId}|{s.CustomNettarifOre}|{s.SupplierId}|{s.CustomTillaegOre}|{s.QuietFromHour}|{s.QuietToHour}";

    /// <summary>In the tick that lands 55-60 minutes before the start ("om en time"). With a 5-minute tick that is exactly
    /// one tick; the sent-log covers the rest.</summary>
    public static bool Due(DateTime start, DateTime now)
    {
        var ahead = start - now;
        return ahead <= LeadTime && ahead > LeadTime - Tick;
    }

    public static string LogKey(Guid subscriberId, string kind, DateTime windowStart) => $"{subscriberId}|{kind}|{windowStart:yyyy-MM-dd HH:mm}";

    /// <summary>One message to a phone that got beskeder before the kundenummer rule: please sign up. Beskeder keep
    /// working either way (user 2026-10-22). Sent in the daytime only, never at night.</summary>
    public static readonly PushMessage AskForCode = new("ElPriser – skriv dig op gratis",
        "Du får beskeder fra ElPriser. Skriv dig op gratis på itmartin.dk og få et kundenummer – tryk her. Dine beskeder fortsætter uanset hvad.",
        "/besked");

    public static bool ShouldAskForCode(PushSubscriber s, DateTime nowDk) =>
        s.CustomerCode.Length == 0 && s.AskedForCodeAt is null && nowDk.Hour is >= 10 and < 20;

    /// <summary>The messages due now for one price snapshot: (kind, window start, who wants it, message).</summary>
    public static List<(string Kind, DateTime Start, Func<PushSubscriber, bool> Wants, PushMessage Message)> DueNow(PriceSnapshot snap, DateTime now)
    {
        var due = new List<(string, DateTime, Func<PushSubscriber, bool>, PushMessage)>();
        if (!snap.HasData || snap.Appliances.Count == 0) return due;
        var w = snap.Appliances[0];
        var unit = snap.AllIn ? "kr/kWh alt inkl." : "kr/kWh spot";
        var cheapest = w.CheapestToday is { } t && t.Start > now ? t : w.CheapestTomorrow;
        if (cheapest is not null && Due(cheapest.Start, now))
            due.Add(("cheapest", cheapest.Start, s => s.NotifyCheapest, new PushMessage("Billigste strøm om en time",
                $"Kl. {cheapest.Start:HH:mm}–{cheapest.End:HH:mm} er dagens billigste to timer: {cheapest.AvgKrPerKwh:0.00} {unit}. En vask (1 kWh) koster {cheapest.CostKr:0.00} kr.")));
        if (w.DearestUpcoming is { } dearest && Due(dearest.Start, now))
            due.Add(("dearest", dearest.Start, s => s.NotifyExpensive, new PushMessage("Dyr strøm om en time",
                $"Kl. {dearest.Start:HH:mm}–{dearest.End:HH:mm} er dagens dyreste: {dearest.AvgKrPerKwh:0.00} {unit}"
                + (cheapest is not null ? $" mod {cheapest.AvgKrPerKwh:0.00} kl. {cheapest.Start:HH:mm}." : "."))));
        return due;
    }
}
