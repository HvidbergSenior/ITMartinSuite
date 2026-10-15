using ITMartinBibliotek.Domain;

namespace ITMartinBibliotek.Server;

// Small words and signs the pages share.
public static class Display
{
    public static string Icon(MediaKind k) => k switch { MediaKind.Musik => "♪", MediaKind.Serie => "▤", _ => "▶" };

    public static string Format(MediaFormat f) => f switch
    {
        MediaFormat.Dvd => "DVD",
        MediaFormat.BluRay => "Blu-ray",
        MediaFormat.Cd => "CD",
        _ => "Digital"
    };
}
