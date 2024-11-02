using System;
using System.Collections.Generic;
using System.Linq;
using PerfectRandom.Sulfur.Core;

namespace SulfurRecipeBook;

internal class IngredientRange
{
    public int Min { get; set; } = int.MaxValue;
    public int Max { get; set; } = 0;
}

public class RecipeAnalyzer
{
    // identifierと表示用の名前のマッピング
    private readonly Dictionary<string, string> _displayNames = new();

    public Dictionary<string, string> AnalyzeRecipes(List<CraftingRecipe> recipes)
    {
        _displayNames.Clear();

        // 材料の組み合わせごとの分析結果を保持
        var ingredientCombinations =
            new Dictionary<
                string,
                List<(Dictionary<string, int> Ingredients, string Result, int Quantity)>
            >();

        // レシピを材料の組み合わせでグループ化
        foreach (var recipe in recipes)
        {
            // 表示名のマッピングを記録
            foreach (var ingredient in recipe.itemsNeeded)
            {
                _displayNames[ingredient.item.identifier] = ingredient.item.LocalizedDisplayName;
            }
            _displayNames[recipe.createsItem.identifier] = recipe.createsItem.LocalizedDisplayName;

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
                    new List<(Dictionary<string, int>, string, int)>();
            }
            ingredientCombinations[ingredientKey]
                .Add((quantities, recipe.createsItem.identifier, recipe.quantityCreated));
        }

        // 結果をフォーマット
        var results = new Dictionary<string, string>();
        foreach (var (ingKey, recipesList) in ingredientCombinations)
        {
            // 材料ごとの使用量の最小値と最大値を計算
            var ingredientRanges = new Dictionary<string, IngredientRange>();
            var resultRanges = new Dictionary<string, IngredientRange>();

            foreach (var (ingredients, result, quantity) in recipesList)
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
                    resultRanges[result] = new IngredientRange();
                }
                resultRanges[result].Min = Math.Min(resultRanges[result].Min, quantity);
                resultRanges[result].Max = Math.Max(resultRanges[result].Max, quantity);
            }

            // 結果を整形（LocalizedDisplayNameを使用）
            var ingredientsStr = ingredientRanges
                .Select(kvp => FormatRange(_displayNames[kvp.Key], kvp.Value))
                .ToList();

            foreach (var (resultName, ranges) in resultRanges)
            {
                var resultStr = FormatRange(_displayNames[resultName], ranges);
                var formula =
                    $"{string.Join("<size=-1><color=#6D8791> + </color></size>", ingredientsStr)}<size=-1><color=#6D8791> = </color></size>{resultStr}";
                results[ingKey] = formula;
            }
        }

        return results;
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
