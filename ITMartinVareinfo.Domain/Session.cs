namespace ITMartinVareinfo.Domain;

/// <summary>A session Martin prepares (user 2026-10-26: "I prepare a session with 4 helpers. I point to 20 boxes and
/// name them" - "a helper should ONLY have their Testkasse. I prepare which boxes so that they just have to choose").
/// The boxes of every open session are one shared pool: a helper taps a free one, the others see who has it until it
/// is done. A helper never types a box number.</summary>
public sealed class WorkSession
{
    /// <summary>A session holds at most this many boxes (a day's work for many helpers).</summary>
    public const int MaxBoxes = 200;

    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public List<SessionBox> Boxes { get; set; } = [];

    public bool Open => ClosedAt is null;

    public static WorkSession Start(string? name, DateTime now) => new()
    {
        Name = string.IsNullOrWhiteSpace(name) ? $"Session {now.ToLocalTime():d. MMM}" : name.Trim(),
        CreatedAt = now,
    };

    /// <summary>Points to a box (a number written on it, 1-MaxNormal). Throws when it is already in this session,
    /// out of range, or the session is full.</summary>
    public SessionBox Add(int number, string? name)
    {
        if (number is < 1 or > BoxClaim.MaxNormal) throw new InvalidOperationException($"Kassens nummer skal være 1-{BoxClaim.MaxNormal}.");
        if (Boxes.Count >= MaxBoxes) throw new InvalidOperationException($"En session har højst {MaxBoxes} kasser.");
        var code = BoxCodes.For(number);
        if (Boxes.Any(b => b.BoxCode == code)) throw new InvalidOperationException($"Kasse {number} er allerede med.");
        var box = new SessionBox { BoxCode = code, Name = (name ?? "").Trim(), SortOrder = Boxes.Count };
        Boxes.Add(box);
        return box;
    }

    public void Close(DateTime now) => ClosedAt ??= now;

    /// <summary>The next free numbers after every number used so far (boxes in sessions, boxes helpers have taken) -
    /// new boxes never get a number that is already on another box. Test boxes (9000-9999) do not count.</summary>
    public static List<int> NextNumbers(IEnumerable<int> used, int count)
    {
        var last = used.Where(n => n is > 0 and <= BoxClaim.MaxNormal).DefaultIfEmpty(0).Max();
        if (last + count > BoxClaim.MaxNormal) throw new InvalidOperationException("Der er ikke flere kassenumre.");
        return Enumerable.Range(last + 1, Math.Max(0, count)).ToList();
    }
}

/// <summary>One box in a session: the number on the box and the name Martin gave it ("Anders And 1970'erne").</summary>
public sealed class SessionBox
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public string BoxCode { get; set; } = "";   // K0007
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    /// <summary>The helper this box is for (user 2026-10-26, "Box per helper"): Taker.Id - a kundenummer helper is minus
    /// the number. Null = the shared pool, any helper can take it.</summary>
    public int? HelperId { get; set; }
    public string HelperName { get; set; } = "";

    public int Number => BoxCodes.Number(BoxCode);

    /// <summary>Martin sees every box; a helper the boxes given to them, or the pool ones.</summary>
    public bool IsFor(Taker me) => me.IsAdmin || HelperId is null || HelperId == me.Id;

    public void GiveTo(int? helperId, string? name) => (HelperId, HelperName) = helperId is null ? (null, "") : (helperId, (name ?? "").Trim());

    /// <summary>"Kasse 7 – Anders And 1970'erne", or just "Kasse 7".</summary>
    public string Title => Name == "" ? $"Kasse {Number}" : $"Kasse {Number} – {Name}";
}

/// <summary>How a session box looks to one helper right now.</summary>
public enum ChoiceState { Free, Mine, Busy, Done }

/// <summary>One button on the helper's list.</summary>
public sealed record BoxChoice(string BoxCode, string Title, ChoiceState State, string Who, int Photos)
{
    public bool CanTake => State is ChoiceState.Free or ChoiceState.Mine;

    /// <summary>What the helper sees for a box: free, theirs, someone else's right now, or done. A box left without a
    /// photo for BoxClaim.Idle is free again.</summary>
    public static BoxChoice For(SessionBox box, BoxClaim? claim, Taker me, DateTime now, int photos)
    {
        var state = claim switch
        {
            null => ChoiceState.Free,
            { DoneAt: not null } => ChoiceState.Done,
            _ when claim.OpenFor(me.Id, now) => ChoiceState.Mine,
            _ when claim.BusyFor(me.Id, now) => ChoiceState.Busy,
            _ => ChoiceState.Free,
        };
        return new BoxChoice(box.BoxCode, box.Title, state, claim?.HelperName ?? "", photos);
    }
}
