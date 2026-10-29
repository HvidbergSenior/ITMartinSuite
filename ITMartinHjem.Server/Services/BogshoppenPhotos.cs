namespace ITMartinHjem.Server.Services;

// The picture in the head box on every bogshoppen.dk tab, like Martin's on itmartin.dk (user 2026-10-29: "pictures of Anders And
// blade and similar on Bogshoppen like ITMartin"). Bogshoppen's OWN copies, photographed in Vareinfo (boxes K0002/K0003, 2026-10-26) -
// not covers from the internet. Square crops in wwwroot/bogshoppen/, metadata gone. Changes every minute, like MartinPhotos.
public static class BogshoppenPhotos
{
    public static readonly IReadOnlyList<MartinPhotos.Photo> All =
    [
        new("/bogshoppen/bs-anders-and.jpg", "Anders And & Co. fra 80'erne"),
        new("/bogshoppen/bs-anders-and-basserne.jpg", "Anders And og Basserne"),
        new("/bogshoppen/bs-blandet.jpg", "Blade, bøger og film"),
    ];

    public static MartinPhotos.Photo Now => All[(int)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute % All.Count)];
}
