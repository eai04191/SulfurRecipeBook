using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.Items;
using PerfectRandom.Sulfur.Core.UI.ItemDescription;
using UnityEngine;
using UnityEngine.UI;

namespace SulfurRecipeBook;

[HarmonyPatch]
internal class ReversePatch
{
    [HarmonyReversePatch]
    [HarmonyPatch(typeof(ItemDescription), "AddAttribute", typeof(string))]
    public static void AddTextLineAsAttribute(object instance, string key) =>
        throw new NotImplementedException("It's a stub");

    [HarmonyReversePatch]
    [HarmonyPatch(typeof(ItemDescription), "AddDescriptionText", typeof(string))]
    public static void AddTextLineAsDescription(object instance, string descriptionText) =>
        throw new NotImplementedException("It's a stub");
}

[HarmonyPatch]
internal class RecipeBook
{
    private static readonly List<CraftingRecipe> AllRecipe = new Func<List<CraftingRecipe>>(() =>
    {
        var cm = Singleton<CraftingManager>.Instance;
        return cm.genericRecipes.Concat(cm.cookingRecipes).Where(r => r.canBeCrafted).ToList();
    })();

    private static readonly RecipeAnalyzer Analyzer = new();

    private static List<CraftingRecipe> FindRecipe(InventoryItem targetItem)
    {
        var result = AllRecipe
            .Where(recipe =>
                recipe.itemsNeeded.Any(ingredient =>
                    ingredient.item.identifier == targetItem.itemDefinition.identifier
                )
            )
            .ToList();

        return result;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemDescription), nameof(ItemDescription.Setup))]
    static void DescriptionSetupPostfix(ItemDescription __instance, InventoryItem inventoryItem)
    {
        Plugin.Logger.LogInfo($"I: {inventoryItem.itemDefinition.LocalizedDisplayName}");
        var recipes = FindRecipe(inventoryItem);
        if (recipes.Count == 0)
        {
            // Plugin.Logger.LogInfo("No recipe found for this item.");
            return;
        }

        ReversePatch.AddTextLineAsAttribute(__instance, "Recipes:");

        var results = Analyzer.AnalyzeRecipes(recipes);

        foreach (var recipe in results.Values)
        {
            ReversePatch.AddTextLineAsDescription(__instance, recipe);
            UpdatePadding(__instance);
        }

        var width = results.Count > 20 ? 500 : 350;
        __instance.gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 100);
    }

    private static void UpdatePadding(ItemDescription instance)
    {
        var contents = instance.transform.GetChild(0);
        var lastChild = contents.GetChild(contents.childCount - 1);
        var layoutGroup = lastChild.GetComponentInChildren<HorizontalLayoutGroup>();
        if (layoutGroup == null)
        {
            Plugin.Logger.LogInfo("No HorizontalLayoutGroup found.");
            return;
        }
        layoutGroup.padding.bottom = 0;
    }
}
