using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class ConcertBioRules
{
    public static readonly List<TransactionRule> Items =
    [
        RulesFactory.Entertainment(
            "cinemaxx",
            "CinemaxX",
            Category.KoncertBio,
            ComparingType.Contains),

        RulesFactory.Entertainment(
            "paradisbio",
            "Paradis Bio",
            Category.KoncertBio,
            ComparingType.Contains),

        RulesFactory.Entertainment(
            "oest for paradis",
            "Øst for Paradis",
            Category.KoncertBio,
            ComparingType.Contains),

        RulesFactory.Entertainment(
            "ticketmaster",
            "Ticketmaster",
            Category.KoncertBio,
            ComparingType.Contains),

        RulesFactory.Entertainment(
            "musikhuset",
            "Musikhuset",
            Category.KoncertBio,
            ComparingType.Contains),

        RulesFactory.Entertainment(
            "train",
            "Train",
            Category.KoncertBio,
            ComparingType.Word),

        RulesFactory.Entertainment(
            "voxhall",
            "VoxHall",
            Category.KoncertBio,
            ComparingType.Contains),

        RulesFactory.Entertainment(
            "tivoli friheden",
            "Tivoli Friheden",
            Category.KoncertBio,
            ComparingType.Contains),
        RulesFactory.Entertainment(
        "myticket",
        "MyTicket",
        Category.KoncertBio,
        ComparingType.Contains),
        
        RulesFactory.Entertainment(
            "dk safeticket dk",
            "Safeticket",
            Category.KoncertBio,
            ComparingType.Exact),
    ];
}