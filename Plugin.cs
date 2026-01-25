using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SulfurRecipeBook;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    public static ConfigEntry<bool> ConfigShowHealPerSlot;
    public static ConfigEntry<KeyCode> ConfigPrevPageKey;
    public static ConfigEntry<KeyCode> ConfigNextPageKey;

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        // Config
        ConfigShowHealPerSlot = Config.Bind(
            "General",
            "ShowHealPerSlot",
            false,
            "Show heal per slot in item description"
        );

        ConfigPrevPageKey = Config.Bind(
            "Keybinds",
            "PrevPageKey",
            KeyCode.F1,
            "Key to go to previous recipe page"
        );

        ConfigNextPageKey = Config.Bind(
            "Keybinds",
            "NextPageKey",
            KeyCode.F2,
            "Key to go to next recipe page"
        );

        // Harmony patching
        Harmony.CreateAndPatchAll(typeof(RecipeBook));
        Harmony.CreateAndPatchAll(typeof(ReversePatch));
    }
}
