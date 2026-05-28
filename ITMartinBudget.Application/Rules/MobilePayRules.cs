using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class MobilePayRules
{
    public static readonly List<TransactionRule> Items =
    [
        // Family

        RulesFactory.TransfersToFamily(
            "mobilepay bertil hvidberg john",
            "Bertil"),

        RulesFactory.TransfersToFamily(
            "mobilepay julius hvidberg john",
            "Julius"),

        RulesFactory.TransfersFromOutsideReceived(
            "mobilepay sonja johnsen",
            "Sonja"),

        RulesFactory.TransfersFromOutsideReceived(
            "mobilepay marianne hvidbe",
            "Marianne"),

        // Friends / External

        RulesFactory.TransfersFromOutsideReceived(
            "jan isoe kjaer",
            "Jan Isøe Kjær"),

        RulesFactory.TransfersFromOutsideReceived(
            "mobilepay bert moeller joh",
            "Bert Møller"),

        RulesFactory.TransfersFromOutsideReceived(
            "mobilepay mike ea basho",
            "Mike"),

        RulesFactory.TransfersFromOutsideReceived(
            "mobilepay ida koster hei",
            "Ida Koster"),

        RulesFactory.TransfersFromOutsideReceived(
            "mobilepay anne bro friis",
            "Anne Bro Friis"),

        RulesFactory.TransfersFromOutsideReceived(
            "mobilepay mathilde rass k",
            "Mathilde"),

        RulesFactory.TransfersFromOutsideReceived(
            "mobilepay laura fogdal li",
            "Laura Fogdal"),

        RulesFactory.TransfersFromOutsideReceived(
            "mobilepay frida kjedsmark",
            "Frida Kjedsmark"),

        RulesFactory.TransfersFromOutsideReceived(
            "mobilepay inge kjaerulf t",
            "Inge Kjærulf"),

        // External outgoing

        RulesFactory.TransfersToOutsideGiven(
            "mobilepay inge kjaerulf torp",
            "Inge Kjærulf Torp"),

        RulesFactory.TransfersToOutsideGiven(
            "mobilepay karl jon nielsen",
            "Karl Jon Nielsen"),

        RulesFactory.TransfersToOutsideGiven(
            "mobilepay mette toft vestbjerg",
            "Mette Toft Vestbjerg"),

        RulesFactory.TransfersToOutsideGiven(
            "mobilepay karsten juel bunch",
            "Karsten Juel Bunch"),

        RulesFactory.TransfersToOutsideGiven(
            "mobilepay arne bro friis jense",
            "Arne Bro Friis"),

        RulesFactory.TransfersToOutsideGiven(
            "mobilepay ida koster hebos",
            "Ida Koster"),

        // Stores / fallback shopping

        RulesFactory.ClothesAndShoes(
            "salling",
            "Salling"),

        RulesFactory.ThingsOtherThanClothes(
            "sp alpex",
            "SP Alpex",
            Category.Boern),

        RulesFactory.EverydayGrocery(
            "rema1000 trige",
            "Rema 1000"),

        RulesFactory.EntertainmentExpense(
            "united tickets",
            "United Tickets"),
        
    ];
}