namespace ITMartinKunder.Domain;

// The free apps one sign-up gives access to (user 2026-10-21: "the available free apps I should have control of").
// Martin switches them on/off in Kunder; the sign-up boxes on the sites show the ones that are on.
public sealed class FreeApp
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string Text { get; set; } = "";     // one short line: what it does for you
    public bool Available { get; set; } = true;
    public int Sort { get; set; }

    /// <summary>What there is on the day Kunder starts (user 2026-10-21: Stem, ElPriser, Temperatur).</summary>
    public static List<FreeApp> Start() =>
    [
        new() { Name = "ElPriser", Url = "https://elpriser.itmartin.dk", Text = "Se hvornår strømmen er billigst i dag og i morgen.", Sort = 10 },
        new() { Name = "Temperatur", Url = "https://temperatur.itmartin.dk", Text = "Den målte temperatur ved den nærmeste vejrstation.", Sort = 20 },
        new() { Name = "Stem", Url = "https://stem.itmartin.dk", Text = "Lav en afstemning, og se hvad folk vælger.", Sort = 30 },
    ];

    public void Change(string name, string url, string text, bool available, int sort)
    {
        if (name.Trim().Length == 0) throw new ArgumentException("Appen skal have et navn.");
        (Name, Url, Text, Available, Sort) = (Customer.Cut(name.Trim(), 60), Customer.Cut(url.Trim(), 200), Customer.Cut(text.Trim(), 200), available, sort);
    }
}
