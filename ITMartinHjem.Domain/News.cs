namespace ITMartinHjem.Domain;

/// <summary>"Nyt" on the front page (user 2026-11-12: "Also interesting news -> MY LOF evenings"): Martin's own evening
/// course at LOF Aarhus, agreed with LOF by mail 12.11 (dates + room booked). Hammel (Thursdays?) and a Viborg workshop
/// are not settled yet - add them here when they are. Shown until the last evening has passed.</summary>
public static class News
{
    public sealed record Course(string Title, string Teaser, string Place, string Time, IReadOnlyList<DateOnly> Dates, string Href);

    public static readonly Course LofAi = new(
        "Kunstig intelligens for begyndere – lær at hoppe, før du springer",
        "Mit aftenhold hos LOF Aarhus. Vi tager det roligt, og hver aften laver du din egen lille hjælper – fx en elplan, en budgethjælper eller en fakta-eller-fup-hjælper. Ingen forkundskaber.",
        "Fjordsgade forenings- og fritidshus, Aarhus",
        "Tirsdage kl. 19.00–21.15",
        [new(2027, 2, 2), new(2027, 2, 9), new(2027, 2, 23), new(2027, 3, 2), new(2027, 3, 9)],
        "https://lof.dk/aarhus");

    /// <summary>Show the course until its last evening is over.</summary>
    public static bool Show(Course c, DateOnly today) => today <= c.Dates[^1];

    public static string DateLine(Course c) =>
        string.Join(", ", c.Dates.Select(d => $"{d.Day}.{d.Month}")) + $" {c.Dates[^1].Year}";
}
