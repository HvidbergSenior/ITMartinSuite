using ITMartinBudget.Application.Models;
using ITMartinBudget.Application.Models;

using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class FromOutsideTransferRules
{
    public static readonly List<TransactionRule> Items =
    [
        RulesFactory.GiftIncome(
            "mobilepay marianne hvidberg",
            "Marianne"),

        RulesFactory.TransfersFromOutsideReceived(
            "mobilepay dorthe moeller",
            "Dorthe"),

        RulesFactory.TransfersFromOutsideReceived(
            "mobilepay bent moeller",
            "Bent"),

        RulesFactory.TransfersFromOutsideReceived(
            "mobilepay sonja john",
            "Sonja"),
        RulesFactory.TransfersToFamily(
        "mobilepay marianne hvidberg",
        "Marianne"),

        RulesFactory.TransfersFromOutsideReceived(
            "mobilepay mike ea basho",
            "Mike"),

        RulesFactory.TransfersFromOutsideReceived(
            "mobilepay inge kjaerulff",
            "Inge"),

        RulesFactory.TransfersFromOutsideReceived(
            "mobilepay jan isoe kjaer",
            "Jan"),

        RulesFactory.TransfersFromOutsideReceived(
            "mobilepay laura fogdal",
            "Laura"),

        RulesFactory.TransfersFromOutsideReceived(
            "mobilepay anne bro friis",
            "Anne Bro Friis"),
        RulesFactory.TransfersToOutsideGiven(
            "mobilepay sonja johnsen",
            "Sonja"),

        RulesFactory.TransfersToOutsideGiven(
            "mobilepay dorthe donsaek ebbese",
            "Dorthe"),
        RulesFactory.TransfersToOutsideGiven(
            "jan isoe kjaer",
            "Jan Isøe Kjær"),
        RulesFactory.TransfersToOutsideGiven(
            "mobilepay mette toft vestbjerg",
            "Mette"),

        RulesFactory.TransfersToOutsideGiven(
            "mobilepay karsten juul bunch",
            "Karsten"),

        RulesFactory.TransfersToOutsideGiven(
            "mobilepay kristian hertoft",
            "Kristian"),
        RulesFactory.TransfersFromOutsideReceived(
            "frida kjelsmark",
            "Frida"),

        RulesFactory.TransfersToOutsideGiven(
            "frida kjelsmark",
            "Frida"),

        RulesFactory.TransfersFromOutsideReceived(
            "inge kjaerulff",
            "Inge"),

        RulesFactory.TransfersToOutsideGiven(
            "inge kjaerulff",
            "Inge"),

        RulesFactory.TransfersFromOutsideReceived(
            "anne bro friis",
            "Anne Bro Friis"),

        RulesFactory.TransfersToOutsideGiven(
            "anne bro friis",
            "Anne Bro Friis"),
        RulesFactory.TransfersFromOutsideReceived(
            "mille ea bastho",
            "Mille Ea Bastho"),

        RulesFactory.TransfersFromOutsideReceived(
            "emma staehr",
            "Emma Stæhr"),

        RulesFactory.TransfersFromOutsideReceived(
            "michael guldham",
            "Michael Guldham"),

        RulesFactory.TransfersFromOutsideReceived(
            "mathias olin hvidber",
            "Mathias Olin"),

        RulesFactory.TransfersFromOutsideReceived(
            "dorthe donbaek ebbese",
            "Dorthe Donbæk"),

        RulesFactory.TransfersFromOutsideReceived(
            "siri alice birkefeld",
            "Siri Alice"),

        RulesFactory.TransfersFromOutsideReceived(
            "mette clemmensen",
            "Mette Clemmensen"),
        RulesFactory.TransfersFromOutsideReceived(
            "kongsvingervej",
            "Kongsvingervej"),
    ];
}