namespace ITMartinSkrivSange.Domain.Studio;

public sealed record ChordSegment(double StartSeconds, double EndSeconds, string Chord);

public static class ChordChart
{
    // Renders detected segments as a readable, editable chord-chart block
    // (mm:ss timestamp per chord change) - same textarea format the rest of
    // the app already uses for chord charts, so the result drops straight in.
    public static string Format(IEnumerable<ChordSegment> segments) =>
        string.Join("\n", segments.Select(s =>
            $"{TimeSpan.FromSeconds(s.StartSeconds):mm\\:ss} {s.Chord}"));
}
