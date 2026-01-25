using System;
using System.Collections.Generic;
using System.Linq;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.Items;
using PerfectRandom.Sulfur.Core.Stats;

namespace SulfurRecipeBook;

internal class IngredientRange
{
    public int Min { get; set; } = int.MaxValue;
    public int Max { get; set; } = 0;
}

public static class RecipeAnalyzer
{
    public static Dictionary<string, string> AnalyzeRecipes(List<RecipeData> recipes)
    {
        // Hold analysis results for each ingredient combination
        var ingredientCombinations =
            new Dictionary<
                string,
                List<(
                    Dictionary<ushort, int> Ingredients,
                    ushort Result,
                    int Quantity,
                    RecipeData Recipe
                )>
            >();

        // Group recipes by ingredient combination
        foreach (var recipe in recipes)
        {
            // Create key by sorting ingredients by identifier
            var ingredients = recipe.itemsNeeded.Select(i => i.item.value).OrderBy(name => name);
            var ingredientKey = string.Join("+", ingredients);

            // Record ingredient quantities
            var quantities = recipe.itemsNeeded.ToDictionary(
                ing => ing.item.value,
                ing => ing.quantity
            );

            if (!ingredientCombinations.ContainsKey(ingredientKey))
            {
                ingredientCombinations[ingredientKey] =
                    new List<(Dictionary<ushort, int>, ushort, int, RecipeData)>();
            }
            ingredientCombinations[ingredientKey]
                .Add((quantities, recipe.createsItem.value, recipe.quantityCreated, recipe));
        }

        // Format results
        var results = new Dictionary<string, string>();
        foreach (var (ingKey, recipesList) in ingredientCombinations)
        {
            // Calculate min and max quantities for each ingredient
            var ingredientRanges = new Dictionary<ushort, IngredientRange>();
            var resultRanges =
                new Dictionary<ushort, (IngredientRange Range, List<RecipeData> Recipes)>();

            foreach (var (ingredients, result, quantity, recipe) in recipesList)
            {
                // Update ingredient range
                foreach (var (ingName, ingredientQuantity) in ingredients)
                {
                    if (!ingredientRanges.ContainsKey(ingName))
                    {
                        ingredientRanges[ingName] = new IngredientRange();
                    }
                    ingredientRanges[ingName].Min = Math.Min(
                        ingredientRanges[ingName].Min,
                        ingredientQuantity
                    );
                    ingredientRanges[ingName].Max = Math.Max(
                        ingredientRanges[ingName].Max,
                        ingredientQuantity
                    );
                }

                // Update result range
                if (!resultRanges.ContainsKey(result))
                {
                    resultRanges[result] = (new IngredientRange(), new List<RecipeData>());
                }
                resultRanges[result].Range.Min = Math.Min(resultRanges[result].Range.Min, quantity);
                resultRanges[result].Range.Max = Math.Max(resultRanges[result].Range.Max, quantity);
                resultRanges[result].Recipes.Add(recipe);
            }

            // Build formatted string
            var ingredientsStr = ingredientRanges
                .Select(kvp =>
                    FormatRange(GetDisplayName(recipesList[0].Recipe, kvp.Key), kvp.Value)
                )
                .ToList();

            foreach (var (resultName, (ranges, recipesInRange)) in resultRanges)
            {
                var efficiency = CalculateHealPerSlot(recipesInRange[0].createsItem.GetAsset());

                var resultStr = FormatRange(GetDisplayName(recipesInRange[0], resultName), ranges);
                if (Plugin.ConfigShowHealPerSlot.Value && efficiency > 0)
                {
                    resultStr += $"<size=-1> [{efficiency:F2}]</size>";
                }

                var formula =
                    string.Join("<size=-1><color=#6D8791> + </color></size>", ingredientsStr)
                    + "<size=-1><color=#6D8791> = </color></size>"
                    + resultStr;
                results[ingKey] = formula;
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

    private static string FormatRange(string itemName, IngredientRange range)
    {
        if (range.Min == range.Max)
        {
            return $"{itemName}<size=-1><color=#6D8791>x{range.Min}</color></size>";
        }
        return $"{itemName}<size=-1><color=#6D8791>x({range.Min}~{range.Max})</color></size>";
    }
}
