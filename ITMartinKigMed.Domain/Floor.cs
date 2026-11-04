namespace ITMartinKigMed.Domain;

public sealed record Hand(string Viewer, string Name, DateTime At);
public sealed record Stage(string Viewer, string Name, string Token, DateTime Since);

// "Ræk hånden op": viewers ask to speak, Martin gives one of them the floor ("scenen").
// The guest publishes video with a one-time token; nobody else can publish as the guest.
public sealed class Floor
{
    private readonly List<Hand> _hands = [];

    public IReadOnlyList<Hand> Hands => _hands;
    public Stage? OnStage { get; private set; }

    // "Fri tale": Martin has said yes in advance - a raised hand gets the floor at once when nobody else has it,
    // and the next waiting hand gets it when the guest leaves. Off again when the session ends.
    public bool FreeTalk { get; private set; }

    public void SetFreeTalk(bool on, DateTime now) { FreeTalk = on; if (on) Next(now); }

    public bool Raise(string viewer, string name, DateTime now)
    {
        if (OnStage?.Viewer == viewer || _hands.Any(h => h.Viewer == viewer)) return false;
        _hands.Add(new Hand(viewer, Texts.NameOf(name), now));
        if (FreeTalk) Next(now);
        return true;
    }

    private void Next(DateTime now) { if (OnStage is null && _hands.Count > 0) Give(_hands[0].Viewer, now); }

    public void Lower(string viewer) => _hands.RemoveAll(h => h.Viewer == viewer);

    // Only someone who has raised a hand can get the floor; the previous guest loses it.
    public bool Give(string viewer, DateTime now)
    {
        var hand = _hands.FirstOrDefault(h => h.Viewer == viewer);
        if (hand is null) return false;
        _hands.Remove(hand);
        OnStage = new Stage(viewer, hand.Name, Guid.NewGuid().ToString("N"), now);
        return true;
    }

    // Martin takes the floor back (viewer null) - or the guest leaves by themselves.
    public void End(string? viewer = null, DateTime? now = null)
    {
        if (viewer is not null && OnStage?.Viewer != viewer) return;
        OnStage = null;
        if (FreeTalk && now is { } t) Next(t);
    }

    public void Clear() { _hands.Clear(); OnStage = null; FreeTalk = false; }

    public bool TokenOk(string? token) => OnStage is { } st && token == st.Token;
}
