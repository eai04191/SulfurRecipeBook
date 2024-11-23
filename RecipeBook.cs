using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using I2.Loc;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.Items;
using PerfectRandom.Sulfur.Core.Stats;
using PerfectRandom.Sulfur.Core.UI.ItemDescription;
using TMPro;
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
        // Plugin.Logger.LogInfo($"I: {inventoryItem.itemDefinition.LocalizedDisplayName}");
        var recipes = FindRecipe(inventoryItem);
        if (recipes.Count == 0)
        {
            // Plugin.Logger.LogInfo("No recipe found for this item.");
            return;
        }

        ReversePatch.AddTextLineAsAttribute(__instance, "Recipes:");

        var results = RecipeAnalyzer.AnalyzeRecipes(recipes);

        foreach (var recipe in results.Values)
        {
            ReversePatch.AddTextLineAsDescription(__instance, recipe);
            UpdateLatestHorizontalLayoutGroupPadding(__instance, new RectOffset(8, 2, 2, 0));
        }

        var width = results.Count > 20 ? 500 : 350;
        __instance.gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 100);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemDescription), "AddDescriptionText")]
    static void AddDescriptionTextPostfix(ItemDescription __instance, string descriptionText)
    {
        // ヒールアイテムのdescriptionの場合回復効率を表示
        if (Plugin.ConfigShowHealPerSlot.Value == false)
        {
            return;
        }

        var item = Traverse
            .Create(__instance)
            .Field("inventoryRepresentation")
            .GetValue<InventoryItem>();

        if (
            item.itemDefinition.buffsOnConsume.Find(b =>
                b.attributeNew.id == EntityAttributes.Stat_HealthRegen
            ) == null
        )
        {
            return;
        }

        foreach (var buff in item.itemDefinition.buffsOnConsume)
        {
            if (buff.attributeNew.id != EntityAttributes.Stat_HealthRegen)
            {
                return;
            }

            var rawTranslation = LocalizationManager.GetTranslation(
                "ItemDescriptions/DynamicString_HealthConsumable",
                true,
                0,
                true,
                false,
                null,
                null,
                true
            );
            var translation = rawTranslation
                .Replace("VALUE_X", buff.totalValueOverride.ToString())
                .Replace("DURATION_X", buff.duration.ToString());

            if (translation != descriptionText)
            {
                return;
            }

            var hps = RecipeAnalyzer.CalculateHealPerSlot(item.itemDefinition);
            var newDescription = $"{descriptionText} [Heal per Slot: {hps}]";
            UpdateLatestDescription(__instance, newDescription);
        }
    }

    private static void UpdateLatestHorizontalLayoutGroupPadding(
        ItemDescription instance,
        RectOffset padding
    )
    {
        var contents = instance.transform.GetChild(0);
        var lastChild = contents.GetChild(contents.childCount - 1);
        var layoutGroup = lastChild.GetComponentInChildren<HorizontalLayoutGroup>();
        if (layoutGroup == null)
        {
            Plugin.Logger.LogInfo("No HorizontalLayoutGroup found.");
            return;
        }

        layoutGroup.padding = padding;
    }

    private static void UpdateLatestDescription(ItemDescription instance, string newDescription)
    {
        var contents = instance.transform.GetChild(0);
        var lastChild = contents.GetChild(contents.childCount - 1);
        var tm = lastChild.GetComponentInChildren<TextMeshProUGUI>();
        if (tm == null)
        {
            Plugin.Logger.LogInfo("No TextMeshProUGUI found.");
            return;
        }

        tm.text = newDescription;
    }
}
