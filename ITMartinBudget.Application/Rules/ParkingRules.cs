using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class ParkingRules
{
    public static readonly List<TransactionRule> Items =
    [
        Parking(
            "easypark",
            "EasyPark"),

        Parking(
            "apcoa",
            "APCOA"),

        Parking(
            "parkzone",
            "ParkZone"),

        Parking(
            "q park",
            "Q-Park"),

        Parking(
            "city p hus",
            "City P-Hus"),

        Parking(
            "parkeringskompagniet",
            "Parkeringskompagniet"),

        Parking(
            "parkman",
            "ParkMan"
            ),

        Parking(
            "onepark",
            "OnePark"
            ),

        Parking(
            "parkering aarhus",
            "Aarhus Parkering"),
        
        Parking(
        "easypark",
        "EasyPark"),

        Parking(
            "apcoa",
            "APCOA"),

        Parking(
            "q park",
            "Q-Park"),
    ];
}