namespace ITMartinHjem.Server.Services;

// Martin's photo in the head box on every itmartin.dk tab, changing by itself (user 2026-10-27: picked 6, 8, 9, 12, 13, 15, 17, 18,
// 20, 21 from his newest photos, "automatic change", "a little bigger and the other people can be there"). Square crops in
// wwwroot/martin/, metadata gone. Changes every minute - the prerender and the live render show the same one, so nothing jumps.
public static class MartinPhotos
{
    public static readonly IReadOnlyList<string> All =
    [
        "/martin/martin-20250329.jpg", "/martin/martin-20250405.jpg", "/martin/martin-20250502.jpg", "/martin/martin-20250721.jpg",
        "/martin/martin-20250724.jpg", "/martin/martin-20250917.jpg", "/martin/martin-20251220.jpg", "/martin/martin-20260210.jpg",
        "/martin/martin-20260604.jpg", "/martin/martin-20260605.jpg",
    ];

    public static string Now => All[(int)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute % All.Count)];
}
