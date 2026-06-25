namespace ITMartinMusicHelper.Web.Data;

public record VocalExercise(
    string Id, string Icon, string Title,
    string Duration, string Description,
    string[] Steps, string? Pattern = null
);

public static class VocalLibrary
{
    public static readonly VocalExercise[] Warmups =
    [
        new("breath", "🌬", "Åndedræt & Støtte",
            "3 min",
            "Stemmestøtte handler om at bruge maven som motor. Uden god åndedrætsstøtte vil stemmen skælve og løbe tør for luft midt i fraser.",
            [
                "Læg hånden på maven. Indånd dybt — maven skal udvide sig UDAD (ikke brystet op).",
                "Pust langsomt ud på 'ssss' i 8 tælleslag. Mærk maven trække ind gradvist.",
                "Gentag 4 gange. Prøv derefter at synge en tone og hold den i 8 slag med samme kontrol.",
                "Forsøg 'staccato': pust ud i korte stødet — 's s s s s s s s'. Maven arbejder aktivt.",
            ],
            Pattern: "Ind: 4 tæl  |  Ud: 8 tæl  |  Gentag 4x"),

        new("hum", "🎵", "Humming & Resonans",
            "5 min",
            "Hummer aktiverer resonansrummet i kraniet og varmer stemmen op uden at belaste stemmelæberne. Start altid her før du synger fuldt.",
            [
                "Luk munden let og hum en komfortabel tone (F eller G er typisk godt). Mærk vibrationen i læberne og næsen.",
                "Glid langsomt op ad en kvint (5 toner) og ned igen: 1-2-3-4-5-4-3-2-1 på 'hm'.",
                "Prøv 'ng'-lyden (som i 'sing'): den åbner resonansen i baghovedet.",
                "Hum melodien på den sang du vil øve, uden ord. Lyt om tonen sidder rigtigt.",
            ],
            Pattern: "1 - 2 - 3 - 4 - 5 - 4 - 3 - 2 - 1  (på 'hm')"),

        new("liproll", "💨", "Læberuller & Tungeskala",
            "3 min",
            "Læberulling (lip trill) varmer stemmen op med minimal belastning og hjælper med at udligne brud i stemmen (passaggio).",
            [
                "Slap munden let og lad læberne rulle ('brrr'). Hold en tone mens du ruller. Svært? Hold fingrene ved mundvigerne og støt kinderne.",
                "Glid op en oktav og ned: start lav, glid til toppen, glid ned. Holde 'brrrr' hele vejen.",
                "Skift til tungeskala: rul tungen ('rrrrr') i stedet. Aktiverer en anden del af resonansen.",
                "Synge 'Ma-me-mi-mo-mu' op ad en 5-toners skala. Exaggerer vokalerne.",
            ],
            Pattern: "brrr: ned - op oktav - ned  |  Gentag 5 gange"),

        new("scales", "🎼", "Toneskalaer",
            "5 min",
            "Skalaøvelser bygger stemmefleksibilitet og hjælper hjernen med at 'høre' toneniveauerne præcist. Brug et instrument til reference.",
            [
                "Spil en C-dur skala på guitaren/klaveret. Syng hver tone på 'la': C-D-E-F-G-A-H-C.",
                "Gentag op en halv tone (C#), derefter D, D# osv. Stop når det begynder at kræve anstrengelse.",
                "Forsøg nu 5-toners skala ned: 5-4-3-2-1 på 'na'. Hurtigt og let.",
                "Arpeggier: spil og syng 1-3-5-8 (C-E-G-C). Lad stemmen 'hoppe' let.",
            ],
            Pattern: "1-2-3-4-5-4-3-2-1  på 'la'  |  Flyttes ½ tone op ad gangen"),

        new("vowels", "🗣", "Vokalformning",
            "4 min",
            "Klare vokaler giver klar tekstforståelse og åbner resonansen. Mange sangere synger vokaler for smalt — lær at åbne kæben.",
            [
                "Åbn kæben (to fingre bredt). Sig 'AH' og hold tonen. Mærk ganen hæve sig som et hvælv.",
                "Glid mellem vokaler på samme tone: AH - EH - IH - OH - UH. Hold tonen igennem.",
                "Forsøg det samme op ad en 3-toners skala: 1-2-3 på 'AH', 3-2-1 på 'EH'.",
                "Synge teksten på din sang kun på vokaler: 'Jeg elsker dig' → 'E E E I'. Hør om tonerne er rene.",
            ],
            Pattern: "AH - EH - IH - OH - UH  (samme tone, åben kæbe)"),

        new("powerup", "🔥", "Fuld Opvarmning (10 min)",
            "10 min",
            "Kør alle fem øvelser i rækkefølge. Gør dette før hver sangøvelse eller optagesession.",
            [
                "🌬 Åndedræt: 2 min — In 4, ud 8 på 'sss'. 4 gentagelser.",
                "💨 Læberuller: 2 min — Brrr op og ned, hele stemmeregistret.",
                "🎵 Humming: 2 min — 1-2-3-4-5 og ned på 'hm', op ad semitonerne.",
                "🎼 Skalaer: 2 min — 5-toners skala på 'la', op og ned, flyttes ½ tone.",
                "🗣 Vokaler: 2 min — AH-EH-IH-OH-UH på 3-toners skala.",
            ],
            Pattern: "2+2+2+2+2 = 10 min — stemmen er klar"),
    ];
}
