using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class ConcertBioRules
{
    public static readonly List<TransactionRule> Items =
    [
        // Cinema

        ConcertsBio(
            "cinemaxx",
            "CinemaxX",
            Category.KoncertBio),

        ConcertsBio(
            "paradisbio",
            "Paradis Bio",
            Category.KoncertBio),

        ConcertsBio(
            "oest for paradis",
            "Øst for Paradis",
            Category.KoncertBio),

        ConcertsBio(
            "ticketmaster",
            "Ticketmaster",
            Category.KoncertBio),

        ConcertsBio(
            "musikhuset",
            "Musikhuset",
            Category.KoncertBio),

        ConcertsBio(
            "train",
            "Train",
            Category.KoncertBio,
            ComparingType.Word),

        ConcertsBio(
            "voxhall",
            "VoxHall",
            Category.KoncertBio),

        ConcertsBio(
            "tivoli friheden",
            "Tivoli Friheden",
            Category.KoncertBio)
    ];
}