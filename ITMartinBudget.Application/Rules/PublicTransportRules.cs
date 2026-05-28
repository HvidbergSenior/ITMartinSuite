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
            "Rejsekort"),

        PublicTransport(
            "brobizz",
            "BroBizz"),

        PublicTransport(
            "dsb",
            "DSB"),

        PublicTransport(
            "midttrafik",
            "Midttrafik"),

        PublicTransport(
            "letbane",
            "Letbane"),

        PublicTransport(
            "molslinjen",
            "Molslinjen"),

        PublicTransport(
            "go collect",
            "GoCollective"),

        PublicTransport(
            "flixbus",
            "FlixBus"),

        PublicTransport(
            "kombardo",
            "Kombardo")
    ];
}