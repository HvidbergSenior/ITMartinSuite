namespace ITMartinBudget.Domain.Enums;

public enum Category
{
    // Income

    Indkomst = 1,

    // Housing & fixed

    BoligVedligehold = 10,
    Regninger = 11,
    Forsikring = 12,
    TelefonTvInternet = 13,

    // Savings & transfers

    Opsparing = 20,
    Overfoersel = 21,
    OverfoerselTilBertil = 22,
    OverfoerselTilEigil = 23,
    OverfoerselTilJulius = 24,
    FagforeningAKasse = 25,

    // Food

    Dagligvarer = 30,
    Takeaway = 31,
    Restaurant = 32,
    Cafe = 33,

    // Entertainment

    Streaming = 40,
    KoncertBio = 41,
    Gaming = 42,
    Apps = 43,
    Northside = 44,
    Fritid = 45,

    // Transport

    Parkering = 50,
    Braendstof = 51,
    OffentligTransport = 52,
    BilVedligehold = 53,

    // Shopping

    Toej = 60,
    Elektronik = 61,
    Bolig = 62,

    // Health

    Sundhed = 70,

    // Family

    Boern = 80,
    Kaeledyr = 81,
    Gaver = 82,

    // Travel

    RejserUdflugter = 90,

    // Financial

    Pension = 100,
    Refund = 101,
    Skat = 102,
    Gebyrer = 103,
    Renter = 104,
    KommuneAndStat = 105,

    // Transfers

    OverfoerselFraFamilie = 110,
    OverfoerselFraIkkeFamilie = 111,
    OverfoerselTilFamilie = 112,
    OverfoerselTilIkkeFamilie = 113,

    // Misc

    OtherRepairThanCar = 120,
    Subscription = 121,
    OtherThanGroceries = 122,

    // Fallback

    Andet = 999,
    Ferie,
    Husleje,
    Aktier
}