using System.Globalization;
using System.Text;
using EasyPass.Domain;

namespace EasyPass.Application;

public sealed record ImportResultat(int Nye, int Opdateret, IReadOnlyList<string> Sprunget);

/// <summary>
/// CSV import (fx the export from the old Easy-Pass). Columns are found by name, Danish or English, any order; ; or , both work.
/// A facility column holds ja/nej/yes/no/true/false/1/0 (empty = not checked). Same name + same postnr = the same place (updated).
/// </summary>
public sealed class ImportSteder(IStedStore store, TimeProvider clock)
{
    private static readonly Dictionary<string, string> Felter = new(StringComparer.OrdinalIgnoreCase)
    {
        ["navn"] = "navn", ["name"] = "navn", ["kategori"] = "kategori", ["category"] = "kategori", ["type"] = "kategori",
        ["beskrivelse"] = "beskrivelse", ["description"] = "beskrivelse", ["adresse"] = "adresse", ["address"] = "adresse",
        ["postnr"] = "postnr", ["postnummer"] = "postnr", ["postal_code"] = "postnr", ["postalcode"] = "postnr", ["zip"] = "postnr",
        ["by"] = "by", ["city"] = "by", ["lat"] = "lat", ["latitude"] = "lat", ["lng"] = "lng", ["lon"] = "lng", ["longitude"] = "lng",
        ["hjemmeside"] = "hjemmeside", ["website"] = "hjemmeside", ["website_url"] = "hjemmeside", ["url"] = "hjemmeside",
        ["telefon"] = "telefon", ["phone"] = "telefon",
        // facilities, also the old Easy-Pass column names
        ["wheelchair_accessible"] = "niveaufri", ["has_elevator"] = "elevator", ["has_accessible_restroom"] = "toilet",
        ["has_accessible_parking"] = "parkering", ["sunflower_lanyard_recognized"] = "solsikke",
    };

    public ImportResultat Udfoer(string csv)
    {
        var rows = Csv.Parse(csv);
        if (rows.Count < 2) return new(0, 0, ["Filen har ingen rækker under overskriften."]);
        var header = rows[0].Select(h => Kolonne(h.Trim())).ToList();
        if (!header.Contains("navn")) return new(0, 0, ["Der mangler en kolonne der hedder navn (eller name)."]);

        var existing = store.Alle().ToDictionary(s => Key(s.Navn, s.Postnr), StringComparer.OrdinalIgnoreCase);
        var now = clock.GetUtcNow().UtcDateTime;
        var gem = new List<Sted>();
        var skipped = new List<string>();
        int nye = 0, opd = 0;

        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            string Get(string f) { var ix = header.IndexOf(f); return ix >= 0 && ix < row.Count ? row[ix].Trim() : ""; }

            var navn = Get("navn");
            var key = Key(navn, Get("postnr"));
            var s = existing.TryGetValue(key, out var old) ? old : new Sted();
            s.Navn = navn;
            s.Kategori = KendtKategori(Get("kategori"), s.Kategori);
            if (Get("beskrivelse") is { Length: > 0 } b) s.Beskrivelse = b;
            if (Get("adresse") is { Length: > 0 } a) s.Adresse = a;
            if (Get("postnr") is { Length: > 0 } pn) s.Postnr = pn;
            if (Get("by") is { Length: > 0 } by) s.By = by;
            if (Tal(Get("lat")) is { } lat) s.Lat = lat;
            if (Tal(Get("lng")) is { } lng) s.Lng = lng;
            if (Get("hjemmeside") is { Length: > 0 } w) s.Hjemmeside = w;
            if (Get("telefon") is { Length: > 0 } t) s.Telefon = t;
            foreach (var f in Katalog.Faciliteter)
                if (JaNej(Get(f.Noegle)) is { } svar) s.Faciliteter[f.Noegle] = svar;
            s.Kilde = "Easy-Pass";
            s.SidstTjekket = now;

            var p = StedRegler.Problemer(s);
            if (p.Count > 0) { skipped.Add($"Række {i + 1} ({(navn.Length > 0 ? navn : "uden navn")}): {p[0]}"); continue; }
            if (old is null) { nye++; existing[key] = s; } else opd++;
            gem.Add(s);
        }

        store.GemMange(gem);
        return new(nye, opd, skipped.Take(50).ToList());
    }

    private static string Kolonne(string h) =>
        Felter.TryGetValue(h, out var f) ? f : Katalog.FindFacilitet(h.ToLowerInvariant()) is not null ? h.ToLowerInvariant() : h;

    private static string Key(string navn, string postnr) => $"{navn.Trim()}|{postnr.Trim()}";

    private static string KendtKategori(string k, string fallback) =>
        k.Length == 0 ? fallback : Katalog.Kategorier.FirstOrDefault(x => x.Equals(k, StringComparison.OrdinalIgnoreCase)) ?? "Andet";

    private static double? Tal(string s) =>
        double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;

    private static Svar? JaNej(string s) => s.ToLowerInvariant() switch
    {
        "ja" or "yes" or "true" or "1" or "x" => Svar.Ja,
        "nej" or "no" or "false" or "0" => Svar.Nej,
        _ => null,
    };
}

/// <summary>Small CSV reader: quotes, "" inside quotes, ; or , (whichever the header line has more of).</summary>
public static class Csv
{
    public static List<List<string>> Parse(string text)
    {
        text = text.TrimStart('﻿');
        var firstLine = text.Split('\n')[0];
        var sep = firstLine.Count(c => c == ';') > firstLine.Count(c => c == ',') ? ';' : ',';
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else cell.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == sep) { row.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\n') { row.Add(cell.ToString().TrimEnd('\r')); cell.Clear(); rows.Add(row); row = []; }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString().TrimEnd('\r')); rows.Add(row); }
        return rows;
    }
}
