namespace ITMartinHjem.Server.Services;

// Martin's photo in the head box on every itmartin.dk tab, changing by itself (user 2026-10-27: picked 6, 8, 9, 12, 13, 15, 17, 18,
// 20, 21 from his newest photos, "automatic change", "a little bigger and the other people can be there"). Square crops in
// wwwroot/martin/, metadata gone. Changes every minute - the prerender and the live render show the same one, so nothing jumps.
// Place = small text on top of the picture (user 2026-10-27: "a location description (short). Like Northside"); "" = no text.
// The photos have no GPS (Facebook strips it): the texts are Martin's own (2026-10-27).
public static class MartinPhotos
{
    public sealed record Photo(string Src, string Place);

    public static readonly IReadOnlyList<Photo> All =
    [
        new("/martin/martin-20250329.jpg", "TV2 koncert 2025"),
        new("/martin/martin-20250405.jpg", "Skitur 2025"),
        new("/martin/martin-20250502.jpg", "Eigil student 2025"),
        new("/martin/martin-20250721.jpg", "Familien på ferie 2025"),
        new("/martin/martin-20250724.jpg", "Sommerferie"),
        new("/martin/martin-20250917.jpg", "Minder min far hvert år den 17/9 (elskede lakridspiber og rødvin)"),
        new("/martin/martin-20251220.jpg", "Jeg tror på julemanden"),
        new("/martin/martin-20260210.jpg", "Min sten, der holder mig oppe"),
        new("/martin/martin-20260604.jpg", "Northside 2026"),
        new("/martin/martin-20260605.jpg", "Northside 2026"),
    ];

    public static Photo Now => All[(int)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute % All.Count)];
}
