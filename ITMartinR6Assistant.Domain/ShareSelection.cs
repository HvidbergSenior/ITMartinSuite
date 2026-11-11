namespace ITMartinR6Assistant.Domain;

// What the owner shares with the other players during one match (user 2026-11-11: "I can see all statistics but players
// only need to see for the selected"): the 5 maps the game offers and the operators in the ban lists.
// The owner always sees everything; the others only what is picked here. Cleared when a new match starts.
public class ShareSelection
{
    private readonly HashSet<string> _maps = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _ops = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Maps => _maps;
    public IReadOnlyCollection<string> Operators => _ops;

    public bool IsMapShown(string map) => _maps.Contains(map);
    public bool IsOperatorShown(string op) => _ops.Contains(op);

    public void ToggleMap(string map) { if (!_maps.Remove(map)) _maps.Add(map); }
    public void ToggleOperator(string op) { if (!_ops.Remove(op)) _ops.Add(op); }

    public void Clear() { _maps.Clear(); _ops.Clear(); }

    // The owner sees all items; everyone else only the shared ones.
    public IEnumerable<T> VisibleMaps<T>(IEnumerable<T> items, Func<T, string> name, bool isOwner) =>
        isOwner ? items : items.Where(i => _maps.Contains(name(i)));

    public IEnumerable<T> VisibleOperators<T>(IEnumerable<T> items, Func<T, string> name, bool isOwner) =>
        isOwner ? items : items.Where(i => _ops.Contains(name(i)));
}
