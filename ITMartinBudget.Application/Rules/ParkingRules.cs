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
            "EasyPark",
            ComparingType.Contains),

        Parking(
            "apcoa",
            "APCOA",
            ComparingType.Contains),

        Parking(
            "parkzone",
            "ParkZone",
            ComparingType.Contains),

        Parking(
            "q park",
            "Q-Park",
            ComparingType.Contains),

        Parking(
            "city p hus",
            "City P-Hus",
            ComparingType.Contains),

        Parking(
            "parkeringskompagniet",
            "Parkeringskompagniet",
            ComparingType.Contains),

        Parking(
            "parkman",
            "ParkMan",
            ComparingType.Contains),

        Parking(
            "onepark",
            "OnePark",
            ComparingType.Contains),

        Parking(
            "parkering aarhus",
            "Aarhus Parkering",
            ComparingType.Contains),

        Parking(
            "easy park",
            "EasyPark",
            ComparingType.Contains)
    ];
}