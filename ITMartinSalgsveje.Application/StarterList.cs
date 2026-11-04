using ITMartinSalgsveje.Domain;

namespace ITMartinSalgsveje.Application;

/// <summary>The first salgsveje - from the market search and the talk on 2026-11-04. Dates are what was found then; check before booking.</summary>
public static class StarterList
{
    public static List<SalesChannel> For(Guid groupId)
    {
        SalesChannel C(string name, string kind, string place, string how, string cost = "", string booking = "",
            string sells = "", string link = "", DateTime? date = null, string status = SalesStatus.Idea) => new()
        {
            GroupId = groupId, Name = name, Kind = kind, Place = place, How = how, Cost = cost, Booking = booking,
            WhatSells = sells, Link = link, NextDate = date, Status = status,
        };

        return
        [
            C("Shoppe Loppe", SalesKinds.Marked, "Silkeborg",
                "Indendørs loppesupermarked med over 400 stande. Man lejer en stand i 4 uger, og de sælger for os hver dag.",
                "Spørg om pris for 4 uger", "Book på hjemmesiden", "Bøger, DVD, CD, blade - alt det billige", "https://shoppe-loppe.dk/"),
            C("Habengut på Godsbanen", SalesKinds.Marked, "Godsbanen, Skovgaardsgade 3C, Aarhus",
                "Indendørs marked i midten af Aarhus over to dage.", "Spørg om pris", "Kontakt Godsbanen", "", "",
                new DateTime(2027, 1, 30)),
            C("Loppemarked i Them Hallerne", SalesKinds.Marked, "Them, ved Silkeborg",
                "Stand med bord og stol. Gratis entré for kunderne.", "250 kr for en stand", "Spørg om næste dato",
                "", "https://detskerisilkeborg.dk/det-sker/loppemarked-i-them-hallerne-gratis-entre-stand-250-kr-q18h"),
            C("Bagagerumsmarked", SalesKinds.Marked, "Silkeborg (Bindslevs Plads) og Randers (Bauhaus)",
                "Sælg fra bilen. Udendørs, mest forår og sommer.", "", "Kun SMS til Ole Rask, 28 34 48 78", "",
                "https://bagagerumsmarkeder.dk/information/"),
            C("Randers kræmmer- og loppemarked", SalesKinds.Marked, "Arena Randers",
                "Stort indendørs marked. Var 7.-8. februar 2026 - kommer måske igen.", "", "jharrangementer.dk", "",
                "https://www.arenaranders.dk/arrangementer/randers-store-kraemmer-loppemarked/"),
            C("Smukt Marked", SalesKinds.Marked, "Ry",
                "Lørdage og weekender fra april til august, kl. 10-15."),
            C("Copenhagen Comics", SalesKinds.Events, "København SV",
                "Tegneseriefestival - var 23.-24. maj 2026. Godt til tegneserier og AA-blade.", "", "Spørg om stand",
                "Tegneserier, AA-blade, Superhelte"),
            C("Live-auktion i Kig med", SalesKinds.LiveAuktion, "Online, egen stream",
                "Vis tingene live i Kig med, folk byder i Snak. Forhåndsbud (Byd) kan komme til senere.", "Gratis", "",
                "De 10 dyreste, sjældne blade og albums"),
            C("bogshoppen.dk", SalesKinds.Online, "Online",
                "Vores egen side med varer og priser. Varerne kommer fra Lager.", "Gratis", "", "Alt med pris",
                "https://bogshoppen.dk/produkter", status: SalesStatus.Tried),
            C("antikvariat.net", SalesKinds.Online, "Online",
                "Danske antikvariaters fælles side for brugte bøger.", "Spørg om pris", "", "Bøger fra 50 kr og op"),
            C("Discogs", SalesKinds.Online, "Online",
                "Verdens største side for plader og CD'er.", "Gebyr pr. salg", "", "CD og vinyl"),
            C("Tillidsbod", SalesKinds.Tillidsbod, "Ved vejen, en café eller en butik",
                "En kasse med faste priser og et MobilePay-skilt. Nogen skal fylde den op og tømme den.", "", "",
                "Blade, DVD, VHS - ting til 10-25 kr"),
            C("Kasse til plejehjem", SalesKinds.UdMedKassen, "Plejehjem, ældreklubber, aktivitetscentre",
                "Pak en kasse, ring og aftal en dag, tag den med ud. Det der ikke sælges, kommer med hjem igen.", "", "Ring til aktivitetsmedarbejderen",
                "AA-blade, Rasmus Klump, Skipper Skræk, Storm P, gamle film"),
            C("Kasse til skoler og børnehaver", SalesKinds.UdMedKassen, "Børnehaver, SFO, skoler",
                "Kasse med børnebøger og tegneserier - fx når forældrene henter.", "", "Spørg lederen", "Børnebøger, tegneserier, spil"),
            C("Kasse til arbejdspladser", SalesKinds.UdMedKassen, "Kantiner og frokoststuer",
                "En blandet kasse der kan blive stående som tillidsbod.", "", "", "Krimier, romaner, film"),
            C("Kommission i butik eller café", SalesKinds.Kommission, "Genbrugsbutikker, caféer, antikvariater",
                "En hylde med vores ting - de tager en del af salget.", "Typisk en procentdel", "", "Bøger, tegneserier"),
            C("Marianne: strikketøj", SalesKinds.LigeTilKunden, "Markeder og venner",
                "Mariannes egne strikkede trøjer og andet. Hendes salg er hendes.", "", "", "Trøjer, huer, sokker"),
            C("Kassevis til forhandlere", SalesKinds.Kassevis, "Antikvariater, samlere, rekvisitter",
                "Sælg en hel kasse eller bøger på meter til indretning og film.", "", "", "Det der ikke sælger enkeltvis"),
        ];
    }
}
