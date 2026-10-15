using ITMartinBibliotek.Domain;

namespace ITMartinBibliotek.Server;

// Every link into the shelf in one place, so a page never builds a key by hand.
public static class Links
{
    public static string Artist(Artist a) => "/kunstner/" + Uri.EscapeDataString(a.Key);
    public static string ArtistByName(string name) => "/kunstner/" + Uri.EscapeDataString(Shelf.ArtistKey(name));
    public static string Album(Album a) => "/album/" + Uri.EscapeDataString(a.Key);
    public static string Music => "/?kind=musik";
}
