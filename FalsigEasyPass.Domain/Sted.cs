namespace FalsigEasyPass.Domain;

/// <summary>A place people can visit: what it is, where it is, and what it has for people with special needs.</summary>
public sealed class Sted
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Navn { get; set; } = "";
    public string Kategori { get; set; } = "Andet";
    public string Beskrivelse { get; set; } = "";
    public string Adresse { get; set; } = "";
    public string Postnr { get; set; } = "";
    public string By { get; set; } = "";
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public string Hjemmeside { get; set; } = "";
    public string Telefon { get; set; } = "";

    /// <summary>Facility key -> Ja/Nej. A key that is not here = Ukendt.</summary>
    public Dictionary<string, Svar> Faciliteter { get; set; } = [];

    /// <summary>Where the facts come from: "Eksempel", "Easy-Pass", "Stedet selv", "Besøgende".</summary>
    public string Kilde { get; set; } = "Eksempel";
    public DateTime? SidstTjekket { get; set; }

    public Svar Har(string facilitet) => Faciliteter.GetValueOrDefault(facilitet, Svar.Ukendt);

    public bool ErEksempel => Kilde == "Eksempel";
}

public static class StedRegler
{
    /// <summary>What is wrong with a place before it may be saved (Danish, empty = fine).</summary>
    public static IReadOnlyList<string> Problemer(Sted s)
    {
        var p = new List<string>();
        if (string.IsNullOrWhiteSpace(s.Navn)) p.Add("Stedet skal have et navn.");
        if (string.IsNullOrWhiteSpace(s.By) && string.IsNullOrWhiteSpace(s.Postnr)) p.Add("Skriv by eller postnummer.");
        if (s.Postnr.Length > 0 && !(s.Postnr.Length == 4 && s.Postnr.All(char.IsDigit))) p.Add("Postnummer er fire cifre.");
        if (!Katalog.Kategorier.Contains(s.Kategori)) p.Add("Vælg en kategori fra listen.");
        if (s.Lat is < 54 or > 58 || s.Lng is < 7 or > 16) p.Add("Placeringen ligger ikke i Danmark.");
        foreach (var k in s.Faciliteter.Keys)
            if (Katalog.FindFacilitet(k) is null) p.Add($"Ukendt facilitet: {k}.");
        return p;
    }

    /// <summary>Straight-line distance in km (good enough to sort "nær mig").</summary>
    public static double Km(double lat1, double lng1, double lat2, double lng2)
    {
        const double r = 6371;
        double Rad(double d) => d * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLng = Rad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * r * Math.Asin(Math.Sqrt(a));
    }
}
