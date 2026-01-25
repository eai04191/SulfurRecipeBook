using System;
using System.Collections.Generic;
using System.Linq;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.Items;
using PerfectRandom.Sulfur.Core.Stats;

namespace SulfurRecipeBook;

internal class QuantityRange
{
    public int Min { get; set; } = int.MaxValue;
    public int Max { get; set; }

    public void Update(int value)
    {
        Min = Math.Min(Min, value);
        Max = Math.Max(Max, value);
    }
}

public static class RecipeAnalyzer
{
    private const string SeparatorColor = "<size=-1><color=#6D8791>";
    private const string ColorEnd = "</color></size>";

    public static Dictionary<string, string> AnalyzeRecipes(List<RecipeData> recipes)
    {
        // Group recipes by ingredient combination
        var grouped = recipes.GroupBy(recipe =>
            string.Join("+", recipe.itemsNeeded.Select(i => i.item.value).OrderBy(x => x))
        );

        var results = new Dictionary<string, string>();

        foreach (var group in grouped)
        {
            var recipeList = group.ToList();
            var ingredientRanges = new Dictionary<ushort, QuantityRange>();
            var resultRanges =
                new Dictionary<ushort, (QuantityRange Range, RecipeData FirstRecipe)>();

            foreach (var recipe in recipeList)
            {
                // Update ingredient ranges
                foreach (var ingredient in recipe.itemsNeeded)
                {
                    var id = ingredient.item.value;
                    if (!ingredientRanges.TryGetValue(id, out var range))
                    {
                        range = new QuantityRange();
                        ingredientRanges[id] = range;
                    }
                    range.Update(ingredient.quantity);
                }

                // Update result range
                var resultId = recipe.createsItem.value;
                if (!resultRanges.TryGetValue(resultId, out var resultData))
                {
                    resultData = (new QuantityRange(), recipe);
                    resultRanges[resultId] = resultData;
                }
                resultData.Range.Update(recipe.quantityCreated);
            }

            // Build formatted string
            var firstRecipe = recipeList[0];
            var ingredientsStr = ingredientRanges.Select(kvp =>
                FormatRange(GetDisplayName(firstRecipe, kvp.Key), kvp.Value)
            );

            foreach (var (resultId, (range, recipe)) in resultRanges)
            {
                var resultStr = FormatRange(GetDisplayName(recipe, resultId), range);

                if (Plugin.ConfigShowHealPerSlot.Value)
                {
                    var efficiency = CalculateHealPerSlot(recipe.createsItem.GetAsset());
                    if (efficiency > 0)
                    {
                        resultStr += $"<size=-1> [{efficiency:F2}]</size>";
                    }
                }

                var formula =
                    string.Join($"{SeparatorColor} + {ColorEnd}", ingredientsStr)
                    + $"{SeparatorColor} = {ColorEnd}"
                    + resultStr;

                results[group.Key] = formula;
            }
        }

        return results;
    }

    private static string GetDisplayName(RecipeData recipe, ushort identifier)
    {
        if (identifier == recipe.createsItem.value)
        {
            return recipe.createsItem.GetAsset().LocalizedDisplayName;
        }

        var ingredient = recipe.itemsNeeded.FirstOrDefault(i => i.item.value == identifier);
        return ingredient.item.GetAsset().LocalizedDisplayName ?? identifier.ToString();
    }

    public static float CalculateHealPerSlot(ItemDefinition item)
    {
        var inventorySize = item.inventorySize.x * item.inventorySize.y;
        var healthRegenBuff = item.buffsOnConsume.Find(buff =>
            buff.attributeId == EntityAttributes.Stat_HealthRegen
        );
        return healthRegenBuff?.totalValueOverride / inventorySize ?? 0f;
    }

    private static string FormatRange(string itemName, QuantityRange range)
    {
        var quantityStr = range.Min == range.Max ? $"x{range.Min}" : $"x({range.Min}~{range.Max})";

        return $"{itemName}{SeparatorColor}{quantityStr}{ColorEnd}";
    }
}
