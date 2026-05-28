using static ITMartinBudget.Application.Rules.RulesFactory;

using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class PublicTransportRules
{
    public static readonly List<TransactionRule> Items =
    [
        PublicTransport(
            "rejsekort",
            "Rejsekort",
            ComparingType.Contains),

        PublicTransport(
            "brobizz",
            "BroBizz",
            ComparingType.Contains),

        PublicTransport(
            "dsb",
            "DSB",
            ComparingType.Word),

        PublicTransport(
            "midttrafik",
            "Midttrafik",
            ComparingType.Contains),

        PublicTransport(
            "letbane",
            "Letbane",
            ComparingType.Contains),

        PublicTransport(
            "molslinjen",
            "Molslinjen",
            ComparingType.Contains),

        PublicTransport(
            "go collect",
            "GoCollective",
            ComparingType.Contains),

        PublicTransport(
            "flixbus",
            "FlixBus",
            ComparingType.Contains),

        PublicTransport(
            "kombardo",
            "Kombardo",
            ComparingType.Contains)
    ];
}