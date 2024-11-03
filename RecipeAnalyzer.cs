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
    public static Dictionary<string, string> AnalyzeRecipes(List<CraftingRecipe> recipes)
    {
        // 材料の組み合わせごとの分析結果を保持
        var ingredientCombinations =
            new Dictionary<
                string,
                List<(
                    Dictionary<string, int> Ingredients,
                    string Result,
                    int Quantity,
                    CraftingRecipe Recipe
                )>
            >();

        // レシピを材料の組み合わせでグループ化
        foreach (var recipe in recipes)
        {
            // 材料をidentifierでソートしてキーを作成
            var ingredients = recipe
                .itemsNeeded.Select(i => i.item.identifier)
                .OrderBy(name => name);
            var ingredientKey = string.Join("+", ingredients);

            // 材料の数量を記録
            var quantities = recipe.itemsNeeded.ToDictionary(
                ing => ing.item.identifier,
                ing => ing.quantity
            );

            if (!ingredientCombinations.ContainsKey(ingredientKey))
            {
                ingredientCombinations[ingredientKey] =
                    new List<(Dictionary<string, int>, string, int, CraftingRecipe)>();
            }
            ingredientCombinations[ingredientKey]
                .Add((quantities, recipe.createsItem.identifier, recipe.quantityCreated, recipe));
        }

        // 結果をフォーマット
        var results = new Dictionary<string, string>();
        foreach (var (ingKey, recipesList) in ingredientCombinations)
        {
            // 材料ごとの使用量の最小値と最大値を計算
            var ingredientRanges = new Dictionary<string, IngredientRange>();
            var resultRanges =
                new Dictionary<string, (IngredientRange Range, List<CraftingRecipe> Recipes)>();

            foreach (var (ingredients, result, quantity, recipe) in recipesList)
            {
                // 材料の範囲を更新
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

                // 生成物の範囲を更新
                if (!resultRanges.ContainsKey(result))
                {
                    resultRanges[result] = (new IngredientRange(), new List<CraftingRecipe>());
                }
                resultRanges[result].Range.Min = Math.Min(resultRanges[result].Range.Min, quantity);
                resultRanges[result].Range.Max = Math.Max(resultRanges[result].Range.Max, quantity);
                resultRanges[result].Recipes.Add(recipe);
            }

            // 結果を整形
            var ingredientsStr = ingredientRanges
                .Select(kvp =>
                    FormatRange(GetDisplayName(recipesList[0].Recipe, kvp.Key), kvp.Value)
                )
                .ToList();

            foreach (var (resultName, (ranges, recipesInRange)) in resultRanges)
            {
                var efficiency = CalculateEfficiency(recipesInRange[0].createsItem);

                var resultStr = FormatRange(GetDisplayName(recipesInRange[0], resultName), ranges);
                if (efficiency > 0)
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

    private static string GetDisplayName(CraftingRecipe recipe, string identifier)
    {
        if (identifier == recipe.createsItem.identifier)
        {
            return recipe.createsItem.LocalizedDisplayName;
        }

        var ingredient = recipe.itemsNeeded.FirstOrDefault(i => i.item.identifier == identifier);
        return ingredient.item.LocalizedDisplayName ?? identifier;
    }

    public static float CalculateEfficiency(ItemDefinition item)
    {
        var inventorySize = item.inventorySize.x * item.inventorySize.y;
        var healthRegenBuff = item.buffsOnConsume.Find(buff =>
            buff.attributeNew.id == EntityAttributes.Stat_HealthRegen
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
