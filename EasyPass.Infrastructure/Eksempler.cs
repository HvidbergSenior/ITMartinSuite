using EasyPass.Domain;

namespace EasyPass.Infrastructure;

/// <summary>
/// Until the real places are imported (CSV from the old Easy-Pass), an empty data folder gets a few EXAMPLE places so the
/// pages show how it works. Every one is named "Eksempel: ..." and has Kilde = "Eksempel" - never presented as real facts.
/// Admin can delete them all with one press.
/// </summary>
internal static class Eksempler
{
    public static IEnumerable<Sted> Alle()
    {
        Sted S(string navn, string kat, string by, string postnr, double lat, double lng, string tekst, params (string, Svar)[] f) => new()
        {
            Navn = "Eksempel: " + navn, Kategori = kat, By = by, Postnr = postnr, Lat = lat, Lng = lng, Beskrivelse = tekst,
            Faciliteter = f.ToDictionary(x => x.Item1, x => x.Item2), Kilde = "Eksempel",
        };

        yield return S("Byens museum", "Museum", "Aarhus", "8000", 56.157, 10.204,
            "Et museum i én etage med bænke i hver sal. Eksempel - ikke et rigtigt sted.",
            ("niveaufri", Svar.Ja), ("elevator", Svar.Ja), ("toilet", Svar.Ja), ("parkering", Svar.Ja), ("siddepladser", Svar.Ja),
            ("stille-rum", Svar.Ja), ("roligt", Svar.Ja), ("skrift", Svar.Ja), ("ledsagerkort", Svar.Ja), ("hund", Svar.Ja), ("solsikke", Svar.Ja));
        yield return S("Havnecaféen", "Spisested", "Aarhus", "8000", 56.153, 10.214,
            "Hyggelig café med musik om aftenen. Eksempel - ikke et rigtigt sted.",
            ("niveaufri", Svar.Ja), ("toilet", Svar.Nej), ("siddepladser", Svar.Ja), ("roligt", Svar.Nej), ("hund", Svar.Ja));
        yield return S("Skovstien rundt om søen", "Natur og park", "Silkeborg", "8600", 56.169, 9.545,
            "2 km fast grussti med bænke for hver 300 meter. Eksempel - ikke et rigtigt sted.",
            ("niveaufri", Svar.Ja), ("parkering", Svar.Ja), ("siddepladser", Svar.Ja), ("roligt", Svar.Ja), ("korte-afstande", Svar.Nej), ("hund", Svar.Ja));
        yield return S("Teatret i midtbyen", "Kultur og teater", "Viborg", "8800", 56.451, 9.402,
            "Teater med teleslynge i store sal. Eksempel - ikke et rigtigt sted.",
            ("niveaufri", Svar.Ja), ("elevator", Svar.Ja), ("toilet", Svar.Ja), ("teleslynge", Svar.Ja), ("ledsagerkort", Svar.Ja), ("solsikke", Svar.Nej));
        yield return S("Klatreparken", "Aktivitet", "Randers", "8900", 56.460, 10.036,
            "Klatring i træerne. Eksempel - ikke et rigtigt sted.",
            ("niveaufri", Svar.Nej), ("toilet", Svar.Ja), ("parkering", Svar.Ja), ("roligt", Svar.Nej), ("allergivenlig", Svar.Ja));
    }
}
