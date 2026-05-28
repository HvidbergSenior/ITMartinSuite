using ITMartinBudget.Application.Models;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Rules;

public static class MobilePayRules
{
    public static readonly List<TransactionRule> Items =
    [
        // Family

        RulesFactory.TransfersFamilyFromUs(
            "mobilepay bertil hvidberg john",
            "Bertil",
            ComparingType.Contains),

        RulesFactory.TransfersFamilyFromUs(
            "mobilepay julius hvidberg john",
            "Julius",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay sonja johnsen",
            "Sonja",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay marianne hvidbe",
            "Marianne",
            ComparingType.Contains),

        // Friends / External

        RulesFactory.TransfersOutsideToUs(
            "jan isoe kjaer",
            "Jan Isøe Kjær",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay bert moeller joh",
            "Bert Møller",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay mike ea basho",
            "Mike",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay ida koster hei",
            "Ida Koster",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay anne bro friis",
            "Anne Bro Friis",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay mathilde rass k",
            "Mathilde",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay laura fogdal li",
            "Laura Fogdal",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay frida kjedsmark",
            "Frida Kjedsmark",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideToUs(
            "mobilepay inge kjaerulf t",
            "Inge Kjærulf",
            ComparingType.Contains),

        // External outgoing

        RulesFactory.TransfersOutsideFromUs(
            "mobilepay inge kjaerulf torp",
            "Inge Kjærulf Torp",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "mobilepay karl jon nielsen",
            "Karl Jon Nielsen",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "mobilepay mette toft vestbjerg",
            "Mette Toft Vestbjerg",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "mobilepay karsten juel bunch",
            "Karsten Juel Bunch",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "mobilepay arne bro friis jense",
            "Arne Bro Friis",
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "mobilepay ida koster hebos",
            "Ida Koster",
            ComparingType.Contains),

        // Stores / fallback shopping

        RulesFactory.ClothesAndShoes(
            "salling",
            "Salling",
            Category.Toej,
            ComparingType.Contains),

        RulesFactory.GeneralShopping(
            "sp alpex",
            "SP Alpex",
            Category.Boern,
            ComparingType.Contains),

        RulesFactory.EverydayGrocery(
            "rema1000 trige",
            "Rema 1000",
            ComparingType.Contains),

        RulesFactory.Entertainment(
            "united tickets",
            "United Tickets",
            Category.KoncertBio,
            ComparingType.Contains),

        RulesFactory.TransfersOutsideFromUs(
            "marie elizabeth",
            "Marie Elizabeth",
            ComparingType.Contains)
    ];
}