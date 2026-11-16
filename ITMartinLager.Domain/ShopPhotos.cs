namespace ITMartinLager.Domain;

/// <summary>Which photos bogshoppen.dk may show (user 2026-11-16: "all the bad pictures I can not have in the shop"). Measured on
/// the 2,655 things for sale: 319 had only the whole pile photo, 323 were thin strips (CD spines ~176x1220 px, side views),
/// 1 was tiny. Those show "billede kommer" until Ret udsnittet or a new photo - the thing stays for sale.</summary>
public static class ShopPhotos
{
    public const int MinSide = 120;
    public const double MaxRatio = 3.0;

    public static string Words(string problem) => problem switch
    {
        "skjult" => "Billedet er skjult – det vises ikke på bogshoppen.dk.",
        "hele bunken" => "Kun billedet af hele bunken – det vises ikke på bogshoppen.dk. Brug »Ret udsnittet«.",
        "smal stribe" => "Billedet er en smal stribe (fx kun ryggen) – det vises ikke på bogshoppen.dk. Brug »Ret udsnittet« eller tag et nyt.",
        "for lille" => "Billedet er for lille – det vises ikke på bogshoppen.dk. Tag et nyt.",
        "intet billede" => "Intet billede.",
        _ => "",
    };
}
