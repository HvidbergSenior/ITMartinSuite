using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Services;

public class CategoryService : ICategoryService
{
    private static readonly Dictionary<Category, string[]> Keywords = new()
    {
        [Category.Income] =
        [
            "løn",
            "lønoverførsel",
            "salary",
            "overskydende skat",
            "feriepenge"
        ],

        [Category.Food] =
        [
            "føtex",
            "foetex",
            "netto",
            "rema",
            "lidl",
            "kvickly",
            "salling",
            "meny",
            "løvbjerg"
        ],

        [Category.Transport] =
        [
            "circle k",
            "uno-x",
            "ok",
            "easypark",
            "rejsekort",
            "benzin",
            "parkering",
            "mekaniker",
            "epass24"
        ],

        [Category.Bills] =
        [
            "spotify",
            "netflix",
            "telenor",
            "one.com",
            "jetbrains",
            "openai",
            "apple.com/bill",
            "alka",
            "a-kasse",
            "3f",
            "nrgi"
        ],

        [Category.Health] =
        [
            "apotek",
            "tandlæge",
            "fitness"
        ],

        [Category.Shopping] =
        [
            "adidas",
            "arket",
            "quint",
            "elgiganten",
            "proshop"
        ],

        [Category.Travel] =
        [
            "hotel",
            "travel",
            "superspeed",
            "airbnb"
        ],

        [Category.Savings] =
        [
            "opsparingskonto",
            "børneopsparing"
        ],

        [Category.Transfer] =
        [
            "mobilepay",
            "til 7633"
        ]
    };

    public Task<Category> DetectAsync(string text)
    {
        text = text
            .ToLowerInvariant()
            .Trim();

        foreach (var pair in Keywords)
        {
            if (pair.Value.Any(text.Contains))
            {
                return Task.FromResult(pair.Key);
            }
        }

        return Task.FromResult(Category.Other);
    }
}