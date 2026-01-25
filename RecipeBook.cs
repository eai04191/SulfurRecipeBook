#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using I2.Loc;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.Items;
using PerfectRandom.Sulfur.Core.Stats;
using PerfectRandom.Sulfur.Core.UI.Inventory;
using PerfectRandom.Sulfur.Core.UI.ItemDescription;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SulfurRecipeBook;

[HarmonyPatch]
internal class ReversePatch
{
    [HarmonyReversePatch]
    [HarmonyPatch(typeof(ItemDescription), "AddAttribute",
        new[] { typeof(string), typeof(int), typeof(string), typeof(string), typeof(bool) },
        new[] { ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal })]
    public static void AddTextLineAsAttribute(object instance, string key, ref int childIndex, string value = "", string modifier = "", bool skipSeparator = false) =>
        throw new NotImplementedException("It's a stub");

    [HarmonyReversePatch]
    [HarmonyPatch(typeof(ItemDescription), "AddDescriptionText",
        new[] { typeof(string), typeof(int) },
        new[] { ArgumentType.Normal, ArgumentType.Ref })]
    public static void AddTextLineAsDescription(object instance, string descriptionText, ref int childIndex) =>
        throw new NotImplementedException("It's a stub");
}

[HarmonyPatch]
internal class RecipeBook
{
    private static readonly List<RecipeData> AllRecipe = new Func<List<RecipeData>>(() =>
    {
        var rd = StaticInstance<AsyncAssetLoading>.Instance.recipeDatabase;
        return rd.enchantmentRecipes.Concat(rd.cookingRecipes).Where(r => r.canBeCrafted).ToList();
    })();

    private static bool _fontAtlasPopulationModeUpdated;
    private static int _currentPage;
    private static int _totalPages;
    private const int ItemsPerPage = 20;
    private static ItemDescription? _lastItemDescription;
    private static InventoryItem? _lastInventoryItem;
    private static Vector2? _originalSizeDelta;

    private static InventoryItem GetInventoryItem(ItemDescription instance)
    {
        return Traverse.Create(instance).Field("inventoryRepresentation").GetValue<InventoryItem>();
    }

    private static Transform GetContentTransform(ItemDescription instance)
    {
        return Traverse.Create(instance).Field("contentTransform").GetValue<Transform>();
    }

    private static List<RecipeData> FindRecipe(InventoryItem targetItem)
    {
        var result = AllRecipe
            .Where(recipe =>
                recipe.itemsNeeded.Any(ingredient =>
                    ingredient.item.value == targetItem.itemDefinition.id.value
                )
            )
            .ToList();

        return result;
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(InventoryUI), "Update")]
    private static void HandlePageNavigationInput()
    {
        var newPage = _currentPage;

        if (Input.GetKeyDown(Plugin.ConfigPrevPageKey.Value) && _currentPage > 0)
        {
            newPage = _currentPage - 1;
        }
        else if (Input.GetKeyDown(Plugin.ConfigNextPageKey.Value) && _currentPage < _totalPages - 1)
        {
            newPage = _currentPage + 1;
        }
        if (newPage == _currentPage) return;
        
        _currentPage = newPage;
        RefreshItemDescription();
        Plugin.Logger.LogInfo($"Switched to page {_currentPage}");
    }

    private static void RefreshItemDescription()
    {
        if (_lastItemDescription is null) return;
        var inventoryItem = GetInventoryItem(_lastItemDescription);

        // Trigger OnDisable to clear pooled UI elements
        _lastItemDescription.gameObject.SetActive(false);
        _lastItemDescription.gameObject.SetActive(true);
        _lastItemDescription.Setup(inventoryItem);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemDescription), nameof(ItemDescription.Setup), new Type[] { typeof(InventoryItem) })]
    private static void DescriptionSetupPostfix(ItemDescription __instance, InventoryItem inventoryItem)
    {
        var rectTransform = __instance.gameObject.GetComponent<RectTransform>();

        // Save original sizeDelta on first call
        _originalSizeDelta ??= rectTransform.sizeDelta;

        // Reset sizeDelta to original
        rectTransform.sizeDelta = _originalSizeDelta.Value;

        // Reset page when viewing a different item
        if (_lastInventoryItem != inventoryItem)
        {
            _currentPage = 0;
        }

        _lastItemDescription = __instance;
        _lastInventoryItem = inventoryItem;

        var recipes = FindRecipe(inventoryItem);
        if (recipes.Count == 0)
        {
            // Plugin.Logger.LogInfo("No recipe found for this item.");
            return;
        }
        
        var results = RecipeAnalyzer.AnalyzeRecipes(recipes);
        Plugin.Logger.LogInfo($"Found {results.Count} recipes for this item.");
        _totalPages = (int)Math.Ceiling((double)results.Count / ItemsPerPage);
        var currentPageItems = results.Skip(_currentPage * ItemsPerPage).Take(ItemsPerPage).ToList();
        
        var contentTransform = GetContentTransform(__instance);

        // Set font atlasPopulationMode to Static before adding text
        // to disable dynamic character addition and improve performance
        if (!_fontAtlasPopulationModeUpdated)
        {
            var existingText = contentTransform.GetComponentInChildren<TextMeshProUGUI>();
            if (existingText is not null)
            {
                existingText.font.atlasPopulationMode = AtlasPopulationMode.Static;
                _fontAtlasPopulationModeUpdated = true;
            }
        }

        var childIndex = contentTransform.childCount;
        ReversePatch.AddTextLineAsAttribute(__instance, $"Recipes ({_currentPage + 1} / {_totalPages}):", ref childIndex);

        foreach (var recipe in currentPageItems)
        {
            ReversePatch.AddTextLineAsDescription(__instance, recipe.Value, ref childIndex);
        }

        // Extend width for recipe list
        __instance.gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 100);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemDescription), "AddDescriptionText")]
    static void AddDescriptionTextPostfix(ItemDescription __instance, string descriptionText)
    {
        // Show heal efficiency for healing items
        if (Plugin.ConfigShowHealPerSlot.Value == false)
        {
            return;
        }

        var item = GetInventoryItem(__instance);

        if (
            item.itemDefinition.buffsOnConsume.Find(b =>
                b.attributeId == EntityAttributes.Stat_HealthRegen
            ) == null
        )
        {
            return;
        }

        foreach (var buff in item.itemDefinition.buffsOnConsume)
        {
            if (buff.attributeId != EntityAttributes.Stat_HealthRegen)
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
